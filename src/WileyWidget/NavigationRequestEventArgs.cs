#nullable enable

using System;
using Prism.Navigation.Regions;

namespace WileyWidget;

/// <summary>
/// Event arguments for navigation requests
/// </summary>
public class NavigationRequestEventArgs : EventArgs
{
    /// <summary>
    /// Gets the target view or page to navigate to
    /// </summary>
    public string? Target { get; }

    /// <summary>
    /// Gets the panel name for navigation
    /// </summary>
    public string? PanelName { get; }

    /// <summary>
    /// Gets the view name for display
    /// </summary>
    public string? ViewName { get; }

    /// <summary>
    /// Gets the navigation parameters
    /// </summary>
    public object? Parameters { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the navigation was handled
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to clear the navigation history
    /// </summary>
    public bool ClearHistory { get; set; }

    /// <summary>
    /// Initializes a new instance of the NavigationRequestEventArgs class
    /// </summary>
    /// <param name="target">The target view or page to navigate to</param>
    /// <param name="parameters">The navigation parameters</param>
    public NavigationRequestEventArgs(string? target, object? parameters = null)
    {
        Target = target;
        PanelName = target;
        ViewName = target;
        Parameters = parameters;
    }

    /// <summary>
    /// Initializes a new instance of the NavigationRequestEventArgs class with history clearing
    /// </summary>
    /// <param name="target">The target view or page to navigate to</param>
    /// <param name="parameters">The navigation parameters</param>
    /// <param name="clearHistory">Whether to clear the navigation history</param>
    public NavigationRequestEventArgs(string? target, object? parameters, bool clearHistory)
        : this(target, parameters)
    {
        ClearHistory = clearHistory;
    }
}
