using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Interface for analytics services providing data analysis and scenario modeling
    /// </summary>
    /// <summary>
    /// Represents a interface for ianalyticsservice.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Performs exploratory data analysis on budget data
        /// </summary>
        Task<BudgetAnalysisResult> PerformExploratoryAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a what-if scenario for rate adjustments
        /// </summary>
        Task<RateScenarioResult> RunRateScenarioAsync(RateScenarioParameters parameters);

        /// <summary>
        /// Generates predictive forecast for budget reserves
        /// </summary>
        Task<ReserveForecastResult> GenerateReserveForecastAsync(int yearsAhead);
    }

    /// <summary>
    /// Parameters for rate scenario analysis
    /// </summary>
    /// <summary>
    /// Represents a class for ratescenarioparameters.
    /// </summary>
    public class RateScenarioParameters
    {
        /// <summary>
        /// Gets or sets the rateincreasepercentage.
        /// </summary>
        public decimal RateIncreasePercentage { get; set; }
        /// <summary>
        /// Gets or sets the expenseincreasepercentage.
        /// </summary>
        public decimal ExpenseIncreasePercentage { get; set; }
        /// <summary>
        /// Gets or sets the revenuetargetpercentage.
        /// </summary>
        public decimal RevenueTargetPercentage { get; set; }
        /// <summary>
        /// Gets or sets the projectionyears.
        /// </summary>
        public int ProjectionYears { get; set; } = 3;
    }

    /// <summary>
    /// Result of exploratory budget analysis
    /// </summary>
    /// <summary>
    /// Represents a class for budgetanalysisresult.
    /// </summary>
    public class BudgetAnalysisResult
    {
        public Dictionary<string, decimal> CategoryBreakdown { get; set; } = new();
        public List<VarianceAnalysis> TopVariances { get; set; } = new();
        /// <summary>
        /// Gets or sets the trenddata.
        /// </summary>
        public TrendAnalysis TrendData { get; set; } = new();
        public List<string> Insights { get; set; } = new();
    }

    /// <summary>
    /// Result of rate scenario analysis
    /// </summary>
    /// <summary>
    /// Represents a class for ratescenarioresult.
    /// </summary>
    public class RateScenarioResult
    {
        /// <summary>
        /// Gets or sets the currentrate.
        /// </summary>
        public decimal CurrentRate { get; set; }
        /// <summary>
        /// Gets or sets the projectedrate.
        /// </summary>
        public decimal ProjectedRate { get; set; }
        /// <summary>
        /// Gets or sets the revenueimpact.
        /// </summary>
        public decimal RevenueImpact { get; set; }
        /// <summary>
        /// Gets or sets the reserveimpact.
        /// </summary>
        public decimal ReserveImpact { get; set; }
        public List<YearlyProjection> Projections { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Result of reserve forecast
    /// </summary>
    /// <summary>
    /// Represents a class for reserveforecastresult.
    /// </summary>
    public class ReserveForecastResult
    {
        /// <summary>
        /// Gets or sets the currentreserves.
        /// </summary>
        public decimal CurrentReserves { get; set; }
        public List<ForecastPoint> ForecastPoints { get; set; } = new();
        /// <summary>
        /// Gets or sets the recommendedreservelevel.
        /// </summary>
        public decimal RecommendedReserveLevel { get; set; }
        /// <summary>
        /// Gets or sets the riskassessment.
        /// </summary>
        public string RiskAssessment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Variance analysis for accounts
    /// </summary>
    /// <summary>
    /// Represents a class for varianceanalysis.
    /// </summary>
    public class VarianceAnalysis
    {
        /// <summary>
        /// Gets or sets the accountnumber.
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the accountname.
        /// </summary>
        public string AccountName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the budgetedamount.
        /// </summary>
        public decimal BudgetedAmount { get; set; }
        /// <summary>
        /// Gets or sets the actualamount.
        /// </summary>
        public decimal ActualAmount { get; set; }
        /// <summary>
        /// Gets or sets the varianceamount.
        /// </summary>
        public decimal VarianceAmount { get; set; }
        /// <summary>
        /// Gets or sets the variancepercentage.
        /// </summary>
        public decimal VariancePercentage { get; set; }
    }

    /// <summary>
    /// Trend analysis data
    /// </summary>
    /// <summary>
    /// Represents a class for trendanalysis.
    /// </summary>
    public class TrendAnalysis
    {
        public List<MonthlyTrend> MonthlyTrends { get; set; } = new();
        /// <summary>
        /// Gets or sets the overalltrend.
        /// </summary>
        public string OverallTrend { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the growthrate.
        /// </summary>
        public decimal GrowthRate { get; set; }
    }

    /// <summary>
    /// Monthly trend data
    /// </summary>
    /// <summary>
    /// Represents a class for monthlytrend.
    /// </summary>
    public class MonthlyTrend
    {
        /// <summary>
        /// Gets or sets the month.
        /// </summary>
        public string Month { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the budgeted.
        /// </summary>
        public decimal Budgeted { get; set; }
        /// <summary>
        /// Gets or sets the actual.
        /// </summary>
        public decimal Actual { get; set; }
        /// <summary>
        /// Gets or sets the variance.
        /// </summary>
        public decimal Variance { get; set; }
    }

    /// <summary>
    /// Yearly projection for scenarios
    /// </summary>
    /// <summary>
    /// Represents a class for yearlyprojection.
    /// </summary>
    public class YearlyProjection
    {
        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        public int Year { get; set; }
        /// <summary>
        /// Gets or sets the projectedrevenue.
        /// </summary>
        public decimal ProjectedRevenue { get; set; }
        /// <summary>
        /// Gets or sets the projectedexpenses.
        /// </summary>
        public decimal ProjectedExpenses { get; set; }
        /// <summary>
        /// Gets or sets the projectedreserves.
        /// </summary>
        public decimal ProjectedReserves { get; set; }
        /// <summary>
        /// Gets or sets the risklevel.
        /// </summary>
        public decimal RiskLevel { get; set; }
    }

    /// <summary>
    /// Forecast data point
    /// </summary>
    /// <summary>
    /// Represents a class for forecastpoint.
    /// </summary>
    public class ForecastPoint
    {
        /// <summary>
        /// Gets or sets the date.
        /// </summary>
        public DateTime Date { get; set; }
        /// <summary>
        /// Gets or sets the predictedreserves.
        /// </summary>
        public decimal PredictedReserves { get; set; }
        /// <summary>
        /// Gets or sets the confidenceinterval.
        /// </summary>
        public decimal ConfidenceInterval { get; set; }
    }
}
