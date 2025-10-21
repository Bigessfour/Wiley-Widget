using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// Centralized theme service for managing application themes.
/// Provides a unified API for theme management across the entire application.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current theme name.
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Gets whether the current theme is dark.
    /// </summary>
    bool IsDarkTheme { get; }

    /// <summary>
    /// Gets the current VisualStyles enum value.
    /// </summary>
    VisualStyles CurrentVisualStyle { get; }

    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Applies a theme by name.
    /// </summary>
    /// <param name="themeName">The theme name to apply.</param>
    void ApplyTheme(string themeName);

    /// <summary>
    /// Applies a theme to a specific window.
    /// </summary>
    /// <param name="window">The window to apply the theme to.</param>
    /// <param name="themeName">The theme name.</param>
    void ApplyThemeToWindow(Window window, string themeName);

    /// <summary>
    /// Applies a theme to a specific control.
    /// </summary>
    /// <param name="control">The control to apply the theme to.</param>
    /// <param name="themeName">The theme name.</param>
    void ApplyThemeToControl(FrameworkElement control, string themeName);

    /// <summary>
    /// Converts a theme name to VisualStyles enum.
    /// </summary>
    /// <param name="themeName">The theme name.</param>
    /// <returns>The VisualStyles enum value.</returns>
    VisualStyles ToVisualStyle(string themeName);

    /// <summary>
    /// Converts VisualStyles enum to theme name.
    /// </summary>
    /// <param name="style">The VisualStyles enum value.</param>
    /// <returns>The theme name.</returns>
    string FromVisualStyle(VisualStyles style);

    /// <summary>
    /// Resets to the default theme.
    /// </summary>
    void ResetToDefault();

    /// <summary>
    /// Gets all available theme names.
    /// </summary>
    /// <returns>Array of available theme names.</returns>
    string[] GetAvailableThemes();
}

/// <summary>
/// Implementation of the theme service.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ThemeService> _logger;

    /// <summary>
    /// Available themes in the application.
    /// </summary>
    public static readonly string[] AvailableThemes = new[]
    {
        "FluentLight",
        "FluentDark",
        "SystemTheme"
    };

    /// <summary>
    /// Default theme to use when no preference is set.
    /// </summary>
    public const string DefaultTheme = "FluentLight";

    public ThemeService(ISettingsService settingsService, ILogger<ThemeService>? logger = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? NullLogger<ThemeService>.Instance;
    }

    public string CurrentTheme => _settingsService.Current.Theme ?? DefaultTheme;

    public bool IsDarkTheme => CurrentTheme.Contains("Dark");

    public VisualStyles CurrentVisualStyle => ToVisualStyle(CurrentTheme);

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public void ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            _logger.LogWarning("Attempted to apply null or empty theme name");
            return;
        }

        var normalizedTheme = NormalizeTheme(themeName);
        if (!Array.Exists(AvailableThemes, t => t == normalizedTheme))
        {
            _logger.LogWarning("Theme '{Theme}' is not in the list of available themes", normalizedTheme);
            return;
        }

        var oldTheme = CurrentTheme;
        if (oldTheme == normalizedTheme)
        {
            _logger.LogDebug("Theme '{Theme}' is already active", normalizedTheme);
            return;
        }

        try
        {
            // Apply globally using SfSkinManager
            if (normalizedTheme == "SystemTheme")
            {
                SfSkinManager.ApplicationTheme = new Theme("SystemTheme");
            }
            else
            {
                SfSkinManager.ApplicationTheme = new Theme(normalizedTheme);
            }

            // Persist the theme setting
            _settingsService.Current.Theme = normalizedTheme;
            _settingsService.Save();

            // Raise event
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, normalizedTheme));

            _logger.LogInformation("Theme changed from '{OldTheme}' to '{NewTheme}'", oldTheme, normalizedTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme '{Theme}'", normalizedTheme);
            throw;
        }
    }

    public void ApplyThemeToWindow(Window window, string themeName)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (string.IsNullOrWhiteSpace(themeName)) return;

        try
        {
            var normalizedTheme = NormalizeTheme(themeName);
            var visualStyle = ToVisualStyle(normalizedTheme);

            SfSkinManager.SetVisualStyle(window, visualStyle);
            _logger.LogDebug("Applied theme '{Theme}' to window '{WindowType}'",
                normalizedTheme, window.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply theme '{Theme}' to window '{WindowType}'",
                themeName, window.GetType().Name);
        }
    }

    public void ApplyThemeToControl(FrameworkElement control, string themeName)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (string.IsNullOrWhiteSpace(themeName)) return;

        try
        {
            var normalizedTheme = NormalizeTheme(themeName);
            var visualStyle = ToVisualStyle(normalizedTheme);

            SfSkinManager.SetVisualStyle(control, visualStyle);
            _logger.LogDebug("Applied theme '{Theme}' to control '{ControlType}'",
                normalizedTheme, control.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply theme '{Theme}' to control '{ControlType}'",
                themeName, control.GetType().Name);
        }
    }

    public VisualStyles ToVisualStyle(string themeName)
    {
        var normalized = NormalizeTheme(themeName);
        return normalized switch
        {
            "FluentLight" => VisualStyles.FluentLight,
            "FluentDark" => VisualStyles.FluentDark,
            "SystemTheme" => VisualStyles.SystemTheme,
            _ => VisualStyles.FluentLight
        };
    }

    public string FromVisualStyle(VisualStyles style)
    {
        return style switch
        {
            VisualStyles.FluentLight => "FluentLight",
            VisualStyles.FluentDark => "FluentDark",
            VisualStyles.SystemTheme => "SystemTheme",
            _ => DefaultTheme
        };
    }

    public void ResetToDefault()
    {
        ApplyTheme(DefaultTheme);
    }

    public string[] GetAvailableThemes()
    {
        return (string[])AvailableThemes.Clone();
    }

    private string NormalizeTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName)) return DefaultTheme;

        // Handle legacy theme names
        return themeName.Replace(" ", string.Empty) switch
        {
            "Light" => "FluentLight",
            "Dark" => "FluentDark",
            "FluentLight" => "FluentLight",
            "FluentDark" => "FluentDark",
            "SystemTheme" => "SystemTheme",
            "System" => "SystemTheme",
            _ => DefaultTheme
        };
    }
}

/// <summary>
/// Event arguments for theme change notifications.
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public string OldTheme { get; }
    public string NewTheme { get; }

    public ThemeChangedEventArgs(string oldTheme, string newTheme)
    {
        OldTheme = oldTheme;
        NewTheme = newTheme;
    }
}