using System.Drawing;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Placeholder icon service that returns null icons until real assets are wired.
    /// </summary>
    public sealed class ThemeIconService : IThemeIconService
    {
        public Image? GetIcon(string name, AppTheme theme, int size)
        {
            _ = name;
            _ = theme;
            _ = size;
            return null;
        }
    }
}
