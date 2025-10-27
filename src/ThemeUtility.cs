using System;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;

namespace WileyWidget.Services;

/// <summary>
/// Shared utility class for theme management across the application.
/// Eliminates code duplication and provides consistent theme handling.
/// </summary>
public static class ThemeUtility
{
    /// <summary>
    /// Normalizes theme names to canonical Syncfusion theme names.
    /// </summary>
    public static string NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "FluentDark";
        raw = raw.Replace(" ", string.Empty); // allow "Fluent Dark" legacy
        return raw switch
        {
            "FluentDark" => "FluentDark",
            "FluentLight" => "FluentLight",
            "MaterialDark" => "FluentDark", // Map legacy MaterialDark to FluentDark
            "MaterialLight" => "FluentLight", // Map legacy MaterialLight to FluentLight
            _ => "FluentDark" // default
        };
    }

    /// <summary>
    /// Converts theme name string to VisualStyles enum.
    /// </summary>
    public static VisualStyles ToVisualStyle(string themeName)
    {
        var normalized = NormalizeTheme(themeName);
        return normalized switch
        {
            "FluentDark" => VisualStyles.FluentDark,
            "FluentLight" => VisualStyles.FluentLight,
            _ => VisualStyles.FluentDark
        };
    }

    /// <summary>
    /// Attempts to apply a Syncfusion theme with fallback to FluentLight if requested theme fails.
    /// </summary>
    public static void TryApplyTheme(System.Windows.Window window, string themeName)
    {
        try
        {
            var canonical = NormalizeTheme(themeName);
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
            SfSkinManager.SetTheme(window, new Theme(canonical));
#pragma warning restore CA2000 // Dispose objects before losing scope
            SfSkinManager.SetVisualStyle(window, ToVisualStyle(canonical));
            Log.Information("Successfully applied theme: {Theme} to window {WindowType}",
                           canonical, window.GetType().Name);
        }
        catch (Exception ex)
        {
            ErrorReportingService.Instance.ReportError(ex, $"Theme_Apply_{themeName}", showToUser: false);

            if (themeName != "FluentLight")
            {
                // Fallback to FluentLight
                try
                {
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                    SfSkinManager.SetTheme(window, new Theme("FluentLight"));
#pragma warning restore CA2000 // Dispose objects before losing scope
                    SfSkinManager.SetVisualStyle(window, VisualStyles.FluentLight);
                    Log.Warning("Applied fallback theme 'FluentLight' after failing to apply '{ThemeName}' to window {WindowType}",
                               themeName, window.GetType().Name);
                }
                catch (Exception fallbackEx)
                {
                    ErrorReportingService.Instance.ReportError(fallbackEx, "Theme_Fallback_Failed", showToUser: true);
                }
            }
        }
    }

    /// <summary>
    /// Applies the current theme from settings to a window.
    /// </summary>
    public static void ApplyCurrentTheme(System.Windows.Window window)
    {
        var currentTheme = SettingsService.Instance.Current.Theme;
        TryApplyTheme(window, currentTheme);
    }
}
