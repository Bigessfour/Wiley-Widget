using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models;

/// <summary>
/// Represents a budget entry with hierarchical support and GASB compliance
/// </summary>
/// <summary>
/// Represents a class for budgetentry.
/// </summary>
public class BudgetEntry : IAuditable
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public int Id { get; set; }

    [Required, MaxLength(50), RegularExpression(@"^\d{3}(\.\d{1,2})?$", ErrorMessage = "AccountNumber must be like '405' or '410.1'")]
    /// <summary>
    /// Gets or sets the accountnumber.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty; // e.g., "410.1"

    [Required, MaxLength(200)]
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    /// <summary>
    /// Gets or sets the budgetedamount.
    /// </summary>
    public decimal BudgetedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    /// <summary>
    /// Gets or sets the actualamount.
    /// </summary>
    public decimal ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    /// <summary>
    /// Gets or sets the variance.
    /// </summary>
    public decimal Variance { get; set; } // Computed in ViewModel, persisted

    public int? ParentId { get; set; } // Hierarchy support
    [ForeignKey("ParentId")]
    public BudgetEntry? Parent { get; set; }
    public ICollection<BudgetEntry> Children { get; set; } = new List<BudgetEntry>();

    // Multi-year support
    [Required]
    /// <summary>
    /// Gets or sets the fiscalyear.
    /// </summary>
    public int FiscalYear { get; set; } // e.g., 2026
    /// <summary>
    /// Gets or sets the startperiod.
    /// </summary>
    public DateTime StartPeriod { get; set; }
    /// <summary>
    /// Gets or sets the endperiod.
    /// </summary>
    public DateTime EndPeriod { get; set; }

    // GASB compliance
    /// <summary>
    /// Gets or sets the fundtype.
    /// </summary>
    public FundType FundType { get; set; } // Enum
    [Column(TypeName = "decimal(18,2)")]
    /// <summary>
    /// Gets or sets the encumbranceamount.
    /// </summary>
    public decimal EncumbranceAmount { get; set; } // Reserved funds
    /// <summary>
    /// Gets or sets the isgasbcompliant.
    /// </summary>
    public bool IsGASBCompliant { get; set; } = true;

    // Relationships
    /// <summary>
    /// Gets or sets the departmentid.
    /// </summary>
    public int DepartmentId { get; set; }
    [ForeignKey("DepartmentId")]
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    public Department Department { get; set; } = null!;
    // Convenience property for DepartmentCode
    public string? DepartmentCode { get; set; }
    public int? FundId { get; set; }
    [ForeignKey("FundId")]
    public Fund? Fund { get; set; }
    /// <summary>
    /// Gets or sets the municipalaccountid.
    /// </summary>
    public int MunicipalAccountId { get; set; }
    [ForeignKey("MunicipalAccountId")]
    /// <summary>
    /// Gets or sets the municipalaccount.
    /// </summary>
    public MunicipalAccount MunicipalAccount { get; set; } = null!;

    // Local Excel import tracking
    [MaxLength(500)]
    public string? SourceFilePath { get; set; } // e.g., "C:\Budgets\TOW_2026.xlsx"
    // New: Excel metadata
    public int? SourceRowNumber { get; set; } // For error reporting
    // New: GASB activity code
    [MaxLength(10)]
    public string? ActivityCode { get; set; } // e.g., "GOV" for governmental
    // New: Transactions
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    // Auditing (simplified)
    /// <summary>
    /// Gets or sets the createdat.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Computed properties for compatibility
    public decimal TotalBudget => BudgetedAmount;
    public decimal ActualSpent => ActualAmount;
}
