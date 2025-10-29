#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.Models;

/// <summary>
/// Represents a Widget entity for the WileyWidget application
/// </summary>
public class Widget : INotifyPropertyChanged
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
    /// Unique identifier for the widget
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private string _name = string.Empty;

    /// <summary>
    /// Name of the widget (required, max 100 characters)
    /// </summary>
    [Required(ErrorMessage = "Widget name is required")]
    [StringLength(100, ErrorMessage = "Widget name cannot exceed 100 characters")]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name), nameof(DisplayName));
            }
        }
    }

    private string _description = string.Empty;

    /// <summary>
    /// Description of the widget (optional, max 500 characters)
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description
    {
        get => _description;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(_description, sanitized, StringComparison.Ordinal))
            {
                _description = sanitized;
                OnPropertyChanged(nameof(Description));
            }
        }
    }

    private decimal _price;

    /// <summary>
    /// Price of the widget (must be greater than 0)
    /// </summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    [DataType(DataType.Currency)]
    public decimal Price
    {
        get => _price;
        set
        {
            if (_price != value)
            {
                _price = value;
                OnPropertyChanged(nameof(Price), nameof(FormattedPrice));
            }
        }
    }

    private int _quantity;

    /// <summary>
    /// Quantity in stock (optional)
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
            }
        }
    }

    private bool _isActive = true;

    /// <summary>
    /// Whether the widget is active/available
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    /// <summary>
    /// Date when the widget was created
    /// </summary>
    [DataType(DataType.DateTime)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the widget was last modified
    /// </summary>
    [DataType(DataType.DateTime)]
    public DateTime? ModifiedDate { get; set; }

    private string _category = string.Empty;

    /// <summary>
    /// Category or type of widget
    /// </summary>
    [StringLength(50)]
    public string Category
    {
        get => _category;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(_category, sanitized, StringComparison.Ordinal))
            {
                _category = sanitized;
                OnPropertyChanged(nameof(Category));
            }
        }
    }

    private string _sku = string.Empty;

    /// <summary>
    /// SKU (Stock Keeping Unit) for the widget
    /// </summary>
    [StringLength(20)]
    public string SKU
    {
        get => _sku;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (!string.Equals(_sku, sanitized, StringComparison.Ordinal))
            {
                _sku = sanitized;
                OnPropertyChanged(nameof(SKU), nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Updates the ModifiedDate when the widget is changed
    /// </summary>
    public void MarkAsModified()
    {
        ModifiedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns a formatted price string
    /// </summary>
    public string FormattedPrice => $"${Price:N2}";

    /// <summary>
    /// Returns a display name combining name and SKU
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(SKU) ? Name : $"{Name} ({SKU})";
}
