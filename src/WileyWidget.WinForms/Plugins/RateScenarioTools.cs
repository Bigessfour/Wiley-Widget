#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Provides safe, read-only rate scenario analysis and what-if planning tools for kernel-based agents.
    /// All operations perform financial projections and recommendations; no mutations are permitted.
    /// Injects rate calculation and scenario planning services with comprehensive logging.
    /// </summary>
    public sealed class RateScenarioTools
    {
        private readonly IChargeCalculatorService _chargeCalculatorService;
        private readonly IWhatIfScenarioEngine _whatIfScenarioEngine;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RateScenarioTools>? _logger;

        /// <summary>
        /// Initializes a new instance of the RateScenarioTools plugin.
        /// </summary>
        /// <param name="chargeCalculatorService">Service for calculating recommended service charges.</param>
        /// <param name="whatIfScenarioEngine">Engine for generating comprehensive what-if scenarios.</param>
        /// <param name="scopeFactory">Factory for creating service scopes to resolve scoped services per-call.</param>
        /// <param name="logger">Optional logger for audit and diagnostic logging.</param>
        public RateScenarioTools(
            IChargeCalculatorService chargeCalculatorService,
            IWhatIfScenarioEngine whatIfScenarioEngine,
            IServiceScopeFactory scopeFactory,
            ILogger<RateScenarioTools>? logger = null)
        {
            _chargeCalculatorService = chargeCalculatorService ?? throw new ArgumentNullException(nameof(chargeCalculatorService));
            _whatIfScenarioEngine = whatIfScenarioEngine ?? throw new ArgumentNullException(nameof(whatIfScenarioEngine));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger;
        }

        /// <summary>
        /// Result of a rate recommendation calculation.
        /// </summary>
        [Description("Rate recommendation analysis result")]
        public sealed class RecommendationResult
        {
            [JsonPropertyName("enterpriseId")]
            public int EnterpriseId { get; set; }

            [JsonPropertyName("enterpriseName")]
            public string EnterpriseName { get; set; } = string.Empty;

            [JsonPropertyName("currentRate")]
            public decimal CurrentRate { get; set; }

            [JsonPropertyName("recommendedRate")]
            public decimal RecommendedRate { get; set; }

            [JsonPropertyName("rateIncreaseAmount")]
            public decimal RateIncreaseAmount { get; set; }

            [JsonPropertyName("rateIncreasePercentage")]
            public decimal RateIncreasePercentage { get; set; }

            [JsonPropertyName("targetMargin")]
            public decimal TargetMargin { get; set; }

            [JsonPropertyName("totalMonthlyExpenses")]
            public decimal TotalMonthlyExpenses { get; set; }

            [JsonPropertyName("monthlyRevenueAtRecommended")]
            public decimal MonthlyRevenueAtRecommended { get; set; }

            [JsonPropertyName("monthlySurplus")]
            public decimal MonthlySurplus { get; set; }

            [JsonPropertyName("breakEvenRate")]
            public decimal BreakEvenRate { get; set; }

            [JsonPropertyName("citizenCount")]
            public int CitizenCount { get; set; }

            [JsonPropertyName("rationale")]
            public string Rationale { get; set; } = string.Empty;

            [JsonPropertyName("isValid")]
            public bool IsValid { get; set; }

            [JsonPropertyName("validationReason")]
            public string ValidationReason { get; set; } = string.Empty;

            [JsonPropertyName("warnings")]
            public List<string> Warnings { get; set; } = new();

            [JsonPropertyName("assumptions")]
            public List<string> Assumptions { get; set; } = new();

            [JsonPropertyName("calculationDate")]
            public DateTime CalculationDate { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Result of a what-if scenario analysis.
        /// </summary>
        [Description("What-if scenario analysis result")]
        public sealed class WhatIfScenarioResult
        {
            [JsonPropertyName("scenarioName")]
            public string ScenarioName { get; set; } = string.Empty;

            [JsonPropertyName("scenarioDescription")]
            public string ScenarioDescription { get; set; } = string.Empty;

            [JsonPropertyName("baselineRate")]
            public decimal BaselineRate { get; set; }

            [JsonPropertyName("baselineMonthlyExpenses")]
            public decimal BaselineMonthlyExpenses { get; set; }

            [JsonPropertyName("baselineMonthlyBalance")]
            public decimal BaselineMonthlyBalance { get; set; }

            [JsonPropertyName("projectionYears")]
            public int ProjectionYears { get; set; }

            [JsonPropertyName("rateIncreasePercentage")]
            public decimal RateIncreasePercentage { get; set; }

            [JsonPropertyName("projectedRate")]
            public decimal ProjectedRate { get; set; }

            [JsonPropertyName("projectedMonthlyExpenses")]
            public decimal ProjectedMonthlyExpenses { get; set; }

            [JsonPropertyName("projectedMonthlyRevenue")]
            public decimal ProjectedMonthlyRevenue { get; set; }

            [JsonPropertyName("projectedMonthlyBalance")]
            public decimal ProjectedMonthlyBalance { get; set; }

            [JsonPropertyName("totalExpenseGrowth")]
            public decimal TotalExpenseGrowth { get; set; }

            [JsonPropertyName("balanceChange")]
            public decimal BalanceChange { get; set; }

            [JsonPropertyName("breakEvenPoint")]
            public int BreakEvenMonth { get; set; }

            [JsonPropertyName("riskLevel")]
            public string RiskLevel { get; set; } = "Low";

            [JsonPropertyName("concerns")]
            public List<string> Concerns { get; set; } = new();

            [JsonPropertyName("recommendations")]
            public List<string> Recommendations { get; set; } = new();

            [JsonPropertyName("monthlyProgression")]
            public List<MonthlyProjection> MonthlyProgression { get; set; } = new();

            [JsonPropertyName("generatedDate")]
            public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Monthly projection data point for scenarios.
        /// </summary>
        [Description("Single month's financial projection")]
        public sealed class MonthlyProjection
        {
            [JsonPropertyName("month")]
            public int Month { get; set; }

            [JsonPropertyName("year")]
            public int Year { get; set; }

            [JsonPropertyName("monthlyRate")]
            public decimal MonthlyRate { get; set; }

            [JsonPropertyName("monthlyRevenue")]
            public decimal MonthlyRevenue { get; set; }

            [JsonPropertyName("monthlyExpenses")]
            public decimal MonthlyExpenses { get; set; }

            [JsonPropertyName("monthlyBalance")]
            public decimal MonthlyBalance { get; set; }

            [JsonPropertyName("cumulativeBalance")]
            public decimal CumulativeBalance { get; set; }
        }

        /// <summary>
        /// Result of a reserve forecast analysis.
        /// </summary>
        [Description("Reserve impact forecast result")]
        public sealed class ReserveForecastResult
        {
            [JsonPropertyName("enterpriseId")]
            public int EnterpriseId { get; set; }

            [JsonPropertyName("enterpriseName")]
            public string EnterpriseName { get; set; } = string.Empty;

            [JsonPropertyName("currentReserve")]
            public decimal CurrentReserve { get; set; }

            [JsonPropertyName("annualContribution")]
            public decimal AnnualContribution { get; set; }

            [JsonPropertyName("monthlyContribution")]
            public decimal MonthlyContribution { get; set; }

            [JsonPropertyName("projectionYears")]
            public int ProjectionYears { get; set; }

            [JsonPropertyName("projectedReserveAtEnd")]
            public decimal ProjectedReserveAtEnd { get; set; }

            [JsonPropertyName("targetReserveLevel")]
            public decimal TargetReserveLevel { get; set; }

            [JsonPropertyName("reserveCoverageMonths")]
            public decimal ReserveCoverageMonths { get; set; }

            [JsonPropertyName("targetCoverageMonths")]
            public decimal TargetCoverageMonths { get; set; }

            [JsonPropertyName("isOnTrack")]
            public bool IsOnTrack { get; set; }

            [JsonPropertyName("yearlyBreakdown")]
            public List<YearlyReserveProjection> YearlyBreakdown { get; set; } = new();

            [JsonPropertyName("insights")]
            public List<string> Insights { get; set; } = new();

            [JsonPropertyName("recommendations")]
            public List<string> Recommendations { get; set; } = new();

            [JsonPropertyName("generatedDate")]
            public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Yearly reserve projection data point.
        /// </summary>
        [Description("Annual reserve projection")]
        public sealed class YearlyReserveProjection
        {
            [JsonPropertyName("year")]
            public int Year { get; set; }

            [JsonPropertyName("beginningBalance")]
            public decimal BeginningBalance { get; set; }

            [JsonPropertyName("annualContribution")]
            public decimal AnnualContribution { get; set; }

            [JsonPropertyName("endingBalance")]
            public decimal EndingBalance { get; set; }

            [JsonPropertyName("monthsOfCoverage")]
            public decimal MonthsOfCoverage { get; set; }
        }

        /// <summary>
        /// Calculates recommended rates based on expenses and target margin.
        /// </summary>
        /// <param name="expenses">Dictionary mapping department/category names to their expenses.</param>
        /// <param name="targetMargin">Target margin percentage (default: 15%).</param>
        /// <returns>Rate recommendation result as JSON.</returns>
        [KernelFunction("calculate_recommended_rates")]
        [Description("Calculate recommended service rates based on expenses and target profit margin. Returns structured JSON.")]
        public RecommendationResult CalculateRecommendedRates(
            [Description("Dictionary of expenses (e.g., {'Personnel': 50000, 'Maintenance': 20000})")] Dictionary<string, decimal> expenses,
            [Description("Target margin percentage (default: 15)")] decimal targetMargin = 15m)
        {
            if (expenses == null || expenses.Count == 0)
            {
                throw new ArgumentException("Expenses dictionary cannot be null or empty.", nameof(expenses));
            }

            if (targetMargin < 0 || targetMargin > 100)
            {
                throw new ArgumentException("Target margin must be between 0 and 100.", nameof(targetMargin));
            }

            _logger?.LogInformation(
                "RateScenarioTools: CalculateRecommendedRates invoked with {ExpenseCount} categories, margin={TargetMargin}%",
                expenses.Count,
                targetMargin);

            try
            {
                var totalExpenses = expenses.Values.Sum();
                var marginFactor = 1m + (targetMargin / 100m);
                var requiredRevenue = totalExpenses * marginFactor;

                var rationale = $"Based on total expenses of ${totalExpenses:F2} with {targetMargin}% margin target, " +
                    $"the required revenue is ${requiredRevenue:F2} annually.";

                var result = new RecommendationResult
                {
                    EnterpriseId = 0,
                    EnterpriseName = "Generic Analysis",
                    CurrentRate = 0,
                    RecommendedRate = requiredRevenue / 12m,
                    RateIncreaseAmount = 0,
                    RateIncreasePercentage = 0,
                    TargetMargin = targetMargin,
                    TotalMonthlyExpenses = totalExpenses / 12m,
                    MonthlyRevenueAtRecommended = requiredRevenue / 12m,
                    MonthlySurplus = (requiredRevenue - totalExpenses) / 12m,
                    BreakEvenRate = totalExpenses / 12m,
                    CitizenCount = 0,
                    Rationale = rationale,
                    IsValid = true,
                    ValidationReason = "Calculation successful",
                    Assumptions = new List<string>
                    {
                        $"Target margin: {targetMargin}%",
                        $"Total expenses analyzed: ${totalExpenses:F2}",
                        $"Categories analyzed: {string.Join(", ", expenses.Keys)}"
                    }
                };

                _logger?.LogInformation(
                    "RateScenarioTools: CalculateRecommendedRates completed - TotalExpenses={Expenses}, RecommendedRate={Rate}",
                    totalExpenses,
                    result.RecommendedRate);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RateScenarioTools: Error in CalculateRecommendedRates");
                throw;
            }
        }

        /// <summary>
        /// Runs a what-if scenario with rate increase and multi-year projections.
        /// </summary>
        /// <param name="scenarioDescription">Description of the scenario being modeled.</param>
        /// <param name="rateIncreasePct">Rate increase percentage (e.g., 5 for 5%).</param>
        /// <param name="years">Number of years to project (default: 5).</param>
        /// <returns>What-if scenario result as JSON.</returns>
        [KernelFunction("run_what_if_scenario")]
        [Description("Run a what-if scenario with rate increase and multi-year projections. Returns structured JSON.")]
        public async Task<WhatIfScenarioResult> RunWhatIfScenario(
            [Description("Scenario description (e.g., '5% rate increase with 3% expense growth')")] string scenarioDescription,
            [Description("Rate increase percentage (e.g., 5 for 5%)")] decimal rateIncreasePct,
            [Description("Number of years to project (default: 5)")] int years = 5)
        {
            if (string.IsNullOrWhiteSpace(scenarioDescription))
            {
                throw new ArgumentException("Scenario description cannot be null or empty.", nameof(scenarioDescription));
            }

            if (rateIncreasePct < 0 || rateIncreasePct > 100)
            {
                throw new ArgumentException("Rate increase percentage must be between 0 and 100.", nameof(rateIncreasePct));
            }

            if (years < 1 || years > 20)
            {
                throw new ArgumentException("Years must be between 1 and 20.", nameof(years));
            }

            _logger?.LogInformation(
                "RateScenarioTools: RunWhatIfScenario invoked - description='{Description}', rateIncrease={RateIncrease}%, years={Years}",
                scenarioDescription,
                rateIncreasePct,
                years);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var enterpriseRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IEnterpriseRepository>(scope.ServiceProvider);
                var enterprises = await enterpriseRepository.GetAllAsync();
                var baselineEnterprise = enterprises.FirstOrDefault();

                if (baselineEnterprise == null)
                {
                    _logger?.LogWarning("RateScenarioTools: No enterprises available for scenario modeling");
                    return CreateEmptyWhatIfScenarioResult(scenarioDescription, rateIncreasePct, years);
                }

                var baselineRate = baselineEnterprise.CurrentRate;
                var baselineMonthlyExpenses = baselineEnterprise.MonthlyExpenses;
                var baselineMonthlyBalance = baselineEnterprise.MonthlyBalance;
                var projectedRate = baselineRate * (1m + (rateIncreasePct / 100m));

                var monthlyProjections = new List<MonthlyProjection>();
                var cumulativeBalance = 0m;
                var expenseGrowthMonthly = 0.0025m; // ~3% annual

                for (int y = 0; y < years; y++)
                {
                    for (int m = 1; m <= 12; m++)
                    {
                        var monthNumber = (y * 12) + m;
                        var monthlyExpenses = baselineMonthlyExpenses * (decimal)Math.Pow((double)(1m + expenseGrowthMonthly), monthNumber);
                        var monthlyRevenue = baselineEnterprise.CitizenCount * projectedRate;
                        var monthlyBalance = monthlyRevenue - monthlyExpenses;
                        cumulativeBalance += monthlyBalance;

                        monthlyProjections.Add(new MonthlyProjection
                        {
                            Month = m,
                            Year = DateTime.UtcNow.Year + y,
                            MonthlyRate = projectedRate,
                            MonthlyRevenue = monthlyRevenue,
                            MonthlyExpenses = monthlyExpenses,
                            MonthlyBalance = monthlyBalance,
                            CumulativeBalance = cumulativeBalance
                        });
                    }
                }

                var breakEvenMonth = monthlyProjections.FirstOrDefault(p => p.CumulativeBalance > 0)?.Month ?? 0;
                var totalExpenseGrowth = (monthlyProjections.Last().MonthlyExpenses - baselineMonthlyExpenses) / baselineMonthlyExpenses;

                var result = new WhatIfScenarioResult
                {
                    ScenarioName = $"Scenario: {rateIncreasePct}% Rate Increase",
                    ScenarioDescription = scenarioDescription,
                    BaselineRate = baselineRate,
                    BaselineMonthlyExpenses = baselineMonthlyExpenses,
                    BaselineMonthlyBalance = baselineMonthlyBalance,
                    ProjectionYears = years,
                    RateIncreasePercentage = rateIncreasePct,
                    ProjectedRate = projectedRate,
                    ProjectedMonthlyExpenses = monthlyProjections.Last().MonthlyExpenses,
                    ProjectedMonthlyRevenue = monthlyProjections.Last().MonthlyRevenue,
                    ProjectedMonthlyBalance = monthlyProjections.Last().MonthlyBalance,
                    TotalExpenseGrowth = totalExpenseGrowth,
                    BalanceChange = monthlyProjections.Last().CumulativeBalance - (baselineMonthlyBalance * 12 * years),
                    BreakEvenMonth = breakEvenMonth,
                    MonthlyProgression = monthlyProjections.Take(24).ToList(),
                    RiskLevel = cumulativeBalance > 0 ? "Low" : "High",
                    Recommendations = GenerateScenarioRecommendations(rateIncreasePct, cumulativeBalance, baselineMonthlyBalance)
                };

                _logger?.LogInformation(
                    "RateScenarioTools: RunWhatIfScenario completed - ProjectedRate={ProjectedRate}, BreakEven={BreakEven}, CumulativeBalance={Balance}",
                    projectedRate,
                    breakEvenMonth,
                    cumulativeBalance);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RateScenarioTools: Error in RunWhatIfScenario");
                throw;
            }
        }

        /// <summary>
        /// Projects the impact of reserve contributions over time.
        /// </summary>
        /// <param name="enterpriseId">ID of the enterprise.</param>
        /// <param name="annualContribution">Annual dollar amount to contribute to reserves.</param>
        /// <returns>Reserve forecast result as JSON.</returns>
        [KernelFunction("project_reserves_impact")]
        [Description("Project the impact of annual reserve contributions over multiple years. Returns structured JSON.")]
        public async Task<ReserveForecastResult> ProjectReservesImpact(
            [Description("Enterprise ID")] int enterpriseId,
            [Description("Annual dollar contribution to reserves")] decimal annualContribution)
        {
            if (enterpriseId <= 0)
            {
                throw new ArgumentException("Enterprise ID must be greater than zero.", nameof(enterpriseId));
            }

            if (annualContribution < 0)
            {
                throw new ArgumentException("Annual contribution cannot be negative.", nameof(annualContribution));
            }

            _logger?.LogInformation(
                "RateScenarioTools: ProjectReservesImpact invoked for enterprise {EnterpriseId}, contribution={Contribution}",
                enterpriseId,
                annualContribution);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var enterpriseRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IEnterpriseRepository>(scope.ServiceProvider);
                var enterprise = await enterpriseRepository.GetByIdAsync(enterpriseId);
                if (enterprise == null)
                {
                    _logger?.LogWarning("RateScenarioTools: Enterprise {EnterpriseId} not found", enterpriseId);
                    return CreateEmptyReserveForecastResult(enterpriseId, annualContribution);
                }

                const int projectionYears = 5;
                const decimal targetReserveMonths = 6m;
                var yearlyBreakdown = new List<YearlyReserveProjection>();
                var currentReserve = enterprise.TotalBudget;
                var monthlyExpenses = enterprise.MonthlyExpenses;
                var targetReserveLevel = monthlyExpenses * targetReserveMonths;

                for (int y = 1; y <= projectionYears; y++)
                {
                    var previousBalance = currentReserve;
                    currentReserve += annualContribution;
                    var monthsOfCoverage = monthlyExpenses > 0 ? currentReserve / monthlyExpenses : 0;

                    yearlyBreakdown.Add(new YearlyReserveProjection
                    {
                        Year = DateTime.UtcNow.Year + y,
                        BeginningBalance = previousBalance,
                        AnnualContribution = annualContribution,
                        EndingBalance = currentReserve,
                        MonthsOfCoverage = monthsOfCoverage
                    });
                }

                var isOnTrack = yearlyBreakdown.Last().MonthsOfCoverage >= targetReserveMonths;

                var result = new ReserveForecastResult
                {
                    EnterpriseId = enterpriseId,
                    EnterpriseName = enterprise.Name,
                    CurrentReserve = enterprise.TotalBudget,
                    AnnualContribution = annualContribution,
                    MonthlyContribution = annualContribution / 12m,
                    ProjectionYears = projectionYears,
                    ProjectedReserveAtEnd = currentReserve,
                    TargetReserveLevel = targetReserveLevel,
                    ReserveCoverageMonths = monthlyExpenses > 0 ? enterprise.TotalBudget / monthlyExpenses : 0,
                    TargetCoverageMonths = targetReserveMonths,
                    IsOnTrack = isOnTrack,
                    YearlyBreakdown = yearlyBreakdown,
                    Insights = GenerateReserveInsights(enterprise, annualContribution, targetReserveLevel, isOnTrack),
                    Recommendations = GenerateReserveRecommendations(isOnTrack, annualContribution, enterprise)
                };

                _logger?.LogInformation(
                    "RateScenarioTools: ProjectReservesImpact completed - ProjectedReserve={Reserve}, OnTrack={OnTrack}",
                    result.ProjectedReserveAtEnd,
                    isOnTrack);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RateScenarioTools: Error in ProjectReservesImpact for enterprise {EnterpriseId}", enterpriseId);
                throw;
            }
        }

        private static List<string> GenerateScenarioRecommendations(decimal rateIncrease, decimal cumulativeBalance, decimal baselineBalance)
        {
            var recommendations = new List<string>();

            if (cumulativeBalance > baselineBalance * 12 * 5)
            {
                recommendations.Add("✅ Strong positive cash flow. Consider debt reduction or infrastructure investments.");
            }
            else if (cumulativeBalance < 0)
            {
                recommendations.Add("⚠️ Negative cumulative balance projected. Consider higher rate increase or expense controls.");
            }

            if (rateIncrease > 10)
            {
                recommendations.Add("💡 Rate increase exceeds 10%. Plan public engagement and communication strategy.");
            }

            recommendations.Add("💡 Monitor actual vs. projected results quarterly and adjust as needed.");
            recommendations.Add("💡 Consider impact on rate-sensitive customers (elderly, low-income demographics).");

            return recommendations;
        }

        private static List<string> GenerateReserveInsights(
            Enterprise enterprise,
            decimal contribution,
            decimal targetLevel,
            bool isOnTrack)
        {
            var insights = new List<string>();

            var currentCoverageMonths = enterprise.MonthlyExpenses > 0 ? enterprise.TotalBudget / enterprise.MonthlyExpenses : 0;

            if (currentCoverageMonths < 3)
            {
                insights.Add("🔴 Current reserves are critically low (less than 3 months of expenses).");
            }
            else if (currentCoverageMonths < 6)
            {
                insights.Add("🟡 Current reserves are below industry standard (less than 6 months of expenses).");
            }
            else
            {
                insights.Add("✅ Current reserves meet industry standards.");
            }

            var yearsToTarget = contribution > 0 ? (targetLevel - enterprise.TotalBudget) / contribution : 0;
            if (yearsToTarget > 0)
            {
                insights.Add($"📊 At current contribution rate, target reserves in {yearsToTarget:F1} years.");
            }

            return insights;
        }

        private static List<string> GenerateReserveRecommendations(
            bool isOnTrack,
            decimal contribution,
            Enterprise enterprise)
        {
            var recommendations = new List<string>();

            if (isOnTrack)
            {
                recommendations.Add("✅ Reserve contribution plan is on track to meet targets.");
            }
            else
            {
                recommendations.Add("💡 Increase annual contribution to accelerate reserve build-up.");
            }

            if (contribution == 0)
            {
                recommendations.Add("⚠️ No reserve contributions planned. Consider allocating 5-10% of operating budget to reserves.");
            }

            recommendations.Add("💡 Review reserves quarterly and adjust contributions based on financial performance.");
            recommendations.Add("💡 Establish clear reserve policies for emergency access and planned capital projects.");

            return recommendations;
        }

        private static WhatIfScenarioResult CreateEmptyWhatIfScenarioResult(
            string scenarioDescription,
            decimal rateIncreasePct,
            int years)
        {
            return new WhatIfScenarioResult
            {
                ScenarioName = $"Scenario: {rateIncreasePct}% Rate Increase",
                ScenarioDescription = scenarioDescription,
                ProjectionYears = years,
                RateIncreasePercentage = rateIncreasePct,
                RiskLevel = "Unknown",
                Recommendations = new List<string> { "No enterprise data available for projection." }
            };
        }

        private static ReserveForecastResult CreateEmptyReserveForecastResult(int enterpriseId, decimal annualContribution)
        {
            return new ReserveForecastResult
            {
                EnterpriseId = enterpriseId,
                EnterpriseName = "Unknown",
                AnnualContribution = annualContribution,
                MonthlyContribution = annualContribution / 12m,
                Insights = new List<string> { "Enterprise not found." },
                Recommendations = new List<string> { "Verify enterprise ID and retry." }
            };
        }
    }
}
