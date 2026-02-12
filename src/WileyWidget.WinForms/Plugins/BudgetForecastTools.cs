#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Tools for forecasting next-year municipal budgets with inflation, cost trends, and goal-based adjustments.
    /// Provides JARVIS autonomous capability to generate baseline budget proposals for city clerks.
    /// </summary>
    public sealed class BudgetForecastTools
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BudgetForecastTools>? _logger;
        private readonly IAIService? _aiService;

        public BudgetForecastTools(
            IServiceScopeFactory scopeFactory,
            ILogger<BudgetForecastTools>? logger = null,
            IAIService? aiService = null)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger;
            _aiService = aiService;
        }

        /// <summary>
        /// Generate a proposed budget for next fiscal year, factoring in inflation, cost trends, and selected goals.
        /// Uses historical budget data and applies smart defaults for municipal utility cost escalation.
        /// Returns a baseline proposed budget suitable for city clerk review and adjustment.
        /// </summary>
        [KernelFunction("forecast_next_year_budget")]
        [Description("Generate a proposed budget for next fiscal year with inflation adjustments and goal-based line items. Returns detailed forecast with justifications.")]
        public async Task<BudgetForecastResult> ForecastNextYearBudget(
            [Description("Enterprise ID to forecast budget for")] int enterpriseId,
            [Description("Current fiscal year (e.g., 2025)")] int currentFiscalYear,
            [Description("Inflation rate as decimal (e.g., 0.035 for 3.5%, default: auto-calculated)")] decimal? inflationRate = null,
            [Description("Comma-separated list of goals to fund (e.g., 'New equipment, Staff training, Infrastructure upgrade')")] string? goals = null,
            [Description("Use AI to generate goal-specific budget recommendations")] bool useAiRecommendations = true,
            CancellationToken cancellationToken = default)
        {
            if (enterpriseId <= 0) throw new ArgumentException("Enterprise ID must be greater than zero", nameof(enterpriseId));
            if (currentFiscalYear < 2000 || currentFiscalYear > 2100) throw new ArgumentException("Current fiscal year must be between 2000 and 2100", nameof(currentFiscalYear));

            _logger?.LogInformation("BudgetForecast: Generating forecast for enterprise {EnterpriseId}, FY {CurrentFY} -> {ProposedFY}, inflation={Inflation}, goals={Goals}",
                enterpriseId, currentFiscalYear, currentFiscalYear + 1, inflationRate, goals);

            using var scope = _scopeFactory.CreateScope();
            var enterpriseRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IEnterpriseRepository>(scope.ServiceProvider);
            var budgetRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IBudgetRepository>(scope.ServiceProvider);

            var enterprise = enterpriseRepository != null ? await enterpriseRepository.GetByIdAsync(enterpriseId, cancellationToken) : null;
            if (enterprise == null)
            {
                _logger?.LogWarning("BudgetForecast: Enterprise {EnterpriseId} not found", enterpriseId);
                return new BudgetForecastResult
                {
                    EnterpriseId = enterpriseId,
                    EnterpriseName = "Unknown",
                    CurrentFiscalYear = currentFiscalYear,
                    ProposedFiscalYear = currentFiscalYear + 1,
                    Summary = "Enterprise not found",
                    IsValid = false
                };
            }

            // Fetch current year budget data (all entries for fiscal year)
            var currentBudgetEntries = budgetRepository != null
                ? (await budgetRepository.GetByFiscalYearAsync(currentFiscalYear, cancellationToken))?.ToList()
                : null;

            if (currentBudgetEntries == null || !currentBudgetEntries.Any())
            {
                _logger?.LogWarning("BudgetForecast: No budget data found for enterprise {EnterpriseId} FY {FY}", enterpriseId, currentFiscalYear);
                return new BudgetForecastResult
                {
                    EnterpriseId = enterpriseId,
                    EnterpriseName = enterprise.Name ?? "Unknown",
                    CurrentFiscalYear = currentFiscalYear,
                    ProposedFiscalYear = currentFiscalYear + 1,
                    Summary = "No historical budget data available for forecasting",
                    IsValid = false
                };
            }

            // Calculate or use provided inflation rate
            var effectiveInflationRate = inflationRate ?? await CalculateHistoricalInflationRate(budgetRepository, enterpriseId, currentFiscalYear, cancellationToken);
            _logger?.LogInformation("BudgetForecast: Using inflation rate {Rate:P2}", effectiveInflationRate);

            // Parse goals
            var goalList = string.IsNullOrWhiteSpace(goals)
                ? new List<string>()
                : goals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var proposedLineItems = new List<ProposedLineItem>();
            var assumptions = new List<string>
            {
                $"Base inflation rate: {effectiveInflationRate:P2}",
                "Municipal utilities typically see 2-5% annual cost increase baseline",
                "Labor costs increase faster than inflation (benefits, insurance)",
                "Equipment and infrastructure costs subject to supply chain volatility"
            };

            decimal totalCurrentBudget = 0m;
            decimal totalProposedBudget = 0m;

            // Apply smart escalation to each budget category
            foreach (var entry in currentBudgetEntries)
            {
                var currentAmount = entry.BudgetedAmount;
                totalCurrentBudget += currentAmount;

                // Determine escalation factor based on category
                var escalationFactor = DetermineEscalationFactor(entry, effectiveInflationRate);
                var proposedAmount = Math.Round(currentAmount * (1m + escalationFactor), 2);

                var lineItem = new ProposedLineItem
                {
                    Category = entry.Fund?.Name ?? "General",
                    Description = entry.Description ?? "Budget Line Item",
                    CurrentAmount = currentAmount,
                    ProposedAmount = proposedAmount,
                    Increase = proposedAmount - currentAmount,
                    IncreasePercent = currentAmount > 0 ? ((proposedAmount - currentAmount) / currentAmount) * 100m : 0m,
                    Justification = GetEscalationJustification(entry, escalationFactor, effectiveInflationRate),
                    IsGoalDriven = false
                };

                proposedLineItems.Add(lineItem);
                totalProposedBudget += proposedAmount;
            }

            // Add goal-driven line items
            if (goalList.Any())
            {
                _logger?.LogInformation("BudgetForecast: Processing {Count} goals", goalList.Count);
                var goalBudgets = await GenerateGoalBudgets(enterprise, goalList, currentBudgetEntries, useAiRecommendations, cancellationToken);
                proposedLineItems.AddRange(goalBudgets);
                totalProposedBudget += goalBudgets.Sum(g => g.ProposedAmount);
                assumptions.Add($"Added {goalList.Count} goal-driven budget line items");
            }

            var totalIncrease = totalProposedBudget - totalCurrentBudget;
            var totalIncreasePercent = totalCurrentBudget > 0 ? (totalIncrease / totalCurrentBudget) * 100m : 0m;

            // Fetch historical trends (last 3 years)
            var historicalTrends = budgetRepository != null
                ? await budgetRepository.GetHistoricalBudgetSummaryAsync(3, currentFiscalYear, cancellationToken)
                : new List<HistoricalBudgetYear>();

            var summary = $"Proposed FY {currentFiscalYear + 1} budget for {enterprise.Name}: ${totalProposedBudget:N2} " +
                         $"(+${totalIncrease:N2} / +{totalIncreasePercent:F2}% from FY {currentFiscalYear}). " +
                         $"Includes {proposedLineItems.Count} line items with inflation-adjusted baseline and {goalList.Count} strategic goals.";

            var result = new BudgetForecastResult
            {
                EnterpriseId = enterpriseId,
                EnterpriseName = enterprise.Name ?? "Unknown",
                CurrentFiscalYear = currentFiscalYear,
                ProposedFiscalYear = currentFiscalYear + 1,
                TotalCurrentBudget = totalCurrentBudget,
                TotalProposedBudget = totalProposedBudget,
                TotalIncrease = totalIncrease,
                TotalIncreasePercent = totalIncreasePercent,
                InflationRate = effectiveInflationRate,
                ProposedLineItems = proposedLineItems,
                Goals = goalList,
                Assumptions = assumptions,
                Summary = summary,
                IsValid = true,
                GeneratedDate = DateTime.Now,
                HistoricalTrends = historicalTrends
            };

            _logger?.LogInformation("BudgetForecast: Forecast complete - {LineItems} line items, ${Total:N2} total proposed", proposedLineItems.Count, totalProposedBudget);
            return result;
        }

        private async Task<decimal> CalculateHistoricalInflationRate(IBudgetRepository? budgetRepository, int enterpriseId, int currentFiscalYear, CancellationToken cancellationToken)
        {
            // Try to calculate from historical trends (3-year lookback)
            if (budgetRepository == null) return 0.035m; // Default 3.5%

            try
            {
                var priorYear1 = currentFiscalYear - 1;
                var priorYear2 = currentFiscalYear - 2;

                var currentYearData = await budgetRepository.GetByFiscalYearAsync(currentFiscalYear, cancellationToken);
                var priorYear1Data = await budgetRepository.GetByFiscalYearAsync(priorYear1, cancellationToken);
                var priorYear2Data = await budgetRepository.GetByFiscalYearAsync(priorYear2, cancellationToken);

                var currentTotal = currentYearData?.Sum(b => b.BudgetedAmount) ?? 0m;
                var priorTotal1 = priorYear1Data?.Sum(b => b.BudgetedAmount) ?? 0m;
                var priorTotal2 = priorYear2Data?.Sum(b => b.BudgetedAmount) ?? 0m;

                if (currentTotal > 0 && priorTotal1 > 0)
                {
                    var rate1 = (currentTotal - priorTotal1) / priorTotal1;
                    if (priorTotal1 > 0 && priorTotal2 > 0)
                    {
                        var rate2 = (priorTotal1 - priorTotal2) / priorTotal2;
                        return (rate1 + rate2) / 2m; // Average 2-year trend
                    }
                    return rate1;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "BudgetForecast: Failed to calculate historical inflation rate - using default");
            }

            return 0.035m; // Default 3.5% if no historical data
        }

        private decimal DetermineEscalationFactor(BudgetEntry entry, decimal baseInflationRate)
        {
            // Smart escalation based on category patterns
            var description = (entry.Description ?? string.Empty).ToLowerInvariant();
            var category = (entry.Fund?.Name ?? string.Empty).ToLowerInvariant();

            // Labor costs: base inflation + 2-4% (benefits, insurance increases)
            if (description.Contains("salary") || description.Contains("wage") || description.Contains("payroll") ||
                description.Contains("staff") || description.Contains("personnel") || description.Contains("labor"))
            {
                return baseInflationRate + 0.03m; // +3% on top of inflation
            }

            // Benefits/insurance: base inflation + 4-6% (healthcare escalation)
            if (description.Contains("benefit") || description.Contains("insurance") || description.Contains("health") ||
                description.Contains("medical") || description.Contains("retirement"))
            {
                return baseInflationRate + 0.05m; // +5% on top of inflation
            }

            // Utilities: base inflation + 3-5% (energy costs volatile)
            if (description.Contains("electric") || description.Contains("gas") || description.Contains("utility") ||
                description.Contains("water") || description.Contains("sewer") || description.Contains("power"))
            {
                return baseInflationRate + 0.04m; // +4% on top of inflation
            }

            // Equipment/capital: base inflation + 2-3% (supply chain)
            if (description.Contains("equipment") || description.Contains("vehicle") || description.Contains("capital") ||
                description.Contains("machinery") || description.Contains("infrastructure"))
            {
                return baseInflationRate + 0.025m; // +2.5% on top of inflation
            }

            // Professional services: base inflation + 2%
            if (description.Contains("consult") || description.Contains("legal") || description.Contains("audit") ||
                description.Contains("professional") || description.Contains("service"))
            {
                return baseInflationRate + 0.02m;
            }

            // Default: base inflation + 1% safety margin
            return baseInflationRate + 0.01m;
        }

        private string GetEscalationJustification(BudgetEntry entry, decimal escalationFactor, decimal baseInflationRate)
        {
            var description = (entry.Description ?? string.Empty).ToLowerInvariant();
            var excessRate = escalationFactor - baseInflationRate;

            if (description.Contains("salary") || description.Contains("wage") || description.Contains("staff"))
                return $"Labor cost inflation ({escalationFactor:P2}): base inflation + competitive wage adjustments + benefit increases";

            if (description.Contains("benefit") || description.Contains("insurance") || description.Contains("health"))
                return $"Benefits inflation ({escalationFactor:P2}): base inflation + healthcare cost escalation (industry avg 5-7%)";

            if (description.Contains("electric") || description.Contains("utility") || description.Contains("power"))
                return $"Utility cost inflation ({escalationFactor:P2}): base inflation + energy market volatility + regulatory costs";

            if (description.Contains("equipment") || description.Contains("vehicle") || description.Contains("capital"))
                return $"Equipment cost inflation ({escalationFactor:P2}): base inflation + supply chain impacts + technology upgrades";

            if (excessRate > 0.01m)
                return $"Adjusted inflation ({escalationFactor:P2}): base inflation + category-specific cost pressures";

            return $"Standard inflation adjustment ({escalationFactor:P2})";
        }

        private async Task<List<ProposedLineItem>> GenerateGoalBudgets(Enterprise enterprise, List<string> goals, List<BudgetEntry> historicalData, bool useAi, CancellationToken cancellationToken)
        {
            var goalBudgets = new List<ProposedLineItem>();
            var currentBudgetTotal = historicalData.Sum(b => b.BudgetedAmount);

            foreach (var goal in goals)
            {
                var estimatedAmount = EstimateGoalBudget(goal, currentBudgetTotal);
                var justification = await GenerateGoalJustification(enterprise, goal, estimatedAmount, useAi, cancellationToken);

                goalBudgets.Add(new ProposedLineItem
                {
                    Category = "Strategic Goals",
                    Description = goal,
                    CurrentAmount = 0m, // New line item
                    ProposedAmount = estimatedAmount,
                    Increase = estimatedAmount,
                    IncreasePercent = 0m, // N/A for new items
                    Justification = justification,
                    IsGoalDriven = true
                });
            }

            return goalBudgets;
        }

        private decimal EstimateGoalBudget(string goal, decimal currentBudgetTotal)
        {
            var goalLower = goal.ToLowerInvariant();

            // Smart estimation based on goal keywords
            if (goalLower.Contains("equipment") || goalLower.Contains("vehicle") || goalLower.Contains("truck"))
                return Math.Max(50000m, currentBudgetTotal * 0.05m); // 5% of budget or $50k

            if (goalLower.Contains("infrastructure") || goalLower.Contains("facility") || goalLower.Contains("building"))
                return Math.Max(100000m, currentBudgetTotal * 0.10m); // 10% of budget or $100k

            if (goalLower.Contains("training") || goalLower.Contains("education") || goalLower.Contains("development"))
                return Math.Max(5000m, currentBudgetTotal * 0.01m); // 1% of budget or $5k

            if (goalLower.Contains("software") || goalLower.Contains("technology") || goalLower.Contains("system"))
                return Math.Max(20000m, currentBudgetTotal * 0.03m); // 3% of budget or $20k

            if (goalLower.Contains("staff") || goalLower.Contains("hire") || goalLower.Contains("personnel"))
                return Math.Max(60000m, currentBudgetTotal * 0.08m); // 8% of budget or $60k (salary+benefits)

            // Default: 2% of current budget or $10k minimum
            return Math.Max(10000m, currentBudgetTotal * 0.02m);
        }

        private async Task<string> GenerateGoalJustification(Enterprise enterprise, string goal, decimal estimatedAmount, bool useAi, CancellationToken cancellationToken)
        {
            if (useAi && _aiService != null)
            {
                try
                {
                    var prompt = $"Generate a concise budget justification (2-3 sentences) for this municipal utility goal:\n" +
                                $"Enterprise: {enterprise.Name}\n" +
                                $"Goal: {goal}\n" +
                                $"Estimated Budget: ${estimatedAmount:N2}\n" +
                                $"Focus on operational impact, service improvement, and fiscal responsibility.";

                    var response = await _aiService.SendMessageAsync(prompt, new List<object>(), cancellationToken);
                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        return response.Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "BudgetForecast: AI justification failed for goal '{Goal}' - using default", goal);
                }
            }

            // Fallback: template-based justification
            return $"Strategic investment in {goal} to improve service delivery and operational efficiency. " +
                   $"Estimated cost ${estimatedAmount:N2} based on industry benchmarks and current budget scale. " +
                   $"Essential for maintaining competitive service levels and meeting community needs.";
        }
    }
}
