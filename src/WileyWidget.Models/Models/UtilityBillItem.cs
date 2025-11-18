#nullable enable
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents an individual line item on a utility bill
/// Supports modular billing with different charge types
/// </summary>
public class UtilityBillItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [Key]
    public int Id { get; set; }

    [Required]
    public int BillId { get; set; }

    [ForeignKey(nameof(BillId))]
    public UtilityBill? Bill { get; set; }

    private BillItemType _itemType;

    [Required]
    public BillItemType ItemType
    {
        get => _itemType;
        set
        {
            if (_itemType != value)
            {
                _itemType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ItemTypeDescription));
            }
        }
    }

    private string _description = string.Empty;

    [Required]
    [StringLength(200)]
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _quantity;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private decimal _unitPrice;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (_unitPrice != value)
            {
                _unitPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    private string? _unitOfMeasure;

    [StringLength(20)]
    public string? UnitOfMeasure
    {
        get => _unitOfMeasure;
        set
        {
            if (_unitOfMeasure != value)
            {
                _unitOfMeasure = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isTaxable;

    public bool IsTaxable
    {
        get => _isTaxable;
        set
        {
            if (_isTaxable != value)
            {
                _isTaxable = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _taxRate;

    [Column(TypeName = "decimal(5,2)")]
    public decimal TaxRate
    {
        get => _taxRate;
        set
        {
            if (_taxRate != value)
            {
                _taxRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TaxAmount));
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

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Line item total amount (quantity × unit price)
    /// </summary>
    [NotMapped]
    public decimal TotalAmount => Quantity * UnitPrice;

    /// <summary>
    /// Tax amount if taxable
    /// </summary>
    [NotMapped]
    public decimal TaxAmount => IsTaxable ? TotalAmount * (TaxRate / 100) : 0;

    /// <summary>
    /// Total including tax
    /// </summary>
    [NotMapped]
    public decimal TotalWithTax => TotalAmount + TaxAmount;

    /// <summary>
    /// Human-readable item type description
    /// </summary>
    [NotMapped]
    public string ItemTypeDescription => ItemType switch
    {
        BillItemType.WaterUsage => "Water Usage",
        BillItemType.WaterBaseFee => "Water Base Fee",
        BillItemType.SewerUsage => "Sewer Usage",
        BillItemType.SewerBaseFee => "Sewer Base Fee",
        BillItemType.GarbageService => "Garbage Service",
        BillItemType.RecyclingService => "Recycling Service",
        BillItemType.StormwaterFee => "Stormwater Fee",
        BillItemType.LateFee => "Late Fee",
        BillItemType.ReconnectionFee => "Reconnection Fee",
        BillItemType.Adjustment => "Adjustment",
        BillItemType.Other => "Other",
        _ => "Unknown"
    };

    /// <summary>
    /// Formatted unit price for display
    /// </summary>
    [NotMapped]
    public string FormattedUnitPrice => UnitPrice.ToString("C2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formatted total for display
    /// </summary>
    [NotMapped]
    public string FormattedTotal => TotalAmount.ToString("C2", CultureInfo.InvariantCulture);
}

/// <summary>
/// Types of bill items for utility billing
/// </summary>
public enum BillItemType
{
    WaterUsage = 0,
    WaterBaseFee = 1,
    SewerUsage = 2,
    SewerBaseFee = 3,
    GarbageService = 4,
    RecyclingService = 5,
    StormwaterFee = 6,
    LateFee = 7,
    ReconnectionFee = 8,
    Adjustment = 9,
    Other = 10
}
