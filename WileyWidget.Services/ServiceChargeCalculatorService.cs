using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using BusinessInterfaces = WileyWidget.Business.Interfaces;
// Ensure FundType is available in this context

namespace WileyWidget.Services;

/// <summary>
/// Service for calculating recommended monthly service charges based on actual expenses
/// with comprehensive rate validation and budget logic
/// </summary>
public class ServiceChargeCalculatorService : IChargeCalculatorService
{
    private readonly BusinessInterfaces.IEnterpriseRepository _enterpriseRepository;
    private readonly BusinessInterfaces.IMunicipalAccountRepository _municipalAccountRepository;
    private readonly BusinessInterfaces.IBudgetRepository _budgetRepository;

    // Rate validation constants
    private const decimal MIN_RATE_PER_CUSTOMER = 1.00m;
    private const decimal MAX_RATE_PER_CUSTOMER = 500.00m;
    private const decimal MAX_RATE_INCREASE_PERCENTAGE = 25.0m; // Max 25% increase
    private const decimal MIN_COVERAGE_RATIO = 0.95m; // Must cover at least 95% of expenses
    private const decimal TARGET_RESERVE_RATIO = 0.10m; // Target 10% reserves
    private const decimal MAX_DEBT_SERVICE_RATIO = 0.20m; // Max 20% of revenue for debt service

    public ServiceChargeCalculatorService(
        BusinessInterfaces.IEnterpriseRepository enterpriseRepository,
        BusinessInterfaces.IMunicipalAccountRepository municipalAccountRepository,
        BusinessInterfaces.IBudgetRepository budgetRepository)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
    }

    // Parameterless constructor for testing/mocking
    protected ServiceChargeCalculatorService()
    {
        _enterpriseRepository = null!;
        _municipalAccountRepository = null!;
        _budgetRepository = null!;
    }

    /// <summary>
    /// Calculate recommended monthly service charge for an enterprise with rate validation
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

            // Get current budget data for validation
            var currentBudgets = await _budgetRepository.GetByFiscalYearAsync(DateTime.Now.Year);

            // Calculate total monthly expenses from accounts
            var totalMonthlyExpenses = expenseAccounts
                .Where(a => a.Type == AccountType.Expense && a.BudgetAmount > 0)
                .Sum(a => a.BudgetAmount / 12); // Convert annual budget to monthly

            // Add operational expenses from enterprise
            totalMonthlyExpenses += enterprise.MonthlyExpenses;

            // Calculate recommended charge with markup for reserves and profit
            var recommendedCharge = CalculateChargeWithReserves(totalMonthlyExpenses, enterprise.CitizenCount);

            // Validate the recommended rate against budgets and constraints
            var validationResult = await ValidateRateAgainstBudgetAsync(
                enterpriseId, recommendedCharge.RecommendedRate, currentBudgets, totalMonthlyExpenses, fundType);

            // Adjust rate if validation fails
            if (!validationResult.IsValid && validationResult.SuggestedRate.HasValue)
            {
                recommendedCharge = CalculateChargeWithReserves(totalMonthlyExpenses, enterprise.CitizenCount, validationResult.SuggestedRate.Value);
                Log.Warning("Rate adjusted from {OriginalRate} to {AdjustedRate} due to validation: {Reason}",
                    recommendedCharge.RecommendedRate, validationResult.SuggestedRate.Value, validationResult.Reason);
            }

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
                RateValidation = validationResult,
                CalculationDate = DateTime.Now,
                Assumptions = new List<string>
                {
                    "10% operating reserve allocation",
                    "5% profit margin for sustainability",
                    "Based on current expense accounts and enterprise data",
                    "Monthly calculations based on annual budgets divided by 12",
                    $"Rate validated against {currentBudgets.Count()} budget entries",
                    "Maximum rate increase limited to 25% annually"
                }
            };

            Log.Information("Calculated service charge recommendation for {Enterprise}: Current ${CurrentRate}, Recommended ${RecommendedRate}, Validation: {Valid}",
                enterprise.Name, enterprise.CurrentRate, recommendedCharge.RecommendedRate, validationResult.IsValid);

            return recommendation;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating service charge for enterprise {EnterpriseId}", enterpriseId);
            throw;
        }
    }

    /// <summary>
    /// Calculates the service charge for a given amount.
    /// </summary>
    /// <param name="amount">The amount to calculate charge for</param>
    /// <returns>The calculated charge</returns>
    public decimal CalculateCharge(decimal amount)
    {
        // Simple calculation: assume 1% of the amount as charge
        // This is a basic implementation - in practice this would be more complex
        var charge = amount * 0.01m;

        // Ensure charge is within valid range
        charge = Math.Max(MIN_RATE_PER_CUSTOMER, Math.Min(MAX_RATE_PER_CUSTOMER, charge));

        Log.Information("Calculated service charge of {Charge} for amount {Amount}", charge, amount);
        return charge;
    }

    /// <summary>
    /// Validate a proposed rate against budget constraints and business rules
    /// </summary>
    private FundType MapMunicipalFundTypeToFundType(MunicipalFundType municipalFundType)
    {
        // Map MunicipalFundType to GASB-compliant FundType
        return municipalFundType switch
        {
            MunicipalFundType.Water => FundType.EnterpriseFund,
            MunicipalFundType.Sewer => FundType.EnterpriseFund,
            MunicipalFundType.Trash => FundType.EnterpriseFund,
            MunicipalFundType.Utility => FundType.EnterpriseFund,
            MunicipalFundType.General => FundType.GeneralFund,
            MunicipalFundType.Enterprise => FundType.EnterpriseFund,
            MunicipalFundType.SpecialRevenue => FundType.SpecialRevenue,
            MunicipalFundType.CapitalProjects => FundType.CapitalProjects,
            MunicipalFundType.DebtService => FundType.DebtService,
            MunicipalFundType.InternalService => FundType.EnterpriseFund,
            MunicipalFundType.Trust => FundType.PermanentFund,
            MunicipalFundType.Agency => FundType.PermanentFund,
            MunicipalFundType.ConservationTrust => FundType.PermanentFund,
            MunicipalFundType.Recreation => FundType.SpecialRevenue,
            _ => FundType.GeneralFund
        };
    }

    private async Task<RateValidationResult> ValidateRateAgainstBudgetAsync(
        int enterpriseId, decimal proposedRate, IEnumerable<BudgetEntry> budgets, decimal totalMonthlyExpenses, MunicipalFundType fundType)
    {
        var validationResult = new RateValidationResult { IsValid = true };

        // Get enterprise for current rate comparison
        var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
        if (enterprise == null)
        {
            validationResult.IsValid = false;
            validationResult.Reason = "Enterprise not found";
            return validationResult;
        }

        // Use the fundType parameter for budget filtering

        // 1. Check minimum and maximum rate constraints
        if (proposedRate < MIN_RATE_PER_CUSTOMER)
        {
            validationResult.IsValid = false;
            validationResult.Reason = $"Rate ${proposedRate} below minimum ${MIN_RATE_PER_CUSTOMER} per customer";
            validationResult.SuggestedRate = MIN_RATE_PER_CUSTOMER;
            return validationResult;
        }

        if (proposedRate > MAX_RATE_PER_CUSTOMER)
        {
            validationResult.IsValid = false;
            validationResult.Reason = $"Rate ${proposedRate} exceeds maximum ${MAX_RATE_PER_CUSTOMER} per customer";
            validationResult.SuggestedRate = MAX_RATE_PER_CUSTOMER;
            return validationResult;
        }

        // 2. Check maximum rate increase constraint
        var maxAllowedRate = enterprise.CurrentRate * (1 + MAX_RATE_INCREASE_PERCENTAGE / 100);
        if (proposedRate > maxAllowedRate)
        {
            validationResult.IsValid = false;
            validationResult.Reason = $"Rate increase exceeds maximum {MAX_RATE_INCREASE_PERCENTAGE}% limit";
            validationResult.SuggestedRate = Math.Round(maxAllowedRate, 2);
            return validationResult;
        }

        // 3. Check coverage ratio (revenue must cover expenses)
        var projectedMonthlyRevenue = proposedRate * enterprise.CitizenCount;
        var coverageRatio = projectedMonthlyRevenue / totalMonthlyExpenses;

        if (coverageRatio < MIN_COVERAGE_RATIO)
        {
            validationResult.IsValid = false;
            validationResult.Reason = $"Coverage ratio {coverageRatio:P1} below minimum {MIN_COVERAGE_RATIO:P0}";
            var requiredRevenue = totalMonthlyExpenses * MIN_COVERAGE_RATIO;
            return validationResult;
        }

        // 4. Check budget allocations for reasonableness
        var totalBudgetedExpenses = budgets
            .Where(b => b.FundType == MapMunicipalFundTypeToFundType(fundType))
            .Sum(b => b.BudgetedAmount);

        if (totalBudgetedExpenses > 0)
        {
            var budgetRatio = totalMonthlyExpenses * 12 / totalBudgetedExpenses; // Annualize for comparison
            if (budgetRatio > 1.5m) // Expenses 50% over budget
            {
                validationResult.Warnings.Add($"Expenses are {budgetRatio:P0} of budgeted amount - consider budget review");
            }
        }

        // 5. Check debt service ratio if applicable
        var debtServiceExpenses = budgets
            .Where(b => b.Description.Contains("debt", StringComparison.OrdinalIgnoreCase) ||
                       b.Description.Contains("interest", StringComparison.OrdinalIgnoreCase))
            .Sum(b => b.BudgetedAmount / 12); // Monthly

        if (debtServiceExpenses > 0)
        {
            var debtServiceRatio = debtServiceExpenses / projectedMonthlyRevenue;
            if (debtServiceRatio > MAX_DEBT_SERVICE_RATIO)
            {
                validationResult.Warnings.Add($"Debt service ratio {debtServiceRatio:P1} exceeds recommended maximum {MAX_DEBT_SERVICE_RATIO:P0}");
            }
        }

        validationResult.CoverageRatio = coverageRatio;
        validationResult.DebtServiceRatio = debtServiceExpenses > 0 ? debtServiceExpenses / projectedMonthlyRevenue : 0;

        return validationResult;
    }

    /// <summary>
    /// Calculate charge including reserves and profit margins
    /// </summary>
    private (decimal RecommendedRate, decimal MonthlyRevenue, decimal ReserveAllocation) CalculateChargeWithReserves(decimal totalMonthlyExpenses, int citizenCount, decimal? overrideRate = null)
    {
        if (citizenCount <= 0)
            throw new ArgumentException("Citizen count must be greater than 0");

        // If override rate is provided, use it instead of calculating
        if (overrideRate.HasValue)
        {
            var overrideMonthlyRevenue = overrideRate.Value * citizenCount;
            var overrideReserveAllocation = totalMonthlyExpenses * TARGET_RESERVE_RATIO;
            return (overrideRate.Value, overrideMonthlyRevenue, overrideReserveAllocation);
        }

        // Add 10% for operating reserves
        var expensesWithReserves = totalMonthlyExpenses * (1 + TARGET_RESERVE_RATIO);

        // Add 5% for profit/sustainability margin
        var expensesWithProfit = expensesWithReserves * 1.05m;

        // Calculate per-citizen rate
        var recommendedRate = Math.Round(expensesWithProfit / citizenCount, 2);

        // Calculate monthly revenue at recommended rate
        var monthlyRevenue = recommendedRate * citizenCount;

        // Calculate reserve allocation
        var reserveAllocation = totalMonthlyExpenses * TARGET_RESERVE_RATIO;

        return (recommendedRate, monthlyRevenue, reserveAllocation);
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
}

// DTO types for service charge recommendation and scenarios were moved to
// the WileyWidget.Models project so they can be shared by abstractions and
// other consumers. The service implementation now uses the model types.
