using System;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Centralized service for application-wide theme management and notifications.
/// Coordinates between UI settings and the global SfSkinManager state.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Event raised when the application theme has changed.
    /// </summary>
    event EventHandler<string> ThemeChanged;

    /// <summary>
    /// Gets the current application theme name.
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Gets a value indicating whether the current theme is a dark theme.
    /// </summary>
    bool IsDark { get; }

    /// <summary>
    /// Applies the specified theme globally and notifies subscribers.
    /// This also persists the theme choice to application settings.
    /// </summary>
    /// <param name="themeName">The name of the theme to apply (e.g., "Office2019Dark").</param>
    void ApplyTheme(string themeName);
}
