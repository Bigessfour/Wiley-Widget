using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Analytics Hub panel, orchestrating multiple analytics tabs.
/// </summary>
public partial class AnalyticsHubViewModel : ObservableObject, IAnalyticsHubViewModel
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsHubViewModel> _logger;
    private readonly IScenarioSnapshotRepository? _scenarioSnapshotRepository;

    // Global/shared properties
    [ObservableProperty]
    private int selectedFiscalYear;

    [ObservableProperty]
    private ObservableCollection<int> fiscalYears = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    // Tab-specific ViewModels
    public OverviewTabViewModel Overview { get; }
    public TrendsTabViewModel Trends { get; }
    public ScenariosTabViewModel Scenarios { get; }
    public VariancesTabViewModel Variances { get; }

    // Commands
    public IAsyncRelayCommand RefreshAllCommand { get; }

    public AnalyticsHubViewModel(
        IAnalyticsService analyticsService,
        ILogger<AnalyticsHubViewModel> logger,
        IScenarioSnapshotRepository? scenarioSnapshotRepository = null)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scenarioSnapshotRepository = scenarioSnapshotRepository;

        // Initialize sub-ViewModels
        Overview = new OverviewTabViewModel(analyticsService, logger);
        Trends = new TrendsTabViewModel(analyticsService, logger);
        Scenarios = new ScenariosTabViewModel(analyticsService, logger, _scenarioSnapshotRepository);
        Variances = new VariancesTabViewModel(analyticsService, logger);

        // Initialize commands
        RefreshAllCommand = new AsyncRelayCommand(LoadAllAsync);

        // Load initial data
        _ = LoadInitialDataAsync();
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            // Load fiscal years (hardcoded for now)
            FiscalYears.Clear();
            var currentYear = DateTime.Now.Year;
            for (int i = currentYear - 2; i <= currentYear + 1; i++)
            {
                FiscalYears.Add(i);
            }

            // Set default fiscal year to current
            SelectedFiscalYear = DateTime.Now.Year;

            _logger.LogDebug("AnalyticsHubViewModel initial data loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial data for AnalyticsHubViewModel");
        }
    }

    private async Task LoadAllAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing all analytics tabs");

            // Load all tabs in parallel
            await Task.WhenAll(
                Overview.LoadAsync(),
                Trends.LoadAsync(),
                Scenarios.LoadAsync(),
                Variances.LoadAsync()
            );

            _logger.LogInformation("All analytics tabs refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh all analytics tabs");
            throw;
        }
    }
}

/// <summary>
/// Base class for analytics tab ViewModels.
/// </summary>
public abstract partial class AnalyticsTabViewModelBase : ObservableObject
{
    protected readonly IAnalyticsService AnalyticsService;
    protected readonly ILogger Logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    protected AnalyticsTabViewModelBase(IAnalyticsService analyticsService, ILogger logger)
    {
        AnalyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public virtual async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Failed to load data for {TabName}", GetType().Name);
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected abstract Task LoadDataAsync();
}

/// <summary>
/// ViewModel for the Overview tab.
/// </summary>
public partial class OverviewTabViewModel : AnalyticsTabViewModelBase
{
    [ObservableProperty]
    private decimal totalBudget;

    [ObservableProperty]
    private decimal totalActual;

    [ObservableProperty]
    private decimal totalVariance;

    [ObservableProperty]
    private int overBudgetCount;

    [ObservableProperty]
    private int underBudgetCount;

    [ObservableProperty]
    private ObservableCollection<WileyWidget.Services.Abstractions.BudgetMetric> metrics = new();

    [ObservableProperty]
    private ObservableCollection<WileyWidget.Services.Abstractions.SummaryKpi> kpis = new();

    public OverviewTabViewModel(IAnalyticsService analyticsService, ILogger logger)
        : base(analyticsService, logger)
    {
    }

    protected override async Task LoadDataAsync()
    {
        // Load overview data - aggregate totals and top variances
        var overviewData = await AnalyticsService.GetBudgetOverviewAsync();
        TotalBudget = overviewData.TotalBudget;
        TotalActual = overviewData.TotalActual;
        TotalVariance = overviewData.TotalVariance;
        OverBudgetCount = overviewData.OverBudgetCount;
        UnderBudgetCount = overviewData.UnderBudgetCount;

        // Load metrics
        Metrics.Clear();
        var metrics = await AnalyticsService.GetBudgetMetricsAsync();
        foreach (var metric in metrics)
        {
            Metrics.Add(metric);
        }

        // Load KPIs
        Kpis.Clear();
        var kpis = await AnalyticsService.GetSummaryKpisAsync();
        foreach (var kpi in kpis)
        {
            Kpis.Add(kpi);
        }
    }
}

/// <summary>
/// ViewModel for the Trends & Forecasts tab.
/// </summary>
public partial class TrendsTabViewModel : AnalyticsTabViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<TrendSeries> trendData = new();

    [ObservableProperty]
    private ObservableCollection<ForecastPoint> forecastData = new();

    [ObservableProperty]
    private ObservableCollection<DepartmentVariance> departmentVariances = new();

    [ObservableProperty]
    private int projectionYears = 3;

    public TrendsTabViewModel(IAnalyticsService analyticsService, ILogger logger)
        : base(analyticsService, logger)
    {
    }

    protected override async Task LoadDataAsync()
    {
        // Load historical trends and forecast data
        var trends = await AnalyticsService.GetTrendDataAsync(ProjectionYears);
        TrendData.Clear();
        foreach (var series in trends)
        {
            var points = new ObservableCollection<TrendPoint>(series.Points);
            TrendData.Add(new TrendSeries(series.Name, points.ToList()));
        }

        // Load forecast data
        var forecastResult = await AnalyticsService.GenerateReserveForecastAsync(ProjectionYears);
        ForecastData.Clear();
        foreach (var point in forecastResult.ForecastPoints)
        {
            ForecastData.Add(point);
        }

        // Load department variance data (derived from variance details)
        var varianceDetails = await AnalyticsService.GetVarianceDetailsAsync();
        DepartmentVariances.Clear();
        var deptGroups = varianceDetails
            .GroupBy(x => x.Department)
            .Select(g => new DepartmentVariance(
                Department: g.Key,
                AverageVariancePercent: g.Average(x => x.VariancePercent),
                TotalBudgeted: g.Sum(x => x.Budget),
                TotalActual: g.Sum(x => x.Actual),
                Count: g.Count()))
            .OrderByDescending(x => Math.Abs(x.AverageVariancePercent))
            .Take(10);

        foreach (var dept in deptGroups)
        {
            DepartmentVariances.Add(dept);
        }
    }
}

/// <summary>
/// ViewModel for the Scenarios tab.
/// </summary>
public partial class ScenariosTabViewModel : AnalyticsTabViewModelBase
{
    private readonly IScenarioSnapshotRepository? _scenarioSnapshotRepository;

    [ObservableProperty]
    private decimal rateIncreasePercent;

    [ObservableProperty]
    private decimal expenseIncreasePercent;

    [ObservableProperty]
    private decimal revenueTarget;

    [ObservableProperty]
    private ObservableCollection<ScenarioResult> scenarioResults = new();

    public IAsyncRelayCommand RunScenarioCommand { get; }

    public ScenariosTabViewModel(
        IAnalyticsService analyticsService,
        ILogger logger,
        IScenarioSnapshotRepository? scenarioSnapshotRepository = null)
        : base(analyticsService, logger)
    {
        _scenarioSnapshotRepository = scenarioSnapshotRepository;
        RunScenarioCommand = new AsyncRelayCommand(RunScenarioAsync);
    }

    public async Task<bool> SaveCurrentScenarioAsync(string scenarioName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            ErrorMessage = "Scenario name is required.";
            return false;
        }

        if (_scenarioSnapshotRepository == null)
        {
            ErrorMessage = "Scenario persistence service is unavailable.";
            Logger.LogWarning("Save scenario requested but IScenarioSnapshotRepository is not available");
            return false;
        }

        try
        {
            var latestResult = ScenarioResults.FirstOrDefault();
            var snapshot = new SavedScenarioSnapshot
            {
                Name = scenarioName.Trim(),
                Description = latestResult?.Description,
                RateIncreasePercent = RateIncreasePercent,
                ExpenseIncreasePercent = ExpenseIncreasePercent,
                RevenueTarget = RevenueTarget,
                ProjectedValue = latestResult?.ProjectedValue ?? 0m,
                Variance = latestResult?.Variance ?? 0m,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _scenarioSnapshotRepository.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            ErrorMessage = null;
            Logger.LogInformation("Saved scenario snapshot '{ScenarioName}'", snapshot.Name);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Failed to save scenario snapshot '{ScenarioName}'", scenarioName);
            return false;
        }
    }

    protected override async Task LoadDataAsync()
    {
        // Load default scenario parameters
        RateIncreasePercent = 5.0m; // 5% rate increase
        ExpenseIncreasePercent = 3.0m; // 3% expense increase
        RevenueTarget = 1000000m; // $1M revenue target
    }

    private async Task RunScenarioAsync()
    {
        try
        {
            IsLoading = true;
            var results = await AnalyticsService.RunScenarioAsync(
                RateIncreasePercent,
                ExpenseIncreasePercent,
                RevenueTarget);

            ScenarioResults.Clear();
            ScenarioResults.Add(results);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Failed to run scenario");
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>
/// ViewModel for the Variances tab.
/// </summary>
public partial class VariancesTabViewModel : AnalyticsTabViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<WileyWidget.Services.Abstractions.VarianceRecord> variances = new();

    public VariancesTabViewModel(IAnalyticsService analyticsService, ILogger logger)
        : base(analyticsService, logger)
    {
    }

    protected override async Task LoadDataAsync()
    {
        // Load detailed variance records
        var varianceData = await AnalyticsService.GetVarianceDetailsAsync();
        Variances.Clear();
        foreach (var variance in varianceData)
        {
            Variances.Add(variance);
        }
    }
}

// Data models (these would typically be in a separate Models folder)

public record DepartmentVariance(string Department, decimal AverageVariancePercent, decimal TotalBudgeted, decimal TotalActual, int Count);
