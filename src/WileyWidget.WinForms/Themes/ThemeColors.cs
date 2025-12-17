using System.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Themes;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Themes
{
    /// <summary>
    /// Provides access to Syncfusion theme colors and brush resources.
    /// Acts as a thin wrapper around SfSkinManager theme system.
    /// CRITICAL: SfSkinManager is the SOLE PROPRIETOR of all theme and color decisions.
    /// This class provides orchestration methods AND theme-aware color accessors.
    /// Per Syncfusion documentation: SetVisualStyle on a form automatically cascades to all child controls.
    /// DO NOT manually set BackColor, ForeColor - use theme accessors only when styling elements like charts.
    /// </summary>
    internal static class ThemeColors
    {
        // Updated theme name for Syncfusion v31.2.15+ (Office2019Colorful for modern professional look)
        public const string DefaultTheme = "Office2019Colorful";

        /// <summary>
        /// DEPRECATED: Custom color properties removed. Use SfSkinManager themes exclusively.
        /// If you need semantic colors (success, error, warning), use Syncfusion's built-in theme colors.
        /// For special cases, query the theme system directly rather than bypassing it.
        /// </summary>
        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color PrimaryAccent => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color Success => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color Error => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color Warning => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color Background => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color CardBackground => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color TextPrimary => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color HeaderBackground => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color HeaderText => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color AlternatingRowBackground => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", true)]
        public static Color GaugeArc => throw new InvalidOperationException("Custom colors removed. Use SfSkinManager themes.");

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
                SfSkinManager.SetVisualStyle(form, theme);

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
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            }
            catch (Exception ex)
            {
                // Assembly may already be loaded (not an error)
                Serilog.Log.Debug(ex, "Office2019Theme assembly load skipped (may already be loaded)");
            }
        }

        // NOTE: The following methods were deprecated and removed.
        // SfSkinManager should be used exclusively for theme management.
        // For semantic status colors, use standard .NET colors (Color.Red, Color.Green, Color.Orange).
        // Charts and controls receive theming automatically through SfSkinManager cascade.

        /// <summary>
        /// DEPRECATED: Do not use. SfSkinManager handles all theme colors.
        /// For semantic colors, use standard Color.DodgerBlue, Color.Red, Color.Green, etc.
        /// </summary>
        [Obsolete("Use standard .NET colors (Color.DodgerBlue, Color.Red, etc.) instead. SfSkinManager handles theme colors.", error: true)]
        public static Color GetPrimaryColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SfSkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SfSkinManager handles theme colors.", error: true)]
        public static Color GetSecondaryColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SfSkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SfSkinManager handles theme colors.", error: true)]
        public static Color GetForeColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SfSkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SfSkinManager handles theme colors.", error: true)]
        public static Color GetBackColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. SfSkinManager handles all theme colors.
        /// </summary>
        [Obsolete("Use standard .NET colors instead. SfSkinManager handles theme colors.", error: true)]
        public static Color GetBorderColor() => throw new NotSupportedException("Use standard .NET colors instead");

        /// <summary>
        /// DEPRECATED: Do not use. For chart brushes, let SfSkinManager handle theming.
        /// </summary>
        [Obsolete("Let SfSkinManager handle chart theming through cascade.", error: true)]
        public static BrushInfo GetPrimaryBrush() => throw new NotSupportedException("Let SfSkinManager handle chart theming");

        /// <summary>
        /// DEPRECATED: Do not use. For chart brushes, let SfSkinManager handle theming.
        /// </summary>
        [Obsolete("Let SfSkinManager handle chart theming through cascade.", error: true)]
        public static BrushInfo GetSecondaryBrush() => throw new NotSupportedException("Let SfSkinManager handle chart theming");

        /// <summary>
        /// DEPRECATED: Do not use. For chart brushes, let SfSkinManager handle theming.
        /// </summary>
        [Obsolete("Let SfSkinManager handle chart theming through cascade.", error: true)]
        public static BrushInfo GetGradientBrush(Color color1, Color color2, GradientStyle style = GradientStyle.Vertical)
        {
            throw new NotSupportedException("Let SfSkinManager handle chart theming");
        }

        /// <summary>
        /// DEPRECATED: Custom grid styling removed. SfSkinManager themes SfDataGrid automatically.
        /// Theme cascade from parent form handles ALL styling - no manual color overrides needed.
        /// If you need custom grid appearance, customize the Office2019 theme itself, don't bypass it.
        /// </summary>
        [Obsolete("Custom grid styling removed. SfSkinManager themes grids automatically via cascade. Do not manually set colors.", true)]
        public static void ApplySfDataGridTheme(Syncfusion.WinForms.DataGrid.SfDataGrid grid)
        {
            throw new InvalidOperationException(
                "ApplySfDataGridTheme is deprecated. SfSkinManager automatically themes SfDataGrid via cascade. " +
                "Do not manually set BackColor, ForeColor, or other style properties - let SfSkinManager control everything.");
        }
    }
}
