#nullable enable

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for BudgetEntry data operations with comprehensive SigNoz telemetry
/// </summary>
public class BudgetRepository : IBudgetRepository
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ITelemetryService? _telemetryService;

    // Activity source for repository-level telemetry
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.BudgetRepository");

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetRepository(
        AppDbContext context,
        IMemoryCache cache,
        ITelemetryService? telemetryService = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// Gets budget hierarchy for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear)
    {
        using var activity = ActivitySource.StartActivity("BudgetRepository.GetBudgetHierarchy");
        activity?.SetTag("fiscal_year", fiscalYear);
        activity?.SetTag("operation.type", "query");

        try
        {
        var result = await _context.GetBudgetHierarchy(fiscalYear).ToListAsync();

                activity?.SetTag("result.count", result.Count());
        activity?.SetStatus(ActivityStatusCode.Ok);

                return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        _telemetryService?.RecordException(ex, ("fiscal_year", fiscalYear));
        throw;
        }
    }

    /// <summary>
    /// Gets all budget entries for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear)
    {
        using var activity = ActivitySource.StartActivity("BudgetRepository.GetByFiscalYear");
        activity?.SetTag("fiscal_year", fiscalYear);
        activity?.SetTag("operation.type", "query");
        activity?.SetTag("cache.enabled", true);

        string cacheKey = $"BudgetEntries_FiscalYear_{fiscalYear}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            activity?.SetTag("cache.hit", false);
            try
            {
                budgetEntries = await _context.BudgetEntries
                    .Where(be => be.FiscalYear == fiscalYear)
                    .Include(be => be.Department)
                    .Include(be => be.Fund)
                    .AsSplitQuery()
                    .AsNoTracking()
                    .ToListAsync();

                // Cache for 30 minutes
                _cache.Set(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));

                activity?.SetTag("result.count", budgetEntries.Count());
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _telemetryService?.RecordException(ex, ("fiscal_year", fiscalYear));
                throw;
            }
        }
        else
        {
            activity?.SetTag("cache.hit", true);
            activity?.SetTag("result.count", budgetEntries?.Count() ?? 0);
        }

        return budgetEntries ?? Enumerable.Empty<BudgetEntry>();
    }


    /// <summary>
    /// Gets paged budget entries with sorting support
    /// </summary>
    public async Task<(IEnumerable<BudgetEntry> Items, int TotalCount)> GetPagedAsync(
        int pageNumber = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = false,
        int? fiscalYear = null)
    {
        var query = _context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsSplitQuery()
            .AsQueryable();

        // Apply fiscal year filter if specified
        if (fiscalYear.HasValue)
        {
            query = query.Where(be => be.FiscalYear == fiscalYear.Value);
        }

        // Apply sorting
        query = ApplySorting(query, sortBy, sortDescending);

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply paging
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Gets an IQueryable for flexible querying and paging
    /// </summary>
    public Task<IQueryable<BudgetEntry>> GetQueryableAsync()
    {
        // Returning IQueryable tied to the current scoped DbContext. Callers should only enumerate
        // or materialize the query while the scope (and DbContext) is still alive.
        return Task.FromResult(_context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsSplitQuery()
            .AsQueryable());
    }

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    public async Task<BudgetEntry?> GetByIdAsync(int id)
    {
        return await _context.BudgetEntries
            .Include(be => be.Parent)
            .Include(be => be.Children)
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsNoTracking()
            .FirstOrDefaultAsync(be => be.Id == id);
    }

    /// <summary>
    /// Gets budget entries by fund
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByFundAsync(int fundId)
    {
        string cacheKey = $"BudgetEntries_Fund_{fundId}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            budgetEntries = await _context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.FundId == fundId)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets budget entries by department
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId)
    {
        string cacheKey = $"BudgetEntries_Department_{departmentId}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            budgetEntries = await _context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.DepartmentId == departmentId)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets budget entries by fund and fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByFundAndFiscalYearAsync(int fundId, int fiscalYear)
    {
        string cacheKey = $"BudgetEntries_Fund_{fundId}_Year_{fiscalYear}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            budgetEntries = await _context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.FundId == fundId && be.FiscalYear == fiscalYear)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets budget entries by department and fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByDepartmentAndFiscalYearAsync(int departmentId, int fiscalYear)
    {
        string cacheKey = $"BudgetEntries_Department_{departmentId}_Year_{fiscalYear}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            budgetEntries = await _context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.DepartmentId == departmentId && be.FiscalYear == fiscalYear)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets sewer enterprise fund budget entries for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetSewerBudgetEntriesAsync(int fiscalYear)
    {
        // Sewer Enterprise Fund is FundId = 2 (Enterprise Fund)
        const int sewerFundId = 2;
        string cacheKey = $"BudgetEntries_Sewer_Year_{fiscalYear}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            budgetEntries = await _context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.FundId == sewerFundId && be.FiscalYear == fiscalYear)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    public async Task AddAsync(BudgetEntry budgetEntry)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        _context.BudgetEntries.Add(budgetEntry);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    public async Task UpdateAsync(BudgetEntry budgetEntry)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        _context.BudgetEntries.Update(budgetEntry);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a budget entry
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var budgetEntry = await _context.BudgetEntries.FindAsync(id);
        if (budgetEntry != null)
        {
            _context.BudgetEntries.Remove(budgetEntry);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets budget summary data for reporting
    /// </summary>
    public async Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate)
    {
        var summaryData = await _context.BudgetEntries
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate)
            .Select(be => new
            {
                be.BudgetedAmount,
                be.ActualAmount,
                FundCode = be.Fund != null ? be.Fund.FundCode : null,
                FundName = be.Fund != null ? be.Fund.Name : "Unknown"
            })
            .ToListAsync();

        var analysis = new BudgetVarianceAnalysis
        {
            AnalysisDate = DateTime.UtcNow,
            BudgetPeriod = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            TotalBudgeted = summaryData.Sum(x => x.BudgetedAmount),
            TotalActual = summaryData.Sum(x => x.ActualAmount),
        };

        analysis.TotalVariance = analysis.TotalBudgeted - analysis.TotalActual;
        analysis.TotalVariancePercentage = analysis.TotalBudgeted != 0
            ? (analysis.TotalVariance / analysis.TotalBudgeted) * 100
            : 0;

        // Group by funds
        analysis.FundSummaries = summaryData
            .GroupBy(x => new { x.FundCode, x.FundName })
            .Where(g => g.Key.FundCode != null)
            .Select(g => new FundSummary
            {
                Fund = new BudgetFundType { Code = g.Key.FundCode!, Name = g.Key.FundName },
                FundName = g.Key.FundName,
                TotalBudgeted = g.Sum(x => x.BudgetedAmount),
                TotalActual = g.Sum(x => x.ActualAmount),
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
        return await _context.BudgetEntries
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate)
            .Select(be => new
            {
                DepartmentId = be.DepartmentId,
                DepartmentName = be.Department != null ? be.Department.Name : "Unknown",
                be.BudgetedAmount,
                be.ActualAmount
            })
            .GroupBy(x => new { x.DepartmentId, x.DepartmentName })
            .Select(g => new DepartmentSummary
            {
                Department = new Department { Id = g.Key.DepartmentId, Name = g.Key.DepartmentName },
                DepartmentName = g.Key.DepartmentName,
                TotalBudgeted = g.Sum(x => x.BudgetedAmount),
                TotalActual = g.Sum(x => x.ActualAmount),
                AccountCount = g.Count()
            })
            .ToListAsync();
    }

    /// <summary>
    /// Gets fund allocations data for reporting
    /// </summary>
    public async Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.BudgetEntries
            .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate)
            .Select(be => new
            {
                FundCode = be.Fund != null ? be.Fund.FundCode : null,
                FundName = be.Fund != null ? be.Fund.Name : "Unknown",
                be.BudgetedAmount,
                be.ActualAmount
            })
            .GroupBy(x => new { x.FundCode, x.FundName })
            .Where(g => g.Key.FundCode != null)
            .Select(g => new FundSummary
            {
                Fund = new BudgetFundType { Code = g.Key.FundCode!, Name = g.Key.FundName },
                FundName = g.Key.FundName,
                TotalBudgeted = g.Sum(x => x.BudgetedAmount),
                TotalActual = g.Sum(x => x.ActualAmount),
                AccountCount = g.Count()
            })
            .ToListAsync();
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
        // Try to filter by Department.EnterpriseId or Fund.EnterpriseId if such properties exist.
        var query = _context.BudgetEntries
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
        var query = _context.BudgetEntries
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
        var query = _context.BudgetEntries
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

    private IQueryable<BudgetEntry> ApplySorting(IQueryable<BudgetEntry> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            return sortDescending
                ? query.OrderByDescending(be => be.CreatedAt)
                : query.OrderBy(be => be.CreatedAt);
        }

        return sortBy.ToLowerInvariant() switch
        {
            "createdat" => sortDescending
                ? query.OrderByDescending(be => be.CreatedAt)
                : query.OrderBy(be => be.CreatedAt),
            "budgetedamount" => sortDescending
                ? query.OrderByDescending(be => be.BudgetedAmount)
                : query.OrderBy(be => be.BudgetedAmount),
            "actualamount" => sortDescending
                ? query.OrderByDescending(be => be.ActualAmount)
                : query.OrderBy(be => be.ActualAmount),
            "fiscalyear" => sortDescending
                ? query.OrderByDescending(be => be.FiscalYear)
                : query.OrderBy(be => be.FiscalYear),
            "department" => sortDescending
                ? query.OrderByDescending(be => be.Department != null ? be.Department.Name : "")
                : query.OrderBy(be => be.Department != null ? be.Department.Name : ""),
            "fund" => sortDescending
                ? query.OrderByDescending(be => be.Fund != null ? be.Fund.Name : "")
                : query.OrderBy(be => be.Fund != null ? be.Fund.Name : ""),
            _ => sortDescending
                ? query.OrderByDescending(be => be.CreatedAt)
                : query.OrderBy(be => be.CreatedAt)
        };
    }
}
