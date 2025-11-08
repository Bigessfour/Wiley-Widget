using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using System.Windows.Media;

namespace WileyWidget.Services;

/// <summary>
/// Centralized theme service for managing application themes with high-DPI support.
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
    /// Gets the current DPI scale factor.
    /// </summary>
    double DpiScaleFactor { get; }

    /// <summary>
    /// Gets whether high-DPI scaling is enabled.
    /// </summary>
    bool IsHighDpiEnabled { get; }

    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Event raised when DPI settings change.
    /// </summary>
    event EventHandler<DpiChangedEventArgs>? DpiChanged;

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
    /// Applies a theme to a specific control.</param>
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
    /// Scales a value for high-DPI displays.
    /// </summary>
    /// <param name="value">The value to scale.</param>
    /// <returns>The scaled value.</returns>
    double ScaleForDpi(double value);

    /// <summary>
    /// Gets the recommended font size for the current DPI.
    /// </summary>
    /// <param name="baseSize">The base font size.</param>
    /// <returns>The scaled font size.</returns>
    double GetScaledFontSize(double baseSize);

    /// <summary>
    /// Updates DPI scaling when system DPI changes.
    /// </summary>
    /// <param name="newDpiScale">The new DPI scale factor.</param>
    void UpdateDpiScale(double newDpiScale);

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
    private double _dpiScaleFactor = 1.0;
    private bool _isHighDpiEnabled = true;

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

    /// <summary>
    /// Standard DPI scale factor (96 DPI).
    /// </summary>
    public const double StandardDpi = 96.0;

    /// <summary>
    /// High DPI threshold (125% scaling).
    /// </summary>
    public const double HighDpiThreshold = 1.25;

    public ThemeService(ISettingsService settingsService, ILogger<ThemeService>? logger = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? NullLogger<ThemeService>.Instance;

        // Initialize DPI scaling
        InitializeDpiScaling();
    }

    public string CurrentTheme => _settingsService.Current.Theme ?? DefaultTheme;

    public bool IsDarkTheme => CurrentTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase);

    public VisualStyles CurrentVisualStyle => ToVisualStyle(CurrentTheme);

    public double DpiScaleFactor => _dpiScaleFactor;

    public bool IsHighDpiEnabled => _isHighDpiEnabled;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public event EventHandler<DpiChangedEventArgs>? DpiChanged;

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

    public double ScaleForDpi(double value)
    {
        return _isHighDpiEnabled ? value * _dpiScaleFactor : value;
    }

    public double GetScaledFontSize(double baseSize)
    {
        // Font scaling follows different rules than general UI scaling
        // Use a more conservative scaling factor for fonts
        var fontScaleFactor = Math.Min(_dpiScaleFactor, 1.5); // Cap at 150% for readability
        return _isHighDpiEnabled ? baseSize * fontScaleFactor : baseSize;
    }

    public void UpdateDpiScale(double newDpiScale)
    {
        if (Math.Abs(newDpiScale - _dpiScaleFactor) < 0.01) return; // No significant change

        var oldScale = _dpiScaleFactor;
        _dpiScaleFactor = Math.Max(0.5, Math.Min(3.0, newDpiScale)); // Clamp between 50% and 300%
        _isHighDpiEnabled = _dpiScaleFactor >= HighDpiThreshold;

        _logger.LogInformation("DPI scale updated from {OldScale} to {NewScale}, High-DPI: {IsHighDpi}",
            oldScale, _dpiScaleFactor, _isHighDpiEnabled);

        // Raise DPI changed event
        DpiChanged?.Invoke(this, new DpiChangedEventArgs(oldScale, _dpiScaleFactor, _isHighDpiEnabled));

        // Re-apply current theme to ensure proper scaling
        ApplyTheme(CurrentTheme);
    }

    public string[] GetAvailableThemes()
    {
        return (string[])AvailableThemes.Clone();
    }

    /// <summary>
    /// Initializes DPI scaling based on system settings.
    /// </summary>
    private void InitializeDpiScaling()
    {
        try
        {
            // Get system DPI information
            var presentationSource = PresentationSource.FromVisual(Application.Current?.MainWindow);
            if (presentationSource?.CompositionTarget != null)
            {
                var matrix = presentationSource.CompositionTarget.TransformToDevice;
                var dpiX = StandardDpi * matrix.M11;
                var dpiY = StandardDpi * matrix.M22;

                // Use the higher DPI value for scaling calculations
                var systemDpi = Math.Max(dpiX, dpiY);
                _dpiScaleFactor = systemDpi / StandardDpi;
                _isHighDpiEnabled = _dpiScaleFactor >= HighDpiThreshold;

                _logger.LogInformation("Initialized DPI scaling: System DPI {SystemDpi}, Scale Factor {ScaleFactor}, High-DPI {IsHighDpi}",
                    systemDpi, _dpiScaleFactor, _isHighDpiEnabled);
            }
            else
            {
                // Fallback to default scaling
                _dpiScaleFactor = 1.0;
                _isHighDpiEnabled = false;
                _logger.LogWarning("Could not determine system DPI, using default scaling");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DPI scaling, using defaults");
            _dpiScaleFactor = 1.0;
            _isHighDpiEnabled = false;
        }
    }

    private string NormalizeTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName)) return DefaultTheme;

        // Handle legacy theme names
        return themeName.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase) switch
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

/// <summary>
/// Event arguments for DPI change notifications.
/// </summary>
public class DpiChangedEventArgs : EventArgs
{
    public double OldDpiScale { get; }
    public double NewDpiScale { get; }
    public bool IsHighDpiEnabled { get; }

    public DpiChangedEventArgs(double oldDpiScale, double newDpiScale, bool isHighDpiEnabled)
    {
        OldDpiScale = oldDpiScale;
        NewDpiScale = newDpiScale;
        IsHighDpiEnabled = isHighDpiEnabled;
    }
}
