using System;
using System.Collections.Generic;

namespace WileyWidget.Models;

/// <summary>
/// Represents AI-generated recommendations for service charges to customers
/// Includes analysis and suggestions for utility billing optimization
/// </summary>
public class ServiceChargeRecommendation
{
    /// <summary>
    /// Gets or sets the recommendation identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the utility customer ID
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the utility type (Water, Sewer, Garbage, etc.)
    /// </summary>
    public string UtilityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recommended charge amount
    /// </summary>
    public decimal RecommendedCharge { get; set; }

    /// <summary>
    /// Gets or sets the current/baseline charge for comparison
    /// </summary>
    public decimal CurrentCharge { get; set; }

    /// <summary>
    /// Gets or sets the confidence level (0-100) of the recommendation
    /// </summary>
    public decimal ConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets the reason/explanation for the recommendation
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recommended action
    /// </summary>
    public string RecommendedAction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority level (Low, Medium, High, Critical)
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Gets or sets whether this recommendation has been reviewed
    /// </summary>
    public bool IsReviewed { get; set; }

    /// <summary>
    /// Gets or sets whether this recommendation has been approved
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// Gets or sets the date when the recommendation was generated
    /// </summary>
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date when the recommendation was reviewed
    /// </summary>
    public DateTime? ReviewedDate { get; set; }

    /// <summary>
    /// Gets or sets the related analysis data
    /// </summary>
    public string AnalysisData { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the projected impact (monthly revenue impact if approved)
    /// </summary>
    public decimal ProjectedImpact { get; set; }

    /// <summary>
    /// Gets or sets optional notes about the recommendation
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Gets a percentage change from current to recommended charge
    /// </summary>
    public decimal PercentageChange =>
        CurrentCharge != 0 ? ((RecommendedCharge - CurrentCharge) / CurrentCharge) * 100 : 0;

    // ----- Compatibility / extended properties used by services -----
    /// <summary>
    /// Enterprise (utility) id the recommendation belongs to
    /// </summary>
    public int EnterpriseId { get; set; }

    /// <summary>
    /// Human readable enterprise / utility name
    /// </summary>
    public string EnterpriseName { get; set; } = string.Empty;

    /// <summary>
    /// Alias for CurrentCharge used by some services
    /// </summary>
    public decimal CurrentRate { get => CurrentCharge; set => CurrentCharge = value; }

    /// <summary>
    /// Alias for RecommendedCharge used by some services
    /// </summary>
    public decimal RecommendedRate { get => RecommendedCharge; set => RecommendedCharge = value; }

    /// <summary>
    /// Total monthly operating expenses used for calculations
    /// </summary>
    public decimal TotalMonthlyExpenses { get; set; }

    /// <summary>
    /// Monthly revenue expected at the recommended rate
    /// </summary>
    public decimal MonthlyRevenueAtRecommended { get; set; }

    /// <summary>
    /// Monthly surplus after applying recommended revenue - expenses
    /// </summary>
    public decimal MonthlySurplus { get; set; }

    /// <summary>
    /// Amount reserved for operating reserves from revenue
    /// </summary>
    public decimal ReserveAllocation { get; set; }

    /// <summary>
    /// Optional break-even analysis result attached to recommendation
    /// </summary>
    public BreakEvenAnalysis? BreakEvenAnalysis { get; set; }

    /// <summary>
    /// Rate validation information (warnings, suggested rate, etc.)
    /// </summary>
    public RateValidationResult? RateValidation { get; set; }

    /// <summary>
    /// Timestamp when this recommendation was calculated
    /// </summary>
    public DateTime CalculationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Assumptions used to create the recommendation
    /// </summary>
    public List<string> Assumptions { get; set; } = new();
}
