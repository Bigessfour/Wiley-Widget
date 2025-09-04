using System;
using System.Linq;
using System.Windows;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Infrastructure.Logging;

namespace WileyWidget.UI.Theming;

/// <summary>
/// Centralized service for applying and managing Syncfusion + WPF Fluent themes.
/// Refactored to use consistent theming strategy and proper resource management.
/// </summary>
public static class ThemeService
{
    /// <summary>
    /// Supported theme names for Syncfusion WPF 30.2.4
    /// Per Syncfusion documentation: https://help.syncfusion.com/wpf/themes/getting-started
    /// </summary>
    private static readonly string[] SupportedThemes =
    [
        "FluentDark",
        "FluentLight",
        "MaterialDark",
        "MaterialLight",
        "Office2019Colorful",
        "Office365",
        "HighContrast"
    ];

    private static string _currentTheme = "FluentDark";
    private static bool _isInitialized;

    /// <summary>
    /// Event raised when the application theme changes
    /// </summary>
    public static event EventHandler<ThemeChangedEventArgs> ThemeChanged;

    /// <summary>
    /// Gets the currently applied theme name
    /// </summary>
    public static string CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets whether the theme system has been initialized
    /// </summary>
    public static bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initialize the theme system with global settings
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
        {
            Log.Warning("ThemeService already initialized");
            return;
        }

        try
        {
            Log.Information("🎨 Initializing ThemeService...");

            // Set global theme application mode - Syncfusion documented approach
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            Log.Information("✅ SfSkinManager.ApplyThemeAsDefaultStyle enabled");

            _isInitialized = true;
            Log.Information("✅ ThemeService initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ ThemeService initialization failed");
            throw;
        }
    }

    /// <summary>
    /// Apply a theme to the entire application using Syncfusion WPF 30.2.4 documented approach
    /// Per Syncfusion documentation: https://help.syncfusion.com/wpf/themes/getting-started
    /// </summary>
    public static void ApplyApplicationTheme(string themeName)
    {
    EnsureInitialized();

        var theme = NormalizeTheme(themeName);
        Log.Information("🎨 Applying Syncfusion application theme: {Theme}", theme);

        try
        {
            // Store previous theme for event
            var previousTheme = _currentTheme;

            // CRITICAL: Syncfusion WPF 30.2.4 documented approach
            // ONLY use SfSkinManager - DO NOT manually load WPF resource dictionaries
            // https://help.syncfusion.com/wpf/themes/getting-started#apply-theme-to-application
            SfSkinManager.ApplicationTheme = new Theme(theme);

            _currentTheme = theme;
            StructuredLogger.LogThemeChange(previousTheme, theme, false);

            // Raise theme changed event
            OnThemeChanged(new ThemeChangedEventArgs(previousTheme, theme, false));

            Log.Information("✅ Syncfusion application theme applied successfully: {Theme}", theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to apply Syncfusion application theme: {Theme}", theme);
            throw;
        }
    }

    /// <summary>
    /// Apply a theme to a specific window
    /// </summary>
    public static void ApplyWindowTheme(Window window, string themeName)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
    EnsureInitialized();

        var theme = NormalizeTheme(themeName);
        Log.Information("🎨 Applying window theme: {Theme} to {Window}", theme, window.GetType().Name);

        try
        {
            // Apply per-window Syncfusion theme
#pragma warning disable CA2000
            SfSkinManager.SetTheme(window, new Theme(theme));
#pragma warning restore CA2000

            Log.Information("✅ Window theme applied successfully: {Theme}", theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to apply window theme: {Theme} to {Window}", theme, window.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Apply theme to both application and specific window
    /// </summary>
    public static void ApplyCompleteTheme(Window window, string themeName)
    {
        ApplyApplicationTheme(themeName);
        ApplyWindowTheme(window, themeName);
    }

    /// <summary>
    /// Change theme initiated by user action (e.g., from UI)
    /// </summary>
    public static void ChangeTheme(string themeName, bool userInitiated = true)
    {
    EnsureInitialized();

        var theme = NormalizeTheme(themeName);
        var previousTheme = _currentTheme;

        if (string.Equals(previousTheme, theme, StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("Theme change requested but already applied: {Theme}", theme);
            return;
        }

        Log.Information("🎨 User-initiated theme change: {FromTheme} → {ToTheme}", previousTheme, theme);

        try
        {
            // Apply Syncfusion theme globally - documented approach
            SfSkinManager.ApplicationTheme = new Theme(theme);

            _currentTheme = theme;
            StructuredLogger.LogThemeChange(previousTheme, theme, userInitiated);

            // Raise theme changed event
            OnThemeChanged(new ThemeChangedEventArgs(previousTheme, theme, userInitiated));

            Log.Information("✅ Theme changed successfully: {FromTheme} → {ToTheme}", previousTheme, theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to change theme: {FromTheme} → {ToTheme}", previousTheme, theme);
            throw;
        }
    }

    /// <summary>
    /// Get list of supported theme names
    /// </summary>
    public static string[] GetSupportedThemes() => SupportedThemes;

    /// <summary>
    /// Check if a theme is supported
    /// </summary>
    public static bool IsThemeSupported(string themeName) =>
        SupportedThemes.Any(t => string.Equals(t, themeName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Normalize a theme name to a supported canonical theme
    /// </summary>
    public static string NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "FluentDark";

        raw = raw.Replace(" ", string.Empty, StringComparison.Ordinal);
        return SupportedThemes.FirstOrDefault(t =>
            string.Equals(t, raw, StringComparison.OrdinalIgnoreCase)) ?? "FluentDark";
    }

    /// <summary>
    /// Event arguments for theme change notifications
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public string FromTheme { get; }
        public string ToTheme { get; }
        public bool UserInitiated { get; }
        public DateTime Timestamp { get; }

        public ThemeChangedEventArgs(string fromTheme, string toTheme, bool userInitiated)
        {
            FromTheme = fromTheme;
            ToTheme = toTheme;
            UserInitiated = userInitiated;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Raises the ThemeChanged event
    /// </summary>
    private static void OnThemeChanged(ThemeChangedEventArgs e)
    {
        ThemeChanged?.Invoke(null, e);
    }

    /// <summary>
    /// Idempotent guard ensuring initialization before any theme application.
    /// Avoids runtime InvalidOperationExceptions if callers invoke theming too early.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (_isInitialized) return;
        try
        {
            Log.Debug("ThemeService auto-initializing via EnsureInitialized()");
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed implicit ThemeService initialization");
            throw; // preserve original failure semantics
        }
    }
}
