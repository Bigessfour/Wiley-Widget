using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a budget insight for dashboard display.
/// Provides analytical information about budget trends and patterns.
/// </summary>
public class BudgetInsight : INotifyPropertyChanged
{
    private string _category = string.Empty;
    private string _insight = string.Empty;
    private decimal _amount;
    private double _percentageChange;
    private string _trend = string.Empty;
    private DateTime _periodStart;
    private DateTime _periodEnd;
    private string _severity = "Info";

    /// <summary>
    /// Gets or sets the budget category (e.g., "Operating Expenses", "Capital Projects").
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
    /// Gets or sets the insight description.
    /// </summary>
    public string Insight
    {
        get => _insight;
        set
        {
            if (_insight != value)
            {
                _insight = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the amount associated with this insight.
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
    /// Gets or sets the percentage change (positive for increase, negative for decrease).
    /// </summary>
    public double PercentageChange
    {
        get => _percentageChange;
        set
        {
            if (_percentageChange != value)
            {
                _percentageChange = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the trend indicator (e.g., "Up", "Down", "Stable").
    /// </summary>
    public string Trend
    {
        get => _trend;
        set
        {
            if (_trend != value)
            {
                _trend = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the start date of the analysis period.
    /// </summary>
    public DateTime PeriodStart
    {
        get => _periodStart;
        set
        {
            if (_periodStart != value)
            {
                _periodStart = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the end date of the analysis period.
    /// </summary>
    public DateTime PeriodEnd
    {
        get => _periodEnd;
        set
        {
            if (_periodEnd != value)
            {
                _periodEnd = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the severity level (Info, Warning, Critical).
    /// </summary>
    public string Severity
    {
        get => _severity;
        set
        {
            if (_severity != value)
            {
                _severity = value;
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
