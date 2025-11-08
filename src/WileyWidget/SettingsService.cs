using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Persisted user-facing settings. Keep only values that must survive restarts; transient UI state stays in memory.
/// Nullable primitives used to distinguish 'not yet set' from legitimate 0 values (e.g., window geometry).
/// </summary>
// AppSettings moved to Models/AppSettings.cs

/// <summary>
/// Simple service for loading/saving <see cref="AppSettings"/> as JSON in AppData.
/// Handles corruption by renaming the bad file and regenerating defaults.
/// FIXED: Removed static singleton pattern to prevent circular dependency and DI timeout issues.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger<SettingsService> _logger;

    private string _root = string.Empty;
    private string _file = string.Empty;

    /// <summary>
    /// The in-memory settings instance.
    /// Implemented to satisfy <see cref="ISettingsService"/> and used throughout the app.
    /// </summary>
    public AppSettings Current { get; private set; } = new AppSettings();

    /// <summary>
    /// Primary constructor for DI container resolution.
    /// Configuration and logger are injected by the DI container.
    /// </summary>
    public SettingsService(IConfiguration? configuration, ILogger<SettingsService>? logger)
    {
        _configuration = configuration;
        _logger = logger ?? NullLogger<SettingsService>.Instance;
        InitializePaths();
    }

    private void InitializePaths()
    {
        var overrideDir = _configuration?["Settings:Directory"]
                          ?? Environment.GetEnvironmentVariable("WILEYWIDGET_SETTINGS_DIR");

        _root = string.IsNullOrWhiteSpace(overrideDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget")
            : overrideDir;
        _file = Path.Combine(_root, "settings.json");

        _logger.LogDebug("Settings directory resolved to {SettingsDirectory}.", _root);
    }

    public void ResetForTests()
    {
        // Don't call InitializePaths() as it would overwrite the test directory set via reflection
        Current = new AppSettings();
    }

    /// <summary>
    /// Loads the persisted application settings and returns the in-memory instance for fluent usage.
    /// </summary>
    public AppSettings LoadSettings()
    {
        Load();
        return Current;
    }

    /// <summary>
    /// Loads settings from disk or creates a new file if absent. If deserialization fails, the corrupt
    /// file is renamed with a timestamp suffix to aid post-mortem diagnostics.
    /// </summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                Directory.CreateDirectory(_root);
                Save();
                _logger.LogInformation("Settings file not found. Created default settings at {SettingsFile}.", _file);
                return;
            }
            var json = File.ReadAllText(_file);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded != null)
                Current = loaded;
            // Migration: populate new Qbo* from legacy QuickBooks* if empty (first-run after upgrade).
            if (string.IsNullOrWhiteSpace(Current.QboAccessToken) && !string.IsNullOrWhiteSpace(Current.QuickBooksAccessToken))
            {
                Current.QboAccessToken = Current.QuickBooksAccessToken;
                Current.QboRefreshToken = Current.QuickBooksRefreshToken;
                if (Current.QuickBooksTokenExpiresUtc.HasValue)
                    Current.QboTokenExpiry = Current.QuickBooksTokenExpiresUtc.Value;
            }
            _logger.LogDebug("Settings loaded successfully from {SettingsFile}.", _file);
        }
        catch (Exception ex)
        {
            // rename corrupted file and recreate
            try
            {
                if (File.Exists(_file))
                {
                    var bad = _file + ".bad_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    File.Move(_file, bad);
                    _logger.LogWarning(ex, "Settings file corrupt; moved to {BackupFile} and regenerating defaults.", bad);
                }
            }
            catch { }
            Current = new AppSettings();
            Save();
        }
    }

    /// <summary>
    /// Writes current settings to disk (indented JSON). Failures are swallowed intentionally: user
    /// experience should not degrade due to IO issues—consider surfacing via telemetry later.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_root);
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
            _logger.LogDebug("Settings saved successfully to {SettingsFile}.", _file);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist settings to {SettingsFile}.", _file);
        }
    }

    /// <summary>
    /// Saves fiscal year settings to the current settings
    /// </summary>
    /// <param name="month">Fiscal year start month (1-12)</param>
    /// <param name="day">Fiscal year start day</param>
    public void SaveFiscalYearSettings(int month, int day)
    {
        try
        {
            Current.FiscalYearStartMonth = month;
            Current.FiscalYearStartDay = day;
            Save();
            _logger.LogInformation("Fiscal year settings saved: Month={Month}, Day={Day}", month, day);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save fiscal year settings");
            throw;
        }
    }

    /// <summary>
    /// Loads application settings asynchronously
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async System.Threading.Tasks.Task LoadAsync()
    {
        await System.Threading.Tasks.Task.Run(() => Load());
    }

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        try
        {
            var property = typeof(AppSettings).GetProperty(key);
            if (property == null)
            {
                _logger.LogWarning("Attempted to get unknown setting key: {Key}", key);
                return string.Empty;
            }

            var value = property.GetValue(Current);
            return value?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get setting value for key: {Key}", key);
            return string.Empty;
        }
    }

    public void Set(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        try
        {
            var property = typeof(AppSettings).GetProperty(key);
            if (property == null)
            {
                _logger.LogWarning("Attempted to set unknown setting key: {Key}", key);
                return;
            }

            // Handle type conversion based on property type
            object? convertedValue = null;
            var propertyType = property.PropertyType;

            if (propertyType == typeof(string))
            {
                convertedValue = value;
            }
            else if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                if (int.TryParse(value, out var intValue))
                {
                    convertedValue = propertyType == typeof(int?) ? (int?)intValue : intValue;
                }
            }
            else if (propertyType == typeof(double) || propertyType == typeof(double?))
            {
                if (double.TryParse(value, out var doubleValue))
                {
                    convertedValue = propertyType == typeof(double?) ? (double?)doubleValue : doubleValue;
                }
            }
            else if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                if (bool.TryParse(value, out var boolValue))
                {
                    convertedValue = propertyType == typeof(bool?) ? (bool?)boolValue : boolValue;
                }
            }
            else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                if (DateTime.TryParse(value, out var dateTimeValue))
                {
                    convertedValue = propertyType == typeof(DateTime?) ? (DateTime?)dateTimeValue : dateTimeValue;
                }
            }

            if (convertedValue != null)
            {
                property.SetValue(Current, convertedValue);
                _logger.LogDebug("Setting {Key} updated to {Value}", key, convertedValue);
            }
            else
            {
                _logger.LogWarning("Failed to convert value '{Value}' for setting key: {Key}", value, key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set setting value for key: {Key}", key);
        }
    }
}
