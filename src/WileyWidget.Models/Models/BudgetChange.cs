using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a budget change record for tracking historical modifications.
/// </summary>
public class BudgetChange : INotifyPropertyChanged
{
    private DateTime _changeDate;
    private string _fundName = string.Empty;
    private string _accountName = string.Empty;
    private decimal _previousAmount;
    private decimal _newAmount;
    private decimal _changeAmount;
    private double _changePercentage;
    private string _changedBy = string.Empty;
    private string _reason = string.Empty;
    private string _changeType = string.Empty;

    /// <summary>
    /// Gets or sets the date when the change was made.
    /// </summary>
    public DateTime ChangeDate
    {
        get => _changeDate;
        set
        {
            if (_changeDate != value)
            {
                _changeDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the fund name associated with the change.
    /// </summary>
    public string FundName
    {
        get => _fundName;
        set
        {
            if (_fundName != value)
            {
                _fundName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the account name associated with the change.
    /// </summary>
    public string AccountName
    {
        get => _accountName;
        set
        {
            if (_accountName != value)
            {
                _accountName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the previous budget amount before the change.
    /// </summary>
    public decimal PreviousAmount
    {
        get => _previousAmount;
        set
        {
            if (_previousAmount != value)
            {
                _previousAmount = value;
                OnPropertyChanged();
                UpdateChangeCalculations();
            }
        }
    }

    /// <summary>
    /// Gets or sets the new budget amount after the change.
    /// </summary>
    public decimal NewAmount
    {
        get => _newAmount;
        set
        {
            if (_newAmount != value)
            {
                _newAmount = value;
                OnPropertyChanged();
                UpdateChangeCalculations();
            }
        }
    }

    /// <summary>
    /// Gets or sets the absolute change amount.
    /// </summary>
    public decimal ChangeAmount
    {
        get => _changeAmount;
        set
        {
            if (_changeAmount != value)
            {
                _changeAmount = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the percentage change.
    /// </summary>
    public double ChangePercentage
    {
        get => _changePercentage;
        set
        {
            if (_changePercentage != value)
            {
                _changePercentage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the user who made the change.
    /// </summary>
    public string ChangedBy
    {
        get => _changedBy;
        set
        {
            if (_changedBy != value)
            {
                _changedBy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the reason for the change.
    /// </summary>
    public string Reason
    {
        get => _reason;
        set
        {
            if (_reason != value)
            {
                _reason = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the type of change (e.g., "Increase", "Decrease", "Transfer").
    /// </summary>
    public string ChangeType
    {
        get => _changeType;
        set
        {
            if (_changeType != value)
            {
                _changeType = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateChangeCalculations()
    {
        ChangeAmount = NewAmount - PreviousAmount;
        if (PreviousAmount != 0)
        {
            ChangePercentage = (double)((NewAmount - PreviousAmount) / PreviousAmount * 100);
        }
        else
        {
            ChangePercentage = NewAmount > 0 ? 100 : 0;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
