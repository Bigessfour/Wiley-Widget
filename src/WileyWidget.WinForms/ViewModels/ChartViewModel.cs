using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for budget analytics chart panel using Syncfusion.Windows.Forms.Chart.
    /// Provides comprehensive budget variance analysis with multiple chart types (column, line, pie) and drill-down capabilities.
    /// Supports filtering by department, fiscal year, and date ranges.
    /// </summary>
    public partial class ChartViewModel : ObservableObject
    {
        #region Fields

        private readonly ILogger<ChartViewModel> _logger;
        private readonly IDashboardService _dashboardService;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IConfiguration _configuration;
        private CancellationTokenSource? _loadCancellationTokenSource;

        #endregion

        #region Observable Properties

        /// <summary>
        /// Error message to display to the user when operations fail.
        /// </summary>
        [ObservableProperty]
        private string? errorMessage;

        /// <summary>
        /// Indicates whether a data loading operation is in progress.
        /// </summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>
        /// Selected fiscal year for filtering chart data.
        /// </summary>
        [ObservableProperty]
        private int selectedYear;

        /// <summary>
        /// Selected category filter (e.g., "All Categories", department name).
        /// </summary>
        [ObservableProperty]
        private string selectedCategory = "All Categories";

        /// <summary>
        /// Selected department filter for drill-down analysis.
        /// </summary>
        [ObservableProperty]
        private string? selectedDepartment;

        /// <summary>
        /// Start date for custom date range filtering.
        /// </summary>
        [ObservableProperty]
        private DateTime selectedStartDate;

        /// <summary>
        /// End date for custom date range filtering.
        /// </summary>
        [ObservableProperty]
        private DateTime selectedEndDate;

        /// <summary>
        /// Selected chart type (e.g., "Column", "Line", "Pie").
        /// </summary>
        [ObservableProperty]
        private string selectedChartType = "Column";

        /// <summary>
        /// Total budgeted amount across all displayed items.
        /// </summary>
        [ObservableProperty]
        private decimal totalBudgeted;

        /// <summary>
        /// Total actual spending across all displayed items.
        /// </summary>
        [ObservableProperty]
        private decimal totalActual;

        /// <summary>
        /// Total variance (budgeted - actual) across all displayed items.
        /// </summary>
        [ObservableProperty]
        private decimal totalVariance;

        /// <summary>
        /// Variance as a percentage of budgeted amount.
        /// </summary>
        [ObservableProperty]
        private decimal variancePercentage;

        /// <summary>
        /// Number of departments with budget data.
        /// </summary>
        [ObservableProperty]
        private int departmentCount;

        /// <summary>
        /// Status text for the status bar.
        /// </summary>
        [ObservableProperty]
        private string statusText = "Ready";

        #endregion

        #region Collections

        /// <summary>
        /// Monthly revenue data for line/area charts.
        /// </summary>
        public ObservableCollection<MonthlyRevenue> MonthlyRevenueData { get; } = new();

        /// <summary>
        /// Category breakdown data for pie charts.
        /// </summary>
        public ObservableCollection<(string Category, decimal Value)> PieChartData { get; } = new();

        /// <summary>
        /// Primary chart data (department variance) for column charts.
        /// </summary>
        public ObservableCollection<KeyValuePair<string, decimal>> ChartData { get; } = new();

        /// <summary>
        /// Line chart data points for trend visualization.
        /// </summary>
        public ObservableCollection<ChartDataPoint> LineChartData { get; } = new();

        /// <summary>
        /// Available departments for filter dropdown.
        /// </summary>
        public ObservableCollection<string> AvailableDepartments { get; } = new();

        /// <summary>
        /// Department summary details for drill-down.
        /// </summary>
        public ObservableCollection<DepartmentSummary> DepartmentDetails { get; } = new();

        #endregion

        #region Commands

        /// <summary>
        /// Command to refresh chart data from the repository.
        /// </summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>
        /// Command to load charts for a specific fiscal year.
        /// </summary>
        public IAsyncRelayCommand<int> LoadChartsByYearCommand { get; }

        /// <summary>
        /// Command to export chart data to CSV.
        /// </summary>
        public IRelayCommand ExportDataCommand { get; }

        /// <summary>
        /// Command to reset filters to default values.
        /// </summary>
        public IRelayCommand ResetFiltersCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ChartViewModel"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="dashboardService">Service for retrieving dashboard data.</param>
        /// <param name="budgetRepository">Repository for budget variance analysis.</param>
        /// <param name="configuration">Application configuration.</param>
        public ChartViewModel(
            ILogger<ChartViewModel> logger,
            IDashboardService dashboardService,
            IBudgetRepository budgetRepository,
            IConfiguration? configuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _configuration = configuration ?? new ConfigurationBuilder().Build();

            // Determine default fiscal year using consistent logic
            var now = DateTime.Now;
            var defaultFiscalYear = (now.Month >= 7) ? now.Year + 1 : now.Year;
            SelectedYear = _configuration.GetValue("UI:DefaultFiscalYear", defaultFiscalYear);

            // Set fiscal year date range (July 1 - June 30)
            SelectedStartDate = new DateTime(SelectedYear - 1, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            SelectedEndDate = new DateTime(SelectedYear, 6, 30, 23, 59, 59, DateTimeKind.Utc);

            // Initialize commands
            RefreshCommand = new AsyncRelayCommand(LoadChartDataAsync);
            LoadChartsByYearCommand = new AsyncRelayCommand<int>(year => LoadChartsAsync(year));
            ExportDataCommand = new RelayCommand(ExportData);
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            // Sample population disabled: do not load synthetic utility budget data here
            _logger.LogWarning("ChartViewModel constructor: sample population disabled. Ensure repositories provide real data.");

            _logger.LogInformation("ChartViewModel constructed for FY {FiscalYear}", SelectedYear);
        }

        /// <summary>
        /// Parameterless constructor for design-time support.
        /// </summary>
        public ChartViewModel()
            : this(
                WileyWidget.WinForms.Logging.NullLogger<ChartViewModel>.Instance,
                new FakeDashboardService(),
                new FakeBudgetRepository(),
                null)
        {
            // Design-time/sample population disabled in production builds
            _logger.LogWarning("ChartViewModel default constructor: sample population disabled.");
        }

        #endregion

        #region Data Loading Methods

        /// <summary>
        /// Loads chart data with optional year filter and cancellation support.
        /// Primary method for fetching budget variance data from repository and transforming to chart-ready format.
        /// </summary>
        /// <param name="year">Optional fiscal year to load. Uses SelectedYear if null.</param>
        /// <param name="category">Optional category filter. Uses SelectedCategory if null.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        public async Task LoadChartsAsync(int? year = null, string? category = null, CancellationToken cancellationToken = default)
        {
            // Cancel any existing load operation
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _loadCancellationTokenSource.Token;

            var yearToLoad = year ?? SelectedYear;
            if (yearToLoad < 2000 || yearToLoad > DateTime.UtcNow.Year + 10)
            {
                ErrorMessage = $"Year must be between 2000 and {DateTime.UtcNow.Year + 10}";
                _logger.LogWarning("Invalid year requested: {Year}", yearToLoad);
                return;
            }

            var categoryToLoad = category ?? SelectedCategory;

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StatusText = "Loading chart data...";

                _logger.LogInformation("Loading charts for FY {Year}, category '{Category}', department '{Department}'",
                    yearToLoad, categoryToLoad, SelectedDepartment ?? "All");

                token.ThrowIfCancellationRequested();

                // Determine date range for fiscal year
                var fiscalYearStart = new DateTime(yearToLoad - 1, 7, 1);
                var fiscalYearEnd = new DateTime(yearToLoad, 6, 30);

                // Load budget variance analysis from repository
                var budgetAnalysis = await _budgetRepository.GetBudgetSummaryAsync(
                    fiscalYearStart,
                    fiscalYearEnd,
                    token);

                // Load real Town of Wiley budget data (Sanitation, Apartments, etc.)
                var townOfWileyData = await _budgetRepository.GetTownOfWileyBudgetDataAsync(token);
                if (townOfWileyData != null && townOfWileyData.Count > 0)
                {
                    _logger.LogInformation("Loaded {Count} rows from Town of Wiley budget data", townOfWileyData.Count);
                    foreach (var row in townOfWileyData)
                    {
                        _logger.LogDebug("Budget row: {Department} - {Description} - Budgeted: {Budgeted:C}",
                            row.MappedDepartment, row.Description, row.BudgetYear);
                    }
                }
                else
                {
                    _logger.LogWarning("No Town of Wiley budget data found - check database import");
                }

                token.ThrowIfCancellationRequested();

                if (budgetAnalysis != null)
                {
                    _logger.LogInformation("Budget analysis loaded: {DeptCount} departments, Total Budget: {Budget:C}",
                        budgetAnalysis.DepartmentSummaries?.Count ?? 0,
                        budgetAnalysis.TotalBudgeted);

                    // Transform budget analysis to chart data
                    await TransformBudgetAnalysisToChartsAsync(budgetAnalysis, token);

                    // Update summary properties
                    UpdateSummaryProperties(budgetAnalysis);

                    StatusText = $"Loaded {DepartmentCount} departments for FY {yearToLoad}";
                }
                else
                {
                    _logger.LogWarning("Budget repository returned null analysis - generating sample data");
                    await GenerateSampleDataAsync(yearToLoad, categoryToLoad, token);
                    StatusText = "Displaying sample data (no repository data available)";
                }

                _logger.LogInformation("Chart data loaded successfully: {ChartPoints} data points", ChartData.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Chart load was canceled");
                ErrorMessage = null;
                StatusText = "Load canceled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chart data for FY {Year}", yearToLoad);
                ErrorMessage = $"Failed to load charts: {ex.Message}";
                StatusText = "Error loading data";

                // Fallback to sample data on error
                try
                {
                    await GenerateSampleDataAsync(yearToLoad, categoryToLoad, token);
                    StatusText = "Displaying sample data (load error)";
                }
                catch
                {
                    // Suppress secondary errors
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads chart data using current filter settings.
        /// </summary>
        public async Task LoadChartDataAsync(CancellationToken cancellationToken = default)
        {
            await LoadChartsAsync(SelectedYear, SelectedCategory);
        }

        /// <summary>
        /// Transforms BudgetVarianceAnalysis from repository into chart-ready collections.
        /// </summary>
        private async Task TransformBudgetAnalysisToChartsAsync(BudgetVarianceAnalysis analysis, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Clear existing data
                ChartData.Clear();
                DepartmentDetails.Clear();
                AvailableDepartments.Clear();
                PieChartData.Clear();
                MonthlyRevenueData.Clear();

                if (analysis.DepartmentSummaries == null || !analysis.DepartmentSummaries.Any())
                {
                    _logger.LogWarning("No department summaries in budget analysis");
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Apply department filter if specified
                var departments = analysis.DepartmentSummaries.AsEnumerable();
                if (!string.IsNullOrEmpty(SelectedDepartment) && SelectedDepartment != "All")
                {
                    departments = departments.Where(d =>
                        string.Equals(d.DepartmentName, SelectedDepartment, StringComparison.OrdinalIgnoreCase));
                }

                var deptList = departments.ToList();

                // Populate main chart data (department variance)
                foreach (var dept in deptList.OrderByDescending(d => d.Variance))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ChartData.Add(new KeyValuePair<string, decimal>(dept.DepartmentName ?? "Unknown", dept.Variance));
                    DepartmentDetails.Add(dept);

                    // Populate pie chart data (actual spending by department)
                    if (dept.Actual > 0)
                    {
                        PieChartData.Add((dept.DepartmentName ?? "Unknown", dept.Actual));
                    }
                }

                // Populate available departments for filter dropdown
                AvailableDepartments.Add("All");
                foreach (var dept in analysis.DepartmentSummaries.OrderBy(d => d.DepartmentName))
                {
                    if (!string.IsNullOrEmpty(dept.DepartmentName))
                    {
                        AvailableDepartments.Add(dept.DepartmentName);
                    }
                }

                // Generate monthly revenue trend data from budget entries
                GenerateMonthlyTrendData(analysis);

                _logger.LogInformation("Transformed budget analysis: {ChartPoints} chart points, {DeptCount} departments",
                    ChartData.Count, DepartmentDetails.Count);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates monthly trend data for line charts from budget analysis.
        /// </summary>
        private void GenerateMonthlyTrendData(BudgetVarianceAnalysis analysis)
        {
            var monthNames = new[] { "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

            // Distribute total actual spending evenly across fiscal year months
            // In production, this would query actual monthly data from repository
            var monthlyAmount = analysis.TotalActual / 12;

            for (int i = 0; i < 12; i++)
            {
                var monthName = monthNames[i];
                var amount = monthlyAmount * (decimal)(0.8 + (i * 0.02)); // Slight variation

                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = monthName,
                    MonthNumber = i + 1,
                    Amount = amount
                });

                LineChartData.Add(new ChartDataPoint
                {
                    XValue = monthName,
                    YValue = (double)amount,
                    Label = monthName
                });
            }
        }

        /// <summary>
        /// Updates summary properties from budget analysis.
        /// </summary>
        private void UpdateSummaryProperties(BudgetVarianceAnalysis analysis)
        {
            TotalBudgeted = analysis.TotalBudgeted;
            TotalActual = analysis.TotalActual;
            TotalVariance = analysis.TotalVariance;
            VariancePercentage = analysis.TotalVariancePercentage;
            DepartmentCount = analysis.DepartmentSummaries?.Count ?? 0;

            _logger.LogDebug("Summary updated: Budget={Budget:C}, Actual={Actual:C}, Variance={Variance:C} ({Percent:F1}%)",
                TotalBudgeted, TotalActual, TotalVariance, VariancePercentage);
        }

        #endregion

        #region Sample Data Generation

        /// <summary>
        /// Generates realistic sample data when repository is unavailable or returns no data.
        /// </summary>
        /// <param name="year">Fiscal year for sample data.</param>
        /// <param name="category">Category filter (currently unused for sample generation).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task GenerateSampleDataAsync(int year, string? category, CancellationToken cancellationToken)
        {
            _logger.LogWarning("GenerateSampleDataAsync called: sample data generation disabled. Ensure budget repository is configured.");
            // Clear any synthetic data and return immediately
            ChartData.Clear();
            MonthlyRevenueData.Clear();
            PieChartData.Clear();
            LineChartData.Clear();
            DepartmentDetails.Clear();
            AvailableDepartments.Clear();
            await Task.CompletedTask;
        }

        #endregion

        #region Filter and Command Handlers

        /// <summary>
        /// Handles changes to the SelectedDepartment property.
        /// </summary>
        partial void OnSelectedDepartmentChanged(string? value)
        {
            _logger.LogDebug("Department filter changed to: {Department}", value ?? "All");
            // Reload data with new filter
            _ = LoadChartDataAsync();
        }

        /// <summary>
        /// Handles changes to the SelectedYear property.
        /// </summary>
        partial void OnSelectedYearChanged(int value)
        {
            _logger.LogDebug("Fiscal year changed to: {Year}", value);

            // Update date range for new fiscal year
            SelectedStartDate = new DateTime(value - 1, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            SelectedEndDate = new DateTime(value, 6, 30, 23, 59, 59, DateTimeKind.Utc);

            // Reload data for new year
            _ = LoadChartDataAsync();
        }

        /// <summary>
        /// Handles changes to the SelectedCategory property.
        /// </summary>
        partial void OnSelectedCategoryChanged(string value)
        {
            _logger.LogDebug("Category filter changed to: {Category}", value);
            _ = LoadChartDataAsync();
        }

        /// <summary>
        /// Exports current chart data to CSV format.
        /// </summary>
        private void ExportData()
        {
            try
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Department,Budgeted,Actual,Variance,Variance %");

                foreach (var dept in DepartmentDetails)
                {
                    csv.Append(System.FormattableString.Invariant($"{dept.DepartmentName},{dept.Budgeted:F2},{dept.Actual:F2},{dept.Variance:F2},{dept.VariancePercentage:F2}"));
                    csv.AppendLine();
                }

                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"BudgetChart_FY{SelectedYear}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = System.IO.Path.Combine(desktopPath, fileName);

                System.IO.File.WriteAllText(filePath, csv.ToString());

                StatusText = $"Data exported to {fileName}";
                _logger.LogInformation("Chart data exported to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export chart data");
                ErrorMessage = $"Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Resets all filters to default values.
        /// </summary>
        private void ResetFilters()
        {
            SelectedDepartment = null;
            SelectedCategory = "All Categories";

            var now = DateTime.Now;
            var defaultYear = (now.Month >= 7) ? now.Year + 1 : now.Year;
            SelectedYear = _configuration.GetValue("UI:DefaultFiscalYear", defaultYear);

            _logger.LogInformation("Filters reset to defaults");
            StatusText = "Filters reset";
        }

        /// <summary>
        /// Loads sample utility budget data with Water, Sewer, Trash, and Apartments departments.
        /// </summary>
        private void LoadSampleUtilityBudgetData()
        {
            _logger.LogWarning("LoadSampleUtilityBudgetData called: sample data disabled. Ensure budget repository is configured for real data.");
            AvailableDepartments.Clear();
            DepartmentDetails.Clear();
            ChartData.Clear();
            PieChartData.Clear();
            LineChartData.Clear();
            MonthlyRevenueData.Clear();
            StatusText = "No sample chart data available";
            SelectedCategory = "All Categories";
            SelectedDepartment = null;
        }

        /// <summary>
        /// Calculates and updates the summary totals based on current DepartmentDetails.
        /// </summary>
        private void CalculateTotals()
        {
            if (DepartmentDetails.Count == 0)
            {
                TotalBudgeted = 0m;
                TotalActual = 0m;
                TotalVariance = 0m;
                VariancePercentage = 0m;
                DepartmentCount = 0;
                return;
            }

            TotalBudgeted = DepartmentDetails.Sum(d => d.Budgeted);
            TotalActual = DepartmentDetails.Sum(d => d.Actual);
            TotalVariance = TotalActual - TotalBudgeted; // Positive = over budget
            VariancePercentage = TotalBudgeted > 0 ? Math.Round((TotalVariance / TotalBudgeted) * 100m, 1) : 0m;
            DepartmentCount = DepartmentDetails.Select(d => d.DepartmentName).Distinct().Count();

            _logger.LogDebug("Totals calculated: Budget={Budget:C}, Actual={Actual:C}, Variance={Variance:C} ({Percent:F1}%)",
                TotalBudgeted, TotalActual, TotalVariance, VariancePercentage);
        }

        #endregion

        #region Fake Services for Design-Time

        private class FakeDashboardService : IDashboardService
        {
            public Task<IEnumerable<DashboardItem>> GetDashboardDataAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(Enumerable.Empty<DashboardItem>());

            public Task<IEnumerable<DashboardItem>> GetDashboardItemsAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(Enumerable.Empty<DashboardItem>());

            public Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(CancellationToken cancellationToken = default)
                => Task.FromResult((0, (DateTime?)null, (DateTime?)null));

            public Task PopulateDashboardMetricsFromWileyDataAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task PopulateDepartmentSummariesFromSanitationAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private class FakeBudgetRepository : IBudgetRepository
        {
            public Task<BudgetVarianceAnalysis> GetBudgetSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
                => Task.FromResult<BudgetVarianceAnalysis>(null!);

            public Task AddAsync(BudgetEntry entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<BudgetEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<BudgetEntry?>(null);
            public Task<IEnumerable<BudgetEntry>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task UpdateAsync(BudgetEntry entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByAccountAsync(string accountNumber, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<decimal> GetTotalBudgetedAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(0m);
            public Task<decimal> GetTotalActualAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(0m);
            public Task<int> GetRevenueAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(0);
            public Task<int> GetExpenseAccountCountAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(0);
            public Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult((0, (DateTime?)null, (DateTime?)null));
            public Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByFundAsync(int fundId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByFundAndFiscalYearAsync(int fundId, int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByDepartmentAndFiscalYearAsync(int departmentId, int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetSewerBudgetEntriesAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<IEnumerable<BudgetEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<BudgetEntry>());
            public Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult<BudgetVarianceAnalysis>(null!);
            public Task<List<DepartmentSummary>> GetDepartmentBreakdownAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<DepartmentSummary>());
            public Task<List<FundSummary>> GetFundAllocationsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<FundSummary>());
            public Task<BudgetVarianceAnalysis> GetYearEndSummaryAsync(int year, CancellationToken cancellationToken = default) => Task.FromResult<BudgetVarianceAnalysis>(null!);
            public Task<BudgetVarianceAnalysis> GetBudgetSummaryByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult<BudgetVarianceAnalysis>(null!);
            public Task<BudgetVarianceAnalysis> GetVarianceAnalysisByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult<BudgetVarianceAnalysis>(null!);
            public Task<List<DepartmentSummary>> GetDepartmentBreakdownByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<DepartmentSummary>());
            public Task<List<FundSummary>> GetFundAllocationsByEnterpriseAsync(int enterpriseId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new List<FundSummary>());

            // Implement missing BulkUpdateActualsAsync for design-time fake
            public Task<int> BulkUpdateActualsAsync(IDictionary<string, decimal> actualsByAccountNumber, int fiscalYear, CancellationToken cancellationToken = default)
            {
                // Fake implementation: do nothing and report zero rows updated
                return Task.FromResult(0);
            }

            // Implement missing GetTownOfWileyBudgetDataAsync for design-time fake
            public Task<IReadOnlyList<TownOfWileyBudget2026>> GetTownOfWileyBudgetDataAsync(CancellationToken cancellationToken = default)
            {
                // Fake implementation: return empty list
                return Task.FromResult<IReadOnlyList<TownOfWileyBudget2026>>(Array.Empty<TownOfWileyBudget2026>().AsReadOnly());
            }

            // Implement missing GetHistoricalBudgetSummaryAsync for design-time fake
            public Task<List<HistoricalBudgetYear>> GetHistoricalBudgetSummaryAsync(int yearsBack, int currentFiscalYear, CancellationToken cancellationToken = default)
            {
                // Fake implementation: return empty list
                return Task.FromResult(new List<HistoricalBudgetYear>());
            }
        }

        #endregion
    }

    /// <summary>
    /// Helper extension methods for ChartViewModel.
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// Generates a random decimal value between min (inclusive) and max (exclusive).
        /// </summary>
        public static decimal NextDecimal(this Random random, decimal min, decimal max)
        {
            double range = (double)(max - min);
            double sample = random.NextDouble();
            double scaled = sample * range;
            return min + (decimal)scaled;
        }
    }
}

namespace WileyWidget.WinForms.ViewModels { }
