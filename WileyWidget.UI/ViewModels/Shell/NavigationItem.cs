using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Prism.Mvvm;

namespace WileyWidget.ViewModels.Shell;

/// <summary>
/// Navigation metadata for shell sections. Provides rich descriptors and snapshot creation.
/// </summary>
public partial class NavigationItem : BindableBase
{
    private readonly Func<NavigationSnapshot> _snapshotFactory;

    public NavigationItem(
        string route,
        string name,
        string icon,
        string description,
        object viewModel,
        IReadOnlyList<string>? breadcrumb = null)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("Route cannot be empty.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        Route = route.Trim();
        Icon = string.IsNullOrWhiteSpace(icon) ? string.Empty : icon.Trim();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        Name = name.Trim();

        Description = string.IsNullOrWhiteSpace(description)
            ? Name
            : description.Trim();

        Breadcrumb = BuildBreadcrumb(breadcrumb, Name);

        _snapshotFactory = () => NavigationSnapshot.Create(Route, Name, Icon, Description, ViewModel, Breadcrumb);
    }

    /// <summary>
    /// Stable route key used for analytics and tab restoration.
    /// </summary>
    public string Route { get; }

    /// <summary>
    /// Icon glyph or resource key.
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Short description displayed in navigation list.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Breadcrumb trail describing the logical navigation path.
    /// </summary>
    public IReadOnlyList<string> Breadcrumb { get; }

    /// <summary>
    /// View model instance associated with the navigation entry.
    /// </summary>
    public object ViewModel { get; }

    private string name = string.Empty;
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    /// <summary>
    /// Generates an immutable snapshot representing the current navigation entry.
    /// </summary>
    public NavigationSnapshot CreateSnapshot() => _snapshotFactory();

    private static IReadOnlyList<string> BuildBreadcrumb(IReadOnlyList<string>? breadcrumb, string displayName)
    {
        if (breadcrumb is null)
        {
            return new ReadOnlyCollection<string>(new List<string> { "Shell", displayName });
        }

        var items = new List<string>(breadcrumb.Count);
        foreach (var entry in breadcrumb)
        {
            if (!string.IsNullOrWhiteSpace(entry))
            {
                items.Add(entry.Trim());
            }
        }

        if (items.Count == 0)
        {
            items.Add(displayName);
        }

        return new ReadOnlyCollection<string>(items);
    }
}
