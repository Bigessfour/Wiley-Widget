using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a data point for budget trend visualization.
/// Used in DashboardViewModel for displaying budget trends over time.
/// </summary>
public class BudgetTrendItem : INotifyPropertyChanged
{
    private string _period = string.Empty;
    private decimal _amount;
    private decimal _projectedAmount;
    private string _category = string.Empty;
    private DateTime _date;

    /// <summary>
    /// Gets or sets the period label (e.g., "Q1 2025", "Jan 2025", "2025").
    /// </summary>
    public string Period
    {
        get => _period;
        set
        {
            if (_period != value)
            {
                _period = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the actual budget amount for this period.
    /// </summary>
    public decimal Amount
    {
        get => _amount;
        set
        {
            if (_amount != value)
            {
                _amount = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the projected budget amount for this period.
    /// </summary>
    public decimal ProjectedAmount
    {
        get => _projectedAmount;
        set
        {
            if (_projectedAmount != value)
            {
                _projectedAmount = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _actualAmount;
    private decimal _variance;
    private decimal _change;
    private string _trendDirection = "Flat";

    /// <summary>
    /// Sum of actual amounts for this period (if available)
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
            }
        }
    }

    /// <summary>
    /// Variance between budgeted and actual for this period
    /// </summary>
    public decimal Variance
    {
        get => _variance;
        set
        {
            if (_variance != value)
            {
                _variance = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Change from the previous period (budgeted amount)
    /// </summary>
    public decimal Change
    {
        get => _change;
        set
        {
            if (_change != value)
            {
                _change = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Trend direction ("Up", "Down", "Flat")
    /// </summary>
    public string TrendDirection
    {
        get => _trendDirection;
        set
        {
            if (_trendDirection != value)
            {
                _trendDirection = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the budget category (e.g., "Revenue", "Expenses", "Capital").
    /// </summary>
    public string Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the date for this budget period.
    /// </summary>
    public DateTime Date
    {
        get => _date;
        set
        {
            if (_date != value)
            {
                _date = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
