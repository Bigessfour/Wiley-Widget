using System.Drawing;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services;

public interface IThemeIconService
{
    /// <summary>
    /// Get a theme-aware icon image for a semantic name (e.g. "add", "edit").
    /// Returns a System.Drawing.Image sized to the provided pixel size. Returns null if not found.
    /// </summary>
    Image? GetIcon(string name, AppTheme theme, int size = 24);

    /// <summary>
    /// Preload a list of icons to warm caches.
    /// </summary>
    void Preload(IEnumerable<string> names, AppTheme theme, int size = 24);
}
