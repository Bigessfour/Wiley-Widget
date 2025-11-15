using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class SettingsViewModel : ObservableRecipient, INavigationAware
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
        private string databaseConnectionString;

        [ObservableProperty]
        private string quickBooksCompanyFile;

        [ObservableProperty]
        private ObservableCollection<string> availableThemes = new();

        [ObservableProperty]
        private string selectedTheme;

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
                var settings = await _settingsService.LoadSettingsAsync();
                DatabaseConnectionString = settings.DatabaseConnectionString;
                QuickBooksCompanyFile = settings.QuickBooksCompanyFile;
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

                var settings = new AppSettings
                {
                    DatabaseConnectionString = DatabaseConnectionString,
                    QuickBooksCompanyFile = QuickBooksCompanyFile,
                    Theme = SelectedTheme
                };

                await _settingsService.SaveSettingsAsync(settings);
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

                await _settingsService.ResetToDefaultsAsync();
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

                DatabaseConnected = await _settingsService.TestDatabaseConnectionAsync(DatabaseConnectionString);
                StatusMessage = DatabaseConnected ? "Database connection successful" : "Database connection failed";

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

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Settings");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Settings");
        }
    }

    public class AppSettings
    {
        public string DatabaseConnectionString { get; set; } = string.Empty;
        public string QuickBooksCompanyFile { get; set; } = string.Empty;
        public string Theme { get; set; } = "Light";
    }
}