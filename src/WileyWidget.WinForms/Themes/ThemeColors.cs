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
        // Updated theme name for Syncfusion v31.2.15+ (Office2019Colorful as required)
        public const string DefaultTheme = "Office2019Colorful";

        /// <summary>
        /// Gets the primary accent color from the current theme.
        /// Fallback: Blue accent for Office2019Colorful theme.
        /// </summary>
        public static Color PrimaryAccent => GetThemeColor("PrimaryAccent", Color.FromArgb(0, 120, 215));

        /// <summary>
        /// Gets the success/positive color from the current theme.
        /// Fallback: Green for Office2019Colorful theme.
        /// </summary>
        public static Color Success => GetThemeColor("Success", Color.FromArgb(16, 137, 62));

        /// <summary>
        /// Gets the error/negative color from the current theme.
        /// Fallback: Red for Office2019Colorful theme.
        /// </summary>
        public static Color Error => GetThemeColor("Error", Color.FromArgb(232, 17, 35));

        /// <summary>
        /// Gets the warning color from the current theme.
        /// Fallback: Orange for Office2019Colorful theme.
        /// </summary>
        public static Color Warning => GetThemeColor("Warning", Color.FromArgb(255, 185, 0));

        /// <summary>
        /// Gets the background color from the current theme.
        /// Fallback: Light gray for Office2019Colorful theme.
        /// </summary>
        public static Color Background => GetThemeColor("Background", Color.FromArgb(240, 240, 240));

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
        public static Color AlternatingRowBackground => GetThemeColor("AlternatingRow", Color.FromArgb(245, 245, 245));

        /// <summary>
        /// Gets the gauge arc background color from the current theme.
        /// </summary>
        public static Color GaugeArc => GetThemeColor("GaugeArc", Color.LightGray);

        /// <summary>
        /// Retrieves a color from the active theme with fallback support.
        /// </summary>
        /// <param name="colorName">The semantic name of the color.</param>
        /// <param name="fallback">Fallback color if theme color cannot be retrieved.</param>
        /// <returns>Theme-aware color or fallback.</returns>
        private static Color GetThemeColor(string colorName, Color fallback)
        {
            // TODO: Once Syncfusion theme assembly is properly loaded,
            // retrieve colors dynamically from SkinManager.
            // For now, return semantic fallback colors that match Office2019Colorful.
            return fallback;
        }

        /// <summary>
        /// Applies the default theme globally to a form and all its child Syncfusion controls.
        /// This is the ONLY approved method for applying themes in Wiley Widget.
        /// All child controls (SfDataGrid, RadialGauge, ChartControl, etc.) automatically
        /// inherit the theme from this single call - no individual control theme setting is allowed.
        /// </summary>
        /// <param name="form">The form to apply theming to.</param>
        public static void ApplyTheme(Form form)
        {
            if (form == null) return;

            try
            {
                // Ensure Office2019Theme assembly is loaded
                EnsureThemeAssemblyLoaded();

                // Apply global theme - this cascades to all Syncfusion controls
                SfSkinManager.SetVisualStyle(form, DefaultTheme);
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
        /// Applies consistent theme styling to an SfDataGrid control.
        /// </summary>
        /// <param name="grid">The SfDataGrid to theme.</param>
        public static void ApplySfDataGridTheme(Syncfusion.WinForms.DataGrid.SfDataGrid grid)
        {
            if (grid == null) return;

            try
            {
                // Apply SfSkinManager theme
                SfSkinManager.SetVisualStyle(grid, DefaultTheme);

                // Apply custom style properties that SfSkinManager doesn't handle
                grid.Style.HeaderStyle.BackColor = HeaderBackground;
                grid.Style.HeaderStyle.TextColor = Color.White;
                grid.Style.HeaderStyle.Font = new Syncfusion.WinForms.DataGrid.Styles.GridFontInfo(new Font("Segoe UI", 9F, FontStyle.Bold));
            }
            catch (Exception ex)
            {
                // Log theme application error but don't fail
                Serilog.Log.Warning(ex, "SfDataGrid theme application failed for grid {GridName}", grid.Name);
            }
        }
    }
}
