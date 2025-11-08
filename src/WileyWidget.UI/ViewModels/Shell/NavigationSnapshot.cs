using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WileyWidget.ViewModels.Shell;

/// <summary>
/// Immutable snapshot describing a shell navigation state for history tracking and telemetry.
/// </summary>
/// <param name="Id">Unique identifier for the navigation occurrence.</param>
/// <param name="Route">Stable route key.</param>
/// <param name="DisplayName">User-facing title.</param>
/// <param name="Icon">Glyph or resource identifier.</param>
/// <param name="Description">Short description of the destination.</param>
/// <param name="ViewModel">View model instance displayed for this snapshot.</param>
/// <param name="Timestamp">UTC timestamp when the snapshot was recorded.</param>
/// <param name="Breadcrumb">Breadcrumb trail for analytics and restoration.</param>
public sealed record NavigationSnapshot(
    Guid Id,
    string Route,
    string DisplayName,
    string Icon,
    string Description,
    object ViewModel,
    DateTimeOffset Timestamp,
    IReadOnlyList<string> Breadcrumb)
{
    public static NavigationSnapshot Create(
        string route,
        string displayName,
        string icon,
        string description,
        object viewModel,
        IReadOnlyList<string> breadcrumb)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(breadcrumb);

        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("Route cannot be empty.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        var sanitizedRoute = route.Trim();
        var sanitizedDisplayName = displayName.Trim();

        var sanitizedDescription = string.IsNullOrWhiteSpace(description)
            ? sanitizedDisplayName
            : description.Trim();

        var breadcrumbItems = new List<string>();
        foreach (var entry in breadcrumb)
        {
            if (!string.IsNullOrWhiteSpace(entry))
            {
                breadcrumbItems.Add(entry.Trim());
            }
        }

        if (breadcrumbItems.Count == 0)
        {
            breadcrumbItems.Add(sanitizedDisplayName);
        }

        var readOnlyBreadcrumb = new ReadOnlyCollection<string>(breadcrumbItems);

        return new NavigationSnapshot(
            Guid.NewGuid(),
            sanitizedRoute,
            sanitizedDisplayName,
            icon,
            sanitizedDescription,
            viewModel,
            DateTimeOffset.UtcNow,
            readOnlyBreadcrumb);
    }
}

/// <summary>
/// Describes how a navigation request was triggered. Useful for telemetry and analytics.
/// </summary>
public enum NavigationTrigger
{
    Startup,
    Selection,
    Command,
    Back,
    Forward
}
