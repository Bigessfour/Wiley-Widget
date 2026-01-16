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
    /// Service providing analytics capabilities for budget data analysis and scenario modeling
    /// backed by fresh DbContext instances created via IDbContextFactory to avoid disposed scopes.
    /// </summary>
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<AnalyticsService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs exploratory data analysis on budget data
        /// </summary>
        public async Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, string? entityName = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Performing exploratory analysis for period {Start} to {End} (Entity={Entity})", startDate, endDate, entityName);

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var budgetEntries = await context.BudgetEntries
                .AsNoTracking()
                .Include(be => be.Fund)
                .Include(be => be.MunicipalAccount)
                .Where(be => be.StartPeriod >= startDate && be.EndPeriod <= endDate)
                .ToListAsync(cancellationToken);

            var accounts = await context.MunicipalAccounts.AsNoTracking().ToListAsync(cancellationToken);
            var enterprises = await context.Enterprises.AsNoTracking().ToListAsync(cancellationToken);
            var availableEntities = BuildAvailableEntities(budgetEntries, enterprises);

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                budgetEntries = FilterEntriesByEntityName(budgetEntries, entityName);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var result = new BudgetAnalysisResult
            {
                CategoryBreakdown = budgetEntries
                    .GroupBy(be => GetCategoryFromAccount(be.AccountNumber, accounts))
                    .ToDictionary(g => g.Key, g => g.Sum(be => be.ActualAmount)),
                TopVariances = BuildTopVariances(budgetEntries, accounts),
                TrendData = AnalyzeTrends(budgetEntries),
                Insights = new List<string>(),
                AvailableEntities = availableEntities
            };

            result.Insights = GenerateInsights(result);
            return result;
        }

        /// <summary>
        /// Runs a what-if scenario for rate adjustments
        /// </summary>
        public async Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Running rate scenario with {Rate}% increase, {Expense}% expense increase",
                parameters.RateIncreasePercentage * 100, parameters.ExpenseIncreasePercentage * 100);

            var currentYear = DateTime.Now.Year;
            var startDate = new DateTime(currentYear - 1, 7, 1);
            var endDate = new DateTime(currentYear, 6, 30);

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var budgetEntries = await context.BudgetEntries
                .AsNoTracking()
                .Where(be => be.CreatedAt >= startDate && be.CreatedAt <= endDate)
                .ToListAsync(cancellationToken);

            if (!budgetEntries.Any())
            {
                throw new InvalidOperationException("No budget data available for scenario analysis");
            }

            var totalActual = budgetEntries.Sum(be => be.ActualAmount);
            var totalBudgeted = budgetEntries.Sum(be => be.BudgetedAmount);
            var variance = totalBudgeted - totalActual;

            var result = new RateScenarioResult
            {
                CurrentRate = 0, // Placeholder until rate source is available
                ProjectedRate = 0,
                RevenueImpact = totalActual * parameters.RateIncreasePercentage,
                ReserveImpact = variance * (1 + parameters.ExpenseIncreasePercentage)
            };

            for (int i = 1; i <= parameters.ProjectionYears; i++)
            {
                var projection = new YearlyProjection
                {
                    Year = currentYear + i,
                    ProjectedRevenue = totalActual * (1 + parameters.RateIncreasePercentage) * (decimal)Math.Pow(1.02, i),
                    ProjectedExpenses = totalActual * (1 + parameters.ExpenseIncreasePercentage) * (decimal)Math.Pow(1.03, i),
                    RiskLevel = CalculateRiskLevel(parameters.RateIncreasePercentage, parameters.ExpenseIncreasePercentage)
                };
                projection.ProjectedReserves = projection.ProjectedRevenue - projection.ProjectedExpenses;
                result.Projections.Add(projection);
            }

            result.Recommendations = GenerateRecommendations(result);
            return result;
        }

        /// <summary>
        /// Generates predictive forecast for budget reserves
        /// </summary>
        public async Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Generating reserve forecast for {Years} years ahead", yearsAhead);

            var historicalData = await GetHistoricalReserveDataAsync(cancellationToken);

            var result = new ReserveForecastResult
            {
                CurrentReserves = historicalData.LastOrDefault()?.Reserves ?? 0,
                ForecastPoints = new List<ForecastPoint>(),
                RecommendedReserveLevel = 0,
                RiskAssessment = "Low"
            };

            if (historicalData.Count >= 2)
            {
                var trend = CalculateTrend(historicalData);
                var lastDate = historicalData.Last().Date;
                var lastReserves = historicalData.Last().Reserves;

                for (int i = 1; i <= yearsAhead * 12; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var forecastDate = lastDate.AddMonths(i);
                    var predictedReserves = lastReserves + (trend * i);

                    result.ForecastPoints.Add(new ForecastPoint
                    {
                        Date = forecastDate,
                        PredictedReserves = Math.Max(0, predictedReserves),
                        ConfidenceInterval = Math.Abs(predictedReserves * 0.1m)
                    });
                }
            }

            return result;
        }

        private static List<string> BuildAvailableEntities(IEnumerable<BudgetEntry> budgetEntries, IEnumerable<Enterprise> enterprises)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fundName in budgetEntries.Select(be => be.Fund?.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                names.Add(fundName!.Trim());
            }

            foreach (var enterpriseName in enterprises.Select(e => e.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                names.Add(enterpriseName!.Trim());
            }

            return names.OrderBy(n => n).ToList();
        }

        private static List<BudgetEntry> FilterEntriesByEntityName(IEnumerable<BudgetEntry> entries, string entityName)
        {
            var trimmed = entityName.Trim();
            return entries.Where(be =>
                (!string.IsNullOrWhiteSpace(be.Fund?.Name) && string.Equals(be.Fund!.Name, trimmed, StringComparison.OrdinalIgnoreCase)) ||
                (trimmed.Contains("Sanitation", StringComparison.OrdinalIgnoreCase) && (be.Fund?.Name?.Contains("Sewer", StringComparison.OrdinalIgnoreCase) == true || be.Fund?.Name?.Contains("Sanitation", StringComparison.OrdinalIgnoreCase) == true)) ||
                (trimmed.Contains("Utility", StringComparison.OrdinalIgnoreCase) && (be.Fund?.Name?.Contains("Water", StringComparison.OrdinalIgnoreCase) == true || be.Fund?.Name?.Contains("Trash", StringComparison.OrdinalIgnoreCase) == true)) ||
                (!string.IsNullOrWhiteSpace(be.MunicipalAccount?.Name) && be.MunicipalAccount!.Name!.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();
        }

        private static List<VarianceAnalysis> BuildTopVariances(IEnumerable<BudgetEntry> budgetEntries, IEnumerable<MunicipalAccount> accounts)
        {
            return budgetEntries
                .Where(be => be.BudgetedAmount > 0)
                .Select(be => new VarianceAnalysis
                {
                    AccountNumber = be.AccountNumber,
                    AccountName = GetAccountName(be.AccountNumber, accounts),
                    BudgetedAmount = be.BudgetedAmount,
                    ActualAmount = be.ActualAmount,
                    VarianceAmount = be.ActualAmount - be.BudgetedAmount,
                    VariancePercentage = be.BudgetedAmount > 0 ? ((be.ActualAmount - be.BudgetedAmount) / be.BudgetedAmount) * 100 : 0
                })
                .OrderByDescending(v => Math.Abs(v.VarianceAmount))
                .Take(10)
                .ToList();
        }

        private static TrendAnalysis AnalyzeTrends(IEnumerable<BudgetEntry> entries)
        {
            var monthlyData = entries
                .GroupBy(be => new { be.StartPeriod.Year, be.StartPeriod.Month })
                .Select(g => new MonthlyTrend
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Budgeted = g.Sum(be => be.BudgetedAmount),
                    Actual = g.Sum(be => be.ActualAmount),
                    Variance = g.Sum(be => be.ActualAmount - be.BudgetedAmount)
                })
                .OrderBy(mt => mt.Month)
                .ToList();

            var growthRate = 0m;
            if (monthlyData.Count >= 2)
            {
                var first = monthlyData.First().Actual;
                var last = monthlyData.Last().Actual;
                if (first != 0)
                {
                    growthRate = ((last - first) / first) / monthlyData.Count * 12; // Annualized
                }
            }

            return new TrendAnalysis
            {
                MonthlyTrends = monthlyData,
                OverallTrend = growthRate > 0 ? "Increasing" : growthRate < 0 ? "Decreasing" : "Stable",
                GrowthRate = growthRate
            };
        }

        private static List<string> GenerateInsights(BudgetAnalysisResult result)
        {
            var insights = new List<string>();

            if (result.TopVariances.Any())
            {
                var largestVariance = result.TopVariances.First();
                insights.Add($"Largest budget variance: {largestVariance.AccountName} (${Math.Abs(largestVariance.VarianceAmount):N0})");
            }

            if (result.TrendData.GrowthRate > 0.05m)
            {
                insights.Add($"Expenses are growing rapidly ({result.TrendData.GrowthRate:P1} annually)");
            }

            var overBudgetCategories = result.CategoryBreakdown.Where(kvp => kvp.Value < 0).ToList();
            if (overBudgetCategories.Any())
            {
                insights.Add($"{overBudgetCategories.Count} categories are over budget");
            }

            return insights;
        }

        private static decimal CalculateRiskLevel(decimal rateIncrease, decimal expenseIncrease) => (rateIncrease + expenseIncrease) / 2;

        private static List<string> GenerateRecommendations(RateScenarioResult result)
        {
            var recommendations = new List<string>();

            if (result.RevenueImpact > result.ReserveImpact)
            {
                recommendations.Add("Rate increase should cover expense growth adequately");
            }
            else
            {
                recommendations.Add("Consider additional revenue measures or expense controls");
            }

            if (result.Projections.Any(p => p.ProjectedReserves < 0))
            {
                recommendations.Add("Warning: Negative reserves projected in future years");
            }

            return recommendations;
        }

        private static string GetCategoryFromAccount(string accountNumber, IEnumerable<MunicipalAccount> accounts)
        {
            var account = accounts.FirstOrDefault(a => a.AccountNumber?.Value == accountNumber);
            return account?.TypeDescription ?? "Other";
        }

        private static string GetAccountName(string accountNumber, IEnumerable<MunicipalAccount> accounts)
        {
            var account = accounts.FirstOrDefault(a => a.AccountNumber?.Value == accountNumber);
            return account?.Name ?? accountNumber;
        }

        private Task<List<ReserveDataPoint>> GetHistoricalReserveDataAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CalculateDefaultReservePoints());
        }

        private static List<ReserveDataPoint> CalculateDefaultReservePoints()
        {
            var data = new List<ReserveDataPoint>();
            var currentDate = DateTime.Now.AddYears(-2);

            for (int i = 0; i < 24; i++)
            {
                data.Add(new ReserveDataPoint
                {
                    Date = currentDate.AddMonths(i),
                    Reserves = 100000 + (i * 1000) // Sample increasing reserves
                });
            }

            return data;
        }

        private static decimal CalculateTrend(IReadOnlyList<ReserveDataPoint> data)
        {
            if (data.Count < 2) return 0;

            var first = data.First();
            var last = data.Last();
            var months = (decimal)((last.Date - first.Date).TotalDays / 30.44);
            if (months == 0) return 0;

            return (last.Reserves - first.Reserves) / months;
        }

        private class ReserveDataPoint
        {
            public DateTime Date { get; set; }
            public decimal Reserves { get; set; }
        }
    }
}
