#nullable enable
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Global settings for fiscal year configuration
/// Used to categorize financial data into current, past, and future periods
/// </summary>
public class FiscalYearSettings : INotifyPropertyChanged
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
    /// Unique identifier (singleton pattern - only one record should exist)
    /// </summary>
    [Key]
    public int Id { get; set; } = 1;

    private int _fiscalYearStartMonth = 7; // Default to July (common fiscal year start)

    /// <summary>
    /// Month when fiscal year begins (1-12, where 1 = January)
    /// </summary>
    [Required]
    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
    public int FiscalYearStartMonth
    {
        get => _fiscalYearStartMonth;
        set
        {
            if (_fiscalYearStartMonth != value)
            {
                _fiscalYearStartMonth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FiscalYearStartDate));
            }
        }
    }

    private int _fiscalYearStartDay = 1; // Default to 1st day of month

    /// <summary>
    /// Day of month when fiscal year begins (1-31)
    /// </summary>
    [Required]
    [Range(1, 31, ErrorMessage = "Day must be between 1 and 31")]
    public int FiscalYearStartDay
    {
        get => _fiscalYearStartDay;
        set
        {
            if (_fiscalYearStartDay != value)
            {
                _fiscalYearStartDay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FiscalYearStartDate));
            }
        }
    }

    /// <summary>
    /// Row version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Last time settings were modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Computed property: Fiscal year start date for current year
    /// </summary>
    [NotMapped]
    public DateTime FiscalYearStartDate
    {
        get
        {
            try
            {
                var currentYear = DateTime.Now.Year;
                return new DateTime(currentYear, FiscalYearStartMonth, FiscalYearStartDay);
            }
            catch
            {
                // Fallback for invalid date combinations (e.g., Feb 31)
                return new DateTime(DateTime.Now.Year, FiscalYearStartMonth, 1);
            }
        }
    }

    /// <summary>
    /// Get the current fiscal year start date relative to a specific date
    /// </summary>
    public DateTime GetCurrentFiscalYearStart(DateTime referenceDate)
    {
        try
        {
            var fiscalStart = new DateTime(referenceDate.Year, FiscalYearStartMonth, FiscalYearStartDay);
            
            // If the reference date is before this year's fiscal start, use last year's fiscal start
            if (referenceDate < fiscalStart)
            {
                fiscalStart = new DateTime(referenceDate.Year - 1, FiscalYearStartMonth, FiscalYearStartDay);
            }
            
            return fiscalStart;
        }
        catch
        {
            return new DateTime(referenceDate.Year, FiscalYearStartMonth, 1);
        }
    }

    /// <summary>
    /// Get the current fiscal year end date relative to a specific date
    /// </summary>
    public DateTime GetCurrentFiscalYearEnd(DateTime referenceDate)
    {
        var fiscalStart = GetCurrentFiscalYearStart(referenceDate);
        return fiscalStart.AddYears(1).AddDays(-1);
    }

    /// <summary>
    /// Determine if a date falls within the current fiscal year
    /// </summary>
    public bool IsCurrentFiscalYear(DateTime date)
    {
        var fiscalStart = GetCurrentFiscalYearStart(DateTime.Now);
        var fiscalEnd = GetCurrentFiscalYearEnd(DateTime.Now);
        return date >= fiscalStart && date <= fiscalEnd;
    }

    /// <summary>
    /// Determine if a date is in a past fiscal year
    /// </summary>
    public bool IsPastFiscalYear(DateTime date)
    {
        var fiscalStart = GetCurrentFiscalYearStart(DateTime.Now);
        return date < fiscalStart;
    }

    /// <summary>
    /// Determine if a date is in a future fiscal year
    /// </summary>
    public bool IsFutureFiscalYear(DateTime date)
    {
        var fiscalEnd = GetCurrentFiscalYearEnd(DateTime.Now);
        return date > fiscalEnd;
    }

    /// <summary>
    /// Get fiscal year period classification
    /// </summary>
    public FiscalPeriod GetFiscalPeriod(DateTime date)
    {
        if (IsCurrentFiscalYear(date)) return FiscalPeriod.Current;
        if (IsPastFiscalYear(date)) return FiscalPeriod.Past;
        return FiscalPeriod.Future;
    }
}

/// <summary>
/// Fiscal period classification for data integrity
/// </summary>
public enum FiscalPeriod
{
    Past,
    Current,
    Future
}
