using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Centralized service for application-wide theme management and notifications.
/// Coordinates between UI settings and the global SfSkinManager state via ThemeColors utility.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ThemeService> _logger;
    private readonly ISettingsService? _settingsService;
    private string _currentTheme;

    /// <summary>
    /// Event raised when the application theme has changed.
    /// </summary>
    public event EventHandler<string>? ThemeChanged;

    /// <summary>
    /// Gets the current application theme name.
    /// </summary>
    public string CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets a value indicating whether the current theme is a dark theme.
    /// </summary>
    public bool IsDark => _currentTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase) ||
                          _currentTheme.Contains("Black", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public ThemeService(IConfiguration configuration, ILogger<ThemeService> logger, ISettingsService? settingsService = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService;

        // Load theme from persisted settings first, then configuration, then default.
        var persistedTheme = _settingsService?.Current?.Theme;
        var configuredTheme = !string.IsNullOrWhiteSpace(persistedTheme)
            ? persistedTheme
            : _configuration["UI:Theme"] ?? ThemeColors.DefaultTheme;
        _currentTheme = ThemeColors.ValidateTheme(configuredTheme, _logger);

        _logger.LogInformation("ThemeService initialized with theme: {Theme}", _currentTheme);
    }

    /// <summary>
    /// Applies the specified theme globally and notifies subscribers.
    /// This also persists the theme choice to application settings.
    /// </summary>
    /// <param name="themeName">The name of the theme to apply (e.g., "Office2019Dark").</param>
    public void ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            _logger.LogWarning("Attempted to apply null or empty theme name. Ignoring.");
            return;
        }

        var validatedTheme = ThemeColors.ValidateTheme(themeName, _logger);
        if (validatedTheme == _currentTheme)
        {
            _logger.LogDebug("Theme '{Theme}' is already active. Skipping.", validatedTheme);
            return;
        }

        var previousTheme = _currentTheme;
        _currentTheme = validatedTheme;

        _logger.LogInformation("Theme changed from '{PreviousTheme}' to '{NewTheme}'", previousTheme, _currentTheme);

        // Ensure theme assembly is loaded
        ThemeColors.EnsureThemeAssemblyLoadedForTheme(_currentTheme, _logger);

        // Notify subscribers
        try
        {
            ThemeChanged?.Invoke(this, _currentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying theme change subscribers");
        }

        if (_settingsService != null)
        {
            try
            {
                _settingsService.Current.Theme = _currentTheme;
                _settingsService.Save();
                _logger.LogDebug("Persisted active theme '{Theme}' to user settings.", _currentTheme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist theme '{Theme}' to user settings", _currentTheme);
            }
        }
    }
}
