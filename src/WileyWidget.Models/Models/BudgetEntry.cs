using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WileyWidget.Models.Entities;

namespace WileyWidget.Models;

/// <summary>
/// Represents a budget entry with hierarchical support and GASB compliance
/// </summary>
public class BudgetEntry : IAuditable
{
    public int Id { get; set; }

    [Required, MaxLength(50), RegularExpression(@"^\d{3}(\.\d{1,2})?$", ErrorMessage = "AccountNumber must be like '405' or '410.1'")]
    public string AccountNumber { get; set; } = string.Empty; // e.g., "410.1"

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Variance { get; set; } // Computed in ViewModel, persisted

    public int? ParentId { get; set; } // Hierarchy support
    [ForeignKey("ParentId")]
    public BudgetEntry? Parent { get; set; }
    public ICollection<BudgetEntry> Children { get; set; } = new List<BudgetEntry>();

    // Multi-year support
    [Required]
    public int FiscalYear { get; set; } // e.g., 2026
    public DateTime StartPeriod { get; set; }
    public DateTime EndPeriod { get; set; }

    // GASB compliance
    public FundType FundType { get; set; } // Enum
    [Column(TypeName = "decimal(18,2)")]
    public decimal EncumbranceAmount { get; set; } // Reserved funds
    public bool IsGASBCompliant { get; set; } = true;

    // Relationships
    public int DepartmentId { get; set; }
    [ForeignKey("DepartmentId")]
    public Department Department { get; set; } = null!;
    public int? FundId { get; set; }
    [ForeignKey("FundId")]
    public Fund? Fund { get; set; }
    public int MunicipalAccountId { get; set; }
    [ForeignKey("MunicipalAccountId")]
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Computed properties for compatibility
    public decimal TotalBudget => BudgetedAmount;
    public decimal ActualSpent => ActualAmount;
}
