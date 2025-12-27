using System;
using System.Collections.Generic;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a class for servicechargerecommendation.
    /// </summary>
    public class ServiceChargeRecommendation
    {
        /// <summary>
        /// Gets or sets the enterpriseid.
        /// </summary>
        public int EnterpriseId { get; set; }
        /// <summary>
        /// Gets or sets the enterprisename.
        /// </summary>
        public string EnterpriseName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the currentrate.
        /// </summary>
        /// <summary>
        /// Gets or sets the currentrate.
        /// </summary>
        public decimal CurrentRate { get; set; }
        /// <summary>
        /// Gets or sets the recommendedrate.
        /// </summary>
        public decimal RecommendedRate { get; set; }
        /// <summary>
        /// Gets or sets the totalmonthlyexpenses.
        /// </summary>
        public decimal TotalMonthlyExpenses { get; set; }
        /// <summary>
        /// Gets or sets the monthlyrevenueatrecommended.
        /// </summary>
        public decimal MonthlyRevenueAtRecommended { get; set; }
        /// <summary>
        /// Gets or sets the monthlysurplus.
        /// </summary>
        public decimal MonthlySurplus { get; set; }
        /// <summary>
        /// Gets or sets the reserveallocation.
        /// </summary>
        public decimal ReserveAllocation { get; set; }
        /// <summary>
        /// Gets or sets the breakevenanalysis.
        /// </summary>
        public BreakEvenAnalysis BreakEvenAnalysis { get; set; } = new();
        /// <summary>
        /// Gets or sets the ratevalidation.
        /// </summary>
        public RateValidationResult RateValidation { get; set; } = new();
        /// <summary>
        /// Gets or sets the calculationdate.
        /// </summary>
        public DateTime CalculationDate { get; set; }
        public List<string> Assumptions { get; set; } = new();
    }
    /// <summary>
    /// Represents a class for ratevalidationresult.
    /// </summary>

    public class RateValidationResult
    {
        /// <summary>
        /// Gets or sets the isvalid.
        /// </summary>
        public bool IsValid { get; set; }
        /// <summary>
        /// Gets or sets the reason.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
        public decimal? SuggestedRate { get; set; }
        /// <summary>
        /// Gets or sets the coverageratio.
        /// </summary>
        /// <summary>
        /// Gets or sets the coverageratio.
        /// </summary>
        public decimal CoverageRatio { get; set; }
        /// <summary>
        /// Gets or sets the debtserviceratio.
        /// </summary>
        public decimal DebtServiceRatio { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
    /// <summary>
    /// Represents a class for breakevenanalysis.
    /// </summary>

    public class BreakEvenAnalysis
    {
        /// <summary>
        /// Gets or sets the breakevenrate.
        /// </summary>
        public decimal BreakEvenRate { get; set; }
        /// <summary>
        /// Gets or sets the currentsurplusdeficit.
        /// </summary>
        public decimal CurrentSurplusDeficit { get; set; }
        /// <summary>
        /// Gets or sets the requiredrateincrease.
        /// </summary>
        public decimal RequiredRateIncrease { get; set; }
        public decimal CoverageRatio { get; set; }
    }
    /// <summary>
    /// Represents a class for whatifscenario.
    /// </summary>

    public class WhatIfScenario
    {
        /// <summary>
        /// Gets or sets the scenarioname.
        /// </summary>
        public string ScenarioName { get; set; } = string.Empty;
        public decimal CurrentRate { get; set; }
        /// <summary>
        /// Gets or sets the proposedrate.
        /// </summary>
        public decimal ProposedRate { get; set; }
        /// <summary>
        /// Gets or sets the currentmonthlyexpenses.
        /// </summary>
        public decimal CurrentMonthlyExpenses { get; set; }
        /// <summary>
        /// Gets or sets the proposedmonthlyexpenses.
        /// </summary>
        public decimal ProposedMonthlyExpenses { get; set; }
        /// <summary>
        /// Gets or sets the currentmonthlyrevenue.
        /// </summary>
        public decimal CurrentMonthlyRevenue { get; set; }
        /// <summary>
        /// Gets or sets the proposedmonthlyrevenue.
        /// </summary>
        public decimal ProposedMonthlyRevenue { get; set; }
        /// <summary>
        /// Gets or sets the currentmonthlybalance.
        /// </summary>
        public decimal CurrentMonthlyBalance { get; set; }
        /// <summary>
        /// Gets or sets the proposedmonthlybalance.
        /// </summary>
        public decimal ProposedMonthlyBalance { get; set; }
        /// <summary>
        /// Gets or sets the impactanalysis.
        /// </summary>
        public string ImpactAnalysis { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
    }
}
