using Serilog;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Service for managing application themes.
    /// </summary>
    /// <summary>
    /// Represents a interface for ithemeservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ithemeservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ithemeservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ithemeservice.
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
    /// <summary>
    /// Represents a class for themeservice.
    /// </summary>
    /// <summary>
    /// Represents a class for themeservice.
    /// </summary>
    /// <summary>
    /// Represents a class for themeservice.
    /// </summary>
    /// <summary>
    /// Represents a class for themeservice.
    /// </summary>
    public class ThemeService : IThemeService
    {
        /// <summary>
        /// Represents the _logger.
        /// </summary>
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
        /// <summary>
        /// Performs settheme. Parameters: theme.
        /// </summary>
        /// <param name="theme">The theme.</param>
        /// <summary>
        /// Performs settheme. Parameters: theme.
        /// </summary>
        /// <param name="theme">The theme.</param>
        /// <summary>
        /// Performs settheme. Parameters: theme.
        /// </summary>
        /// <param name="theme">The theme.</param>
        /// <summary>
        /// Performs settheme. Parameters: theme.
        /// </summary>
        /// <param name="theme">The theme.</param>

        public void SetTheme(AppTheme theme)
        {
            if (_currentTheme != theme)
            {
                var oldTheme = _currentTheme;
                _currentTheme = theme;
                _logger.Information("Theme changed from {OldTheme} to {NewTheme}", oldTheme, theme);
                ThemeChanged?.Invoke(this, theme);
            }
            else
            {
                _logger.Debug("Theme {Theme} already active, no change needed", theme);
            }
        }
    }
}
