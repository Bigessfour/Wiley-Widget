using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Service for managing application resource dictionaries and fallbacks.
/// Handles core resource loading, diagnostics, and fallback injection.
/// </summary>
public interface IResourceManagementService
{
    /// <summary>
    /// Loads core application ResourceDictionaries.
    /// </summary>
    void LoadCoreResources();

    /// <summary>
    /// Ensures critical resource fallbacks exist.
    /// </summary>
    void EnsureCriticalResourceFallbacks();

    /// <summary>
    /// Ensures extended fallback resources.
    /// </summary>
    void EnsureExtendedFallbacks();

    /// <summary>
    /// Diagnoses duplicate resource keys across merged dictionaries.
    /// </summary>
    void DiagnoseDuplicateResourceKeys();

    /// <summary>
    /// Verifies global resources exist.
    /// </summary>
    void VerifyGlobalResources(string[] keys);

    /// <summary>
    /// Removes duplicate merged dictionaries.
    /// </summary>
    void RemoveDuplicateMergedDictionaries();

    /// <summary>
    /// Prevents custom overrides of theme keys.
    /// </summary>
    void PreventCustomOverridesOfThemeKeys();

    /// <summary>
    /// Gets diagnostic information about duplicate resources.
    /// </summary>
    IReadOnlyList<string> DuplicateResourceDiagnostics { get; }
}

/// <summary>
/// Implementation of resource management service.
/// </summary>
public class ResourceManagementService : IResourceManagementService
{
    private readonly List<string> _duplicateResourceDiagnostics = new();

    public IReadOnlyList<string> DuplicateResourceDiagnostics => _duplicateResourceDiagnostics.AsReadOnly();

    /// <summary>
    /// Loads core application ResourceDictionaries using simple relative paths.
    /// Replaces encoded pack URIs to avoid inconsistent resolution with space in folder name.
    /// Idempotent: skips dictionaries already loaded.
    /// </summary>
    public void LoadCoreResources()
    {
        var order = new[]
        {
            "Wiley Widget/Resources/Colors.xaml",
            "Wiley Widget/Resources/Typography.xaml",
            "Wiley Widget/Resources/Spacing.xaml",
            "Wiley Widget/Resources/SyncfusionThemeAdapter.xaml",
            "Wiley Widget/Resources/Controls.Base.xaml",
            "Wiley Widget/Resources/Features/Dashboard.Stub.xaml",
            "Wiley Widget/Resources/Features/Reports.Stub.xaml",
            "Wiley Widget/Resources/SyncfusionResources.xaml",
            "Wiley Widget/Resources/UserOverrides.xaml"
        };

        if (Application.Current.Resources == null)
        {
            Application.Current.Resources = new ResourceDictionary();
        }

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in Application.Current.Resources.MergedDictionaries)
        {
            var src = d.Source?.OriginalString ?? string.Empty;
            existing.Add(src.Replace('%', ' ')); // normalize any encoded remnants
        }

        foreach (var path in order)
        {
            if (existing.Contains(path)) continue; // already loaded
            try
            {
                var dict = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(dict);
                Console.WriteLine($"📦 Loaded resource dictionary: {path}");
                Log.Information("Loaded resource dictionary: {Path}", path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load resource dictionary {path}: {ex.Message}");
                Log.Warning(ex, "Failed to load resource dictionary {Path}", path);
            }
        }
    }

    /// <summary>
    /// Ensures critical style resources exist even if a dictionary failed to load.
    /// Injects minimal fallback definitions when missing.
    /// </summary>
    public void EnsureCriticalResourceFallbacks()
    {
        if (Application.Current.Resources == null) 
            Application.Current.Resources = new ResourceDictionary();

        // Order matters slightly for BasedOn chains
        EnsureConverter("BoolToVis");
        EnsureTextStyle("Text.Body", 13, FontWeights.Normal, foreground: System.Windows.Media.Brushes.Black);
        EnsureTextStyle("CardTitle", 14, FontWeights.SemiBold, basedOn: "Text.Body");
        EnsureTextStyle("CardValue", 20, FontWeights.Bold, foreground: System.Windows.Media.Brushes.DarkBlue, basedOn: "Text.Body");
        EnsureValidationStyles();
    }

    /// <summary>
    /// Ensures extended fallback resources for UI components.
    /// </summary>
    public void EnsureExtendedFallbacks()
    {
        if (Application.Current.Resources == null) 
            Application.Current.Resources = new ResourceDictionary();

        // Simple helper local function
        void Brush(string key, string hex)
        {
            if (Application.Current.TryFindResource(key) == null)
            {
                Application.Current.Resources[key] = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex);
                Console.WriteLine($"🛠️ Injected brush fallback: {key}");
                Log.Information("Injected brush fallback: {Key}", key);
            }
        }

        // Brushes / styles referenced by MainWindow (proactively guarded)
        Brush("StatusBorderBrush", "#FFB0B0B0");
        Brush("StatusBackground", "#FFF8F8F8");
        Brush("WarningBackground", "#FFFFF4E5");
        Brush("WarningBorderBrush", "#FFE0B060");
        Brush("WarningTextForeground", "#FF4A3A00");
        Brush("TestApiBackground", "#FF2D7DD2");
        Brush("SaveSettingsBackground", "#FF2E8B57");
        Brush("LoadSettingsBackground", "#FF4169E1");
        Brush("RemoveApiBackground", "#FFB22222");
        Brush("ApiInfoBackground", "#FF8A2BE2");
        Brush("GuideButtonBackground", "#FF1976D2");
        Brush("GuideButtonForeground", "#FFFFFFFF");
        Brush("TestConfigForeground", "#FFFFFFFF");
        Brush("ThemeStatusBarBackground", "#FF202124");
        Brush("ThemeStatusBarBorderBrush", "#33000000");

        // Status bar style
        if (Application.Current.TryFindResource("ThemeStatusBarBorder") == null)
        {
            var style = new Style(typeof(Border))
            {
                Setters =
                {
                    new Setter(Border.BackgroundProperty, new DynamicResourceExtension("ThemeStatusBarBackground")),
                    new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("ThemeStatusBarBorderBrush")),
                    new Setter(Border.BorderThicknessProperty, new Thickness(1,0,0,0))
                }
            };
            Application.Current.Resources["ThemeStatusBarBorder"] = style;
            Console.WriteLine("🛠️ Injected style fallback: ThemeStatusBarBorder");
            Log.Information("Injected style fallback: ThemeStatusBarBorder");
        }

        // Generic themed button style
        if (Application.Current.TryFindResource("ThemeSfButton") == null)
        {
            var btn = new Style(typeof(Button))
            {
                Setters =
                {
                    new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White),
                    new Setter(Control.PaddingProperty, new Thickness(12,6,12,6)),
                    new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                    new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand)
                }
            };
            Application.Current.Resources["ThemeSfButton"] = btn;
            Console.WriteLine("🛠️ Injected style fallback: ThemeSfButton");
            Log.Information("Injected style fallback: ThemeSfButton");
        }
    }

    /// <summary>
    /// Logs duplicate resource keys across merged dictionaries, showing first and final provider.
    /// Helps verify cleanup of primitive duplicates.
    /// </summary>
    public void DiagnoseDuplicateResourceKeys()
    {
        var md = Application.Current.Resources?.MergedDictionaries;
        if (md == null || md.Count == 0) return;

        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var dict in md)
        {
            var src = dict.Source?.OriginalString ?? "__INLINE";
            foreach (var key in dict.Keys)
            {
                if (key is string sk)
                {
                    if (!map.TryGetValue(sk, out var list))
                    {
                        list = new List<string>();
                        map[sk] = list;
                    }
                    list.Add(src);
                }
            }
        }

        int duplicateCount = 0;
        _duplicateResourceDiagnostics.Clear();
        foreach (var kvp in map)
        {
            if (kvp.Value.Count > 1)
            {
                if (duplicateCount == 0)
                {
                    Console.WriteLine("🔍 Duplicate resource keys detected (showing up to 50):");
                    _duplicateResourceDiagnostics.Add("Duplicate resource keys detected:");
                }
                duplicateCount++;
                if (duplicateCount <= 50)
                {
                    var line = $" • {kvp.Key} => first: {kvp.Value[0]} final: {kvp.Value[^1]} total:{kvp.Value.Count}";
                    Console.WriteLine(line);
                    _duplicateResourceDiagnostics.Add(line);
                }
            }
        }
        if (duplicateCount > 0)
        {
            var summary = $"Duplicate summary: {duplicateCount} keys duplicated across dictionaries";
            Console.WriteLine($"🔍 {summary}");
            _duplicateResourceDiagnostics.Add(summary);
            Log.Information(summary);
        }
        else
        {
            Console.WriteLine("✅ No duplicate resource keys detected across merged dictionaries");
            _duplicateResourceDiagnostics.Add("No duplicate resource keys detected across merged dictionaries");
        }

        // Persist to log file
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var file = Path.Combine(logDir, "ResourceDiagnostics.txt");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllLines(file, new[] { $"=== Resource Key Diagnostic @ {timestamp} ===" });
            File.AppendAllLines(file, _duplicateResourceDiagnostics);
            File.AppendAllLines(file, new[] { string.Empty });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to write resource diagnostics file: {ex.Message}");
            Log.Warning(ex, "Failed to write resource diagnostics file");
        }
    }

    /// <summary>
    /// Logs missing critical resource keys (does not throw). Uses Application-level resource lookup.
    /// </summary>
    public void VerifyGlobalResources(string[] keys)
    {
        if (keys == null || keys.Length == 0) return;
        foreach (var k in keys)
        {
            var found = Application.Current.TryFindResource(k) != null;
            if (!found)
            {
                Console.WriteLine($"❌ MISSING RESOURCE KEY: {k}");
                Log.Warning("Missing global resource key {Key}", k);
            }
            else
            {
                Console.WriteLine($"✅ Resource key present: {k}");
            }
        }
    }

    /// <summary>
    /// Removes duplicate merged dictionaries (same Source) to prevent 'Item has already been added' exceptions.
    /// </summary>
    public void RemoveDuplicateMergedDictionaries()
    {
        var appResources = Application.Current.Resources;
        if (appResources == null) return;
        var md = appResources.MergedDictionaries;
        if (md == null || md.Count <= 1) return;

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = md.Count - 1; i >= 0; i--)
        {
            var src = md[i].Source?.OriginalString ?? $"__INLINE_{i}";
            if (seen.ContainsKey(src))
            {
                Console.WriteLine($"🧹 Removing duplicate merged ResourceDictionary: {src}");
                Log.Information("Removing duplicate merged ResourceDictionary: {Source}", src);
                md.RemoveAt(i);
            }
            else
            {
                seen[src] = 1;
            }
        }
    }

    /// <summary>
    /// Prevents the custom resource dictionary from overriding keys provided by Syncfusion Fluent Light/Dark themes.
    /// If collisions are detected, the custom entries are removed so that the official theme resource is used.
    /// This follows WPF merged dictionary key precedence avoidance (no undocumented Syncfusion APIs used).
    /// </summary>
    public void PreventCustomOverridesOfThemeKeys()
    {
        var md = Application.Current.Resources?.MergedDictionaries;
        if (md == null || md.Count == 0) return;

        ResourceDictionary custom = null;
        var themeKeySet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dict in md)
        {
            var src = dict.Source?.OriginalString ?? string.Empty;
            if (src.Contains("FluentLight.xaml", StringComparison.OrdinalIgnoreCase) ||
                src.Contains("FluentDark.xaml", StringComparison.OrdinalIgnoreCase))
            {
                // Collect theme keys (string keys only)
                foreach (var key in dict.Keys)
                {
                    if (key is string sk)
                    {
                        themeKeySet.Add(sk);
                    }
                }
            }
            else if (src.Contains("Wiley Widget/Resources/SyncfusionResources.xaml", StringComparison.OrdinalIgnoreCase))
            {
                custom = dict; // authoritative custom dictionary
            }
        }

        if (custom == null || themeKeySet.Count == 0) return; // nothing to do

        var toRemove = new List<string>();
        foreach (var key in custom.Keys)
        {
            if (key is string sk && themeKeySet.Contains(sk))
            {
                toRemove.Add(sk);
            }
        }

        if (toRemove.Count == 0) return;

        // Remove conflicting entries so that theme versions are used.
        int logged = 0;
        foreach (var k in toRemove)
        {
            custom.Remove(k);
            if (logged < 20)
            {
                Console.WriteLine($"🛡️ Removed custom resource overriding theme key: {k}");
                Log.Warning("Custom resource key {Key} removed to prevent override of Syncfusion Fluent theme", k);
                logged++;
            }
        }
        if (toRemove.Count > logged)
        {
            Console.WriteLine($"🛡️ {toRemove.Count - logged} additional overridden keys removed (suppressed log)");
        }
    }

    private void EnsureConverter(string key)
    {
        if (Application.Current.TryFindResource(key) != null) return;
        Application.Current.Resources[key] = new BooleanToVisibilityConverter();
        Console.WriteLine($"🛠️ Injected converter fallback: {key}");
        Log.Information("Injected converter fallback: {Key}", key);
    }

    private void EnsureTextStyle(string key, double fontSize, FontWeight weight, System.Windows.Media.Brush foreground = null, string basedOn = null)
    {
        if (Application.Current.TryFindResource(key) != null) return;
        var style = new Style(typeof(TextBlock));
        if (!string.IsNullOrEmpty(basedOn) && Application.Current.TryFindResource(basedOn) is Style baseStyle)
        {
            style.BasedOn = baseStyle;
        }
        style.Setters.Add(new Setter(TextBlock.FontSizeProperty, fontSize));
        if (weight != FontWeights.Normal)
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, weight));
        if (foreground != null)
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, foreground));
        Application.Current.Resources[key] = style;
        Console.WriteLine($"🛠️ Injected text style fallback: {key}");
        Log.Information("Injected text style fallback: {Key}", key);
    }

    private void EnsureValidationStyles()
    {
        if (Application.Current.TryFindResource("ValidationErrorStyle") == null)
        {
            var errStyle = new Style(typeof(TextBlock))
            {
                Setters =
                {
                    new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Red),
                    new Setter(TextBlock.FontSizeProperty, 12.0),
                    new Setter(TextBlock.MarginProperty, new Thickness(0,0,0,2))
                }
            };
            Application.Current.Resources["ValidationErrorStyle"] = errStyle;
            Console.WriteLine("🛠️ Injected validation fallback: ValidationErrorStyle");
            Log.Information("Injected validation fallback: ValidationErrorStyle");
        }

        if (Application.Current.TryFindResource("ValidatedTextBox") == null)
        {
            var tbStyle = new Style(typeof(TextBox));
            // Use default template - only supply Validation.ErrorTemplate via Setter (safer than full replacement)
            tbStyle.Setters.Add(new Setter(Validation.ErrorTemplateProperty, BuildSimpleErrorTemplate()));
            Application.Current.Resources["ValidatedTextBox"] = tbStyle;
            Console.WriteLine("🛠️ Injected validation fallback: ValidatedTextBox");
            Log.Information("Injected validation fallback: ValidatedTextBox");
        }
    }

    private ControlTemplate BuildSimpleErrorTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(DockPanel));
        factory.SetValue(DockPanel.LastChildFillProperty, true);
        var icon = new FrameworkElementFactory(typeof(TextBlock));
        icon.SetValue(TextBlock.TextProperty, "!");
        icon.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Red);
        icon.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        factory.AppendChild(icon);
        var placeholder = new FrameworkElementFactory(typeof(AdornedElementPlaceholder));
        factory.AppendChild(placeholder);
        var template = new ControlTemplate(typeof(Control));
        template.VisualTree = factory;
        return template;
    }
}
