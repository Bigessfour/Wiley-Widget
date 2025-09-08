using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls; // for Border/TextBlock styles
using System.Xml.Linq;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Configuration;
using WileyWidget.UI.Theming;

namespace WileyWidget.Services
{
    /// <summary>
    /// Central theme manager around Syncfusion SfSkinManager (v30.x): handles license registration,
    /// theme normalization and persistence, dynamic .resx merging, required style injection, and
    /// fallback to FluentDark on failure.
    /// </summary>
    public interface IThemeManager
    {
        string CurrentTheme { get; }
        IReadOnlyList<string> AvailableThemes { get; }
        event EventHandler<string> ThemeChanged;
        bool Initialize();
        System.Threading.Tasks.Task<bool> ApplyThemeAsync(string themeName, bool persist = true);
        void LoadResxResources(string directory = "Resources", CultureInfo culture = null);
        void EnsureCustomStyles();
    }

    public sealed class ThemeManager : IThemeManager
    {
        private readonly SettingsService _settings;
        private readonly HashSet<string> _loadedResx = new(StringComparer.OrdinalIgnoreCase);
        private bool _initialized;
        private const string DefaultTheme = "FluentDark"; // Syncfusion documented theme
        private static readonly object _sync = new();

        public event EventHandler<string> ThemeChanged;

        public ThemeManager(ISettingsService settings)
        {
            _settings = (SettingsService)settings;
        }

    public string CurrentTheme => WileyWidget.UI.Theming.ThemeService.CurrentTheme;

    public IReadOnlyList<string> AvailableThemes => WileyWidget.UI.Theming.ThemeService.GetSupportedThemes();

        public bool Initialize()
        {
            if (_initialized) return true;
            lock (_sync)
            {
                if (_initialized) return true;

                WileyWidget.UI.Theming.ThemeService.Initialize();

                // Apply persisted or default theme
                var startTheme = WileyWidget.UI.Theming.ThemeService.NormalizeTheme(_settings.Current?.Theme ?? DefaultTheme);
                SafeApply(startTheme, persist: false);
                _initialized = true;
                return true;
            }
        }

        public async System.Threading.Tasks.Task<bool> ApplyThemeAsync(string themeName, bool persist = true)
        {
            return await System.Threading.Tasks.Task.Run(() => SafeApply(themeName, persist));
        }

        private bool SafeApply(string themeName, bool persist)
        {
            var normalized = WileyWidget.UI.Theming.ThemeService.NormalizeTheme(themeName);
            try
            {
                Log.Information("Applying Syncfusion theme {Theme}", normalized);
                SfSkinManager.ApplicationTheme = new Theme(normalized);
                WileyWidget.UI.Theming.ThemeService.SetCurrentTheme(normalized);
                if (persist)
                {
                    _settings.Current.Theme = normalized;
                    _settings.Save();
                }
                ThemeChanged?.Invoke(this, normalized);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Theme apply failed for {Theme} – falling back to {Fallback}", normalized, DefaultTheme);
                try
                {
                    SfSkinManager.ApplicationTheme = new Theme(DefaultTheme);
                    WileyWidget.UI.Theming.ThemeService.SetCurrentTheme(DefaultTheme);
                    if (persist)
                    {
                        _settings.Current.Theme = DefaultTheme;
                        _settings.Save();
                    }
                    ThemeChanged?.Invoke(this, DefaultTheme);
                }
                catch (Exception inner)
                {
                    Log.Error(inner, "Fallback theme application failed – continuing with system defaults");
                }
                return false;
            }
        }

        public void LoadResxResources(string directory = "Resources", CultureInfo culture = null)
        {
            try
            {
                var baseDir = Path.IsPathRooted(directory) ? directory : Path.Combine(AppContext.BaseDirectory, directory);
                if (!Directory.Exists(baseDir)) return;
                culture ??= CultureInfo.CurrentUICulture;

                foreach (var resx in Directory.EnumerateFiles(baseDir, "*.resx", SearchOption.TopDirectoryOnly))
                {
                    if (_loadedResx.Contains(resx)) continue;
                    try
                    {
                        var rd = new ResourceDictionary();
                        var doc = XDocument.Load(resx);
                        foreach (var data in doc.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
                        {
                            var nameAttr = data.Attribute("name")?.Value;
                            var valueEl = data.Element("value")?.Value;
                            if (!string.IsNullOrWhiteSpace(nameAttr) && valueEl != null)
                            {
                                // Simple string value insertion; can extend for types if needed
                                rd[nameAttr] = valueEl;
                            }
                        }
                        if (rd.Count > 0)
                        {
                            Application.Current.Resources.MergedDictionaries.Add(rd);
                            _loadedResx.Add(resx);
                            Log.Information("Merged resx resource dictionary {File} ({Count} entries)", Path.GetFileName(resx), rd.Count);
                        }
                    }
                    catch (Exception exFile)
                    {
                        Log.Warning(exFile, "Failed to merge resx {Resx}", resx);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Resx merge operation failed");
            }
        }

        public void EnsureCustomStyles()
        {
            try
            {
                AddStyleIfMissing("DashboardCard", BuildDashboardCardStyle());
                AddStyleIfMissing("CardTitle", BuildCardTitleStyle());
                AddStyleIfMissing("CardValue", BuildCardValueStyle());
                // Warning styles referenced in XAML
                AddBrushIfMissing("WarningBackground", Color.FromArgb(32, 255, 165, 0));
                AddBrushIfMissing("WarningBorderBrush", Color.FromArgb(255, 255, 165, 0));
                AddBrushIfMissing("WarningTextForeground", Color.FromArgb(255, 255, 140, 0));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed ensuring custom styles");
            }
        }

        private void AddStyleIfMissing(string key, Style style)
        {
            if (style == null) return;
            if (!Application.Current.Resources.Contains(key))
            {
                style.Seal();
                Application.Current.Resources[key] = style;
                Log.Information("Injected missing style {Key}", key);
            }
        }

        private void AddBrushIfMissing(string key, Color color)
        {
            if (!Application.Current.Resources.Contains(key))
            {
                Application.Current.Resources[key] = new SolidColorBrush(color);
                Log.Information("Injected missing brush {Key}", key);
            }
        }

        // Basic style definitions – intentionally minimal (can be replaced by dedicated XAML dictionaries later)
        private Style BuildDashboardCardStyle()
        {
            var style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(12)));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(8)));
            style.Setters.Add(new Setter(Border.MarginProperty, new Thickness(6)));
            style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, GetBrushSafe("CardBorderBrush", Colors.DimGray)));
            style.Setters.Add(new Setter(Border.BackgroundProperty, GetBrushSafe("CardBackgroundBrush", Color.FromArgb(32, 255, 255, 255))));
            return style;
        }

        private Style BuildCardTitleStyle()
        {
            var style = new Style(typeof(System.Windows.Controls.TextBlock));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, 14.0));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.MarginProperty, new Thickness(0,0,0,4)));
            return style;
        }

        private Style BuildCardValueStyle()
        {
            var style = new Style(typeof(System.Windows.Controls.TextBlock));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, 18.0));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.MarginProperty, new Thickness(0,2,0,0)));
            return style;
        }

        private Brush GetBrushSafe(string key, Color fallback)
            => Application.Current.Resources.Contains(key)
                ? Application.Current.Resources[key] as Brush ?? new SolidColorBrush(fallback)
                : new SolidColorBrush(fallback);
    }
}
