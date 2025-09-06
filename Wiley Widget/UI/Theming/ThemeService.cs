using System;
using System.Linq;
using System.Windows;
using Serilog;
using Syncfusion.SfSkinManager;

namespace WileyWidget.UI.Theming;

// Simplified theme service that provides essential theme utilities without interfering with SfSkinManager
public static class ThemeService
{
    // Supported theme names for Syncfusion WPF
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

    // Gets the currently applied theme name
    public static string CurrentTheme => _currentTheme;

    // Gets whether the theme system has been initialized
    public static bool IsInitialized => _isInitialized;

    // Initialize the theme system with global settings
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
            // Don't rethrow - allow application to continue with default styling
            _isInitialized = false;
        }
    }

    // Get list of supported themes
    public static string[] GetSupportedThemes() => SupportedThemes;

    // Normalize theme names to internal format
    public static string NormalizeTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
            return "FluentDark";

        var normalized = themeName.Trim();

        // Handle common variations
        return normalized.ToLowerInvariant() switch
        {
            "dark" or "fluentdark" => "FluentDark",
            "light" or "fluentlight" => "FluentLight",
            "materialdark" => "MaterialDark",
            "materiallight" => "MaterialLight",
            "office2019" or "office2019colorful" => "Office2019Colorful",
            "office365" => "Office365",
            "highcontrast" => "HighContrast",
            _ => SupportedThemes.Contains(normalized) ? normalized : "FluentDark"
        };
    }

    // Update current theme (called by external theme application)
    public static void SetCurrentTheme(string themeName)
    {
        _currentTheme = NormalizeTheme(themeName);
    }
}
