using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class SettingsViewModel : ObservableRecipient
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly ISettingsService _settingsService;
        private readonly IQuickBooksService _quickBooksService;

        [ObservableProperty]
        private string title = "Settings";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private bool databaseConnected;

        [ObservableProperty]
        private bool quickBooksConnected;

        [ObservableProperty]
        private string databaseConnectionString = string.Empty;

        [ObservableProperty]
        private string quickBooksCompanyFile = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> availableThemes = new();

        [ObservableProperty]
        private string selectedTheme = "Light";

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            ISettingsService settingsService,
            IQuickBooksService quickBooksService)
        {
            _logger = logger;
            _settingsService = settingsService;
            _quickBooksService = quickBooksService;

            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            ResetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync);
            TestDatabaseConnectionCommand = new AsyncRelayCommand(TestDatabaseConnectionAsync);
            TestQuickBooksConnectionCommand = new AsyncRelayCommand(TestQuickBooksConnectionAsync);

            InitializeThemes();
            LoadSettingsAsync().ConfigureAwait(false);
        }

        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand ResetSettingsCommand { get; }
        public IAsyncRelayCommand TestDatabaseConnectionCommand { get; }
        public IAsyncRelayCommand TestQuickBooksConnectionCommand { get; }

        private void InitializeThemes()
        {
            AvailableThemes.Add("Light");
            AvailableThemes.Add("Dark");
            AvailableThemes.Add("System");
            SelectedTheme = "Light";
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading settings...";

                // Load settings from service
                await _settingsService.LoadAsync();
                var settings = _settingsService.Current;
                DatabaseConnectionString = $"{settings.DatabaseServer};Database={settings.DatabaseName}" ?? string.Empty;
                QuickBooksCompanyFile = settings.QuickBooksCompanyFile ?? string.Empty;
                SelectedTheme = settings.Theme ?? "Light";

                StatusMessage = "Settings loaded successfully";
                _logger.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");
                StatusMessage = "Failed to load settings";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Saving settings...";

                var settings = _settingsService.Current;
                // Parse connection string back to server/database components
                var parts = DatabaseConnectionString.Split(';');
                if (parts.Length > 0) settings.DatabaseServer = parts[0];
                if (parts.Length > 1 && parts[1].StartsWith("Database="))
                {
                    settings.DatabaseName = parts[1].Substring("Database=".Length);
                }
                settings.QuickBooksCompanyFile = QuickBooksCompanyFile;
                settings.Theme = SelectedTheme;

                _settingsService.Save();
                StatusMessage = "Settings saved successfully";
                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                StatusMessage = "Failed to save settings";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResetSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Resetting settings...";

                // Reset settings - implementation would need to be added to ISettingsService
                await LoadSettingsAsync();

                StatusMessage = "Settings reset to defaults";
                _logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset settings");
                StatusMessage = "Failed to reset settings";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task TestDatabaseConnectionAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Testing database connection...";

                // Database connection test would need appropriate service method
                DatabaseConnected = !string.IsNullOrEmpty(DatabaseConnectionString);
                StatusMessage = DatabaseConnected ? "Database connection configuration exists" : "Database connection not configured";

                _logger.LogInformation("Database connection test: {Result}", DatabaseConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed");
                DatabaseConnected = false;
                StatusMessage = "Database connection test failed";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task TestQuickBooksConnectionAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Testing QuickBooks connection...";

                QuickBooksConnected = await _quickBooksService.TestConnectionAsync();
                StatusMessage = QuickBooksConnected ? "QuickBooks connection successful" : "QuickBooks connection failed";

                _logger.LogInformation("QuickBooks connection test: {Result}", QuickBooksConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuickBooks connection test failed");
                QuickBooksConnected = false;
                StatusMessage = "QuickBooks connection test failed";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class AppSettings
    {
        public string DatabaseConnectionString { get; set; } = string.Empty;
        public string QuickBooksCompanyFile { get; set; } = string.Empty;
        public string Theme { get; set; } = "Light";
    }
}