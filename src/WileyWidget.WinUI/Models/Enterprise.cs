#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WileyWidget.Models;
// using WileyWidget.Attributes; (same namespace now)
// using WileyWidget.Data; (interfaces moved to Models)

namespace WileyWidget.WinUI.Models;

public enum EnterpriseStatus
{
    Active,
    Inactive,
    Suspended
}

/// <summary>
/// Represents a municipal enterprise (Water, Sewer, Trash, Apartments)
/// Implements audit tracking and soft delete for compliance
/// </summary>
public class Enterprise : INotifyPropertyChanged, ISoftDeletable
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
    /// Default constructor
    /// </summary>
    public Enterprise()
    {
        // Ensure string properties are initialized to empty strings
        _name = string.Empty;
        _type = string.Empty;
        _notes = string.Empty;
    }

    /// <summary>
    /// Unique identifier for the enterprise
    /// </summary>
    [Key]
    [GridDisplay(99, 80, Visible = true)] // Put ID at the end
    public int Id { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private string _name = string.Empty;

    /// <summary>
    /// Name of the enterprise (Water, Sewer, Trash, Apartments)
    /// </summary>
    [Required(ErrorMessage = "Enterprise name is required")]
    [StringLength(100, ErrorMessage = "Enterprise name cannot exceed 100 characters")]
    [GridDisplay(1, 150)]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    private string? _description;

    /// <summary>
    /// Description of the enterprise
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description
    {
        get => _description;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (!string.Equals(_description, sanitized, StringComparison.Ordinal))
            {
                _description = sanitized;
                OnPropertyChanged(nameof(Description));
            }
        }
    }

    private decimal _currentRate;

    /// <summary>
    /// Current rate charged per citizen (e.g., $5.00 per month for water)
    /// </summary>
    [Required(ErrorMessage = "Current rate is required")]
    [Range(0.01, 9999.99, ErrorMessage = "Rate must be between 0.01 and 9999.99")]
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(3, 100, DecimalDigits = 2)]
    public decimal CurrentRate
    {
        get => _currentRate;
        set
        {
            if (_currentRate != value)
            {
                _currentRate = value;
                OnPropertyChanged(nameof(CurrentRate), nameof(MonthlyRevenue), nameof(MonthlyBalance), nameof(BreakEvenRate));
            }
        }
    }

    private decimal _monthlyExpenses;

    /// <summary>
    /// Monthly expenses (sum of employee compensation + maintenance + other operational costs)
    /// </summary>
    [Required(ErrorMessage = "Monthly expenses are required")]
    [Range(0, double.MaxValue, ErrorMessage = "Monthly expenses cannot be negative")]
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(5, 120, DecimalDigits = 2)]
    public decimal MonthlyExpenses
    {
        get => _monthlyExpenses;
        set
        {
            if (_monthlyExpenses != value)
            {
                _monthlyExpenses = value;
                OnPropertyChanged(nameof(MonthlyExpenses), nameof(MonthlyBalance));
            }
        }
    }

    /// <summary>
    /// Monthly revenue (calculated as CitizenCount * CurrentRate)
    /// </summary>
    [NotMapped]
    [GridDisplay(6, 120, DecimalDigits = 2)]
    public decimal MonthlyRevenue => CitizenCount * CurrentRate;

    private int _citizenCount;

    /// <summary>
    /// Number of citizens served by this enterprise
    /// </summary>
    [Required(ErrorMessage = "Citizen count is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Citizen count must be at least 1")]
    [GridDisplay(4, 80, DecimalDigits = 0)]
    public int CitizenCount
    {
        get => _citizenCount;
        set
        {
            if (_citizenCount != value)
            {
                _citizenCount = value;
                OnPropertyChanged(nameof(CitizenCount), nameof(MonthlyRevenue), nameof(MonthlyBalance), nameof(BreakEvenRate));
            }
        }
    }

    private decimal _totalBudget;

    /// <summary>
    /// Total budget allocated for this enterprise
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(8, 120, DecimalDigits = 2)]
    public decimal TotalBudget
    {
        get => _totalBudget;
        set
        {
            if (_totalBudget != value)
            {
                _totalBudget = value;
                OnPropertyChanged(nameof(TotalBudget));
            }
        }
    }

    private decimal _budgetAmount;

    /// <summary>
    /// Budget amount for this enterprise
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
                OnPropertyChanged(nameof(BudgetAmount));
            }
        }
    }

    /// <summary>
    /// Last modified date for this enterprise
    /// </summary>
    public DateTime? LastModified { get; set; }

    private string? _type = string.Empty;

    /// <summary>
    /// Type of enterprise (Water, Sewer, etc.)
    /// </summary>
    [StringLength(50, ErrorMessage = "Type cannot exceed 50 characters")]
    public string? Type
    {
        get => _type;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(_type, sanitized, StringComparison.Ordinal))
            {
                _type = sanitized;
                OnPropertyChanged(nameof(Type));
            }
        }
    }

    private string? _notes = string.Empty;

    /// <summary>
    /// Additional notes about the enterprise
    /// </summary>
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    [GridDisplay(9, 200)]
    public string? Notes
    {
        get => _notes;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(_notes, sanitized, StringComparison.Ordinal))
            {
                _notes = sanitized;
                OnPropertyChanged(nameof(Notes));
            }
        }
    }

    private EnterpriseStatus _status = EnterpriseStatus.Active;

    /// <summary>
    /// Operational status of the enterprise for grouping and filtering
    /// </summary>
    [GridDisplay(7, 100)]
    public EnterpriseStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    /// <summary>
    /// Convenience: Last updated timestamp (for UI binding)
    /// </summary>
    [NotMapped]
    public DateTime LastUpdated => DateTime.Now;

    /// <summary>
    /// Navigation property for budget interactions
    /// </summary>
    public virtual ICollection<BudgetInteraction> BudgetInteractions { get; set; } = new List<BudgetInteraction>();

    /// <summary>
    /// Calculated property: Monthly deficit/surplus (Revenue - Expenses)
    /// </summary>
    [NotMapped]
    [GridDisplay(7, 120, DecimalDigits = 2)]
    public decimal MonthlyBalance => MonthlyRevenue - MonthlyExpenses;

    /// <summary>
    /// Calculated property: Break-even rate needed to cover expenses
    /// </summary>
    [NotMapped]
    public decimal BreakEvenRate => CitizenCount > 0 ? MonthlyExpenses / CitizenCount : 0;

    /// <summary>
    /// Selection state for bulk operations (not persisted)
    /// </summary>
    [NotMapped]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
    private bool _isSelected;

    // ========== Meter Reading Fields (Water Enterprise Only) ==========

    private decimal? _meterReading;

    /// <summary>
    /// Current meter reading value (Water enterprise only)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(10, 100, DecimalDigits = 2)]
    public decimal? MeterReading
    {
        get => _meterReading;
        set
        {
            if (_meterReading != value)
            {
                _meterReading = value;
                OnPropertyChanged(nameof(MeterReading), nameof(WaterConsumption));
            }
        }
    }

    private DateTime? _meterReadDate;

    /// <summary>
    /// Date and time the meter was read (Water enterprise only)
    /// </summary>
    [GridDisplay(11, 120)]
    public DateTime? MeterReadDate
    {
        get => _meterReadDate;
        set
        {
            if (_meterReadDate != value)
            {
                _meterReadDate = value;
                OnPropertyChanged(nameof(MeterReadDate));
            }
        }
    }

    private decimal? _previousMeterReading;

    /// <summary>
    /// Previous meter reading value for consumption calculation (Water enterprise only)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    [GridDisplay(12, 100, DecimalDigits = 2)]
    public decimal? PreviousMeterReading
    {
        get => _previousMeterReading;
        set
        {
            if (_previousMeterReading != value)
            {
                _previousMeterReading = value;
                OnPropertyChanged(nameof(PreviousMeterReading), nameof(WaterConsumption));
            }
        }
    }

    private DateTime? _previousMeterReadDate;

    /// <summary>
    /// Date of the previous meter reading (Water enterprise only)
    /// </summary>
    [GridDisplay(13, 120)]
    public DateTime? PreviousMeterReadDate
    {
        get => _previousMeterReadDate;
        set
        {
            if (_previousMeterReadDate != value)
            {
                _previousMeterReadDate = value;
                OnPropertyChanged(nameof(PreviousMeterReadDate));
            }
        }
    }

    /// <summary>
    /// Calculated water consumption (Current - Previous meter reading)
    /// </summary>
    [NotMapped]
    [GridDisplay(14, 100, DecimalDigits = 2)]
    public decimal? WaterConsumption =>
        MeterReading.HasValue && PreviousMeterReading.HasValue
            ? MeterReading.Value - PreviousMeterReading.Value
            : null;

    #region IAuditable Implementation

    /// <summary>
    /// Date and time when the enterprise was created (UTC)
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the enterprise was last modified (UTC)
    /// </summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    /// User who created the enterprise
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User who last modified the enterprise
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Date and time when the enterprise was created (UTC) - IAuditable implementation
    /// </summary>
    public DateTime CreatedAt { get => CreatedDate; set => CreatedDate = value; }

    /// <summary>
    /// Date and time when the enterprise was last modified (UTC) - IAuditable implementation
    /// </summary>
    public DateTime? UpdatedAt { get => ModifiedDate; set => ModifiedDate = value; }

    #endregion

    #region ISoftDeletable Implementation

    /// <summary>
    /// Whether the enterprise has been soft-deleted (retained for audit/compliance)
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Date and time when the enterprise was soft-deleted (UTC)
    /// </summary>
    public DateTime? DeletedDate { get; set; }

    /// <summary>
    /// User who soft-deleted the enterprise
    /// </summary>
    public string? DeletedBy { get; set; }

    #endregion

    #region Domain Behavior Methods

    /// <summary>
    /// Determines if the enterprise is operating profitably
    /// </summary>
    public bool IsProfitable() => MonthlyBalance > 0;

    /// <summary>
    /// Calculates the rate adjustment needed to reach a target balance
    /// </summary>
    /// <param name="targetBalance">Desired monthly balance</param>
    /// <returns>Required rate adjustment (positive = increase, negative = decrease)</returns>
    public decimal CalculateRateAdjustmentForTarget(decimal targetBalance)
    {
        if (CitizenCount == 0)
            return 0;

        var targetRevenue = MonthlyExpenses + targetBalance;
        var targetRate = targetRevenue / CitizenCount;
        return targetRate - CurrentRate;
    }

    /// <summary>
    /// Validates if a rate change would result in a reasonable outcome
    /// </summary>
    /// <param name="proposedRate">The rate to validate</param>
    /// <param name="errorMessage">Error message if invalid</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool ValidateRateChange(decimal proposedRate, out string? errorMessage)
    {
        if (proposedRate < 0)
        {
            errorMessage = "Rate cannot be negative";
            return false;
        }

        if (proposedRate > 9999.99m)
        {
            errorMessage = "Rate exceeds maximum allowed value";
            return false;
        }

        // Warn if rate increase is > 50% (unusual for municipal services)
        var percentIncrease = CurrentRate > 0 ? ((proposedRate - CurrentRate) / CurrentRate) * 100 : 0;
        if (percentIncrease > 50)
        {
            errorMessage = $"Warning: Rate increase of {percentIncrease:F1}% exceeds typical adjustment range";
            return true; // Return true but with warning
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Calculates the annual revenue projection
    /// </summary>
    public decimal ProjectAnnualRevenue() => MonthlyRevenue * 12;

    /// <summary>
    /// Calculates the annual expense projection
    /// </summary>
    public decimal ProjectAnnualExpenses() => MonthlyExpenses * 12;

    /// <summary>
    /// Calculates variance from break-even (positive = profitable, negative = loss)
    /// </summary>
    public decimal CalculateBreakEvenVariance() => CurrentRate - BreakEvenRate;

    /// <summary>
    /// Determines the recommended rate adjustment based on current financial health
    /// </summary>
    public string GetRateRecommendation()
    {
        var variance = CalculateBreakEvenVariance();

        if (variance >= 1.0m)
            return $"Current rate is ${variance:F2} above break-even. Consider rate reduction or reserve building.";
        else if (variance >= 0)
            return "Current rate is at or near break-even. Monitor closely.";
        else if (variance >= -1.0m)
            return $"Current rate is ${Math.Abs(variance):F2} below break-even. Minor rate increase recommended.";
        else
            return $"Current rate is ${Math.Abs(variance):F2} below break-even. Immediate rate adjustment required.";
    }

    /// <summary>
    /// Updates meter readings and validates water consumption
    /// </summary>
    public bool UpdateMeterReading(decimal newReading, DateTime readDate, out string? errorMessage)
    {
        if (newReading < 0)
        {
            errorMessage = "Meter reading cannot be negative";
            return false;
        }

        if (MeterReading.HasValue && newReading < MeterReading.Value)
        {
            errorMessage = "New meter reading cannot be less than previous reading (meter rollover not supported)";
            return false;
        }

        if (readDate < MeterReadDate)
        {
            errorMessage = "New read date cannot be before previous read date";
            return false;
        }

        // Update readings
        PreviousMeterReading = MeterReading;
        PreviousMeterReadDate = MeterReadDate;
        MeterReading = newReading;
        MeterReadDate = readDate;

        errorMessage = null;
        return true;
    }

    #endregion
}