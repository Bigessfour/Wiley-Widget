using System.Collections.ObjectModel;

namespace WileyWidget.Models;

/// <summary>
/// Represents a navigation item in the hierarchical tree view.
/// Uses init-only properties for immutable initialization pattern.
/// </summary>
public class NavigationItem
{
    /// <summary>
    /// Gets the display name of the navigation item
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the account number or identifier (e.g., "405.1")
    /// </summary>
    public string AccountNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description or tooltip text
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the icon or symbol for the item
    /// </summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// Gets the command to execute when selected
    /// </summary>
    public object? Command { get; init; }

    /// <summary>
    /// Gets the command parameter
    /// </summary>
    public object? CommandParameter { get; init; }

    /// <summary>
    /// Gets the child navigation items
    /// </summary>
    public ObservableCollection<NavigationItem> Children { get; init; } = new();

    /// <summary>
    /// Gets or sets whether this item is expanded in the tree view
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Gets or sets whether this item is selected
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets the display text combining account number and name
    /// </summary>
    public string DisplayText => string.IsNullOrEmpty(AccountNumber)
        ? Name
        : $"{AccountNumber} - {Name}";
}
