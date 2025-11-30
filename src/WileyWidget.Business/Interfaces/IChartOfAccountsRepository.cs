#nullable enable

using WileyWidget.Models.Entities;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for ChartOfAccountEntry entities.
/// Defines data access operations for the chart of accounts.
/// </summary>
public interface IChartOfAccountsRepository
{
    /// <summary>
    /// Gets all chart of accounts entries.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetAllAsync();

    /// <summary>
    /// Gets only active chart of accounts entries.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetActiveAsync();

    /// <summary>
    /// Gets a chart of accounts entry by ID.
    /// </summary>
    Task<ChartOfAccountEntry?> GetByIdAsync(int id);

    /// <summary>
    /// Gets chart of accounts entries by fund ID.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetByFundIdAsync(int fundId);

    /// <summary>
    /// Gets chart of accounts entries by fund code.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetByFundCodeAsync(string fundCode);

    /// <summary>
    /// Gets chart of accounts entries by account type ID.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetByAccountTypeIdAsync(int accountTypeId);

    /// <summary>
    /// Gets a specific account by number and fund ID.
    /// </summary>
    Task<ChartOfAccountEntry?> GetByAccountNumberAndFundAsync(string accountNumber, int fundId);

    /// <summary>
    /// Adds a new chart of accounts entry.
    /// </summary>
    Task AddAsync(ChartOfAccountEntry entry);

    /// <summary>
    /// Adds multiple chart of accounts entries.
    /// </summary>
    Task AddRangeAsync(IEnumerable<ChartOfAccountEntry> entries);

    /// <summary>
    /// Updates an existing chart of accounts entry.
    /// </summary>
    Task UpdateAsync(ChartOfAccountEntry entry);

    /// <summary>
    /// Deletes a chart of accounts entry by ID. Returns true when an entity was removed.
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks existence of an account by number and fund.
    /// </summary>
    Task<bool> ExistsAsync(string accountNumber, int fundId);

    /// <summary>
    /// Gets income accounts for a specific fund.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetIncomeAccountsAsync(int fundId);

    /// <summary>
    /// Gets expense accounts for a specific fund.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetExpenseAccountsAsync(int fundId);

    /// <summary>
    /// Gets the hierarchical tree of accounts for a fund.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetHierarchyAsync(int fundId);

    /// <summary>
    /// Gets child accounts of a parent account.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetChildrenAsync(int parentAccountId);

    /// <summary>
    /// Gets accounts with their related Fund and AccountType eagerly loaded.
    /// </summary>
    Task<IEnumerable<ChartOfAccountEntry>> GetWithRelatedAsync();

    /// <summary>
    /// Gets a summary count of accounts by fund.
    /// </summary>
    Task<IDictionary<string, int>> GetAccountCountsByFundAsync();
}
