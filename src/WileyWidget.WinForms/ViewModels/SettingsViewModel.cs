using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Represents a configurable application setting for display in the settings form.
    /// </summary>
    public class SettingItem
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public object? Value { get; set; }
        public Type ValueType { get; set; } = typeof(string);
        public string Category { get; set; } = "General";
    }

    /// <summary>
    /// ViewModel for the Settings view providing comprehensive application configuration,
    /// theme management, connection settings, and user preferences.
    /// Implements full MVVM pattern with async save/load and validation.
    /// </summary>
    public partial class SettingsViewModel : ObservableRecipient
    {
        private readonly ILogger<SettingsViewModel>? _logger;
        private readonly string _settingsFilePath;

        [ObservableProperty]
        private string appTitle = "Wiley Widget Settings";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasUnsavedChanges;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private string? successMessage;

        [ObservableProperty]
        private AppTheme selectedTheme = AppTheme.Light;

        [ObservableProperty]
        private string connectionString = string.Empty;

        [ObservableProperty]
        private bool autoSaveEnabled = true;

        [ObservableProperty]
        private int autoSaveIntervalMinutes = 5;

        [ObservableProperty]
        private bool showNotifications = true;

        [ObservableProperty]
        private string defaultExportPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        [ObservableProperty]
        private string dateFormat = "MM/dd/yyyy";

        [ObservableProperty]
        private string currencyFormat = "C2";

        [ObservableProperty]
        private bool enableLogging = true;

        [ObservableProperty]
        private string logLevel = "Information";

        [ObservableProperty]
        private bool openEditFormsDocked = false;

        [ObservableProperty]
        private DateTime lastSaved = DateTime.MinValue;

        /// <summary>
        /// Collection of available themes for two-way binding.
        /// </summary>
        public ObservableCollection<AppTheme> Themes { get; } = new();

        /// <summary>
        /// Collection of available log levels.
        /// </summary>
        public ObservableCollection<string> LogLevels { get; } = new()
        {
            "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
        };

        /// <summary>
        /// Collection of date format options.
        /// </summary>
        public ObservableCollection<string> DateFormats { get; } = new()
        {
            "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "MMMM dd, yyyy"
        };

        /// <summary>
        /// Initializes a new instance with optional DI dependencies.
        /// </summary>
        public SettingsViewModel(ILogger<SettingsViewModel>? logger = null)
        {
            _logger = logger;
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WileyWidget",
                "settings.json");

            // Populate themes collection
            foreach (var theme in Enum.GetValues<AppTheme>())
            {
                Themes.Add(theme);
            }

            ApplyCommand = new RelayCommand(Apply);
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
            LoadCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ResetCommand = new RelayCommand(ResetToDefaults);
            BrowseExportPathCommand = new RelayCommand(BrowseExportPath);

            // Load settings on startup
            _ = LoadSettingsAsync();
        }

        /// <summary>Command to apply theme immediately.</summary>
        public IRelayCommand ApplyCommand { get; }

        /// <summary>Command to save all settings to disk.</summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>Command to load settings from disk.</summary>
        public IAsyncRelayCommand LoadCommand { get; }

        /// <summary>Command to reset settings to defaults.</summary>
        public IRelayCommand ResetCommand { get; }

        /// <summary>Command to browse for export path.</summary>
        public IRelayCommand BrowseExportPathCommand { get; }

        /// <summary>
        /// Applies the selected theme immediately for live preview.
        /// </summary>
        private void Apply()
        {
            try
            {
                ThemeManager.SetTheme(SelectedTheme);
                SuccessMessage = $"Theme changed to {SelectedTheme}";
                _logger?.LogInformation("Theme applied: {Theme}", SelectedTheme);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to apply theme: {ex.Message}";
                _logger?.LogError(ex, "Failed to apply theme");
            }
        }

        /// <summary>
        /// Saves all settings to the settings file asynchronously.
        /// </summary>
        private async Task SaveSettingsAsync(CancellationToken ct = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _logger?.LogInformation("Saving settings to {Path}", _settingsFilePath);

                var settings = new
                {
                    Theme = SelectedTheme.ToString(),
                    ConnectionString,
                    AutoSaveEnabled,
                    AutoSaveIntervalMinutes,
                    ShowNotifications,
                    DefaultExportPath,
                    DateFormat,
                    CurrencyFormat,
                    EnableLogging,
                    LogLevel,
                    OpenEditFormsDocked,
                    SavedAt = DateTime.Now
                };

                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFilePath, json, ct);

                LastSaved = DateTime.Now;
                HasUnsavedChanges = false;
                SuccessMessage = "Settings saved successfully";
                _logger?.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save settings: {ex.Message}";
                _logger?.LogError(ex, "Failed to save settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads settings from the settings file asynchronously.
        /// </summary>
        private async Task LoadSettingsAsync(CancellationToken ct = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                if (!File.Exists(_settingsFilePath))
                {
                    _logger?.LogInformation("No settings file found, using defaults");
                    return;
                }

                _logger?.LogInformation("Loading settings from {Path}", _settingsFilePath);
                var json = await File.ReadAllTextAsync(_settingsFilePath, ct);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Theme", out var themeEl) &&
                    Enum.TryParse<AppTheme>(themeEl.GetString(), out var theme))
                {
                    SelectedTheme = theme;
                }

                if (root.TryGetProperty("ConnectionString", out var connEl))
                    ConnectionString = connEl.GetString() ?? string.Empty;

                if (root.TryGetProperty("AutoSaveEnabled", out var autoEl))
                    AutoSaveEnabled = autoEl.GetBoolean();

                if (root.TryGetProperty("AutoSaveIntervalMinutes", out var intervalEl))
                    AutoSaveIntervalMinutes = intervalEl.GetInt32();

                if (root.TryGetProperty("ShowNotifications", out var notifyEl))
                    ShowNotifications = notifyEl.GetBoolean();

                if (root.TryGetProperty("DefaultExportPath", out var exportEl))
                    DefaultExportPath = exportEl.GetString() ?? DefaultExportPath;

                if (root.TryGetProperty("DateFormat", out var dateEl))
                    DateFormat = dateEl.GetString() ?? DateFormat;

                if (root.TryGetProperty("CurrencyFormat", out var currEl))
                    CurrencyFormat = currEl.GetString() ?? CurrencyFormat;

                if (root.TryGetProperty("EnableLogging", out var logEl))
                    EnableLogging = logEl.GetBoolean();

                if (root.TryGetProperty("LogLevel", out var levelEl))
                    LogLevel = levelEl.GetString() ?? LogLevel;

                if (root.TryGetProperty("OpenEditFormsDocked", out var dockedEl))
                    OpenEditFormsDocked = dockedEl.GetBoolean();

                HasUnsavedChanges = false;
                _logger?.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load settings: {ex.Message}";
                _logger?.LogError(ex, "Failed to load settings");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        private void ResetToDefaults()
        {
            SelectedTheme = AppTheme.Light;
            ConnectionString = string.Empty;
            AutoSaveEnabled = true;
            AutoSaveIntervalMinutes = 5;
            ShowNotifications = true;
            DefaultExportPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DateFormat = "MM/dd/yyyy";
            CurrencyFormat = "C2";
            EnableLogging = true;
            LogLevel = "Information";
            HasUnsavedChanges = true;
            SuccessMessage = "Settings reset to defaults";
            _logger?.LogInformation("Settings reset to defaults");
        }

        /// <summary>
        /// Opens folder browser dialog for export path selection.
        /// </summary>
        private void BrowseExportPath()
        {
            // In a real implementation, this would open a FolderBrowserDialog
            // For now, mark as changed
            HasUnsavedChanges = true;
        }

        // Property change handlers to track unsaved changes
        partial void OnSelectedThemeChanged(AppTheme value) => HasUnsavedChanges = true;
        partial void OnConnectionStringChanged(string value) => HasUnsavedChanges = true;
        partial void OnAutoSaveEnabledChanged(bool value) => HasUnsavedChanges = true;
        partial void OnAutoSaveIntervalMinutesChanged(int value) => HasUnsavedChanges = true;
        partial void OnShowNotificationsChanged(bool value) => HasUnsavedChanges = true;
        partial void OnDefaultExportPathChanged(string value) => HasUnsavedChanges = true;
        partial void OnDateFormatChanged(string value) => HasUnsavedChanges = true;
        partial void OnCurrencyFormatChanged(string value) => HasUnsavedChanges = true;
        partial void OnEnableLoggingChanged(bool value) => HasUnsavedChanges = true;
        partial void OnLogLevelChanged(string value) => HasUnsavedChanges = true;
    }
}
