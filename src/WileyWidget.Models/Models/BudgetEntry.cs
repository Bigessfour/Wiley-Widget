using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
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
    public int? MunicipalAccountId { get; set; }
    [ForeignKey("MunicipalAccountId")]
    public MunicipalAccount? MunicipalAccount { get; set; }

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

    // Child allocation lines used by the BudgetPanel details grid.
    // Not mapped yet so this UI enhancement can ship before a schema migration.
    [NotMapped]
    public ObservableCollection<BudgetAllocationLine> AllocationLines { get; set; } = new();

    // Auditing (simplified)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Computed properties for compatibility
    public decimal TotalBudget => BudgetedAmount;
    public decimal ActualSpent => ActualAmount;
    [NotMapped]
    public decimal Remaining => BudgetedAmount - ActualAmount;

    [NotMapped]
    public decimal PercentOfBudget => BudgetedAmount > 0 ? Math.Round((ActualAmount / BudgetedAmount) * 100m, 2) : 0m;

    // Fractional percent for UI bindings (0.0 .. 1.0). Use this for SfDataGrid "P" formats.
    [NotMapped]
    public decimal PercentOfBudgetFraction => BudgetedAmount > 0 ? Math.Round(ActualAmount / BudgetedAmount, 4) : 0m;

    /// <summary>Gets the remaining budget amount (Proposed minus Spent). What the Mayor wants to see.</summary>
    [NotMapped]
    public decimal RemainingAmount => BudgetedAmount - ActualAmount;

    /// <summary>Gets percent remaining as a 0-1 fraction for P2 grid columns. Green = crushing it. Red = call the council.</summary>
    [NotMapped]
    public decimal PercentRemainingFraction => BudgetedAmount > 0 ? Math.Round((BudgetedAmount - ActualAmount) / BudgetedAmount, 4) : 0m;

    // Entity-specific computed helpers for UI presentation (Town of Wiley vs Wiley Sanitation District)
    [NotMapped]
    public string EntityName => Fund?.Name ?? MunicipalAccount?.Name ?? FundType.ToString();

    // Account and Department name helpers for datagrid display
    [NotMapped]
    public string AccountName
    {
        // Show MunicipalAccount name when linked; fall back to editable Description otherwise.
        get => !string.IsNullOrWhiteSpace(MunicipalAccount?.Name) ? MunicipalAccount!.Name : Description;
        // Writes go to Description so the grid column is fully editable.
        set => Description = value;
    }

    [NotMapped]
    public string AccountTypeName => MunicipalAccount?.Type.ToString() ?? "Unknown";

    [NotMapped]
    public string FinancialClassification
    {
        get
        {
            if (TryGetAccountRoot(out var accountRoot))
            {
                if (accountRoot >= 400 && accountRoot <= 499)
                {
                    return "Income";
                }

                if (accountRoot >= 500)
                {
                    return IsAdministrativeGeneralExpense()
                        ? "Administrative & General Expense"
                        : "Operating Expense";
                }
            }

            if (MunicipalAccount != null)
            {
                if (IsIncomeAccountType(MunicipalAccount.Type))
                {
                    return "Income";
                }

                if (IsExpenseAccountType(MunicipalAccount.Type))
                {
                    return IsAdministrativeGeneralExpense()
                        ? "Administrative & General Expense"
                        : "Operating Expense";
                }
            }

            return IsAdministrativeGeneralExpense()
                ? "Administrative & General Expense"
                : "Operating Expense";
        }
    }

    [NotMapped]
    public string DepartmentName => Department?.Name ?? string.Empty;

    [NotMapped]
    public string FundTypeDescription => FundType.ToString();

    [NotMapped]
    public decimal VarianceAmount => Variance;

    [NotMapped]
    public decimal VariancePercentage => BudgetedAmount != 0m
        ? Math.Round(Variance / BudgetedAmount, 4)
        : 0m;

    [NotMapped]
    public decimal TownOfWileyBudgetedAmount => IsTownOfWiley() ? BudgetedAmount : 0m;

    [NotMapped]
    public decimal TownOfWileyActualAmount => IsTownOfWiley() ? ActualAmount : 0m;

    [NotMapped]
    public decimal WsdBudgetedAmount => IsWsd() ? BudgetedAmount : 0m;

    [NotMapped]
    public decimal WsdActualAmount => IsWsd() ? ActualAmount : 0m;

    [NotMapped]
    public decimal AllocationTotalFraction => AllocationLines.Count == 0
        ? (FundId.HasValue ? 1m : 0m)
        : Math.Round(AllocationLines.Sum(line => line.AllocationPercentage), 4);

    [NotMapped]
    public int AllocationSplitCount => AllocationLines.Count == 0 ? 1 : AllocationLines.Count;

    [NotMapped]
    public string AllocationSummary => $"{AllocationSplitCount} split{(AllocationSplitCount == 1 ? string.Empty : "s")}";

    private bool IsAdministrativeGeneralExpense()
    {
        if (!string.IsNullOrWhiteSpace(Department?.Name)
            && Department.Name.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(Department?.DepartmentCode)
            && Department.DepartmentCode.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return Description.IndexOf("administrative", StringComparison.OrdinalIgnoreCase) >= 0
            || Description.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0
            || Description.IndexOf("general", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool TryGetAccountRoot(out int accountRoot)
    {
        accountRoot = 0;

        if (string.IsNullOrWhiteSpace(AccountNumber))
        {
            return false;
        }

        var rootSegment = AccountNumber.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rootSegment))
        {
            return false;
        }

        if (rootSegment.Length > 3)
        {
            rootSegment = rootSegment[..3];
        }

        return int.TryParse(rootSegment, out accountRoot);
    }

    private static bool IsIncomeAccountType(AccountType accountType)
    {
        return accountType == AccountType.Taxes
            || accountType == AccountType.Fees
            || accountType == AccountType.Grants
            || accountType == AccountType.Interest
            || accountType == AccountType.Sales
            || accountType == AccountType.Revenue;
    }

    private static bool IsExpenseAccountType(AccountType accountType)
    {
        return accountType == AccountType.Salaries
            || accountType == AccountType.Supplies
            || accountType == AccountType.Services
            || accountType == AccountType.Utilities
            || accountType == AccountType.Maintenance
            || accountType == AccountType.Insurance
            || accountType == AccountType.Depreciation
            || accountType == AccountType.Expense
            || accountType == AccountType.PermitsAndAssessments
            || accountType == AccountType.ProfessionalServices
            || accountType == AccountType.ContractLabor
            || accountType == AccountType.DuesAndSubscriptions
            || accountType == AccountType.CapitalOutlay
            || accountType == AccountType.Transfers;
    }

    private bool IsWsd()
    {
        return Fund?.Name != null && Fund.Name.IndexOf("sanitation", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsTownOfWiley()
    {
        if (Fund?.Name == null) return false;
        // Consider funds containing 'sanitation' as WSD; treat 'town' or 'wiley' (but not sanitation) as Town of Wiley
        if (Fund.Name.IndexOf("sanitation", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return Fund.Name.IndexOf("town", StringComparison.OrdinalIgnoreCase) >= 0
               || Fund.Name.IndexOf("wiley", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
