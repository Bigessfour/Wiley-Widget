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
            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

            _logger.LogInformation("MainViewModel constructed with IDashboardService");
        }

        /// <summary>
        /// Load dashboard data from services.
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

                // Load dashboard items
                _logger.LogDebug("MainViewModel: LoadDataAsync - Calling GetDashboardItemsAsync");
                var dashboardItems = await _dashboardService.GetDashboardItemsAsync(cancellationToken);
                _logger.LogInformation("MainViewModel: LoadDataAsync - Retrieved {Count} dashboard items", dashboardItems?.Count() ?? 0);

                cancellationToken.ThrowIfCancellationRequested();

                // Parse dashboard items into properties
                ActivityItems.Clear();
                foreach (DashboardItem item in dashboardItems!)
                {
                    if (cancellationToken.IsCancellationRequested) break;

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

                    ActivityItems.Add(new ActivityItem
                    {
                        Timestamp = DateTime.Now,
                        Activity = $"{item.Title}: {item.Value}",
                        Category = item.Category ?? "General"
                    });
                }

                LastUpdateTime = DateTime.Now.ToString("g", System.Globalization.CultureInfo.CurrentCulture);

                _logger.LogInformation("MainViewModel: LoadDataAsync - Dashboard data loaded successfully. {ItemCount} items processed", ActivityItems.Count);
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
            }
            finally
            {
                IsLoading = false;
            }
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

        public void ProcessDashboard(IEnumerable<DashboardItem> dashboardItems)
        {
            ArgumentNullException.ThrowIfNull(dashboardItems);

            foreach (DashboardItem item in dashboardItems)
            {
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

