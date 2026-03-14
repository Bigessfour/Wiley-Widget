using System;
using System.Collections.Generic;

namespace WileyWidget.WinForms.Configuration;

public static class AuthenticationModes
{
    public const string DevelopmentBypass = "DevelopmentBypass";
    public const string LocalIdentity = "LocalIdentity";
}

/// <summary>
/// Strongly-typed representation of the Authentication section in appsettings.
/// </summary>
public sealed class AuthenticationOptions
{
    public string Mode { get; set; } = AuthenticationModes.DevelopmentBypass;

    public AuthenticationEnvironmentOptions Environment { get; set; } = new();

    public DevelopmentBypassAuthenticationOptions DevelopmentBypass { get; set; } = new();

    public LocalIdentityAuthenticationOptions LocalIdentity { get; set; } = new();

    public UserOnboardingOptions Onboarding { get; set; } = new();

    public bool IsDevelopmentBypassMode =>
        string.Equals(Mode, AuthenticationModes.DevelopmentBypass, StringComparison.OrdinalIgnoreCase);

    public bool IsLocalIdentityMode =>
        string.Equals(Mode, AuthenticationModes.LocalIdentity, StringComparison.OrdinalIgnoreCase);
}

public sealed class AuthenticationEnvironmentOptions
{
    public bool EnableModeOverride { get; set; } = true;

    public string DevelopmentMode { get; set; } = AuthenticationModes.DevelopmentBypass;

    public string NonDevelopmentMode { get; set; } = AuthenticationModes.LocalIdentity;

    public string ForceDevelopmentBypassFlag { get; set; } = "WILEYWIDGET_AUTH_FORCE_DEVELOPMENT_BYPASS";

    public string ForceLocalIdentityFlag { get; set; } = "WILEYWIDGET_AUTH_FORCE_LOCAL_IDENTITY";
}

public sealed class DevelopmentBypassAuthenticationOptions
{
    public string UserId { get; set; } = "dev-user";

    public string DisplayName { get; set; } = "Development User";

    public string Email { get; set; } = "developer@local";
}

public sealed class LocalIdentityAuthenticationOptions
{
    public bool Enabled { get; set; } = true;

    public bool RequireConfirmedAccount { get; set; }

    public bool RequireConfirmedEmail { get; set; }

    public bool RequireConfirmedPhoneNumber { get; set; }

    public bool AllowPersistentRememberMe { get; set; } = true;

    public bool RememberMeDefaultSelection { get; set; }

    public int RememberMeDurationDays { get; set; } = 14;

    public string RememberMeVaultKey { get; set; } = "WILEYWIDGET_AUTH_LOCAL_IDENTITY_REMEMBER_ME";

    public bool AllowInitialAdminRegistration { get; set; } = true;

    public string SeedAdminUserName { get; set; } = "admin";

    public string SeedAdminDisplayName { get; set; } = "Administrator";

    public string SeedAdminEmail { get; set; } = "admin@local";

    public string BootstrapPassword { get; set; } = string.Empty;

    public string BootstrapPasswordEnvironmentVariable { get; set; } = "WILEYWIDGET_BOOTSTRAP_ADMIN_PASSWORD";
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
