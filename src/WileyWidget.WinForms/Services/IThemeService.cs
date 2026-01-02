using Serilog;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Service for managing application themes.
    /// </summary>
    public interface IThemeService
    {
        AppTheme CurrentTheme { get; }
        AppTheme Preference { get; }
        void SetTheme(AppTheme theme);
        event EventHandler<AppTheme>? ThemeChanged;
    }

    /// <summary>
    /// Default implementation of theme service.
    /// </summary>
    public class ThemeService : IThemeService
    {
        private readonly ILogger _logger;
        private AppTheme _currentTheme = AppTheme.Office2019Colorful;

        public ThemeService(ILogger logger)
        {
            _logger = logger?.ForContext<ThemeService>() ?? throw new ArgumentNullException(nameof(logger));
            _logger.Information("ThemeService initialized with default theme: {Theme}", _currentTheme);
        }

        public AppTheme CurrentTheme => _currentTheme;

        public AppTheme Preference => _currentTheme;

        public event EventHandler<AppTheme>? ThemeChanged;

        public void SetTheme(AppTheme theme)
        {
            if (_currentTheme != theme)
            {
                var oldTheme = _currentTheme;
                _currentTheme = theme;
                _logger.Information("Theme changed from {OldTheme} to {NewTheme}", oldTheme, theme);
                ThemeChanged?.Invoke(this, theme);

                // Bridge: Forward theme change to ThemeManager for backward compatibility
                // TODO: Remove this bridge once all panels use ThemeService directly
                WileyWidget.WinForms.Theming.ThemeManager.SetTheme(theme);
            }
            else
            {
                _logger.Debug("Theme {Theme} already active, no change needed", theme);
            }
        }
    }
}
