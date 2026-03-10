using System;
using System.Collections.Generic;

namespace WileyWidget.WinForms.Configuration;

public static class AuthenticationModes
{
    public const string DevelopmentBypass = "DevelopmentBypass";
    public const string ExternalId = "ExternalId";
}

public static class ExternalIdRedirectUris
{
    // Microsoft guidance: embedded browser desktop apps should use nativeclient redirect URI.
    public const string EmbeddedNativeClient = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    // Microsoft guidance: system browser desktop apps should use localhost redirect URI.
    public const string SystemBrowserLocalhost = "http://localhost";
}

/// <summary>
/// Strongly-typed representation of the Authentication section in appsettings.
/// </summary>
public sealed class AuthenticationOptions
{
    public string Mode { get; set; } = AuthenticationModes.DevelopmentBypass;

    public DevelopmentBypassAuthenticationOptions DevelopmentBypass { get; set; } = new();

    public ExternalIdAuthenticationOptions ExternalId { get; set; } = new();

    public UserOnboardingOptions Onboarding { get; set; } = new();

    public bool IsDevelopmentBypassMode =>
        string.Equals(Mode, AuthenticationModes.DevelopmentBypass, StringComparison.OrdinalIgnoreCase);

    public bool IsExternalIdMode =>
        string.Equals(Mode, AuthenticationModes.ExternalId, StringComparison.OrdinalIgnoreCase);
}

public sealed class DevelopmentBypassAuthenticationOptions
{
    public string UserId { get; set; } = "dev-user";

    public string DisplayName { get; set; } = "Development User";

    public string Email { get; set; } = "developer@local";
}

public sealed class ExternalIdAuthenticationOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = ExternalIdRedirectUris.EmbeddedNativeClient;

    public bool UseEmbeddedWebView { get; set; } = true;

    public List<string> Scopes { get; set; } = new() { "openid", "profile", "email", "offline_access" };
}

public sealed class UserOnboardingOptions
{
    public bool Enabled { get; set; } = true;

    public bool RequireTermsConsent { get; set; } = true;

    public bool RequirePrivacyConsent { get; set; } = true;

    public bool EnablePhoneForSmsMfa { get; set; } = true;

    // Optional hard requirement for phone data if SMS MFA enrollment is enforced.
    public bool RequirePhoneForSmsMfaEnrollment { get; set; }

    // Consent policy versions tracked in stored user facts for auditability.
    public string TermsConsentVersion { get; set; } = "2026-03";

    public string PrivacyConsentVersion { get; set; } = "2026-03";

    // For local development only: auto-mark consent as accepted to avoid blocking workflows.
    public bool AutoGrantConsentInDevelopmentBypass { get; set; }

    // Keep the initial required set minimal to reduce sign-up friction.
    public List<string> RequiredProfileFields { get; set; } = new()
    {
        "DisplayName",
        "Email"
    };

    // Progressive profile collection fields for later prompts.
    public List<string> DeferredProfileFields { get; set; } = new()
    {
        "GivenName",
        "Surname",
        "PhoneNumber",
        "StreetAddress",
        "City",
        "State",
        "PostalCode",
        "Country"
    };
}
