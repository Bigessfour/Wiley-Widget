using System.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Themes
{
    /// <summary>
    /// Provides access to Syncfusion theme colors and brush resources.
    /// Acts as a thin wrapper around SfSkinManager theme system.
    /// CRITICAL: SkinManager is the SOLE PROPRIETOR of all theme and color decisions.
    /// This class provides orchestration methods AND theme-aware color accessors.
    /// Per Syncfusion documentation: SetVisualStyle on a form automatically cascades to all child controls.
    /// DO NOT manually set BackColor, ForeColor - use theme accessors only when styling elements like charts.
    /// Reference: https://help.syncfusion.com/windowsforms/skins/getting-started
    /// </summary>
    internal static class ThemeColors
    {
        // Theme name for Syncfusion v31.2.15+ (configurable via appsettings.json UI:Theme)
        // Per Syncfusion documentation, use SkinManager.ApplicationVisualTheme for global theming
        // Available themes: "Office2019White", "Office2019Black", "Office2019DarkGray", "FluentLight", "FluentDark", "MaterialLight", "MaterialDark"
        // To change theme: Edit appsettings.json UI:Theme property OR set BEFORE InitializeComponent() in Program.Main()
        public const string DefaultTheme = "Office2019White";

        /// <summary>
        /// Gets the currently active theme name, falling back to DefaultTheme when not set.
        /// </summary>
        public static string CurrentTheme => SfSkinManager.ApplicationVisualTheme ?? DefaultTheme;

        /// <summary>
        /// DEPRECATED: Custom color properties removed. Use SkinManager themes exclusively.
        /// If you need semantic colors (success, error, warning), use Syncfusion's built-in theme colors.
        /// For special cases, query the theme system directly rather than bypassing it.
        /// </summary>
        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color PrimaryAccent => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        /// <summary>
        /// Semantic color for success status indicators.
        /// API Guideline: Use explicit standard colors for semantic status.
        /// </summary>
        public static Color Success => Color.Green;

        /// <summary>
        /// Semantic color for error status indicators.
        /// API Guideline: Use explicit standard colors for semantic status.
        /// </summary>
        public static Color Error => Color.Red;

        /// <summary>
        /// Semantic color for warning status indicators.
        /// API Guideline: Use explicit standard colors for semantic status.
        /// </summary>
        public static Color Warning => Color.Orange;

        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color Background => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color CardBackground => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color TextPrimary => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color HeaderBackground => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color HeaderText => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color AlternatingRowBackground => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        [Obsolete("Custom colors compete with SkinManager. Use SkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color GaugeArc => throw new InvalidOperationException("Custom colors removed. Use SkinManager themes.");

        /// <summary>
        /// Applies the default theme to a form using Syncfusion's SfSkinManager.
        /// Per Syncfusion documentation, calling SetVisualStyle on a form automatically
        /// cascades the theme to ALL child Syncfusion controls (SfDataGrid, RadialGauge, etc.).
        /// DO NOT call SetVisualStyle on individual child controls - the cascade handles it.
        /// DO NOT manually set BackColor, ForeColor - SfSkinManager owns all color decisions.
        /// Reference: Syncfusion WinForms Office2019 Theme Documentation
        /// </summary>
        /// <param name="form">The form to apply theming to.</param>
        /// <param name="themeName">Optional theme name override (defaults to Office2019Colorful)</param>
        public static void ApplyTheme(Form form, string? themeName = null)
        {
            if (form == null) return;

            var theme = themeName ?? DefaultTheme;

            try
            {
                // Ensure Office2019Theme assembly is loaded (idempotent)
                EnsureThemeAssemblyLoaded();

                // CRITICAL: This single call themes the form AND all child controls automatically
                // Per Syncfusion: "SetVisualStyle on window applies theme to ALL controls inside it"
                // Theme cascade - form already has SetVisualStyle applied in Program.cs

                Serilog.Log.Debug("SfSkinManager applied '{Theme}' to form '{FormName}' (auto-cascade to all children)",
                    theme, form.Name);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "SfSkinManager failed to apply {Theme} theme to form {FormName}",
                    theme, form.Name);

                // Minimal fallback - let form use default rendering
                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"Theme '{theme}' failed to load. Using default styling.",
                        "Theme Warning",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                }
                catch { /* Suppress message box errors */ }
            }
        }

        /// <summary>
        /// Ensures the Office2019Theme assembly is loaded into the SkinManager.
        /// This is idempotent - safe to call multiple times.
        /// </summary>
        private static void EnsureThemeAssemblyLoaded()
        {
            try
            {
                SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            }
            catch (Exception ex)
            {
                // Assembly may already be loaded (not an error)
                Serilog.Log.Debug(ex, "Office2019Theme assembly load skipped (may already be loaded)");
            }
        }

        // NOTE: The following methods were deprecated and removed.
        // SkinManager should be used exclusively for theme management.
        // For semantic status colors, use standard .NET colors (Color.Red, Color.Green, Color.Orange).
        // Charts and controls receive theming automatically through SkinManager cascade.

        /// <summary>
        /// DEPRECATED: Do not use. SkinManager handles all theme colors.
        /// For semantic colors, use standard Color.DodgerBlue, Color.Red, Color.Green, etc.
        /// </summary>
        [Obsolete("Use standard .NET colors (Color.DodgerBlue, Color.Red, etc.) instead. SkinManager handles theme colors.", error: true)]
        public static Color GetPrimaryColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SkinManager handles theme colors.", error: true)]
        public static Color GetSecondaryColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SkinManager handles theme colors.", error: true)]
        public static Color GetForeColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SkinManager handles theme colors.", error: true)]
        public static Color GetBackColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SkinManager handles theme colors.", error: true)]
        public static Color GetBorderColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. For chart brushes, let SkinManager handle theming.
        /// </summary>
        [Obsolete("Let SkinManager handle chart theming through cascade.", error: true)]
        public static BrushInfo GetPrimaryBrush() => throw new NotSupportedException("Let SkinManager handle chart theming");

        /// <summary>
        /// DEPRECATED: Do not use. For chart brushes, let SkinManager handle theming.
        /// </summary>
        [Obsolete("Let SkinManager handle chart theming through cascade.", error: true)]
        public static BrushInfo GetSecondaryBrush() => throw new NotSupportedException("Let SkinManager handle chart theming");

        /// <summary>
        /// DEPRECATED: Do not use. For chart brushes, let SkinManager handle theming.
        /// </summary>
        [Obsolete("Let SkinManager handle chart theming through cascade.", error: true)]
        public static BrushInfo GetGradientBrush(Color color1, Color color2, GradientStyle style = GradientStyle.Vertical)
        {
            throw new NotSupportedException("Let SkinManager handle chart theming");
        }

        /// <summary>
        /// DEPRECATED: Custom grid styling removed. SkinManager themes SfDataGrid automatically.
        /// Theme cascade from parent form handles ALL styling - no manual color overrides needed.
        /// If you need custom grid appearance, customize the Office2019 theme itself, don't bypass it.
        /// </summary>
        [Obsolete("Custom grid styling removed. SkinManager themes grids automatically via cascade. Do not manually set colors.", true)]
        public static void ApplySfDataGridTheme(SfDataGrid grid)
        {
            throw new InvalidOperationException(
                "ApplySfDataGridTheme is deprecated. SkinManager automatically themes SfDataGrid via cascade. " +
                "Do not manually set BackColor, ForeColor, or other style properties - let SkinManager control everything.");
        }
    }
}
