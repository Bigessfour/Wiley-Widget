using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Interface for analytics services providing data analysis and scenario modeling
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Performs exploratory data analysis on budget data
        /// </summary>
        Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, string? entityName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a what-if scenario for rate adjustments
        /// </summary>
        Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates predictive forecast for budget reserves
        /// </summary>
        Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Parameters for rate scenario analysis
    /// </summary>
    public class RateScenarioParameters
    {
        public decimal RateIncreasePercentage { get; set; }
        public decimal ExpenseIncreasePercentage { get; set; }
        public decimal RevenueTargetPercentage { get; set; }
        public int ProjectionYears { get; set; } = 3;
    }

    /// <summary>
    /// Result of exploratory budget analysis
    /// </summary>
    public class BudgetAnalysisResult
    {
        public Dictionary<string, decimal> CategoryBreakdown { get; set; } = new();
        public List<VarianceAnalysis> TopVariances { get; set; } = new();
        public TrendAnalysis TrendData { get; set; } = new();
        public List<string> Insights { get; set; } = new();
        public List<string> AvailableEntities { get; set; } = new();
    }

    /// <summary>
    /// Result of rate scenario analysis
    /// </summary>
    public class RateScenarioResult
    {
        public decimal CurrentRate { get; set; }
        public decimal ProjectedRate { get; set; }
        public decimal RevenueImpact { get; set; }
        public decimal ReserveImpact { get; set; }
        public List<YearlyProjection> Projections { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Result of reserve forecast
    /// </summary>
    public class ReserveForecastResult
    {
        public decimal CurrentReserves { get; set; }
        public List<ForecastPoint> ForecastPoints { get; set; } = new();
        public decimal RecommendedReserveLevel { get; set; }
        public string RiskAssessment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Variance analysis for accounts
    /// </summary>
    public class VarianceAnalysis
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal BudgetedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal VarianceAmount { get; set; }
        public decimal VariancePercentage { get; set; }
    }

    /// <summary>
    /// Trend analysis data
    /// </summary>
    public class TrendAnalysis
    {
        public List<MonthlyTrend> MonthlyTrends { get; set; } = new();
        public string OverallTrend { get; set; } = string.Empty;
        public decimal GrowthRate { get; set; }
    }

    /// <summary>
    /// Monthly trend data
    /// </summary>
    public class MonthlyTrend
    {
        public string Month { get; set; } = string.Empty;
        public decimal Budgeted { get; set; }
        public decimal Actual { get; set; }
        public decimal Variance { get; set; }
    }

    /// <summary>
    /// Yearly projection for scenarios
    /// </summary>
    public class YearlyProjection
    {
        public int Year { get; set; }
        public decimal ProjectedRevenue { get; set; }
        public decimal ProjectedExpenses { get; set; }
        public decimal ProjectedReserves { get; set; }
        public decimal RiskLevel { get; set; }
    }

    /// <summary>
    /// Forecast data point
    /// </summary>
    public class ForecastPoint
    {
        public DateTime Date { get; set; }
        public decimal PredictedReserves { get; set; }
        public decimal ConfidenceInterval { get; set; }
    }
}
