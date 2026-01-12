using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Manages user preferences and settings persistence.
    /// Stores preferences in JSON format for easy migration and backup.
    /// </summary>
    public class UserPreferencesService : IDisposable
    {
        private readonly ILogger<UserPreferencesService> _logger;
        private readonly string _preferencesPath;
        private Dictionary<string, object> _preferences = new();
        private bool _isDirty;

        public event EventHandler<PreferenceChangedEventArgs>? PreferenceChanged;

        public UserPreferencesService(ILogger<UserPreferencesService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _preferencesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WileyWidget",
                "user-preferences.json");

            Directory.CreateDirectory(Path.GetDirectoryName(_preferencesPath)!);
            _ = LoadPreferencesAsync();
        }

        /// <summary>
        /// Gets a preference value.
        /// </summary>
        public T? GetPreference<T>(string key, T? defaultValue = default)
        {
            try
            {
                if (_preferences.TryGetValue(key, out var value))
                {
                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement);
                    }
                    return (T?)value;
                }
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting preference {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a preference value.
        /// </summary>
        public async Task SetPreferenceAsync<T>(string key, T value)
        {
            try
            {
                _preferences[key] = value;
                _isDirty = true;

                PreferenceChanged?.Invoke(this, new PreferenceChangedEventArgs { Key = key, Value = value });

                // Auto-save after each change
                await SavePreferencesAsync();

                _logger.LogDebug("Preference set: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set preference {Key}", key);
            }
        }

        /// <summary>
        /// Gets all preferences.
        /// </summary>
        public Dictionary<string, object> GetAllPreferences() => new(_preferences);

        /// <summary>
        /// Loads preferences from disk.
        /// </summary>
        public async Task LoadPreferencesAsync()
        {
            try
            {
                if (!File.Exists(_preferencesPath))
                {
                    LoadDefaults();
                    return;
                }

                var json = await File.ReadAllTextAsync(_preferencesPath);
                _preferences = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();

                _logger.LogInformation("Preferences loaded from {Path}", _preferencesPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load preferences, using defaults");
                LoadDefaults();
            }
        }

        /// <summary>
        /// Saves preferences to disk.
        /// </summary>
        public async Task SavePreferencesAsync()
        {
            if (!_isDirty) return;

            try
            {
                var json = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_preferencesPath, json);
                _isDirty = false;

                _logger.LogDebug("Preferences saved to {Path}", _preferencesPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save preferences");
            }
        }

        /// <summary>
        /// Resets all preferences to defaults.
        /// </summary>
        public async Task ResetAsync()
        {
            LoadDefaults();
            await SavePreferencesAsync();
            _logger.LogInformation("Preferences reset to defaults");
        }

        private void LoadDefaults()
        {
            _preferences = new Dictionary<string, object>
            {
                ["Theme"] = "Office2019Colorful",
                ["DashboardAutoShow"] = true,
                ["SaveWindowState"] = true,
                ["NotificationsEnabled"] = true,
                ["AutoSave"] = true,
                ["SearchResultsLimit"] = 100,
                ["RecentFiles"] = new List<string>(),
                ["LastOpenedPanels"] = new List<string>()
            };
            _isDirty = false;
            _logger.LogDebug("Default preferences loaded");
        }

        public void Dispose()
        {
            _ = SavePreferencesAsync().Wait(5000);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Event args for preference changes.
    /// </summary>
    public class PreferenceChangedEventArgs : EventArgs
    {
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
    }
}
