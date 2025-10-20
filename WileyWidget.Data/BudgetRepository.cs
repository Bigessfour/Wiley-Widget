#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for BudgetEntry data operations
/// </summary>
public class BudgetRepository : IBudgetRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets budget hierarchy for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GetBudgetHierarchy(fiscalYear).ToListAsync();
    }

    /// <summary>
    /// Gets all budget entries for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.FiscalYear == fiscalYear)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    public async Task<BudgetEntry?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .Include(be => be.Parent)
            .Include(be => be.Children)
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsNoTracking()
            .FirstOrDefaultAsync(be => be.Id == id);
    }

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    public async Task AddAsync(BudgetEntry budgetEntry)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.BudgetEntries.Add(budgetEntry);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    public async Task UpdateAsync(BudgetEntry budgetEntry)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.BudgetEntries.Update(budgetEntry);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a budget entry
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var budgetEntry = await context.BudgetEntries.FindAsync(id);
        if (budgetEntry != null)
        {
            context.BudgetEntries.Remove(budgetEntry);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets budget summary data for reporting
    /// </summary>
    public async Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var budgetEntries = await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate)
            .ToListAsync();

        var analysis = new BudgetVarianceAnalysis
        {
            AnalysisDate = DateTime.UtcNow,
            BudgetPeriod = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            TotalBudgeted = budgetEntries.Sum(be => be.BudgetedAmount),
            TotalActual = budgetEntries.Sum(be => be.ActualAmount),
        };
        
        analysis.TotalVariance = analysis.TotalBudgeted - analysis.TotalActual;
        analysis.TotalVariancePercentage = analysis.TotalBudgeted != 0 
            ? (analysis.TotalVariance / analysis.TotalBudgeted) * 100 
            : 0;

        // Group by funds
        analysis.FundSummaries = budgetEntries
            .GroupBy(be => be.Fund)
            .Where(g => g.Key != null)
            .Select(g => new FundSummary
            {
                Fund = new BudgetFundType { Code = g.Key!.FundCode, Name = g.Key.Name },
                FundName = g.Key?.Name ?? "Unknown",
                TotalBudgeted = g.Sum(be => be.BudgetedAmount),
                TotalActual = g.Sum(be => be.ActualAmount),
                AccountCount = g.Count()
            })
            .ToList();

        // Calculate variances for fund summaries
        foreach (var fundSummary in analysis.FundSummaries)
        {
            fundSummary.Variance = fundSummary.TotalBudgeted - fundSummary.TotalActual;
            fundSummary.VariancePercentage = fundSummary.TotalBudgeted != 0 
                ? (fundSummary.Variance / fundSummary.TotalBudgeted) * 100 
                : 0;
        }

        return analysis;
    }

    /// <summary>
    /// Gets variance analysis data for reporting
    /// </summary>
    public async Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime startDate, DateTime endDate)
    {
        // For now, return the same as budget summary - in a real implementation this would have more detailed variance analysis
        return await GetBudgetSummaryAsync(startDate, endDate);
    }

    /// <summary>
    /// Gets department breakdown data for reporting
    /// </summary>
    public async Task<List<DepartmentSummary>> GetDepartmentBreakdownAsync(DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var budgetEntries = await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate)
            .ToListAsync();

        return budgetEntries
            .GroupBy(be => be.Department)
            .Where(g => g.Key != null)
            .Select(g => new DepartmentSummary
            {
                Department = g.Key,
                DepartmentName = g.Key?.Name ?? "Unknown",
                TotalBudgeted = g.Sum(be => be.BudgetedAmount),
                TotalActual = g.Sum(be => be.ActualAmount),
                AccountCount = g.Count()
            })
            .ToList();
    }

    /// <summary>
    /// Gets fund allocations data for reporting
    /// </summary>
    public async Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var budgetEntries = await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate)
            .ToListAsync();

        return budgetEntries
            .GroupBy(be => be.Fund)
            .Where(g => g.Key != null)
            .Select(g => new FundSummary
            {
                Fund = new BudgetFundType { Code = g.Key!.FundCode, Name = g.Key.Name },
                FundName = g.Key?.Name ?? "Unknown",
                TotalBudgeted = g.Sum(be => be.BudgetedAmount),
                TotalActual = g.Sum(be => be.ActualAmount),
                AccountCount = g.Count()
            })
            .ToList();
    }

    /// <summary>
    /// Gets year-end summary data for reporting
    /// </summary>
    public async Task<BudgetVarianceAnalysis> GetYearEndSummaryAsync(int year)
    {
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year, 12, 31);
        
        return await GetBudgetSummaryAsync(startDate, endDate);
    }

    // Enterprise-scoped overloads — assumes BudgetEntry has implicit enterprise association via Department/Fund or related tables.
    // If not available, this falls back to date-only filters (no-op enterprise filter).
    public async Task<BudgetVarianceAnalysis> GetBudgetSummaryByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Try to filter by Department.EnterpriseId or Fund.EnterpriseId if such properties exist.
        var query = context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate);

        // Dynamic enterprise filter if present on Department or Fund
        // Note: Model does not currently expose EnterpriseId on Department/Fund.
        // Keeping the hook for future schema support; currently acts as no-op.

        var budgetEntries = await query.ToListAsync();
        // Reuse existing aggregation logic via in-memory projection
        var analysis = new BudgetVarianceAnalysis
        {
            AnalysisDate = DateTime.UtcNow,
            BudgetPeriod = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            TotalBudgeted = budgetEntries.Sum(be => be.BudgetedAmount),
            TotalActual = budgetEntries.Sum(be => be.ActualAmount),
        };
        analysis.TotalVariance = analysis.TotalBudgeted - analysis.TotalActual;
        analysis.TotalVariancePercentage = analysis.TotalBudgeted != 0
            ? (analysis.TotalVariance / analysis.TotalBudgeted) * 100
            : 0;

        analysis.FundSummaries = budgetEntries
            .GroupBy(be => be.Fund)
            .Where(g => g.Key != null)
            .Select(g => new FundSummary
            {
                Fund = new BudgetFundType { Code = g.Key!.FundCode, Name = g.Key.Name },
                FundName = g.Key?.Name ?? "Unknown",
                TotalBudgeted = g.Sum(be => be.BudgetedAmount),
                TotalActual = g.Sum(be => be.ActualAmount),
                AccountCount = g.Count()
            })
            .ToList();

        foreach (var fundSummary in analysis.FundSummaries)
        {
            fundSummary.Variance = fundSummary.TotalBudgeted - fundSummary.TotalActual;
            fundSummary.VariancePercentage = fundSummary.TotalBudgeted != 0
                ? (fundSummary.Variance / fundSummary.TotalBudgeted) * 100
                : 0;
        }

        return analysis;
    }

    public Task<BudgetVarianceAnalysis> GetVarianceAnalysisByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate)
        => GetBudgetSummaryByEnterpriseAsync(enterpriseId, startDate, endDate);

    public async Task<List<DepartmentSummary>> GetDepartmentBreakdownByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate);

        var budgetEntries = await query.ToListAsync();
        return budgetEntries
            .GroupBy(be => be.Department)
            .Where(g => g.Key != null)
            .Select(g => new DepartmentSummary
            {
                Department = g.Key,
                DepartmentName = g.Key?.Name ?? "Unknown",
                TotalBudgeted = g.Sum(be => be.BudgetedAmount),
                TotalActual = g.Sum(be => be.ActualAmount),
                AccountCount = g.Count()
            })
            .ToList();
    }

    public async Task<List<FundSummary>> GetFundAllocationsByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate);

        var budgetEntries = await query.ToListAsync();
        return budgetEntries
            .GroupBy(be => be.Fund)
            .Where(g => g.Key != null)
            .Select(g => new FundSummary
            {
                Fund = new BudgetFundType { Code = g.Key!.FundCode, Name = g.Key.Name },
                FundName = g.Key?.Name ?? "Unknown",
                TotalBudgeted = g.Sum(be => be.BudgetedAmount),
                TotalActual = g.Sum(be => be.ActualAmount),
                AccountCount = g.Count()
            })
            .ToList();
    }
}