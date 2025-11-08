using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a detailed budget item for display in budget analysis views.
/// Used in BudgetViewModel for detailed budget tracking and comparison.
/// </summary>
public class BudgetDetailItem : INotifyPropertyChanged
{
    private string _enterpriseName = string.Empty;
    private decimal _budgetAmount;
    private decimal _actualAmount;
    private decimal _variance;
    private double _rateIncrease;
    private string _status = string.Empty;
    private DateTime _lastUpdated;
    private string _notes = string.Empty;

    /// <summary>
    /// Gets or sets the name of the enterprise.
    /// </summary>
    public string EnterpriseName
    {
        get => _enterpriseName;
        set
        {
            if (_enterpriseName != value)
            {
                _enterpriseName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the budgeted amount.
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
                UpdateVariance();
            }
        }
    }

    /// <summary>
    /// Gets or sets the actual amount spent or collected.
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
                UpdateVariance();
            }
        }
    }

    /// <summary>
    /// Gets or sets the variance between budget and actual (calculated).
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
    /// Gets or sets the rate increase percentage.
    /// </summary>
    public double RateIncrease
    {
        get => _rateIncrease;
        set
        {
            if (Math.Abs(_rateIncrease - value) > 0.001)
            {
                _rateIncrease = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the status of the budget item (e.g., "On Track", "Over Budget", "Under Budget").
    /// </summary>
    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    public DateTime LastUpdated
    {
        get => _lastUpdated;
        set
        {
            if (_lastUpdated != value)
            {
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets optional notes about this budget item.
    /// </summary>
    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Updates the variance based on current budget and actual amounts.
    /// </summary>
    private void UpdateVariance()
    {
        Variance = ActualAmount - BudgetAmount;
        
        // Update status based on variance
        if (Variance > 0)
            Status = "Over Budget";
        else if (Variance < 0)
            Status = "Under Budget";
        else
            Status = "On Track";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
