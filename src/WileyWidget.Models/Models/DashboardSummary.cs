namespace WileyWidget.Models;

public class DashboardSummary
{
    public IReadOnlyList<DashboardMetric> Metrics { get; init; } = Array.Empty<DashboardMetric>();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string MunicipalityName { get; init; } = "Town of Wiley";
    public int FiscalYear { get; init; } = DateTime.UtcNow.Year;
}