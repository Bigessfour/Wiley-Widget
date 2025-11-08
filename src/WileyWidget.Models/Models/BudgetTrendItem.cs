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
