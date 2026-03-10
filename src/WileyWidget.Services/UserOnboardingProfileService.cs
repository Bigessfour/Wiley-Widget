using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Initializes and evaluates user onboarding/profile data using the persistent user-memory store.
/// Keeps first-login collection minimal while supporting progressive profile completion.
/// </summary>
public sealed class UserOnboardingProfileService : IUserOnboardingProfileService
{
    private const string ProfileDisplayNameKey = "UserProfile.DisplayName";
    private const string ProfileEmailKey = "UserProfile.Email";
    private const string ProfileGivenNameKey = "UserProfile.GivenName";
    private const string ProfileSurnameKey = "UserProfile.Surname";
    private const string ProfilePhoneNumberKey = "UserProfile.PhoneNumber";
    private const string ProfileStreetAddressKey = "UserProfile.StreetAddress";
    private const string ProfileCityKey = "UserProfile.City";
    private const string ProfileStateKey = "UserProfile.State";
    private const string ProfilePostalCodeKey = "UserProfile.PostalCode";
    private const string ProfileCountryKey = "UserProfile.Country";

    private const string ConsentTermsAcceptedKey = "Consent.TermsAccepted";
    private const string ConsentPrivacyAcceptedKey = "Consent.PrivacyAccepted";
    private const string ConsentTermsAcceptedAtUtcKey = "Consent.TermsAcceptedAtUtc";
    private const string ConsentPrivacyAcceptedAtUtcKey = "Consent.PrivacyAcceptedAtUtc";
    private const string ConsentTermsVersionKey = "Consent.TermsVersion";
    private const string ConsentPrivacyVersionKey = "Consent.PrivacyVersion";
    private const string ConsentTermsSourceKey = "Consent.TermsSource";
    private const string ConsentPrivacySourceKey = "Consent.PrivacySource";

    private const string SecuritySmsMfaPlannedKey = "Security.SmsMfaPlanned";
    private const string SecuritySmsMfaPhoneAvailableKey = "Security.SmsMfaPhoneAvailable";
    private const string AuthProviderKey = "Auth.Provider";
    private const string OnboardingSourceKey = "Onboarding.Source";
    private const string OnboardingLastEvaluatedAtUtcKey = "Onboarding.LastEvaluatedAtUtc";

    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly ILogger<UserOnboardingProfileService> _logger;

    public UserOnboardingProfileService(
        IUserMemoryRepository userMemoryRepository,
        ILogger<UserOnboardingProfileService> logger)
    {
        _userMemoryRepository = userMemoryRepository ?? throw new ArgumentNullException(nameof(userMemoryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserOnboardingInitializationResult> EnsureInitializedAsync(
        UserOnboardingInitializationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new ArgumentException("UserId is required.", nameof(request));
        }

        var evaluatedAtUtc = DateTime.UtcNow;
        var existingFacts = await _userMemoryRepository
            .GetFactsForUserAsync(request.UserId, take: 512, cancellationToken)
            .ConfigureAwait(false);

        var byKey = existingFacts
            .GroupBy(item => item.FactKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAtUtc).First().FactValue, StringComparer.OrdinalIgnoreCase);

        await UpsertIfPresentAsync(request.UserId, ProfileDisplayNameKey, request.DisplayName, 0.98, cancellationToken).ConfigureAwait(false);
        await UpsertIfPresentAsync(request.UserId, ProfileEmailKey, request.Email, 0.98, cancellationToken).ConfigureAwait(false);
        await UpsertIfPresentAsync(request.UserId, AuthProviderKey, request.AuthenticationProvider, 0.99, cancellationToken).ConfigureAwait(false);
        await UpsertIfPresentAsync(request.UserId, OnboardingSourceKey, request.Source, 0.95, cancellationToken).ConfigureAwait(false);
        await UpsertIfPresentAsync(
            request.UserId,
            OnboardingLastEvaluatedAtUtcKey,
            evaluatedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            0.95,
            cancellationToken).ConfigureAwait(false);

        if (request.ProfileFields != null)
        {
            foreach (var item in request.ProfileFields)
            {
                var mappedKey = MapFieldToFactKey(item.Key);
                if (string.IsNullOrWhiteSpace(mappedKey))
                {
                    continue;
                }

                await UpsertIfPresentAsync(request.UserId, mappedKey, item.Value, 0.9, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    byKey[mappedKey] = item.Value.Trim();
                }
            }
        }

        if (request.EnablePhoneForSmsMfa)
        {
            await UpsertIfMissingAsync(request.UserId, SecuritySmsMfaPlannedKey, "true", 0.95, byKey, cancellationToken).ConfigureAwait(false);
            byKey[SecuritySmsMfaPlannedKey] = "true";

            var hasPhoneNumber = byKey.TryGetValue(ProfilePhoneNumberKey, out var phoneNumber)
                && !string.IsNullOrWhiteSpace(phoneNumber);

            await UpsertIfPresentAsync(
                request.UserId,
                SecuritySmsMfaPhoneAvailableKey,
                hasPhoneNumber ? "true" : "false",
                0.9,
                cancellationToken).ConfigureAwait(false);

            byKey[SecuritySmsMfaPhoneAvailableKey] = hasPhoneNumber ? "true" : "false";
        }

        await ApplyConsentStateAsync(
            request.UserId,
            requireConsent: request.RequireTermsConsent,
            consentGranted: request.TermsConsentGranted,
            acceptedKey: ConsentTermsAcceptedKey,
            acceptedAtKey: ConsentTermsAcceptedAtUtcKey,
            versionKey: ConsentTermsVersionKey,
            sourceKey: ConsentTermsSourceKey,
            configuredVersion: request.TermsConsentVersion,
            source: request.Source,
            byKey: byKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await ApplyConsentStateAsync(
            request.UserId,
            requireConsent: request.RequirePrivacyConsent,
            consentGranted: request.PrivacyConsentGranted,
            acceptedKey: ConsentPrivacyAcceptedKey,
            acceptedAtKey: ConsentPrivacyAcceptedAtUtcKey,
            versionKey: ConsentPrivacyVersionKey,
            sourceKey: ConsentPrivacySourceKey,
            configuredVersion: request.PrivacyConsentVersion,
            source: request.Source,
            byKey: byKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            byKey[ProfileDisplayNameKey] = request.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            byKey[ProfileEmailKey] = request.Email;
        }

        var requiredFields = request.RequiredProfileFields
            .Where(static field => !string.IsNullOrWhiteSpace(field))
            .Select(static field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (request.RequirePhoneForSmsMfaEnrollment && !requiredFields.Contains("PhoneNumber", StringComparer.OrdinalIgnoreCase))
        {
            requiredFields.Add("PhoneNumber");
        }

        var missing = EvaluateMissingFields(requiredFields, byKey, request.RequireTermsConsent, request.RequirePrivacyConsent);

        var termsPending = request.RequireTermsConsent && IsPendingConsent(byKey, ConsentTermsAcceptedKey);
        var privacyPending = request.RequirePrivacyConsent && IsPendingConsent(byKey, ConsentPrivacyAcceptedKey);

        var deferredFields = request.DeferredProfileFields
            .Where(static field => !string.IsNullOrWhiteSpace(field))
            .Select(static field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingDeferredCount = deferredFields.Count(field =>
        {
            var factKey = MapFieldToFactKey(field);
            return string.IsNullOrWhiteSpace(factKey)
                || !byKey.TryGetValue(factKey, out var value)
                || string.IsNullOrWhiteSpace(value);
        });

        _logger.LogInformation(
            "Onboarding profile evaluated for {UserId}. RequiredMissing={RequiredMissing}, DeferredMissing={DeferredMissing}, TermsPending={TermsPending}, PrivacyPending={PrivacyPending}",
            request.UserId,
            missing.Count,
            missingDeferredCount,
            termsPending,
            privacyPending);

        return new UserOnboardingInitializationResult(
            IsComplete: missing.Count == 0,
            MissingRequiredFields: missing,
            TermsConsentPending: termsPending,
            PrivacyConsentPending: privacyPending,
            EvaluatedAtUtc: evaluatedAtUtc);
    }

    private async Task UpsertIfPresentAsync(
        string userId,
        string key,
        string? value,
        double confidence,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        await _userMemoryRepository.UpsertFactAsync(
            new UserMemoryFact
            {
                UserId = userId,
                FactKey = key,
                FactValue = value.Trim(),
                Confidence = confidence
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyConsentStateAsync(
        string userId,
        bool requireConsent,
        bool? consentGranted,
        string acceptedKey,
        string acceptedAtKey,
        string versionKey,
        string sourceKey,
        string? configuredVersion,
        string source,
        Dictionary<string, string> byKey,
        CancellationToken cancellationToken)
    {
        if (!requireConsent)
        {
            return;
        }

        var version = string.IsNullOrWhiteSpace(configuredVersion)
            ? "unspecified"
            : configuredVersion.Trim();

        await UpsertIfPresentAsync(userId, versionKey, version, 0.95, cancellationToken).ConfigureAwait(false);
        await UpsertIfPresentAsync(userId, sourceKey, source, 0.9, cancellationToken).ConfigureAwait(false);
        byKey[versionKey] = version;
        byKey[sourceKey] = source;

        if (consentGranted == true)
        {
            var acceptedAtUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            await UpsertIfPresentAsync(userId, acceptedKey, "accepted", 1.0, cancellationToken).ConfigureAwait(false);
            await UpsertIfPresentAsync(userId, acceptedAtKey, acceptedAtUtc, 1.0, cancellationToken).ConfigureAwait(false);
            byKey[acceptedKey] = "accepted";
            byKey[acceptedAtKey] = acceptedAtUtc;
            return;
        }

        if (consentGranted == false)
        {
            await UpsertIfPresentAsync(userId, acceptedKey, "denied", 1.0, cancellationToken).ConfigureAwait(false);
            byKey[acceptedKey] = "denied";
            return;
        }

        await UpsertIfMissingAsync(userId, acceptedKey, "pending", 0.95, byKey, cancellationToken).ConfigureAwait(false);
        if (!byKey.ContainsKey(acceptedKey))
        {
            byKey[acceptedKey] = "pending";
        }
    }

    private async Task UpsertIfMissingAsync(
        string userId,
        string key,
        string value,
        double confidence,
        IReadOnlyDictionary<string, string> existingValues,
        CancellationToken cancellationToken)
    {
        if (existingValues.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        await _userMemoryRepository.UpsertFactAsync(
            new UserMemoryFact
            {
                UserId = userId,
                FactKey = key,
                FactValue = value,
                Confidence = confidence
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static List<string> EvaluateMissingFields(
        IReadOnlyCollection<string> requiredProfileFields,
        IReadOnlyDictionary<string, string> byKey,
        bool requireTermsConsent,
        bool requirePrivacyConsent)
    {
        var missing = new List<string>();

        foreach (var field in requiredProfileFields)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            var key = field.Trim();
            var factKey = MapFieldToFactKey(key);
            if (string.IsNullOrWhiteSpace(factKey))
            {
                continue;
            }

            if (!byKey.TryGetValue(factKey, out var value) || string.IsNullOrWhiteSpace(value))
            {
                missing.Add(key);
            }
        }

        if (requireTermsConsent && IsPendingConsent(byKey, ConsentTermsAcceptedKey))
        {
            missing.Add("TermsConsent");
        }

        if (requirePrivacyConsent && IsPendingConsent(byKey, ConsentPrivacyAcceptedKey))
        {
            missing.Add("PrivacyConsent");
        }

        return missing
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsPendingConsent(IReadOnlyDictionary<string, string> byKey, string key)
    {
        if (!byKey.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "accepted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string? MapFieldToFactKey(string field)
    {
        var normalized = field.Trim();
        return normalized switch
        {
            "DisplayName" => ProfileDisplayNameKey,
            "Email" => ProfileEmailKey,
            "GivenName" => ProfileGivenNameKey,
            "Surname" => ProfileSurnameKey,
            "LastName" => ProfileSurnameKey,
            "PhoneNumber" => ProfilePhoneNumberKey,
            "Phone" => ProfilePhoneNumberKey,
            "StreetAddress" => ProfileStreetAddressKey,
            "Address" => ProfileStreetAddressKey,
            "City" => ProfileCityKey,
            "State" => ProfileStateKey,
            "Region" => ProfileStateKey,
            "PostalCode" => ProfilePostalCodeKey,
            "ZipCode" => ProfilePostalCodeKey,
            "Country" => ProfileCountryKey,
            _ => null
        };
    }
}
