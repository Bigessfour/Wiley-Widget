using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Production-hardened ViewModel for the main dashboard panel.
    /// Provides metric summaries, recent activity grid data, loading/error state,
    /// and derived analytics for rich UI presentation (cards, conditional formatting).
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

        // Derived analytic properties
        [ObservableProperty]
        private decimal variancePercentage;

        [ObservableProperty]
        private decimal budgetUtilizationPercentage;

        [ObservableProperty]
        private bool isPositiveVariance;

        // UI-friendly formatted strings (for direct binding, no converters needed)
        public string FormattedTotalBudget => TotalBudget.ToString("C", CultureInfo.CurrentCulture);
        public string FormattedTotalActual => TotalActual.ToString("C", CultureInfo.CurrentCulture);
        public string FormattedVariance => Variance >= 0 ? $"+{Variance.ToString("C", CultureInfo.CurrentCulture)}" : Variance.ToString("C", CultureInfo.CurrentCulture);
        public string FormattedVariancePercentage => $"{VariancePercentage:F1}% {(IsPositiveVariance ? "under" : "over")} budget";
        public string FormattedBudgetUtilization => $"{BudgetUtilizationPercentage:F1}% utilized";

        /// <summary>
        /// Collection of recent activity items bound to the dashboard SfDataGrid.
        /// Always kept sorted newest-first.
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

            _logger.LogInformation("MainViewModel constructed");
        }

        private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("RefreshDataAsync started");
                ActivityItems.Clear();
                TotalBudget = TotalActual = Variance = 0;
                ActiveAccountCount = TotalDepartments = 0;
                ErrorMessage = null;

                await LoadDataAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh failed");
                _aiLoggingService.LogError("Dashboard Refresh", ex);
                throw;
            }
        }

        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _logger.LogInformation("LoadDataAsync started");

                cancellationToken.ThrowIfCancellationRequested();

                IEnumerable<DashboardItem>? dashboardItems = null;

                try
                {
                    var getDashboardDataMethod = _dashboardService.GetType().GetMethod("GetDashboardDataAsync");
                    if (getDashboardDataMethod != null)
                    {
                        _logger.LogDebug("Calling GetDashboardDataAsync via reflection");
                        var task = (Task<IEnumerable<DashboardItem>>?)getDashboardDataMethod.Invoke(
                            _dashboardService, new object[] { cancellationToken });
                        dashboardItems = await (task ?? Task.FromResult(Enumerable.Empty<DashboardItem>()));
                    }
                    else
                    {
                        _logger.LogDebug("Calling GetDashboardItemsAsync fallback");
                        dashboardItems = await _dashboardService.GetDashboardItemsAsync(cancellationToken);
                    }
                }
                catch (Exception serviceEx)
                {
                    _logger.LogWarning(serviceEx, "Service call failed — falling back to sample data");
                    _aiLoggingService.LogError("Dashboard Service Failure", serviceEx);
                    dashboardItems = GetSampleDashboardData();
                    ErrorMessage = "Live data unavailable — showing sample data";
                }

                if (dashboardItems == null || !dashboardItems.Any())
                {
                    _logger.LogWarning("No data returned — using sample data");
                    dashboardItems = GetSampleDashboardData();
                    ErrorMessage ??= "No live data — displaying sample dashboard";
                }

                ProcessDashboard(dashboardItems);

                LastUpdateTime = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
                _logger.LogInformation("Dashboard loaded successfully — {Count} activity items", ActivityItems.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Load canceled by user");
                ErrorMessage = null;
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load failed critically");
                _aiLoggingService.LogError("Dashboard Load Critical", ex);
                ErrorMessage = $"Load failed: {ex.Message}";

                try
                {
                    ProcessDashboard(GetSampleDashboardData());
                    ErrorMessage += " — showing sample data as fallback";
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Even sample fallback failed");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<DashboardItem> GetSampleDashboardData()
        {
            var now = DateTime.Now;
            return new[]
            {
                new DashboardItem { Title = "Total Budget", Value = "1250000.00", Category = "budget", Description = "Annual allocated budget" },
                new DashboardItem { Title = "Total Actual", Value = "987500.50", Category = "actual", Description = "YTD spending" },
                new DashboardItem { Title = "Variance", Value = "262499.50", Category = "variance", Description = "Remaining (positive = under)" },
                new DashboardItem { Title = "Active Accounts", Value = "42", Category = "accounts", Description = "Live municipal accounts" },
                new DashboardItem { Title = "Departments", Value = "8", Category = "departments", Description = "Tracked departments" },
                new DashboardItem { Title = "Recent Activity", Value = "Budget entry approved", Category = "activity", Description = "Finance dept" },
                new DashboardItem { Title = "Recent Activity", Value = "Invoice #1234 processed", Category = "activity", Description = "Accounts payable" },
                new DashboardItem { Title = "Recent Activity", Value = "New account activated", Category = "activity", Description = "Customer #5678" },
                new DashboardItem { Title = "Recent Activity", Value = "Report generated", Category = "activity", Description = "Monthly summary" }
            };
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("InitializeAsync called");
            await LoadDataAsync(cancellationToken);
        }

        public void ProcessDashboard(IEnumerable<DashboardItem> dashboardItems)
        {
            ArgumentNullException.ThrowIfNull(dashboardItems);

            ActivityItems.Clear();

            foreach (var item in dashboardItems)
            {
                if (item == null) continue;

                switch (item.Category?.ToLowerInvariant() ?? string.Empty)
                {
                    case "budget" when decimal.TryParse(item.Value, out var budget):
                        TotalBudget = budget;
                        break;
                    case "actual" when decimal.TryParse(item.Value, out var actual):
                        TotalActual = actual;
                        break;
                    case "variance" when decimal.TryParse(item.Value, out var varianceVal):
                        Variance = varianceVal;
                        break;
                    case "accounts" when int.TryParse(item.Value, out var accounts):
                        ActiveAccountCount = accounts;
                        break;
                    case "departments" when int.TryParse(item.Value, out var depts):
                        TotalDepartments = depts;
                        break;
                }

                // Only add explicit activity items or create from metrics
                var hasTimestampProp = item.GetType().GetProperty("Timestamp");
                if (item.Category?.ToLowerInvariant() == "activity" || hasTimestampProp != null)
                {
                    // Prefer explicit Timestamp if provided by the item (backwards-compatible via reflection)
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
                        Details = item.Description
                    });
                }
            }

            // Ensure newest activities first
            var sorted = ActivityItems.OrderByDescending(a => a.Timestamp).ToList();
            ActivityItems.Clear();
            foreach (var item in sorted) ActivityItems.Add(item);

            // Trigger updates for all derived properties
            OnPropertyChanged(nameof(FormattedTotalBudget));
            OnPropertyChanged(nameof(FormattedTotalActual));
            OnPropertyChanged(nameof(FormattedVariance));
            OnPropertyChanged(nameof(FormattedVariancePercentage));
            OnPropertyChanged(nameof(FormattedBudgetUtilization));

            LastUpdateTime = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
            _logger.LogInformation("ProcessDashboard completed — {Items} activities", ActivityItems.Count);
        }

        // Partial methods to update derived properties
        partial void OnTotalBudgetChanged(decimal oldValue, decimal newValue) => UpdateDerivedMetrics();
        partial void OnTotalActualChanged(decimal oldValue, decimal newValue) => UpdateDerivedMetrics();
        partial void OnVarianceChanged(decimal oldValue, decimal newValue) => UpdateDerivedMetrics();

        private void UpdateDerivedMetrics()
        {
            IsPositiveVariance = Variance >= 0;

            VariancePercentage = TotalBudget > 0 ? Math.Abs(Variance / TotalBudget) * 100 : 0;
            BudgetUtilizationPercentage = TotalBudget > 0 ? (TotalActual / TotalBudget) * 100 : 0;

            // Notify UI-bound formatted properties
            OnPropertyChanged(nameof(FormattedVariance));
            OnPropertyChanged(nameof(FormattedVariancePercentage));
            OnPropertyChanged(nameof(FormattedBudgetUtilization));
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
