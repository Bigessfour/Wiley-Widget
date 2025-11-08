using System;
using System.Collections.Generic;

namespace WileyWidget.Models
{
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
        public RateValidationResult RateValidation { get; set; } = new();
        public DateTime CalculationDate { get; set; }
        public List<string> Assumptions { get; set; } = new();
    }

    public class RateValidationResult
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public decimal? SuggestedRate { get; set; }
        public decimal CoverageRatio { get; set; }
        public decimal DebtServiceRatio { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class BreakEvenAnalysis
    {
        public decimal BreakEvenRate { get; set; }
        public decimal CurrentSurplusDeficit { get; set; }
        public decimal RequiredRateIncrease { get; set; }
        public decimal CoverageRatio { get; set; }
    }

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
}
