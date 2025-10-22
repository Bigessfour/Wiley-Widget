using System;
using System.Windows.Controls;

namespace WileyWidget.Services;

/// <summary>
/// Interface for navigation services that manage navigation between views in a WPF application.
/// Provides methods for navigating to different views, going back in navigation history,
/// and checking navigation state.
///
/// NAVIGATION ARCHITECTURE:
/// This service handles traditional page-level navigation within WPF Frame controls.
/// It is separate from ShellViewModel which handles content hosting and section navigation.
/// Use INavigationService for page-based navigation scenarios, and ShellViewModel for
/// switching between major application sections (Dashboard, Enterprises, Budget, etc.).
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets a value indicating whether the navigation service can navigate back.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Gets the current content being displayed.
    /// </summary>
    object? CurrentContent { get; }

    /// <summary>
    /// Navigates to the specified view model or content.
    /// </summary>
    /// <param name="content">The view model or content to navigate to.</param>
    /// <param name="parameter">Optional navigation parameter.</param>
    void Navigate(object content, object? parameter = null);

    /// <summary>
    /// Navigates to a view model of the specified type.
    /// </summary>
    /// <typeparam name="TViewModel">The type of view model to navigate to.</typeparam>
    /// <param name="parameter">Optional navigation parameter.</param>
    void Navigate<TViewModel>(object? parameter = null) where TViewModel : class;

    /// <summary>
    /// Navigates back to the previous view in the navigation history.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    event EventHandler<NavigationEventArgs>? Navigated;
}

/// <summary>
/// Event arguments for navigation events.
/// </summary>
public class NavigationEventArgs : EventArgs
{
    /// <summary>
    /// Gets the content that was navigated to.
    /// </summary>
    public object? Content { get; }

    /// <summary>
    /// Gets the navigation parameter.
    /// </summary>
    public object? Parameter { get; }

    /// <summary>
    /// Gets the content that was navigated from.
    /// </summary>
    public object? PreviousContent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationEventArgs"/> class.
    /// </summary>
    /// <param name="content">The content navigated to.</param>
    /// <param name="parameter">The navigation parameter.</param>
    /// <param name="previousContent">The previous content.</param>
    public NavigationEventArgs(object? content, object? parameter, object? previousContent)
    {
        Content = content;
        Parameter = parameter;
        PreviousContent = previousContent;
    }
}
