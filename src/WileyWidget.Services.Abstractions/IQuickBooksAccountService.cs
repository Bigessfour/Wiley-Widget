using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for retrieving QuickBooks Chart of Accounts and account data.
/// Requires valid OAuth token from IQuickBooksAuthService.
/// </summary>
public interface IQuickBooksAccountService
{
    /// <summary>
    /// Retrieves all accounts from QuickBooks using the Chart of Accounts query.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of accounts if successful, empty list if no valid token or connection error.</returns>
    Task<List<QuickBooksAccount>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves accounts filtered by account classification.
    /// </summary>
    /// <param name="classification">Account classification to filter by (e.g., Asset, Liability, Equity, Income, Expense).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Filtered list of accounts.</returns>
    Task<List<QuickBooksAccount>> GetAccountsByClassificationAsync(string classification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current balance of an account.
    /// </summary>
    /// <param name="accountId">QuickBooks account ID.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Account balance if successful, null if not found.</returns>
    Task<decimal?> GetAccountBalanceAsync(string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached accounts without making an API call.
    /// </summary>
    /// <returns>Cached list of accounts, empty if not cached.</returns>
    List<QuickBooksAccount> GetCachedAccounts();

    /// <summary>
    /// Clears the account cache.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Represents a QuickBooks Chart of Accounts entry.
/// Based on Intuit QBO Account entity.
/// </summary>
public sealed record QuickBooksAccount
{
    /// <summary>
    /// Unique QuickBooks account ID.
    /// </summary>
    public required string AccountId { get; init; }

    /// <summary>
    /// Account name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Account description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Account type (Asset, Liability, Equity, Income, Expense, etc.).
    /// </summary>
    public required string AccountType { get; init; }

    /// <summary>
    /// Account subtype classification.
    /// </summary>
    public string? Classification { get; init; }

    /// <summary>
    /// Account subtype (e.g., "CashOnHand", "Checking", "SavingsAccount" for Assets).
    /// </summary>
    public string? AccountSubType { get; init; }

    /// <summary>
    /// Current account balance.
    /// </summary>
    public decimal CurrentBalance { get; init; }

    /// <summary>
    /// Sync token for concurrent update safety (used by QuickBooks API).
    /// </summary>
    public string? SyncToken { get; init; }

    /// <summary>
    /// Whether the account is active.
    /// </summary>
    public bool Active { get; init; } = true;

    /// <summary>
    /// Currency code (e.g., "USD").
    /// </summary>
    public string? CurrencyCode { get; init; }

    /// <summary>
    /// Timestamp when this account data was last fetched.
    /// </summary>
    public DateTime FetchedAtUtc { get; init; } = DateTime.UtcNow;
}
