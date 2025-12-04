using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the settings view. Orchestrates UI interactions and delegates
    /// all business logic to ISettingsManagementService (MVVM purity - Phase 3 refactoring).
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly ISettingsManagementService _settingsService;

        [ObservableProperty]
        private string title = "Settings";

        [ObservableProperty]
        private string applicationName = string.Empty;

        [ObservableProperty]
        private string databaseConnectionString = string.Empty;

        [ObservableProperty]
        private string logLevel = "Information";

        [ObservableProperty]
        private bool enableTelemetry = false;

        [ObservableProperty]
        private int cacheExpirationMinutes = 60;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private string? successMessage;

        public IAsyncRelayCommand LoadCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }

        public SettingsViewModel(ILogger<SettingsViewModel> logger, ISettingsManagementService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            try
            {
                LoadCommand = new AsyncRelayCommand(LoadAsync);
                SaveCommand = new AsyncRelayCommand(SaveAsync);
                _logger.LogInformation("SettingsViewModel constructed with ISettingsManagementService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed during SettingsViewModel construction");
                throw;
            }
        }

        private async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _logger.LogInformation("Loading settings");

                // Delegate business logic to service
                var settings = await _settingsService.LoadSettingsAsync(cancellationToken);

                // Update UI properties
                DatabaseConnectionString = settings.DatabaseConnectionString;
                ApplicationName = settings.ApplicationName;
                LogLevel = settings.LogLevel;
                EnableTelemetry = settings.EnableTelemetry;
                CacheExpirationMinutes = settings.CacheExpirationMinutes;

                _logger.LogInformation("Settings loaded successfully");
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "Settings loading canceled");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");
                ErrorMessage = "Failed to load settings";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                SuccessMessage = null;
                _logger.LogInformation("Saving settings");

                // Create DTO from UI properties
                var settings = new SettingsDto(
                    DatabaseConnectionString,
                    ApplicationName,
                    LogLevel,
                    EnableTelemetry,
                    CacheExpirationMinutes
                );

                // Delegate business logic to service
                var result = await _settingsService.SaveSettingsAsync(settings, cancellationToken);

                if (!result.Success)
                {
                    ErrorMessage = string.Join("; ", result.ValidationErrors);
                    _logger.LogWarning("Settings validation failed: {Errors}", ErrorMessage);
                    return;
                }

                SuccessMessage = "Settings saved successfully";
                _logger.LogInformation("Settings saved successfully");
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "Settings save operation canceled");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                ErrorMessage = "Failed to save settings";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
