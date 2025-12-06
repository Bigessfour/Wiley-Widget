using System;
using System.Collections.Generic;

namespace WileyWidget.Models;

/// <summary>
/// Result of validating a proposed rate against budget constraints.
/// </summary>
public class RateValidationResult
{
    /// <summary>
    /// Whether the proposed rate is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Reason for validation result.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Suggested rate if validation failed.
    /// </summary>
    public decimal? SuggestedRate { get; set; }

    /// <summary>
    /// Percentage difference from current rate.
    /// </summary>
    public decimal? PercentageIncrease { get; set; }

    /// <summary>
    /// Impact on monthly revenue.
    /// </summary>
    public decimal? RevenueImpact { get; set; }

    /// <summary>
    /// Budget coverage ratio at proposed rate.
    /// </summary>
    public decimal? CoverageRatio { get; set; }

    /// <summary>
    /// Warning messages about the rate.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Debt service ratio for the rate.
    /// </summary>
    public decimal? DebtServiceRatio { get; set; }
}
