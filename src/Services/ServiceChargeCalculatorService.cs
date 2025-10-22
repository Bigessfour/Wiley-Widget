using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WileyWidget.Data;
using WileyWidget.Models;
using BusinessInterfaces = WileyWidget.Business.Interfaces;

namespace WileyWidget.Services;

/// <summary>
/// Service for calculating recommended monthly service charges based on actual expenses
/// </summary>
public class ServiceChargeCalculatorService : IChargeCalculatorService
{
    private readonly BusinessInterfaces.IEnterpriseRepository _enterpriseRepository;
    private readonly BusinessInterfaces.IMunicipalAccountRepository _municipalAccountRepository;

    public ServiceChargeCalculatorService(BusinessInterfaces.IEnterpriseRepository enterpriseRepository, BusinessInterfaces.IMunicipalAccountRepository municipalAccountRepository)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));
    }

    // Parameterless constructor for testing/mocking
    protected ServiceChargeCalculatorService()
    {
        _enterpriseRepository = null!;
        _municipalAccountRepository = null!;
    }

    /// <summary>
    /// Calculate recommended monthly service charge for an enterprise
    /// </summary>
    public async Task<ServiceChargeRecommendation> CalculateRecommendedChargeAsync(int enterpriseId)
    {
        try
        {
            var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
            if (enterprise == null)
            {
                throw new ArgumentException($"Enterprise with ID {enterpriseId} not found");
            }

            // Get related expense accounts
            var fundType = enterprise.Type switch
            {
                "Water" => MunicipalFundType.Water,
                "Sewer" => MunicipalFundType.Sewer,
                "Trash" => MunicipalFundType.Trash,
                "General" => MunicipalFundType.General,
                _ => MunicipalFundType.Enterprise
            };

            var expenseAccounts = await _municipalAccountRepository.GetByFundAsync(fundType);

            // Calculate total monthly expenses from accounts
            var totalMonthlyExpenses = expenseAccounts
                .Where(a => a.Type == AccountType.Expense && a.BudgetAmount > 0)
                .Sum(a => a.BudgetAmount / 12); // Convert annual budget to monthly

            // Add operational expenses from enterprise
            totalMonthlyExpenses += enterprise.MonthlyExpenses;

            // Calculate recommended charge with markup for reserves and profit
            var recommendedCharge = CalculateChargeWithReserves(totalMonthlyExpenses, enterprise.CitizenCount);

            // Calculate break-even analysis
            var breakEvenAnalysis = CalculateBreakEvenAnalysis(enterprise, totalMonthlyExpenses);

            var recommendation = new ServiceChargeRecommendation
            {
                EnterpriseId = enterpriseId,
                EnterpriseName = enterprise.Name,
                CurrentRate = enterprise.CurrentRate,
                RecommendedRate = recommendedCharge.RecommendedRate,
                TotalMonthlyExpenses = totalMonthlyExpenses,
                MonthlyRevenueAtRecommended = recommendedCharge.MonthlyRevenue,
                MonthlySurplus = recommendedCharge.MonthlyRevenue - totalMonthlyExpenses,
                ReserveAllocation = recommendedCharge.ReserveAllocation,
                BreakEvenAnalysis = breakEvenAnalysis,
                CalculationDate = DateTime.Now,
                Assumptions = new List<string>
                {
                    "10% operating reserve allocation",
                    "5% profit margin for sustainability",
                    "Based on current expense accounts and enterprise data",
                    "Monthly calculations based on annual budgets divided by 12"
                }
            };

            Log.Information("Calculated service charge recommendation for {Enterprise}: Current ${CurrentRate}, Recommended ${RecommendedRate}",
                enterprise.Name, enterprise.CurrentRate, recommendedCharge.RecommendedRate);

            return recommendation;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating service charge for enterprise {EnterpriseId}", enterpriseId);
            throw;
        }
    }

    /// <summary>
    /// Calculate charge including reserves and profit margins
    /// </summary>
    private (decimal RecommendedRate, decimal MonthlyRevenue, decimal ReserveAllocation) CalculateChargeWithReserves(decimal totalMonthlyExpenses, int citizenCount)
    {
        if (citizenCount <= 0)
            throw new ArgumentException("Citizen count must be greater than 0");

        // Add 10% for operating reserves
        var expensesWithReserves = totalMonthlyExpenses * 1.10m;

        // Add 5% for profit/sustainability margin
        var expensesWithProfit = expensesWithReserves * 1.05m;

        // Calculate per-citizen rate
        var recommendedRate = Math.Round(expensesWithProfit / citizenCount, 2);

        // Calculate monthly revenue at recommended rate
        var monthlyRevenue = recommendedRate * citizenCount;

        // Calculate reserve allocation
        var reserveAllocation = totalMonthlyExpenses * 0.10m;

        return (recommendedRate, monthlyRevenue, reserveAllocation);
    }

    /// <summary>
    /// Calculate break-even analysis
    /// </summary>
    private BreakEvenAnalysis CalculateBreakEvenAnalysis(Enterprise enterprise, decimal totalMonthlyExpenses)
    {
        var breakEvenRate = enterprise.CitizenCount > 0 ? totalMonthlyExpenses / enterprise.CitizenCount : 0;

        return new BreakEvenAnalysis
        {
            BreakEvenRate = Math.Round(breakEvenRate, 2),
            CurrentSurplusDeficit = enterprise.MonthlyBalance,
            RequiredRateIncrease = breakEvenRate > enterprise.CurrentRate ? breakEvenRate - enterprise.CurrentRate : 0,
            CoverageRatio = enterprise.CurrentRate > 0 ? (enterprise.MonthlyRevenue / totalMonthlyExpenses) : 0
        };
    }

    /// <summary>
    /// Generate what-if scenario for service charge changes
    /// </summary>
    public async Task<WhatIfScenario> GenerateChargeScenarioAsync(int enterpriseId, decimal proposedRateIncrease, decimal proposedExpenseChange = 0)
    {
        var currentRecommendation = await CalculateRecommendedChargeAsync(enterpriseId);

        var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
        if (enterprise == null)
            throw new ArgumentException($"Enterprise with ID {enterpriseId} not found");

        // Calculate new scenario
        var newRate = enterprise.CurrentRate + proposedRateIncrease;
        var newMonthlyExpenses = currentRecommendation.TotalMonthlyExpenses + proposedExpenseChange;
        var newMonthlyRevenue = newRate * enterprise.CitizenCount;
        var newMonthlyBalance = newMonthlyRevenue - newMonthlyExpenses;

        return new WhatIfScenario
        {
            ScenarioName = $"Rate Increase: ${proposedRateIncrease:N2}, Expense Change: ${proposedExpenseChange:N2}",
            CurrentRate = enterprise.CurrentRate,
            ProposedRate = newRate,
            CurrentMonthlyExpenses = currentRecommendation.TotalMonthlyExpenses,
            ProposedMonthlyExpenses = newMonthlyExpenses,
            CurrentMonthlyRevenue = enterprise.MonthlyRevenue,
            ProposedMonthlyRevenue = newMonthlyRevenue,
            CurrentMonthlyBalance = enterprise.MonthlyBalance,
            ProposedMonthlyBalance = newMonthlyBalance,
            ImpactAnalysis = GenerateImpactAnalysis(newMonthlyBalance, enterprise.MonthlyBalance),
            Recommendations = GenerateScenarioRecommendations(newMonthlyBalance, proposedRateIncrease, proposedExpenseChange)
        };
    }

    /// <summary>
    /// Generate impact analysis for scenario
    /// </summary>
    private string GenerateImpactAnalysis(decimal newBalance, decimal currentSurplus)
    {
        var impact = new List<string>();

        if (newBalance > currentSurplus)
        {
            var improvement = newBalance - currentSurplus;
            impact.Add($"Monthly surplus improves by ${improvement:N2}");
            impact.Add("Increased reserves available for capital improvements");
        }
        else if (newBalance < currentSurplus)
        {
            var decline = currentSurplus - newBalance;
            impact.Add($"Monthly surplus decreases by ${decline:N2}");
            impact.Add("Potential reduction in available reserves");
        }
        else
        {
            impact.Add("No change in monthly surplus");
        }

        if (newBalance > 0)
        {
            impact.Add("Positive cash flow maintained");
        }
        else
        {
            impact.Add("Warning: Negative cash flow - service sustainability at risk");
        }

        return string.Join("\n• ", impact);
    }

    /// <summary>
    /// Generate recommendations for scenario
    /// </summary>
    private List<string> GenerateScenarioRecommendations(decimal newBalance, decimal rateIncrease, decimal expenseChange)
    {
        var recommendations = new List<string>();

        if (newBalance < 0)
        {
            recommendations.Add("Consider additional rate increase to maintain positive cash flow");
            recommendations.Add("Review expense reduction opportunities");
        }
        else if (newBalance > 0 && rateIncrease > 0)
        {
            recommendations.Add("Rate increase appears sustainable");
            recommendations.Add("Monitor customer satisfaction with new rates");
        }

        if (expenseChange > 0)
        {
            recommendations.Add("Monitor expense trends to ensure accuracy of projections");
        }

        if (newBalance > 1000) // Arbitrary threshold for "healthy" surplus
        {
            recommendations.Add("Consider using surplus for infrastructure improvements");
            recommendations.Add("Evaluate reserve fund contributions");
        }

        return recommendations;
    }
}

/// <summary>
/// Service charge recommendation result
/// </summary>
public class ServiceChargeRecommendation
{
    public int EnterpriseId { get; set; }
    public string EnterpriseName { get; set; } = string.Empty;
    public decimal CurrentRate { get; set; }
    public decimal RecommendedRate { get; set; }
    public decimal TotalMonthlyExpenses { get; set; }
    public decimal MonthlyRevenueAtRecommended { get; set; }
    public decimal MonthlySurplus { get; set; }
    public decimal ReserveAllocation { get; set; }
    public BreakEvenAnalysis BreakEvenAnalysis { get; set; } = new();
    public DateTime CalculationDate { get; set; }
    public List<string> Assumptions { get; set; } = new();
}

/// <summary>
/// Break-even analysis result
/// </summary>
public class BreakEvenAnalysis
{
    public decimal BreakEvenRate { get; set; }
    public decimal CurrentSurplusDeficit { get; set; }
    public decimal RequiredRateIncrease { get; set; }
    public decimal CoverageRatio { get; set; }
}

/// <summary>
/// What-if scenario result
/// </summary>
public class WhatIfScenario
{
    public string ScenarioName { get; set; } = string.Empty;
    public decimal CurrentRate { get; set; }
    public decimal ProposedRate { get; set; }
    public decimal CurrentMonthlyExpenses { get; set; }
    public decimal ProposedMonthlyExpenses { get; set; }
    public decimal CurrentMonthlyRevenue { get; set; }
    public decimal ProposedMonthlyRevenue { get; set; }
    public decimal CurrentMonthlyBalance { get; set; }
    public decimal ProposedMonthlyBalance { get; set; }
    public string ImpactAnalysis { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
}
