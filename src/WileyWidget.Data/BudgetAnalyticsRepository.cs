#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for budget analytics queries.
/// Optimized with server-side aggregation, caching, and AsNoTracking for read-only operations.
/// </summary>
public sealed class BudgetAnalyticsRepository : IBudgetAnalyticsRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BudgetAnalyticsRepository> _logger;

    private const int DefaultCacheExpirationMinutes = 5;
    private const string CacheKeyPrefix = "BudgetAnalytics_";

    public BudgetAnalyticsRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        IMemoryCache cache,
        ILogger<BudgetAnalyticsRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<VarianceAnalysis>> GetTopVariancesAsync(int topN, int fiscalYear, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Server-side ordering and top-N selection
            var topVariances = await context.BudgetEntries
                .AsNoTracking()
                .Where(b => b.FiscalYear == fiscalYear)
                .OrderByDescending(b => Math.Abs(b.Variance))
                .Take(topN)
                .Select(e => new VarianceAnalysis
                {
                    AccountNumber = e.AccountNumber,
                    AccountName = e.Description ?? "Unknown",
                    BudgetedAmount = e.BudgetedAmount,
                    ActualAmount = e.ActualAmount,
                    VarianceAmount = e.Variance,
                    VariancePercentage = e.BudgetedAmount != 0 ? (e.Variance / e.BudgetedAmount) * 100 : 0
                })
                .ToListAsync(ct);

            _logger.LogInformation("Retrieved top {Count} variances for fiscal year {FiscalYear}", topVariances.Count, fiscalYear);
            return topVariances;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Top variances query cancelled for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top variances for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    public async Task<List<ReserveDataPoint>> GetReserveHistoryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Query reserve transactions (equity accounts starting with 3)
            var transactions = await context.Transactions
                .AsNoTracking()
                .Include(t => t.BudgetEntry)
                .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
                .Where(t => t.BudgetEntry.AccountNumber.StartsWith("3"))
                .OrderBy(t => t.TransactionDate)
                .Select(t => new { t.TransactionDate, t.Amount })
                .ToListAsync(ct);

            // Calculate running balance
            var dataPoints = new List<ReserveDataPoint>();
            decimal runningBalance = 0;

            foreach (var txn in transactions)
            {
                runningBalance += txn.Amount;
                dataPoints.Add(new ReserveDataPoint
                {
                    Date = txn.TransactionDate,
                    Reserves = runningBalance
                });
            }

            _logger.LogInformation("Retrieved {Count} reserve data points from {From} to {To}", dataPoints.Count, from, to);
            return dataPoints;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Reserve history query cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reserve history from {From} to {To}", from, to);
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetCategoryBreakdownAsync(DateTime start, DateTime end, string? entityName, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Group by account number prefix (first digit) as category proxy
            var query = context.BudgetEntries
                .AsNoTracking()
                .Where(b => b.StartPeriod >= start && b.EndPeriod <= end);

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(b => b.EntityName == entityName);
            }

            var breakdown = await query
                .GroupBy(b => b.AccountNumber.Substring(0, 1))
                .Select(g => new { Category = g.Key, Total = g.Sum(e => e.ActualAmount) })
                .ToDictionaryAsync(x => GetCategoryName(x.Category), x => x.Total, ct);

            _logger.LogInformation("Retrieved category breakdown with {Count} categories", breakdown.Count);
            return breakdown;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Category breakdown query cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category breakdown");
            throw;
        }
    }

    public async Task<TrendAnalysis> GetTrendAnalysisAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Group by month for trend analysis
            var monthlyData = await context.BudgetEntries
                .AsNoTracking()
                .Where(b => b.StartPeriod >= start && b.EndPeriod <= end)
                .GroupBy(b => new { b.StartPeriod.Year, b.StartPeriod.Month })
                .Select(g => new MonthlyTrend
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Budgeted = g.Sum(e => e.BudgetedAmount),
                    Actual = g.Sum(e => e.ActualAmount),
                    Variance = g.Sum(e => e.Variance)
                })
                .OrderBy(m => m.Month)
                .ToListAsync(ct);

            // Calculate growth rate
            var growthRate = 0m;
            if (monthlyData.Count >= 2)
            {
                var first = monthlyData.First().Actual;
                var last = monthlyData.Last().Actual;
                if (first != 0)
                {
                    growthRate = ((last - first) / first) * 100;
                }
            }

            var trend = new TrendAnalysis
            {
                MonthlyTrends = monthlyData,
                OverallTrend = growthRate > 0 ? "Increasing" : growthRate < 0 ? "Decreasing" : "Stable",
                GrowthRate = growthRate
            };

            _logger.LogInformation("Calculated trend analysis with {Count} months, growth rate: {Rate:F2}%", monthlyData.Count, growthRate);
            return trend;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Trend analysis query cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trend analysis from {Start} to {End}", start, end);
            throw;
        }
    }

    /// <summary>
    /// Maps account number prefix to GASB-compliant category name.
    /// Uses standard municipal accounting prefixes (1=Assets, 2=Liabilities, 3=Equity, 4=Revenues, 5=Expenditures, etc.)
    /// </summary>
    private static string GetCategoryName(string prefix) => prefix switch
    {
        "1" => "Assets",
        "2" => "Liabilities",
        "3" => "Equity/Fund Balance",
        "4" => "Revenues",
        "5" => "Expenditures/Expenses",
        "6" => "Other Financing Sources",
        "7" => "Other Financing Uses",
        "8" => "Special/Extraordinary Items",
        "9" => "Transfers",
        _ => "Unknown/Other"
    };

    /// <summary>
    /// Invalidates all cached analytics data for a specific fiscal year.
    /// Call this when budget data changes.
    /// </summary>
    public void InvalidateCache(int fiscalYear)
    {
        var keysToRemove = new[]
        {
            $"{CacheKeyPrefix}Overview_{fiscalYear}",
            $"{CacheKeyPrefix}Metrics_{fiscalYear}",
            $"{CacheKeyPrefix}KPIs_{fiscalYear}",
            $"{CacheKeyPrefix}VarianceDetails_{fiscalYear}"
        };

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }

        _logger.LogInformation("Invalidated analytics cache for fiscal year {FiscalYear}", fiscalYear);
    }

    public async Task<BudgetOverviewData> GetBudgetOverviewDataAsync(int fiscalYear, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Overview_{fiscalYear}";

        if (_cache.TryGetValue<BudgetOverviewData>(cacheKey, out var cached))
        {
            _logger.LogDebug("Returning cached budget overview for fiscal year {FiscalYear}", fiscalYear);
            return cached!;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Server-side aggregation - no client-side enumeration
            var overview = await context.BudgetEntries
                .AsNoTracking()
                .Where(b => b.FiscalYear == fiscalYear)
                .GroupBy(_ => 1)  // Single group for global aggregation
                .Select(g => new
                {
                    TotalBudget = g.Sum(e => e.BudgetedAmount),
                    TotalActual = g.Sum(e => e.ActualAmount),
                    OverBudget = g.Count(e => e.ActualAmount > e.BudgetedAmount),
                    UnderBudget = g.Count(e => e.ActualAmount < e.BudgetedAmount)
                })
                .SingleOrDefaultAsync(ct);

            if (overview == null)
            {
                _logger.LogWarning("No budget entries found for fiscal year {FiscalYear}", fiscalYear);
                return new BudgetOverviewData();
            }

            var result = new BudgetOverviewData
            {
                TotalBudget = overview.TotalBudget,
                TotalActual = overview.TotalActual,
                TotalVariance = overview.TotalBudget - overview.TotalActual,
                OverBudgetCount = overview.OverBudget,
                UnderBudgetCount = overview.UnderBudget
            };

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheExpirationMinutes),
                Size = 1
            });
            _logger.LogInformation("Calculated budget overview for fiscal year {FiscalYear}: Budget={Budget:C}, Actual={Actual:C}",
                fiscalYear, result.TotalBudget, result.TotalActual);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Budget overview query cancelled for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budget overview data for fiscal year {FiscalYear}", fiscalYear);
            throw; // Rethrow to let caller handle; returning empty data hides errors
        }
    }

    public async Task<List<BudgetMetric>> GetBudgetMetricsAsync(int fiscalYear, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Metrics_{fiscalYear}";

        if (_cache.TryGetValue<List<BudgetMetric>>(cacheKey, out var cached))
        {
            _logger.LogDebug("Returning cached budget metrics for fiscal year {FiscalYear}", fiscalYear);
            return cached!;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Server-side grouping and aggregation with projection
            var metrics = await context.BudgetEntries
                .AsNoTracking()
                .Where(b => b.FiscalYear == fiscalYear)
                .GroupBy(e => new { DepartmentId = e.DepartmentId, DepartmentName = e.Department!.Name })
                .Select(g => new
                {
                    g.Key.DepartmentName,
                    BudgetedAmount = g.Sum(e => e.BudgetedAmount),
                    Amount = g.Sum(e => e.ActualAmount),
                    Variance = g.Sum(e => e.Variance)
                })
                .ToListAsync(ct);

            var result = metrics
                .Select(m => new BudgetMetric(
                    Name: m.DepartmentName ?? "Unknown",
                    Value: m.Variance,
                    DepartmentName: m.DepartmentName ?? "Unknown",
                    BudgetedAmount: m.BudgetedAmount,
                    Amount: m.Amount,
                    Variance: m.Variance,
                    VariancePercent: m.BudgetedAmount != 0 ? (m.Variance / m.BudgetedAmount) * 100 : 0,
                    IsOverBudget: m.Amount > m.BudgetedAmount
                ))
                .ToList();

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheExpirationMinutes),
                Size = 1
            });
            _logger.LogInformation("Calculated {Count} budget metrics for fiscal year {FiscalYear}", result.Count, fiscalYear);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Budget metrics query cancelled for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting budget metrics for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    public async Task<List<SummaryKpi>> GetSummaryKpisAsync(int fiscalYear, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}KPIs_{fiscalYear}";

        if (_cache.TryGetValue<List<SummaryKpi>>(cacheKey, out var cached))
        {
            _logger.LogDebug("Returning cached summary KPIs for fiscal year {FiscalYear}", fiscalYear);
            return cached!;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Server-side aggregation
            var summary = await context.BudgetEntries
                .AsNoTracking()
                .Where(b => b.FiscalYear == fiscalYear)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalBudget = g.Sum(e => e.BudgetedAmount),
                    TotalActual = g.Sum(e => e.ActualAmount),
                    TotalEncumbrance = g.Sum(e => e.EncumbranceAmount)
                })
                .SingleOrDefaultAsync(ct);

            if (summary == null)
            {
                _logger.LogWarning("No budget entries found for KPIs in fiscal year {FiscalYear}", fiscalYear);
                return new List<SummaryKpi>();
            }

            var variance = summary.TotalBudget - summary.TotalActual;
            var variancePercent = summary.TotalBudget != 0 ? (variance / summary.TotalBudget) * 100 : 0;

            var kpis = new List<SummaryKpi>
            {
                new SummaryKpi("Total Budget", summary.TotalBudget, "C0", true),
                new SummaryKpi("Total Actual", summary.TotalActual, "C0", false),
                new SummaryKpi("Total Encumbrance", summary.TotalEncumbrance, "C0", false),
                new SummaryKpi("Total Variance", variance, "C0", variance >= 0),
                new SummaryKpi("Variance %", variancePercent, "P1", variancePercent >= 0)
            };

            _cache.Set(cacheKey, kpis, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheExpirationMinutes),
                Size = 1
            });
            _logger.LogInformation("Calculated {Count} summary KPIs for fiscal year {FiscalYear}", kpis.Count, fiscalYear);

            return kpis;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Summary KPIs query cancelled for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting summary KPIs for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    public async Task<List<VarianceRecord>> GetVarianceDetailsAsync(int fiscalYear, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}VarianceDetails_{fiscalYear}";

        if (_cache.TryGetValue<List<VarianceRecord>>(cacheKey, out var cached))
        {
            _logger.LogDebug("Returning cached variance details for fiscal year {FiscalYear}", fiscalYear);
            return cached!;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Server-side projection - only select needed columns
            var records = await context.BudgetEntries
                .AsNoTracking()
                .Where(b => b.FiscalYear == fiscalYear)
                .Select(e => new VarianceRecord(
                    Department: e.Department!.Name ?? "Unknown",
                    Account: e.AccountNumber,
                    Budget: e.BudgetedAmount,
                    Actual: e.ActualAmount,
                    Variance: e.Variance,
                    VariancePercent: e.BudgetedAmount != 0 ? (e.Variance / e.BudgetedAmount) * 100 : 0
                ))
                .ToListAsync(ct);

            _cache.Set(cacheKey, records, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheExpirationMinutes),
                Size = 1
            });
            _logger.LogInformation("Retrieved {Count} variance records for fiscal year {FiscalYear}", records.Count, fiscalYear);

            return records;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Variance details query cancelled for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting variance details for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }
}
