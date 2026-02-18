namespace WileyWidget.Models;

/// <summary>
/// Persisted snapshot of scenario inputs and calculated outputs from the Analytics Hub Scenarios tab.
/// </summary>
public class SavedScenarioSnapshot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal RateIncreasePercent { get; set; }

    public decimal ExpenseIncreasePercent { get; set; }

    public decimal RevenueTarget { get; set; }

    public decimal ProjectedValue { get; set; }

    public decimal Variance { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
