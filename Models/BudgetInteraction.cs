using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents interactions between enterprises (e.g., shared costs, dependencies)
/// </summary>
public class BudgetInteraction
{
    /// <summary>
    /// Unique identifier for the budget interaction
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the primary enterprise
    /// </summary>
    [Required(ErrorMessage = "Primary enterprise is required")]
    public int PrimaryEnterpriseId { get; set; }

    /// <summary>
    /// Foreign key to the secondary enterprise (can be null for enterprise-specific costs)
    /// </summary>
    public int? SecondaryEnterpriseId { get; set; }

    /// <summary>
    /// Type of interaction (SharedCost, Dependency, Transfer, etc.)
    /// </summary>
    [Required(ErrorMessage = "Interaction type is required")]
    [StringLength(50, ErrorMessage = "Interaction type cannot exceed 50 characters")]
    public string InteractionType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the interaction
    /// </summary>
    [Required(ErrorMessage = "Description is required")]
    [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Monthly cost/value of this interaction
    /// </summary>
    [Required(ErrorMessage = "Monthly amount is required")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyAmount { get; set; }

    /// <summary>
    /// Whether this is a cost (true) or revenue (false)
    /// </summary>
    [Required]
    public bool IsCost { get; set; } = true;

    /// <summary>
    /// Notes about this interaction
    /// </summary>
    [StringLength(300, ErrorMessage = "Notes cannot exceed 300 characters")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to primary enterprise
    /// </summary>
    [ForeignKey("PrimaryEnterpriseId")]
    public virtual Enterprise PrimaryEnterprise { get; set; } = null!;

    /// <summary>
    /// Navigation property to secondary enterprise (optional)
    /// </summary>
    [ForeignKey("SecondaryEnterpriseId")]
    public virtual Enterprise SecondaryEnterprise { get; set; }
}
