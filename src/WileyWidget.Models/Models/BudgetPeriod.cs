#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a budget period for multi-year budget tracking
/// </summary>
public class BudgetPeriod : INotifyPropertyChanged
{
    private int _id;
    private int _year;
    private string _name = string.Empty;
    private DateTime _createdDate = DateTime.UtcNow;
    private BudgetStatus _status = BudgetStatus.Draft;
    private DateTime _startDate;
    private DateTime _endDate;
    private bool _isActive;

    /// <summary>
    /// Unique identifier for the budget period
    /// </summary>
    [Key]
    public int Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Budget year
    /// </summary>
    [Required]
    public int Year
    {
        get => _year;
        set
        {
            if (_year != value)
            {
                _year = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Budget period name (e.g., "2026 Proposed", "2025 Adopted")
    /// </summary>
    [Required(ErrorMessage = "Budget period name is required")]
    [StringLength(100, ErrorMessage = "Budget period name cannot exceed 100 characters")]
    public string Name
    {
        get => _name;
        set
        {
            var newValue = value ?? string.Empty;
            if (_name != newValue)
            {
                _name = newValue;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Date this budget period was created
    /// </summary>
    [Required]
    public DateTime CreatedDate
    {
        get => _createdDate;
        set
        {
            if (_createdDate != value)
            {
                _createdDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Current status of the budget period
    /// </summary>
    [Required]
    public BudgetStatus Status
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
    /// Start date of the budget period
    /// </summary>
    [Required]
    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (_startDate != value)
            {
                _startDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// End date of the budget period
    /// </summary>
    [Required]
    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (_endDate != value)
            {
                _endDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether this budget period is currently active
    /// </summary>
    [Required]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Accounts associated with this budget period
    /// </summary>
    public ICollection<MunicipalAccount> Accounts { get; set; } = new List<MunicipalAccount>();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Budget period status enumeration
/// </summary>
public enum BudgetStatus
{
    Draft,
    Proposed,
    Adopted,
    Executed
}