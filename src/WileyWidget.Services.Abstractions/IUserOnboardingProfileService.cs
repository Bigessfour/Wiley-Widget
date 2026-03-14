using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

public sealed record UserOnboardingInitializationRequest(
    string UserId,
    string? DisplayName,
    string? Email,
    string AuthenticationProvider,
    IReadOnlyCollection<string> RequiredProfileFields,
    IReadOnlyCollection<string> DeferredProfileFields,
    bool RequireTermsConsent,
    bool RequirePrivacyConsent,
    bool EnablePhoneForSmsMfa,
    bool RequirePhoneForSmsMfaEnrollment,
    bool? TermsConsentGranted,
    bool? PrivacyConsentGranted,
    string? TermsConsentVersion,
    string? PrivacyConsentVersion,
    IReadOnlyDictionary<string, string?>? ProfileFields = null,
    string Source = "authentication-bootstrap");

public sealed record UserOnboardingInitializationResult(
    bool IsComplete,
    IReadOnlyList<string> MissingRequiredFields,
    bool TermsConsentPending,
    bool PrivacyConsentPending,
    DateTime EvaluatedAtUtc);

public interface IUserOnboardingProfileService
{
    Task<UserOnboardingInitializationResult> EnsureInitializedAsync(
        UserOnboardingInitializationRequest request,
        CancellationToken cancellationToken = default);
}
