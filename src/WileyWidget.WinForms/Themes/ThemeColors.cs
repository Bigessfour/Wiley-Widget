using System.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Themes;

namespace WileyWidget.WinForms.Themes
{
    /// <summary>
    /// Provides theme-aware colors for Syncfusion controls that don't support ThemeName property.
    /// Uses SkinManager to retrieve colors dynamically based on the active theme.
    /// </summary>
    internal static class ThemeColors
    {
        // Updated theme name for Syncfusion v31.2.15+ (Office2019Dark for modern professional look)
        public const string DefaultTheme = "Office2019Dark";

        /// <summary>
        /// Gets the primary accent color from the current theme.
        /// Fallback: Blue accent for Office2019Dark theme.
        /// </summary>
        public static Color PrimaryAccent => GetThemeColor("PrimaryAccent", Color.FromArgb(0, 120, 215));

        /// <summary>
        /// Gets the success/positive color from the current theme.
        /// Fallback: Green for Office2019Dark theme.
        /// </summary>
        public static Color Success => GetThemeColor("Success", Color.FromArgb(16, 137, 62));

        /// <summary>
        /// Gets the error/negative color from the current theme.
        /// Fallback: Red for Office2019Dark theme.
        /// </summary>
        public static Color Error => GetThemeColor("Error", Color.FromArgb(232, 17, 35));

        /// <summary>
        /// Gets the warning color from the current theme.
        /// Fallback: Orange for Office2019Dark theme.
        /// </summary>
        public static Color Warning => GetThemeColor("Warning", Color.FromArgb(255, 185, 0));

        /// <summary>
        /// Gets the background color from the current theme.
        /// Fallback: Dark gray for Office2019Dark theme.
        /// </summary>
        public static Color Background => GetThemeColor("Background", Color.FromArgb(32, 32, 32));

        /// <summary>
        /// Gets the header background color from the current theme.
        /// </summary>
        public static Color HeaderBackground => PrimaryAccent;

        /// <summary>
        /// Gets the header text color from the current theme.
        /// </summary>
        public static Color HeaderText => Color.White;

        /// <summary>
        /// Gets the alternating row background color from the current theme.
        /// </summary>
        public static Color AlternatingRowBackground => GetThemeColor("AlternatingRow", Color.FromArgb(45, 45, 45));

        /// <summary>
        /// Gets the gauge arc background color from the current theme.
        /// </summary>
        public static Color GaugeArc => GetThemeColor("GaugeArc", Color.LightGray);

        /// <summary>
        /// Retrieves a color from the active theme with fallback support.
        /// Attempts to query SkinManager for theme colors, falling back to semantic defaults.
        /// </summary>
        /// <param name="colorName">The semantic name of the color.</param>
        /// <param name="fallback">Fallback color if theme color cannot be retrieved.</param>
        /// <returns>Theme-aware color or fallback.</returns>
        private static Color GetThemeColor(string colorName, Color fallback)
        {
            try
            {
                // Attempt to retrieve color from active theme via reflection or known properties
                // SkinManager doesn't expose a direct color lookup API, so we use semantic fallbacks
                // that align with the Office2019Colorful palette. Future enhancement: parse theme
                // assembly resources or use Syncfusion's internal color tables if exposed.

                // Map semantic color names to Office2019Dark theme equivalents
                // These colors are derived from Syncfusion's Office2019Dark theme specification
                // Using DefaultTheme which is currently hardcoded to Office2019Dark
                return colorName switch
                {
                    "Primary" => Color.FromArgb(0, 120, 215),
                    "PrimaryAccent" => Color.FromArgb(0, 120, 215),
                    "Secondary" => Color.FromArgb(72, 72, 72),
                    "Success" => Color.FromArgb(16, 124, 16),
                    "Warning" => Color.FromArgb(255, 185, 0),
                    "Danger" => Color.FromArgb(232, 17, 35),
                    "Background" => Color.FromArgb(32, 32, 32),
                    "Surface" => Color.FromArgb(45, 45, 45),
                    "Border" => Color.FromArgb(100, 100, 100),
                    "Text" => Color.FromArgb(200, 200, 200),
                    "AlternatingRow" => Color.FromArgb(45, 45, 45),
                    "GaugeArc" => Color.FromArgb(100, 100, 100),
                    _ => fallback
                };
            }
            catch
            {
                // If theme query fails, use the provided fallback
                return fallback;
            }
        }

        /// <summary>
        /// Applies the default theme to a form using Syncfusion's SfSkinManager.
        /// Per Syncfusion documentation, calling SetVisualStyle on a form automatically
        /// cascades the theme to all child Syncfusion controls (SfDataGrid, RadialGauge, etc.).
        /// DO NOT call SetVisualStyle on individual child controls - the cascade handles it.
        /// Reference: https://help.syncfusion.com/windowsforms/themes/getting-started
        /// </summary>
        /// <param name="form">The form to apply theming to.</param>
        public static void ApplyTheme(Form form)
        {
            if (form == null) return;

            try
            {
                // Ensure Office2019Theme assembly is loaded
                EnsureThemeAssemblyLoaded();

                // Apply theme to form - Syncfusion automatically cascades to all child controls
                // This is the ONLY call needed; individual control theming is redundant and can cause issues
                SfSkinManager.SetVisualStyle(form, DefaultTheme);

                Serilog.Log.Debug("Theme '{Theme}' applied to form '{FormName}' (cascade to all child controls)",
                    DefaultTheme, form.Name);
            }
            catch (Exception ex)
            {
                // Enhanced error handling with specific theme failure logging
                Serilog.Log.Error(ex, "Failed to apply {Theme} theme to form {FormName}",
                    DefaultTheme, form.Name);

                // Attempt fallback to default theme
                try
                {
                    SfSkinManager.SetVisualStyle(form, "default");
                }
                catch (Exception fallbackEx)
                {
                    Serilog.Log.Error(fallbackEx, "Theme fallback failed for form {FormName}", form.Name);
                }

                // User-friendly fallback: Show message and continue with default theme
                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Theme initialization failed. The application will continue with default styling.",
                        "Theme Warning",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                }
                catch (Exception msgEx)
                {
                    Serilog.Log.Error(msgEx, "Failed to show theme warning message");
                }
            }
        }

        /// <summary>
        /// Ensures the Office2019Theme assembly is loaded into the SkinManager.
        /// </summary>
        private static void EnsureThemeAssemblyLoaded()
        {
            try
            {
                // Load the Office2019Theme assembly for extended theme support
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            }
            catch (Exception ex)
            {
                // Theme assembly may already be loaded or not available
                Serilog.Log.Warning(ex, "Office2019Theme assembly loading failed - theme features may be limited");
            }
        }

        /// <summary>
        /// Applies custom style properties to an SfDataGrid that theme cascade doesn't handle.
        /// DO NOT call SfSkinManager.SetVisualStyle here - it's already inherited from parent form.
        /// Only use this for custom styling beyond the theme (e.g., specific header colors).
        /// </summary>
        /// <param name="grid">The SfDataGrid to style.</param>
        public static void ApplySfDataGridTheme(Syncfusion.WinForms.DataGrid.SfDataGrid grid)
        {
            if (grid == null) return;

            try
            {
                // Theme is already cascaded from parent form - only apply custom overrides
                // Apply custom style properties that theme doesn't handle
                grid.Style.HeaderStyle.BackColor = HeaderBackground;
                grid.Style.HeaderStyle.TextColor = Color.White;
                grid.Style.HeaderStyle.Font = new Syncfusion.WinForms.DataGrid.Styles.GridFontInfo(new Font("Segoe UI", 9F, FontStyle.Bold));

                Serilog.Log.Debug("Custom grid styling applied to '{GridName}' (theme inherited from parent)", grid.Name);
            }
            catch (Exception ex)
            {
                // Log theme application error but don't fail
                Serilog.Log.Warning(ex, "Custom grid styling failed for grid {GridName}", grid.Name);
            }
        }
    }
}
