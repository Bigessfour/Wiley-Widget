#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace WileyWidget.Models;

/// <summary>
/// Represents a municipal enterprise (Water, Sewer, Trash, Apartments)
/// </summary>
public class Enterprise : IValidatableObject
{
    #region Properties

    /// <summary>
    /// Unique identifier for the enterprise
    /// </summary>
    [Key]
    [ConcurrencyCheck]
    public int Id { get; set; }

    /// <summary>
    /// Name of the enterprise (Water, Sewer, Trash, Apartments)
    /// </summary>
    [Required(ErrorMessage = "Enterprise name is required")]
    [StringLength(100, ErrorMessage = "Enterprise name cannot exceed 100 characters")]
    [ConcurrencyCheck]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current rate charged per citizen (e.g., $5.00 per month for water)
    /// </summary>
    [Required(ErrorMessage = "Current rate is required")]
    [Range(0.01, 1000, ErrorMessage = "Rates can't be zero—citizens ain't free!")]
    [Column(TypeName = "decimal(18,2)")]
    [ConcurrencyCheck]
    public decimal CurrentRate { get; set; }

    /// <summary>
    /// Monthly expenses (sum of employee compensation + maintenance + other operational costs)
    /// </summary>
    [Required(ErrorMessage = "Monthly expenses are required")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Monthly expenses cannot be negative")]
    [Column(TypeName = "decimal(18,2)")]
    [ConcurrencyCheck]
    public decimal MonthlyExpenses { get; set; }

    /// <summary>
    /// Monthly revenue (calculated as CitizenCount * CurrentRate)
    /// Represents total revenue from all rate payers for this enterprise
    /// </summary>
    [NotMapped]
    public decimal MonthlyRevenue
    {
        get { return CitizenCount * CurrentRate; }
    }

    /// <summary>
    /// Number of rate payers served by this enterprise.
    /// NOTE: In this application, "citizen" refers to any person or entity paying for municipal services
    /// (rate payer), not necessarily a legal citizen. Examples include tenants in apartments,
    /// homeowners, or businesses receiving utility services.
    /// This count is used for revenue calculations and per-person cost analysis.
    /// </summary>
    [Required(ErrorMessage = "Rate payer count is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Rate payer count must be at least 1")]
    [ConcurrencyCheck]
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
    /// Enhanced with Grok for predictive analytics
    /// </summary>
    [NotMapped]
    public decimal MonthlyBalance
    {
        get { return MonthlyRevenue - MonthlyExpenses; }
    }

    /// <summary>
    /// Calculated property: Monthly deficit (Expenses - Revenue)
    /// Can be offloaded to Grok for complex scenario analysis
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
    /// Computed deficit from Grok analysis (can be stored if needed)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ComputedDeficit { get; set; }

    /// <summary>
    /// Suggested rate hike from Grok analysis
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? SuggestedRateHike { get; set; }

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
    [StringLength(100)]
    public string? QboClassId { get; set; }

    /// <summary>
    /// Sync status with QuickBooks Online
    /// </summary>
    public QboSyncStatus QboSyncStatus { get; set; } = QboSyncStatus.Pending;

    /// <summary>
    /// Last sync timestamp with QuickBooks Online
    /// </summary>
    public DateTime? QboLastSync { get; set; }

    #endregion

    #region Calculated Properties
    /// QBO sync: Because manual entries are for chumps.
    /// </summary>
    [NotMapped]
    public bool SyncToQboReady => !string.IsNullOrEmpty(Name) && CurrentRate > 0;

    #endregion

    #region Methods

    /// <summary>
    /// Validates the enterprise data
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Deficit > 0)
        {
            yield return new ValidationResult("Warning: Enterprise is operating at a deficit!", new[] { "Deficit" });
        }
    }

    /// <summary>
    /// Returns a string representation for debug logs
    /// </summary>
    /// <returns>Format: 'EnterpriseName: Deficit/Surplus $X.XX'</returns>
    public override string ToString()
    {
        string status = MonthlyBalance >= 0 ? "Surplus" : "Deficit";
        decimal amount = Math.Abs(MonthlyBalance);
        return $"{Name}: {status} ${amount:F2}";
    }

    #endregion
}
