using System.Collections.ObjectModel;

namespace WileyWidget.Models;

/// <summary>
/// Represents a navigation item in the hierarchical tree view
/// </summary>
public class NavigationItem
{
    /// <summary>
    /// Display name of the navigation item
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Account number or identifier (e.g., "405.1")
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Description or tooltip text
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icon or symbol for the item
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
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Whether this item is selected
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Display text combining account number and name
    /// </summary>
    public string DisplayText => string.IsNullOrEmpty(AccountNumber)
        ? Name
        : $"{AccountNumber} - {Name}";
}