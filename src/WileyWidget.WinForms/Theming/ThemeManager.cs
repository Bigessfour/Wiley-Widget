using System;
using System.Windows.Forms;
using System.Drawing;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Theming
{
    /// <summary>
    /// Lightweight theme manager placeholder to satisfy UI dependencies.
    /// </summary>
    public static class ThemeManager
    {
        public const string VisualTheme = ThemeColors.DefaultTheme;

        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

        public static ThemePalette Colors { get; } = ThemePalette.CreateDefault();

        public static event EventHandler<AppTheme>? ThemeChanged;

        public static void ApplyThemeToControl(Control control)
        {
            // Placeholder: hook real theming here
            _ = control;
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
        public static ThemePalette CreateDefault() => new(ThemeColors.PrimaryAccent, ThemeColors.Background, Color.Black);
    }
}
