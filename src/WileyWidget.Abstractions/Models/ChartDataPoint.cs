namespace WileyWidget.Abstractions.Models
{
    /// <summary>
    /// Lightweight DTO representing a category/value pair used by charts
    /// </summary>
    public class ChartDataPoint
    {
        public string Category { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}