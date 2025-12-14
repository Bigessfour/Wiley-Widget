#nullable enable

using System.Collections.Generic;
using System.Threading;
using Intuit.Ipp.Data;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for MunicipalAccount entities.
/// Defines data access operations for municipal accounting accounts.
/// </summary>
public interface IMunicipalAccountRepository
{
    /// <summary>
    /// Gets all municipal accounts.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a municipal account by ID.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets accounts by account number.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets accounts by department.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new municipal account.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount> AddAsync(MunicipalAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing municipal account.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount> UpdateAsync(MunicipalAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a municipal account by ID.
    /// </summary>
    System.Threading.Tasks.Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs municipal accounts from QuickBooks.
    /// </summary>
    System.Threading.Tasks.Task SyncFromQuickBooksAsync(List<Account> qbAccounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports chart of accounts data from QuickBooks for production use.
    /// </summary>
    /// <param name="chartAccounts">List of QuickBooks accounts to import</param>
    System.Threading.Tasks.Task ImportChartOfAccountsAsync(List<Account> chartAccounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a budget analysis for a specific period.
    /// </summary>
    System.Threading.Tasks.Task<object> GetBudgetAnalysisAsync(int periodId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets accounts filtered by fund type.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetByFundAsync(MunicipalFundType fund, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets accounts filtered by account type.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all municipal accounts with related entities eagerly loaded.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetAllWithRelatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current active budget period.
    /// </summary>
    System.Threading.Tasks.Task<BudgetPeriod?> GetCurrentActiveBudgetPeriodAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of municipal accounts.
    /// </summary>
    System.Threading.Tasks.Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
