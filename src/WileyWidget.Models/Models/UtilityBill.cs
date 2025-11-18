#nullable enable
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a utility bill for a customer
/// </summary>
public class UtilityBill : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [Key]
    public int Id { get; set; }

    [Required]
    public int CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public UtilityCustomer? Customer { get; set; }

    private string _billNumber = string.Empty;

    [Required]
    [StringLength(50)]
    public string BillNumber
    {
        get => _billNumber;
        set
        {
            if (_billNumber != value)
            {
                _billNumber = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime _billDate;

    [Required]
    public DateTime BillDate
    {
        get => _billDate;
        set
        {
            if (_billDate != value)
            {
                _billDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverdue));
            }
        }
    }

    private DateTime _dueDate;

    [Required]
    public DateTime DueDate
    {
        get => _dueDate;
        set
        {
            if (_dueDate != value)
            {
                _dueDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverdue));
                OnPropertyChanged(nameof(DaysUntilDue));
            }
        }
    }

    private DateTime _periodStartDate;

    [Required]
    public DateTime PeriodStartDate
    {
        get => _periodStartDate;
        set
        {
            if (_periodStartDate != value)
            {
                _periodStartDate = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime _periodEndDate;

    [Required]
    public DateTime PeriodEndDate
    {
        get => _periodEndDate;
        set
        {
            if (_periodEndDate != value)
            {
                _periodEndDate = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _waterCharges;

    [Column(TypeName = "decimal(18,2)")]
    public decimal WaterCharges
    {
        get => _waterCharges;
        set
        {
            if (_waterCharges != value)
            {
                _waterCharges = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private decimal _sewerCharges;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SewerCharges
    {
        get => _sewerCharges;
        set
        {
            if (_sewerCharges != value)
            {
                _sewerCharges = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private decimal _garbageCharges;

    [Column(TypeName = "decimal(18,2)")]
    public decimal GarbageCharges
    {
        get => _garbageCharges;
        set
        {
            if (_garbageCharges != value)
            {
                _garbageCharges = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private decimal _stormwaterCharges;

    [Column(TypeName = "decimal(18,2)")]
    public decimal StormwaterCharges
    {
        get => _stormwaterCharges;
        set
        {
            if (_stormwaterCharges != value)
            {
                _stormwaterCharges = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private decimal _lateFees;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LateFees
    {
        get => _lateFees;
        set
        {
            if (_lateFees != value)
            {
                _lateFees = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private decimal _otherCharges;

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherCharges
    {
        get => _otherCharges;
        set
        {
            if (_otherCharges != value)
            {
                _otherCharges = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private decimal _amountPaid;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid
    {
        get => _amountPaid;
        set
        {
            if (_amountPaid != value)
            {
                _amountPaid = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AmountDue));
                OnPropertyChanged(nameof(IsPaid));
            }
        }
    }

    private BillStatus _status = BillStatus.Pending;

    public BillStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusDescription));
                OnPropertyChanged(nameof(IsPaid));
            }
        }
    }

    private DateTime? _paidDate;

    public DateTime? PaidDate
    {
        get => _paidDate;
        set
        {
            if (_paidDate != value)
            {
                _paidDate = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _notes;

    [StringLength(500)]
    public string? Notes
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

    private int _waterUsageGallons;

    public int WaterUsageGallons
    {
        get => _waterUsageGallons;
        set
        {
            if (_waterUsageGallons != value)
            {
                _waterUsageGallons = value;
                OnPropertyChanged();
            }
        }
    }

    private int _previousMeterReading;

    public int PreviousMeterReading
    {
        get => _previousMeterReading;
        set
        {
            if (_previousMeterReading != value)
            {
                _previousMeterReading = value;
                OnPropertyChanged();
            }
        }
    }

    private int _currentMeterReading;

    public int CurrentMeterReading
    {
        get => _currentMeterReading;
        set
        {
            if (_currentMeterReading != value)
            {
                _currentMeterReading = value;
                OnPropertyChanged();
            }
        }
    }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? LastModifiedDate { get; set; }

    /// <summary>
    /// Navigation property for related charges
    /// </summary>
    public ICollection<Charge>? Charges { get; set; }

    /// <summary>
    /// Total amount charged on the bill
    /// </summary>
    [NotMapped]
    public decimal TotalAmount => WaterCharges + SewerCharges + GarbageCharges +
                                  StormwaterCharges + LateFees + OtherCharges;

    /// <summary>
    /// Amount still due on the bill
    /// </summary>
    [NotMapped]
    public decimal AmountDue => TotalAmount - AmountPaid;

    /// <summary>
    /// Whether the bill is fully paid
    /// </summary>
    [NotMapped]
    public bool IsPaid => Status == BillStatus.Paid || AmountDue <= 0;

    /// <summary>
    /// Whether the bill is overdue
    /// </summary>
    [NotMapped]
    public bool IsOverdue => !IsPaid && DueDate < DateTime.Today;

    /// <summary>
    /// Days until due date (negative if overdue)
    /// </summary>
    [NotMapped]
    public int DaysUntilDue => (DueDate - DateTime.Today).Days;

    /// <summary>
    /// Human-readable status description
    /// </summary>
    [NotMapped]
    public string StatusDescription => Status switch
    {
        BillStatus.Pending => "Pending",
        BillStatus.Sent => "Sent",
        BillStatus.PartiallyPaid => "Partially Paid",
        BillStatus.Paid => "Paid",
        BillStatus.Overdue => "Overdue",
        BillStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    /// <summary>
    /// Formatted total amount for display
    /// </summary>
    [NotMapped]
    public string FormattedTotal => TotalAmount.ToString("C2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formatted amount due for display
    /// </summary>
    [NotMapped]
    public string FormattedAmountDue => AmountDue.ToString("C2", CultureInfo.InvariantCulture);
}

/// <summary>
/// Status of a utility bill
/// </summary>
public enum BillStatus
{
    Pending = 0,
    Sent = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}
