using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for retrieving and caching QuickBooks Chart of Accounts.
/// Synchronizes account data from QuickBooks Online using OAuth.
/// </summary>
public interface IQuickBooksChartOfAccountsService
{
    /// <summary>
    /// Fetches the Chart of Accounts from QuickBooks and caches it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of accounts if successful, empty list if error.</returns>
    Task<List<QuickBooksAccountInfo>> FetchAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached accounts without making an API call.
    /// </summary>
    /// <returns>Cached account list, or empty list if cache is empty.</returns>
    List<QuickBooksAccountInfo> GetCachedAccounts();

    /// <summary>
    /// Gets a specific account by ID from cache.
    /// </summary>
    /// <param name="accountId">The QuickBooks account ID.</param>
    /// <returns>Account if found in cache, null otherwise.</returns>
    QuickBooksAccountInfo? GetAccountById(string accountId);

    /// <summary>
    /// Clears the cached accounts.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Checks if the account cache is populated.
    /// </summary>
    /// <returns>True if accounts are cached, false otherwise.</returns>
    bool HasCachedAccounts();
}

/// <summary>
/// Represents a QuickBooks account from the Chart of Accounts.
/// Based on Intuit QBO Account entity.
/// </summary>
public sealed class QuickBooksAccountInfo
{
    /// <summary>
    /// Unique identifier for the account in QuickBooks.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Display name of the account.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Account number (if assigned).
    /// </summary>
    public string? AccountNumber { get; init; }

    /// <summary>
    /// Type of account (e.g., "Asset", "Liability", "Equity", "Income", "Expense").
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Subtype of account (e.g., "Cash", "Credit Card", "Accounts Receivable").
    /// </summary>
    public string? SubType { get; init; }

    /// <summary>
    /// Current balance of the account.
    /// </summary>
    public decimal CurrentBalance { get; init; }

    /// <summary>
    /// ISO 4217 currency code.
    /// </summary>
    public string? CurrencyId { get; init; }

    /// <summary>
    /// Whether the account is active/enabled.
    /// </summary>
    public bool Active { get; init; } = true;

    /// <summary>
    /// Account description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Account classification (e.g., "Asset", "Liability", etc.).
    /// </summary>
    public string? Classification { get; init; }

    /// <summary>
    /// Timestamp when this account data was last fetched.
    /// </summary>
    public DateTime FetchedAtUtc { get; init; } = DateTime.UtcNow;
}
