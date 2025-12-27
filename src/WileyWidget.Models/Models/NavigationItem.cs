using System.Collections.ObjectModel;

namespace WileyWidget.Models;

/// <summary>
/// Represents a navigation item in the hierarchical tree view
/// </summary>
/// <summary>
/// Represents a class for navigationitem.
/// </summary>
public class NavigationItem
{
    /// <summary>
    /// Display name of the navigation item
    /// </summary>
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Account number or identifier (e.g., "405.1")
    /// </summary>
    /// <summary>
    /// Gets or sets the accountnumber.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Description or tooltip text
    /// </summary>
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icon or symbol for the item
    /// </summary>
    /// <summary>
    /// Gets or sets the icon.
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Command to execute when selected
    /// </summary>
    public object? Command { get; set; }

    /// <summary>
    /// Command parameter
    /// </summary>
    public object? CommandParameter { get; set; }

    /// <summary>
    /// Child navigation items
    /// </summary>
    public ObservableCollection<NavigationItem> Children { get; set; } = new();

    /// <summary>
    /// Whether this item is expanded in the tree view
    /// </summary>
    /// <summary>
    /// Gets or sets the isexpanded.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Whether this item is selected
    /// </summary>
    /// <summary>
    /// Gets or sets the isselected.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Display text combining account number and name
    /// </summary>
    public string DisplayText => string.IsNullOrEmpty(AccountNumber)
        ? Name
        : $"{AccountNumber} - {Name}";
}
