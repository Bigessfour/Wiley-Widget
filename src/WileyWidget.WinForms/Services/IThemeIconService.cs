using System.Drawing;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Minimal icon service used by UI components to fetch themed icons.
    /// </summary>
    public interface IThemeIconService
    {
        /// <summary>
        /// Gets a value indicating whether this service has been disposed.
        /// </summary>
        bool IsDisposed { get; }

        Image? GetIcon(string name, AppTheme theme, int size, bool disabled = false);
        Task<Image?> GetIconAsync(string name, AppTheme theme, int size, bool disabled = false);
    }
}
