using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyWidget.Data;
using WileyWidget.Models.Entities;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Establishes the runtime user identity for Wiley Widget.
/// Supports development bypass and local ASP.NET Core Identity sign-in.
/// </summary>
public sealed class AuthenticationBootstrapper : IAuthenticationBootstrapper
    , IDisposable
{
    private sealed record PersistedLocalIdentitySessionRecord(
        string UserId,
        string SecurityStamp,
        DateTimeOffset ExpiresUtc,
        string EnvironmentName);

    private readonly AuthenticationOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ISecretVaultService? _secretVaultService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly SignInManager<AppIdentityUser> _signInManager;
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly IUserContext _userContext;
    private readonly IUserOnboardingProfileService _userOnboardingProfileService;
    private readonly ILogger<AuthenticationBootstrapper> _logger;
    private readonly SemaphoreSlim _identityStoreInitializationLock = new(1, 1);
    private AuthenticationSessionResult? _currentSession;
    private volatile bool _identityStoreReady;

    public AuthenticationBootstrapper(
        IOptions<AuthenticationOptions> options,
        IHostEnvironment hostEnvironment,
        ISecretVaultService? secretVaultService,
        IDbContextFactory<AppDbContext> dbContextFactory,
        SignInManager<AppIdentityUser> signInManager,
        UserManager<AppIdentityUser> userManager,
        IUserContext userContext,
        IUserOnboardingProfileService userOnboardingProfileService,
        ILogger<AuthenticationBootstrapper> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _secretVaultService = secretVaultService;
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _userOnboardingProfileService = userOnboardingProfileService ?? throw new ArgumentNullException(nameof(userOnboardingProfileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation(
            "Authentication bootstrapper initialized. Environment={EnvironmentName}, Mode={AuthenticationMode}, RememberMeEnabled={RememberMeEnabled}",
            _hostEnvironment.EnvironmentName,
            _options.Mode,
            SupportsPersistentRememberMe);
    }

    public AuthenticationSessionResult? CurrentSession => _currentSession;

    public bool IsHostedLocalIdentityMode =>
        _currentSession == null
        && _options.IsLocalIdentityMode
        && _options.LocalIdentity.Enabled;

    public bool SupportsPersistentRememberMe =>
        _options.LocalIdentity.AllowPersistentRememberMe
        && _secretVaultService != null;

    public bool DefaultRememberMeSelection =>
        SupportsPersistentRememberMe
        && _options.LocalIdentity.RememberMeDefaultSelection;

    public async Task<AuthenticationSessionResult> EnsureAuthenticatedAsync(IWin32Window? ownerWindow, CancellationToken cancellationToken = default)
    {
        if (_currentSession != null)
        {
            return _currentSession;
        }

        if (_options.IsDevelopmentBypassMode)
        {
            return await ActivateAuthenticatedSessionAsync(
                CreateDevelopmentBypassSession(),
                cancellationToken,
                "Authentication mode DevelopmentBypass active. User={UserId}").ConfigureAwait(false);
        }

        if (!_options.IsLocalIdentityMode)
        {
            throw new InvalidOperationException($"Unsupported authentication mode '{_options.Mode}'.");
        }

        var rememberedSession = await TryRestoreRememberedSessionAsync(cancellationToken).ConfigureAwait(false);
        if (rememberedSession != null)
        {
            return rememberedSession;
        }

        var localSession = await AuthenticateWithLocalIdentityAsync(ownerWindow, cancellationToken).ConfigureAwait(false);
        return await ActivateAuthenticatedSessionAsync(
            localSession,
            cancellationToken,
            "Authentication succeeded. User={UserId}, Provider={Provider}").ConfigureAwait(false);
    }

    public async Task<AuthenticationSessionResult?> TryRestoreRememberedSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentSession != null)
        {
            return _currentSession;
        }

        if (!_options.IsLocalIdentityMode || !_options.LocalIdentity.Enabled || !SupportsPersistentRememberMe)
        {
            return null;
        }

        await EnsureIdentityStoreReadyAsync(cancellationToken).ConfigureAwait(false);

        var persistedPayload = await _secretVaultService!
            .GetSecretAsync(BuildRememberMeSecretKey(), cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(persistedPayload))
        {
            return null;
        }

        PersistedLocalIdentitySessionRecord? rememberedSession;
        try
        {
            rememberedSession = JsonSerializer.Deserialize<PersistedLocalIdentitySessionRecord>(persistedPayload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Remember-me session payload could not be parsed. Clearing stored session.");
            await ClearRememberedLocalIdentitySessionAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (rememberedSession == null
            || rememberedSession.ExpiresUtc <= DateTimeOffset.UtcNow
            || !string.Equals(rememberedSession.EnvironmentName, _hostEnvironment.EnvironmentName, StringComparison.OrdinalIgnoreCase))
        {
            await ClearRememberedLocalIdentitySessionAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var user = await _userManager.FindByIdAsync(rememberedSession.UserId).ConfigureAwait(false);
        if (user == null || !user.IsEnabled)
        {
            await ClearRememberedLocalIdentitySessionAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var currentSecurityStamp = await _userManager.GetSecurityStampAsync(user).ConfigureAwait(false);
        if (!string.Equals(currentSecurityStamp, rememberedSession.SecurityStamp, StringComparison.Ordinal))
        {
            await ClearRememberedLocalIdentitySessionAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        user.LastSignedInAtUtc = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            _logger.LogWarning(
                "Remember-me session restored for {UserName}, but updating LastSignedInAtUtc failed: {Errors}",
                user.UserName,
                BuildIdentityErrorMessage(updateResult));
        }

        var session = BuildLocalIdentitySession(user, AuthenticationModes.LocalIdentity);
        return await ActivateAuthenticatedSessionAsync(
            session,
            cancellationToken,
            "Remembered local identity session restored. User={UserId}, Provider={Provider}").ConfigureAwait(false);
    }

    public async Task<bool> HasHostedLocalIdentityUsersAsync(CancellationToken cancellationToken = default)
    {
        EnsureLocalIdentityModeEnabled();
        await EnsureSeededLocalIdentityUserAsync(cancellationToken).ConfigureAwait(false);
        return await HasAnyLocalIdentityUsersAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AuthenticationSessionResult> SignInHostedLocalIdentityAsync(
        string userNameOrEmail,
        string password,
        bool rememberMe = false,
        CancellationToken cancellationToken = default)
    {
        EnsureLocalIdentityModeEnabled();
        var session = await AuthenticateLocalUserCredentialsAsync(userNameOrEmail, password, cancellationToken).ConfigureAwait(false);
        var activatedSession = await ActivateAuthenticatedSessionAsync(
            session,
            cancellationToken,
            "Hosted local identity sign-in succeeded. User={UserId}, Provider={Provider}").ConfigureAwait(false);
        await UpdateRememberMePreferenceAsync(activatedSession, rememberMe, cancellationToken).ConfigureAwait(false);
        return activatedSession;
    }

    public async Task<AuthenticationSessionResult> RegisterHostedLocalIdentityAsync(
        LocalIdentityRegistrationRequest request,
        bool rememberMe = false,
        CancellationToken cancellationToken = default)
    {
        EnsureLocalIdentityModeEnabled();
        await EnsureSeededLocalIdentityUserAsync(cancellationToken).ConfigureAwait(false);
        var session = await RegisterInitialLocalIdentityUserAsync(request, cancellationToken).ConfigureAwait(false);
        var activatedSession = await ActivateAuthenticatedSessionAsync(
            session,
            cancellationToken,
            "Hosted local identity bootstrap succeeded. User={UserId}, Provider={Provider}").ConfigureAwait(false);
        await UpdateRememberMePreferenceAsync(activatedSession, rememberMe, cancellationToken).ConfigureAwait(false);
        return activatedSession;
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

    private async Task<AuthenticationSessionResult> AuthenticateWithLocalIdentityAsync(IWin32Window? ownerWindow, CancellationToken cancellationToken)
    {
        if (!_options.LocalIdentity.Enabled)
        {
            throw new InvalidOperationException("Authentication:LocalIdentity:Enabled must be true when Mode is LocalIdentity.");
        }

        await EnsureSeededLocalIdentityUserAsync(cancellationToken).ConfigureAwait(false);

        if (!await HasAnyLocalIdentityUsersAsync(cancellationToken).ConfigureAwait(false)
            && !_options.LocalIdentity.AllowInitialAdminRegistration)
        {
            throw new InvalidOperationException(
                "No local identity user exists. Enable initial administrator registration or configure a bootstrap admin password.");
        }

        _logger.LogError(
            "Interactive LocalIdentity sign-in was requested through {MethodName}, but the application now requires hosted authentication via MainForm. OwnerWindowProvided={OwnerWindowProvided}",
            nameof(EnsureAuthenticatedAsync),
            ownerWindow != null);

        throw new InvalidOperationException(
            "Interactive local identity sign-in must be hosted in the MainForm authentication panel. Start the application in hosted LocalIdentity mode or use the hosted sign-in APIs.");
    }

    private Task<bool> HasAnyLocalIdentityUsersAsync(CancellationToken cancellationToken)
    {
        return HasAnyLocalIdentityUsersCoreAsync(cancellationToken);
    }

    private async Task<bool> HasAnyLocalIdentityUsersCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureIdentityStoreReadyAsync(cancellationToken).ConfigureAwait(false);
        return await _userManager.Users.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AuthenticationSessionResult> RegisterInitialLocalIdentityUserAsync(
        LocalIdentityRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.LocalIdentity.AllowInitialAdminRegistration)
        {
            throw new InvalidOperationException("Initial administrator registration is disabled.");
        }

        if (await HasAnyLocalIdentityUsersAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A local account already exists. Sign in with that account instead.");
        }

        var user = new AppIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = true,
            IsEnabled = true,
            LockoutEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(BuildIdentityErrorMessage(createResult));
        }

        _logger.LogInformation("Created initial local administrator account {UserName} ({Email})", user.UserName, user.Email);
        return BuildLocalIdentitySession(user, provider: AuthenticationModes.LocalIdentity);
    }

    private async Task<AuthenticationSessionResult> AuthenticateLocalUserCredentialsAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIdentityStoreReadyAsync(cancellationToken).ConfigureAwait(false);

        var normalizedIdentity = userNameOrEmail.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIdentity) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Username/email and password are required.");
        }

        var user = await FindUserByIdentityAsync(normalizedIdentity).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogWarning("Local identity sign-in failed because the account was not found. Identity={Identity}", normalizedIdentity);
            throw new InvalidOperationException("Invalid username/email or password.");
        }

        if (!user.IsEnabled)
        {
            _logger.LogWarning("Local identity sign-in blocked because the account is disabled. UserId={UserId}", user.Id);
            throw new InvalidOperationException("This account is disabled.");
        }

        var signInResult = await _signInManager
            .CheckPasswordSignInAsync(user, password, lockoutOnFailure: _userManager.SupportsUserLockout)
            .ConfigureAwait(false);

        if (signInResult.Succeeded)
        {
            user.LastSignedInAtUtc = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning("User sign-in succeeded but profile update failed for {UserName}: {Errors}",
                    user.UserName,
                    BuildIdentityErrorMessage(updateResult));
            }

            return BuildLocalIdentitySession(user, provider: AuthenticationModes.LocalIdentity);
        }

        _logger.LogWarning(
            "Local identity sign-in failed. UserId={UserId} LockedOut={IsLockedOut} RequiresTwoFactor={RequiresTwoFactor} IsNotAllowed={IsNotAllowed}",
            user.Id,
            signInResult.IsLockedOut,
            signInResult.RequiresTwoFactor,
            signInResult.IsNotAllowed);

        throw new InvalidOperationException(
            await BuildInteractiveSignInFailureMessageAsync(user, signInResult).ConfigureAwait(false));
    }

    private async Task<string> BuildInteractiveSignInFailureMessageAsync(AppIdentityUser user, SignInResult signInResult)
    {
        if (signInResult.IsLockedOut)
        {
            if (_userManager.SupportsUserLockout)
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user).ConfigureAwait(false);
                if (lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow)
                {
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "This account is temporarily locked. Try again after {0:g}.",
                        lockoutEnd.Value.LocalDateTime);
                }
            }

            return "This account is temporarily locked due to repeated failed sign-in attempts.";
        }

        if (signInResult.RequiresTwoFactor)
        {
            return "This account requires two-factor authentication, which is not yet supported by the desktop sign-in flow.";
        }

        if (signInResult.IsNotAllowed)
        {
            if (_options.LocalIdentity.RequireConfirmedEmail && !user.EmailConfirmed)
            {
                return "This account must confirm its email address before it can sign in.";
            }

            if (_options.LocalIdentity.RequireConfirmedPhoneNumber && !user.PhoneNumberConfirmed)
            {
                return "This account must confirm its phone number before it can sign in.";
            }

            if (_options.LocalIdentity.RequireConfirmedAccount)
            {
                return "This account must complete account confirmation before it can sign in.";
            }

            return "This account is not permitted to sign in.";
        }

        return "Invalid username/email or password.";
    }

    private async Task<AppIdentityUser?> FindUserByIdentityAsync(string identity)
    {
        var byUserName = await _userManager.FindByNameAsync(identity).ConfigureAwait(false);
        if (byUserName != null)
        {
            return byUserName;
        }

        return identity.Contains('@', StringComparison.Ordinal)
            ? await _userManager.FindByEmailAsync(identity).ConfigureAwait(false)
            : null;
    }

    private async Task EnsureSeededLocalIdentityUserAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIdentityStoreReadyAsync(cancellationToken).ConfigureAwait(false);

        if (await HasAnyLocalIdentityUsersAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var bootstrapPassword = ResolveBootstrapPassword();
        if (string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            return;
        }

        var bootstrapUser = new AppIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = _options.LocalIdentity.SeedAdminUserName.Trim(),
            Email = _options.LocalIdentity.SeedAdminEmail.Trim(),
            DisplayName = _options.LocalIdentity.SeedAdminDisplayName.Trim(),
            EmailConfirmed = true,
            IsEnabled = true,
            LockoutEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(bootstrapUser, bootstrapPassword).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to seed the bootstrap local identity user: {BuildIdentityErrorMessage(createResult)}");
        }

        _logger.LogInformation(
            "Seeded bootstrap local identity account {UserName} from configured bootstrap password source.",
            bootstrapUser.UserName);
    }

    private string? ResolveBootstrapPassword()
    {
        if (!string.IsNullOrWhiteSpace(_options.LocalIdentity.BootstrapPassword))
        {
            return _options.LocalIdentity.BootstrapPassword;
        }

        if (string.IsNullOrWhiteSpace(_options.LocalIdentity.BootstrapPasswordEnvironmentVariable))
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(_options.LocalIdentity.BootstrapPasswordEnvironmentVariable);
    }

    private async Task EnsureIdentityStoreReadyAsync(CancellationToken cancellationToken)
    {
        if (_identityStoreReady)
        {
            return;
        }

        await _identityStoreInitializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_identityStoreReady)
            {
                return;
            }

            using var context = _dbContextFactory.CreateDbContext();
            if (context.Database.IsRelational())
            {
                _logger.LogInformation("Ensuring local identity store schema is available before authentication queries.");
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }

            _identityStoreReady = true;
        }
        finally
        {
            _identityStoreInitializationLock.Release();
        }
    }

    private static string BuildIdentityErrorMessage(IdentityResult result)
    {
        return string.Join(Environment.NewLine, result.Errors.Select(static error => error.Description));
    }

    private static AuthenticationSessionResult BuildLocalIdentitySession(AppIdentityUser user, string provider)
    {
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName ?? user.Email ?? user.Id.ToString()
            : user.DisplayName;

        var email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email;

        return new AuthenticationSessionResult(
            UserId: user.Id.ToString(),
            DisplayName: displayName,
            Email: email,
            Provider: provider,
            IsDevelopmentBypass: false,
            ProfileFields: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["DisplayName"] = displayName,
                ["Email"] = email,
                ["UserName"] = user.UserName
            });
    }

    private void ApplySession(AuthenticationSessionResult session)
    {
        _currentSession = session;
        _userContext.SetCurrentUser(session.UserId, session.DisplayName);
    }

    private void EnsureLocalIdentityModeEnabled()
    {
        if (!_options.IsLocalIdentityMode || !_options.LocalIdentity.Enabled)
        {
            throw new InvalidOperationException("Local identity authentication is not enabled for the current startup mode.");
        }
    }

    private async Task UpdateRememberMePreferenceAsync(
        AuthenticationSessionResult session,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        if (!SupportsPersistentRememberMe)
        {
            return;
        }

        if (!rememberMe)
        {
            await ClearRememberedLocalIdentitySessionAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var user = await _userManager.FindByIdAsync(session.UserId).ConfigureAwait(false);
        if (user == null)
        {
            await ClearRememberedLocalIdentitySessionAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var securityStamp = await _userManager.GetSecurityStampAsync(user).ConfigureAwait(false);
        var payload = JsonSerializer.Serialize(new PersistedLocalIdentitySessionRecord(
            UserId: user.Id.ToString(),
            SecurityStamp: securityStamp,
            ExpiresUtc: DateTimeOffset.UtcNow.AddDays(_options.LocalIdentity.RememberMeDurationDays),
            EnvironmentName: _hostEnvironment.EnvironmentName));

        await _secretVaultService!
            .SetSecretAsync(BuildRememberMeSecretKey(), payload, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task ClearRememberedLocalIdentitySessionAsync(CancellationToken cancellationToken)
    {
        if (_secretVaultService == null)
        {
            return Task.CompletedTask;
        }

        return _secretVaultService.DeleteSecretAsync(BuildRememberMeSecretKey(), cancellationToken);
    }

    public void Dispose()
    {
        _identityStoreInitializationLock.Dispose();
    }

    private string BuildRememberMeSecretKey()
    {
        return $"{_options.LocalIdentity.RememberMeVaultKey}_{_hostEnvironment.EnvironmentName}";
    }

    private async Task<AuthenticationSessionResult> ActivateAuthenticatedSessionAsync(
        AuthenticationSessionResult session,
        CancellationToken cancellationToken,
        string completionLogMessage)
    {
        ApplySession(session);
        await InitializeOnboardingProfileAsync(session, cancellationToken).ConfigureAwait(false);

        if (completionLogMessage.Contains("{Provider}", StringComparison.Ordinal))
        {
            _logger.LogInformation(completionLogMessage, session.UserId, session.Provider);
        }
        else
        {
            _logger.LogInformation(completionLogMessage, session.UserId);
        }

        return session;
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
                        Source: session.IsDevelopmentBypass ? "development-bypass" : "local-identity"),
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
}
