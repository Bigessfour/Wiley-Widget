using System.Collections.Generic;
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
    Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear);

    /// <summary>
    /// Gets all budget entries for a fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear);

    /// <summary>
    /// Gets budget entries by fund
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByFundAsync(int fundId);

    /// <summary>
    /// Gets budget entries by department
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId);

    /// <summary>
    /// Gets budget entries by fund and fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByFundAndFiscalYearAsync(int fundId, int fiscalYear);

    /// <summary>
    /// Gets budget entries by department and fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByDepartmentAndFiscalYearAsync(int departmentId, int fiscalYear);

    /// <summary>
    /// Gets sewer enterprise fund budget entries for a fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetSewerBudgetEntriesAsync(int fiscalYear);

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    Task<BudgetEntry?> GetByIdAsync(int id);

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    Task AddAsync(BudgetEntry budgetEntry);

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    Task UpdateAsync(BudgetEntry budgetEntry);

    /// <summary>
    /// Deletes a budget entry
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// Gets budget summary data for reporting
    /// </summary>
    Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets variance analysis data for reporting
    /// </summary>
    Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets department breakdown data for reporting
    /// </summary>
    Task<List<DepartmentSummary>> GetDepartmentBreakdownAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets fund allocations data for reporting
    /// </summary>
    Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets year-end summary data for reporting
    /// </summary>
    Task<BudgetVarianceAnalysis> GetYearEndSummaryAsync(int year);

    // Enterprise-scoped reporting (if data model supports enterprise association)
    Task<BudgetVarianceAnalysis> GetBudgetSummaryByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate);
    Task<BudgetVarianceAnalysis> GetVarianceAnalysisByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate);
    Task<List<DepartmentSummary>> GetDepartmentBreakdownByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate);
    Task<List<FundSummary>> GetFundAllocationsByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate);
}
