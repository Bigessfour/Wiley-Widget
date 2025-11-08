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
        raw = raw.Replace(" ", string.Empty, StringComparison.Ordinal); // allow "Fluent Dark" legacy
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
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        try
        {
            var canonical = NormalizeTheme(themeName);
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
            SfSkinManager.SetTheme(window, new Theme(canonical));
#pragma warning restore CA2000 // Dispose objects before losing scope
            // SetVisualStyle is redundant - SetTheme already applies it per Syncfusion v31.1.17 docs
            Log.Information("Successfully applied theme: {Theme} to window {WindowType}",
                           canonical, window.GetType().Name);
        }
        catch (Exception ex)
        {
            // FIXED: Log error directly instead of using ErrorReportingService.Instance
            // ErrorReportingService should be resolved via DI, not accessed statically
            Log.Error(ex, "Failed to apply theme {ThemeName} to window {WindowType}", themeName, window.GetType().Name);

            if (themeName != "FluentLight")
            {
                // Fallback to FluentLight
                try
                {
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                    SfSkinManager.SetTheme(window, new Theme("FluentLight"));
#pragma warning restore CA2000 // Dispose objects before losing scope
                    // SetVisualStyle is redundant - SetTheme already applies it per Syncfusion v31.1.17 docs
                    Log.Warning("Applied fallback theme 'FluentLight' after failing to apply '{ThemeName}' to window {WindowType}",
                               themeName, window.GetType().Name);
                }
                catch (Exception fallbackEx)
                {
                    // FIXED: Log critical error directly instead of using ErrorReportingService.Instance
                    Log.Fatal(fallbackEx, "Theme fallback to FluentLight failed - UI may be unstable");
                }
            }
        }
    }

    /// <summary>
    /// Applies the current theme from settings to a window.
    /// FIXED: Removed dependency on static SettingsService.Instance to prevent DI resolution timeout.
    /// Theme should be applied via ThemeService or passed explicitly.
    /// </summary>
    [Obsolete("Use IThemeService.ApplyCurrentTheme() or TryApplyTheme() with explicit theme name instead")]
    public static void ApplyCurrentTheme(System.Windows.Window window)
    {
        // Fallback to default theme when settings service is not available
        // In production, theme should be managed by IThemeService which has proper DI
        const string defaultTheme = "FluentDark";
        TryApplyTheme(window, defaultTheme);
    }
}
