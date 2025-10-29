using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Service for managing application localization and culture switching
/// </summary>
public class LocalizationService : INotifyPropertyChanged, IDisposable
{
    private readonly ILogger<LocalizationService> _logger;
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;
    private CultureInfo _currentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Current culture for data formatting
    /// </summary>
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        private set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                OnPropertyChanged(nameof(CurrentCulture));
                UpdateThreadCulture();
            }
        }
    }

    /// <summary>
    /// Current UI culture for resource localization
    /// </summary>
    public CultureInfo CurrentUICulture
    {
        get => _currentUICulture;
        private set
        {
            if (_currentUICulture != value)
            {
                _currentUICulture = value;
                OnPropertyChanged(nameof(CurrentUICulture));
                UpdateThreadCulture();
            }
        }
    }

    public LocalizationService(ILogger<LocalizationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize resource manager for Strings.resx
        _resourceManager = new ResourceManager("WileyWidget.Resources.Strings",
            typeof(LocalizationService).Assembly);

        // Start with current thread cultures
        _currentCulture = Thread.CurrentThread.CurrentCulture;
        _currentUICulture = Thread.CurrentThread.CurrentUICulture;

        _logger.LogInformation("Localization service initialized with culture {Culture} / UI culture {UICulture}",
            _currentCulture.Name, _currentUICulture.Name);
    }

    /// <summary>
    /// Sets the current culture for both data formatting and UI localization
    /// </summary>
    public void SetCulture(string cultureName)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CurrentCulture = culture;
            CurrentUICulture = culture;

            _logger.LogInformation("Culture changed to {CultureName}", cultureName);
        }
        catch (CultureNotFoundException ex)
        {
            _logger.LogWarning(ex, "Culture {CultureName} not found, keeping current culture", cultureName);
        }
    }

    /// <summary>
    /// Sets separate cultures for data formatting and UI localization
    /// </summary>
    public void SetCultures(string cultureName, string uiCultureName)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var uiCulture = CultureInfo.GetCultureInfo(uiCultureName);

            CurrentCulture = culture;
            CurrentUICulture = uiCulture;

            _logger.LogInformation("Cultures changed to {CultureName} / {UICultureName}",
                cultureName, uiCultureName);
        }
        catch (CultureNotFoundException ex)
        {
            _logger.LogWarning(ex, "Culture {CultureName} or {UICultureName} not found",
                cultureName, uiCultureName);
        }
    }

    /// <summary>
    /// Gets a localized string by key
    /// </summary>
    public string GetString(string key)
    {
        try
        {
            var value = _resourceManager.GetString(key, _currentUICulture);
            return value ?? $"[{key}]"; // Return key in brackets if not found
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get localized string for key {Key}", key);
            return $"[{key}]";
        }
    }

    /// <summary>
    /// Gets a localized string with fallback to default culture
    /// </summary>
    public string GetString(string key, string fallback)
    {
        var localized = GetString(key);
        return localized.StartsWith("[", StringComparison.Ordinal) && localized.EndsWith("]", StringComparison.Ordinal) ? fallback : localized;
    }

    /// <summary>
    /// Gets available cultures from resource files
    /// </summary>
    public CultureInfo[] GetAvailableCultures()
    {
        // In a real implementation, you might scan for .resx files
        // For now, return common cultures
        return new[]
        {
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("es"),
            CultureInfo.GetCultureInfo("fr"),
            CultureInfo.GetCultureInfo("de")
        };
    }

    private void UpdateThreadCulture()
    {
        try
        {
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            Thread.CurrentThread.CurrentUICulture = _currentUICulture;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update thread culture");
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resourceManager.ReleaseAllResources();
            _logger.LogInformation("Localization service disposed");
        }
    }
}
