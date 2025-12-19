namespace WileyWidget.WinForms.Models;

/// <summary>
/// Represents state/national benchmark data for department rates.
/// Used for comparison against current and suggested charges.
/// </summary>
public class StateBenchmarkModel
{
    /// <summary>
    /// Department name: "Water", "Sewer", "Trash", "Apartments"
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// State-wide average rate for this department
    /// </summary>
    public decimal StateAverage { get; set; }

    /// <summary>
    /// Average rate for towns of similar size (population-adjusted)
    /// </summary>
    public decimal TownSizeAverage { get; set; }

    /// <summary>
    /// National average rate (for reference)
    /// </summary>
    public decimal NationalAverage { get; set; }

    /// <summary>
    /// Data source citation (e.g., "AWWA / EPA WaterSense 2024")
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Year of the benchmark data
    /// </summary>
    public int Year { get; set; } = DateTime.Now.Year;

    /// <summary>
    /// Population range this benchmark applies to (e.g., "5,000-10,000")
    /// </summary>
    public string PopulationRange { get; set; } = string.Empty;
}
