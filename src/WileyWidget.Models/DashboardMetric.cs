namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a dashboard metric for display in charts, gauges, and grids.
    /// Used for KPI display on the dashboard.
    /// </summary>
    public class DashboardMetric
    {
        /// <summary>
        /// Unique identifier for the metric.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display title for the metric.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Current value of the metric.
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Previous period value for comparison.
        /// </summary>
        public decimal PreviousValue { get; set; }

        /// <summary>
        /// Percentage change from previous period.
        /// </summary>
        public decimal PercentageChange { get; set; }

        /// <summary>
        /// Category for grouping metrics (e.g., "Budget", "Revenue", "Expense").
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Optional unit of measurement (e.g., "$", "%", "units").
        /// </summary>
        public string Unit { get; set; } = "$";

        /// <summary>
        /// Status indicator (e.g., "Healthy", "Warning", "Critical").
        /// </summary>
        public string Status { get; set; } = "Neutral";
    }
}
