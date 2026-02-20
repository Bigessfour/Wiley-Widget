using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Repository implementation for analytics-specific data operations
    /// </summary>
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<AnalyticsRepository> _logger;

        public AnalyticsRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<AnalyticsRepository> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets historical reserve data points for forecasting
        /// </summary>
        public async Task<IEnumerable<ReserveDataPoint>> GetHistoricalReserveDataAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Get transactions that affect reserves (savings, investments, fund balances)
            var reserveTransactions = await context.Transactions
                .AsNoTracking()
                .Include(t => t.BudgetEntry)
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
                .Where(t => t.BudgetEntry.AccountNumber.StartsWith("1") || // Assets
                           t.BudgetEntry.AccountNumber.StartsWith("2") || // Liabilities
                           t.BudgetEntry.AccountNumber.StartsWith("3"))   // Equity/Reserves
                .OrderBy(t => t.TransactionDate)
                .ToListAsync(cancellationToken);

            if (!reserveTransactions.Any())
            {
                _logger.LogWarning("No reserve transactions found for period {Start} to {End}", startDate, endDate);
                return Array.Empty<ReserveDataPoint>();
            }

            // Calculate running reserve balance
            var dataPoints = new List<ReserveDataPoint>();
            decimal runningBalance = 0;

            foreach (var transaction in reserveTransactions)
            {
                runningBalance += transaction.Amount;

                dataPoints.Add(new ReserveDataPoint
                {
                    Date = transaction.TransactionDate,
                    Reserves = runningBalance
                });
            }

            _logger.LogInformation("Retrieved {Count} reserve data points for forecasting", dataPoints.Count);
            return dataPoints;
        }

        /// <summary>
        /// Gets current reserve balance
        /// </summary>
        public async Task<decimal> GetCurrentReserveBalanceAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Calculate current reserves from latest transactions
            var latestTransactions = await context.Transactions
                .AsNoTracking()
                .Include(t => t.BudgetEntry)
                .Where(t => t.BudgetEntry.AccountNumber.StartsWith("3")) // Equity/Reserves accounts
                .OrderByDescending(t => t.TransactionDate)
                .Take(100) // Get recent transactions for calculation
                .ToListAsync(cancellationToken);

            if (!latestTransactions.Any())
            {
                _logger.LogWarning("No reserve transactions found for current balance calculation");
                return 0;
            }

            // Calculate balance from most recent transactions
            var balance = latestTransactions.Sum(t => t.Amount);

            _logger.LogInformation("Calculated current reserve balance: {Balance:C}", balance);
            return balance;
        }

        /// <summary>
        /// Gets budget entries for variance analysis
        /// </summary>
        public async Task<IEnumerable<BudgetEntry>> GetBudgetEntriesForVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.BudgetEntries
                .AsNoTracking()
                .Include(be => be.Fund)
                .Include(be => be.MunicipalAccount)
                .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
                .Where(be => be.BudgetedAmount > 0) // Only entries with budget amounts
                .OrderBy(be => be.AccountNumber)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets municipal account information for analytics
        /// </summary>
        public async Task<IEnumerable<MunicipalAccount>> GetMunicipalAccountsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.MunicipalAccounts
                .AsNoTracking()
                .OrderBy(ma => ma.AccountNumber)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets available entity names for filtering
        /// </summary>
        public async Task<IEnumerable<string>> GetAvailableEntitiesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await context.BudgetEntries
                .AsNoTracking()
                .Where(be => !string.IsNullOrWhiteSpace(be.EntityName))
                .Select(be => be.EntityName!)
                .Distinct()
                .OrderBy(entity => entity)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets trend data for revenue, expenses, and net position grouped by fiscal year,
        /// extended with simple linear projections for <paramref name="projectionYears"/> future years.
        /// </summary>
        public async Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Pull lightweight projection from DB; group in memory to avoid EF Core
            // translation issues with conditional aggregations inside GroupBy.
            var entries = await context.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear > 2000)
                .Select(be => new
                {
                    be.FiscalYear,
                    be.AccountNumber,
                    be.ActualAmount,
                    be.BudgetedAmount
                })
                .ToListAsync(cancellationToken);

            if (!entries.Any())
            {
                _logger.LogWarning("GetTrendDataAsync: No budget entries found for trend analysis");
                return new List<TrendSeries>();
            }

            // GASB account number prefixes: 4xx = Revenue, 5xx/6xx = Expenditures
            var byYear = entries
                .GroupBy(e => e.FiscalYear)
                .Select(g => new
                {
                    FiscalYear = g.Key,
                    ActualRevenue = g.Where(e => e.AccountNumber.StartsWith("4")).Sum(e => e.ActualAmount),
                    ActualExpenses = g.Where(e => e.AccountNumber.StartsWith("5") || e.AccountNumber.StartsWith("6")).Sum(e => e.ActualAmount),
                    BudgetedRevenue = g.Where(e => e.AccountNumber.StartsWith("4")).Sum(e => e.BudgetedAmount),
                    BudgetedExpenses = g.Where(e => e.AccountNumber.StartsWith("5") || e.AccountNumber.StartsWith("6")).Sum(e => e.BudgetedAmount)
                })
                .OrderBy(g => g.FiscalYear)
                .ToList();

            // Build historical points (one point per fiscal year, dated Jan 1 of that year)
            var revenuePoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.ActualRevenue)).ToList();
            var expensePoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.ActualExpenses)).ToList();
            var budgetedRevenuePoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.BudgetedRevenue)).ToList();
            var netPositionPoints = byYear.Select(y => new TrendPoint(new DateTime(y.FiscalYear, 1, 1), y.ActualRevenue - y.ActualExpenses)).ToList();

            // Append projected future years using average YoY growth from available history
            if (projectionYears > 0 && byYear.Count >= 2)
            {
                int lastYear = byYear.Last().FiscalYear;
                decimal revenueGrowth = CalculateAvgGrowthRate(byYear.Select(y => y.ActualRevenue).ToList());
                decimal expenseGrowth = CalculateAvgGrowthRate(byYear.Select(y => y.ActualExpenses).ToList());

                decimal projRevenue = byYear.Last().ActualRevenue;
                decimal projExpenses = byYear.Last().ActualExpenses;

                for (int i = 1; i <= projectionYears; i++)
                {
                    projRevenue = Math.Round(projRevenue * (1 + revenueGrowth), 2);
                    projExpenses = Math.Round(projExpenses * (1 + expenseGrowth), 2);
                    var projDate = new DateTime(lastYear + i, 1, 1);
                    revenuePoints.Add(new TrendPoint(projDate, projRevenue));
                    expensePoints.Add(new TrendPoint(projDate, projExpenses));
                    netPositionPoints.Add(new TrendPoint(projDate, Math.Round(projRevenue - projExpenses, 2)));
                    // Budgeted projection uses same growth rate as actual revenue/expenses
                    budgetedRevenuePoints.Add(new TrendPoint(projDate, Math.Round(byYear.Last().BudgetedRevenue * (decimal)Math.Pow((double)(1 + revenueGrowth), i), 2)));
                }
            }

            _logger.LogInformation(
                "GetTrendDataAsync: {HistoricalYears} historical fiscal year(s) + {ProjectedYears} projected year(s)",
                byYear.Count, projectionYears);

            return new List<TrendSeries>
            {
                new TrendSeries("Revenue",          revenuePoints),
                new TrendSeries("Expenses",         expensePoints),
                new TrendSeries("Budgeted Revenue", budgetedRevenuePoints),
                new TrendSeries("Net Position",     netPositionPoints)
            };
        }

        /// <summary>
        /// Calculates the average year-over-year growth rate from a list of ordered values.
        /// Clamped to ±50% to prevent runaway projections from outlier data.
        /// Returns 0.02 (2%) if fewer than two non-zero values are present.
        /// </summary>
        private static decimal CalculateAvgGrowthRate(List<decimal> values)
        {
            if (values.Count < 2) return 0.02m;

            decimal total = 0m;
            int count = 0;

            for (int i = 1; i < values.Count; i++)
            {
                if (values[i - 1] != 0)
                {
                    decimal rate = (values[i] - values[i - 1]) / Math.Abs(values[i - 1]);
                    total += Math.Clamp(rate, -0.50m, 0.50m);
                    count++;
                }
            }

            return count > 0 ? total / count : 0.02m;
        }

        /// <summary>
        /// Runs a scenario analysis
        /// </summary>
        public async Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Clamp extreme inputs to avoid unrealistic projections
            rateIncreasePercent = Math.Max(-50m, Math.Min(200m, rateIncreasePercent));
            expenseIncreasePercent = Math.Max(-50m, Math.Min(200m, expenseIncreasePercent));

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Prefer current fiscal year data; fall back to most recent available year
            var currentYear = DateTime.Now.Year;
            var yearEntries = await context.BudgetEntries
                .AsNoTracking()
                .Where(be => be.FiscalYear == currentYear)
                .Select(be => new { be.AccountNumber, be.ActualAmount, be.BudgetedAmount })
                .ToListAsync(cancellationToken);

            if (!yearEntries.Any())
            {
                var latestYear = await context.BudgetEntries
                    .AsNoTracking()
                    .OrderByDescending(be => be.FiscalYear)
                    .Select(be => be.FiscalYear)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestYear == 0)
                {
                    _logger.LogWarning("RunScenarioAsync: No budget entries available; returning empty scenario");
                    return new ScenarioResult("No data available", 0m, -revenueTarget);
                }

                yearEntries = await context.BudgetEntries
                    .AsNoTracking()
                    .Where(be => be.FiscalYear == latestYear)
                    .Select(be => new { be.AccountNumber, be.ActualAmount, be.BudgetedAmount })
                    .ToListAsync(cancellationToken);
                currentYear = latestYear;
            }

            // Use actuals when available; fall back to budgeted amounts
            decimal baselineRevenue = yearEntries.Where(e => e.AccountNumber.StartsWith("4"))
                .Select(e => e.ActualAmount == 0 ? e.BudgetedAmount : e.ActualAmount)
                .Sum();

            decimal baselineExpenses = yearEntries.Where(e => e.AccountNumber.StartsWith("5") || e.AccountNumber.StartsWith("6"))
                .Select(e => e.ActualAmount == 0 ? e.BudgetedAmount : e.ActualAmount)
                .Sum();

            if (baselineRevenue == 0 && baselineExpenses == 0)
            {
                _logger.LogWarning("RunScenarioAsync: No revenue or expense data for fiscal year {Year}", currentYear);
                return new ScenarioResult($"No data for FY {currentYear}", 0m, -revenueTarget);
            }

            var projectedRevenue = baselineRevenue * (1 + rateIncreasePercent / 100m);
            var projectedExpenses = baselineExpenses * (1 + expenseIncreasePercent / 100m);
            var projectedNet = Math.Round(projectedRevenue - projectedExpenses, 2);

            var varianceToTarget = Math.Round(projectedRevenue - revenueTarget, 2);
            var description = $"FY {currentYear}: +{rateIncreasePercent:0.##}% revenue, +{expenseIncreasePercent:0.##}% expenses vs target {revenueTarget:C0}";

            _logger.LogInformation(
                "Scenario analysis: year={Year}, baselineRevenue={BaselineRevenue}, baselineExpenses={BaselineExpenses}, projectedRevenue={ProjectedRevenue}, projectedExpenses={ProjectedExpenses}, target={Target}",
                currentYear, baselineRevenue, baselineExpenses, projectedRevenue, projectedExpenses, revenueTarget);

            return new ScenarioResult(description, projectedNet, varianceToTarget);
        }
    }
}
