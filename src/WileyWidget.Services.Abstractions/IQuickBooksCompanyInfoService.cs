using System;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for retrieving QuickBooks company/realm information after OAuth authentication.
/// Provides cached access to company details like name, taxID, and realm ID.
/// </summary>
public interface IQuickBooksCompanyInfoService
{
    /// <summary>
    /// Gets the company information from QuickBooks.
    /// Uses OAuth token from QuickBooksAuthService for API requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Company information if successful, null if no valid token or connection error.</returns>
    Task<QuickBooksCompanyInfo?> GetCompanyInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cached company information without making an API call.
    /// </summary>
    /// <returns>Company information if cached, null otherwise.</returns>
    QuickBooksCompanyInfo? GetCachedCompanyInfo();

    /// <summary>
    /// Clears the cached company information.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Represents QuickBooks company information retrieved via CompanyInfo query.
/// Based on Intuit QBO CompanyInfo entity.
/// </summary>
public sealed record QuickBooksCompanyInfo
{
    /// <summary>
    /// Company display name.
    /// </summary>
    public required string CompanyName { get; init; }

    /// <summary>
    /// Company legal name.
    /// </summary>
    public string? LegalName { get; init; }

    /// <summary>
    /// Primary email address.
    /// </summary>
    public string? PrimaryEmailAddress { get; init; }

    /// <summary>
    /// Country code (e.g., "US").
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// Tax identifier / Federal Employer ID Number.
    /// </summary>
    public string? TaxIdentifier { get; init; }

    /// <summary>
    /// Web URL of the company.
    /// </summary>
    public string? WebAddr { get; init; }

    /// <summary>
    /// QuickBooks Realm ID (tenant identifier).
    /// </summary>
    public string? RealmId { get; init; }

    /// <summary>
    /// ISO 4217 currency code (e.g., "USD").
    /// </summary>
    public string? CurrencyCode { get; init; }

    /// <summary>
    /// Timestamp when this information was last fetched.
    /// </summary>
    public DateTime FetchedAtUtc { get; init; } = DateTime.UtcNow;
}

