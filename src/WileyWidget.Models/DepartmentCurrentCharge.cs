namespace WileyWidget.Models;

/// <summary>
/// Entity for persisting current monthly charges per department.
/// Stores user-edited charge amounts for Water, Sewer, Trash, Apartments.
/// </summary>
public class DepartmentCurrentCharge
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Department name: "Water", "Sewer", "Trash", "Apartments"
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Current monthly charge per customer/unit
    /// </summary>
    public decimal CurrentCharge { get; set; }

    /// <summary>
    /// Number of customers/units for this department
    /// </summary>
    public int CustomerCount { get; set; }

    /// <summary>
    /// Last time this charge was updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who last updated this charge
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Optional notes about the charge
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Indicates if this charge is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
