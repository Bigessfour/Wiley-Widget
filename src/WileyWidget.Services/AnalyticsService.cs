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
using WileyWidget.Business.Interfaces;
using Polly;
using Polly.Retry;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service providing analytics capabilities for budget data analysis and scenario modeling
    /// backed by repository abstractions for better testability and separation of concerns.
    /// </summary>
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly IAnalyticsRepository _analyticsRepository;
        private readonly IBudgetAnalyticsRepository _budgetAnalyticsRepository;
        private readonly ILogger<AnalyticsService> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public AnalyticsService(
            IBudgetRepository budgetRepository,
            IAnalyticsRepository analyticsRepository,
            IBudgetAnalyticsRepository budgetAnalyticsRepository,
            ILogger<AnalyticsService> logger)
        {
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _analyticsRepository = analyticsRepository ?? throw new ArgumentNullException(nameof(analyticsRepository));
            _budgetAnalyticsRepository = budgetAnalyticsRepository ?? throw new ArgumentNullException(nameof(budgetAnalyticsRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure retry policy for transient failures
            _retryPolicy = Policy
                .Handle<DbUpdateException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, "Retry {RetryCount} after {Delay}ms", retryCount, timeSpan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Performs exploratory data analysis on budget data
        /// </summary>
        public async Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, string? entityName = null, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Performing exploratory analysis for period {Start} to {End} (Entity={Entity})", startDate, endDate, entityName);

                // Get analytics-specific data from IAnalyticsRepository
                var accounts = await _analyticsRepository.GetMunicipalAccountsAsync(cancellationToken);
                var availableEntities = await _analyticsRepository.GetAvailableEntitiesAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                var result = new BudgetAnalysisResult
                {
                    CategoryBreakdown = await _budgetAnalyticsRepository.GetCategoryBreakdownAsync(startDate, endDate, entityName, cancellationToken),
                    TopVariances = await _budgetAnalyticsRepository.GetTopVariancesAsync(10, startDate.Year, cancellationToken), // Top 10 for current fiscal year
                    TrendData = await _budgetAnalyticsRepository.GetTrendAnalysisAsync(startDate, endDate, cancellationToken),
                    Insights = new List<string>(),
                    AvailableEntities = availableEntities.ToList()
                };

                result.Insights = GenerateInsights(result);
                return result;
            });
        }

        /// <summary>
        /// Runs a what-if scenario for rate adjustments
        /// </summary>
        public async Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(parameters);
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation("Running rate scenario with {Rate}% increase, {Expense}% expense increase",
                    parameters.RateIncreasePercentage * 100, parameters.ExpenseIncreasePercentage * 100);

                var currentYear = DateTime.Now.Year;
                var startDate = new DateTime(currentYear - 1, 7, 1);
                var endDate = new DateTime(currentYear, 6, 30);

                var budgetEntries = await _budgetRepository.GetByDateRangeAsync(startDate, endDate, cancellationToken);

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
            });
        }

        /// <summary>
        /// Generates predictive forecast for budget reserves
        /// </summary>
        public async Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation("Generating reserve forecast for {Years} years ahead", yearsAhead);

                var endDate = DateTime.Now;
                var startDate = endDate.AddYears(-2); // Look back 2 years for historical data

                var historicalData = await _budgetAnalyticsRepository.GetReserveHistoryAsync(startDate, endDate, cancellationToken);
                var currentReserves = await _analyticsRepository.GetCurrentReserveBalanceAsync(cancellationToken);

                var result = new ReserveForecastResult
                {
                    CurrentReserves = currentReserves,
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

                    // Calculate recommended reserve level (typically 25-50% of annual budget)
                    var annualBudget = await GetAnnualBudgetAsync(cancellationToken);
                    result.RecommendedReserveLevel = annualBudget * 0.25m; // 25% of annual budget

                    result.RiskAssessment = AssessRiskLevel(trend, currentReserves, result.RecommendedReserveLevel);
                }
                else
                {
                    _logger.LogWarning("Insufficient historical data for reserve forecasting");
                    result.RiskAssessment = "Insufficient Data";
                }

                return result;
            });
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

        private async Task<decimal> GetAnnualBudgetAsync(CancellationToken cancellationToken)
        {
            var currentYear = DateTime.Now.Year;
            var startDate = new DateTime(currentYear, 7, 1);
            var endDate = new DateTime(currentYear + 1, 6, 30);

            var budgetEntries = await _budgetRepository.GetByDateRangeAsync(startDate, endDate, cancellationToken);
            return budgetEntries.Sum(be => be.BudgetedAmount);
        }

        private static string AssessRiskLevel(decimal trend, decimal currentReserves, decimal recommendedLevel)
        {
            if (currentReserves < recommendedLevel * 0.5m)
                return "High - Reserves critically low";
            if (currentReserves < recommendedLevel)
                return "Medium - Reserves below recommended level";
            if (trend < 0)
                return "Medium - Reserves declining";
            return "Low - Reserves adequate";
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

        /// <summary>
        /// Gets budget overview data for the specified fiscal year
        /// </summary>
        public async Task<BudgetOverviewData> GetBudgetOverviewAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var year = fiscalYear ?? DateTime.Now.Year;
                var data = await _budgetAnalyticsRepository.GetBudgetOverviewDataAsync(year, cancellationToken);

                return new BudgetOverviewData
                {
                    TotalBudget = data.TotalBudget,
                    TotalActual = data.TotalActual,
                    TotalVariance = data.TotalVariance,
                    OverBudgetCount = data.OverBudgetCount,
                    UnderBudgetCount = data.UnderBudgetCount
                };
            });
        }

        /// <summary>
        /// Gets budget metrics for grid display
        /// </summary>
        public async Task<List<BudgetMetric>> GetBudgetMetricsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var year = fiscalYear ?? DateTime.Now.Year;
                var data = await _budgetAnalyticsRepository.GetBudgetMetricsAsync(year, cancellationToken);

                return data.Select(d => new BudgetMetric(
                    d.Name,
                    d.Value,
                    d.DepartmentName,
                    d.BudgetedAmount,
                    d.Amount,
                    d.Variance,
                    d.VariancePercent,
                    d.IsOverBudget)).ToList();
            });
        }

        /// <summary>
        /// Gets summary KPIs for dashboard display
        /// </summary>
        public async Task<List<SummaryKpi>> GetSummaryKpisAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var year = fiscalYear ?? DateTime.Now.Year;
                var data = await _budgetAnalyticsRepository.GetSummaryKpisAsync(year, cancellationToken);

                return data.Select(d => new SummaryKpi(d.Title, d.Value, d.Format, d.IsPositive)).ToList();
            });
        }

        /// <summary>
        /// Gets trend data for forecasting
        /// </summary>
        public async Task<List<TrendSeries>> GetTrendDataAsync(int projectionYears = 3, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var data = await _analyticsRepository.GetTrendDataAsync(projectionYears, cancellationToken);

                return data.Select(d => new TrendSeries(d.Name, d.Points.Select(p => new TrendPoint(p.Date, p.Value)).ToList())).ToList();
            });
        }

        /// <summary>
        /// Runs a scenario analysis
        /// </summary>
        public async Task<ScenarioResult> RunScenarioAsync(decimal rateIncreasePercent, decimal expenseIncreasePercent, decimal revenueTarget, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var result = await _analyticsRepository.RunScenarioAsync(rateIncreasePercent, expenseIncreasePercent, revenueTarget, cancellationToken);

                return new ScenarioResult(result.Description, result.ProjectedValue, result.Variance);
            });
        }

        /// <summary>
        /// Gets detailed variance records
        /// </summary>
        public async Task<List<VarianceRecord>> GetVarianceDetailsAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var year = fiscalYear ?? DateTime.Now.Year;
                var data = await _budgetAnalyticsRepository.GetVarianceDetailsAsync(year, cancellationToken);

                return data.Select(d => new VarianceRecord(d.Department, d.Account, d.Budget, d.Actual, d.Variance, d.VariancePercent)).ToList();
            });
        }
    }
}
