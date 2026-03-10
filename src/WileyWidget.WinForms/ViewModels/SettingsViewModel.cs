using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Simple view model backing the Settings panel with lightweight validation hooks.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly List<string> _validationMessages = new();

        private readonly WileyWidget.Services.Abstractions.ISettingsService? _settingsService;
        private readonly IThemeService? _themeService;

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            WileyWidget.Services.Abstractions.ISettingsService? settingsService = null,
            IThemeService? themeService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService; // optional for DI tests that don't register ISettingsService
            _themeService = themeService;

            _logger.LogDebug("SettingsViewModel constructor started");

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            BrowseExportPathCommand = new RelayCommand(() => BrowseExportPathRequested?.Invoke(this, EventArgs.Empty));
            SaveCommand = new RelayCommand(Save);
            ResetAiCommand = new RelayCommand(ResetAiSettingsToDefaults);

            // Initialize theme from service if available
            if (_themeService != null)
            {
                selectedTheme = ThemeColors.ValidateTheme(_themeService.CurrentTheme, _logger);
            }
            else if (!string.IsNullOrWhiteSpace(_settingsService?.Current?.Theme))
            {
                selectedTheme = ThemeColors.ValidateTheme(_settingsService.Current.Theme, _logger);
            }

            // Ensure the currently active theme is always represented in the selector.
            if (!AvailableThemes.Contains(selectedTheme, StringComparer.OrdinalIgnoreCase))
            {
                AvailableThemes.Insert(0, selectedTheme);
            }

            // Initialize XAI properties from current settings if available
            if (_settingsService?.Current != null)
            {
                var cur = _settingsService.Current;
                EnableAi = cur.EnableAI;
                XaiApiKey = cur.XaiApiKey ?? string.Empty;
                XaiModel = cur.XaiModel;
                XaiApiEndpoint = cur.XaiApiEndpoint;
                XaiTimeout = cur.XaiTimeout;
                XaiMaxTokens = cur.XaiMaxTokens;
                XaiTemperature = cur.XaiTemperature;
                ApplicationFont = cur.ApplicationFont ?? "Segoe UI, 9pt";
                defaultExportPath = NormalizeExportPath(cur.DefaultExportPath);
                cur.DefaultExportPath = defaultExportPath;
            }

            EnsureDefaultExportPath();

            _logger.LogInformation("SettingsViewModel initialized with default export path: {DefaultExportPath}", DefaultExportPath);
        }

        /// <summary>
        /// Available Syncfusion WinForms themes. User selection is applied globally via SfSkinManager.ApplicationVisualTheme.
        /// Each theme cascades to all controls in the application automatically.
        /// Supported in this workspace: Office 2016 (Black, White, DarkGray, Colorful), Office2019Colorful, and HighContrastBlack.
        /// </summary>
        public ObservableCollection<string> AvailableThemes { get; } = new(ThemeColors.GetSupportedThemes());

        public IReadOnlyList<string> Themes => AvailableThemes;

        [ObservableProperty]
        private string appTitle = "Wiley Widget Settings";

        [ObservableProperty]
        private string selectedTheme = ThemeColors.DefaultTheme;

        partial void OnSelectedThemeChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            _logger.LogInformation("SelectedTheme changed to: {Theme}", value);

            if (_themeService != null)
            {
                _themeService.ApplyTheme(value);
            }
            else if (_settingsService != null)
            {
                _settingsService.Current.Theme = ThemeColors.ValidateTheme(value, _logger);
                _settingsService.Save();
            }

            MarkDirty();
        }

        [ObservableProperty]
        private bool openEditFormsDocked;

        [ObservableProperty]
        private bool useDemoData;

        [ObservableProperty]
        private int autoSaveIntervalMinutes = 5;

        [ObservableProperty]
        private string applicationFont = "Segoe UI, 9pt";

        [ObservableProperty]
        private string logLevel = "Information";

        [ObservableProperty]
        private string dateFormat = "yyyy-MM-dd";

        [ObservableProperty]
        private string currencyFormat = "C2";

        [ObservableProperty]
        private string defaultExportPath = BuildDefaultExportPath();

        // XAI / AI settings
        private bool enableAi = true;

        public bool EnableAi
        {
            get => enableAi;
            set
            {
                if (SetProperty(ref enableAi, value))
                {
                    OnEnableAiChanged(value);
                }
            }
        }

        [ObservableProperty]
        private string xaiApiKey = string.Empty;

        [ObservableProperty]
        private string syncfusionLicenseKey = string.Empty;

        [ObservableProperty]
        private string xaiModel = "grok-4.1";

        [ObservableProperty]
        private string xaiApiEndpoint = "https://api.x.ai/v1";

        [ObservableProperty]
        private int xaiTimeout = 30;

        [ObservableProperty]
        private int xaiMaxTokens = 2000;

        [ObservableProperty]
        private double xaiTemperature = 0.7;

        [ObservableProperty]
        private bool hasUnsavedChanges;

        public IAsyncRelayCommand LoadCommand { get; }

        public IRelayCommand BrowseExportPathCommand { get; }

        public IRelayCommand SaveCommand { get; }

        public IRelayCommand ResetAiCommand { get; }

        public event EventHandler? BrowseExportPathRequested;

        partial void OnAppTitleChanged(string value)
        {
            _logger.LogDebug("AppTitle changed to: {AppTitle}", value);
            MarkDirty();
        }

        partial void OnOpenEditFormsDockedChanged(bool value)
        {
            _logger.LogInformation("OpenEditFormsDocked changed to: {OpenEditFormsDocked}", value);
            MarkDirty();
        }

        partial void OnUseDemoDataChanged(bool value)
        {
            _logger.LogInformation("UseDemoData changed to: {UseDemoData}", value);
            MarkDirty();
        }

        partial void OnAutoSaveIntervalMinutesChanged(int value)
        {
            _logger.LogInformation("AutoSaveIntervalMinutes changed to: {AutoSaveIntervalMinutes}", value);
            MarkDirty();
        }

        partial void OnApplicationFontChanged(string value)
        {
            _logger.LogInformation("ApplicationFont changed to: {ApplicationFont}", value);
            if (_settingsService != null)
            {
                _settingsService.Current.ApplicationFont = value;
            }
            MarkDirty();
        }

        partial void OnLogLevelChanged(string value)
        {
            _logger.LogInformation("LogLevel changed to: {LogLevel}", value);
            MarkDirty();
        }

        partial void OnDateFormatChanged(string value)
        {
            _logger.LogDebug("DateFormat changed to: {DateFormat}", value);
            MarkDirty();
        }

        partial void OnCurrencyFormatChanged(string value)
        {
            _logger.LogDebug("CurrencyFormat changed to: {CurrencyFormat}", value);
            MarkDirty();
        }

        partial void OnDefaultExportPathChanged(string value)
        {
            _logger.LogInformation("DefaultExportPath changed to: {DefaultExportPath}", value);
            if (_settingsService != null)
            {
                _settingsService.Current.DefaultExportPath = value;
            }
            MarkDirty();
        }

        private void MarkDirty()
        {
            HasUnsavedChanges = true;
        }

        private void OnEnableAiChanged(bool value)
        {
            _logger.LogInformation("EnableAI changed to: {EnableAI}", value);
            if (_settingsService != null)
            {
                _settingsService.Current.EnableAI = value;
            }
            MarkDirty();
        }

        partial void OnXaiApiKeyChanged(string value)
        {
            // Never log raw API keys - log length only
            _logger.LogInformation("XAI API key updated (length: {Length})", value?.Length ?? 0);
            if (_settingsService != null)
            {
                _settingsService.Current.XaiApiKey = string.IsNullOrWhiteSpace(value) ? null : value;
            }
            MarkDirty();
        }

        partial void OnSyncfusionLicenseKeyChanged(string value)
        {
            // Never log raw license keys - log length only
            _logger.LogInformation("Syncfusion license key updated (length: {Length})", value?.Length ?? 0);
            MarkDirty();
        }

        partial void OnXaiModelChanged(string value)
        {
            _logger.LogInformation("XAI model changed to: {Model}", value);
            if (_settingsService != null)
            {
                _settingsService.Current.XaiModel = value;
            }
            MarkDirty();
        }

        partial void OnXaiApiEndpointChanged(string value)
        {
            _logger.LogInformation("XAI API endpoint changed to: {Endpoint}", value);
            if (_settingsService != null)
            {
                _settingsService.Current.XaiApiEndpoint = value;
            }
            MarkDirty();
        }

        partial void OnXaiTimeoutChanged(int value)
        {
            _logger.LogInformation("XAI timeout changed to: {Timeout}s", value);
            if (_settingsService != null)
            {
                _settingsService.Current.XaiTimeout = value;
            }
            MarkDirty();
        }

        partial void OnXaiMaxTokensChanged(int value)
        {
            _logger.LogInformation("XAI max tokens changed to: {MaxTokens}", value);
            if (_settingsService != null)
            {
                _settingsService.Current.XaiMaxTokens = value;
            }
            MarkDirty();
        }

        partial void OnXaiTemperatureChanged(double value)
        {
            _logger.LogInformation("XAI temperature changed to: {Temperature}", value);
            if (_settingsService != null)
            {
                _settingsService.Current.XaiTemperature = value;
            }
            MarkDirty();
        }

        private async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Loading settings");

            if (_settingsService != null)
            {
                await _settingsService.LoadAsync();

                // Populate view model fields without marking dirty
                var cur = _settingsService.Current;
                HasUnsavedChanges = false; // clear before assigning so property Setters don't mark dirty

                EnableAi = cur.EnableAI;
                XaiApiKey = cur.XaiApiKey ?? string.Empty;
                XaiModel = cur.XaiModel;
                XaiApiEndpoint = cur.XaiApiEndpoint;
                XaiTimeout = cur.XaiTimeout;
                XaiMaxTokens = cur.XaiMaxTokens;
                XaiTemperature = cur.XaiTemperature;
                ApplicationFont = cur.ApplicationFont ?? "Segoe UI, 9pt";
                DefaultExportPath = NormalizeExportPath(cur.DefaultExportPath);

                EnsureDefaultExportPath();

                HasUnsavedChanges = false;
            }

            _logger.LogDebug("Settings loaded successfully");
        }

        private void Save()
        {
            _logger.LogInformation("Saving settings");

            EnsureDefaultExportPath();

            if (!ValidateSettings())
            {
                _logger.LogWarning("Settings validation failed - not saving");
                return;
            }

            try
            {
                if (_settingsService != null)
                {
                    _settingsService.Save();
                    HasUnsavedChanges = false;
                    _logger.LogInformation("Settings saved successfully");
                }
                else
                {
                    _logger.LogWarning("SettingsService not available - cannot persist settings");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
            }
        }

        private void ResetAiSettingsToDefaults()
        {
            _logger.LogInformation("Resetting XAI settings to defaults");
            XaiApiKey = string.Empty;
            XaiModel = "grok-4.1";
            XaiApiEndpoint = "https://api.x.ai/v1";
            XaiTimeout = 30;
            XaiMaxTokens = 2000;
            XaiTemperature = 0.7;
            MarkDirty();
        }

        private static string BuildDefaultExportPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "WileyWidget",
                "Exports");
        }

        private static string NormalizeExportPath(string? exportPath)
        {
            return string.IsNullOrWhiteSpace(exportPath)
                ? BuildDefaultExportPath()
                : exportPath.Trim();
        }

        private void EnsureDefaultExportPath()
        {
            var effectiveExportPath = NormalizeExportPath(DefaultExportPath);

            if (!string.Equals(DefaultExportPath, effectiveExportPath, StringComparison.Ordinal))
            {
                DefaultExportPath = effectiveExportPath;
                _logger.LogInformation("Default export path fallback applied: {DefaultExportPath}", DefaultExportPath);
            }

            if (_settingsService?.Current != null)
            {
                _settingsService.Current.DefaultExportPath = effectiveExportPath;
            }
        }

        /// <summary>
        /// Simple validation hook for the Settings panel.
        /// </summary>
        public bool ValidateSettings()
        {
            _logger.LogDebug("Validating settings");
            _validationMessages.Clear();

            EnsureDefaultExportPath();

            if (string.IsNullOrWhiteSpace(AppTitle))
            {
                _validationMessages.Add("Application title cannot be empty.");
                _logger.LogWarning("Validation failed: AppTitle is empty");
            }

            if (string.IsNullOrWhiteSpace(DateFormat))
            {
                _validationMessages.Add("Date format cannot be empty.");
                _logger.LogWarning("Validation failed: Date format is empty");
            }

            if (string.IsNullOrWhiteSpace(CurrencyFormat))
            {
                _validationMessages.Add("Currency format cannot be empty.");
                _logger.LogWarning("Validation failed: Currency format is empty");
            }

            // Validate XAI settings when AI is enabled
            if (EnableAi)
            {
                if (string.IsNullOrWhiteSpace(XaiApiKey))
                {
                    _validationMessages.Add("API key is required when AI is enabled.");
                    _logger.LogWarning("Validation failed: XAI API key required when AI enabled");
                }

                if (string.IsNullOrWhiteSpace(XaiApiEndpoint) || !Uri.TryCreate(XaiApiEndpoint, UriKind.Absolute, out var _))
                {
                    _validationMessages.Add("Valid XAI API endpoint is required when AI is enabled.");
                    _logger.LogWarning("Validation failed: XAI API endpoint invalid");
                }

                if (XaiTimeout <= 0 || XaiTimeout > 300)
                {
                    _validationMessages.Add("XAI timeout must be between 1 and 300 seconds.");
                    _logger.LogWarning("Validation failed: XAI timeout out of range");
                }

                if (XaiMaxTokens <= 0 || XaiMaxTokens > 65536)
                {
                    _validationMessages.Add("XAI max tokens must be a positive value under 65536.");
                    _logger.LogWarning("Validation failed: XAI max tokens out of range");
                }

                if (XaiTemperature < 0.0 || XaiTemperature > 1.0)
                {
                    _validationMessages.Add("XAI temperature must be between 0.0 and 1.0.");
                    _logger.LogWarning("Validation failed: XAI temperature out of range");
                }
            }

            var isValid = _validationMessages.Count == 0;
            _logger.LogInformation("Settings validation completed: {IsValid}, Errors: {ErrorCount}", isValid, _validationMessages.Count);
            return isValid;
        }

        public List<string> GetValidationSummary() => new(_validationMessages);
    }
}
