namespace WileyWidget.Models
{
    /// <summary>
    /// Chart metric data point
    /// </summary>
    public record Metric(
        string Category,
        double Value,
        string Color
    );
}