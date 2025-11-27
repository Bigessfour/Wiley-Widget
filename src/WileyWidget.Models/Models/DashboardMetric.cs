using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a dashboard metric with trend information
    /// </summary>
    public class DashboardMetric
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public Trend Trend { get; set; } = Trend.Stable;
        public double ChangePercent { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Trend direction for metrics
    /// </summary>
    public enum Trend
    {
        Up,
        Down,
        Stable
    }
}