namespace WileyWidget.WinForms.Models;

/// <summary>
/// Represents state/national benchmark data for department rates.
/// Used for comparison against current and suggested charges.
/// </summary>
/// <summary>
/// Represents a class for statebenchmarkmodel.
/// </summary>
/// <summary>
/// Represents a class for statebenchmarkmodel.
/// </summary>
/// <summary>
/// Represents a class for statebenchmarkmodel.
/// </summary>
/// <summary>
/// Represents a class for statebenchmarkmodel.
/// </summary>
public class StateBenchmarkModel
{
    /// <summary>
    /// Department name: "Water", "Sewer", "Trash", "Apartments"
    /// </summary>
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// State-wide average rate for this department
    /// </summary>
    /// <summary>
    /// Gets or sets the stateaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the stateaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the stateaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the stateaverage.
    /// </summary>
    public decimal StateAverage { get; set; }

    /// <summary>
    /// Average rate for towns of similar size (population-adjusted)
    /// </summary>
    /// <summary>
    /// Gets or sets the townsizeaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the townsizeaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the townsizeaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the townsizeaverage.
    /// </summary>
    public decimal TownSizeAverage { get; set; }

    /// <summary>
    /// National average rate (for reference)
    /// </summary>
    /// <summary>
    /// Gets or sets the nationalaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the nationalaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the nationalaverage.
    /// </summary>
    /// <summary>
    /// Gets or sets the nationalaverage.
    /// </summary>
    public decimal NationalAverage { get; set; }

    /// <summary>
    /// Data source citation (e.g., "AWWA / EPA WaterSense 2024")
    /// </summary>
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Year of the benchmark data
    /// </summary>
    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    public int Year { get; set; } = DateTime.Now.Year;

    /// <summary>
    /// Population range this benchmark applies to (e.g., "5,000-10,000")
    /// </summary>
    /// <summary>
    /// Gets or sets the populationrange.
    /// </summary>
    /// <summary>
    /// Gets or sets the populationrange.
    /// </summary>
    /// <summary>
    /// Gets or sets the populationrange.
    /// </summary>
    /// <summary>
    /// Gets or sets the populationrange.
    /// </summary>
    public string PopulationRange { get; set; } = string.Empty;
}
