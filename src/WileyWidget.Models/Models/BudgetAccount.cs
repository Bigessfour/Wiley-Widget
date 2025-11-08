using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a hierarchical GASB-compliant budget account
/// Supports parent-child relationships for accounts like 410, 410.1, 410.1.1
/// </summary>
public class BudgetAccount : INotifyPropertyChanged
{
    private string _accountNumber = string.Empty;
    private string _description = string.Empty;
    private string _fundType = string.Empty;
    private decimal _budgetAmount;
    private decimal _actualAmount;
    private decimal _variance;
    private double _percentageUsed;
    private bool _isOverBudget;
    private int _parentId = -1;

    /// <summary>
    /// Gets or sets the hierarchical account number (e.g., "410", "410.1", "410.1.1")
    /// </summary>
    public string AccountNumber
    {
        get => _accountNumber;
        set
        {
            if (_accountNumber != value)
            {
                _accountNumber = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the account description
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the fund type code (General, Special Revenue, Capital, etc.)
    /// </summary>
    public string FundType
    {
        get => _fundType;
        set
        {
            if (_fundType != value)
            {
                _fundType = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the budgeted amount (must be positive per GASB rules)
    /// </summary>
    public decimal BudgetAmount
    {
        get => _budgetAmount;
        set
        {
            if (_budgetAmount != value)
            {
                _budgetAmount = value;
                OnPropertyChanged();
                UpdateCalculatedFields();
            }
        }
    }

    /// <summary>
    /// Gets or sets the actual expenses to date
    /// </summary>
    public decimal ActualAmount
    {
        get => _actualAmount;
        set
        {
            if (_actualAmount != value)
            {
                _actualAmount = value;
                OnPropertyChanged();
                UpdateCalculatedFields();
            }
        }
    }

    /// <summary>
    /// Gets the variance (Budget - Actual)
    /// Negative values indicate over-budget
    /// </summary>
    public decimal Variance
    {
        get => _variance;
        private set
        {
            if (_variance != value)
            {
                _variance = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the percentage of budget used
    /// </summary>
    public double PercentageUsed
    {
        get => _percentageUsed;
        private set
        {
            if (Math.Abs(_percentageUsed - value) > 0.0001)
            {
                _percentageUsed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets whether this account is over budget
    /// </summary>
    public bool IsOverBudget
    {
        get => _isOverBudget;
        private set
        {
            if (_isOverBudget != value)
            {
                _isOverBudget = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the parent account ID for hierarchical structure
    /// -1 indicates root level
    /// </summary>
    public int ParentId
    {
        get => _parentId;
        set
        {
            if (_parentId != value)
            {
                _parentId = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the account ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets the collection of child accounts
    /// </summary>
    public ObservableCollection<BudgetAccount> Children { get; } = new();

    /// <summary>
    /// Updates calculated fields (Variance, PercentageUsed, IsOverBudget)
    /// </summary>
    private void UpdateCalculatedFields()
    {
        Variance = BudgetAmount - ActualAmount;
        IsOverBudget = Variance < 0;
        
        if (BudgetAmount > 0)
        {
            PercentageUsed = (double)(ActualAmount / BudgetAmount);
        }
        else
        {
            PercentageUsed = 0;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a fund type for budget categorization
/// </summary>
/// <summary>
/// Represents a GASB fund type for budget classification
/// </summary>
public class BudgetFundType
{
    /// <summary>
    /// Gets or sets the fund type code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fund type name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Static collection of standard GASB fund types
    /// </summary>
    public static ObservableCollection<BudgetFundType> GetStandardFundTypes()
    {
        return new ObservableCollection<BudgetFundType>
        {
            new() { Code = "GF", Name = "General Fund" },
            new() { Code = "SR", Name = "Special Revenue" },
            new() { Code = "DS", Name = "Debt Service" },
            new() { Code = "CP", Name = "Capital Projects" },
            new() { Code = "PF", Name = "Permanent Fund" },
            new() { Code = "EF", Name = "Enterprise Fund" },
            new() { Code = "ISF", Name = "Internal Service Fund" },
            new() { Code = "PT", Name = "Pension Trust" },
            new() { Code = "IT", Name = "Investment Trust" },
            new() { Code = "PBT", Name = "Private-Purpose Trust" },
            new() { Code = "CF", Name = "Custodial Fund" }
        };
    }
}

/// <summary>
/// Data model for budget distribution chart
/// </summary>
public class BudgetDistributionData
{
    public string FundType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Data model for budget comparison chart
/// </summary>
public class BudgetComparisonData
{
    public string Category { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal ActualAmount { get; set; }
}
