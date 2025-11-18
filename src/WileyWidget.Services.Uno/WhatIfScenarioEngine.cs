using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WileyWidget.Data;
using WileyWidget.Models;
using BusinessInterfaces = WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// Service for generating comprehensive What-If financial scenarios
/// </summary>
public class WhatIfScenarioEngine : IWhatIfScenarioEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChargeCalculatorService _chargeCalculator;
    private readonly BusinessInterfaces.IEnterpriseRepository _enterpriseRepository;
    private readonly BusinessInterfaces.IMunicipalAccountRepository _municipalAccountRepository;

    public WhatIfScenarioEngine(IServiceScopeFactory scopeFactory, IChargeCalculatorService chargeCalculator, BusinessInterfaces.IEnterpriseRepository enterpriseRepository, BusinessInterfaces.IMunicipalAccountRepository municipalAccountRepository)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _chargeCalculator = chargeCalculator ?? throw new ArgumentNullException(nameof(chargeCalculator));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));
    }

    /// <summary>
    /// Generate comprehensive what-if scenario for multiple changes
    /// </summary>
    public async Task<ComprehensiveScenario> GenerateComprehensiveScenarioAsync(
        int enterpriseId,
        ScenarioParameters parameters)
    {
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));

        var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
        if (enterprise == null)
            throw new ArgumentException($"Enterprise with ID {enterpriseId} not found");

        // Get baseline data
        var baselineRecommendation = await _chargeCalculator.CalculateRecommendedChargeAsync(enterpriseId);
        var fundType = enterprise.Type switch
        {
            "Water" => MunicipalFundType.Water,
            "Sewer" => MunicipalFundType.Sewer,
            "Trash" => MunicipalFundType.Trash,
            _ => MunicipalFundType.Enterprise
        };

        var expenseAccounts = await _municipalAccountRepository.GetByFundAsync(fundType);

        // Calculate scenario impacts
        var payRaiseImpact = CalculatePayRaiseImpact(parameters.PayRaisePercentage, enterprise);
        var benefitsImpact = CalculateBenefitsImpact(parameters.BenefitsIncreaseAmount, enterprise);
        var equipmentImpact = CalculateEquipmentImpact(parameters.EquipmentPurchaseAmount, parameters.EquipmentFinancingYears);
        var reserveImpact = CalculateReserveImpact(parameters.ReservePercentage, baselineRecommendation);

        // Calculate total impact
        var totalExpenseIncrease = payRaiseImpact.AnnualIncrease +
                                  benefitsImpact.AnnualIncrease +
                                  equipmentImpact.AnnualIncrease +
                                  reserveImpact.AnnualIncrease;

        var totalMonthlyExpenseIncrease = totalExpenseIncrease / 12;

        // Generate recommendations
        var recommendations = GenerateComprehensiveRecommendations(
            payRaiseImpact, benefitsImpact, equipmentImpact, reserveImpact,
            baselineRecommendation, enterprise, parameters);

        var scenario = new ComprehensiveScenario
        {
            ScenarioName = GenerateScenarioName(parameters),
            BaselineData = new BaselineData
            {
                CurrentRate = enterprise.CurrentRate,
                MonthlyExpenses = baselineRecommendation.TotalMonthlyExpenses,
                MonthlyRevenue = enterprise.MonthlyRevenue,
                MonthlyBalance = enterprise.MonthlyBalance,
                CitizenCount = enterprise.CitizenCount
            },
            ScenarioImpacts = new List<ScenarioImpact>
            {
                payRaiseImpact,
                benefitsImpact,
                equipmentImpact,
                reserveImpact
            },
            TotalImpact = new TotalImpact
            {
                TotalAnnualExpenseIncrease = totalExpenseIncrease,
                TotalMonthlyExpenseIncrease = totalMonthlyExpenseIncrease,
                RequiredRateIncrease = CalculateRequiredRateIncrease(totalMonthlyExpenseIncrease, enterprise.CitizenCount),
                NewMonthlyRate = enterprise.CurrentRate + CalculateRequiredRateIncrease(totalMonthlyExpenseIncrease, enterprise.CitizenCount),
                NewMonthlyRevenue = (enterprise.CurrentRate + CalculateRequiredRateIncrease(totalMonthlyExpenseIncrease, enterprise.CitizenCount)) * enterprise.CitizenCount,
                NewMonthlyBalance = (enterprise.CurrentRate + CalculateRequiredRateIncrease(totalMonthlyExpenseIncrease, enterprise.CitizenCount)) * enterprise.CitizenCount - (baselineRecommendation.TotalMonthlyExpenses + totalMonthlyExpenseIncrease)
            },
            Recommendations = recommendations,
            RiskAssessment = AssessScenarioRisks(totalExpenseIncrease, baselineRecommendation, enterprise),
            GeneratedDate = DateTime.Now
        };

        Log.Information("Generated comprehensive scenario '{ScenarioName}' for {Enterprise}",
            scenario.ScenarioName, enterprise.Name);

        return scenario;
    }

    /// <summary>
    /// Calculate pay raise impact
    /// </summary>
    private ScenarioImpact CalculatePayRaiseImpact(decimal payRaisePercentage, Enterprise enterprise)
    {
        // Assume 60% of expenses are personnel costs (industry standard for utilities)
        var personnelExpenses = enterprise.MonthlyExpenses * 0.60m * 12; // Annual
        var annualIncrease = personnelExpenses * payRaisePercentage;

        return new ScenarioImpact
        {
            Category = "Employee Pay Raise",
            Description = $"{(payRaisePercentage * 100):0}% pay increase across all employees",
            AnnualIncrease = annualIncrease,
            MonthlyIncrease = annualIncrease / 12,
            ImpactLevel = annualIncrease > enterprise.MonthlyExpenses * 12 * 0.05m ? "High" : "Medium",
            Details = new List<string>
            {
                $"Current estimated personnel costs: ${personnelExpenses:N2}/year",
                $"Annual increase: ${annualIncrease:N2}",
                $"Monthly increase: ${annualIncrease/12:N2}",
                "Assumes 60% of operating expenses are personnel-related"
            }
        };
    }

    /// <summary>
    /// Calculate benefits impact
    /// </summary>
    private ScenarioImpact CalculateBenefitsImpact(decimal benefitsIncreaseAmount, Enterprise enterprise)
    {
        var annualIncrease = benefitsIncreaseAmount * 12; // Convert monthly to annual

        return new ScenarioImpact
        {
            Category = "Employee Benefits",
            Description = $"${benefitsIncreaseAmount:N2}/month increase in benefits costs",
            AnnualIncrease = annualIncrease,
            MonthlyIncrease = benefitsIncreaseAmount,
            ImpactLevel = benefitsIncreaseAmount > enterprise.MonthlyExpenses * 0.10m ? "High" : "Medium",
            Details = new List<string>
            {
                $"Monthly benefits increase: ${benefitsIncreaseAmount:N2}",
                $"Annual benefits increase: ${annualIncrease:N2}",
                "Includes healthcare, retirement, and other benefits"
            }
        };
    }

    /// <summary>
    /// Calculate equipment purchase impact
    /// </summary>
    private ScenarioImpact CalculateEquipmentImpact(decimal equipmentAmount, int financingYears)
    {
        if (financingYears <= 0) financingYears = 1;

        // Assume 5% interest rate for equipment financing
        var annualInterestRate = 0.05m;
        var monthlyPayment = CalculateLoanPayment(equipmentAmount, annualInterestRate, financingYears * 12);
        var annualIncrease = monthlyPayment * 12;

        return new ScenarioImpact
        {
            Category = "Equipment Purchase",
            Description = $"${equipmentAmount:N2} equipment purchase over {financingYears} years",
            AnnualIncrease = annualIncrease,
            MonthlyIncrease = monthlyPayment,
            ImpactLevel = equipmentAmount > 100000 ? "High" : "Medium",
            Details = new List<string>
            {
                $"Equipment cost: ${equipmentAmount:N2}",
                $"Financing period: {financingYears} years",
                $"Monthly payment: ${monthlyPayment:N2} (5% interest)",
                $"Annual cost: ${annualIncrease:N2}",
                "Assumes equipment will improve operational efficiency"
            }
        };
    }

    /// <summary>
    /// Calculate reserve impact
    /// </summary>
    private ScenarioImpact CalculateReserveImpact(decimal reservePercentage, ServiceChargeRecommendation baseline)
    {
        var annualReserveAmount = baseline.TotalMonthlyExpenses * 12 * reservePercentage;
        var monthlyIncrease = annualReserveAmount / 12;

        return new ScenarioImpact
        {
            Category = "Operating Reserve",
            Description = $"{(reservePercentage * 100):0}% increase in operating reserve allocation",
            AnnualIncrease = annualReserveAmount,
            MonthlyIncrease = monthlyIncrease,
            ImpactLevel = "Low", // Reserves are generally positive
            Details = new List<string>
            {
                $"Reserve percentage: {(reservePercentage * 100):0}%",
                $"Annual reserve allocation: ${annualReserveAmount:N2}",
                $"Monthly reserve increase: ${monthlyIncrease:N2}",
                "Provides financial stability and emergency funds"
            }
        };
    }

    /// <summary>
    /// Calculate loan payment using standard amortization formula
    /// </summary>
    private decimal CalculateLoanPayment(decimal principal, decimal annualRate, int months)
    {
        var monthlyRate = annualRate / 12;
        if (monthlyRate == 0) return principal / months;

        var rateDouble = (double)monthlyRate;
        var payment = principal * (decimal)(rateDouble * Math.Pow(1 + rateDouble, months)) /
                     (decimal)(Math.Pow(1 + rateDouble, months) - 1);

        return Math.Round(payment, 2);
    }

    /// <summary>
    /// Calculate required rate increase
    /// </summary>
    private decimal CalculateRequiredRateIncrease(decimal monthlyExpenseIncrease, int citizenCount)
    {
        if (citizenCount <= 0) return 0;
        return Math.Round(monthlyExpenseIncrease / citizenCount, 2);
    }

    /// <summary>
    /// Generate scenario name
    /// </summary>
    private string GenerateScenarioName(ScenarioParameters parameters)
    {
        var parts = new List<string>();

        if (parameters.PayRaisePercentage > 0)
            parts.Add($"Pay Raise {(parameters.PayRaisePercentage * 100):0}%");

        if (parameters.BenefitsIncreaseAmount > 0)
            parts.Add($"${parameters.BenefitsIncreaseAmount}/mo Benefits");

        if (parameters.EquipmentPurchaseAmount > 0)
            parts.Add($"${parameters.EquipmentPurchaseAmount} Equipment");

        if (parameters.ReservePercentage > 0)
            parts.Add($"{(parameters.ReservePercentage * 100):0}% Reserve");

        return parts.Count > 0 ? string.Join(", ", parts) : "Base Scenario";
    }

    /// <summary>
    /// Generate comprehensive recommendations
    /// </summary>
    private List<string> GenerateComprehensiveRecommendations(
        ScenarioImpact payRaise, ScenarioImpact benefits, ScenarioImpact equipment,
        ScenarioImpact reserve, ServiceChargeRecommendation baseline, Enterprise enterprise,
        ScenarioParameters parameters)
    {
        var recommendations = new List<string>();

        // Rate increase recommendations
        var totalMonthlyIncrease = payRaise.MonthlyIncrease + benefits.MonthlyIncrease +
                                  equipment.MonthlyIncrease + reserve.MonthlyIncrease;
        var requiredRateIncrease = CalculateRequiredRateIncrease(totalMonthlyIncrease, enterprise.CitizenCount);

        if (requiredRateIncrease > enterprise.CurrentRate * 0.25m)
        {
            recommendations.Add($"⚠️ Large rate increase required (${requiredRateIncrease:N2}). Consider phasing in over multiple years.");
        }
        else if (requiredRateIncrease > 0)
        {
            recommendations.Add($"✅ Moderate rate increase of ${requiredRateIncrease:N2} needed to maintain financial stability.");
        }

        // Equipment recommendations
        if (parameters.EquipmentPurchaseAmount > 50000)
        {
            recommendations.Add("💡 Consider equipment financing options to spread costs over time.");
            recommendations.Add("💡 Evaluate equipment efficiency improvements that may offset increased costs.");
        }

        // Benefits recommendations
        if (parameters.BenefitsIncreaseAmount > enterprise.MonthlyExpenses * 0.05m)
        {
            recommendations.Add("💡 Review benefits competitiveness in local market before implementation.");
        }

        // Reserve recommendations
        if (parameters.ReservePercentage >= 10)
        {
            recommendations.Add("✅ Strong reserve allocation supports long-term financial stability.");
        }

        // General recommendations
        recommendations.Add("💡 Monitor actual vs. projected expenses quarterly.");
        recommendations.Add("💡 Consider public input sessions before implementing rate changes.");
        recommendations.Add("💡 Evaluate energy efficiency programs to offset cost increases.");

        return recommendations;
    }

    /// <summary>
    /// Assess scenario risks
    /// </summary>
    private RiskAssessment AssessScenarioRisks(decimal totalExpenseIncrease, ServiceChargeRecommendation baseline, Enterprise enterprise)
    {
        var riskLevel = "Low";
        var concerns = new List<string>();

        var expenseIncreasePercentage = baseline.TotalMonthlyExpenses > 0 ?
            (totalExpenseIncrease / 12) / baseline.TotalMonthlyExpenses : 0;

        if (expenseIncreasePercentage > 0.50m) // 50% increase
        {
            riskLevel = "High";
            concerns.Add("Expense increase exceeds 50% - significant financial strain");
        }
        else if (expenseIncreasePercentage > 0.25m) // 25% increase
        {
            riskLevel = "Medium";
            concerns.Add("Expense increase exceeds 25% - moderate financial impact");
        }

        var newBalance = enterprise.MonthlyBalance - (totalExpenseIncrease / 12);
        if (newBalance < 0)
        {
            riskLevel = riskLevel == "Low" ? "Medium" : "High";
            concerns.Add("Projected negative cash flow - service sustainability at risk");
        }

        var recommendations = new List<string>();
        if (concerns.Contains("Projected negative cash flow"))
        {
            recommendations.Add("Implement cost controls immediately");
            recommendations.Add("Consider delaying non-essential expenditures");
            recommendations.Add("Explore additional revenue sources");
        }

        return new RiskAssessment
        {
            RiskLevel = riskLevel,
            Concerns = concerns,
            MitigationStrategies = recommendations
        };
    }
}

/// <summary>
/// Parameters for what-if scenario
/// </summary>
public class ScenarioParameters
{
    public decimal PayRaisePercentage { get; set; }
    public decimal BenefitsIncreaseAmount { get; set; } // Monthly amount
    public decimal EquipmentPurchaseAmount { get; set; }
    public int EquipmentFinancingYears { get; set; } = 5;
    public decimal ReservePercentage { get; set; }
}

/// <summary>
/// Comprehensive scenario result
/// </summary>
public class ComprehensiveScenario
{
    public string ScenarioName { get; set; } = string.Empty;
    public BaselineData BaselineData { get; set; } = new();
    public List<ScenarioImpact> ScenarioImpacts { get; set; } = new();
    public TotalImpact TotalImpact { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public RiskAssessment RiskAssessment { get; set; } = new();
    public DateTime GeneratedDate { get; set; }
}

/// <summary>
/// Baseline financial data
/// </summary>
public class BaselineData
{
    public decimal CurrentRate { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal MonthlyBalance { get; set; }
    public int CitizenCount { get; set; }
}

/// <summary>
/// Individual scenario impact
/// </summary>
public class ScenarioImpact
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal AnnualIncrease { get; set; }
    public decimal MonthlyIncrease { get; set; }
    public string ImpactLevel { get; set; } = string.Empty; // Low, Medium, High
    public List<string> Details { get; set; } = new();
}

/// <summary>
/// Total scenario impact
/// </summary>
public class TotalImpact
{
    public decimal TotalAnnualExpenseIncrease { get; set; }
    public decimal TotalMonthlyExpenseIncrease { get; set; }
    public decimal RequiredRateIncrease { get; set; }
    public decimal NewMonthlyRate { get; set; }
    public decimal NewMonthlyRevenue { get; set; }
    public decimal NewMonthlyBalance { get; set; }
}

/// <summary>
/// Risk assessment
/// </summary>
public class RiskAssessment
{
    public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High
    public List<string> Concerns { get; set; } = new();
    public List<string> MitigationStrategies { get; set; } = new();
}
