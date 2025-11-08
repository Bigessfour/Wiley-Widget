#nullable enable

using System.Collections.Generic;
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
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetAllAsync();

    /// <summary>
    /// Gets a municipal account by ID.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount?> GetByIdAsync(int id);

    /// <summary>
    /// Gets accounts by account number.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber);

    /// <summary>
    /// Gets accounts by department.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId);

    /// <summary>
    /// Adds a new municipal account.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount> AddAsync(MunicipalAccount account);

    /// <summary>
    /// Updates an existing municipal account.
    /// </summary>
    System.Threading.Tasks.Task<MunicipalAccount> UpdateAsync(MunicipalAccount account);

    /// <summary>
    /// Deletes a municipal account by ID.
    /// </summary>
    System.Threading.Tasks.Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Syncs municipal accounts from QuickBooks.
    /// </summary>
    System.Threading.Tasks.Task SyncFromQuickBooksAsync(List<Account> qbAccounts);

    /// <summary>
    /// Imports chart of accounts data from QuickBooks for production use.
    /// </summary>
    /// <param name="chartAccounts">List of QuickBooks accounts to import</param>
    System.Threading.Tasks.Task ImportChartOfAccountsAsync(List<Account> chartAccounts);

    /// <summary>
    /// Gets a budget analysis for a specific period.
    /// </summary>
    System.Threading.Tasks.Task<object> GetBudgetAnalysisAsync(int periodId);

    /// <summary>
    /// Gets accounts filtered by fund type.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetByFundAsync(MunicipalFundType fund);

    /// <summary>
    /// Gets accounts filtered by account type.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type);

    /// <summary>
    /// Gets all municipal accounts with related entities eagerly loaded.
    /// </summary>
    System.Threading.Tasks.Task<IEnumerable<MunicipalAccount>> GetAllWithRelatedAsync();

    /// <summary>
    /// Gets the current active budget period.
    /// </summary>
    System.Threading.Tasks.Task<BudgetPeriod?> GetCurrentActiveBudgetPeriodAsync();
}
