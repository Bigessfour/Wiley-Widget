using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Service for managing application themes.
    /// </summary>
    public interface IThemeService
    {
        AppTheme CurrentTheme { get; }
        void SetTheme(AppTheme theme);
        event EventHandler? ThemeChanged;
    }

    /// <summary>
    /// Default implementation of theme service.
    /// </summary>
    public class ThemeService : IThemeService
    {
        private AppTheme _currentTheme = AppTheme.Office2019Colorful;

        public AppTheme CurrentTheme => _currentTheme;

        public event EventHandler? ThemeChanged;

        public void SetTheme(AppTheme theme)
        {
            if (_currentTheme != theme)
            {
                _currentTheme = theme;
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
