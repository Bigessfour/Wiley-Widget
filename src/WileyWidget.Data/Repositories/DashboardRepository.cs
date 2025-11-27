using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Data.Repositories
{
    /// <summary>
    /// EF Core repository for dashboard data with caching
    /// </summary>
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DashboardRepository> _logger;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public DashboardRepository(
            AppDbContext context,
            IMemoryCache cache,
            ILogger<DashboardRepository> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<decimal> GetTotalBudgetAsync(string fiscalYear)
        {
            var cacheKey = $"TotalBudget_{fiscalYear}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
            {
                _logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
                return cachedValue;
            }

            var year = ParseFiscalYear(fiscalYear);
            var total = await _context.BudgetEntries
                .Where(b => b.FiscalYear == year && b.Parent == null) // Root level only
                .SumAsync(b => b.BudgetedAmount);

            _cache.Set(cacheKey, total, CacheDuration);
            _logger.LogInformation("Total budget for {FiscalYear}: {Total:C}", fiscalYear, total);
            return total;
        }

        public async Task<decimal> GetTotalRevenueAsync(string fiscalYear)
        {
            var cacheKey = $"TotalRevenue_{fiscalYear}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
                return cachedValue;

            var year = ParseFiscalYear(fiscalYear);
            // Get revenue from budget entries (revenues have positive budgeted amounts in certain accounts)
            var revenueTotal = await _context.BudgetEntries
                .Where(b => b.FiscalYear == year && b.ActualAmount > 0 && b.AccountNumber.StartsWith("3")) // Revenue accounts typically start with 3
                .SumAsync(b => b.ActualAmount);

            _cache.Set(cacheKey, revenueTotal, CacheDuration);
            return revenueTotal;
        }

        public async Task<decimal> GetTotalExpensesAsync(string fiscalYear)
        {
            var cacheKey = $"TotalExpenses_{fiscalYear}";
            if (_cache.TryGetValue(cacheKey, out decimal cachedValue))
                return cachedValue;

            var year = ParseFiscalYear(fiscalYear);
            // Get expenses from budget entries
            var expenseTotal = await _context.BudgetEntries
                .Where(b => b.FiscalYear == year && b.ActualAmount > 0 && b.AccountNumber.StartsWith("4")) // Expense accounts typically start with 4
                .SumAsync(b => b.ActualAmount);

            _cache.Set(cacheKey, expenseTotal, CacheDuration);
            return expenseTotal;
        }

        public async Task<List<RevenueByMonth>> GetRevenueTrendAsync(string fiscalYear)
        {
            var cacheKey = $"RevenueTrend_{fiscalYear}";
            if (_cache.TryGetValue(cacheKey, out List<RevenueByMonth>? cachedValue) && cachedValue != null)
                return cachedValue;

            var year = ParseFiscalYear(fiscalYear);
            var fiscalYearStart = new DateTime(year, 1, 1);
            var fiscalYearEnd = new DateTime(year, 12, 31);

            // Get transactions grouped by month from budget entry transactions
            var trend = await _context.BudgetEntries
                .Where(b => b.FiscalYear == year && b.ActualAmount > 0)
                .Include(b => b.Transactions)
                .SelectMany(b => b.Transactions)
                .Where(t => t.TransactionDate >= fiscalYearStart && t.TransactionDate <= fiscalYearEnd)
                .GroupBy(t => new { t.TransactionDate.Month, t.TransactionDate.Year })
                .Select(g => new RevenueByMonth
                {
                    Month = g.Key.Month.ToString(),
                    Year = g.Key.Year,
                    Amount = g.Sum(t => t.Amount)
                })
                .OrderBy(r => r.Year)
                .ThenBy(r => int.Parse(r.Month))
                .ToListAsync();

            // Format month names
            foreach (var item in trend)
            {
                var monthNum = int.Parse(item.Month);
                item.Month = new DateTime(item.Year, monthNum, 1).ToString("MMM");
            }

            _cache.Set(cacheKey, trend, CacheDuration);
            return trend;
        }

        public async Task<List<ExpenseByDepartment>> GetExpenseBreakdownAsync(string fiscalYear)
        {
            var cacheKey = $"ExpenseBreakdown_{fiscalYear}";
            if (_cache.TryGetValue(cacheKey, out List<ExpenseByDepartment>? cachedValue) && cachedValue != null)
                return cachedValue;

            var year = ParseFiscalYear(fiscalYear);
            var totalExpenses = await GetTotalExpensesAsync(fiscalYear);

            var breakdown = await _context.BudgetEntries
                .Where(b => b.FiscalYear == year && b.ActualAmount > 0 && b.AccountNumber.StartsWith("4"))
                .Include(b => b.Department)
                .GroupBy(b => b.Department.Name)
                .Select(g => new ExpenseByDepartment
                {
                    Department = g.Key,
                    Amount = g.Sum(b => b.ActualAmount),
                    PercentOfTotal = totalExpenses > 0 ? (g.Sum(b => b.ActualAmount) / totalExpenses * 100) : 0
                })
                .OrderByDescending(e => e.Amount)
                .ToListAsync();

            _cache.Set(cacheKey, breakdown, CacheDuration);
            return breakdown;
        }

        public async Task<List<DashboardMetric>> GetDashboardMetricsAsync(string fiscalYear)
        {
            var cacheKey = $"DashboardMetrics_{fiscalYear}";
            if (_cache.TryGetValue(cacheKey, out List<DashboardMetric>? cachedValue) && cachedValue != null)
                return cachedValue;

            var year = ParseFiscalYear(fiscalYear);
            var previousYear = year - 1;

            // Get current and previous year data for trend calculation
            var currentBudget = await GetTotalBudgetAsync(fiscalYear);
            var previousBudget = await GetTotalBudgetAsync($"FY {previousYear}");
            var budgetChange = previousBudget > 0 ? ((currentBudget - previousBudget) / previousBudget * 100) : 0;

            var currentRevenue = await GetTotalRevenueAsync(fiscalYear);
            var previousRevenue = await GetTotalRevenueAsync($"FY {previousYear}");
            var revenueChange = previousRevenue > 0 ? ((currentRevenue - previousRevenue) / previousRevenue * 100) : 0;

            var activeAccounts = await GetActiveAccountCountAsync();
            var pendingInvoices = await GetPendingInvoiceCountAsync();
            var currentExpenses = await GetTotalExpensesAsync(fiscalYear);

            var metrics = new List<DashboardMetric>
            {
                new DashboardMetric
                {
                    Name = "Total Budget",
                    Value = (double)currentBudget,
                    Unit = "$",
                    Trend = budgetChange > 0 ? Trend.Up : budgetChange < 0 ? Trend.Down : Trend.Stable,
                    ChangePercent = (double)budgetChange,
                    Description = $"Total municipal budget for {fiscalYear}",
                    LastUpdated = DateTime.Now
                },
                new DashboardMetric
                {
                    Name = "Revenue Growth",
                    Value = (double)revenueChange,
                    Unit = "%",
                    Trend = revenueChange > 0 ? Trend.Up : revenueChange < 0 ? Trend.Down : Trend.Stable,
                    ChangePercent = (double)revenueChange,
                    Description = "Year-over-year revenue growth",
                    LastUpdated = DateTime.Now
                },
                new DashboardMetric
                {
                    Name = "Active Accounts",
                    Value = activeAccounts,
                    Unit = "accounts",
                    Trend = Trend.Stable,
                    ChangePercent = 0,
                    Description = "Number of active municipal accounts",
                    LastUpdated = DateTime.Now
                },
                new DashboardMetric
                {
                    Name = "Pending Invoices",
                    Value = pendingInvoices,
                    Unit = "invoices",
                    Trend = Trend.Down,
                    ChangePercent = -5.0,
                    Description = "Invoices awaiting payment",
                    LastUpdated = DateTime.Now
                },
                new DashboardMetric
                {
                    Name = "Net Position",
                    Value = (double)(currentRevenue - currentExpenses),
                    Unit = "$",
                    Trend = currentRevenue > currentExpenses ? Trend.Up : Trend.Down,
                    ChangePercent = 0,
                    Description = "Current fiscal year net position",
                    LastUpdated = DateTime.Now
                }
            };

            _cache.Set(cacheKey, metrics, CacheDuration);
            return metrics;
        }

        public async Task<List<RecentActivity>> GetRecentActivityAsync(int limit = 10)
        {
            // Get recent transactions from budget entries
            var activities = await _context.BudgetEntries
                .Include(b => b.Transactions)
                .Include(b => b.Department)
                .SelectMany(b => b.Transactions.Select(t => new
                {
                    Transaction = t,
                    Department = b.Department.Name
                }))
                .OrderByDescending(x => x.Transaction.TransactionDate)
                .Take(limit)
                .Select(x => new RecentActivity
                {
                    Id = x.Transaction.Id,
                    Type = x.Transaction.Type,
                    Description = $"{x.Department} - {x.Transaction.Description}",
                    Amount = x.Transaction.Amount,
                    Date = x.Transaction.TransactionDate,
                    Status = "Completed"
                })
                .ToListAsync();

            return activities;
        }

        public async Task<int> GetActiveAccountCountAsync()
        {
            return await _context.MunicipalAccounts
                .Where(a => a.IsActive)
                .CountAsync();
        }

        public async Task<int> GetPendingInvoiceCountAsync()
        {
            return await _context.Invoices
                .Where(i => i.Status == "Pending" || i.Status == "Unpaid")
                .CountAsync();
        }

        private static int ParseFiscalYear(string fiscalYear)
        {
            // Parse "FY 2026" or "2026" to 2026
            var yearStr = fiscalYear.Replace("FY", "").Replace("FY", "").Trim();
            return int.TryParse(yearStr, out var year) ? year : DateTime.Now.Year;
        }
    }
}
