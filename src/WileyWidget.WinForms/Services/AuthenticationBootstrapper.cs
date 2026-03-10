using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System.Text;
using System.Text.Json;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Establishes the runtime user identity for Wiley Widget.
/// Supports development bypass and Microsoft Entra External ID browser-based sign-in.
/// </summary>
public sealed class AuthenticationBootstrapper : IAuthenticationBootstrapper
{
    private readonly AuthenticationOptions _options;
    private readonly IUserContext _userContext;
    private readonly IUserOnboardingProfileService _userOnboardingProfileService;
    private readonly ILogger<AuthenticationBootstrapper> _logger;
    private AuthenticationSessionResult? _currentSession;
    private IPublicClientApplication? _publicClientApp;

    public AuthenticationBootstrapper(
        IOptions<AuthenticationOptions> options,
        IUserContext userContext,
        IUserOnboardingProfileService userOnboardingProfileService,
        ILogger<AuthenticationBootstrapper> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _userOnboardingProfileService = userOnboardingProfileService ?? throw new ArgumentNullException(nameof(userOnboardingProfileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AuthenticationSessionResult? CurrentSession => _currentSession;

    public async Task<AuthenticationSessionResult> EnsureAuthenticatedAsync(IWin32Window? ownerWindow, CancellationToken cancellationToken = default)
    {
        if (_currentSession != null)
        {
            return _currentSession;
        }

        if (_options.IsDevelopmentBypassMode)
        {
            var session = CreateDevelopmentBypassSession();
            ApplySession(session);
            await InitializeOnboardingProfileAsync(session, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Authentication mode DevelopmentBypass active. User={UserId}", session.UserId);
            return session;
        }

        if (!_options.IsExternalIdMode)
        {
            throw new InvalidOperationException($"Unsupported authentication mode '{_options.Mode}'.");
        }

        var sessionResult = await AuthenticateWithExternalIdAsync(ownerWindow, cancellationToken).ConfigureAwait(false);
        ApplySession(sessionResult);
        await InitializeOnboardingProfileAsync(sessionResult, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Authentication succeeded. User={UserId}, Provider={Provider}", sessionResult.UserId, sessionResult.Provider);
        return sessionResult;
    }

    private AuthenticationSessionResult CreateDevelopmentBypassSession()
    {
        var configuredUserId = string.IsNullOrWhiteSpace(_options.DevelopmentBypass.UserId)
            ? Environment.UserName
            : _options.DevelopmentBypass.UserId;

        var configuredDisplayName = string.IsNullOrWhiteSpace(_options.DevelopmentBypass.DisplayName)
            ? Environment.UserName
            : _options.DevelopmentBypass.DisplayName;

        var configuredEmail = string.IsNullOrWhiteSpace(_options.DevelopmentBypass.Email)
            ? null
            : _options.DevelopmentBypass.Email;

        return new AuthenticationSessionResult(
            UserId: configuredUserId,
            DisplayName: configuredDisplayName,
            Email: configuredEmail,
            Provider: AuthenticationModes.DevelopmentBypass,
            IsDevelopmentBypass: true,
            ProfileFields: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["DisplayName"] = configuredDisplayName,
                ["Email"] = configuredEmail
            });
    }

    private async Task<AuthenticationSessionResult> AuthenticateWithExternalIdAsync(IWin32Window? ownerWindow, CancellationToken cancellationToken)
    {
        if (!_options.ExternalId.Enabled)
        {
            throw new InvalidOperationException("Authentication:ExternalId:Enabled must be true when Mode is ExternalId.");
        }

        if (string.IsNullOrWhiteSpace(_options.ExternalId.ClientId))
        {
            throw new InvalidOperationException("Authentication:ExternalId:ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.ExternalId.Authority))
        {
            throw new InvalidOperationException("Authentication:ExternalId:Authority is required.");
        }

        if (!Uri.TryCreate(_options.ExternalId.Authority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException("Authentication:ExternalId:Authority must be an absolute URI.");
        }

        var redirectUri = NormalizeRedirectUri(
            _options.ExternalId.RedirectUri,
            _options.ExternalId.UseEmbeddedWebView);

        WarnIfEmbeddedB2cSocialMayFail(authorityUri, _options.ExternalId.UseEmbeddedWebView);

        if (_publicClientApp == null)
        {
            _publicClientApp = PublicClientApplicationBuilder
                .Create(_options.ExternalId.ClientId)
                .WithAuthority(authorityUri)
                .WithRedirectUri(redirectUri)
                .Build();
        }

        var scopes = _options.ExternalId.Scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (scopes.Length == 0)
        {
            throw new InvalidOperationException("Authentication:ExternalId:Scopes must contain at least one scope.");
        }

        var accounts = await _publicClientApp.GetAccountsAsync().ConfigureAwait(false);
        var preferredAccount = accounts.FirstOrDefault();

        try
        {
            if (preferredAccount != null)
            {
                var silentResult = await _publicClientApp
                    .AcquireTokenSilent(scopes, preferredAccount)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);

                return BuildSession(silentResult, provider: "ExternalId-Silent");
            }
        }
        catch (MsalUiRequiredException)
        {
            _logger.LogInformation("Silent sign-in is unavailable; interactive sign-in required.");
        }

        var interactiveRequest = _publicClientApp
            .AcquireTokenInteractive(scopes)
            .WithUseEmbeddedWebView(_options.ExternalId.UseEmbeddedWebView);

        if (ownerWindow != null && ownerWindow.Handle != IntPtr.Zero)
        {
            interactiveRequest = interactiveRequest.WithParentActivityOrWindow(ownerWindow.Handle);
        }

        var interactiveResult = await interactiveRequest
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildSession(interactiveResult, provider: "ExternalId-Interactive");
    }

    private static AuthenticationSessionResult BuildSession(AuthenticationResult result, string provider)
    {
        var account = result.Account;
        var profileFields = ExtractProfileFieldsFromIdToken(result.IdToken);

        var userId = account?.HomeAccountId?.Identifier;
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = account?.Username;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "external-user";
        }

        var displayName = GetValue(profileFields, "DisplayName")
            ?? account?.Username;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = userId;
        }

        var email = GetValue(profileFields, "Email")
            ?? account?.Username;

        if (string.IsNullOrWhiteSpace(GetValue(profileFields, "DisplayName")))
        {
            profileFields["DisplayName"] = displayName;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            profileFields["Email"] = email;
        }

        return new AuthenticationSessionResult(
            UserId: userId,
            DisplayName: displayName,
            Email: email,
            Provider: provider,
            IsDevelopmentBypass: false,
            ProfileFields: profileFields);
    }

    private void ApplySession(AuthenticationSessionResult session)
    {
        _currentSession = session;
        _userContext.SetCurrentUser(session.UserId, session.DisplayName);
    }

    private async Task InitializeOnboardingProfileAsync(AuthenticationSessionResult session, CancellationToken cancellationToken)
    {
        if (!_options.Onboarding.Enabled)
        {
            return;
        }

        try
        {
            var requiredFields = _options.Onboarding.RequiredProfileFields.Count > 0
                ? _options.Onboarding.RequiredProfileFields
                : new List<string> { "DisplayName", "Email" };

            var deferredFields = _options.Onboarding.DeferredProfileFields.Count > 0
                ? _options.Onboarding.DeferredProfileFields
                : new List<string> { "GivenName", "Surname", "PhoneNumber" };

            var autoGrantConsent = session.IsDevelopmentBypass && _options.Onboarding.AutoGrantConsentInDevelopmentBypass;

            var result = await _userOnboardingProfileService
                .EnsureInitializedAsync(
                    new UserOnboardingInitializationRequest(
                        UserId: session.UserId,
                        DisplayName: session.DisplayName,
                        Email: session.Email,
                        AuthenticationProvider: session.Provider,
                        RequiredProfileFields: requiredFields,
                        DeferredProfileFields: deferredFields,
                        RequireTermsConsent: _options.Onboarding.RequireTermsConsent,
                        RequirePrivacyConsent: _options.Onboarding.RequirePrivacyConsent,
                        EnablePhoneForSmsMfa: _options.Onboarding.EnablePhoneForSmsMfa,
                        RequirePhoneForSmsMfaEnrollment: _options.Onboarding.RequirePhoneForSmsMfaEnrollment,
                        TermsConsentGranted: autoGrantConsent ? true : null,
                        PrivacyConsentGranted: autoGrantConsent ? true : null,
                        TermsConsentVersion: _options.Onboarding.TermsConsentVersion,
                        PrivacyConsentVersion: _options.Onboarding.PrivacyConsentVersion,
                        ProfileFields: session.ProfileFields,
                        Source: session.IsDevelopmentBypass ? "development-bypass" : "external-id"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsComplete)
            {
                _logger.LogInformation(
                    "User onboarding profile is incomplete for {UserId}. Missing fields: {MissingFields}",
                    session.UserId,
                    string.Join(", ", result.MissingRequiredFields));
            }

            if (result.TermsConsentPending || result.PrivacyConsentPending)
            {
                _logger.LogInformation(
                    "User consent pending for {UserId}. TermsPending={TermsPending}, PrivacyPending={PrivacyPending}",
                    session.UserId,
                    result.TermsConsentPending,
                    result.PrivacyConsentPending);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize onboarding profile for user {UserId}", session.UserId);
        }
    }

    private static Dictionary<string, string?> ExtractProfileFieldsFromIdToken(string? idToken)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return result;
        }

        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return result;
        }

        try
        {
            var payloadBytes = DecodeBase64Url(parts[1]);
            using var document = JsonDocument.Parse(payloadBytes);
            var root = document.RootElement;

            AddMappedClaim(root, "name", "DisplayName", result);
            AddMappedClaim(root, "given_name", "GivenName", result);
            AddMappedClaim(root, "family_name", "Surname", result);
            AddMappedClaim(root, "email", "Email", result);
            AddMappedClaim(root, "preferred_username", "Email", result);
            AddMappedClaim(root, "phone_number", "PhoneNumber", result);

            if (root.TryGetProperty("address", out var address) && address.ValueKind == JsonValueKind.Object)
            {
                AddMappedClaim(address, "street_address", "StreetAddress", result);
                AddMappedClaim(address, "locality", "City", result);
                AddMappedClaim(address, "region", "State", result);
                AddMappedClaim(address, "postal_code", "PostalCode", result);
                AddMappedClaim(address, "country", "Country", result);
            }
        }
        catch
        {
            // Intentionally ignore malformed token parsing and continue with account defaults.
        }

        return result;
    }

    private static void AddMappedClaim(JsonElement source, string claimName, string fieldName, IDictionary<string, string?> destination)
    {
        if (!source.TryGetProperty(claimName, out var valueElement) || valueElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = valueElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        destination[fieldName] = value.Trim();
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        return Convert.FromBase64String(normalized);
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private string NormalizeRedirectUri(string? configuredRedirectUri, bool useEmbeddedWebView)
    {
        if (!string.IsNullOrWhiteSpace(configuredRedirectUri))
        {
            if (!Uri.TryCreate(configuredRedirectUri, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException("Authentication:ExternalId:RedirectUri must be an absolute URI.");
            }

            if (useEmbeddedWebView &&
                configuredRedirectUri.StartsWith(ExternalIdRedirectUris.SystemBrowserLocalhost, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "RedirectUri '{RedirectUri}' is configured for system browser while UseEmbeddedWebView=true. " +
                    "Switching to '{ExpectedRedirectUri}'.",
                    configuredRedirectUri,
                    ExternalIdRedirectUris.EmbeddedNativeClient);

                return ExternalIdRedirectUris.EmbeddedNativeClient;
            }

            if (!useEmbeddedWebView &&
                configuredRedirectUri.StartsWith(ExternalIdRedirectUris.EmbeddedNativeClient, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "RedirectUri '{RedirectUri}' is configured for embedded browser while UseEmbeddedWebView=false. " +
                    "Switching to '{ExpectedRedirectUri}'.",
                    configuredRedirectUri,
                    ExternalIdRedirectUris.SystemBrowserLocalhost);

                return ExternalIdRedirectUris.SystemBrowserLocalhost;
            }

            return configuredRedirectUri;
        }

        return useEmbeddedWebView
            ? ExternalIdRedirectUris.EmbeddedNativeClient
            : ExternalIdRedirectUris.SystemBrowserLocalhost;
    }

    private void WarnIfEmbeddedB2cSocialMayFail(Uri authorityUri, bool useEmbeddedWebView)
    {
        if (!useEmbeddedWebView)
        {
            return;
        }

        if (!authorityUri.Host.Contains("b2clogin.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogWarning(
            "Embedded WebView with B2C/b2clogin.com may fail for Google social sign-in. " +
            "Microsoft guidance recommends system browser for that scenario.");
    }
}
