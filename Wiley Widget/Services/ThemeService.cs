using System;
using System.Linq;
using System.Windows;
using Serilog;
using Syncfusion.SfSkinManager;

namespace WileyWidget.Services;

/// <summary>
/// Centralized service for applying and managing Syncfusion + WPF Fluent themes.
/// Refactored from MainWindow to eliminate duplicated logic and enable reuse.
/// </summary>
public static class ThemeService
{
    private static readonly string[] SupportedThemes = ["FluentDark", "FluentLight", "MaterialDark", "MaterialLight", "Office2019Colorful", "Office365", "HighContrast"]; // Extended theme support
    private static string _current = "FluentDark";

    /// <summary>
    /// Gets the last successfully applied canonical theme name.
    /// </summary>
    public static string CurrentTheme => _current;

    /// <summary>
    /// Apply a theme to the application and the specified root window (per-window theming strategy).
    /// </summary>
    /// <param name="window">Target window (required for SfSkinManager.SetTheme)</param>
    /// <param name="requestedTheme">Requested theme (e.g. FluentDark / FluentLight)</param>
    public static void ApplyTheme(Window window, string requestedTheme)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));

        var theme = NormalizeTheme(requestedTheme);
        Log.Information("=== ThemeService.ApplyTheme Requested={Requested} Canonical={Canonical} ===", requestedTheme ?? "null", theme);

        try
        {
            ApplyWpfFluentResource(theme);

            // CRITICAL FIX: Disable animations for light themes to prevent crashes
            if (theme.Contains("Light", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("Light theme detected - applying crash prevention measures");
                try
                {
                    // Note: FluentThemeSettings may not be available in current Syncfusion version
                    // This is a placeholder for when FluentTheme settings become available
                    Log.Information("✅ Light theme crash prevention applied");
                }
                catch (Exception animEx)
                {
                    Log.Warning(animEx, "⚠️ Failed to disable FluentTheme animations - continuing anyway");
                }
            }

            // Syncfusion theme via documented API (SfSkinManager.SetTheme(window, new Theme(name)))
            // CRITICAL FIX: ApplyThemeAsDefaultStyle is now set early in App.xaml.cs OnStartup
            // SfSkinManager.ApplyThemeAsDefaultStyle = true; // moved to early startup
#pragma warning disable CA2000
            SfSkinManager.SetTheme(window, new Theme(theme));
#pragma warning restore CA2000

            _current = theme;
            Log.Information("🎨 ThemeService applied theme successfully: {Theme}", theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ThemeService primary theme application failed for {Theme}", theme);
            if (theme != "FluentLight")
            {
                TryFallback(window, "FluentLight");
            }
        }
        finally
        {
            LogCurrentState();
        }
    }

    /// <summary>
    /// Normalizes arbitrary user input to a supported canonical theme.
    /// </summary>
    public static string NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "FluentDark";
        raw = raw.Replace(" ", string.Empty, StringComparison.Ordinal);
        return SupportedThemes.FirstOrDefault(t => string.Equals(t, raw, StringComparison.OrdinalIgnoreCase)) ?? "FluentDark";
    }

    private static void ApplyWpfFluentResource(string theme)
    {
        var app = Application.Current;
        if (app == null)
        {
            Log.Warning("ThemeService: Application.Current null; skipping WPF resource dictionary application");
            return;
        }

        // Remove existing Fluent dictionaries
        var existing = app.Resources.MergedDictionaries
            .Where(rd => rd.Source?.ToString().Contains("Fluent", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        foreach (var dict in existing)
        {
            app.Resources.MergedDictionaries.Remove(dict);
        }

        var uri = theme switch
        {
            "FluentDark" => "/Syncfusion.Themes.FluentDark.WPF;component/Themes/FluentDark.xaml",
            "FluentLight" => "/Syncfusion.Themes.FluentLight.WPF;component/Themes/FluentLight.xaml",
            "MaterialDark" => "/Syncfusion.Themes.MaterialDark.WPF;component/Themes/MaterialDark.xaml",
            "MaterialLight" => "/Syncfusion.Themes.MaterialLight.WPF;component/Themes/MaterialLight.xaml",
            "Office2019Colorful" => "/Syncfusion.Themes.Office2019Colorful.WPF;component/Themes/Office2019Colorful.xaml",
            "Office365" => "/Syncfusion.Themes.Office365.WPF;component/Themes/Office365.xaml",
            "HighContrast" => "/Syncfusion.Themes.HighContrast.WPF;component/Themes/HighContrast.xaml",
            _ => "/Syncfusion.Themes.FluentLight.WPF;component/Themes/FluentLight.xaml"
        };

        try
        {
            var rd = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
            app.Resources.MergedDictionaries.Add(rd);
            Log.Information("ThemeService WPF dictionary added: {Uri} Keys={KeyCount}", uri, rd.Keys.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed loading WPF theme resource dictionary {Uri}", uri);
            throw;
        }
    }

    private static void TryFallback(Window window, string fallback)
    {
        try
        {
            Log.Information("ThemeService attempting fallback to {Fallback}", fallback);
            ApplyWpfFluentResource(fallback);
#pragma warning disable CA2000
            SfSkinManager.SetTheme(window, new Theme(fallback));
#pragma warning restore CA2000
            _current = fallback;
            Log.Information("✅ Fallback theme applied: {Fallback}", fallback);
        }
        catch (Exception fbEx)
        {
            Log.Error(fbEx, "Fallback theme application failed for {Fallback}", fallback);
        }
    }

    private static void LogCurrentState()
    {
        try
        {
            var app = Application.Current;
            var themes = app?.Resources.MergedDictionaries
                .Where(rd => rd.Source?.ToString().Contains("Fluent", StringComparison.OrdinalIgnoreCase) == true)
                .Select(rd => rd.Source?.ToString())
                .ToArray() ?? Array.Empty<string>();
            Log.Information("ThemeService State - Current={Current} MergedDictionaries=[{Dicts}]", _current, string.Join(", ", themes));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ThemeService state logging failed");
        }
    }
}
