using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service providing analytics capabilities for budget data analysis and scenario modeling
    /// </summary>
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly IMunicipalAccountRepository _accountRepository;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            IBudgetRepository budgetRepository,
            IMunicipalAccountRepository accountRepository,
            ILogger<AnalyticsService> logger)
        {
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs exploratory data analysis on budget data
        /// </summary>
        public async Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, string? entityName = null)
        {
            _logger.LogInformation("Performing exploratory analysis for period {Start} to {End} (Entity={Entity})", startDate, endDate, entityName);

            var budgetEntries = (await _budgetRepository.GetByDateRangeAsync(startDate, endDate)).ToList();
            var accounts = await _accountRepository.GetAllAsync();

            // If an entity name is provided, apply a lightweight heuristic filter to budget entries
            if (!string.IsNullOrWhiteSpace(entityName))
            {
                var sel = entityName.Trim();
                budgetEntries = budgetEntries.Where(be =>
                    (be.Fund != null && !string.IsNullOrWhiteSpace(be.Fund.Name) && string.Equals(be.Fund.Name, sel, StringComparison.OrdinalIgnoreCase))
                    || (sel.IndexOf("Sanitation", StringComparison.OrdinalIgnoreCase) >= 0 && ((be.Fund?.Name?.IndexOf("Sewer", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (be.Fund?.Name?.IndexOf("Sanitation", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))
                    || (sel.IndexOf("Utility", StringComparison.OrdinalIgnoreCase) >= 0 && ((be.Fund?.Name?.IndexOf("Water", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (be.Fund?.Name?.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))
                    || (be.MunicipalAccount != null && be.MunicipalAccount.Name != null && be.MunicipalAccount.Name.IndexOf(sel, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            var result = new BudgetAnalysisResult();

            // Category breakdown
            result.CategoryBreakdown = budgetEntries
                .GroupBy(be => GetCategoryFromAccount(be.AccountNumber, accounts))
                .ToDictionary(g => g.Key, g => g.Sum(be => be.ActualAmount));

            // Top variances
            result.TopVariances = budgetEntries
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

            // Trend analysis
            result.TrendData = AnalyzeTrends(budgetEntries, startDate, endDate);

            // Generate insights
            result.Insights = GenerateInsights(result);

            return result;
        }

        /// <summary>
        /// Runs a what-if scenario for rate adjustments
        /// </summary>
        public async Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            _logger.LogInformation("Running rate scenario with {Rate}% increase, {Expense}% expense increase",
                parameters.RateIncreasePercentage * 100, parameters.ExpenseIncreasePercentage * 100);

            // Get current budget data
            var currentYear = DateTime.Now.Year;
            var startDate = new DateTime(currentYear - 1, 7, 1);
            var endDate = new DateTime(currentYear, 6, 30);

            var budgetSummary = await _budgetRepository.GetBudgetSummaryAsync(startDate, endDate);
            if (budgetSummary == null)
            {
                throw new InvalidOperationException("No budget data available for scenario analysis");
            }

            var result = new RateScenarioResult
            {
                CurrentRate = 0, // Would need to be retrieved from enterprise data
                ProjectedRate = 0, // Calculate based on parameters
                RevenueImpact = budgetSummary.TotalActual * parameters.RateIncreasePercentage,
                ReserveImpact = budgetSummary.TotalVariance * (1 + parameters.ExpenseIncreasePercentage)
            };

            // Generate yearly projections
            for (int i = 1; i <= parameters.ProjectionYears; i++)
            {
                var projection = new YearlyProjection
                {
                    Year = currentYear + i,
                    ProjectedRevenue = budgetSummary.TotalActual * (1 + parameters.RateIncreasePercentage) * (decimal)Math.Pow(1.02, i), // 2% annual growth
                    ProjectedExpenses = budgetSummary.TotalActual * (1 + parameters.ExpenseIncreasePercentage) * (decimal)Math.Pow(1.03, i), // 3% annual growth
                    ProjectedReserves = 0, // Calculate based on revenue - expenses
                    RiskLevel = CalculateRiskLevel(parameters.RateIncreasePercentage, parameters.ExpenseIncreasePercentage)
                };
                projection.ProjectedReserves = projection.ProjectedRevenue - projection.ProjectedExpenses;
                result.Projections.Add(projection);
            }

            // Generate recommendations
            result.Recommendations = GenerateRecommendations(result);

            return result;
        }

        /// <summary>
        /// Generates predictive forecast for budget reserves
        /// </summary>
        public async Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead)
        {
            _logger.LogInformation("Generating reserve forecast for {Years} years ahead", yearsAhead);

            // Simple linear regression based on historical data
            var historicalData = await GetHistoricalReserveDataAsync();

            var result = new ReserveForecastResult
            {
                CurrentReserves = historicalData.LastOrDefault()?.Reserves ?? 0,
                ForecastPoints = new List<ForecastPoint>(),
                RecommendedReserveLevel = 0, // Calculate based on expenses
                RiskAssessment = "Low" // Would be calculated based on variance
            };

            // Generate forecast points using simple trend
            if (historicalData.Count >= 2)
            {
                var trend = CalculateTrend(historicalData);
                var lastDate = historicalData.Last().Date;

                for (int i = 1; i <= yearsAhead * 12; i++)
                {
                    var forecastDate = lastDate.AddMonths(i);
                    var predictedReserves = historicalData.Last().Reserves + (trend * i);

                    result.ForecastPoints.Add(new ForecastPoint
                    {
                        Date = forecastDate,
                        PredictedReserves = Math.Max(0, predictedReserves), // Reserves can't be negative
                        ConfidenceInterval = Math.Abs(predictedReserves * 0.1m) // 10% confidence interval
                    });
                }
            }

            return result;
        }

        private string GetCategoryFromAccount(string accountNumber, IEnumerable<MunicipalAccount> accounts)
        {
            var account = accounts.FirstOrDefault(a => a.AccountNumber?.Value == accountNumber);
            return account?.TypeDescription ?? "Other";
        }

        private string GetAccountName(string accountNumber, IEnumerable<MunicipalAccount> accounts)
        {
            var account = accounts.FirstOrDefault(a => a.AccountNumber?.Value == accountNumber);
            return account?.Name ?? accountNumber;
        }

        private TrendAnalysis AnalyzeTrends(IEnumerable<BudgetEntry> entries, DateTime startDate, DateTime endDate)
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
                if (first > 0)
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

        private List<string> GenerateInsights(BudgetAnalysisResult result)
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

        private decimal CalculateRiskLevel(decimal rateIncrease, decimal expenseIncrease)
        {
            // Simple risk calculation
            return (rateIncrease + expenseIncrease) / 2;
        }

        private List<string> GenerateRecommendations(RateScenarioResult result)
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

        private Task<List<ReserveDataPoint>> GetHistoricalReserveDataAsync()
        {
            // This would query historical reserve data
            // For now, return sample data
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

            return Task.FromResult(data);
        }

        private decimal CalculateTrend(List<ReserveDataPoint> data)
        {
            if (data.Count < 2) return 0;

            var first = data.First();
            var last = data.Last();
            var monthsDouble = (last.Date - first.Date).TotalDays / 30.44;
            var months = (decimal)monthsDouble;
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
