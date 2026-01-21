#nullable enable

namespace WileyWidget.WinForms.Models;

/// <summary>
/// Represents a single row of budget analytics data.
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class BudgetAnalyticsData
{
    /// <summary>Gets the department name.</summary>
    public required string DepartmentName { get; init; }

    /// <summary>Gets the period name (month/quarter/year).</summary>
    public required string PeriodName { get; init; }

    /// <summary>Gets the budgeted amount.</summary>
    public decimal BudgetedAmount { get; init; }

    /// <summary>Gets the actual amount spent.</summary>
    public decimal ActualAmount { get; init; }

    /// <summary>Gets the variance amount (actual - budgeted).</summary>
    public decimal VarianceAmount { get; init; }

    /// <summary>Gets the variance percentage.</summary>
    public string VariancePercent { get; init; } = "0";

    /// <summary>Gets the budget status (Over Budget / Under Budget).</summary>
    public string Status { get; init; } = string.Empty;
}
