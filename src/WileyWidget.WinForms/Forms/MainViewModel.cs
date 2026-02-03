#if false // Moved to WileyWidget.WinForms.ViewModels.MainViewModel
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
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Production-hardened ViewModel for the main dashboard panel.
    /// Provides metric summaries, recent activity grid data, loading/error state,
    /// and derived analytics for rich UI presentation (cards, conditional formatting).
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject, IDisposable, ILazyLoadViewModel
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IDashboardService _dashboardService;
        private readonly IAILoggingService _aiLoggingService;
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

        // Derived analytic properties
        [ObservableProperty]
        private decimal variancePercentage;

        [ObservableProperty]
        private decimal budgetUtilizationPercentage;

        [ObservableProperty]
        private bool isPositiveVariance;

        // Gauge properties for dashboard metric visualization (0-100 scale)
        [ObservableProperty]
        private decimal totalBudgetGauge;  // Budget utilization percentage (0-100%)

        [ObservableProperty]
        private decimal revenueGauge;      // Revenue collection percentage (0-100%)

        [ObservableProperty]
        private decimal expensesGauge;     // Expense ratio percentage (0-100%)

        [ObservableProperty]
        private decimal netPositionGauge;  // Budget variance indicator (0-100%)

        // Explicit summary tile properties
        [ObservableProperty]
        private decimal totalExpenditure;  // Total actual spending (alias for TotalActual visualization)

        [ObservableProperty]
        private decimal remainingBudget;   // TotalBudget - TotalExpenditure

        // Semantic status color for variance indicator
        [ObservableProperty]
        private string varianceStatusColor = "Green";  // Green, Orange, Red

        // Collections for detailed visualizations
        public ObservableCollection<DashboardMetric> Metrics { get; } = new();

        public ObservableCollection<MonthlyRevenue> MonthlyRevenueData { get; } = new();

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
        public object GlobalSearchCommand { get; internal set; }

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
                Metrics.Clear();
                MonthlyRevenueData.Clear();
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

                // Load sample monthly revenue data for trends
                PopulateMonthlyRevenueData();

                LastUpdateTime = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
                _logger.LogInformation("Dashboard loaded successfully — {Count} activities, {Metrics} metrics", ActivityItems.Count, Metrics.Count);
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

        private void PopulateMonthlyRevenueData()
        {
            // Populate with last 12 months of sample data
            // In production, this would come from a repository query
            MonthlyRevenueData.Clear();

            var now = DateTime.Now;
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            for (int i = 11; i >= 0; i--)
            {
                var month = now.AddMonths(-i);
                var monthName = monthNames[month.Month - 1];
                var baseAmount = 100000m + (i * 15000m);  // Increasing trend
                var variance = (decimal)new Random().Next(-5000, 5000);

                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = monthName,
                    Amount = baseAmount,
                    Budget = baseAmount + 25000m,
                    Variance = variance
                });
            }

            _logger.LogDebug("PopulateMonthlyRevenueData: {Count} months loaded", MonthlyRevenueData.Count);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("InitializeAsync called - deferring heavy load to lazy transition");
            // Do not call LoadDataAsync here to support lazy loading
            // Only perform lightweight initialization if needed
            await Task.CompletedTask;
        }

        /// <summary>
        /// Triggered by the UI when the panel becomes visible.
        /// Performs the heavy data load if it hasn't been done yet.
        /// </summary>
        public async Task OnVisibilityChangedAsync(bool isVisible)
        {
            if (isVisible && !IsDataLoaded && !IsLoading)
            {
                _logger.LogInformation("Panel visible for first time; triggering lazy load");
                await LoadDataAsync(CancellationToken.None);
                IsDataLoaded = true;
            }
        }

        public void ProcessDashboard(IEnumerable<DashboardItem> dashboardItems)
        {
            ArgumentNullException.ThrowIfNull(dashboardItems);

            ActivityItems.Clear();
            Metrics.Clear();

            foreach (var item in dashboardItems)
            {
                if (item == null) continue;

                switch (item.Category?.ToLowerInvariant() ?? string.Empty)
                {
                    case "budget" when decimal.TryParse(item.Value, out var budget):
                        TotalBudget = budget;
                        Metrics.Add(new DashboardMetric
                        {
                            Name = item.Title,
                            Value = budget,
                            Category = item.Category ?? string.Empty,
                            Amount = budget
                        });
                        break;
                    case "actual" when decimal.TryParse(item.Value, out var actual):
                        TotalActual = actual;
                        Metrics.Add(new DashboardMetric
                        {
                            Name = item.Title,
                            Value = actual,
                            Category = item.Category ?? string.Empty,
                            Amount = actual
                        });
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
            _logger.LogInformation("ProcessDashboard completed — {Items} activities, {Metrics} metrics", ActivityItems.Count, Metrics.Count);
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

            // Update explicit summary tile properties
            TotalExpenditure = TotalActual;  // Alias for UI clarity
            RemainingBudget = TotalBudget - TotalActual;

            // Calculate gauge values (0-100 scale)
            TotalBudgetGauge = BudgetUtilizationPercentage;  // Budget utilization
            RevenueGauge = TotalBudget > 0 ? Math.Min((TotalActual / TotalBudget) * 100, 100) : 0;  // Revenue as % of budget
            ExpensesGauge = TotalBudget > 0 ? Math.Min((TotalActual / TotalBudget) * 100, 100) : 0;  // Expense ratio
            NetPositionGauge = TotalBudget > 0 ? Math.Min(Math.Max((Variance / TotalBudget) * 100 + 50, 0), 100) : 50;  // Variance centered at 50

            // Update semantic status color based on variance
            if (VariancePercentage >= 10)  // More than 10% under budget (good)
                VarianceStatusColor = "Green";
            else if (VariancePercentage >= 0)  // Under budget but less than 10%
                VarianceStatusColor = "Green";
            else if (VariancePercentage >= -5)  // Over budget by 5% or less
                VarianceStatusColor = "Orange";
            else  // Over budget by more than 5%
                VarianceStatusColor = "Red";

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

    /// <summary>
    /// Dashboard metric item for grid/list display.
    /// Represents a single department or account metric.
    /// </summary>
    public class DashboardMetric
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Category { get; set; } = "General";
        public string DepartmentName { get; set; } = string.Empty;
        public decimal BudgetedAmount { get; set; }
        public decimal Amount { get; set; }  // Actual spending
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Monthly revenue data for trend charts and sparklines.
    /// Represents aggregated financial data for a month.
    /// </summary>
    public class MonthlyRevenue
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Budget { get; set; }
        public decimal Variance { get; set; }
    }
}
#endif
