using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Represents a financial metric for display in the dashboard grid.
    /// </summary>
    public class FinancialMetric
    {
        public string Category { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string FormattedValue { get; set; } = string.Empty;
        public string Color { get; set; } = "#000000";
        public string Icon { get; set; } = string.Empty;
    }
}