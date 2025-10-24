#nullable enable

using System.Collections.ObjectModel;

namespace WileyWidget.Services;

/// <summary>
/// Represents a series of data points for charting
/// </summary>
public class ChartSeries
{
    /// <summary>
    /// Gets or sets the name of the series
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of data points
    /// </summary>
    public ObservableCollection<ChartDataPoint> DataPoints { get; set; } = new();

    /// <summary>
    /// Gets or sets the color of the series
    /// </summary>
    public string? Color { get; set; }
}

/// <summary>
/// Represents a single data point in a chart series
/// </summary>
public class ChartDataPoint
{
    /// <summary>
    /// Gets or sets the X value (category or date)
    /// </summary>
    public object? XValue { get; set; }

    /// <summary>
    /// Gets or sets the Y value (numeric value)
    /// </summary>
    public double YValue { get; set; }

    /// <summary>
    /// Gets or sets the label for the data point
    /// </summary>
    public string? Label { get; set; }
}
