using System.Drawing;
using System.Linq;
using Microsoft.Extensions.Logging;
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
    ///
    /// THEME ENFORCEMENT POLICY (Moderate - per Syncfusion official demos):
    /// - SfSkinManager.SetVisualStyle(form, theme) is the primary theming mechanism
    /// - Theme automatically cascades from form to all child Syncfusion controls
    /// - ALLOWED: Form-level chrome properties (BackColor, CaptionBarColor on MetroForm/SfForm)
    /// - ALLOWED: Semantic status colors (Color.Red for errors, Color.Green for success)
    /// - DISCOURAGED: Manual BackColor/ForeColor on child controls (breaks theme cascade)
    ///
    /// Reference: https://help.syncfusion.com/windowsforms/skins/getting-started
    /// Official Demo: https://github.com/syncfusion/winforms-demos/tree/master/skinmanager/CS
    /// </summary>
    internal static class ThemeColors
    {
        // Theme name for Syncfusion v32.1.19+ (configurable via appsettings.json UI:Theme)
        // Available themes: "Office2019Colorful", "Office2019Black", "Office2019White", "Office2019DarkGray", "Office2019Dark"
        // Note: "Fluent" and "Material" themes require additional NuGet packages which are not currently installed.
        // To change theme: Edit appsettings.json UI:Theme property OR set via IThemeService at runtime
        public const string DefaultTheme = "Office2019Colorful";

        /// <summary>
        /// Valid theme names for validation.
        /// </summary>
        private static readonly string[] ValidThemes = new[]
        {
            "Office2019Colorful",
            "Office2019White",
            "Office2019Black",
            "Office2019DarkGray",
            "Office2019Dark",
            "Default"
        };

        /// <summary>
        /// Gets the currently active theme name, falling back to DefaultTheme when not set.
        /// </summary>
        public static string CurrentTheme => SfSkinManager.ApplicationVisualTheme ?? DefaultTheme;

        /// <summary>
        /// Validates a theme name against the list of supported themes.
        /// Returns the input theme if valid, otherwise returns DefaultTheme.
        /// </summary>
        public static string ValidateTheme(string themeName, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                logger?.LogWarning("Theme name is null or empty. Using default theme '{DefaultTheme}'.", DefaultTheme);
                return DefaultTheme;
            }

            if (System.Array.Exists(ValidThemes, t => string.Equals(t, themeName, System.StringComparison.OrdinalIgnoreCase)))
                return themeName;

            logger?.LogWarning("Invalid theme '{Theme}'. Valid themes: {ValidThemes}. Falling back to '{DefaultTheme}'.",
                themeName, string.Join(", ", ValidThemes), DefaultTheme);
            return DefaultTheme;
        }

        /// <summary>
        /// DEPRECATED: Custom color properties removed. Use SfSkinManager themes exclusively.
        /// EXCEPTIONS: Semantic colors (Success, Error, Warning) and form chrome properties are allowed.
        /// </summary>
        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color PrimaryAccent => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes or semantic colors.");

        /// <summary>
        /// Semantic color for success status indicators (ALLOWED exception to theme enforcement).
        /// Use this for status indicators, validation feedback, and other semantic UI elements.
        /// </summary>
        public static Color Success => Color.Green;

        /// <summary>
        /// Semantic color for error status indicators (ALLOWED exception to theme enforcement).
        /// Use this for error messages, validation failures, and critical warnings.
        /// </summary>
        public static Color Error => Color.Red;

        /// <summary>
        /// Semantic color for warning status indicators (ALLOWED exception to theme enforcement).
        /// Use this for warnings, cautions, and informational alerts.
        /// </summary>
        public static Color Warning => Color.Orange;

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color Background => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color CardBackground => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color TextPrimary => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color HeaderBackground => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color HeaderText => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color AlternatingRowBackground => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes.");

        [Obsolete("Custom colors compete with SfSkinManager. Use SfSkinManager.SetVisualStyle and let theme control colors.", false)]
        public static Color GaugeArc => throw new NotSupportedException("Custom colors removed. Use SfSkinManager themes.");
        /// <summary>
        /// Applies the specified theme to a form using Syncfusion's SfSkinManager.
        ///
        /// BEST PRACTICE (per Syncfusion official demos):
        /// 1. Call SetVisualStyle on the form - theme automatically cascades to all Syncfusion child controls
        /// 2. Individual child controls do NOT need SetVisualStyle called on them
        /// 3. Form chrome properties (BackColor, CaptionBarColor) MAY be set manually after theme application
        /// 4. Regular control properties (BackColor/ForeColor) should rely on theme cascade
        ///
        /// Reference: https://github.com/syncfusion/winforms-demos/tree/master/skinmanager/CS/Form1.cs
        /// </summary>
        /// <param name="form">The form to apply theming to.</param>
        /// <param name="themeName">Optional theme name override (defaults to Office2019Colorful)</param>
        public static void ApplyTheme(Form form, string? themeName = null)
        {
            if (form == null) return;

            var theme = ValidateTheme(themeName ?? DefaultTheme);

            try
            {
                // Ensure Office2019Theme assembly is loaded (idempotent)
                EnsureThemeAssemblyLoaded();

                // Core theming call - applies theme to form and cascades to all Syncfusion child controls
                // Per Syncfusion docs: SetVisualStyle on form is sufficient, theme cascades to all children
                SfSkinManager.SetVisualStyle(form, theme);

                try
                {
                    Serilog.Log.Debug("SfSkinManager applied '{Theme}' to form '{FormName}' (auto-cascade to all children)",
                        theme, form.Name);
                }
                catch (Exception logEx)
                {
                    // Suppress logging errors - don't fail theme application
                    System.Diagnostics.Debug.WriteLine($"ThemeColors.ApplyTheme: Logging failed: {logEx.Message}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Serilog.Log.Error(ex, "SfSkinManager failed to apply {Theme} theme to form {FormName}",
                        theme, form.Name);
                }
                catch (Exception logEx)
                {
                    // Suppress logging errors
                    System.Diagnostics.Debug.WriteLine($"ThemeColors.ApplyTheme: Error logging failed: {logEx.Message}");
                }

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

        private static readonly object ThemeLoadLock = new();
        private static volatile bool _themeAssemblyLoaded;

        /// <summary>
        /// Ensures the Office2019Theme assembly is loaded into SfSkinManager.
        /// Thread-safe and idempotent for multi-threaded initialization paths.
        /// </summary>
        internal static void EnsureThemeAssemblyLoaded(ILogger? logger = null)
        {
            if (_themeAssemblyLoaded)
            {
                return;
            }

            var isTestEnvironment = string.Equals(
                Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (IsOffice2019ThemeAssemblyLoaded())
            {
                _themeAssemblyLoaded = true;
                return;
            }

            if (isTestEnvironment)
            {
                logger?.LogDebug("Theme assembly not loaded yet in test environment; skipping LoadAssembly to avoid theme provider mutation.");
                return;
            }

            lock (ThemeLoadLock)
            {
                if (_themeAssemblyLoaded)
                {
                    return;
                }

                if (IsOffice2019ThemeAssemblyLoaded())
                {
                    _themeAssemblyLoaded = true;
                    return;
                }

                try
                {
                    // Load theme assembly into SfSkinManager (modern API, v6.0+)
                    // Per Syncfusion documentation: SfSkinManager is the recommended API for Windows Forms v32+
                    // Legacy SkinManager is not needed for modern Syncfusion controls
                    SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                    _themeAssemblyLoaded = true;
                    logger?.LogDebug("Office2019Theme assembly loaded into SfSkinManager");
                }
                catch (Exception ex)
                {
                    // Assembly may already be loaded (not an error)
                    try
                    {
                        if (IsOffice2019ThemeAssemblyLoaded())
                        {
                            _themeAssemblyLoaded = true;
                        }
                        Serilog.Log.Debug(ex, "Office2019Theme assembly load skipped (may already be loaded)");
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine($"ThemeColors.EnsureThemeAssemblyLoaded: Logging failed: {ex.Message}");
                    }
                }
            }
        }

        private static bool IsOffice2019ThemeAssemblyLoaded() =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a == typeof(Office2019Theme).Assembly);

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
        public static void ApplySfDataGridTheme(SfDataGrid grid)
        {
            throw new InvalidOperationException(
                "ApplySfDataGridTheme is deprecated. SfSkinManager automatically themes SfDataGrid via cascade. " +
                "Do not manually set BackColor, ForeColor, or other style properties - let SfSkinManager control everything.");
        }
    }
}
