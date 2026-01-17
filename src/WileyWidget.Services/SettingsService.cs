#nullable enable
using System.Threading;
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Extensions.Options;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for loading, saving, and managing application settings.
    /// Persists settings as JSON in the user's AppData folder and handles corruption gracefully.
    /// </summary>
    /// <remarks>
    /// This implementation:
    /// - Serializes settings to JSON format in the user's AppData directory
    /// - Handles file corruption by backing up and regenerating defaults
    /// - Supports fiscal year configuration with validation
    /// - Provides asynchronous loading capabilities
    /// - Uses dependency injection for configuration and logging
    /// </remarks>
    public sealed class SettingsService : ISettingsService
    {
        private readonly IConfiguration? _configuration;
        private string _root = string.Empty;
        private string _file = string.Empty;

        /// <summary>
        /// Gets the current in-memory application settings instance.
        /// </summary>
        /// <remarks>
        /// This property provides access to the current settings state.
        /// Changes to this instance should be followed by a call to <see cref="Save"/> to persist them.
        /// </remarks>
        public AppSettings Current { get; private set; } = new AppSettings();

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        /// <param name="configuration">The configuration instance for resolving settings directory.</param>
        /// <param name="logger">The logger instance for diagnostic output.</param>
        /// <remarks>
        /// The settings directory can be overridden via:
        /// - Configuration: Settings:Directory
        /// - Environment variable: WILEYWIDGET_SETTINGS_DIR
        /// If neither is set, defaults to %AppData%\WileyWidget
        /// </remarks>
        public SettingsService(IConfiguration? configuration)
        {
            _configuration = configuration;
            InitializePaths();
        }

        /// <summary>
        /// Initializes the settings file paths based on configuration.
        /// </summary>
        private void InitializePaths()
        {
            var overrideDir = _configuration?["Settings:Directory"]
                              ?? Environment.GetEnvironmentVariable("WILEYWIDGET_SETTINGS_DIR");

            _root = string.IsNullOrWhiteSpace(overrideDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget")
                : overrideDir;
            _file = Path.Combine(_root, "settings.json");

            Log.Debug("Settings directory resolved to {SettingsDirectory}", _root);
        }

        /// <summary>
        /// Loads settings from disk or creates a new settings file if none exists.
        /// </summary>
        /// <remarks>
        /// If the settings file is corrupted, it will be renamed with a timestamp suffix
        /// and a new file with default values will be created.
        /// </remarks>
        private void Load()
        {
            try
            {
                if (!File.Exists(_file))
                {
                    Directory.CreateDirectory(_root);
                    Save();
                    Log.Information("Settings file not found. Created default settings at {SettingsFile}", _file);
                    return;
                }

                var json = File.ReadAllText(_file);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);

                if (loaded != null)
                {
                    Current = loaded;
                }

                // Migration: populate new Qbo* from legacy QuickBooks* if empty
                if (string.IsNullOrWhiteSpace(Current.QboAccessToken) &&
                    !string.IsNullOrWhiteSpace(Current.QuickBooksAccessToken))
                {
                    Current.QboAccessToken = Current.QuickBooksAccessToken;
                    Current.QboRefreshToken = Current.QuickBooksRefreshToken;
                    if (Current.QuickBooksTokenExpiresUtc.HasValue)
                    {
                        Current.QboTokenExpiry = Current.QuickBooksTokenExpiresUtc.Value;
                    }
                    Log.Information("Migrated legacy QuickBooks settings to QBO settings");
                }

                Log.Debug("Settings loaded successfully from {SettingsFile}", _file);
            }
            catch (Exception ex)
            {
                // Rename corrupted file and recreate with defaults
                HandleCorruptedFile(ex);
            }
        }

        /// <summary>
        /// Handles corrupted settings files by backing them up and creating new defaults.
        /// </summary>
        /// <param name="exception">The exception that occurred during loading.</param>
        private void HandleCorruptedFile(Exception exception)
        {
            try
            {
                if (File.Exists(_file))
                {
                    var backupFile = _file + ".bad_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    File.Move(_file, backupFile);
                    Log.Warning(exception,
                        "Settings file corrupted. Moved to {BackupFile} and regenerating defaults", backupFile);
                }
            }
            catch (Exception backupEx)
            {
                Log.Error(backupEx, "Failed to backup corrupted settings file");
            }

            Current = new AppSettings();
            Save();
        }

        /// <summary>
        /// Saves all current application settings to disk.
        /// </summary>
        /// <remarks>
        /// Settings are serialized as indented JSON for readability.
        /// IO failures are logged but do not throw exceptions to prevent degrading the user experience.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the settings directory cannot be created.</exception>
        public void Save()
        {
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(_root))
                {
                    Directory.CreateDirectory(_root);
                }

                // Serialize with indentation for readability
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null // Keep original property names
                };

                var json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(_file, json);

                Log.Debug("Settings saved successfully to {SettingsFile}", _file);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to persist settings to {SettingsFile}", _file);
                // Don't throw - allow the application to continue running
            }
        }

        /// <summary>
        /// Saves fiscal year settings with validation.
        /// </summary>
        /// <param name="month">The fiscal year start month (1-12, where 1=January).</param>
        /// <param name="day">The fiscal year start day (1-31, validated against month).</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when month is not between 1 and 12, or when day is invalid for the specified month.
        /// </exception>
        /// <remarks>
        /// This method validates the fiscal year date combination to ensure:
        /// - Month is between 1 (January) and 12 (December)
        /// - Day is valid for the specified month (e.g., prevents Feb 30)
        /// - The date can be constructed as a valid DateTime
        /// </remarks>
        public void SaveFiscalYearSettings(int month, int day)
        {
            // Validate month
            if (month < 1 || month > 12)
            {
                var ex = new ArgumentOutOfRangeException(nameof(month), month,
                    "Fiscal year start month must be between 1 (January) and 12 (December)");
                Log.Error(ex, "Invalid fiscal year month: {Month}", month);
                throw ex;
            }

            // Validate day based on month
            var daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, month);
            if (day < 1 || day > daysInMonth)
            {
                var ex = new ArgumentOutOfRangeException(nameof(day), day,
                    $"Fiscal year start day must be between 1 and {daysInMonth} for month {month}");
                Log.Error(ex, "Invalid fiscal year day: {Day} for month {Month}", day, month);
                throw ex;
            }

            // Validate that the date can be constructed
            try
            {
                _ = new DateTime(DateTime.Now.Year, month, day);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Log.Error(ex, "Invalid fiscal year date combination: Month={Month}, Day={Day}", month, day);
                throw new ArgumentException($"Invalid fiscal year date: month {month}, day {day}", ex);
            }

            try
            {
                // Update settings
                Current.FiscalYearStartMonth = month;
                Current.FiscalYearStartDay = day;

                // Update the formatted strings
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
                Current.FiscalYearStart = $"{monthName} {day}";

                // Calculate fiscal year end (previous day of start date, next year)
                var fiscalYearEnd = new DateTime(DateTime.Now.Year + 1, month, day).AddDays(-1);
                var endMonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(fiscalYearEnd.Month);
                Current.FiscalYearEnd = $"{endMonthName} {fiscalYearEnd.Day}";

                // Persist to disk
                Save();

                Log.Information(
                    "Fiscal year settings saved: Month={Month} ({MonthName}), Day={Day}, Start={Start}, End={End}",
                    month, monthName, day, Current.FiscalYearStart, Current.FiscalYearEnd);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save fiscal year settings: Month={Month}, Day={Day}", month, day);
                throw;
            }
        }

        /// <summary>
        /// Loads application settings asynchronously from disk.
        /// </summary>
        /// <returns>A task representing the asynchronous load operation.</returns>
        /// <remarks>
        /// This method wraps the synchronous <see cref="Load"/> operation in a Task
        /// to support asynchronous initialization patterns. The actual file I/O is still synchronous.
        /// </remarks>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Load()).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a setting value by key using reflection.
        /// </summary>
        /// <param name="key">The property name to retrieve.</param>
        /// <returns>The string representation of the setting value, or empty string if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
        /// <remarks>
        /// This is a legacy method maintained for backward compatibility.
        /// Direct property access via <see cref="Current"/> is preferred.
        /// </remarks>
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
                    Log.Warning("Attempted to get unknown setting key: {Key}", key);
                    return string.Empty;
                }

                var value = property.GetValue(Current);
                return value?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get setting value for key: {Key}", key);
                return string.Empty;
            }
        }

        /// <summary>
        /// Sets a setting value by key using reflection.
        /// </summary>
        /// <param name="key">The property name to set.</param>
        /// <param name="value">The string value to set (will be converted to the property's type).</param>
        /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
        /// <remarks>
        /// This is a legacy method maintained for backward compatibility.
        /// Direct property access via <see cref="Current"/> is preferred.
        /// Type conversion is attempted automatically but may fail for complex types.
        /// </remarks>
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
                    Log.Warning("Attempted to set unknown setting key: {Key}", key);
                    return;
                }

                // Convert string value to property type
                object? convertedValue = null;
                var propertyType = property.PropertyType;

                // Handle nullable types
                var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

                if (string.IsNullOrWhiteSpace(value))
                {
                    convertedValue = null;
                }
                else if (underlyingType == typeof(string))
                {
                    convertedValue = value;
                }
                else if (underlyingType == typeof(bool))
                {
                    convertedValue = bool.Parse(value);
                }
                else if (underlyingType == typeof(int))
                {
                    convertedValue = int.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (underlyingType == typeof(double))
                {
                    convertedValue = double.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (underlyingType == typeof(DateTime))
                {
                    convertedValue = DateTime.Parse(value, CultureInfo.InvariantCulture);
                }
                else
                {
                    // Attempt generic conversion
                    convertedValue = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
                }

                property.SetValue(Current, convertedValue);
                Log.Debug("Setting {Key} updated to {Value}", key, value);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set setting value for key: {Key} to {Value}", key, value);
                throw;
            }
        }

        /// <summary>
        /// Loads settings and returns the current instance for fluent usage.
        /// </summary>
        /// <returns>The current <see cref="AppSettings"/> instance after loading.</returns>
        public AppSettings LoadSettings()
        {
            Load();
            return Current;
        }

        public string GetEnvironmentName() => GetValue("Environment") ?? "Production";

        public string GetValue(string key) => _configuration?[key] ?? _configuration?["AppSettings:" + key] ?? string.Empty;

        public void SetValue(string key, string value)
        {
            if (_configuration is IConfigurationRoot root)
            {
                root[key] = value;
            }
        }

        /// <summary>
        /// Resets the settings to defaults. Used primarily for testing.
        /// </summary>
        /// <remarks>
        /// This method does not reinitialize paths - it only resets the in-memory settings.
        /// Used by unit tests to ensure a clean state.
        /// </remarks>
        internal void ResetForTests()
        {
            Current = new AppSettings();
        }
    }
}
