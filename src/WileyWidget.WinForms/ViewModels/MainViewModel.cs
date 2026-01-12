using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the main dashboard. Orchestrates UI interactions and delegates
    /// all business logic to services (MVVM pattern).
    /// Implements IDisposable for proper service cleanup.
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IDashboardService _dashboardService;
        private readonly IAILoggingService _aiLoggingService;
        private bool _disposed;

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

        /// <summary>
        /// Collection of recent activity items for the dashboard.
        /// Bound to data grids in the MainForm.
        /// </summary>
        public ObservableCollection<ActivityItem> ActivityItems { get; } = new();

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }

        public MainViewModel(ILogger<MainViewModel> logger, IDashboardService dashboardService, IAILoggingService aiLoggingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);

            _logger.LogInformation("MainViewModel constructed with IDashboardService");
        }

        /// <summary>
        /// Refresh dashboard data by clearing current data and reloading from services.
        /// </summary>
        private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("MainViewModel: RefreshDataAsync - Clearing and reloading dashboard data");

                // Clear existing data
                ActivityItems.Clear();
                TotalBudget = 0;
                TotalActual = 0;
                Variance = 0;
                ActiveAccountCount = 0;
                TotalDepartments = 0;
                ErrorMessage = null;

                // Reload data
                await LoadDataAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh dashboard data");
                _aiLoggingService.LogError("Dashboard Refresh", ex);
                throw;
            }
        }

        /// <summary>
        /// Load dashboard data from services.
        /// Calls IDashboardService.GetDashboardDataAsync() if available, otherwise falls back to GetDashboardItemsAsync().
        /// Includes sample data fallback for offline/design-time scenarios.
        /// </summary>
        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _logger.LogInformation("MainViewModel: LoadDataAsync - Loading dashboard data");

                // Early cancellation check
                cancellationToken.ThrowIfCancellationRequested();

                // Try to get data from dashboard service
                IEnumerable<DashboardItem>? dashboardItems = null;

                try
                {
                    // Try GetDashboardDataAsync first (if available)
                    var getDashboardDataMethod = _dashboardService.GetType().GetMethod("GetDashboardDataAsync");
                    if (getDashboardDataMethod != null)
                    {
                        _logger.LogDebug("MainViewModel: Calling GetDashboardDataAsync");
                        var task = (Task<IEnumerable<DashboardItem>>?)getDashboardDataMethod.Invoke(
                            _dashboardService,
                            new object[] { cancellationToken });
                        dashboardItems = await (task ?? Task.FromResult(Enumerable.Empty<DashboardItem>()));
                    }
                    else
                    {
                        // Fall back to GetDashboardItemsAsync
                        _logger.LogDebug("MainViewModel: Calling GetDashboardItemsAsync");
                        dashboardItems = await _dashboardService.GetDashboardItemsAsync(cancellationToken);
                    }
                }
                catch (Exception serviceEx)
                {
                    _logger.LogWarning(serviceEx, "Dashboard service call failed, attempting fallback to sample data");
                    _aiLoggingService.LogError("Dashboard Service", serviceEx);

                    // Try sample data fallback
                    dashboardItems = GetSampleDashboardData();
                    ErrorMessage = "Using sample data (service unavailable)";
                }

                if (dashboardItems == null || !dashboardItems.Any())
                {
                    _logger.LogWarning("No dashboard data returned from service, using sample data");
                    dashboardItems = GetSampleDashboardData();
                    if (string.IsNullOrEmpty(ErrorMessage))
                        ErrorMessage = "No data available, showing sample data";
                }

                _logger.LogInformation("MainViewModel: Retrieved {Count} dashboard items", dashboardItems?.Count() ?? 0);

                cancellationToken.ThrowIfCancellationRequested();

                // Process dashboard items into properties
                ProcessDashboard(dashboardItems!);

                LastUpdateTime = DateTime.Now.ToString("g", System.Globalization.CultureInfo.CurrentCulture);

                _logger.LogInformation("MainViewModel: Dashboard data loaded successfully. {ItemCount} items processed", ActivityItems.Count);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Dashboard data loading canceled");
                ErrorMessage = null; // Cancellation is expected
                throw; // Re-throw to propagate cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard data");
                _aiLoggingService.LogError("Dashboard Load", ex);
                ErrorMessage = $"Failed to load dashboard: {ex.Message}";

                // Attempt to show sample data even on error
                try
                {
                    ProcessDashboard(GetSampleDashboardData());
                    _logger.LogInformation("Loaded sample data after error");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Failed to load sample data fallback");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Returns sample dashboard data for design-time or offline scenarios.
        /// </summary>
        private IEnumerable<DashboardItem> GetSampleDashboardData()
        {
            return new[]
            {
                new DashboardItem
                {
                    Title = "Total Budget",
                    Value = "1250000.00",
                    Category = "budget",
                    Description = "Total allocated budget for current fiscal year"
                },
                new DashboardItem
                {
                    Title = "Total Actual",
                    Value = "987500.50",
                    Category = "actual",
                    Description = "Total actual spending to date"
                },
                new DashboardItem
                {
                    Title = "Variance",
                    Value = "262499.50",
                    Category = "variance",
                    Description = "Remaining budget (under/over)"
                },
                new DashboardItem
                {
                    Title = "Active Accounts",
                    Value = "42",
                    Category = "accounts",
                    Description = "Number of active municipal accounts"
                },
                new DashboardItem
                {
                    Title = "Departments",
                    Value = "8",
                    Category = "departments",
                    Description = "Total departments tracked"
                },
                new DashboardItem
                {
                    Title = "Recent Activity",
                    Value = "Budget entry updated",
                    Category = "activity",
                    Description = "Latest system activity"
                },
                new DashboardItem
                {
                    Title = "Sample Transaction",
                    Value = "Invoice #1234 processed",
                    Category = "activity",
                    Description = "Sample transaction for testing"
                }
            };
        }

        /// <summary>
        /// Initialize the view model by loading data.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("MainViewModel: InitializeAsync called");
                await LoadDataAsync(cancellationToken);
                _logger.LogInformation("MainViewModel: InitializeAsync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel initialization failed");
                _aiLoggingService.LogError("MainViewModel Initialize", ex);
                throw;
            }
        }

        /// <summary>
        /// Processes dashboard items and updates all ViewModel properties.
        /// Clears ActivityItems before populating with new data.
        /// </summary>
        /// <param name="dashboardItems">Collection of dashboard items to process</param>
        public void ProcessDashboard(IEnumerable<DashboardItem> dashboardItems)
        {
            ArgumentNullException.ThrowIfNull(dashboardItems);

            // Clear existing activity items before processing new data
            ActivityItems.Clear();

            foreach (DashboardItem item in dashboardItems)
            {
                if (item == null) continue;

                // Map items to properties based on category/title
                switch (item.Category?.ToLowerInvariant() ?? string.Empty)
                {
                    case "budget":
                        if (decimal.TryParse(item.Value, out var budget))
                            TotalBudget = budget;
                        break;
                    case "actual":
                        if (decimal.TryParse(item.Value, out var actual))
                            TotalActual = actual;
                        break;
                    case "variance":
                        if (decimal.TryParse(item.Value, out var varianceValue))
                            Variance = varianceValue;
                        break;
                    case "accounts":
                        if (int.TryParse(item.Value, out var accounts))
                            ActiveAccountCount = accounts;
                        break;
                    case "departments":
                        if (int.TryParse(item.Value, out var depts))
                            TotalDepartments = depts;
                        break;
                }

                // Add to activity items for grid display
                ActivityItems.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now,
                    Activity = $"{item.Title}: {item.Value}",
                    Category = item.Category ?? "General"
                });
            }

            LastUpdateTime = DateTime.Now.ToString("g", System.Globalization.CultureInfo.CurrentCulture);

            _logger.LogInformation("MainViewModel: ProcessDashboard - Dashboard data processed. {ItemCount} items processed", ActivityItems.Count);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("MainViewModel disposing");
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

