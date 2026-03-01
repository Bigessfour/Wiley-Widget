using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for seeding QuickBooks sandbox with sample municipal finance accounts.
/// Automatically creates Chart of Accounts entries after OAuth completes.
/// </summary>
public interface IQuickBooksSandboxSeederService
{
    /// <summary>
    /// Seeds the QuickBooks sandbox with municipal finance accounts.
    /// Creates standard account structure: Assets, Liabilities, Equity, Income, Expenses.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing number of accounts created and any errors.</returns>
    Task<SandboxSeedingResult> SeedSandboxAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of the last seeding operation.
    /// </summary>
    /// <returns>Status of last seeding, or null if never seeded.</returns>
    SandboxSeedingResult? GetLastSeedingStatus();

    /// <summary>
    /// Clears seeding status cache.
    /// </summary>
    void ClearSeedingStatus();
}

/// <summary>
/// Result of a sandbox seeding operation.
/// </summary>
public sealed class SandboxSeedingResult
{
    /// <summary>
    /// Whether the seeding operation succeeded.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Number of accounts successfully created.
    /// </summary>
    public int AccountsCreated { get; set; }

    /// <summary>
    /// Total number of accounts attempted.
    /// </summary>
    public int AccountsAttempted { get; set; }

    /// <summary>
    /// List of created account names.
    /// </summary>
    public List<string> CreatedAccounts { get; set; } = new();

    /// <summary>
    /// List of any errors encountered during seeding.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Timestamp when seeding was performed.
    /// </summary>
    public DateTime SeedTimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall message describing the seeding result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Account template for sandbox seeding.
/// Defines the standard municipal finance account structure.
/// Field names match Intuit QBO REST API v3 Account entity exactly.
/// AccountType enum: https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account
/// </summary>
public sealed class QuickBooksAccountTemplate
{
    /// <summary>
    /// Account name/display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Intuit AccountType enum value (e.g., "Bank", "Other Current Asset", "Accounts Receivable",
    /// "Accounts Payable", "Other Current Liability", "Equity", "Income", "Expense", "Fixed Asset").
    /// Must match the AccountTypeEnum exactly as defined by Intuit QBO API.
    /// </summary>
    public string AccountType { get; set; } = string.Empty;

    /// <summary>
    /// Intuit AccountSubType value (e.g., "Checking", "AccountsReceivable", "RetainedEarnings",
    /// "OtherPrimaryIncome", "PayrollExpenses", etc.).
    /// Must be a valid sub-type for the given AccountType.
    /// </summary>
    public string AccountSubType { get; set; } = string.Empty;

    /// <summary>
    /// Optional account number.
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the account should be active.
    /// </summary>
    public bool Active { get; set; } = true;
}
