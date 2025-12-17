using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Simple view model backing the Settings panel with lightweight validation hooks.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly List<string> _validationMessages = new();

        public SettingsViewModel(ILogger<SettingsViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogDebug("SettingsViewModel constructor started");

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            BrowseExportPathCommand = new RelayCommand(() => BrowseExportPathRequested?.Invoke(this, EventArgs.Empty));
            Themes = Enum.GetValues<AppTheme>().ToList();
            DefaultExportPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            _logger.LogInformation("SettingsViewModel initialized with default export path: {DefaultExportPath}", DefaultExportPath);
        }

        public IReadOnlyList<AppTheme> Themes { get; }

        [ObservableProperty]
        private string appTitle = "Wiley Widget Settings";

        [ObservableProperty]
        private bool openEditFormsDocked;

        [ObservableProperty]
        private bool useDemoData;

        [ObservableProperty]
        private int autoSaveIntervalMinutes = 5;

        [ObservableProperty]
        private string logLevel = "Information";

        [ObservableProperty]
        private string dateFormat = "yyyy-MM-dd";

        [ObservableProperty]
        private string currencyFormat = "C2";

        [ObservableProperty]
        private string defaultExportPath = string.Empty;

        [ObservableProperty]
        private bool hasUnsavedChanges;

        public IAsyncRelayCommand LoadCommand { get; }

        public IRelayCommand BrowseExportPathCommand { get; }

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

        private Task LoadAsync()
        {
            _logger.LogInformation("Loading settings");
            // Placeholder for future persistence integration; currently just clears dirty flag.
            HasUnsavedChanges = false;
            _logger.LogDebug("Settings loaded successfully");
            return Task.CompletedTask;
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

            var isValid = _validationMessages.Count == 0;
            _logger.LogInformation("Settings validation completed: {IsValid}, Errors: {ErrorCount}", isValid, _validationMessages.Count);
            return isValid;
        }

        public List<string> GetValidationSummary() => new(_validationMessages);
    }
}
