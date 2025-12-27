namespace WileyWidget.Models;

/// <summary>
/// Entity for persisting current monthly charges per department.
/// Stores user-edited charge amounts for Water, Sewer, Trash, Apartments.
/// </summary>
/// <summary>
/// Represents a class for departmentcurrentcharge.
/// </summary>
public class DepartmentCurrentCharge
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
    /// Current monthly charge per customer/unit
    /// </summary>
    /// <summary>
    /// Gets or sets the currentcharge.
    /// </summary>
    public decimal CurrentCharge { get; set; }

    /// <summary>
    /// Number of customers/units for this department
    /// </summary>
    /// <summary>
    /// Gets or sets the customercount.
    /// </summary>
    public int CustomerCount { get; set; }

    /// <summary>
    /// Last time this charge was updated
    /// </summary>
    /// <summary>
    /// Gets or sets the lastupdated.
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
    /// <summary>
    /// Gets or sets the isactive.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
