using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for BudgetEntry operations
/// </summary>
public interface IBudgetRepository
{
    /// <summary>
    /// Gets budget hierarchy for a fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all budget entries for a fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets budget entries by fund
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByFundAsync(int fundId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets budget entries by department
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets budget entries by fund and fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByFundAndFiscalYearAsync(int fundId, int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets budget entries by department and fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByDepartmentAndFiscalYearAsync(int departmentId, int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sewer enterprise fund budget entries for a fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetSewerBudgetEntriesAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets budget entries by date range
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    Task<BudgetEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    Task AddAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    Task UpdateAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a budget entry
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets budget summary data for reporting
    /// </summary>
    Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets variance analysis data for reporting
    /// </summary>
    Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets department breakdown data for reporting
    /// </summary>
    Task<List<DepartmentSummary>> GetDepartmentBreakdownAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fund allocations data for reporting
    /// </summary>
    Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets year-end summary data for reporting
    /// </summary>
    Task<BudgetVarianceAnalysis> GetYearEndSummaryAsync(int year, CancellationToken cancellationToken = default);

    // Enterprise-scoped reporting (if data model supports enterprise association)
    Task<BudgetVarianceAnalysis> GetBudgetSummaryByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<BudgetVarianceAnalysis> GetVarianceAnalysisByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<List<DepartmentSummary>> GetDepartmentBreakdownByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<List<FundSummary>> GetFundAllocationsByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets data statistics for a fiscal year
    /// </summary>
    Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of revenue accounts for a fiscal year
    /// </summary>
    Task<int> GetRevenueAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of expense accounts for a fiscal year
    /// </summary>
    Task<int> GetExpenseAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default);
}
