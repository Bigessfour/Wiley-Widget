using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the main dashboard. Orchestrates UI interactions and delegates
    /// all business logic to IMainDashboardService (MVVM purity - Phase 3 refactoring).
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IMainDashboardService _dashboardService;
        private readonly IAILoggingService _aiLoggingService;

        [ObservableProperty]
        private string title = "Wiley Widget — WinForms + .NET 9";

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal variance;

        [ObservableProperty]
        private int activeAccountCount;

        [ObservableProperty]
        private int totalDepartments;

        [ObservableProperty]
        private string? lastUpdateTime;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        public IAsyncRelayCommand LoadDataCommand { get; }

        public MainViewModel(ILogger<MainViewModel> logger, IMainDashboardService dashboardService, IAILoggingService aiLoggingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));

            try
            {
                LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
                _logger.LogInformation("MainViewModel constructed with IMainDashboardService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed during MainViewModel construction");
                throw;
            }
        }

        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _logger.LogInformation("Loading dashboard data");

                // Delegate business logic to service
                var data = await _dashboardService.LoadDashboardDataAsync(cancellationToken);

                // Update UI properties
                TotalBudget = data.TotalBudget;
                TotalActual = data.TotalActual;
                Variance = data.Variance;
                ActiveAccountCount = data.ActiveAccountCount;
                TotalDepartments = data.TotalDepartments;
                LastUpdateTime = data.LastUpdateTime;

                _logger.LogInformation("Dashboard data loaded: {ActiveAccounts} accounts, {Departments} departments, Budget: {Budget:C}",
                    ActiveAccountCount, TotalDepartments, TotalBudget);
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "Dashboard data loading canceled");
                _aiLoggingService.LogError("Dashboard Load", oce);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard data");
                ErrorMessage = "Failed to load dashboard data";
                _aiLoggingService.LogError("Dashboard Load", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await LoadDataAsync(cancellationToken);
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "MainViewModel initialization canceled");
                _aiLoggingService.LogError("MainViewModel Initialize", oce);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel failed during InitializeAsync");
                _aiLoggingService.LogError("MainViewModel Initialize", ex);
                throw;
            }
        }
    }
}
