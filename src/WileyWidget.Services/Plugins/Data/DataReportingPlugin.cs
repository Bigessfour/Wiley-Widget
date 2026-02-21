using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Plugins.Data
{
    public class DataReportingPlugin
    {
        private readonly IAnalyticsService _analyticsService;

        public DataReportingPlugin(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        }

        [KernelFunction]
        [Description("Performs an exploratory analysis of budget data for a given period.")]
        public async Task<string> AnalyzeBudgetAsync([Description("The start date for the analysis (yyyy-MM-dd).")] DateTime startDate,
            [Description("The end date for the analysis (yyyy-MM-dd).")] DateTime endDate,
            [Description("Optional entity name to filter by (e.g. 'Water Fund').")] string? entityName = null, CancellationToken cancellationToken = default)
        {
            var result = await _analyticsService.PerformExploratoryAnalysisAsync(startDate, endDate, entityName);

            // Format result as a readable summary for the LLM
            var sb = new StringBuilder();
            sb.AppendLine($"Budget Analysis ({startDate:d} to {endDate:d})");
            sb.AppendLine($"Data Trend: {result.TrendData.OverallTrend} (Growth: {result.TrendData.GrowthRate:P2})");

            if (result.Insights.Any())
            {
                sb.AppendLine("Key Insights:");
                foreach (var insight in result.Insights)
                    sb.AppendLine($"- {insight}");
            }

            if (result.TopVariances.Any())
            {
                sb.AppendLine("Top Variances:");
                foreach (var v in result.TopVariances.Take(5))
                {
                    sb.AppendLine($"- {v.AccountName}: Budget {v.BudgetedAmount:C}, Actual {v.ActualAmount:C}, Variance {v.VariancePercentage:F1}%");
                }
            }

            return sb.ToString();
        }

        [KernelFunction]
        [Description("Runs a 'What-If' scenario to project revenue and reserves based on rate changes.")]
        public async Task<string> RunRateScenarioAsync([Description("The percentage increase in rates (e.g. 0.05 for 5%).")] decimal rateIncrease,
            [Description("The percentage increase in expenses (e.g. 0.03 for 3%).")] decimal expenseIncrease,
            [Description("Number of years to project.")] int years = 5, CancellationToken cancellationToken = default)
        {
            var parameters = new RateScenarioParameters
            {
                RateIncreasePercentage = rateIncrease,
                ExpenseIncreasePercentage = expenseIncrease,
                ProjectionYears = years
            };

            var result = await _analyticsService.RunRateScenarioAsync(parameters);

            var sb = new StringBuilder();
            sb.AppendLine($"Scenario Results (Rate +{rateIncrease:P0}, Exp +{expenseIncrease:P0}):");

            foreach (var rec in result.Recommendations)
            {
                sb.AppendLine($"- Recommendation: {rec}");
            }

            sb.AppendLine("Projections:");
            foreach (var p in result.Projections)
            {
                sb.AppendLine($"Year {p.Year}: Rev {p.ProjectedRevenue:C0}, Exp {p.ProjectedExpenses:C0}, Rsrv {p.ProjectedReserves:C0}");
            }

            return sb.ToString();
        }
    }
}
