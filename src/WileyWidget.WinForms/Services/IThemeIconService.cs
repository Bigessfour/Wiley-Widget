using System.Drawing;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Minimal icon service used by UI components to fetch themed icons.
    /// </summary>
    public interface IThemeIconService
    {
        Image? GetIcon(string name, AppTheme theme, int size);
    }
}
