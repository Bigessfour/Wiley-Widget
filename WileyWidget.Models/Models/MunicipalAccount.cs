#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace WileyWidget.Models;

/// <summary>
/// Value object representing a hierarchical account number
/// </summary>
[Owned]
public class AccountNumber
{
    /// <summary>
    /// The account number value (e.g., "405.1", "410.2.1", "101-1000-000")
    /// </summary>
    [Required]
    [MaxLength(20)]
    [RegularExpression(@"^\d+([.-]\d+)*$", ErrorMessage = "Account number must be numeric with optional separators (dots or hyphens)")]
    public string Value { get; private set; }

    /// <summary>
    /// The hierarchical level of this account number
    /// </summary>
    [NotMapped]
    public int Level => Value.Split('.').Length;

    /// <summary>
    /// The parent account number (null for root accounts)
    /// </summary>
    [NotMapped]
    public string? ParentNumber => Level > 1 ? string.Join(".", Value.Split('.').Take(Level - 1)) : null;

    /// <summary>
    /// Whether this account number represents a parent account (has child accounts)
    /// </summary>
    [NotMapped]
    public bool IsParent => Value.Contains('.') && Value.Split('.').Length < 3; // Parent accounts typically have 1-2 levels

    /// <summary>
    /// Gets the parent account number string.
    /// </summary>
    /// <returns>The parent account number or null if this is a root account.</returns>
    public string? GetParentNumber() => ParentNumber;

    /// <summary>
    /// Creates a new AccountNumber
    /// </summary>
    public AccountNumber(string value)
    {
        if (!Regex.IsMatch(value, @"^\d+([.-]\d+)*$"))
            throw new ArgumentException("Invalid account number format. Must be numeric with optional separators (dots or hyphens) (e.g., 405, 405.1, 410.2.1, 101-1000-000)");

        Value = value;
    }

    // Required for EF Core
    private AccountNumber()
    {
        Value = string.Empty;
    }

    public override string ToString() => Value;

    public override bool Equals(object? obj) =>
        obj is AccountNumber other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>
/// Represents a municipal accounting account following GASB standards
/// </summary>
public class MunicipalAccount : INotifyPropertyChanged
{
    /// <summary>
    /// Property changed event for data binding
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises PropertyChanged for a specific property
    /// </summary>
    protected void OnPropertyChanged(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Default constructor with sensible defaults
    /// </summary>
    public MunicipalAccount()
    {
        _type = AccountType.Asset;
    }

    /// <summary>
    /// Unique identifier for the municipal account
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Alias for Id for compatibility
    /// </summary>
    public int MunicipalAccountId => Id;

    /// <summary>
    /// Row version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private AccountNumber? _accountNumber;

    /// <summary>
    /// Hierarchical account number following municipal accounting standards (e.g., "405", "405.1", "410.2.1")
    /// </summary>
    [Required(ErrorMessage = "Account number is required")]
    public AccountNumber? AccountNumber
    {
        get => _accountNumber;
        set
        {
            if (!Equals(_accountNumber, value))
            {
                _accountNumber = value;
                OnPropertyChanged(nameof(AccountNumber), nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Computed account number value for queries
    /// </summary>
    public string? AccountNumber_Value { get; set; }

    /// <summary>
    /// Fund class for GASB compliance
    /// </summary>
    public FundClass? FundClass { get; set; }

    /// <summary>
    /// Department this account belongs to
    /// </summary>
    [Required]
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>
    /// Parent account for hierarchical relationships (null for root accounts)
    /// </summary>
    public int? ParentAccountId { get; set; }
    public MunicipalAccount? ParentAccount { get; set; }

    /// <summary>
    /// Child accounts in the hierarchy
    /// </summary>
    public ICollection<MunicipalAccount> ChildAccounts { get; set; } = new List<MunicipalAccount>();

    /// <summary>
    /// Budget entries for multi-year tracking
    /// </summary>
    public ICollection<BudgetEntry> BudgetEntries { get; set; } = new List<BudgetEntry>();

    /// <summary>
    /// Transactions against this account
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Invoices charged to this account
    /// </summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    /// <summary>
    /// Temporary property for multi-year data during import (not persisted)
    /// </summary>
    [NotMapped]
    public MultiYearBudgetData? MultiYearData { get; set; }

    /// <summary>
    /// Budget period this account belongs to
    /// </summary>
    [Required]
    public int BudgetPeriodId { get; set; }
    public BudgetPeriod? BudgetPeriod { get; set; }

    private string _name = string.Empty;

    /// <summary>
    /// Account name/description
    /// </summary>
    [Required(ErrorMessage = "Account name is required")]
    [StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters")]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name), nameof(DisplayName));
            }
        }
    }

    private AccountType _type;

    /// <summary>
    /// Type of account following GASB standards
    /// </summary>
    [Required]
    public AccountType Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged(nameof(Type), nameof(TypeDescription));
            }
        }
    }

    private MunicipalFundType _fund;

    public MunicipalFundType Fund
    {
        get => _fund;
        set
        {
            if (_fund != value)
            {
                _fund = value;
                // Set FundClass based on Fund type
                // FundClass = value switch
                // {
                //     MunicipalFundType.General or MunicipalFundType.SpecialRevenue or MunicipalFundType.CapitalProjects or MunicipalFundType.DebtService => FundClass.Governmental,
                //     MunicipalFundType.Enterprise or MunicipalFundType.InternalService => FundClass.Proprietary,
                //     MunicipalFundType.Trust or MunicipalFundType.Agency => FundClass.Fiduciary,
                //     _ => FundClass.Governmental // Default for additional funds
                // };
                OnPropertyChanged(nameof(Fund), nameof(FundDescription));
            }
        }
    }

    private decimal _balance;

    /// <summary>
    /// Current account balance
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance
    {
        get => _balance;
        set
        {
            if (_balance != value)
            {
                _balance = value;
                OnPropertyChanged(nameof(Balance), nameof(FormattedBalance));
            }
        }
    }

    private decimal _budgetAmount;

    /// <summary>
    /// Budgeted amount for this account
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAmount
    {
        get => _budgetAmount;
        set
        {
            if (_budgetAmount != value)
            {
                _budgetAmount = value;
                OnPropertyChanged(nameof(BudgetAmount), nameof(Variance), nameof(VariancePercent));
            }
        }
    }

    private bool _isActive = true;

    /// <summary>
    /// Whether the account is active
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    /// <summary>
    /// QuickBooks account ID for synchronization
    /// </summary>
    [StringLength(50)]
    public string? QuickBooksId { get; set; }

    /// <summary>
    /// Last synchronized date with QuickBooks
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    /// <summary>
    /// Additional notes about the account
    /// </summary>
    [StringLength(200)]
    public string? Notes { get; set; }

    /// <summary>
    /// Calculated property: Variance between budget and actual
    /// </summary>
    [NotMapped]
    public decimal Variance => BudgetAmount - Balance;

    /// <summary>
    /// Calculated property: Variance percentage
    /// </summary>
    [NotMapped]
    public decimal VariancePercent => BudgetAmount != 0 ? (Variance / BudgetAmount) * 100 : 0;

    /// <summary>
    /// Formatted balance string
    /// </summary>
    [NotMapped]
    public string FormattedBalance => Balance >= 0
        ? $"${Balance:N2}"
        : $"(${Math.Abs(Balance):N2})";

    /// <summary>
    /// Display name combining account number and name
    /// </summary>
    [NotMapped]
    public string DisplayName => $"{AccountNumber?.ToString() ?? ""} - {Name}";

    /// <summary>
    /// Human-readable account type description
    /// </summary>
    [NotMapped]
    public string TypeDescription => Type switch
    {
        AccountType.Cash => "Cash",
        AccountType.Investments => "Investments",
        AccountType.Receivables => "Receivables",
        AccountType.Inventory => "Inventory",
        AccountType.FixedAssets => "Fixed Assets",
        AccountType.Asset => "Asset",
        AccountType.Payables => "Liability",
        AccountType.Debt => "Debt",
        AccountType.AccruedLiabilities => "Accrued Liabilities",
        AccountType.RetainedEarnings => "Equity",
        AccountType.FundBalance => "Fund Balance",
        AccountType.Taxes => "Taxes",
        AccountType.Fees => "Fees",
        AccountType.Grants => "Grants",
        AccountType.Interest => "Interest",
        AccountType.Sales => "Sales",
        AccountType.Revenue => "Revenue",
        AccountType.Salaries => "Salaries",
        AccountType.Supplies => "Supplies",
        AccountType.Services => "Services",
        AccountType.Utilities => "Utilities",
        AccountType.Maintenance => "Maintenance",
        AccountType.Insurance => "Insurance",
        AccountType.Depreciation => "Depreciation",
        AccountType.Expense => "Expense",
        AccountType.PermitsAndAssessments => "Permits and Assessments",
        AccountType.ProfessionalServices => "Professional Services",
        AccountType.ContractLabor => "Contract Labor",
        AccountType.DuesAndSubscriptions => "Dues and Subscriptions",
        AccountType.CapitalOutlay => "Capital Outlay",
        AccountType.Transfers => "Transfers",
        _ => "Unknown"
    };

    /// <summary>
    /// Human-readable fund type description
    /// </summary>
    [NotMapped]
    public string FundDescription => Fund switch
    {
        MunicipalFundType.General => "General Fund",
        MunicipalFundType.SpecialRevenue => "Special Revenue Fund",
        MunicipalFundType.CapitalProjects => "Capital Projects Fund",
        MunicipalFundType.DebtService => "Debt Service Fund",
        MunicipalFundType.Enterprise => "Enterprise Fund",
        MunicipalFundType.InternalService => "Internal Service Fund",
        MunicipalFundType.Trust => "Trust Fund",
        MunicipalFundType.Agency => "Agency Fund",
        MunicipalFundType.ConservationTrust => "Conservation Trust Fund",
        MunicipalFundType.Recreation => "Recreation Fund",
        MunicipalFundType.Utility => "Utility Fund",
        MunicipalFundType.Water => "Water Fund",
        MunicipalFundType.Sewer => "Sewer Fund",
        MunicipalFundType.Trash => "Trash Fund",
        _ => "Unknown"
    };
}

/// <summary>
/// Account types following GASB standards
/// </summary>
public enum AccountType
{
    // Asset Types
    Cash,
    Investments,
    Receivables,
    Inventory,
    FixedAssets,
    Asset,

    // Liability Types
    Payables,
    Debt,
    AccruedLiabilities,

    // Equity Types
    RetainedEarnings,
    FundBalance,

    // Revenue Types
    Taxes,
    Fees,
    Grants,
    Interest,
    Sales,
    Revenue,

    // Expense Types
    Salaries,
    Supplies,
    Services,
    Utilities,
    Maintenance,
    Insurance,
    Depreciation,
    Expense,

    // Municipal-Specific Types
    PermitsAndAssessments,
    ProfessionalServices,
    ContractLabor,
    DuesAndSubscriptions,
    CapitalOutlay,
    Transfers,
    Unknown
}

/// <summary>
/// Fund classes following GASB standards
/// </summary>
public enum FundClass
{
    Governmental,
    Proprietary,
    Fiduciary,
    Memo
}

/// <summary>
/// Fund types for governmental fund accounting
/// </summary>
public enum MunicipalFundType
{
    // Governmental Funds
    General,
    SpecialRevenue,
    CapitalProjects,
    DebtService,

    // Proprietary Funds
    Enterprise,
    InternalService,

    // Fiduciary Funds
    Trust,
    Agency,

    // Additional Municipal Funds
    ConservationTrust,
    Recreation,
    Utility,
    Water,
    Sewer,
    Trash
}

/// <summary>
/// Multi-year budget data structure for import processing
/// </summary>
public class MultiYearBudgetData
{
    public decimal? PriorYear { get; set; }
    public decimal? SevenMonth { get; set; }
    public decimal? Estimate { get; set; }
    public decimal? Budget { get; set; }
}