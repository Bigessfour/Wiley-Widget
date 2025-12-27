namespace WileyWidget.Models;

/// <summary>
/// Entity for persisting AI-recommended goals/adjustments per department.
/// Stores AI-generated adjustment factors from xAI Grok API.
/// </summary>
/// <summary>
/// Represents a class for departmentgoal.
/// </summary>
public class DepartmentGoal
{
    /// <summary>
    /// Primary key
    /// </summary>
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Department name: "Water", "Sewer", "Trash", "Apartments"
    /// </summary>
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// AI-recommended adjustment factor (e.g., 1.15 for 15% profit margin)
    /// </summary>
    /// <summary>
    /// Gets or sets the adjustmentfactor.
    /// </summary>
    public decimal AdjustmentFactor { get; set; } = 1.0m;

    /// <summary>
    /// Target profit margin percentage (e.g., 15.0 for 15%)
    /// </summary>
    /// <summary>
    /// Gets or sets the targetprofitmarginpercent.
    /// </summary>
    public decimal TargetProfitMarginPercent { get; set; }

    /// <summary>
    /// AI-generated recommendation text
    /// </summary>
    public string? RecommendationText { get; set; }

    /// <summary>
    /// Timestamp when AI recommendation was generated
    /// </summary>
    /// <summary>
    /// Gets or sets the generatedat.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source of the recommendation (e.g., "Grok API v1", "Manual")
    /// </summary>
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string Source { get; set; } = "Grok API";

    /// <summary>
    /// Indicates if this goal is currently active
    /// </summary>
    /// <summary>
    /// Gets or sets the isactive.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
