#nullable enable

namespace WileyWidget.WinForms.Models;

/// <summary>
/// Represents a single row of budget analytics data.
/// </summary>
public class BudgetAnalyticsData
{
    /// <summary>Gets or sets the department name.</summary>
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>Gets or sets the period name (month/quarter/year).</summary>
    public string PeriodName { get; set; } = string.Empty;

    /// <summary>Gets or sets the budgeted amount.</summary>
    public decimal BudgetedAmount { get; set; }

    /// <summary>Gets or sets the actual amount spent.</summary>
    public decimal ActualAmount { get; set; }

    /// <summary>Gets or sets the variance amount (actual - budgeted).</summary>
    public decimal VarianceAmount { get; set; }

    /// <summary>Gets or sets the variance percentage.</summary>
    public string VariancePercent { get; set; } = "0";

    /// <summary>Gets or sets the budget status (Over Budget / Under Budget).</summary>
    public string Status { get; set; } = string.Empty;
}
