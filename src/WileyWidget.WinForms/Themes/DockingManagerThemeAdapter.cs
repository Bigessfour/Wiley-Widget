using System;
using System.Collections.Generic;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Services;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Themes
{
    /// <summary>
    /// Bridges SfSkinManager theme system with DockingManager's legacy VisualStyle API.
    ///
    /// PROBLEM: DockingManager does not support SfSkinManager's ThemeName property or SetVisualStyle() method.
    /// It uses a legacy VisualStyle enum from 2000s-era Syncfusion architecture.
    ///
    /// SOLUTION: This adapter translates modern theme names to the closest VisualStyle enum match.
    /// DockingManager supports a limited set of VisualStyle values: Office2003, Office2007, Office2010, Metro.
    ///
    /// REFERENCE: docs/DOCKINGMANAGER_SFSKINMANAGER_INTEGRATION.md
    /// SYNCFUSION API: https://help.syncfusion.com/cr/windowsforms/Syncfusion.Windows.Forms.VisualStyle.html
    /// </summary>
    public class DockingManagerThemeAdapter
    {
        private readonly DockingManager _dockingManager;
        private readonly ILogger? _logger;

        /// <summary>
        /// Theme mapping: Modern theme names → VisualStyle enum.
        /// Maps SfSkinManager theme names to equivalent DockingManager VisualStyle values.
        /// Note: DockingManager uses legacy VisualStyle enum with limited options.
        /// </summary>
        private static readonly Dictionary<string, VisualStyle> ThemeMap = new Dictionary<string, VisualStyle>(StringComparer.OrdinalIgnoreCase)
        {
            // Office family - map to Office2010 as closest modern equivalent
            ["Office2019Colorful"] = VisualStyle.Office2010,
            ["Office2019Black"] = VisualStyle.Office2010,
            ["Office2019White"] = VisualStyle.Office2007,

            // Modern family - map to Metro (only modern option in DockingManager)
            ["ModernColorful"] = VisualStyle.Metro,
            ["ModernDark"] = VisualStyle.Metro,

            // Fluent family - map to Office styles as closest match
            ["FluentLight"] = VisualStyle.Office2007,
            ["FluentDark"] = VisualStyle.Office2010,

            // High Contrast family
            ["HighContrastBlack"] = VisualStyle.Office2010,
            ["HighContrastWhite"] = VisualStyle.Office2007,

            // Fallback handled in ApplyTheme() when unknown theme is provided
        };

        /// <summary>
        /// Creates a new adapter for the specified DockingManager instance.
        /// </summary>
        /// <param name="dockingManager">The DockingManager to theme. Must not be null.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public DockingManagerThemeAdapter(DockingManager dockingManager, ILogger? logger = null)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
            _logger = logger;
        }

        /// <summary>
        /// Applies a modern theme name to the DockingManager using the VisualStyle enum.
        /// Thread-safe. Safe to call multiple times. Logs warnings for unknown themes.
        ///
        /// Theme names are case-insensitive.
        /// </summary>
        /// <param name="themeName">Modern theme name (e.g., "Office2019Colorful"). If null or empty, uses ThemeColors.DefaultTheme.</param>
        public void ApplyTheme(string? themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                themeName = ThemeColors.DefaultTheme;
            }

            if (ThemeMap.TryGetValue(themeName, out var visualStyle))
            {
                try
                {
                    _dockingManager.VisualStyle = visualStyle;
                    _logger?.LogInformation("Applied VisualStyle {VisualStyle} for theme '{ThemeName}' to DockingManager",
                        visualStyle, themeName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to apply VisualStyle {VisualStyle} for theme '{ThemeName}'",
                        visualStyle, themeName);
                }
            }
            else
            {
                // Unknown theme - fall back to default
                _logger?.LogWarning("Unknown theme name '{ThemeName}', falling back to Office2010", themeName);
                _dockingManager.VisualStyle = VisualStyle.Office2010;
            }
        }

        /// <summary>
        /// Gets the DockingManager's current visual style as a string.
        /// Returns the closest matching modern theme name based on current VisualStyle.
        /// </summary>
        /// <returns>The theme name (e.g., "Office2019Colorful"), or ThemeColors.DefaultTheme if unknown.</returns>
        public string GetCurrentThemeName()
        {
                VisualStyle style;
                try
                {
                    style = _dockingManager.VisualStyle;
                }
                catch (Exception ex)
                {
                    // Some Syncfusion VisualStyle accesses can throw (renderer not initialized).
                    // Treat any error reading the current style as an "unknown" style and
                    // return the default theme name so callers remain stable in test hosts.
                    _logger?.LogDebug(ex, "Failed to read DockingManager.VisualStyle - treating as unknown style");
                    return ThemeColors.DefaultTheme;
                }

                // Search the theme map for a matching VisualStyle
                foreach (var kvp in ThemeMap)
                {
                    if (kvp.Value == style)
                    {
                        return kvp.Key;
                    }
                }

                // Unknown style - return default
                return ThemeColors.DefaultTheme;
        }

        /// <summary>
        /// Registers the DockingManager to automatically follow SfSkinManager theme changes.
        ///
        /// Recommended usage: Call this after creating the DockingManager but before calling
        /// SetEnableDocking() on child controls.
        ///
        /// Example:
        /// <code>
        /// var dockingManager = new DockingManager();
        /// dockingManager.HostControl = this;
        /// var adapter = new DockingManagerThemeAdapter(dockingManager, logger);
        /// adapter.RegisterThemeListener(_themeService);  // Pass your IThemeService
        /// // ... then add docking controls
        /// </code>
        /// </summary>
        /// <param name="themeService">Your application's IThemeService implementation.</param>
        public void RegisterThemeListener(IThemeService themeService)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));

            // Apply the current theme immediately
            string currentTheme = themeService.CurrentTheme;
            ApplyTheme(currentTheme);

            // Subscribe to theme changes if supported
            themeService.ThemeChanged += (sender, themeName) =>
            {
                ApplyTheme(themeName);
            };

            _logger?.LogInformation("DockingManager theme adapter registered with IThemeService");
        }
    }
}
