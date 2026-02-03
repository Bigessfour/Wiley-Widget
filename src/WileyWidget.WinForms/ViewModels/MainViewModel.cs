using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for the main dashboard. Orchestrates UI interactions and delegates
    /// all business logic to services (MVVM pattern).
    /// Implements IDisposable for proper service cleanup.
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject, IDisposable, ILazyLoadViewModel, WileyWidget.Abstractions.ILazyLoadViewModel
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IDashboardService _dashboardService;
        private readonly IAILoggingService _aiLoggingService;
        private readonly IQuickBooksService _quickBooksService;
        private readonly IGlobalSearchService _globalSearchService;
        private readonly IStatusProgressService? _statusProgressService;
        private bool _disposed;

        [ObservableProperty]
        private bool isDataLoaded;

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

        [ObservableProperty]
        private decimal variancePercentage;

        [ObservableProperty]
        private decimal budgetUtilizationPercentage;

        [ObservableProperty]
        private bool isPositiveVariance;

        [ObservableProperty]
        private decimal totalExpenditure;

        [ObservableProperty]
        private decimal remainingBudget;

        [ObservableProperty]
        private string varianceStatusColor = "Green";

        [ObservableProperty]
        private ObservableCollection<DashboardMetric> metrics = new();

        [ObservableProperty]
        private ObservableCollection<MonthlyRevenue> monthlyRevenueData = new();

        [ObservableProperty]
        private decimal totalBudgetGauge;

        [ObservableProperty]
        private decimal revenueGauge;

        [ObservableProperty]
        private decimal expensesGauge;

        [ObservableProperty]
        private decimal netPositionGauge;

        [ObservableProperty]
        private ObservableCollection<DepartmentSummary> departmentSummaries = new();

        [ObservableProperty]
        private ObservableCollection<FundSummary> fundSummaries = new();

        /// <summary>
        /// Collection of recent activity items for the dashboard.
        /// Bound to data grids in the MainForm.
        /// </summary>
        public ObservableCollection<ActivityItem> ActivityItems { get; } = new();

        public string FormattedTotalBudget => TotalBudget.ToString("C", CultureInfo.CurrentCulture);
        public string FormattedTotalActual => TotalActual.ToString("C", CultureInfo.CurrentCulture);
        public string FormattedVariance => Variance >= 0
            ? $"+{Variance.ToString("C", CultureInfo.CurrentCulture)}"
            : Variance.ToString("C", CultureInfo.CurrentCulture);
        public string FormattedVariancePercentage => $"{VariancePercentage:F1}% {(IsPositiveVariance ? "under" : "over")} budget";
        public string FormattedBudgetUtilization => $"{BudgetUtilizationPercentage:F1}% utilized";

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand SyncQuickBooksAccountsCommand { get; }
        public IAsyncRelayCommand<string> GlobalSearchCommand { get; }

        public MainViewModel(ILogger<MainViewModel> logger, IDashboardService dashboardService, IAILoggingService aiLoggingService, IQuickBooksService quickBooksService, IGlobalSearchService globalSearchService, IStatusProgressService? statusProgressService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
            _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
            _globalSearchService = globalSearchService ?? throw new ArgumentNullException(nameof(globalSearchService));
            _statusProgressService = statusProgressService;

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);
            SyncQuickBooksAccountsCommand = new AsyncRelayCommand(SyncQuickBooksAccountsAsync);
            GlobalSearchCommand = new AsyncRelayCommand<string>(PerformGlobalSearchAsync);

            // Guard against design-time initialization — prevent fallback/service calls in VS designer
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                _logger.LogInformation("MainViewModel constructed with IDashboardService and IQuickBooksService (runtime mode)");
            }
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
                    _logger.LogWarning(serviceEx, "Dashboard service call failed, loading fallback sample data");

                    // Load comprehensive fallback data
                    dashboardItems = GetSampleDashboardData();
                    _logger.LogInformation("Loaded fallback dashboard data — {Count} items", dashboardItems?.Count() ?? 0);
                    ErrorMessage = "Using fallback data (service unavailable)";
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

                PopulateMonthlyRevenueData();

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
        /// Uses FallbackDataService for comprehensive, consistent fallback metrics.
        /// </summary>
        private IEnumerable<DashboardItem> GetSampleDashboardData()
        {
            var fallbackData = FallbackDataService.GetFallbackDashboardData();
            return fallbackData.Cast<DashboardItem>();
        }

        private void PopulateMonthlyRevenueData()
        {
            MonthlyRevenueData.Clear();

            var now = DateTime.Now;
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            for (int i = 11; i >= 0; i--)
            {
                var month = now.AddMonths(-i);
                var monthName = monthNames[month.Month - 1];
                var baseAmount = 100000m + (i * 15000m);
                var previousAmount = Math.Max(baseAmount - 15000m, 0m);

                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = monthName,
                    Amount = baseAmount,
                    PreviousMonthAmount = previousAmount,
                    Year = month.Year,
                    MonthNumber = month.Month
                });
            }

            _logger.LogDebug("PopulateMonthlyRevenueData: {Count} months loaded", MonthlyRevenueData.Count);
        }

        /// <summary>
        /// Initialize the view model by loading data.
        /// Guards against design-time initialization to prevent errors in VS designer.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Skip initialization in design-time (VS designer) to avoid errors
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                {
                    _logger.LogDebug("MainViewModel: Design-time mode detected, skipping async initialization");
                    return;
                }

                _logger.LogInformation("MainViewModel: InitializeAsync called (runtime mode)");
                await LoadDataAsync(cancellationToken);
                IsDataLoaded = true;
                _logger.LogInformation("MainViewModel: InitializeAsync completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("MainViewModel initialization canceled");
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel initialization failed - continuing with empty dashboard data");
                ErrorMessage = "Dashboard data load failed — showing empty dashboard";

                ActivityItems.Clear();
                Metrics.Clear();
                MonthlyRevenueData.Clear();
                DepartmentSummaries.Clear();
                FundSummaries.Clear();

                TotalBudget = 0;
                TotalActual = 0;
                Variance = 0;
                ActiveAccountCount = 0;
                TotalDepartments = 0;
                TotalBudgetGauge = 0;
                RevenueGauge = 0;
                ExpensesGauge = 0;
                NetPositionGauge = 0;
            }
        }

        public async Task OnVisibilityChangedAsync(bool isVisible)
        {
            if (isVisible && !IsDataLoaded && !IsLoading)
            {
                _logger.LogInformation("Panel visible for first time; triggering lazy load");
                await LoadDataAsync(CancellationToken.None);
                IsDataLoaded = true;
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
            Metrics.Clear();

            foreach (DashboardItem item in dashboardItems)
            {
                if (item == null) continue;

                // Map items to properties based on category/title
                switch (item.Category?.ToLowerInvariant() ?? string.Empty)
                {
                    case "budget":
                        if (decimal.TryParse(item.Value, out var budget))
                        {
                            TotalBudget = budget;
                            Metrics.Add(new DashboardMetric
                            {
                                Name = item.Title,
                                Title = item.Title,
                                Category = item.Category ?? "Budget",
                                Value = (double)budget,
                                Unit = "$",
                                Trend = "Stable",
                                ChangePercent = 0,
                                Description = item.Description ?? string.Empty
                            });
                        }
                        break;
                    case "actual":
                        if (decimal.TryParse(item.Value, out var actual))
                        {
                            TotalActual = actual;
                            Metrics.Add(new DashboardMetric
                            {
                                Name = item.Title,
                                Title = item.Title,
                                Category = item.Category ?? "Actual",
                                Value = (double)actual,
                                Unit = "$",
                                Trend = "Stable",
                                ChangePercent = 0,
                                Description = item.Description ?? string.Empty
                            });
                        }
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
                var hasTimestampProp = item.GetType().GetProperty("Timestamp");
                if (item.Category?.ToLowerInvariant() == "activity" || hasTimestampProp != null)
                {
                    DateTime ts = DateTime.Now;
                    if (hasTimestampProp != null && hasTimestampProp.PropertyType == typeof(DateTime))
                    {
                        var val = hasTimestampProp.GetValue(item);
                        if (val is DateTime dt && dt != default) ts = dt;
                    }

                    ActivityItems.Add(new ActivityItem
                    {
                        Timestamp = ts,
                        Activity = $"{item.Title}: {item.Value}",
                        Category = item.Category ?? "General",
                        Details = item.Description ?? string.Empty
                    });
                }
            }

            var sorted = ActivityItems.OrderByDescending(a => a.Timestamp).ToList();
            ActivityItems.Clear();
            foreach (var item in sorted) ActivityItems.Add(item);

            OnPropertyChanged(nameof(FormattedTotalBudget));
            OnPropertyChanged(nameof(FormattedTotalActual));
            OnPropertyChanged(nameof(FormattedVariance));
            OnPropertyChanged(nameof(FormattedVariancePercentage));
            OnPropertyChanged(nameof(FormattedBudgetUtilization));

            LastUpdateTime = DateTime.Now.ToString("g", System.Globalization.CultureInfo.CurrentCulture);

            _logger.LogInformation("MainViewModel: ProcessDashboard - Dashboard data processed. {ItemCount} items processed", ActivityItems.Count);
        }

        partial void OnTotalBudgetChanged(decimal oldValue, decimal newValue) => UpdateDerivedMetrics();
        partial void OnTotalActualChanged(decimal oldValue, decimal newValue) => UpdateDerivedMetrics();
        partial void OnVarianceChanged(decimal oldValue, decimal newValue) => UpdateDerivedMetrics();

        private void UpdateDerivedMetrics()
        {
            IsPositiveVariance = Variance >= 0;

            VariancePercentage = TotalBudget > 0 ? Math.Abs(Variance / TotalBudget) * 100 : 0;
            BudgetUtilizationPercentage = TotalBudget > 0 ? (TotalActual / TotalBudget) * 100 : 0;

            TotalExpenditure = TotalActual;
            RemainingBudget = TotalBudget - TotalActual;

            TotalBudgetGauge = BudgetUtilizationPercentage;
            RevenueGauge = TotalBudget > 0 ? Math.Min((TotalActual / TotalBudget) * 100, 100) : 0;
            ExpensesGauge = TotalBudget > 0 ? Math.Min((TotalActual / TotalBudget) * 100, 100) : 0;
            NetPositionGauge = TotalBudget > 0 ? Math.Min(Math.Max((Variance / TotalBudget) * 100 + 50, 0), 100) : 50;

            if (VariancePercentage >= 10)
                VarianceStatusColor = "Green";
            else if (VariancePercentage >= 0)
                VarianceStatusColor = "Green";
            else if (VariancePercentage >= -5)
                VarianceStatusColor = "Orange";
            else
                VarianceStatusColor = "Red";

            OnPropertyChanged(nameof(FormattedVariance));
            OnPropertyChanged(nameof(FormattedVariancePercentage));
            OnPropertyChanged(nameof(FormattedBudgetUtilization));
        }

        /// <summary>
        /// Syncs QuickBooks accounts and updates the Dashboard grid.
        /// Called from the "Sync Now" ribbon button.
        /// Handles token refresh, fallback on failure, and error reporting.
        /// </summary>
        private async Task SyncQuickBooksAccountsAsync(CancellationToken cancellationToken = default)
        {
            _statusProgressService?.Start("QuickBooks Sync", "Syncing QuickBooks accounts...", isIndeterminate: false);
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _logger.LogInformation("MainViewModel: SyncQuickBooksAccountsAsync - Starting manual accounts sync");

                // Call QuickBooks service to sync accounts
                var syncResult = await _quickBooksService.SyncAccountsAsync(cancellationToken);

                if (syncResult.Success)
                {
                    _logger.LogInformation("QuickBooks accounts synced successfully. Count: {RecordCount}, Duration: {Duration}ms",
                        syncResult.RecordsSynced, syncResult.Duration.TotalMilliseconds);

                    ErrorMessage = $"✓ Synced {syncResult.RecordsSynced} accounts in {syncResult.Duration.TotalMilliseconds:F0}ms";
                }
                else
                {
                    _logger.LogWarning("QuickBooks accounts sync failed: {ErrorMessage}", syncResult.ErrorMessage);

                    // Display fallback message if using cached/sample accounts
                    if (syncResult.RecordsSynced > 0)
                    {
                        ErrorMessage = $"⚠ Sync failed - showing {syncResult.RecordsSynced} fallback accounts. {syncResult.ErrorMessage}";
                    }
                    else
                    {
                        ErrorMessage = $"✗ Sync failed: {syncResult.ErrorMessage}";
                    }
                }

                // Refresh dashboard to show latest data
                await RefreshDataAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("QuickBooks sync was cancelled");
                ErrorMessage = "Sync cancelled by user";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during QuickBooks accounts sync");
                ErrorMessage = $"✗ Sync failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                _statusProgressService?.Complete("QuickBooks Sync", "QuickBooks sync complete");
            }
        }

        /// <summary>
        /// Performs global search across all modules with resilience and error handling.
        /// Executes via MainViewModel.GlobalSearchCommand with automatic IsBusy state management.
        /// </summary>
        /// <param name="query">Search query string</param>
        /// <param name="cancellationToken">Cancellation token for search operation</param>
        private async Task PerformGlobalSearchAsync(string? query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("Global search attempted with empty query");
                ErrorMessage = "Please enter a search query.";
                return;
            }

            try
            {
                _statusProgressService?.Start("Global Search", $"Searching for '{query}'...", isIndeterminate: false);
                _logger.LogInformation("Global search initiated: '{Query}'", query);
                ErrorMessage = null;

                var results = await _globalSearchService.SearchAsync(query);
                _logger.LogInformation("Global search completed: {ResultCount} results for '{Query}'", results.TotalResults, query);

                // Update UI with results
                LastUpdateTime = DateTime.Now.ToString("g");
                ErrorMessage = results.TotalResults == 0
                    ? $"No results found for '{query}'. Try a different search term."
                    : $"Found {results.TotalResults} results for '{query}'.";
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Global search cancelled for query '{Query}'", query);
                ErrorMessage = "Search was cancelled.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Global search failed for query '{Query}'", query);
                ErrorMessage = $"Search failed: {ex.Message}";
            }
            finally
            {
                _statusProgressService?.Complete("Global Search", "Search complete");
            }
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
