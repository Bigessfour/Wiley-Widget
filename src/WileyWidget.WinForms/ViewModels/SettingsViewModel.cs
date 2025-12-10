using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private readonly List<string> _validationMessages = new();

        public SettingsViewModel()
        {
            LoadCommand = new AsyncRelayCommand(LoadAsync);
            BrowseExportPathCommand = new RelayCommand(() => BrowseExportPathRequested?.Invoke(this, EventArgs.Empty));
            Themes = Enum.GetValues<AppTheme>().ToList();
            DefaultExportPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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

        partial void OnAppTitleChanged(string value) => MarkDirty();
        partial void OnOpenEditFormsDockedChanged(bool value) => MarkDirty();
        partial void OnUseDemoDataChanged(bool value) => MarkDirty();
        partial void OnAutoSaveIntervalMinutesChanged(int value) => MarkDirty();
        partial void OnLogLevelChanged(string value) => MarkDirty();
        partial void OnDateFormatChanged(string value) => MarkDirty();
        partial void OnCurrencyFormatChanged(string value) => MarkDirty();
        partial void OnDefaultExportPathChanged(string value) => MarkDirty();

        private void MarkDirty()
        {
            HasUnsavedChanges = true;
        }

        private Task LoadAsync()
        {
            // Placeholder for future persistence integration; currently just clears dirty flag.
            HasUnsavedChanges = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Simple validation hook for the Settings panel.
        /// </summary>
        public bool ValidateSettings()
        {
            _validationMessages.Clear();

            if (string.IsNullOrWhiteSpace(DateFormat))
            {
                _validationMessages.Add("Date format cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(CurrencyFormat))
            {
                _validationMessages.Add("Currency format cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(DefaultExportPath))
            {
                _validationMessages.Add("Export path is required.");
            }

            return _validationMessages.Count == 0;
        }

        public List<string> GetValidationSummary() => new(_validationMessages);
    }
}
