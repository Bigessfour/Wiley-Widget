using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.WinForms.Services;

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
                selectedTheme = _themeService.CurrentTheme;
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
            }

            _logger.LogInformation("SettingsViewModel initialized with default export path: {DefaultExportPath}", DefaultExportPath);
        }

        /// <summary>
        /// Available Syncfusion WinForms themes. User selection is applied globally via SfSkinManager.ApplicationVisualTheme.
        /// Each theme cascades to all controls in the application automatically.
        /// Per Syncfusion documentation: Only Office2016Theme, Office2019Theme, and HighContrastTheme are supported.
        /// FluentTheme and MaterialTheme are NOT available in Windows Forms (Web/Blazor only).
        /// </summary>
        public IReadOnlyList<string> Themes { get; } = new List<string>
        {
            "Office2019Colorful",
            "Office2019Black",
            "Office2019White"
        };

        [ObservableProperty]
        private string appTitle = "Wiley Widget Settings";

        [ObservableProperty]
        private string selectedTheme = "Office2019Colorful";

        partial void OnSelectedThemeChanged(string value)
        {
            _logger.LogInformation("SelectedTheme changed to: {Theme}", value);
            _themeService?.ApplyTheme(value);
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
        private string defaultExportPath = string.Empty;

        // XAI / AI settings
        [ObservableProperty]
        private bool enableAi = false;

        [ObservableProperty]
        private string xaiApiKey = string.Empty;

        [ObservableProperty]
        private string xaiModel = "grok-4-0709";

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
            MarkDirty();
        }

        private void MarkDirty()
        {
            HasUnsavedChanges = true;
        }

        partial void OnEnableAiChanged(bool value)
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

                HasUnsavedChanges = false;
            }

            _logger.LogDebug("Settings loaded successfully");
        }

        private void Save()
        {
            _logger.LogInformation("Saving settings");

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
            XaiModel = "grok-4-0709";
            XaiApiEndpoint = "https://api.x.ai/v1";
            XaiTimeout = 30;
            XaiMaxTokens = 2000;
            XaiTemperature = 0.7;
            MarkDirty();
        }

        /// <summary>
        /// Simple validation hook for the Settings panel.
        /// </summary>
        public bool ValidateSettings()
        {
            _logger.LogDebug("Validating settings");
            _validationMessages.Clear();

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

            if (string.IsNullOrWhiteSpace(DefaultExportPath))
            {
                _validationMessages.Add("Export path is required.");
                _logger.LogWarning("Validation failed: Export path is empty");
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

