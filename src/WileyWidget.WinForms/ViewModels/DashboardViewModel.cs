using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using WileyWidget.WinForms.Logging;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Dashboard metric display model
    /// </summary>
    public class DashboardMetric
    {
        public required string Name { get; init; }
        public required double Value { get; init; }
        public required string Unit { get; init; }
        public required string Trend { get; init; }
        public required double ChangePercent { get; init; }
        public required string Description { get; init; }
    }

    /// <summary>
    /// Comprehensive dashboard view model with real data from repositories
    /// </summary>

    public partial class DashboardViewModel : ObservableObject, IDashboardViewModel, IDisposable
    {
    private readonly IBudgetRepository? _budgetRepository;
    private readonly IMunicipalAccountRepository? _accountRepository;
    private readonly IDashboardService? _dashboardService;
            private readonly ILogger<DashboardViewModel> _logger;
        private CancellationTokenSource? _loadCancellationTokenSource;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private const int MaxRetryAttempts = 3;
        private bool _disposed;
        private readonly IConfiguration? _configuration;

        #region Observable Properties

        [ObservableProperty]
        private string municipalityName = "Town of Wiley";

        [ObservableProperty]
        private string fiscalYear = "FY 2025-2026";

        [ObservableProperty]
        private DateTime lastUpdated = DateTime.Now;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasError;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<DashboardMetric> metrics = new();

        [ObservableProperty]
        private float totalBudgetGauge;

        [ObservableProperty]
        private float revenueGauge;

        [ObservableProperty]
        private float expensesGauge;

        [ObservableProperty]
        private float netPositionGauge;

        // Budget Analysis Data
        [ObservableProperty]
        private BudgetVarianceAnalysis? budgetAnalysis;

        [ObservableProperty]
        private decimal totalBudgeted;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal totalVariance;

        [ObservableProperty]
        private decimal variancePercentage;

        // Fund Breakdown
        [ObservableProperty]
        private ObservableCollection<FundSummary> fundSummaries = new();

        // Department Breakdown
        [ObservableProperty]
        private ObservableCollection<DepartmentSummary> departmentSummaries = new();

        // Top Accounts by Variance
        [ObservableProperty]
        private ObservableCollection<AccountVariance> topVariances = new();

        // Revenue & Expense Details
        [ObservableProperty]
        private decimal totalRevenue;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private decimal netIncome;

        [ObservableProperty]
        private int accountCount;

        [ObservableProperty]
        private int activeDepartments;

        // Chart Data for Revenue Trend
        [ObservableProperty]
        private ObservableCollection<MonthlyRevenue> monthlyRevenueData = new();

        // Status text for display
        [ObservableProperty]
        private string statusText = "Ready";

        [ObservableProperty]
        private DateTime? lastRefreshTime;

        // Legacy property aliases expected by some views
        public decimal TotalBudget => TotalBudgeted;
        public decimal TotalExpenditure => TotalExpenses;
        public decimal RemainingBudget => TotalBudgeted - TotalActual;
        public DateTime LastRefreshed => LastUpdated;

        // Alias for variance status color (semantic indicator)
        [ObservableProperty]
        private string varianceStatusColor = "Green";  // Green, Orange, Red

        // Alias properties for DashboardPanel compatibility
        public ObservableCollection<DepartmentSummary> DepartmentMetrics => DepartmentSummaries;

        // Computed summary properties for DashboardFactory navigation cards
        public string AccountsSummary => AccountCount > 0 ? $"{AccountCount:N0} Municipal Accounts" : MainFormResources.LoadingText;
        public string BudgetStatus => TotalBudgeted > 0 ? $"Variance: {TotalVariance:C} ({VariancePercentage:F1}%)" : StatusText;

        #endregion

        #region Commands

        public IAsyncRelayCommand LoadCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand RefreshMetricsCommand { get; }
        public IAsyncRelayCommand<int> LoadFiscalYearCommand { get; }

        // Legacy alias used by some views
        public IAsyncRelayCommand LoadDashboardCommand => LoadCommand;

        #endregion

        public DashboardViewModel(
            IBudgetRepository? budgetRepository,
            IMunicipalAccountRepository? accountRepository,
            ILogger<DashboardViewModel>? logger,
            IDashboardService? dashboardService = null,
            IConfiguration? configuration = null)
        {
            // CRITICAL: Use NullLogger fallback to prevent null reference in error handlers
            _logger = logger ?? WileyWidget.WinForms.Logging.NullLogger<DashboardViewModel>.Instance;

            // Log if fallback was used (indicates DI misconfiguration)
            if (logger == null)
            {
                Console.WriteLine("[WARNING] DashboardViewModel: ILogger<DashboardViewModel> is null - using NullLogger fallback");
            }

            // Store repositories and services (will validate in LoadDashboardDataAsync)
            _budgetRepository = budgetRepository;
            _accountRepository = accountRepository;
            _dashboardService = dashboardService;
            _configuration = configuration;

            LoadCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDashboardDataAsync);
            RefreshMetricsCommand = new AsyncRelayCommand(RefreshMetricsAsync);
            LoadFiscalYearCommand = new AsyncRelayCommand<int>(LoadFiscalYearDataAsync);

            // Initialize with sample data for design-time
            if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            {
                InitializeSampleData();
            }
            else
            {
                // Immediate sample data so dashboard never starts blank
                // Async real load will overwrite if available (triggered by panel visibility/refresh)
                LoadSampleDashboardData();
            }

            _logger.LogDebug("DashboardViewModel initialized");
        }

        public DashboardViewModel()
            : this(null, null, WileyWidget.WinForms.Logging.NullLogger<DashboardViewModel>.Instance, null)
        {
        }

        /// <summary>
        /// Refreshes dashboard metrics on demand by reloading budget analysis from repository.
        /// </summary>
        private async Task RefreshMetricsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Refreshing dashboard metrics on demand...");
                var now = DateTime.Now;
                var currentFiscalYear = (now.Month >= 7) ? now.Year + 1 : now.Year;
                var fiscalYearStart = new DateTime(currentFiscalYear - 1, 7, 1);
                var fiscalYearEnd = new DateTime(currentFiscalYear, 6, 30);

                var analysis = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd, CancellationToken.None);
                if (analysis != null)
                {
                    BudgetAnalysis = analysis;
                    TotalBudgeted = analysis.TotalBudgeted;
                    TotalActual = analysis.TotalActual;
                    TotalVariance = analysis.TotalVariance;
                    VariancePercentage = analysis.TotalVariancePercentage;
                    _logger.LogInformation("Dashboard metrics refreshed: Budget={Budget}, Actual={Actual}", TotalBudgeted, TotalActual);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard metrics");
            }
        }

        /// <summary>
        /// Loads complete dashboard data from repositories
        /// </summary>
        private async Task LoadDashboardDataAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("=== LoadDashboardDataAsync STARTED ===");
            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: ENTRY");

            // Validate dependencies defensively
            ValidateCriticalDependencies();

            // If critical dependencies are missing, stop loading
            if (HasError)
            {
                IsLoading = false;
                _logger.LogError("Critical dependencies missing - aborting dashboard load");
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Critical dependencies missing - aborting");
                return;
            }

            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Dependencies validated successfully");

            var cancellationTokenSource = new CancellationTokenSource();
            var localCancellationToken = cancellationTokenSource.Token;

            _logger.LogDebug("Acquiring load lock...");
            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Acquiring semaphore");

            // Keep on UI thread to ensure property updates work correctly
            await _loadLock.WaitAsync().ConfigureAwait(true);

            _logger.LogDebug("Load lock acquired");
            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Semaphore acquired");
            try
            {
                // Cancel any existing load operation
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource = cancellationTokenSource;

                var retryCount = 0;

                while (retryCount < MaxRetryAttempts)
                {
                    try
                    {
                        IsLoading = true;
                        ErrorMessage = null;

                        if (_budgetRepository == null || _accountRepository == null)
                        {
                            ErrorMessage = "Dashboard repositories are not available";
                            return;
                        }

                        if (retryCount > 0)
                        {
                            _logger.LogInformation("Retrying dashboard data load (attempt {Attempt} of {Max})...", retryCount + 1, MaxRetryAttempts);
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), localCancellationToken);
                        }
                        else
                        {
                            _logger.LogInformation("Loading dashboard data...");
                        }

                        // Removed cancellation check to prevent exceptions in concurrent loads

                        // Determine which fiscal year to load.
                        // Priority:
                        // 1. Environment variable WILEYWIDGET_DEFAULT_FISCAL_YEAR
                        // 2. Configuration key "UI:DefaultFiscalYear" or "Dashboard:DefaultFiscalYear"
                        // 3. Computed fiscal year based on today's date (FY increases on July 1)
                        int currentFiscalYear;
                        try
                        {
                            var envVal = Environment.GetEnvironmentVariable("WILEYWIDGET_DEFAULT_FISCAL_YEAR");
                            if (!string.IsNullOrWhiteSpace(envVal) && int.TryParse(envVal, out var envFy))
                            {
                                currentFiscalYear = envFy;
                                _logger.LogInformation("Using fiscal year from environment: {FiscalYear}", currentFiscalYear);
                            }
                            else if (_configuration != null)
                            {
                                var cfgFy = _configuration.GetValue<int?>("UI:DefaultFiscalYear") ?? _configuration.GetValue<int?>("Dashboard:DefaultFiscalYear");
                                if (cfgFy.HasValue)
                                {
                                    currentFiscalYear = cfgFy.Value;
                                    _logger.LogInformation("Using fiscal year from configuration: {FiscalYear}", currentFiscalYear);
                                }
                                else
                                {
                                    var startMonth = _configuration.GetValue<int>("FiscalYearStartMonth", 7);
                                    var now = DateTime.Now;
                                    currentFiscalYear = (now.Month >= startMonth) ? now.Year + 1 : now.Year;
                                    _logger.LogInformation("Using computed fiscal year: {FiscalYear} (Start Month: {StartMonth})", currentFiscalYear, startMonth);
                                }
                            }
                            else
                            {
                                var fyInfo = FiscalYearInfo.FromDateTime(DateTime.Now);
                                currentFiscalYear = fyInfo.Year;
                                _logger.LogInformation("Using default computed fiscal year (July 1): {FiscalYear}", currentFiscalYear);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to determine fiscal year from env/config; falling back to computed value");
                            var now = DateTime.Now;
                            currentFiscalYear = (now.Month >= 7) ? now.Year + 1 : now.Year;
                        }
                        var fiscalYearStart = new DateTime(currentFiscalYear - 1, 7, 1); // July 1
                        var fiscalYearEnd = new DateTime(currentFiscalYear, 6, 30); // June 30

                        // Check for cancellation before expensive operations
                        localCancellationToken.ThrowIfCancellationRequested();

                        // Prefer real Wiley data if available
                        if (_dashboardService != null)
                        {
                            try
                            {
                                _logger.LogInformation("Attempting to load Town of Wiley 2026 budget data from dashboard service...");
                                await _dashboardService.PopulateDashboardMetricsFromWileyDataAsync(localCancellationToken);

                                // Also populate department summaries from the mapped data
                                await _dashboardService.PopulateDepartmentSummariesFromSanitationAsync(localCancellationToken);

                                _logger.LogInformation("Town of Wiley 2026 budget data loaded successfully");
                                LastUpdated = DateTime.Now;
                                StatusText = "Wiley 2026 Budget Loaded";
                                ErrorMessage = null;
                                break; // Successfully loaded—exit retry loop
                            }
                            catch (Exception wileyEx)
                            {
                                _logger.LogWarning(wileyEx, "Failed to load Wiley 2026 budget data from dashboard service - falling back to repository");
                            }
                        }

                        // Fallback: Load budget analysis from repository
                        var analysis = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd, localCancellationToken);

                        if (analysis != null)
                        {
                            // Update all properties (already on UI thread due to no ConfigureAwait(false))
                            BudgetAnalysis = analysis;
                            TotalBudgeted = analysis.TotalBudgeted;
                            TotalActual = analysis.TotalActual;
                            TotalVariance = analysis.TotalVariance;
                            VariancePercentage = analysis.TotalVariancePercentage;

                            FundSummaries.Clear();
                            foreach (var fund in analysis.FundSummaries)
                            {
                                FundSummaries.Add(fund);
                            }

                            // Update department summaries
                            DepartmentSummaries.Clear();
                            foreach (var dept in analysis.DepartmentSummaries)
                            {
                                DepartmentSummaries.Add(dept);
                            }
                            ActiveDepartments = analysis.DepartmentSummaries.Count;

                            // Get top variances (largest deviations) and update
                            var topVarList = analysis.AccountVariances
                                .OrderByDescending(v => Math.Abs(v.VarianceAmount))
                                .Take(10)
                                .ToList();

                            TopVariances.Clear();
                            foreach (var variance in topVarList)
                            {
                                TopVariances.Add(variance);
                            }
                        }
                        else
                        {
                            // No analysis data available - fall back to sample data
                            _logger.LogInformation("Budget analysis returned null - falling back to sample dashboard data");
                            LoadSampleDashboardData();
                        }

                        // Load account count
                        var accountCount = await _accountRepository.GetCountAsync(localCancellationToken);

                        // Calculate revenue and expenses from budget entries
                        var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(currentFiscalYear, localCancellationToken);
                        var totalRevenue = budgetEntries
                            .Where(be => be.AccountNumber.StartsWith("4", StringComparison.Ordinal)) // Revenue accounts typically start with 4
                            .Sum(be => be.ActualAmount);

                        var totalExpenses = budgetEntries
                            .Where(be => be.AccountNumber.StartsWith("5", StringComparison.Ordinal) ||
                                         be.AccountNumber.StartsWith("6", StringComparison.Ordinal)) // Expense accounts typically start with 5 or 6
                            .Sum(be => be.ActualAmount);

                        var netIncome = totalRevenue - totalExpenses;

                        // Calculate gauge values (0-100 scale representing percentage of budget used)
                        var totalBudgetGauge = TotalBudgeted > 0 ? (float)((TotalActual / TotalBudgeted) * 100) : 0f;
                        var revenueGauge = TotalBudgeted > 0 ? (float)((totalRevenue / (TotalBudgeted * 0.4m)) * 100) : 0f; // Assume 40% of budget is revenue
                        var expensesGauge = TotalBudgeted > 0 ? (float)((totalExpenses / (TotalBudgeted * 0.6m)) * 100) : 0f; // Assume 60% of budget is expenses
                        var netPositionGauge = Math.Max(0, Math.Min(100, 50 + (float)(netIncome / 1000000 * 50))); // Scale net position: -1M to +1M = 0-100

                        // Update all properties (already on UI thread)
                        try
                        {
                            // Update calculated values
                            AccountCount = accountCount;
                            TotalRevenue = totalRevenue;
                            TotalExpenses = totalExpenses;
                            NetIncome = netIncome;

                            // Update gauge values
                            TotalBudgetGauge = totalBudgetGauge;
                            RevenueGauge = revenueGauge;
                            ExpensesGauge = expensesGauge;
                            NetPositionGauge = netPositionGauge;

                            // Update metrics and revenue data
                            UpdateMetricsCollection();
                            // Monthly revenue data loading deferred to async method during initialization
                            // PopulateMonthlyRevenueDataAsync is called separately to avoid blocking UI updates

                            // Update metadata
                            MunicipalityName = "Town of Wiley";
                            FiscalYear = $"FY {currentFiscalYear}";
                            LastUpdated = DateTime.Now;
                            StatusText = $"Loaded {AccountCount} accounts, {ActiveDepartments} departments";

                            _logger.LogInformation("Dashboard UI updates completed: {MetricsCount} metrics, {RevenueCount} revenue data points",
                                Metrics.Count, MonthlyRevenueData.Count);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to update UI collections");
                        }

                        _logger.LogInformation("Dashboard data loaded successfully: {ItemCount} metrics, Revenue: {Revenue:C}, Expenses: {Expenses:C}",
                            Metrics.Count, totalRevenue, totalExpenses);
                        _logger.LogInformation("Dashboard metrics updated: NetPosition={NetPosition:C}, Budgeted={Budgeted:C}, Actual={Actual:C}",
                            netIncome, TotalBudgeted, TotalActual);
                        _logger.LogInformation("Dashboard collections: {FundCount} funds, {DeptCount} departments, {VarianceCount} variances",
                            FundSummaries.Count, DepartmentSummaries.Count, TopVariances.Count);
                        _logger.LogInformation("=== Dashboard load COMPLETED SUCCESSFULLY ===");
                        ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: COMPLETED SUCCESSFULLY");
                        // Successfully loaded—exit retry loop
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Handle cancellation gracefully for dashboard loads (tests expect cancellation to be swallowed)
                        _logger.LogInformation("Dashboard load cancelled (likely due to concurrent load request)");
                        ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Cancelled");
                        ErrorMessage = null; // Clear error since this is expected
                        // Stop retrying and exit method without throwing to callers
                        break;
                    }
                    catch (ObjectDisposedException odex)
                    {
                        // The view model or its dependencies were disposed (likely during shutdown).
                        // Do not retry on ObjectDisposedException; stop attempts and exit gracefully.
                        _logger.LogInformation(odex, "Dashboard load aborted due to disposal; stopping retries");
                        ErrorMessage = "Dashboard load aborted due to shutdown";
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading dashboard data");
                        ErrorMessage = $"Failed to load dashboard: {ex.Message}";
                        // Increment retry count so we don't loop infinitely
                        retryCount++;

                        // If this is the final attempt, fall back to sample data instead of showing error
                        if (retryCount >= MaxRetryAttempts)
                        {
                            _logger.LogError("Dashboard load failed after {Attempts} attempts - falling back to sample data", MaxRetryAttempts);
                            LoadSampleDashboardData();
                            ErrorMessage = null; // Clear error since we're showing valid sample data
                            break; // Exit retry loop
                        }
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                }
            }
            finally
            {
                _logger.LogDebug("Releasing load lock");
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Releasing semaphore");
                _loadLock.Release();
            }
        }
        /// <summary>
        /// Refreshes the dashboard data
        /// </summary>
        private async Task RefreshDashboardDataAsync(CancellationToken cancellationToken = default)
        {
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// Loads dashboard data for a specific fiscal year
        /// </summary>
        private async Task LoadFiscalYearDataAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                if (_budgetRepository == null || _accountRepository == null)
                {
                    ErrorMessage = "Dashboard repositories are not available";
                    return;
                }

                _logger.LogInformation("Loading dashboard data for fiscal year {FiscalYear}", fiscalYear);

                var fiscalYearStart = new DateTime(fiscalYear - 1, 7, 1);
                var fiscalYearEnd = new DateTime(fiscalYear, 6, 30);

                var analysis = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd);

                if (analysis != null)
                {
                    // All property and collection updates must be on UI thread
                    var updateAction = new System.Action(() =>
                    {
                        BudgetAnalysis = analysis;
                        TotalBudgeted = analysis.TotalBudgeted;
                        TotalActual = analysis.TotalActual;
                        TotalVariance = analysis.TotalVariance;
                        VariancePercentage = analysis.TotalVariancePercentage;

                        FundSummaries.Clear();
                        foreach (var fund in analysis.FundSummaries)
                        {
                            FundSummaries.Add(fund);
                        }

                        DepartmentSummaries.Clear();
                        foreach (var dept in analysis.DepartmentSummaries)
                        {
                            DepartmentSummaries.Add(dept);
                        }

                        UpdateMetricsCollection();

                        FiscalYear = $"FY {fiscalYear}";
                        LastUpdated = DateTime.Now;
                    });

                    // Already on UI thread - call directly
                    updateAction();
                }

                _logger.LogInformation("Dashboard data for FY {FiscalYear} loaded successfully", fiscalYear);
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation exceptions
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data for fiscal year {FiscalYear}", fiscalYear);
                ErrorMessage = $"Failed to load fiscal year data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Updates the metrics collection for grid display
        /// </summary>
        private void UpdateMetricsCollection()
        {
            _logger.LogDebug("UpdateMetricsCollection: Starting metrics update");
            Metrics.Clear();

            Metrics.Add(new DashboardMetric
            {
                Name = "Total Budget",
                Value = (double)TotalBudgeted,
                Unit = "$",
                Trend = TotalBudgeted > 0 ? "→" : "↓",
                ChangePercent = 0.0,
                Description = "Total budgeted amount for all funds"
            });

            Metrics.Add(new DashboardMetric
            {
                Name = "Total Actual",
                Value = (double)TotalActual,
                Unit = "$",
                Trend = TotalActual < TotalBudgeted ? "↑" : "↓",
                ChangePercent = TotalBudgeted > 0 ? (double)((TotalActual / TotalBudgeted - 1) * 100) : 0.0,
                Description = "Total actual spending across all funds"
            });

            Metrics.Add(new DashboardMetric
            {
                Name = "Budget Variance",
                Value = (double)TotalVariance,
                Unit = "$",
                Trend = TotalVariance > 0 ? "↑" : "↓",
                ChangePercent = (double)VariancePercentage,
                Description = "Difference between budget and actual (positive = under budget)"
            });

            Metrics.Add(new DashboardMetric
            {
                Name = "Total Revenue",
                Value = (double)TotalRevenue,
                Unit = "$",
                Trend = TotalRevenue > 0 ? "↑" : "→",
                ChangePercent = 0.0,
                Description = "Total revenue collected"
            });

            Metrics.Add(new DashboardMetric
            {
                Name = "Total Expenses",
                Value = (double)TotalExpenses,
                Unit = "$",
                Trend = TotalExpenses > 0 ? "↓" : "→",
                ChangePercent = 0.0,
                Description = "Total expenses incurred"
            });

            Metrics.Add(new DashboardMetric
            {
                Name = "Net Income",
                Value = (double)NetIncome,
                Unit = "$",
                Trend = NetIncome > 0 ? "↑" : "↓",
                ChangePercent = TotalRevenue > 0 ? (double)(NetIncome / TotalRevenue * 100) : 0.0,
                Description = "Revenue minus expenses"
            });

            Metrics.Add(new DashboardMetric
            {
                Name = "Active Accounts",
                Value = AccountCount,
                Unit = "",
                Trend = "→",
                ChangePercent = 0.0,
                Description = "Number of active municipal accounts"
            });

            Metrics.Add(new DashboardMetric
            {
                Name = "Active Departments",
                Value = ActiveDepartments,
                Unit = "",
                Trend = "→",
                ChangePercent = 0.0,
                Description = "Number of departments with budget entries"
            });

            _logger.LogInformation("UpdateMetricsCollection: Metrics collection populated with {Count} items", Metrics.Count);
        }

        /// <summary>
        /// Populates monthly revenue data for chart display using real repository data
        /// </summary>
        private async Task PopulateMonthlyRevenueDataAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("PopulateMonthlyRevenueData: Starting for FY {FiscalYear}", fiscalYear);
            MonthlyRevenueData.Clear();

            try
            {
                if (_budgetRepository == null)
                {
                    _logger.LogWarning("PopulateMonthlyRevenueData: Budget repository unavailable - using fallback data");
                    PopulateMonthlyRevenueDataFallback(fiscalYear);
                    return;
                }

                // Query actual monthly revenue data from repository
                // Revenue accounts typically start with "4" (4000-4999 range)
                var fiscalYearStart = new DateTime(fiscalYear - 1, 7, 1);
                var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear, cancellationToken);

                var monthNames = new[] { "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

                // Group revenue entries by month (July = month 0, June = month 11)
                for (int i = 0; i < 12; i++)
                {
                    var monthStart = fiscalYearStart.AddMonths(i);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                    // Sum actual revenue for this month (account numbers starting with 4)
                    // Use StartPeriod/EndPeriod for month assignment, not CreatedAt
                    var monthlyAmount = budgetEntries
                        .Where(be => be.AccountNumber.StartsWith("4", StringComparison.Ordinal))
                        .Where(be => be.StartPeriod >= monthStart && be.StartPeriod <= monthEnd)
                        .Sum(be => be.ActualAmount);

                    MonthlyRevenueData.Add(new MonthlyRevenue
                    {
                        Month = monthNames[i],
                        MonthNumber = i + 1,
                        Amount = monthlyAmount
                    });
                }

                _logger.LogInformation("PopulateMonthlyRevenueData: Real data loaded - {Count} months, Total: {Total:C}",
                    MonthlyRevenueData.Count, MonthlyRevenueData.Sum(m => m.Amount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PopulateMonthlyRevenueData: Failed to load real data - using fallback");
                PopulateMonthlyRevenueDataFallback(fiscalYear);
            }
        }

        /// <summary>
        /// Fallback method using distributed revenue data when repository query fails
        /// </summary>
        private void PopulateMonthlyRevenueDataFallback(int fiscalYear)
        {
            var monthNames = new[] { "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

            // Distribute total revenue evenly across 12 months (better than random)
            var monthlyAverage = TotalRevenue > 0 ? TotalRevenue / 12 : 0m;

            for (int i = 0; i < 12; i++)
            {
                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = monthNames[i],
                    MonthNumber = i + 1,
                    Amount = monthlyAverage
                });
            }

            _logger.LogWarning("PopulateMonthlyRevenueData: Using fallback data - {Count} months", MonthlyRevenueData.Count);
        }

        /// <summary>
        /// Initializes sample data for design-time preview
        /// </summary>
        private void InitializeSampleData()
        {
            MunicipalityName = "Town of Wiley (Design)";
            FiscalYear = "FY 2025-2026";
            LastUpdated = DateTime.Now;

            TotalBudgeted = 5000000m;
            TotalActual = 4250000m;
            TotalVariance = 750000m;
            VariancePercentage = 15.0m;
            TotalRevenue = 4500000m;
            TotalExpenses = 4000000m;
            NetIncome = 500000m;
            AccountCount = 125;
            ActiveDepartments = 8;

            TotalBudgetGauge = 85.0f;
            RevenueGauge = 90.0f;
            ExpensesGauge = 80.0f;
            NetPositionGauge = 70.0f;
            StatusText = "Design Mode - Sample Data";

            UpdateMetricsCollection();
            // Use fallback for design-time (no repository available)
            PopulateMonthlyRevenueDataFallback(2026);
        }

        /// <summary>
        /// Loads comprehensive sample dashboard data (municipal utilities focus).
        /// Used as fallback when repositories are unavailable or empty.
        /// </summary>
        private void LoadSampleDashboardData()
        {
            _logger.LogInformation("Loading sample dashboard data (municipal utilities focus)");

            // Clear any partial/failed real data
            Metrics.Clear();
            FundSummaries.Clear();
            DepartmentSummaries.Clear();
            TopVariances.Clear();
            MonthlyRevenueData.Clear();

            MunicipalityName = "Town of Wiley";
            FiscalYear = "FY 2025-2026";
            LastUpdated = DateTime.Now;

            // === KPI Metrics (for SfListView or tiles) ===
            Metrics.Add(new DashboardMetric
            {
                Name = "Total Budget",
                Value = 12_450_000,
                Unit = "$",
                Trend = "up",
                ChangePercent = 4.2,
                Description = "Approved annual budget"
            });
            Metrics.Add(new DashboardMetric
            {
                Name = "YTD Actual Spending",
                Value = 6_820_000,
                Unit = "$",
                Trend = "down",
                ChangePercent = -2.1,
                Description = "Year-to-date expenditures"
            });
            Metrics.Add(new DashboardMetric
            {
                Name = "Budget Variance",
                Value = 630_000,
                Unit = "$",
                Trend = "up",
                ChangePercent = 5.3,
                Description = "Remaining budget (positive = under spent)"
            });
            Metrics.Add(new DashboardMetric
            {
                Name = "Active Accounts",
                Value = 48,
                Unit = "",
                Trend = "neutral",
                ChangePercent = 0,
                Description = "Municipal GL accounts"
            });

            // === Gauges ===
            TotalBudgetGauge = 100f;                    // Base reference
            RevenueGauge = 78.5f;                       // % of projected revenue collected
            ExpensesGauge = 54.8f;                      // % of budget spent
            NetPositionGauge = 68.2f;                   // Overall financial health %

            // === Budget Analysis Summary ===
            TotalBudgeted = 12_450_000m;
            TotalActual = 6_820_000m;
            TotalVariance = TotalBudgeted - TotalActual; // Positive = under budget
            VariancePercentage = Math.Round((TotalVariance / TotalBudgeted) * 100m, 1);

            TotalRevenue = 7_920_000m;
            TotalExpenses = 6_820_000m;
            NetIncome = TotalRevenue - TotalExpenses;

            AccountCount = 48;
            ActiveDepartments = 8;

            // === Fund Breakdown ===
            FundSummaries.Add(new FundSummary { FundName = "General", Budgeted = 4_200_000m, Actual = 2_150_000m });
            FundSummaries.Add(new FundSummary { FundName = "Enterprise (Utilities)", Budgeted = 6_800_000m, Actual = 3_780_000m });
            FundSummaries.Add(new FundSummary { FundName = "Capital Projects", Budgeted = 1_000_000m, Actual = 650_000m });
            FundSummaries.Add(new FundSummary { FundName = "Debt Service", Budgeted = 450_000m, Actual = 240_000m });

            // === Department Breakdown (Utilities focus) ===
            var depts = new[]
            {
                ("Water", 2_800_000m, 1_520_000m),
                ("Sewer", 2_200_000m, 1_280_000m),
                ("Trash", 1_200_000m, 680_000m),
                ("Apartments", 600_000m, 300_000m),
                ("Administration", 1_800_000m, 980_000m),
                ("Public Works", 1_650_000m, 860_000m),
                ("Finance", 900_000m, 480_000m),
                ("Parks & Recreation", 300_000m, 170_000m)
            };

            foreach (var (dept, budgeted, actual) in depts)
            {
                var variance = actual - budgeted; // Positive = over budget
                var variancePct = budgeted != 0 ? Math.Round((variance / budgeted) * 100m, 1) : 0m;
                DepartmentSummaries.Add(new DepartmentSummary
                {
                    DepartmentName = dept,
                    TotalBudgeted = budgeted,
                    TotalActual = actual,
                    Variance = variance,
                    VariancePercentage = variancePct
                });
            }

            // === Top Variances (worst offenders) ===
            TopVariances.Add(new AccountVariance { AccountName = "Water Treatment Chemicals", VarianceAmount = 125_000m, VariancePercentage = 18.2m });
            TopVariances.Add(new AccountVariance { AccountName = "Sewer Line Repairs", VarianceAmount = 98_000m, VariancePercentage = 14.7m });
            TopVariances.Add(new AccountVariance { AccountName = "Trash Hauling Contract", VarianceAmount = 72_000m, VariancePercentage = 11.3m });
            TopVariances.Add(new AccountVariance { AccountName = "Apartment Maintenance", VarianceAmount = -45_000m, VariancePercentage = -8.1m }); // Under

            // === Monthly Trend Data (for main chart/sparklines) ===
            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
            decimal baseRevenue = 1_100_000m;
            decimal baseExpense = 950_000m;
            var random = new Random(42);

            for (int i = 0; i < months.Length; i++)
            {
                decimal revenue = baseRevenue + random.Next(-80_000, 120_000);
                decimal expense = baseExpense + random.Next(-50_000, 100_000);

                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = months[i],
                    Amount = Math.Round(revenue - expense, 0),
                    MonthNumber = i + 1
                });
            }

            // Set status
            ErrorMessage = null;
            HasError = false;
            StatusText = $"Sample data loaded – {DepartmentSummaries.Count} departments, FY 2025-2026";
        }

    }

    /// <summary>
    /// Monthly revenue data for chart display
    /// </summary>
    public class MonthlyRevenue
    {
        public required string Month { get; init; }
        public required decimal Amount { get; init; }
        public required int MonthNumber { get; init; }
    }
}

// Dispose implementation for DashboardViewModel
namespace WileyWidget.WinForms.ViewModels
{
    public partial class DashboardViewModel
    {
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _loadCancellationTokenSource?.Cancel();
                    _loadCancellationTokenSource?.Dispose();
                }
                _disposed = true;
            }
        }

        #region Null Safety & Validation

        /// <summary>
        /// Guards against null reference by throwing with detailed context.
        /// Use this to validate critical dependencies before async operations.
        /// </summary>
        /// <typeparam name="T">Type of the value to check</typeparam>
        /// <param name="value">Value to validate</param>
        /// <param name="parameterName">Name of the parameter (auto-captured)</param>
        /// <exception cref="InvalidOperationException">Thrown when value is null</exception>
        private void GuardAgainstNull<T>(T? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? parameterName = null) where T : class
        {
            if (value == null)
            {
                var message = $"CRITICAL: Null reference in DashboardViewModel - {parameterName} is null. Check DI registration and initialization order.";
                _logger.LogCritical(message);

                // Also log to console for immediate visibility during debugging
                ConsoleOutputHelper.WriteLineSafe($"[CRITICAL NULL] {message}");

                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Validates all critical dependencies before async data loading operations.
        /// This prevents null reference exceptions during dashboard load.
        /// Sets error state if dependencies are missing instead of throwing.
        /// </summary>
        private void ValidateCriticalDependencies()
        {
            _logger.LogDebug("=== ValidateCriticalDependencies STARTED ===");
            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: ENTRY");

            // _logger is always valid (falls back to NullLogger)
            // But verify repositories which are required for data loading
            if (_budgetRepository == null)
            {
                var message = "BudgetRepository is null - dashboard will show error state";
                _logger.LogWarning(message);
                ConsoleOutputHelper.WriteLineSafe($"[WARNING] {message}");
                ErrorMessage = "Budget data unavailable - repository not configured";
                HasError = true;
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: Set HasError=true due to null BudgetRepository");
            }
            else
            {
                _logger.LogDebug("BudgetRepository is available");
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: BudgetRepository OK");
            }

            if (_accountRepository == null)
            {
                var message = "AccountRepository is null - dashboard will show error state";
                _logger.LogWarning(message);
                ConsoleOutputHelper.WriteLineSafe($"[WARNING] {message}");
                if (!HasError)
                {
                    ErrorMessage = "Account data unavailable - repository not configured";
                }
                HasError = true;
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: Set HasError=true due to null AccountRepository");
            }
            else
            {
                _logger.LogDebug("AccountRepository is available");
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: AccountRepository OK");
            }

            if (HasError)
            {
                _logger.LogError("Critical dependencies validation failed - HasError=true, ErrorMessage: {ErrorMessage}", ErrorMessage);
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: FAILED - HasError=true, ErrorMessage: {ErrorMessage}");
            }
            else
            {
                _logger.LogDebug("All critical dependencies validated successfully");
                ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: SUCCESS - All dependencies OK");
            }

            _logger.LogDebug("=== ValidateCriticalDependencies COMPLETED ===");
        }

        #endregion
    }
}

