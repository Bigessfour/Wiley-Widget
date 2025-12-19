using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Recommended Monthly Charge dashboard.
/// Analyzes department expenses, current charges, and provides AI-driven recommendations.
/// </summary>
public partial class RecommendedMonthlyChargeViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<RecommendedMonthlyChargeViewModel> _logger;
    private bool _disposed;

    #region Observable Properties

    /// <summary>
    /// Collection of department rate analyses
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DepartmentRateModel> departments = new();

    /// <summary>
    /// Collection of state/national benchmark data
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<StateBenchmarkModel> benchmarks = new();

    /// <summary>
    /// Indicates if data is currently being loaded
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Last time data was refreshed
    /// </summary>
    [ObservableProperty]
    private DateTime lastUpdated = DateTime.Now;

    /// <summary>
    /// Total suggested monthly revenue across all departments
    /// </summary>
    [ObservableProperty]
    private decimal totalSuggestedRevenue;

    /// <summary>
    /// Total current monthly revenue across all departments
    /// </summary>
    [ObservableProperty]
    private decimal totalCurrentRevenue;

    /// <summary>
    /// Total monthly expenses across all departments
    /// </summary>
    [ObservableProperty]
    private decimal totalMonthlyExpenses;

    /// <summary>
    /// Overall profitability status
    /// </summary>
    [ObservableProperty]
    private string overallStatus = "Unknown";

    /// <summary>
    /// Overall profitability color for UI
    /// </summary>
    [ObservableProperty]
    private string overallStatusColor = "Gray";

    /// <summary>
    /// Error message if data load fails
    /// </summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// Status text for display
    /// </summary>
    [ObservableProperty]
    private string statusText = "Ready";

    #endregion

    #region Commands

    public IAsyncRelayCommand RefreshDataCommand { get; }
    public IAsyncRelayCommand SaveCurrentChargesCommand { get; }
    public IAsyncRelayCommand QueryGrokCommand { get; }
    public IAsyncRelayCommand<string> ApplyRecommendationCommand { get; }

    #endregion

    public RecommendedMonthlyChargeViewModel(
        ILogger<RecommendedMonthlyChargeViewModel>? logger)
        : base(logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RefreshDataCommand = new AsyncRelayCommand(RefreshDataAsync);
        SaveCurrentChargesCommand = new AsyncRelayCommand(SaveCurrentChargesAsync);
        QueryGrokCommand = new AsyncRelayCommand(QueryGrokForRecommendationsAsync);
        ApplyRecommendationCommand = new AsyncRelayCommand<string>(ApplyRecommendationAsync);

        // Initialize with sample data for design-time
        if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
        {
            InitializeSampleData();
        }

        _logger.LogDebug("RecommendedMonthlyChargeViewModel initialized");
    }

    /// <summary>
    /// Parameterless constructor for design-time support
    /// </summary>
    public RecommendedMonthlyChargeViewModel()
        : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<RecommendedMonthlyChargeViewModel>.Instance)
    {
    }

    /// <summary>
    /// Refreshes all data from QuickBooks and recalculates recommendations
    /// </summary>
    private async Task RefreshDataAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusText = "Loading department data...";

            _logger.LogInformation("Refreshing recommended monthly charge data");

            // TODO: Load actual data from QuickBooks service
            // For now, load sample data
            await Task.Delay(500); // Simulate API call

            LoadDepartmentData();
            LoadBenchmarkData();
            CalculateTotals();

            LastUpdated = DateTime.Now;
            StatusText = $"Loaded {Departments.Count} departments";

            _logger.LogInformation("Data refresh completed: {DeptCount} departments loaded", Departments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing recommended monthly charge data");
            ErrorMessage = $"Failed to refresh data: {ex.Message}";
            StatusText = "Error loading data";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves user-edited current charges to database
    /// </summary>
    private async Task SaveCurrentChargesAsync()
    {
        try
        {
            IsLoading = true;
            StatusText = "Saving current charges...";

            _logger.LogInformation("Saving current charges for {DeptCount} departments", Departments.Count);

            // TODO: Persist to database using Entity Framework
            await Task.Delay(300); // Simulate save operation

            StatusText = "Charges saved successfully";
            _logger.LogInformation("Current charges saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving current charges");
            ErrorMessage = $"Failed to save charges: {ex.Message}";
            StatusText = "Error saving data";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Queries xAI Grok API for AI-driven recommendations
    /// </summary>
    private async Task QueryGrokForRecommendationsAsync()
    {
        try
        {
            IsLoading = true;
            StatusText = "Querying AI for recommendations...";

            _logger.LogInformation("Querying Grok API for rate recommendations");

            // TODO: Call xAI Grok API with department expense data
            // Example prompt: "Based on monthly expenses [Water: $45000, Sewer: $68000, ...], 
            // recommend adjustment factors for full cost recovery + 15% profit margin"

            await Task.Delay(1000); // Simulate API call

            // Apply sample AI recommendations
            foreach (var dept in Departments)
            {
                dept.UpdateSuggested(1.15m); // 15% profit margin
            }

            CalculateTotals();

            StatusText = "AI recommendations applied";
            _logger.LogInformation("Grok recommendations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Grok API");
            ErrorMessage = $"Failed to get AI recommendations: {ex.Message}";
            StatusText = "Error querying AI";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Applies suggested charge for a specific department
    /// </summary>
    private async Task ApplyRecommendationAsync(string? departmentName)
    {
        if (string.IsNullOrEmpty(departmentName))
            return;

        try
        {
            var dept = Departments.FirstOrDefault(d => d.Department == departmentName);
            if (dept == null)
                return;

            dept.CurrentCharge = dept.SuggestedCharge;
            CalculateTotals();

            StatusText = $"Applied recommendation for {departmentName}";
            _logger.LogInformation("Applied recommendation for {Department}: {Charge:C}", departmentName, dept.SuggestedCharge);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying recommendation for {Department}", departmentName);
            ErrorMessage = $"Failed to apply recommendation: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads department expense data (from QuickBooks in production)
    /// </summary>
    private void LoadDepartmentData()
    {
        Departments.Clear();

        // Sample data - replace with QuickBooks integration
        var departments = new[]
        {
            new DepartmentRateModel
            {
                Department = "Water",
                MonthlyExpenses = 45000m,
                CurrentCharge = 50.00m,
                CustomerCount = 850,
                StateAverage = 55.00m,
                AiAdjustmentFactor = 1.1m
            },
            new DepartmentRateModel
            {
                Department = "Sewer",
                MonthlyExpenses = 68000m,
                CurrentCharge = 70.00m,
                CustomerCount = 850,
                StateAverage = 75.00m,
                AiAdjustmentFactor = 1.1m
            },
            new DepartmentRateModel
            {
                Department = "Trash",
                MonthlyExpenses = 28000m,
                CurrentCharge = 30.00m,
                CustomerCount = 850,
                StateAverage = 32.00m,
                AiAdjustmentFactor = 1.05m
            },
            new DepartmentRateModel
            {
                Department = "Apartments",
                MonthlyExpenses = 95000m,
                CurrentCharge = 120.00m,
                CustomerCount = 280,
                StateAverage = 135.00m,
                AiAdjustmentFactor = 1.12m
            }
        };

        foreach (var dept in departments)
        {
            dept.UpdateSuggested(dept.AiAdjustmentFactor);
            Departments.Add(dept);
        }
    }

    /// <summary>
    /// Loads state/national benchmark data
    /// </summary>
    private void LoadBenchmarkData()
    {
        Benchmarks.Clear();

        // Sample benchmark data - replace with configurable source
        var benchmarks = new[]
        {
            new StateBenchmarkModel
            {
                Department = "Water",
                StateAverage = 55.00m,
                TownSizeAverage = 52.00m,
                NationalAverage = 50.00m,
                Source = "AWWA / EPA WaterSense 2024",
                Year = 2024,
                PopulationRange = "5,000-10,000"
            },
            new StateBenchmarkModel
            {
                Department = "Sewer",
                StateAverage = 75.00m,
                TownSizeAverage = 72.00m,
                NationalAverage = 70.00m,
                Source = "Bluefield Research / Move.org",
                Year = 2024,
                PopulationRange = "5,000-10,000"
            },
            new StateBenchmarkModel
            {
                Department = "Trash",
                StateAverage = 32.00m,
                TownSizeAverage = 30.00m,
                NationalAverage = 30.00m,
                Source = "Move.org / Local surveys",
                Year = 2024,
                PopulationRange = "5,000-10,000"
            },
            new StateBenchmarkModel
            {
                Department = "Apartments",
                StateAverage = 135.00m,
                TownSizeAverage = 128.00m,
                NationalAverage = 120.00m,
                Source = "Multifamily Housing Council",
                Year = 2024,
                PopulationRange = "5,000-10,000"
            }
        };

        foreach (var benchmark in benchmarks)
        {
            Benchmarks.Add(benchmark);
        }
    }

    /// <summary>
    /// Calculates total revenue, expenses, and overall status
    /// </summary>
    private void CalculateTotals()
    {
        TotalMonthlyExpenses = Departments.Sum(d => d.MonthlyExpenses);
        TotalCurrentRevenue = Departments.Sum(d => d.CurrentCharge * d.CustomerCount);
        TotalSuggestedRevenue = Departments.Sum(d => d.SuggestedCharge * d.CustomerCount);

        var totalGainLoss = TotalCurrentRevenue - TotalMonthlyExpenses;

        const decimal breakEvenThreshold = 1000m;
        if (totalGainLoss < -breakEvenThreshold)
        {
            OverallStatus = "Losing Money";
            OverallStatusColor = "Red";
        }
        else if (Math.Abs(totalGainLoss) <= breakEvenThreshold)
        {
            OverallStatus = "Breaking Even";
            OverallStatusColor = "Orange";
        }
        else
        {
            OverallStatus = "Profitable";
            OverallStatusColor = "Green";
        }

        _logger.LogDebug("Totals calculated: Revenue={Revenue:C}, Expenses={Expenses:C}, Status={Status}",
            TotalCurrentRevenue, TotalMonthlyExpenses, OverallStatus);
    }

    /// <summary>
    /// Initializes sample data for design-time preview
    /// </summary>
    private void InitializeSampleData()
    {
        LoadDepartmentData();
        LoadBenchmarkData();
        CalculateTotals();

        LastUpdated = DateTime.Now;
        StatusText = "Design Mode - Sample Data";
    }

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
                // Dispose managed resources
            }
            _disposed = true;
        }
    }
}
