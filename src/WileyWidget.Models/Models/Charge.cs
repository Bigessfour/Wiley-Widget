#nullable enable
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents an individual charge item for utility billing
/// </summary>
public class Charge : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the bill this charge belongs to
    /// </summary>
    public int BillId { get; set; }

    /// <summary>
    /// Navigation property to the bill
    /// </summary>
    [ForeignKey(nameof(BillId))]
    public UtilityBill? Bill { get; set; }

    private string _chargeType = string.Empty;

    /// <summary>
    /// Type of charge (Water, Sewer, Garbage, etc.)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ChargeType
    {
        get => _chargeType;
        set
        {
            if (_chargeType != value)
            {
                _chargeType = value;
                OnPropertyChanged();
            }
        }
    }

    private string _description = string.Empty;

    /// <summary>
    /// Description of the charge
    /// </summary>
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

    private decimal _amount;

    /// <summary>
    /// Amount of the charge
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue)]
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

    private decimal _quantity = 1;

    /// <summary>
    /// Quantity for the charge (e.g., cubic feet of water, number of garbage pickups)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    [Range(0, double.MaxValue)]
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _rate;

    /// <summary>
    /// Rate per unit for the charge
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Rate
    {
        get => _rate;
        set
        {
            if (_rate != value)
            {
                _rate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Calculated amount (Quantity * Rate)
    /// </summary>
    [NotMapped]
    public decimal CalculatedAmount => Quantity * Rate;

    /// <summary>
    /// Date the charge was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Date the charge was last modified
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.Now;
}
