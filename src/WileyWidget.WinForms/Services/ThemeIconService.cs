using System.Drawing;
using System.Windows.Forms;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Icon service that provides themed icons for UI controls.
    /// Currently uses system icons as placeholders until custom assets are added.
    /// </summary>
    public sealed class ThemeIconService : IThemeIconService
    {
        public Image? GetIcon(string name, AppTheme theme, int size)
        {
            // For Office2019Colorful theme, return appropriate icons
            if (theme == AppTheme.Office2019Colorful)
            {
                return GetOffice2019ColorfulIcon(name, size);
            }

            // For other themes or unknown names, return null
            return null;
        }

        private static Image? GetOffice2019ColorfulIcon(string name, int size)
        {
            // Use system icons as placeholders for common UI elements
            // These can be replaced with custom icon assets later
            switch (name.ToLowerInvariant())
            {
                case "settings":
                case "options":
                    return SystemIcons.Application.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "help":
                case "info":
                    return SystemIcons.Information.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "close":
                case "exit":
                    return SystemIcons.Error.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "minimize":
                    return SystemIcons.Warning.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "maximize":
                case "restore":
                    return SystemIcons.Question.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "save":
                    return SystemIcons.Shield.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "open":
                case "folder":
                    return SystemIcons.Shield.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "refresh":
                case "reload":
                    return SystemIcons.Shield.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "search":
                    return SystemIcons.Shield.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "add":
                case "plus":
                case "new":
                    return SystemIcons.Shield.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "delete":
                case "remove":
                case "minus":
                    return SystemIcons.Shield.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "edit":
                case "pencil":
                    return SystemIcons.Shield.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "home":
                    return SystemIcons.Application.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "dashboard":
                case "chart":
                    return SystemIcons.Application.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "report":
                case "document":
                    return SystemIcons.Application.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "user":
                case "profile":
                    return SystemIcons.Application.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "notification":
                case "bell":
                    return SystemIcons.Application.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                case "menu":
                case "hamburger":
                    return SystemIcons.Application.ToBitmap().GetThumbnailImage(size, size, null, IntPtr.Zero);

                default:
                    // Return null for unknown icon names
                    return null;
            }
        }
    }
}
