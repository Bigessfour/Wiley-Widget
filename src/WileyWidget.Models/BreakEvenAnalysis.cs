using System;

namespace WileyWidget.Models;

/// <summary>
/// Analysis of break-even rate and coverage metrics.
/// </summary>
public class BreakEvenAnalysis
{
    /// <summary>
    /// The rate needed to break even on monthly expenses.
    /// </summary>
    public decimal BreakEvenRate { get; set; }

    /// <summary>
    /// Current monthly balance (surplus/deficit).
    /// </summary>
    public decimal CurrentSurplusDeficit { get; set; }

    /// <summary>
    /// Rate increase required to break even.
    /// </summary>
    public decimal RequiredRateIncrease { get; set; }

    /// <summary>
    /// Ratio of current revenue to total expenses.
    /// </summary>
    public decimal CoverageRatio { get; set; }
}
