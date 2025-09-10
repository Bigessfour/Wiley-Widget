#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidgetTest;

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
    public decimal MonthlyRevenue
    {
        get { return CitizenCount * CurrentRate; }
    }

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
    public decimal MonthlyBalance
    {
        get { return MonthlyRevenue - MonthlyExpenses; }
    }

    /// <summary>
    /// Calculated property: Monthly deficit (Expenses - Revenue)
    /// Don't let deficits sneak up like unpaid trash bills.
    /// </summary>
    [NotMapped]
    public decimal MonthlyDeficit
    {
        get { return MonthlyExpenses - MonthlyRevenue; }
    }

    /// <summary>
    /// Goal-oriented deficit calculation (Expenses - Revenue)
    /// Because apartments shouldn't apartment your budget.
    /// </summary>
    [NotMapped]
    public decimal Deficit => MonthlyExpenses - MonthlyRevenue;

    /// <summary>
    /// Budget status indicator for visual display
    /// </summary>
    [NotMapped]
    public string BudgetStatus
    {
        get
        {
            if (MonthlyBalance > 0)
                return "Surplus";
            else if (MonthlyBalance < 0)
                return "Deficit";
            else
                return "Break-even";
        }
    }

    /// <summary>
    /// Calculated property: Break-even rate needed to cover expenses
    /// </summary>
    [NotMapped]
    public decimal BreakEvenRate
    {
        get { return CitizenCount > 0 ? MonthlyExpenses / CitizenCount : 0; }
    }

    /// <summary>
    /// Profit margin percentage
    /// </summary>
    [NotMapped]
    public decimal ProfitMargin
    {
        get
        {
            if (MonthlyRevenue == 0) return 0;
            return (MonthlyBalance / MonthlyRevenue) * 100;
        }
    }

    /// <summary>
    /// Revenue per citizen (efficiency metric)
    /// </summary>
    [NotMapped]
    public decimal RevenuePerCitizen
    {
        get { return CitizenCount > 0 ? MonthlyRevenue / CitizenCount : 0; }
    }

    /// <summary>
    /// Expense efficiency ratio (lower is better)
    /// </summary>
    [NotMapped]
    public decimal ExpenseRatio
    {
        get
        {
            if (MonthlyRevenue == 0) return 0;
            return (MonthlyExpenses / MonthlyRevenue) * 100;
        }
    }

    /// <summary>
    /// QuickBooks Online Class ID for fund tracking
    /// </summary>
    [StringLength(50)]
    public string? QboClassId { get; set; }

    /// <summary>
    /// Sync status with QuickBooks Online
    /// </summary>
    public QboSyncStatus QboSyncStatus { get; set; } = QboSyncStatus.Pending;

    /// <summary>
    /// Last sync timestamp with QuickBooks Online
    /// </summary>
    public DateTime? QboLastSync { get; set; }
}
