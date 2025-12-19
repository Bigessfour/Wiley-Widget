#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Represents a budget imported from QuickBooks Online.
/// Maps to QuickBooks Budget entity with all necessary fields for synchronization.
/// </summary>
public class QuickBooksBudget
{
    /// <summary>
    /// Primary key for local database
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// QuickBooks Budget ID (from Intuit API)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string QuickBooksId { get; set; } = string.Empty;

    /// <summary>
    /// Budget name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fiscal year for this budget
    /// </summary>
    [Required]
    public int FiscalYear { get; set; }

    /// <summary>
    /// Start date of the budget period
    /// </summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the budget period
    /// </summary>
    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Budget type (Annual, Quarterly, Monthly, etc.)
    /// </summary>
    [MaxLength(50)]
    public string BudgetType { get; set; } = "Annual";

    /// <summary>
    /// Whether this budget is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Total budgeted amount across all line items
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Last synchronized date from QuickBooks
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    /// <summary>
    /// QuickBooks sync token for optimistic locking
    /// </summary>
    [MaxLength(50)]
    public string? SyncToken { get; set; }

    /// <summary>
    /// Budget line items (details per account)
    /// </summary>
    public ICollection<QuickBooksBudgetLineItem> LineItems { get; set; } = new List<QuickBooksBudgetLineItem>();

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Notes or description for this budget
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Represents a single line item within a QuickBooks budget.
/// Each line item corresponds to a specific account and time period.
/// </summary>
public class QuickBooksBudgetLineItem
{
    /// <summary>
    /// Primary key for local database
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Parent budget ID
    /// </summary>
    [Required]
    public int QuickBooksBudgetId { get; set; }

    /// <summary>
    /// Navigation property to parent budget
    /// </summary>
    [ForeignKey("QuickBooksBudgetId")]
    public QuickBooksBudget Budget { get; set; } = null!;

    /// <summary>
    /// QuickBooks Account ID this line item applies to
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Account name from QuickBooks
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Account number from QuickBooks (if available)
    /// </summary>
    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Budgeted amount for this account in this period
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Period start date for this line item
    /// </summary>
    [Required]
    public DateTime PeriodStartDate { get; set; }

    /// <summary>
    /// Period end date for this line item
    /// </summary>
    [Required]
    public DateTime PeriodEndDate { get; set; }

    /// <summary>
    /// Account type from QuickBooks (Income, Expense, Asset, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? AccountType { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
