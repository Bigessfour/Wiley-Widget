namespace WileyWidget.WinForms.Models;

/// <summary>
/// Represents state/national benchmark data for department rates.
/// Used for comparison against current and suggested charges.
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class StateBenchmarkModel
{
    /// <summary>
    /// Department name: "Water", "Sewer", "Trash", "Apartments"
    /// </summary>
    public required string Department { get; init; }

    /// <summary>
    /// State-wide average rate for this department
    /// </summary>
    public decimal StateAverage { get; init; }

    /// <summary>
    /// Average rate for towns of similar size (population-adjusted)
    /// </summary>
    public decimal TownSizeAverage { get; init; }

    /// <summary>
    /// National average rate (for reference)
    /// </summary>
    public decimal NationalAverage { get; init; }

    /// <summary>
    /// Data source citation (e.g., "AWWA / EPA WaterSense 2024")
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Year of the benchmark data
    /// </summary>
    public int Year { get; init; } = DateTime.Now.Year;

    /// <summary>
    /// Population range this benchmark applies to (e.g., "5,000-10,000")
    /// </summary>
    public string PopulationRange { get; init; } = string.Empty;
}
