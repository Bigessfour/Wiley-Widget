#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents the overall municipal budget summary
/// </summary>
public class OverallBudget : INotifyPropertyChanged, IValidatableObject
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
        if (propertyNames == null) throw new ArgumentNullException(nameof(propertyNames));

        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }
    /// <summary>
    /// Unique identifier for the budget snapshot
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Date this budget snapshot was created
    /// </summary>
    [Required(ErrorMessage = "Snapshot date is required")]
    public DateTime SnapshotDate { get; set; } = DateTime.MinValue;

    private decimal _totalMonthlyRevenue;
    private decimal _totalMonthlyExpenses;

    /// <summary>
    /// Total monthly revenue from all enterprises
    /// </summary>
    [Required(ErrorMessage = "Total monthly revenue is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Total monthly revenue must be greater than zero")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalMonthlyRevenue
    {
        get => _totalMonthlyRevenue;
        set
        {
            if (_totalMonthlyRevenue != value)
            {
                _totalMonthlyRevenue = value;
                OnPropertyChanged(nameof(TotalMonthlyRevenue), nameof(DeficitPercentage));
            }
        }
    }

    /// <summary>
    /// Total monthly expenses from all enterprises
    /// </summary>
    [Required(ErrorMessage = "Total monthly expenses is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Total monthly expenses must be greater than zero")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalMonthlyExpenses
    {
        get => _totalMonthlyExpenses;
        set
        {
            if (_totalMonthlyExpenses != value)
            {
                _totalMonthlyExpenses = value;
                OnPropertyChanged(nameof(TotalMonthlyExpenses), nameof(DeficitPercentage));
            }
        }
    }

    private decimal _totalMonthlyBalance;

    /// <summary>
    /// Total monthly surplus/deficit
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalMonthlyBalance
    {
        get => _totalMonthlyBalance;
        set
        {
            if (_totalMonthlyBalance != value)
            {
                _totalMonthlyBalance = value;
                OnPropertyChanged(nameof(TotalMonthlyBalance), nameof(IsSurplus), nameof(DeficitPercentage));
            }
        }
    }

    private int _totalCitizensServed;

    /// <summary>
    /// Total number of citizens served
    /// </summary>
    [Required(ErrorMessage = "Total citizens served is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Total citizens served must be greater than zero")]
    public int TotalCitizensServed
    {
        get => _totalCitizensServed;
        set
        {
            if (_totalCitizensServed != value)
            {
                _totalCitizensServed = value;
                OnPropertyChanged(nameof(TotalCitizensServed));
            }
        }
    }

    private decimal _averageRatePerCitizen;

    /// <summary>
    /// Average rate per citizen across all enterprises
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageRatePerCitizen
    {
        get => _averageRatePerCitizen;
        set
        {
            if (_averageRatePerCitizen != value)
            {
                _averageRatePerCitizen = value;
                OnPropertyChanged(nameof(AverageRatePerCitizen));
            }
        }
    }

    private string? _notes = string.Empty;

    /// <summary>
    /// Notes about this budget snapshot
    /// </summary>
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes
    {
        get => _notes;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (!string.Equals(_notes, sanitized, StringComparison.Ordinal))
            {
                _notes = sanitized;
                OnPropertyChanged(nameof(Notes));
            }
        }
    }

    /// <summary>
    /// Whether this is the current active budget snapshot
    /// </summary>
    [Required(ErrorMessage = "IsCurrent flag is required")]
    public bool IsCurrent { get; set; } = false;

    /// <summary>
    /// Custom validation for the OverallBudget model
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SnapshotDate == DateTime.MinValue)
        {
            yield return new ValidationResult("Snapshot date must be set to a valid date",
                new[] { nameof(SnapshotDate) });
        }
    }

    /// <summary>
    /// Calculated property: Whether the municipality is running a surplus
    /// </summary>
    [NotMapped]
    public bool IsSurplus => TotalMonthlyBalance > 0;

    /// <summary>
    /// Calculated property: Deficit percentage (negative if surplus)
    /// </summary>
    [NotMapped]
    public decimal DeficitPercentage => TotalMonthlyRevenue > 0 ?
        ((TotalMonthlyBalance / TotalMonthlyRevenue) * 100) : 0;
}
