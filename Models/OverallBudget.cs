using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents the overall municipal budget summary
/// </summary>
public class OverallBudget
{
    /// <summary>
    /// Unique identifier for the budget snapshot
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Date this budget snapshot was created
    /// </summary>
    [Required]
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total monthly revenue from all enterprises
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalMonthlyRevenue { get; set; }

    /// <summary>
    /// Total monthly expenses from all enterprises
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalMonthlyExpenses { get; set; }

    /// <summary>
    /// Total monthly surplus/deficit
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalMonthlyBalance { get; set; }

    /// <summary>
    /// Total number of citizens served
    /// </summary>
    [Required]
    public int TotalCitizensServed { get; set; }

    /// <summary>
    /// Average rate per citizen across all enterprises
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageRatePerCitizen { get; set; }

    /// <summary>
    /// Notes about this budget snapshot
    /// </summary>
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the current active budget snapshot
    /// </summary>
    [Required]
    public bool IsCurrent { get; set; } = false;

    /// <summary>
    /// Calculated property: Whether the municipality is running a surplus
    /// </summary>
    [NotMapped]
    public bool IsSurplus => TotalMonthlyBalance > 0;

    /// <summary>
    /// Calculated property: Deficit percentage (negative if surplus)
    /// </summary>
    [NotMapped]
    public decimal DeficitPercentage => TotalMonthlyRevenue > 0 ?
        ((TotalMonthlyBalance / TotalMonthlyRevenue) * 100) : 0;
}
