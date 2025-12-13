using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Dashboard metric display model
    /// </summary>
    public class DashboardMetric
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Trend { get; set; } = string.Empty;
        public double ChangePercent { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive dashboard view model with real data from repositories
    /// </summary>
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly IBudgetRepository? _budgetRepository;
        private readonly IMunicipalAccountRepository? _accountRepository;
        private readonly ILogger<DashboardViewModel> _logger;
        private CancellationTokenSource? _loadCancellationTokenSource;
        private readonly System.Threading.SemaphoreSlim _loadLock = new(1, 1);
        private const int MaxRetryAttempts = 3;
        private bool _disposed;

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

        // Legacy property aliases expected by some views
        public decimal TotalBudget => TotalBudgeted;
        public decimal TotalExpenditure => TotalExpenses;
        public decimal RemainingBudget => TotalBudgeted - TotalActual;
        public DateTime LastRefreshed => LastUpdated;

        #endregion

        #region Commands

        public IAsyncRelayCommand LoadCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand<int> LoadFiscalYearCommand { get; }

        // Legacy alias used by some views
        public IAsyncRelayCommand LoadDashboardCommand => LoadCommand;

        #endregion

        public DashboardViewModel(
            IBudgetRepository? budgetRepository,
            IMunicipalAccountRepository? accountRepository,
            ILogger<DashboardViewModel>? logger)
        {
            _budgetRepository = budgetRepository;
            _accountRepository = accountRepository;
            _logger = logger ?? NullLogger<DashboardViewModel>.Instance;

            LoadCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDashboardDataAsync);
            LoadFiscalYearCommand = new AsyncRelayCommand<int>(LoadFiscalYearDataAsync);

            // Initialize with sample data for design-time
            if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            {
                InitializeSampleData();
            }
        }

        public DashboardViewModel()
            : this(null, null, NullLogger<DashboardViewModel>.Instance)
        {
        }

        /// <summary>
        /// Loads complete dashboard data from repositories
        /// </summary>
        private async Task LoadDashboardDataAsync()
        {
            // Cancel any existing load operation
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadCancellationTokenSource.Token;

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
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
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("Loading dashboard data...");
                    }

                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();

                    // Get current fiscal year (hardcoded to 2026 for now, should come from settings)
                    var currentFiscalYear = 2026;
                    var fiscalYearStart = new DateTime(currentFiscalYear - 1, 7, 1); // July 1
                    var fiscalYearEnd = new DateTime(currentFiscalYear, 6, 30); // June 30

                    // Check for cancellation before expensive operations
                    cancellationToken.ThrowIfCancellationRequested();

                    // Load budget analysis from repository
                    var analysis = await _budgetRepository.GetBudgetSummaryAsync(fiscalYearStart, fiscalYearEnd);                if (analysis != null)
                {
                    BudgetAnalysis = analysis;

                    // Update top-level summary
                    TotalBudgeted = analysis.TotalBudgeted;
                    TotalActual = analysis.TotalActual;
                    TotalVariance = analysis.TotalVariance;
                    VariancePercentage = analysis.TotalVariancePercentage;

                    // Update fund summaries
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

                    // Get top variances (largest deviations)
                    TopVariances.Clear();
                    var topVarList = analysis.AccountVariances
                        .OrderByDescending(v => Math.Abs(v.VarianceAmount))
                        .Take(10)
                        .ToList();
                    foreach (var variance in topVarList)
                    {
                        TopVariances.Add(variance);
                    }
                }

                // Load account count
                AccountCount = await _accountRepository.GetCountAsync();

                // Calculate revenue and expenses from budget entries
                var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(currentFiscalYear);
                TotalRevenue = budgetEntries
                    .Where(be => be.AccountNumber.StartsWith("4", StringComparison.Ordinal)) // Revenue accounts typically start with 4
                    .Sum(be => be.ActualAmount);

                TotalExpenses = budgetEntries
                    .Where(be => be.AccountNumber.StartsWith("5", StringComparison.Ordinal) ||
                                 be.AccountNumber.StartsWith("6", StringComparison.Ordinal)) // Expense accounts typically start with 5 or 6
                    .Sum(be => be.ActualAmount);

                NetIncome = TotalRevenue - TotalExpenses;

                // Update gauge values (0-100 scale representing percentage of budget used)
                TotalBudgetGauge = TotalBudgeted > 0 ? (float)((TotalActual / TotalBudgeted) * 100) : 0f;
                RevenueGauge = TotalBudgeted > 0 ? (float)((TotalRevenue / (TotalBudgeted * 0.4m)) * 100) : 0f; // Assume 40% of budget is revenue
                ExpensesGauge = TotalBudgeted > 0 ? (float)((TotalExpenses / (TotalBudgeted * 0.6m)) * 100) : 0f; // Assume 60% of budget is expenses
                NetPositionGauge = Math.Max(0, Math.Min(100, 50 + (float)(NetIncome / 1000000 * 50))); // Scale net position: -1M to +1M = 0-100

                // Update metrics collection for grid display
                UpdateMetricsCollection();

                // Populate monthly revenue data for chart
                PopulateMonthlyRevenueData(currentFiscalYear);

                // Update metadata
                MunicipalityName = "Town of Wiley";
                FiscalYear = $"FY {currentFiscalYear}";
                LastUpdated = DateTime.Now;
                StatusText = $"Loaded {AccountCount} accounts, {ActiveDepartments} departments";

                _logger.LogInformation("Dashboard data loaded successfully. Total Budget: {Budget:C}, Total Actual: {Actual:C}, Variance: {Variance:C}",
                    TotalBudgeted, TotalActual, TotalVariance);
                // Successfully loaded—exit retry loop
                break;
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation exceptions
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                ErrorMessage = $"Failed to load dashboard: {ex.Message}";
                // Increment retry count so we don't loop infinitely
                retryCount++;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
    finally
    {
        _loadLock.Release();
    }
}
        /// Refreshes the dashboard data
        /// </summary>
        private async Task RefreshDashboardDataAsync()
        {
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// Loads dashboard data for a specific fiscal year
        /// </summary>
        private async Task LoadFiscalYearDataAsync(int fiscalYear)
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
        }

        /// <summary>
        /// Populates monthly revenue data for chart display
        /// </summary>
        private void PopulateMonthlyRevenueData(int fiscalYear)
        {
            MonthlyRevenueData.Clear();

            // Generate 12 months of data (July to June for fiscal year)
            var monthNames = new[] { "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
            var random = new Random(fiscalYear);

            for (int i = 0; i < 12; i++)
            {
                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = monthNames[i],
                    MonthNumber = i + 1,
                    // Generate sample data - in production, this would come from actual data
                    Amount = TotalRevenue > 0 ? TotalRevenue / 12 * (decimal)(0.8 + random.NextDouble() * 0.4) : 0
                });
            }
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
            PopulateMonthlyRevenueData(2026);
        }

    }

    /// <summary>
    /// Monthly revenue data for chart display
    /// </summary>
    public class MonthlyRevenue
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int MonthNumber { get; set; }
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
    }
}
