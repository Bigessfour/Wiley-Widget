using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models;

/// <summary>
/// Represents a Widget entity for the WileyWidget application
/// </summary>
public class Widget
{
    /// <summary>
    /// Unique identifier for the widget
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Name of the widget (required, max 100 characters)
    /// </summary>
    [Required(ErrorMessage = "Widget name is required")]
    [StringLength(100, ErrorMessage = "Widget name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the widget (optional, max 500 characters)
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Price of the widget (must be greater than 0)
    /// </summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    /// <summary>
    /// Quantity in stock (optional)
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
    public int Quantity { get; set; }

    /// <summary>
    /// Whether the widget is active/available
    /// </summary>
    public bool IsActive { get; set; } = true;

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

    /// <summary>
    /// Category or type of widget
    /// </summary>
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// SKU (Stock Keeping Unit) for the widget
    /// </summary>
    [StringLength(20)]
    public string SKU { get; set; } = string.Empty;

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
    public string DisplayName => string.IsNullOrEmpty(SKU) ? Name : $"{Name} ({SKU})";
}
