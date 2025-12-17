using System;
using System.Windows.Forms;
using System.Drawing;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Theming
{
    /// <summary>
    /// Lightweight theme manager that defers to SkinManager (ThemeColors) for real theming.
    /// </summary>
    public static class ThemeManager
    {
        public const string VisualTheme = ThemeColors.DefaultTheme;

        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Office2019Dark;

        public static ThemePalette Colors { get; } = ThemePalette.CreateDefault();

        public static event EventHandler<AppTheme>? ThemeChanged;

        public static void ApplyThemeToControl(Control control)
        {
            if (control == null) return;

            if (control is Form form)
            {
                // Apply theme to the form; SkinManager cascades to child Syncfusion controls.
                ThemeColors.ApplyTheme(form);
                return;
            }

            try
            {
                SfSkinManager.SetVisualStyle(control, ThemeColors.DefaultTheme);
            }
            catch
            {
                // Best-effort only; do not block if a control does not support SkinManager.
            }
        }

        public static void ApplyTheme(Control control)
        {
            ApplyThemeToControl(control);
        }

        public static void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            ThemeChanged?.Invoke(null, theme);
        }
    }

    /// <summary>
    /// Minimal theme palette used by WinForms controls for semantic colors.
    /// </summary>
    public readonly record struct ThemePalette(Color Accent, Color Surface, Color TextPrimary)
    {
        public static ThemePalette CreateDefault() => new(Color.DodgerBlue, SystemColors.Control, Color.Black);
    }
}
