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
using WileyWidget.Services;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Tools to analyze capital purchase tradeoffs (e.g., replace a truck vs continue repairs).
    /// Exposes a Kernel function `analyze_purchase_tradeoff` for agentic use.
    /// </summary>
    public sealed class PurchaseAnalysisTools
    {
        private readonly IWhatIfScenarioEngine _whatIfScenarioEngine;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PurchaseAnalysisTools>? _logger;

        public PurchaseAnalysisTools(
            IWhatIfScenarioEngine whatIfScenarioEngine,
            IServiceScopeFactory scopeFactory,
            ILogger<PurchaseAnalysisTools>? logger = null)
        {
            _whatIfScenarioEngine = whatIfScenarioEngine ?? throw new ArgumentNullException(nameof(whatIfScenarioEngine));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger;
        }

        /// <summary>
        /// Result for a purchase tradeoff analysis.
        /// </summary>
        [Description("Purchase tradeoff analysis result")]
        public sealed class PurchaseTradeoffResult
        {
            [JsonPropertyName("enterpriseId")] public int EnterpriseId { get; set; }
            [JsonPropertyName("enterpriseName")] public string EnterpriseName { get; set; } = string.Empty;
            [JsonPropertyName("purchaseCost")] public decimal PurchaseCost { get; set; }
            [JsonPropertyName("financingYears")] public int FinancingYears { get; set; }
            [JsonPropertyName("interestRate")] public decimal InterestRate { get; set; }
            [JsonPropertyName("monthlyPayment")] public decimal MonthlyPayment { get; set; }
            [JsonPropertyName("annualPayment")] public decimal AnnualPayment { get; set; }
            [JsonPropertyName("annualRepairSavings")] public decimal AnnualRepairSavings { get; set; }
            [JsonPropertyName("netAnnualSavings")] public decimal NetAnnualSavings { get; set; }
            [JsonPropertyName("paybackYears")] public decimal? PaybackYears { get; set; }
            [JsonPropertyName("recommendation")] public string Recommendation { get; set; } = string.Empty;
            [JsonPropertyName("details")] public List<string> Details { get; set; } = new();
            [JsonPropertyName("isValid")] public bool IsValid { get; set; }
        }

        /// <summary>
        /// Analyze whether purchasing an asset is financially preferable to continuing repairs.
        /// Returns monthly/annual financing costs, expected repair savings, payback estimate and a recommendation.
        /// </summary>
        [KernelFunction("analyze_purchase_tradeoff")]
        [Description("Analyze purchase vs repair tradeoff for a capital asset (returns structured JSON)")]
        public async Task<PurchaseTradeoffResult> AnalyzePurchaseTradeoff(
            [Description("Enterprise ID to evaluate")] int enterpriseId,
            [Description("Purchase cost in dollars")] decimal purchaseCost,
            [Description("Financing term in years (default 5)")] int financingYears = 5,
            [Description("Expected annual repair/operational savings (dollars)")] decimal annualRepairSavings = 0m,
            [Description("Annual interest rate (e.g., 0.05 for 5%)")] decimal annualInterestRate = 0.05m,
            CancellationToken cancellationToken = default)
        {
            if (enterpriseId <= 0) throw new ArgumentException("Enterprise ID must be greater than zero", nameof(enterpriseId));
            if (purchaseCost <= 0) throw new ArgumentException("Purchase cost must be greater than zero", nameof(purchaseCost));
            if (financingYears <= 0) financingYears = 5;
            if (annualInterestRate < 0) annualInterestRate = 0.05m;

            _logger?.LogInformation("PurchaseAnalysis: AnalyzePurchaseTradeoff called for enterprise {EnterpriseId} cost={Cost} years={Years} savings={Savings}", enterpriseId, purchaseCost, financingYears, annualRepairSavings);

            using var scope = _scopeFactory.CreateScope();
            var enterpriseRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IEnterpriseRepository>(scope.ServiceProvider);
            var enterprise = enterpriseRepository != null ? await enterpriseRepository.GetByIdAsync(enterpriseId) : null;

            if (enterprise == null)
            {
                _logger?.LogWarning("PurchaseAnalysis: Enterprise {EnterpriseId} not found", enterpriseId);
                return new PurchaseTradeoffResult
                {
                    EnterpriseId = enterpriseId,
                    EnterpriseName = "Unknown",
                    PurchaseCost = purchaseCost,
                    FinancingYears = financingYears,
                    InterestRate = annualInterestRate,
                    MonthlyPayment = 0m,
                    AnnualPayment = 0m,
                    AnnualRepairSavings = annualRepairSavings,
                    NetAnnualSavings = 0m,
                    PaybackYears = null,
                    Recommendation = "Enterprise not found",
                    IsValid = false
                };
            }

            // Try to leverage the scenario engine to obtain equipment impact if possible
            decimal monthlyPayment;
            decimal annualPayment;
            string equipmentDetails = string.Empty;

            try
            {
                var parameters = new ScenarioParameters
                {
                    EquipmentPurchaseAmount = purchaseCost,
                    EquipmentFinancingYears = financingYears,
                    PayRaisePercentage = 0m,
                    BenefitsIncreaseAmount = 0m,
                    ReservePercentage = 0m
                };

                var scenario = await _whatIfScenarioEngine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters, cancellationToken);
                var equipmentImpact = scenario.ScenarioImpacts?.FirstOrDefault(si => si.Category?.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) >= 0);
                if (equipmentImpact != null)
                {
                    annualPayment = equipmentImpact.AnnualIncrease;
                    monthlyPayment = equipmentImpact.MonthlyIncrease;
                    equipmentDetails = string.Join("; ", equipmentImpact.Details ?? new List<string>());
                }
                else
                {
                    monthlyPayment = CalculateLoanPayment(purchaseCost, annualInterestRate, financingYears * 12);
                    annualPayment = monthlyPayment * 12m;
                    equipmentDetails = "Equipment impact computed locally (scenario engine did not provide details).";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "PurchaseAnalysis: Scenario engine failed - falling back to local calculation");
                monthlyPayment = CalculateLoanPayment(purchaseCost, annualInterestRate, financingYears * 12);
                annualPayment = monthlyPayment * 12m;
                equipmentDetails = $"Scenario engine error: {ex.Message}";
            }

            var netAnnualSavings = annualRepairSavings - annualPayment; // positive means purchase yields net annual savings

            decimal? paybackYears = null;
            if (netAnnualSavings > 0)
            {
                paybackYears = (decimal)Math.Round((double)(purchaseCost / netAnnualSavings), 2);
            }

            string recommendation;
            if (netAnnualSavings > 0)
            {
                recommendation = paybackYears.HasValue
                    ? $"Recommend purchase: positive net annual savings of ${netAnnualSavings:N2}; estimated payback {paybackYears:F2} years."
                    : $"Recommend purchase: positive net annual savings of ${netAnnualSavings:N2}.";
            }
            else if (annualRepairSavings > 0 && netAnnualSavings <= 0)
            {
                recommendation = "Do not purchase based on financials alone; annual financing cost exceeds expected repair savings. Consider deferred purchase or negotiate better financing.";
            }
            else
            {
                recommendation = "No measurable repair savings provided; unable to recommend purchase based on financials alone. Consider qualitative factors (reliability, safety).";
            }

            var details = new List<string>
            {
                $"Enterprise: {enterprise.Name} (ID: {enterprise.Id})",
                $"Purchase cost: ${purchaseCost:N2}",
                $"Financing term: {financingYears} years @ {annualInterestRate:P2}",
                $"Monthly payment: ${monthlyPayment:N2}",
                $"Annual payment: ${annualPayment:N2}",
                $"Expected annual repair savings: ${annualRepairSavings:N2}",
                $"Net annual savings (savings - payment): ${netAnnualSavings:N2}",
                equipmentDetails
            };

            var result = new PurchaseTradeoffResult
            {
                EnterpriseId = enterprise.Id,
                EnterpriseName = enterprise.Name,
                PurchaseCost = purchaseCost,
                FinancingYears = financingYears,
                InterestRate = annualInterestRate,
                MonthlyPayment = Math.Round(monthlyPayment, 2),
                AnnualPayment = Math.Round(annualPayment, 2),
                AnnualRepairSavings = Math.Round(annualRepairSavings, 2),
                NetAnnualSavings = Math.Round(netAnnualSavings, 2),
                PaybackYears = paybackYears == null ? null : Math.Round((decimal)paybackYears, 2),
                Recommendation = recommendation,
                Details = details,
                IsValid = true
            };

            _logger?.LogInformation("PurchaseAnalysis: Analysis complete for {EnterpriseId} - Recommendation: {Rec}", enterpriseId, recommendation);

            return result;
        }

        private static decimal CalculateLoanPayment(decimal principal, decimal annualRate, int months)
        {
            var monthlyRate = annualRate / 12m;
            if (monthlyRate == 0) return Math.Round(principal / months, 2);

            var rateDouble = (double)monthlyRate;
            var payment = principal * (decimal)(rateDouble * Math.Pow(1 + rateDouble, months)) /
                         (decimal)(Math.Pow(1 + rateDouble, months) - 1);

            return Math.Round(payment, 2);
        }
    }
}
