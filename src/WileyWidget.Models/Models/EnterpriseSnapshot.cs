using System;
using System.Collections.Generic;
using System.Globalization;

namespace WileyWidget.Models;

public class EnterpriseSnapshot
{
    public string Name { get; set; } = string.Empty;        // "Water", "Sewer", etc.
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetPosition => Revenue - Expenses;
    public double BreakEvenRatio => Expenses > 0 ? (double)(Revenue / Expenses * 100) : 0;
    public bool IsSelfSustaining => NetPosition >= 0;
    public string CrossSubsidyNote { get; set; } = "Self-funded";
    public List<EnterpriseMonthlyTrendPoint> MonthlyTrend { get; set; } = new();
    public string TrendNarrative { get; set; } = "Twelve-point fiscal trend is unavailable for this enterprise.";
}

public class EnterpriseMonthlyTrendPoint
{
    public DateTime MonthStart { get; set; }
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetPosition => Revenue - Expenses;
    public string MonthLabel => MonthStart.ToString("MMM yy", CultureInfo.CurrentCulture);
}
