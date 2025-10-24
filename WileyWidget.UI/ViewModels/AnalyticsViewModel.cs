#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Prism.Mvvm;
using Prism.Commands;
using Prism.Events;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for the Analytics section of the application
/// </summary>
public partial class AnalyticsViewModel : AsyncViewModelBase
{
    private string? _selectedChartType;
    private string? _selectedTimePeriod;
    private bool _isDataLoaded;
    private readonly IEventAggregator _eventAggregator;

    /// <summary>
    /// Gets the collection of available chart types
    /// </summary>
    public ObservableCollection<string> ChartTypes { get; } = new()
    {
        "Budget vs Actual",
        "Trend Analysis",
        "Department Comparison",
        "Fund Analysis",
        "Variance Report"
    };

    /// <summary>
    /// Gets the collection of available time periods
    /// </summary>
    public ObservableCollection<string> TimePeriods { get; } = new()
    {
        "Current Year",
        "Last 12 Months",
        "Year to Date",
        "Prior Year",
        "Custom Range"
    };

    /// <summary>
    /// Gets or sets the selected chart type
    /// </summary>
    public string? SelectedChartType
    {
        get => _selectedChartType;
        set => SetProperty(ref _selectedChartType, value);
    }

    /// <summary>
    /// Gets or sets the selected time period
    /// </summary>
    public string? SelectedTimePeriod
    {
        get => _selectedTimePeriod;
        set => SetProperty(ref _selectedTimePeriod, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether data has been loaded
    /// </summary>
    public bool IsDataLoaded
    {
        get => _isDataLoaded;
        set => SetProperty(ref _isDataLoaded, value);
    }

    private DateTime? _startDate;
    private DateTime? _endDate;
    private string? _enterpriseId;
    private string? _filter;
    private ObservableCollection<string> _filterOptions = new();

    /// <summary>
    /// Gets or sets the start date for analytics filtering
    /// </summary>
    public DateTime? StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    /// <summary>
    /// Gets or sets the end date for analytics filtering
    /// </summary>
    public DateTime? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    /// <summary>
    /// Gets or sets the selected enterprise ID for filtering
    /// </summary>
    public string? EnterpriseId
    {
        get => _enterpriseId;
        set => SetProperty(ref _enterpriseId, value);
    }

    /// <summary>
    /// Gets the collection of available enterprises for filtering
    /// </summary>
    public ObservableCollection<Enterprise> Enterprises { get; } = new();

    /// <summary>
    /// Gets or sets the current filter text
    /// </summary>
    public string? Filter
    {
        get => _filter;
        set => SetProperty(ref _filter, value);
    }

    /// <summary>
    /// Gets the collection of available filter options
    /// </summary>
    public ObservableCollection<string> FilterOptions => _filterOptions;

    /// <summary>
    /// Gets the command to load analytics data
    /// NOTE: hides AsyncViewModelBase.LoadDataCommand by design to provide a view-specific implementation.
    /// </summary>
    public new DelegateCommand LoadDataCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to refresh the analytics data
    /// </summary>
    public DelegateCommand RefreshDataCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to export the current chart
    /// </summary>
    public DelegateCommand ExportChartCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to drill down into analytics data
    /// </summary>
    public DelegateCommand DrillDownCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to select item for drill down
    /// </summary>
    public DelegateCommand<object> SelectDrillDownItemCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to go back from drill down
    /// </summary>
    public DelegateCommand BackFromDrillDownCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command to refresh analytics data
    /// </summary>
    public DelegateCommand RefreshAnalyticsCommand { get; private set; } = null!;

    /// <summary>
    /// Event raised when analytics data has been loaded
    /// </summary>
    public event EventHandler? DataLoaded;

    /// <summary>
    /// Current analytics data for export
    /// </summary>
    public ObservableCollection<object> CurrentAnalyticsData { get; } = new();

    /// <summary>
    /// Summary statistics for the current analytics
    /// </summary>
    public Dictionary<string, decimal> SummaryStatistics { get; } = new();

    /// <summary>
    /// Detailed drill down data
    /// </summary>
    public ObservableCollection<object> DrillDownData { get; } = new();

    /// <summary>
    /// Indicates if drill down data is available
    /// </summary>
    private bool _hasDrillDownData;
    public bool HasDrillDownData
    {
        get => _hasDrillDownData;
        set
        {
            if (_hasDrillDownData != value)
            {
                _hasDrillDownData = value;
                RaisePropertyChanged();
                BackFromDrillDownCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Current drill down level
    /// </summary>
    private int _drillDownLevel;
    public int DrillDownLevel
    {
        get => _drillDownLevel;
        set
        {
            if (_drillDownLevel != value)
            {
                _drillDownLevel = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Selected item for drill down
    /// </summary>
    private object? _selectedDrillDownItem;
    public object? SelectedDrillDownItem
    {
        get => _selectedDrillDownItem;
        set
        {
            if (_selectedDrillDownItem != value)
            {
                _selectedDrillDownItem = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Budget repository for data access
    /// </summary>
    private readonly IBudgetRepository _budgetRepository;

    /// <summary>
    /// Municipal account repository for data access
    /// </summary>
    private readonly IMunicipalAccountRepository _municipalAccountRepository;

    /// <summary>
    /// Report export service for exporting data
    /// </summary>
    private readonly IReportExportService _reportExportService;

    /// <summary>
    /// Enterprise repository for data access
    /// </summary>
    private readonly IEnterpriseRepository _enterpriseRepository;

    /// <summary>
    /// Initializes a new instance of the AnalyticsViewModel class
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="budgetRepository">The budget repository for data access</param>
    /// <param name="municipalAccountRepository">The municipal account repository for data access</param>
    /// <param name="reportExportService">The report export service for exporting data</param>
    /// <param name="enterpriseRepository">The enterprise repository for data access</param>
    public AnalyticsViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<AnalyticsViewModel> logger, IBudgetRepository budgetRepository, IMunicipalAccountRepository municipalAccountRepository, IReportExportService reportExportService, IEnterpriseRepository enterpriseRepository, IEventAggregator eventAggregator)
        : base(dispatcherHelper, logger)
    {
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        _municipalAccountRepository = municipalAccountRepository ?? throw new ArgumentNullException(nameof(municipalAccountRepository));
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

        Enterprises = new ObservableCollection<Enterprise>();

        // Subscribe to DataLoadedEvent from DashboardViewModel
        _eventAggregator.GetEvent<DataLoadedEvent>().Subscribe(OnDataLoaded, ThreadOption.UIThread);

        InitializeCommands();

        // Initialize default selections
        SelectedChartType = ChartTypes.FirstOrDefault();
        SelectedTimePeriod = TimePeriods.FirstOrDefault();
    }

    private void OnDataLoaded(DataLoadedEvent message)
    {
        // Handle dashboard data loaded event - refresh analytics data if needed
        if (message.ViewModelName == "DashboardViewModel" && !_isDataLoaded)
        {
            Logger.LogInformation("Received DataLoadedEvent from {ViewModelName} with {ItemCount} items. Refreshing analytics data.",
                message.ViewModelName, message.ItemCount);

            // Optionally refresh analytics data when dashboard loads
            // This ensures analytics stays in sync with dashboard data
            _ = ExecuteRefreshAnalyticsDataAsync();
        }
    }

    private void InitializeCommands()
    {
        LoadDataCommand = new DelegateCommand(async () => await ExecuteLoadAnalyticsDataAsync(), () => CanLoadData());
        RefreshDataCommand = new DelegateCommand(async () => await ExecuteRefreshAnalyticsDataAsync(), () => CanRefreshData());
        ExportChartCommand = new DelegateCommand(async () => await ExecuteExportChartAsync(), () => CanExportChart());
        DrillDownCommand = new DelegateCommand(async () => await ExecuteDrillDownAsync(), () => CanDrillDown());
        SelectDrillDownItemCommand = new DelegateCommand<object>(ExecuteSelectDrillDownItem);
        BackFromDrillDownCommand = new DelegateCommand(ExecuteBackFromDrillDown, () => HasDrillDownData);
        RefreshAnalyticsCommand = new DelegateCommand(async () => await ExecuteRefreshAnalyticsDataAsync(), () => CanRefreshData());
    }

    private bool CanLoadData()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(SelectedChartType) && !string.IsNullOrWhiteSpace(SelectedTimePeriod);
    }

    private bool CanRefreshData()
    {
        return IsDataLoaded && !IsBusy;
    }

    private bool CanExportChart()
    {
        return IsDataLoaded && !IsBusy;
    }

    private async Task ExecuteLoadAnalyticsDataAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Clear previous data
            CurrentAnalyticsData.Clear();
            SummaryStatistics.Clear();
            IsDataLoaded = false;

            // Determine date range based on selected time period
            var (startDate, endDate) = GetDateRangeForPeriod(SelectedTimePeriod);

            // Load data based on selected chart type
            switch (SelectedChartType)
            {
                case "Budget vs Actual":
                    await LoadBudgetVsActualDataAsync(startDate, endDate);
                    break;
                case "Trend Analysis":
                    await LoadTrendAnalysisDataAsync(startDate, endDate);
                    break;
                case "Department Comparison":
                    await LoadDepartmentComparisonDataAsync(startDate, endDate);
                    break;
                case "Fund Analysis":
                    await LoadFundAnalysisDataAsync(startDate, endDate);
                    break;
                case "Variance Report":
                    await LoadVarianceReportDataAsync(startDate, endDate);
                    break;
                default:
                    Logger.LogWarning("Unknown chart type selected: {ChartType}", SelectedChartType);
                    break;
            }

            IsDataLoaded = true;
            RaiseDataLoaded();
        }, $"Loading {SelectedChartType} data for {SelectedTimePeriod}...");
    }

    private async Task ExecuteRefreshAnalyticsDataAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Clear existing data
            IsDataLoaded = false;

            // Reload data
            await ExecuteLoadAnalyticsDataAsync();

            Logger.LogInformation("Refreshed analytics data");
        }, "Refreshing analytics data...");
    }

    private async Task ExecuteExportChartAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Create save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Chart Data",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"{SelectedChartType?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;

                // Export the current analytics data
                if (CurrentAnalyticsData.Any())
                {
                    await _reportExportService.ExportToExcelAsync(CurrentAnalyticsData, filePath);
                    Logger.LogInformation("Analytics data exported to {FilePath} with {Count} records", filePath, CurrentAnalyticsData.Count);
                }
                else
                {
                    // Export basic metadata if no data is loaded
                    var metadata = new List<object>
                    {
                        new { ChartType = SelectedChartType, TimePeriod = SelectedTimePeriod, ExportedAt = DateTime.Now, Status = "No data loaded" }
                    };
                    await _reportExportService.ExportToExcelAsync(metadata, filePath);
                    Logger.LogInformation("Analytics metadata exported to {FilePath}", filePath);
                }
            }
        }, "Exporting chart...");
    }

    private bool CanDrillDown()
    {
        return IsDataLoaded && !IsBusy;
    }

    private async Task ExecuteDrillDownAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Implement drill down logic based on current chart type
            switch (SelectedChartType)
            {
                case "Budget vs Actual":
                    await DrillDownBudgetVsActualAsync();
                    break;
                case "Department Comparison":
                    await DrillDownDepartmentComparisonAsync();
                    break;
                case "Fund Analysis":
                    await DrillDownFundAnalysisAsync();
                    break;
                case "Trend Analysis":
                    await DrillDownTrendAnalysisAsync();
                    break;
                case "Variance Report":
                    await DrillDownVarianceReportAsync();
                    break;
                default:
                    Logger.LogWarning("Drill down not implemented for chart type: {ChartType}", SelectedChartType);
                    break;
            }
        }, "Drilling down into data...");
    }

    private void ExecuteSelectDrillDownItem(object? item)
    {
        if (item != null)
        {
            SelectedDrillDownItem = item;
            Logger.LogInformation("Selected drill down item: {Item}", item);
        }
    }

    private void ExecuteBackFromDrillDown()
    {
        DrillDownData.Clear();
        HasDrillDownData = false;
        DrillDownLevel = 0;
        SelectedDrillDownItem = null;
        Logger.LogInformation("Returned from drill down view");
    }

    private void RaiseDataLoaded()
    {
        DataLoaded?.Invoke(this, EventArgs.Empty);
    }

    private (DateTime startDate, DateTime endDate) GetDateRangeForPeriod(string? period)
    {
        var now = DateTime.Now;
        return period switch
        {
            "Current Year" => (new DateTime(now.Year, 1, 1), new DateTime(now.Year, 12, 31)),
            "Last 12 Months" => (now.AddMonths(-12), now),
            "Year to Date" => (new DateTime(now.Year, 1, 1), now),
            "Prior Year" => (new DateTime(now.Year - 1, 1, 1), new DateTime(now.Year - 1, 12, 31)),
            "Custom Range" => (StartDate ?? now.AddMonths(-6), EndDate ?? now),
            _ => (now.AddMonths(-6), now)
        };
    }

    private async Task LoadBudgetVsActualDataAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var budgetSummary = await _budgetRepository.GetBudgetSummaryAsync(startDate, endDate);

            // Add summary data for export
            CurrentAnalyticsData.Add(new
            {
                Category = "Budget Summary",
                TotalBudgeted = budgetSummary.TotalBudgeted,
                TotalActual = budgetSummary.TotalActual,
                TotalVariance = budgetSummary.TotalVariance,
                TotalVariancePercentage = budgetSummary.TotalVariancePercentage,
                Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}"
            });

            // Add fund summaries
            foreach (var fundSummary in budgetSummary.FundSummaries)
            {
                CurrentAnalyticsData.Add(new
                {
                    Category = "Fund Summary",
                    FundName = fundSummary.FundName,
                    BudgetedAmount = fundSummary.TotalBudgeted,
                    ActualAmount = fundSummary.TotalActual,
                    Variance = fundSummary.TotalBudgeted - fundSummary.TotalActual,
                    VariancePercentage = fundSummary.TotalBudgeted != 0 ?
                        ((fundSummary.TotalActual - fundSummary.TotalBudgeted) / fundSummary.TotalBudgeted) * 100 : 0
                });
            }

            // Update summary statistics
            SummaryStatistics["Total Budgeted"] = budgetSummary.TotalBudgeted;
            SummaryStatistics["Total Actual"] = budgetSummary.TotalActual;
            SummaryStatistics["Total Variance"] = budgetSummary.TotalVariance;
            SummaryStatistics["Average Variance %"] = budgetSummary.FundSummaries.Any() ?
                budgetSummary.FundSummaries.Average(f => f.VariancePercentage) : 0;

            Logger.LogInformation("Loaded budget vs actual data for period {StartDate} to {EndDate}", startDate, endDate);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load budget vs actual data");
        }
    }

    private async Task LoadTrendAnalysisDataAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            // Load trend data from budget repository for multiple years
            var currentYear = startDate.Year;
            var years = new[] { currentYear - 2, currentYear - 1, currentYear };

            foreach (var year in years)
            {
                try
                {
                    var budgetData = await _budgetRepository.GetByFiscalYearAsync(year);
                    var totalBudget = budgetData.Sum(b => b.BudgetedAmount);
                    var totalActual = budgetData.Sum(b => b.ActualAmount);

                    CurrentAnalyticsData.Add(new
                    {
                        Category = "Trend Data",
                        FiscalYear = year,
                        TotalBudget = totalBudget,
                        TotalActual = totalActual,
                        Variance = totalBudget - totalActual,
                        BudgetUtilization = totalBudget > 0 ? (totalActual / totalBudget) * 100 : 0
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to load data for fiscal year {Year}", year);
                }
            }

            // Calculate trend statistics
            var trendData = CurrentAnalyticsData.Where(d => d.GetType().GetProperty("FiscalYear") != null).ToList();
            if (trendData.Any())
            {
                var budgets = trendData.Select(d => (decimal)d.GetType().GetProperty("TotalBudget")?.GetValue(d)!).ToList();
                var actuals = trendData.Select(d => (decimal)d.GetType().GetProperty("TotalActual")?.GetValue(d)!).ToList();

                SummaryStatistics["Average Budget"] = budgets.Any() ? budgets.Average() : 0;
                SummaryStatistics["Average Actual"] = actuals.Any() ? actuals.Average() : 0;
                SummaryStatistics["Budget Growth Trend"] = budgets.Count >= 2 ?
                    ((budgets.Last() - budgets.First()) / Math.Max(budgets.First(), 1)) * 100 : 0;
            }

            Logger.LogInformation("Loaded trend analysis data for {StartDate} to {EndDate}", startDate, endDate);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load trend analysis data");
        }
    }

    private async Task LoadDepartmentComparisonDataAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            // Load all municipal accounts for department comparison
            var accounts = await _municipalAccountRepository.GetAllAsync();

            // Group by department and calculate totals
            var departmentGroups = accounts
                .GroupBy(a => a.DepartmentId)
                .Select(g => new
                {
                    DepartmentId = g.Key,
                    DepartmentName = g.First().Department?.Name ?? $"Department {g.Key}",
                    AccountCount = g.Count(),
                    TotalBudget = g.Sum(a => a.BudgetAmount),
                    TotalActual = g.Sum(a => a.Balance),
                    AverageBudgetUtilization = g.Average(a => a.BudgetAmount > 0 ?
                        (a.Balance / a.BudgetAmount) * 100 : 0)
                })
                .OrderByDescending(d => d.TotalBudget)
                .ToList();

            foreach (var dept in departmentGroups)
            {
                CurrentAnalyticsData.Add(new
                {
                    Category = "Department Summary",
                    DepartmentName = dept.DepartmentName,
                    AccountCount = dept.AccountCount,
                    TotalBudget = dept.TotalBudget,
                    TotalActual = dept.TotalActual,
                    Variance = dept.TotalBudget - dept.TotalActual,
                    BudgetUtilization = dept.TotalBudget > 0 ? (dept.TotalActual / dept.TotalBudget) * 100 : 0,
                    AverageUtilization = dept.AverageBudgetUtilization
                });
            }

            // Update summary statistics
            SummaryStatistics["Total Departments"] = departmentGroups.Count;
            SummaryStatistics["Total Accounts"] = accounts.Count();
            SummaryStatistics["Average Budget per Department"] = departmentGroups.Any() ? departmentGroups.Average(d => d.TotalBudget) : 0;
            SummaryStatistics["Average Accounts per Department"] = departmentGroups.Any() ? (decimal)departmentGroups.Average(d => d.AccountCount) : 0;

            Logger.LogInformation("Loaded department comparison data with {Count} accounts across {DeptCount} departments",
                accounts.Count(), departmentGroups.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load department comparison data");
        }
    }

    private async Task LoadFundAnalysisDataAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            // Load accounts by different fund types
            var fundTypes = Enum.GetValues(typeof(MunicipalFundType)).Cast<MunicipalFundType>();

            foreach (var fundType in fundTypes)
            {
                try
                {
                    var fundAccounts = await _municipalAccountRepository.GetByFundAsync(fundType);
                    var totalBudget = fundAccounts.Sum(a => a.BudgetAmount);
                    var totalActual = fundAccounts.Sum(a => a.Balance);

                    CurrentAnalyticsData.Add(new
                    {
                        Category = "Fund Analysis",
                        FundType = fundType.ToString(),
                        AccountCount = fundAccounts.Count(),
                        TotalBudget = totalBudget,
                        TotalActual = totalActual,
                        Variance = totalBudget - totalActual,
                        BudgetUtilization = totalBudget > 0 ? (totalActual / totalBudget) * 100 : 0
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to load data for fund type {FundType}", fundType);
                }
            }

            // Update summary statistics
            var fundData = CurrentAnalyticsData.Where(d => d.GetType().GetProperty("FundType") != null).ToList();
            SummaryStatistics["Total Funds Analyzed"] = fundData.Count;
            SummaryStatistics["Total Fund Budget"] = fundData.Sum(d => (decimal)d.GetType().GetProperty("TotalBudget")?.GetValue(d)!);
            SummaryStatistics["Total Fund Actual"] = fundData.Sum(d => (decimal)d.GetType().GetProperty("TotalActual")?.GetValue(d)!);

            Logger.LogInformation("Loaded fund analysis data for {FundCount} fund types", fundData.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load fund analysis data");
        }
    }

    private async Task LoadVarianceReportDataAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var varianceAnalysis = await _budgetRepository.GetVarianceAnalysisAsync(startDate, endDate);

            // Add overall variance summary
            CurrentAnalyticsData.Add(new
            {
                Category = "Variance Summary",
                Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                TotalBudgeted = varianceAnalysis.TotalBudgeted,
                TotalActual = varianceAnalysis.TotalActual,
                TotalVariance = varianceAnalysis.TotalVariance,
                TotalVariancePercentage = varianceAnalysis.TotalVariancePercentage,
                AnalysisDate = varianceAnalysis.AnalysisDate
            });

            // Add individual account variances (if available)
            // Note: The variance analysis model might not have individual account details
            // This would depend on the repository implementation

            // Update summary statistics
            SummaryStatistics["Total Budgeted"] = varianceAnalysis.TotalBudgeted;
            SummaryStatistics["Total Actual"] = varianceAnalysis.TotalActual;
            SummaryStatistics["Total Variance"] = varianceAnalysis.TotalVariance;
            SummaryStatistics["Variance Percentage"] = varianceAnalysis.TotalVariancePercentage;

            Logger.LogInformation("Loaded variance report data for period {StartDate} to {EndDate}", startDate, endDate);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load variance report data");
        }
    }

    private async Task DrillDownBudgetVsActualAsync()
    {
        try
        {
            // Load detailed budget entries for drill down
            var (startDate, endDate) = GetDateRangeForPeriod(SelectedTimePeriod);
            var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(startDate.Year);

            DrillDownData.Clear();
            foreach (var entry in budgetEntries.Take(50)) // Limit for performance
            {
                DrillDownData.Add(new
                {
                    AccountNumber = entry.AccountNumber,
                    Description = entry.Description,
                    BudgetedAmount = entry.BudgetedAmount,
                    ActualAmount = entry.ActualAmount,
                    Variance = entry.Variance,
                    VariancePercent = entry.BudgetedAmount != 0 ? (entry.Variance / entry.BudgetedAmount) * 100 : 0,
                    Department = entry.Department?.Name ?? "Unknown"
                });
            }

            HasDrillDownData = true;
            DrillDownLevel = 1;
            Logger.LogInformation("Drilled down into budget vs actual details with {Count} entries", DrillDownData.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to drill down into budget vs actual data");
        }
    }

    private async Task DrillDownDepartmentComparisonAsync()
    {
        try
        {
            // Load detailed account data grouped by department
            var accounts = await _municipalAccountRepository.GetAllAsync();

            DrillDownData.Clear();
            var departmentDetails = accounts
                .Where(a => a.Department != null)
                .GroupBy(a => a.DepartmentId)
                .SelectMany(g => g.Select(a => new
                {
                    DepartmentName = g.First().Department?.Name ?? "Unknown",
                    AccountNumber = a.AccountNumber?.ToString() ?? "",
                    Description = a.Name,
                    BudgetAmount = a.BudgetAmount,
                    Balance = a.Balance,
                    Variance = a.BudgetAmount - a.Balance,
                    AccountType = a.Type.ToString()
                }))
                .Take(100); // Limit for performance

            foreach (var detail in departmentDetails)
            {
                DrillDownData.Add(detail);
            }

            HasDrillDownData = true;
            DrillDownLevel = 1;
            Logger.LogInformation("Drilled down into department comparison details with {Count} accounts", DrillDownData.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to drill down into department comparison data");
        }
    }

    private async Task DrillDownFundAnalysisAsync()
    {
        try
        {
            // Load detailed account data by fund type
            var accounts = await _municipalAccountRepository.GetAllAsync();

            DrillDownData.Clear();
            var fundDetails = accounts
                .GroupBy(a => a.Fund)
                .SelectMany(g => g.Select(a => new
                {
                    FundType = g.Key.ToString(),
                    AccountNumber = a.AccountNumber?.ToString() ?? "",
                    Description = a.Name,
                    BudgetAmount = a.BudgetAmount,
                    Balance = a.Balance,
                    Variance = a.BudgetAmount - a.Balance,
                    Department = a.Department?.Name ?? "Unknown"
                }))
                .Take(100); // Limit for performance

            foreach (var detail in fundDetails)
            {
                DrillDownData.Add(detail);
            }

            HasDrillDownData = true;
            DrillDownLevel = 1;
            Logger.LogInformation("Drilled down into fund analysis details with {Count} accounts", DrillDownData.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to drill down into fund analysis data");
        }
    }

    private async Task DrillDownTrendAnalysisAsync()
    {
        try
        {
            // Load multi-year trend data
            DrillDownData.Clear();
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 3; year <= currentYear; year++)
            {
                try
                {
                    var budgetData = await _budgetRepository.GetByFiscalYearAsync(year);
                    var quarterlyData = budgetData
                        .GroupBy(b => (b.StartPeriod.Month - 1) / 3 + 1) // Quarter calculation
                        .Select(g => new
                        {
                            FiscalYear = year,
                            Quarter = g.Key,
                            TotalBudget = g.Sum(b => b.BudgetedAmount),
                            TotalActual = g.Sum(b => b.ActualAmount),
                            AccountCount = g.Count()
                        });

                    foreach (var quarter in quarterlyData)
                    {
                        DrillDownData.Add(quarter);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to load trend data for year {Year}", year);
                }
            }

            HasDrillDownData = true;
            DrillDownLevel = 1;
            Logger.LogInformation("Drilled down into trend analysis details with {Count} data points", DrillDownData.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to drill down into trend analysis data");
        }
    }

    private async Task DrillDownVarianceReportAsync()
    {
        try
        {
            // Load detailed variance data
            var (startDate, endDate) = GetDateRangeForPeriod(SelectedTimePeriod);
            var varianceAnalysis = await _budgetRepository.GetVarianceAnalysisAsync(startDate, endDate);

            DrillDownData.Clear();

            // Add detailed variance entries (mock data since the model may not have individual entries)
            var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(startDate.Year);
            var varianceDetails = budgetEntries
                .Where(b => Math.Abs(b.BudgetedAmount != 0 ? (b.Variance / b.BudgetedAmount) * 100 : 0) > 5) // Show significant variances
                .Select(b => new
                {
                    AccountNumber = b.AccountNumber,
                    Description = b.Description,
                    BudgetedAmount = b.BudgetedAmount,
                    ActualAmount = b.ActualAmount,
                    Variance = b.Variance,
                    VariancePercent = b.BudgetedAmount != 0 ? (b.Variance / b.BudgetedAmount) * 100 : 0,
                    Department = b.Department?.Name ?? "Unknown",
                    VarianceSeverity = Math.Abs(b.BudgetedAmount != 0 ? (b.Variance / b.BudgetedAmount) * 100 : 0) > 20 ? "High" :
                                     Math.Abs(b.BudgetedAmount != 0 ? (b.Variance / b.BudgetedAmount) * 100 : 0) > 10 ? "Medium" : "Low"
                })
                .OrderByDescending(v => Math.Abs(v.VariancePercent))
                .Take(50);

            foreach (var detail in varianceDetails)
            {
                DrillDownData.Add(detail);
            }

            HasDrillDownData = true;
            DrillDownLevel = 1;
            Logger.LogInformation("Drilled down into variance report details with {Count} significant variances", DrillDownData.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to drill down into variance report data");
        }
    }

    /// <summary>
    /// Initializes the ViewModel by loading enterprise data
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadEnterprisesAsync();
    }

    /// <summary>
    /// Loads enterprise data for the analytics filters
    /// </summary>
    private async Task LoadEnterprisesAsync()
    {
        try
        {
            var enterprises = await _enterpriseRepository.GetAllAsync();
            await DispatcherHelper.InvokeAsync(() =>
            {
                Enterprises.Clear();
                foreach (var enterprise in enterprises)
                {
                    Enterprises.Add(enterprise);
                }
            });
            Logger.LogInformation("Loaded {Count} enterprises for analytics filters", enterprises.Count());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load enterprises for analytics");
        }
    }

    /// <summary>
    /// Event arguments for analytics data loaded events
    /// </summary>
    public class AnalyticsDataEventArgs : EventArgs
    {
        public AnalyticsReport Report { get; set; } = new();
        public AnalyticsData Analytics { get; set; } = new();
    }

    /// <summary>
    /// Data structure for analytics report information
    /// </summary>
    public class AnalyticsReport
    {
        public List<EnterpriseMetric> Enterprises { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string ReportType { get; set; } = string.Empty;
        public string TimePeriod { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data structure for enterprise performance metrics
    /// </summary>
    public class EnterpriseMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal CurrentRate { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public int CitizenCount { get; set; }
        public decimal BudgetVariance { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Data structure for chart visualization data
    /// </summary>
    public class AnalyticsData
    {
        public List<WileyWidget.Services.ChartSeries> ChartData { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public string ChartType { get; set; } = string.Empty;
        public Dictionary<string, decimal> SummaryStats { get; set; } = new();
    }
}
