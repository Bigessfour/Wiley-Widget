using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using WileyWidget.WinForms.Logging;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Dashboard metric display model
    /// </summary>
    public class DashboardMetric
    {
        public required string Name { get; init; }
        public string? Title { get; init; }
        public required double Value { get; init; }
        public required string Unit { get; init; }
        public string Category { get; init; } = "General";
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
        private readonly IUiDispatcher _uiDispatcher;
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

        /// <summary>
        /// Monthly budgeted vs actual data bound to the main ChartControl
        /// Ordered by MonthNumber for correct chronological display
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MonthlyBudgetSummary> monthlySummaries = new();

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
        public string AccountsSummary => HasError ? (ErrorMessage ?? "Load failed") : (AccountCount > 0 ? $"{AccountCount:N0} Municipal Accounts" : MainFormResources.LoadingText);
        public string BudgetStatus => HasError ? (ErrorMessage ?? "Load failed") : (TotalBudgeted > 0 ? $"Variance: {TotalVariance:C} ({VariancePercentage:F1}%)" : StatusText);

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
            IConfiguration? configuration = null,
            IUiDispatcher? uiDispatcher = null)
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
            _uiDispatcher = uiDispatcher ?? new InlineUiDispatcher();

            LoadCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDashboardDataAsync);
            RefreshMetricsCommand = new AsyncRelayCommand(RefreshMetricsAsync);
            LoadFiscalYearCommand = new AsyncRelayCommand<int>(LoadFiscalYearDataAsync);

            if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            {
                InitializeEmptyData();
            }
            else
            {
                ResetDashboardToEmptyState("No production data loaded yet");
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

                var analysis = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd, CancellationToken.None).ConfigureAwait(false);
                if (analysis != null)
                {
                    await InvokeOnUiThreadAsync(() =>
                    {
                        BudgetAnalysis = analysis;
                        TotalBudgeted = analysis.TotalBudgeted;
                        TotalActual = analysis.TotalActual;
                        TotalVariance = analysis.TotalVariance;
                        VariancePercentage = analysis.TotalVariancePercentage;
                    }, cancellationToken).ConfigureAwait(false);

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

            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Dependencies validated successfully");

            var cancellationTokenSource = new CancellationTokenSource();
            var localCancellationToken = cancellationTokenSource.Token;

            _logger.LogDebug("Acquiring load lock...");
            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] LoadDashboardDataAsync: Acquiring semaphore");

            await _loadLock.WaitAsync(localCancellationToken).ConfigureAwait(false);

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
                        await InvokeOnUiThreadAsync(() =>
                        {
                            IsLoading = true;
                            ErrorMessage = null;
                        }, localCancellationToken).ConfigureAwait(false);

                        if (_budgetRepository == null || _accountRepository == null)
                        {
                            _logger.LogWarning("One or more dashboard repositories are not configured; loading empty dashboard state.");
                            ResetDashboardToEmptyState("No production dashboard data available yet");
                            break;
                        }

                        if (retryCount > 0)
                        {
                            _logger.LogInformation("Retrying dashboard data load (attempt {Attempt} of {Max})...", retryCount + 1, MaxRetryAttempts);
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), localCancellationToken).ConfigureAwait(false);
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
                                await _dashboardService.PopulateDashboardMetricsFromWileyDataAsync(localCancellationToken).ConfigureAwait(false);

                                // Also populate department summaries from the mapped data
                                await _dashboardService.PopulateDepartmentSummariesFromSanitationAsync(localCancellationToken).ConfigureAwait(false);

                                _logger.LogInformation("Town of Wiley 2026 budget data loaded successfully");
                                await InvokeOnUiThreadAsync(() =>
                                {
                                    LastUpdated = DateTime.Now;
                                    StatusText = "Wiley 2026 Budget Loaded";
                                    ErrorMessage = null;
                                }, localCancellationToken).ConfigureAwait(false);

                                break; // Successfully loaded—exit retry loop
                            }
                            catch (Exception wileyEx)
                            {
                                _logger.LogWarning(wileyEx, "Failed to load Wiley 2026 budget data from dashboard service - falling back to repository");
                            }
                        }

                        // Fallback: Load budget analysis from repository
                        var analysis = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd, localCancellationToken).ConfigureAwait(false);

                        decimal totalBudgetedValue = 0m;
                        decimal totalActualValue = 0m;
                        int activeDepartmentsCount = 0;

                        if (analysis != null)
                        {
                            totalBudgetedValue = analysis.TotalBudgeted;
                            totalActualValue = analysis.TotalActual;
                            var fundList = analysis.FundSummaries.ToList();
                            var departmentList = analysis.DepartmentSummaries.ToList();
                            activeDepartmentsCount = departmentList.Count;
                            var topVarList = analysis.AccountVariances
                                .OrderByDescending(v => Math.Abs(v.VarianceAmount))
                                .Take(10)
                                .ToList();

                            await InvokeOnUiThreadAsync(() =>
                            {
                                BudgetAnalysis = analysis;
                                TotalBudgeted = analysis.TotalBudgeted;
                                TotalActual = analysis.TotalActual;
                                TotalVariance = analysis.TotalVariance;
                                VariancePercentage = analysis.TotalVariancePercentage;

                                FundSummaries.Clear();
                                foreach (var fund in fundList)
                                {
                                    FundSummaries.Add(fund);
                                }

                                DepartmentSummaries.Clear();
                                foreach (var dept in departmentList)
                                {
                                    DepartmentSummaries.Add(dept);
                                }

                                ActiveDepartments = activeDepartmentsCount;

                                TopVariances.Clear();
                                foreach (var variance in topVarList)
                                {
                                    TopVariances.Add(variance);
                                }
                            }, localCancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.LogInformation("Budget analysis returned no data; keeping empty dashboard metrics for the selected fiscal year.");
                            await InvokeOnUiThreadAsync(() =>
                            {
                                ResetDashboardToEmptyState("No budget data available for selected fiscal year");
                            }, localCancellationToken).ConfigureAwait(false);
                        }

                        // Load account count
                        var accountCount = await _accountRepository.GetCountAsync(localCancellationToken).ConfigureAwait(false);

                        // Calculate revenue and expenses from budget entries
                        var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(currentFiscalYear, localCancellationToken).ConfigureAwait(false);
                        var totalRevenue = budgetEntries
                            .Where(be => be.AccountNumber.StartsWith("4", StringComparison.Ordinal)) // Revenue accounts typically start with 4
                            .Sum(be => be.ActualAmount);

                        var totalExpenses = budgetEntries
                            .Where(be => be.AccountNumber.StartsWith("5", StringComparison.Ordinal) ||
                                         be.AccountNumber.StartsWith("6", StringComparison.Ordinal)) // Expense accounts typically start with 5 or 6
                            .Sum(be => be.ActualAmount);

                        var netIncome = totalRevenue - totalExpenses;

                        // Calculate gauge values (0-100 scale representing percentage of budget used)
                        var totalBudgetGauge = totalBudgetedValue > 0 ? (float)((totalActualValue / totalBudgetedValue) * 100) : 0f;
                        var revenueGauge = totalBudgetedValue > 0 ? (float)((totalRevenue / (totalBudgetedValue * 0.4m)) * 100) : 0f; // Assume 40% of budget is revenue
                        var expensesGauge = totalBudgetedValue > 0 ? (float)((totalExpenses / (totalBudgetedValue * 0.6m)) * 100) : 0f; // Assume 60% of budget is expenses
                        var netPositionGauge = Math.Max(0, Math.Min(100, 50 + (float)(netIncome / 1000000 * 50))); // Scale net position: -1M to +1M = 0-100

                        await InvokeOnUiThreadAsync(() =>
                        {
                            AccountCount = accountCount;
                            TotalRevenue = totalRevenue;
                            TotalExpenses = totalExpenses;
                            NetIncome = netIncome;

                            TotalBudgetGauge = totalBudgetGauge;
                            RevenueGauge = revenueGauge;
                            ExpensesGauge = expensesGauge;
                            NetPositionGauge = netPositionGauge;

                            UpdateMetricsCollection();

                            MunicipalityName = "Town of Wiley";
                            FiscalYear = $"FY {currentFiscalYear}";
                            LastUpdated = DateTime.Now;
                            StatusText = $"Loaded {AccountCount} accounts, {ActiveDepartments} departments";
                            HasError = false;
                        }, localCancellationToken).ConfigureAwait(false);

                        _logger.LogInformation("Dashboard data loaded successfully: {ItemCount} metrics, Revenue: {Revenue:C}, Expenses: {Expenses:C}",
                            Metrics.Count, totalRevenue, totalExpenses);
                        _logger.LogInformation("Dashboard metrics updated: NetPosition={NetPosition:C}, Budgeted={Budgeted:C}, Actual={Actual:C}",
                            netIncome, totalBudgetedValue, totalActualValue);
                        _logger.LogInformation("Dashboard collections: {FundCount} funds, {DeptCount} departments, {VarianceCount} variances",
                            analysis?.FundSummaries.Count ?? 0, analysis?.DepartmentSummaries.Count ?? 0, analysis?.AccountVariances.Count ?? 0);
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
                        await InvokeOnUiThreadAsync(() =>
                        {
                            ErrorMessage = null;
                        }, CancellationToken.None).ConfigureAwait(false);
                        // Stop retrying and exit method without throwing to callers
                        break;
                    }
                    catch (ObjectDisposedException odex)
                    {
                        // The view model or its dependencies were disposed (likely during shutdown).
                        // Do not retry on ObjectDisposedException; stop attempts and exit gracefully.
                        _logger.LogInformation(odex, "Dashboard load aborted due to disposal; stopping retries");
                        await InvokeOnUiThreadAsync(() =>
                        {
                            ErrorMessage = "Dashboard load aborted due to shutdown";
                        }, CancellationToken.None).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading dashboard data");
                        await InvokeOnUiThreadAsync(() =>
                        {
                            ErrorMessage = null;
                            HasError = false;
                        }, CancellationToken.None).ConfigureAwait(false);
                        // Increment retry count so we don't loop infinitely
                        retryCount++;

                        // If this is the final attempt, keep dashboard functional with empty state.
                        if (retryCount >= MaxRetryAttempts)
                        {
                            _logger.LogWarning("Dashboard load failed after {Attempts} attempts - using empty dashboard state", MaxRetryAttempts);
                            await InvokeOnUiThreadAsync(() =>
                            {
                                ResetDashboardToEmptyState("Unable to load dashboard data yet");
                            }, CancellationToken.None).ConfigureAwait(false);
                            break; // Exit retry loop
                        }
                    }
                    finally
                    {
                        await InvokeOnUiThreadAsync(() =>
                        {
                            IsLoading = false;
                        }, CancellationToken.None).ConfigureAwait(false);
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
            await LoadDashboardDataAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loads dashboard data for a specific fiscal year
        /// </summary>
        private async Task LoadFiscalYearDataAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            try
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    IsLoading = true;
                    ErrorMessage = null;
                }, cancellationToken).ConfigureAwait(false);

                if (_budgetRepository == null || _accountRepository == null)
                {
                    _logger.LogWarning("Fiscal year load skipped because repositories are not configured.");
                    await InvokeOnUiThreadAsync(() =>
                    {
                        ResetDashboardToEmptyState("No fiscal year data available yet");
                    }, cancellationToken).ConfigureAwait(false);
                    return;
                }

                _logger.LogInformation("Loading dashboard data for fiscal year {FiscalYear}", fiscalYear);

                var fiscalYearStart = new DateTime(fiscalYear - 1, 7, 1);
                var fiscalYearEnd = new DateTime(fiscalYear, 6, 30);

                var analysis = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd).ConfigureAwait(false);

                if (analysis != null)
                {
                    var fundList = analysis.FundSummaries.ToList();
                    var departmentList = analysis.DepartmentSummaries.ToList();

                    await InvokeOnUiThreadAsync(() =>
                    {
                        BudgetAnalysis = analysis;
                        TotalBudgeted = analysis.TotalBudgeted;
                        TotalActual = analysis.TotalActual;
                        TotalVariance = analysis.TotalVariance;
                        VariancePercentage = analysis.TotalVariancePercentage;

                        FundSummaries.Clear();
                        foreach (var fund in fundList)
                        {
                            FundSummaries.Add(fund);
                        }

                        DepartmentSummaries.Clear();
                        foreach (var dept in departmentList)
                        {
                            DepartmentSummaries.Add(dept);
                        }

                        ActiveDepartments = departmentList.Count;

                        UpdateMetricsCollection();

                        FiscalYear = $"FY {fiscalYear}";
                        LastUpdated = DateTime.Now;
                    }, cancellationToken).ConfigureAwait(false);
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
                await InvokeOnUiThreadAsync(() =>
                {
                    ErrorMessage = $"Failed to load fiscal year data: {ex.Message}";
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    IsLoading = false;
                }, CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Updates the metrics collection for grid display
        /// </summary>
        private void UpdateMetricsCollection()
        {
            AssertUiThreadBoundMutation();
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
            await InvokeOnUiThreadAsync(() =>
            {
                MonthlyRevenueData.Clear();
            }, cancellationToken).ConfigureAwait(false);

            try
            {
                if (_budgetRepository == null)
                {
                    _logger.LogWarning("PopulateMonthlyRevenueData: Budget repository unavailable; leaving monthly revenue data empty.");
                    await InvokeOnUiThreadAsync(() =>
                    {
                        ClearMonthlyRevenueData();
                    }, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // Query actual monthly revenue data from repository
                // Revenue accounts typically start with "4" (4000-4999 range)
                var fiscalYearStart = new DateTime(fiscalYear - 1, 7, 1);
                var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear, cancellationToken).ConfigureAwait(false);

                var monthNames = new[] { "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
                var monthValues = new List<MonthlyRevenue>(12);

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

                    monthValues.Add(new MonthlyRevenue
                    {
                        Month = monthNames[i],
                        MonthNumber = i + 1,
                        Amount = monthlyAmount
                    });
                }

                await InvokeOnUiThreadAsync(() =>
                {
                    MonthlyRevenueData.Clear();
                    foreach (var monthValue in monthValues)
                    {
                        MonthlyRevenueData.Add(monthValue);
                    }
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("PopulateMonthlyRevenueData: Real data loaded - {Count} months, Total: {Total:C}",
                    MonthlyRevenueData.Count, MonthlyRevenueData.Sum(m => m.Amount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PopulateMonthlyRevenueData: Failed to load real data; leaving monthly revenue data empty");
                await InvokeOnUiThreadAsync(() =>
                {
                    ClearMonthlyRevenueData();
                }, CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Clears monthly revenue collections when real monthly data is unavailable.
        /// </summary>
        private void ClearMonthlyRevenueData()
        {
            AssertUiThreadBoundMutation();
            MonthlyRevenueData.Clear();
            _logger.LogInformation("PopulateMonthlyRevenueData: no monthly revenue rows loaded.");
        }

        /// <summary>
        /// Design-time initializer keeps dashboard in an empty state.
        /// </summary>
        private void InitializeEmptyData()
        {
            ResetDashboardToEmptyState("Designer initialized with empty dashboard data");
        }

        /// <summary>
        /// Resets dashboard metrics and collections to a safe empty state.
        /// </summary>
        private void ResetDashboardToEmptyState(string emptyStatusText)
        {
            AssertUiThreadBoundMutation();
            Metrics.Clear();
            FundSummaries.Clear();
            DepartmentSummaries.Clear();
            TopVariances.Clear();
            MonthlyRevenueData.Clear();
            MonthlySummaries.Clear();

            BudgetAnalysis = null;
            TotalBudgeted = 0m;
            TotalActual = 0m;
            TotalVariance = 0m;
            VariancePercentage = 0m;
            TotalRevenue = 0m;
            TotalExpenses = 0m;
            NetIncome = 0m;
            AccountCount = 0;
            ActiveDepartments = 0;
            TotalBudgetGauge = 0f;
            RevenueGauge = 0f;
            ExpensesGauge = 0f;
            NetPositionGauge = 0f;

            ErrorMessage = null;
            HasError = false;
            StatusText = emptyStatusText;
            LastUpdated = DateTime.Now;
        }

        private async Task InvokeOnUiThreadAsync(Action action, CancellationToken cancellationToken = default)
        {
            if (_uiDispatcher.CheckAccess())
            {
                AssertUiThreadBoundMutation();
                action();
                return;
            }

            await _uiDispatcher.InvokeAsync(() =>
            {
                AssertUiThreadBoundMutation();
                action();
            }, cancellationToken).ConfigureAwait(false);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void AssertUiThreadBoundMutation([CallerMemberName] string? memberName = null)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                throw new InvalidOperationException($"UI-bound mutation must occur on the UI thread ({memberName ?? "unknown"}).");
            }
        }

    }

    /// <summary>
    /// Monthly revenue data for chart display
    /// </summary>
    /// <summary>
    /// Monthly budget vs actual data for the main column chart (Budgeted orange, Actual blue)
    /// </summary>
    public class MonthlyBudgetSummary
    {
        public required string Month { get; set; }
        public decimal Budgeted { get; set; }
        public decimal Actual { get; set; }
        /// <summary>
        /// Optional numeric month for proper sorting (1=Jan ... 12=Dec)
        /// </summary>
        public int MonthNumber { get; set; }
    }

    public class MonthlyRevenue
    {
        public required string Month { get; init; }
        public required decimal Amount { get; init; }
        public required int MonthNumber { get; init; }
        public decimal PreviousMonthAmount { get; init; }
        public int Year { get; init; }
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
                    var source = Interlocked.Exchange(ref _loadCancellationTokenSource, null);
                    if (source != null)
                    {
                        try
                        {
                            source.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                        }

                        source.Dispose();
                    }
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
        /// Validates critical dependencies in non-blocking mode.
        /// Missing dependencies are logged, but the dashboard continues in an empty state.
        /// </summary>
        private void ValidateCriticalDependencies()
        {
            _logger.LogDebug("=== ValidateCriticalDependencies STARTED ===");
            ConsoleOutputHelper.WriteLineSafe($"[{DateTime.Now:HH:mm:ss.fff}] ValidateCriticalDependencies: ENTRY");

            if (_budgetRepository == null)
            {
                var message = "BudgetRepository is null - dashboard will display empty/zero budget metrics until configured";
                _logger.LogWarning(message);
                ConsoleOutputHelper.WriteLineSafe($"[WARNING] {message}");
            }
            else
            {
                _logger.LogDebug("BudgetRepository is available");
            }

            if (_accountRepository == null)
            {
                var message = "AccountRepository is null - dashboard will display limited account metrics until configured";
                _logger.LogWarning(message);
                ConsoleOutputHelper.WriteLineSafe($"[WARNING] {message}");
            }
            else
            {
                _logger.LogDebug("AccountRepository is available");
            }

            if (_dashboardService == null)
            {
                var message = "DashboardService is null - some aggregated metrics may be unavailable";
                _logger.LogWarning(message);
                ConsoleOutputHelper.WriteLineSafe($"[WARNING] {message}");
            }

            _logger.LogDebug("=== ValidateCriticalDependencies COMPLETED (non-blocking) ===");
        }

        #endregion
    }
}
