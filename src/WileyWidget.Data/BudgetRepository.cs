#nullable enable

using System.Threading;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for BudgetEntry data operations with comprehensive SigNoz telemetry
/// </summary>
public class BudgetRepository : IBudgetRepository
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ITelemetryService? _telemetryService;

    // Activity source for repository-level telemetry
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.BudgetRepository");

    /// <summary>
    /// Constructor with dependency injection
    /// Uses IServiceScopeFactory to create scoped contexts per operation.
    /// </summary>
    public BudgetRepository(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ITelemetryService? telemetryService = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// Safely attempts to get a value from cache, handling disposed cache gracefully
    /// </summary>
    private bool TryGetFromCache<T>(string key, out T? value)
    {
        try
        {
            return _cache.TryGetValue(key, out value);
        }
        catch (ObjectDisposedException)
        {
            Log.Warning("MemoryCache is disposed; cannot retrieve from cache for key '{Key}'.", key);
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Safely sets a value in cache, handling disposed cache gracefully and respecting SizeLimit
    /// </summary>
    private void SetInCache(string key, object value, TimeSpan expiration)
    {
        try
        {
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiration);

            // Required when SizeLimit is configured: assign logical size
            // Use collection count if applicable, else 1
            long size = value switch
            {
                System.Collections.ICollection collection => collection.Count,
                _ => 1
            };
            options.SetSize(size);

            _cache.Set(key, value, options);
        }
        catch (ObjectDisposedException)
        {
            Log.Warning("MemoryCache is disposed; skipping cache update for key '{Key}'.", key);
        }
    }

    /// <summary>
    /// Gets budget hierarchy for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("BudgetRepository.GetBudgetHierarchy");
        activity?.SetTag("fiscal_year", fiscalYear);
        activity?.SetTag("operation.type", "query");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var result = await context.GetBudgetHierarchy(fiscalYear).ToListAsync(cancellationToken);

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
    public async Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("BudgetRepository.GetByFiscalYear");
        activity?.SetTag("fiscal_year", fiscalYear);
        activity?.SetTag("operation.type", "query");
        activity?.SetTag("cache.enabled", true);

        string cacheKey = $"BudgetEntries_FiscalYear_{fiscalYear}";

        // Attempt to read from cache, with fallback on disposal
        IEnumerable<BudgetEntry>? budgetEntries = null;
        if (TryGetFromCache(cacheKey, out budgetEntries))
        {
            activity?.SetTag("cache.hit", true);
            activity?.SetTag("result.count", budgetEntries?.Count() ?? 0);
            return budgetEntries ?? Enumerable.Empty<BudgetEntry>();
        }

        // Cache miss or disposed, fetch from database
        activity?.SetTag("cache.hit", false);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            budgetEntries = await context.BudgetEntries
                .Where(be => be.FiscalYear == fiscalYear)
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Attempt to cache the result
            SetInCache(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));

            activity?.SetTag("result.count", budgetEntries.Count());
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetryService?.RecordException(ex, ("fiscal_year", fiscalYear));
            throw;
        }

        return budgetEntries ?? Enumerable.Empty<BudgetEntry>();
    }

    /// <summary>
    /// Gets budget entries from database directly (fallback when cache is disposed)
    /// </summary>
    private async Task<IEnumerable<BudgetEntry>> GetFromDatabaseAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.BudgetEntries
            .Where(be => be.FiscalYear == fiscalYear)
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }


    /// <summary>
    /// Gets paged budget entries with sorting support
    /// </summary>
    public async Task<(IEnumerable<BudgetEntry> Items, int TotalCount)> GetPagedAsync(int pageNumber = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = false,
        int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
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
    /// NOTE: This returns an IQueryable tied to a DbContext created here; caller is responsible for materializing results promptly.
    /// </summary>
    public Task<IQueryable<BudgetEntry>> GetQueryableAsync(CancellationToken cancellationToken = default)
    {
        var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return Task.FromResult(context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsQueryable());
    }

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    public async Task<BudgetEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.BudgetEntries
            .Include(be => be.Parent)
            .Include(be => be.Children)
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsNoTracking()
            .FirstOrDefaultAsync(be => be.Id == id, cancellationToken);
    }

    /// <summary>
    /// Gets budget entries by date range
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("BudgetRepository.GetByDateRange");
        activity?.SetTag("start_date", startDate);
        activity?.SetTag("end_date", endDate);
        activity?.SetTag("operation.type", "query");
        activity?.SetTag("cache.enabled", true);

        string cacheKey = $"BudgetEntries_DateRange_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";

        if (!TryGetFromCache(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            activity?.SetTag("cache.hit", false);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                budgetEntries = await context.BudgetEntries
                    .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
                    .Include(be => be.Department)
                    .Include(be => be.Fund)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                // Cache for 30 minutes
                SetInCache(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));

                activity?.SetTag("result.count", budgetEntries.Count());
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _telemetryService?.RecordException(ex, ("start_date", startDate), ("end_date", endDate));
                throw;
            }
        }
        else
        {
            activity?.SetTag("cache.hit", true);
        }

        return budgetEntries ?? Enumerable.Empty<BudgetEntry>();
    }

    /// <summary>
    /// Gets budget entries by fund
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByFundAsync(int fundId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"BudgetEntries_Fund_{fundId}";

        if (!TryGetFromCache(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            budgetEntries = await context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.FundId == fundId)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            SetInCache(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets budget entries by department
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"BudgetEntries_Department_{departmentId}";

        if (!TryGetFromCache(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            budgetEntries = await context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.DepartmentId == departmentId)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            SetInCache(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets budget entries by fund and fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByFundAndFiscalYearAsync(int fundId, int fiscalYear, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"BudgetEntries_Fund_{fundId}_Year_{fiscalYear}";

        if (!TryGetFromCache(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            budgetEntries = await context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.FundId == fundId && be.FiscalYear == fiscalYear)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            SetInCache(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets budget entries by department and fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByDepartmentAndFiscalYearAsync(int departmentId, int fiscalYear, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"BudgetEntries_Department_{departmentId}_Year_{fiscalYear}";

        if (!TryGetFromCache(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            budgetEntries = await context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.DepartmentId == departmentId && be.FiscalYear == fiscalYear)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            SetInCache(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Gets sewer enterprise fund budget entries for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetSewerBudgetEntriesAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        // Sewer Enterprise Fund is FundId = 2 (Enterprise Fund)
        const int sewerFundId = 2;
        string cacheKey = $"BudgetEntries_Sewer_Year_{fiscalYear}";

        if (!TryGetFromCache(cacheKey, out IEnumerable<BudgetEntry>? budgetEntries))
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            budgetEntries = await context.BudgetEntries
                .Include(be => be.Department)
                .Include(be => be.Fund)
                .Where(be => be.FundId == sewerFundId && be.FiscalYear == fiscalYear)
                .OrderBy(be => be.AccountNumber)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            SetInCache(cacheKey, budgetEntries, TimeSpan.FromMinutes(30));
        }

        return budgetEntries!;
    }

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    public async Task AddAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.BudgetEntries.Add(budgetEntry);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    public async Task UpdateAsync(BudgetEntry budgetEntry, CancellationToken cancellationToken = default)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.BudgetEntries.Update(budgetEntry);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes a budget entry
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var budgetEntry = await context.BudgetEntries.FindAsync(new object[] { id }, cancellationToken);
        if (budgetEntry != null)
        {
            context.BudgetEntries.Remove(budgetEntry);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gets budget summary data for reporting
    /// </summary>
    public async Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Filter by budget period dates (StartPeriod/EndPeriod), not creation date
        var budgetEntries = await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
            .ToListAsync(cancellationToken);

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
    public async Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // For now, return the same as budget summary - in a real implementation this would have more detailed variance analysis
        return await GetBudgetSummaryAsync(startDate, endDate, cancellationToken);
    }

    /// <summary>
    /// Gets department breakdown data for reporting
    /// </summary>
    public async Task<List<DepartmentSummary>> GetDepartmentBreakdownAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var budgetEntries = await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
            .ToListAsync(cancellationToken);

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
    public async Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var budgetEntries = await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
            .ToListAsync(cancellationToken);

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
    public async Task<BudgetVarianceAnalysis> GetYearEndSummaryAsync(int year, CancellationToken cancellationToken = default)
    {
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year, 12, 31);

        return await GetBudgetSummaryAsync(startDate, endDate, cancellationToken);
    }

    public async Task<BudgetVarianceAnalysis> GetBudgetSummaryByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Try to filter by Department.EnterpriseId or Fund.EnterpriseId if such properties exist.
        var query = context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate);

        // Dynamic enterprise filter if present on Department or Fund
        // Note: Model does not currently expose EnterpriseId on Department/Fund.
        // Keeping the hook for future schema support; currently acts as no-op.

        var budgetEntries = await query.ToListAsync(cancellationToken);
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

    public Task<BudgetVarianceAnalysis> GetVarianceAnalysisByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        => GetBudgetSummaryByEnterpriseAsync(enterpriseId, startDate, endDate, cancellationToken);

    public async Task<List<DepartmentSummary>> GetDepartmentBreakdownByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate);

        var budgetEntries = await query.ToListAsync(cancellationToken);
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

    public async Task<List<FundSummary>> GetFundAllocationsByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate);

        var budgetEntries = await query.ToListAsync(cancellationToken);
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

    public async Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Debug: Check database connection and data
        string? connectionString = null;
        try
        {
            // Only try to get connection string if using relational provider
            if (context.Database.IsRelational())
            {
                connectionString = context.Database.GetConnectionString();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not retrieve database connection string - may be using in-memory provider");
        }

        Log.Information("BudgetRepository: Getting data statistics for FY {FiscalYear} using provider: {Provider}, Connection: {Connection}",
            fiscalYear, context.Database.ProviderName, connectionString ?? "N/A");

        var stats = await context.BudgetEntries
            .Where(be => be.FiscalYear == fiscalYear)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalRecords = g.Count(),
                OldestRecord = g.Min(be => be.CreatedAt),
                NewestRecord = g.Max(be => be.CreatedAt)
            })
            .OrderBy(x => 1)  // Fix: Add OrderBy before FirstOrDefaultAsync to suppress EF warning
            .FirstOrDefaultAsync(cancellationToken);

        Log.Information("BudgetRepository: Query result - TotalRecords: {TotalRecords}, Oldest: {Oldest}, Newest: {Newest}",
            stats?.TotalRecords ?? 0, stats?.OldestRecord, stats?.NewestRecord);

        return stats != null ? (stats.TotalRecords, stats.OldestRecord, stats.NewestRecord) : (0, null, null);
    }

    public async Task<int> GetRevenueAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.BudgetEntries
            .Where(be => be.FiscalYear == fiscalYear && be.AccountNumber.StartsWith("4"))
            .CountAsync(cancellationToken);
    }

    public async Task<int> GetExpenseAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.BudgetEntries
            .Where(be => be.FiscalYear == fiscalYear && (be.AccountNumber.StartsWith("5") || be.AccountNumber.StartsWith("6")))
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Bulk update ActualAmount and Variance for budget entries matching account numbers for a fiscal year.
    /// Returns the number of budget rows updated.
    /// </summary>
    public async Task<int> BulkUpdateActualsAsync(IDictionary<string, decimal> actualsByAccountNumber, int fiscalYear, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("BudgetRepository.BulkUpdateActuals");
        activity?.SetTag("fiscal_year", fiscalYear);
        activity?.SetTag("update.count", actualsByAccountNumber?.Count ?? 0);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (actualsByAccountNumber == null || !actualsByAccountNumber.Any())
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }

            var keys = actualsByAccountNumber.Keys.ToList();
            var entries = await context.BudgetEntries
                .Where(be => be.FiscalYear == fiscalYear && keys.Contains(be.AccountNumber))
                .ToListAsync(cancellationToken);

            var updatableEntries = entries
                .Where(e => !string.IsNullOrEmpty(e.AccountNumber) && actualsByAccountNumber.ContainsKey(e.AccountNumber))
                .ToList();

            foreach (var entry in updatableEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var amt = actualsByAccountNumber[entry.AccountNumber!];
                entry.ActualAmount = amt;
                entry.Variance = entry.BudgetedAmount - entry.ActualAmount;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);

            // Invalidate common fiscal-year caches
            try { _cache.Remove($"BudgetEntries_FiscalYear_{fiscalYear}"); } catch { }
            try { _cache.Remove($"BudgetEntries_Sewer_Year_{fiscalYear}"); } catch { }

            activity?.SetTag("rows.updated", updatableEntries.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return updatableEntries.Count;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetryService?.RecordException(ex, ("fiscal_year", fiscalYear));
            throw;
        }
    }

    /// <summary>
    /// Gets all Town of Wiley 2026 budget data from imported CSV and Sanitation PDF sources
    /// </summary>
    public async Task<IReadOnlyList<TownOfWileyBudget2026>> GetTownOfWileyBudgetDataAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("BudgetRepository.GetTownOfWileyBudgetData");
        activity?.SetTag("operation.type", "query");
        activity?.SetTag("cache.enabled", true);

        const string cacheKey = "TownOfWileyBudget2026_All";

        // Attempt to read from cache
        if (TryGetFromCache(cacheKey, out IReadOnlyList<TownOfWileyBudget2026>? cachedData))
        {
            activity?.SetTag("cache.hit", true);
            activity?.SetTag("result.count", cachedData?.Count ?? 0);
            Console.WriteLine($"[TEST] Repository: Retrieved {cachedData?.Count ?? 0} rows from CACHE");
            return cachedData ?? Array.Empty<TownOfWileyBudget2026>();
        }

        // Cache miss, fetch from database
        activity?.SetTag("cache.hit", false);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // DEBUG: Check if DbSet exists
            var dbSet = context.Set<TownOfWileyBudget2026>();
            Console.WriteLine($"[TEST] Repository: DbSet<TownOfWileyBudget2026> exists");

            var result = await dbSet
                .AsNoTracking()
                .ToListAsync(cancellationToken) ?? new List<TownOfWileyBudget2026>();

            Console.WriteLine($"[TEST] Repository: Fetched {result.Count} rows from DB");
            Console.WriteLine($"[TEST] Repository: Query completed successfully, result is not null={result != null}");

            // No more debug injection - if DB is empty, results are empty
            // Data must be populated via SQL import script

            // Cache the result for 1 hour
            var readOnlyResult = result!.AsReadOnly();
            SetInCache(cacheKey, readOnlyResult, TimeSpan.FromHours(1));

            activity?.SetTag("result.count", result.Count);
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            return readOnlyResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEST] Repository: ERROR fetching TownOfWileyBudget2026: {ex.Message}");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetryService?.RecordException(ex);
            throw;
        }
    }
}
