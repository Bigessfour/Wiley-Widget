using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models;

public class DashboardMetric
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public MetricTrend Trend { get; set; } = MetricTrend.Neutral;
}

public enum MetricTrend { Up, Down, Neutral }