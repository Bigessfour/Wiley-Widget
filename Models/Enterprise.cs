using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents a municipal enterprise (Water, Sewer, Trash, Apartments)
/// </summary>
public class Enterprise
{
    /// <summary>
    /// Unique identifier for the enterprise
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Name of the enterprise (Water, Sewer, Trash, Apartments)
    /// </summary>
    [Required(ErrorMessage = "Enterprise name is required")]
    [StringLength(100, ErrorMessage = "Enterprise name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current rate charged per citizen (e.g., $5.00 per month for water)
    /// </summary>
    [Required(ErrorMessage = "Current rate is required")]
    [Range(0.01, 9999.99, ErrorMessage = "Rate must be between 0.01 and 9999.99")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentRate { get; set; }

    /// <summary>
    /// Monthly expenses (sum of employee compensation + maintenance + other operational costs)
    /// </summary>
    [Required(ErrorMessage = "Monthly expenses are required")]
    [Range(0, double.MaxValue, ErrorMessage = "Monthly expenses cannot be negative")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyExpenses { get; set; }

    /// <summary>
    /// Monthly revenue (calculated as CitizenCount * CurrentRate)
    /// </summary>
    [NotMapped]
    public decimal MonthlyRevenue => CitizenCount * CurrentRate;

    /// <summary>
    /// Number of citizens served by this enterprise
    /// </summary>
    [Required(ErrorMessage = "Citizen count is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Citizen count must be at least 1")]
    public int CitizenCount { get; set; }

    /// <summary>
    /// Additional notes about the enterprise
    /// </summary>
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property for budget interactions
    /// </summary>
    public virtual ICollection<BudgetInteraction> BudgetInteractions { get; set; } = new List<BudgetInteraction>();

    /// <summary>
    /// Calculated property: Monthly deficit/surplus (Revenue - Expenses)
    /// </summary>
    [NotMapped]
    public decimal MonthlyBalance => MonthlyRevenue - MonthlyExpenses;

    /// <summary>
    /// Calculated property: Break-even rate needed to cover expenses
    /// </summary>
    [NotMapped]
    public decimal BreakEvenRate => CitizenCount > 0 ? MonthlyExpenses / CitizenCount : 0;
}
