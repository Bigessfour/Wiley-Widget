using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using WileyWidget.Data;
using WileyWidget.WinForms.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for the Recommended Monthly Charge dashboard.
/// Analyzes department expenses, current charges, and provides AI-driven recommendations.
/// </summary>
public partial class RecommendedMonthlyChargeViewModel : ViewModelBase, IDisposable
{

    private readonly IDepartmentExpenseService? _departmentExpenseService;
    private readonly IGrokRecommendationService? _grokRecommendationService;
    protected IDbContextFactory<AppDbContext>? _dbContextFactory;
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
    /// AI-generated explanation for the recommendations
    /// </summary>
    [ObservableProperty]
    private string? recommendationExplanation;

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
        ILogger<RecommendedMonthlyChargeViewModel>? logger,
        IDepartmentExpenseService? departmentExpenseService = null,
        IGrokRecommendationService? grokRecommendationService = null)
        : base(logger)
    {
        _departmentExpenseService = departmentExpenseService;
        _grokRecommendationService = grokRecommendationService;

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
        : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<RecommendedMonthlyChargeViewModel>.Instance, null, null)
    {
    }

    /// <summary>
    /// Refreshes all data from QuickBooks and recalculates recommendations
    /// </summary>
    private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusText = "Loading department data...";

            _logger.LogInformation("Refreshing recommended monthly charge data");

            // Clear existing data
            Departments.Clear();

            // Load real expense data if service is available
            if (_departmentExpenseService != null)
            {
                await LoadRealDepartmentDataAsync();
            }
            else
            {
                _logger.LogWarning("IDepartmentExpenseService not available - loading sample data");
                LoadSampleDepartmentData();
            }

            LoadBenchmarkData();
            CalculateTotals();

            // Optionally query Grok AI automatically after refresh
            // Uncomment to enable automatic AI recommendations on refresh:
            // if (_grokRecommendationService != null && Departments.Any())
            // {
            //     await QueryGrokForRecommendationsAsync();
            // }

            LastUpdated = DateTime.Now;
            StatusText = $"Loaded {Departments.Count} departments";

            _logger.LogInformation("Data refresh completed: {DeptCount} departments loaded", Departments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing recommended monthly charge data");
            ErrorMessage = $"Failed to refresh data: {ex.Message}";
            StatusText = "Error loading data";

            // Fallback to sample data on error
            _logger.LogWarning("Falling back to sample data due to error");
            LoadSampleDepartmentData();
            LoadBenchmarkData();
            CalculateTotals();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves user-edited current charges to database
    /// </summary>
    private async Task SaveCurrentChargesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Saving current charges...";

            _logger.LogInformation("Saving current charges for {DeptCount} departments", Departments.Count);

            if (_dbContextFactory == null)
            {
                _logger.LogWarning("DbContextFactory not available - cannot save to database");
                StatusText = "Database not available - charges not saved";
                return;
            }

            await using var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

            foreach (var dept in Departments)
            {
                var existing = await context.DepartmentCurrentCharges
                    .FirstOrDefaultAsync(d => d.Department == dept.Department)
                    .ConfigureAwait(false);

                // Get actual customer count for this department
                var customerCount = await context.Charges
                    .Where(c => c.ChargeType == dept.Department)
                    .Select(c => c.BillId)
                    .Distinct()
                    .Join(context.UtilityBills,
                          chargeBillId => chargeBillId,
                          bill => bill.Id,
                          (chargeBillId, bill) => bill.CustomerId)
                    .Distinct()
                    .CountAsync()
                    .ConfigureAwait(false);

                if (existing == null)
                {
                    existing = new DepartmentCurrentCharge
                    {
                        Department = dept.Department,
                        CurrentCharge = dept.CurrentCharge,
                        CustomerCount = customerCount,
                        LastUpdated = DateTime.UtcNow
                    };
                    context.DepartmentCurrentCharges.Add(existing);
                }
                else
                {
                    existing.CurrentCharge = dept.CurrentCharge;
                    existing.CustomerCount = customerCount;
                    existing.LastUpdated = DateTime.UtcNow;
                }
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

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
    private async Task QueryGrokForRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            StatusText = "Querying AI for recommendations...";

            _logger.LogInformation("Querying Grok API for rate recommendations");

            if (_grokRecommendationService == null)
            {
                _logger.LogWarning("IGrokRecommendationService not available - applying default recommendations");
                foreach (var dept in Departments)
                {
                    dept.UpdateSuggested(1.15m);
                }

                RecommendationExplanation = "AI service not available; applied default 15% margin recommendations.";
                CalculateTotals();
                StatusText = "Default recommendations applied (AI unavailable)";
                return;
            }

            if (!Departments.Any())
            {
                _logger.LogWarning("No departments loaded - cannot query Grok");
                RecommendationExplanation = "No department data available to analyze.";
                StatusText = "No data to analyze";
                return;
            }

            // Build prompt with department summary
            var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine("Analyze the following municipal utility department data and recommend optimal monthly charge adjustments:");
            promptBuilder.AppendLine();

            foreach (var dept in Departments)
            {
                promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- {dept.Department}:");
                promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Current Charge: ${dept.CurrentCharge:F2}");
                promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Monthly Expenses: ${dept.MonthlyExpenses:F2}");
                promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Customers: {dept.CustomerCount}");
                promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  State Average: ${dept.StateAverage:F2}");
                promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Current Gain/Loss: ${dept.MonthlyGainLoss:F2}");
                promptBuilder.AppendLine();
            }

            promptBuilder.AppendLine("Consider:");
            promptBuilder.AppendLine("- Maintaining 15% profit margin for operational stability");
            promptBuilder.AppendLine("- Competitive positioning against state averages");
            promptBuilder.AppendLine("- Customer affordability and rate shock avoidance");
            promptBuilder.AppendLine("- Long-term financial sustainability");

            var prompt = promptBuilder.ToString();
            _logger.LogDebug("Grok prompt: {Prompt}", prompt);

            // Build expense dictionary
            var deptExpenses = Departments.ToDictionary(d => d.Department, d => d.MonthlyExpenses);

            // Query Grok for recommendations
            var result = await _grokRecommendationService.GetRecommendedAdjustmentFactorsAsync(deptExpenses, 15.0m);

            // Apply adjustment factors
            foreach (var dept in Departments)
            {
                if (result.AdjustmentFactors.TryGetValue(dept.Department, out var factor))
                {
                    dept.UpdateSuggested(factor);
                    _logger.LogDebug("Applied factor {Factor} to {Department}", factor, dept.Department);
                }
                else
                {
                    _logger.LogDebug("No adjustment factor returned for {Department}; keeping existing suggestion", dept.Department);
                }
            }

            // Set explanation from the result
            RecommendationExplanation = result.Explanation;

            // Log warnings if any
            if (result.Warnings.Any())
            {
                foreach (var warning in result.Warnings)
                {
                    _logger.LogWarning("Grok recommendation warning: {Warning}", warning);
                }
            }

            CalculateTotals();

            StatusText = $"AI recommendations applied ({result.ApiModelUsed})";
            _logger.LogInformation("Grok recommendations applied successfully using {Model}", result.ApiModelUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Grok API");
            ErrorMessage = $"Failed to get AI recommendations: {ex.Message}";
            StatusText = "Error querying AI";
            RecommendationExplanation = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Applies suggested charge for a specific department
    /// </summary>
    private async Task ApplyRecommendationAsync(string? departmentName, CancellationToken cancellationToken = default)
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
    /// Loads department expense data from QuickBooks using real service
    /// </summary>
    private async Task LoadRealDepartmentDataAsync(CancellationToken cancellationToken = default)
    {
        if (_departmentExpenseService == null)
        {
            _logger.LogWarning("IDepartmentExpenseService is null");
            return;
        }

        try
        {
            _logger.LogInformation("Loading real department expense data from QuickBooks");

            // Get last 12 months of data
            var endDate = DateTime.Now;
            var startDate = endDate.AddMonths(-12);

            // Get all department expenses
            var expenseData = await _departmentExpenseService.GetAllDepartmentExpensesAsync(startDate, endDate);

            // Calculate monthly average (divide by 12)
            var monthlyExpenses = expenseData.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value / 12m
            );

            _logger.LogInformation("Loaded expense data for {Count} departments", monthlyExpenses.Count);

            // Define realistic configuration for each department
            var departmentConfigs = new Dictionary<string, (decimal CurrentCharge, int CustomerCount, decimal StateAverage)>
            {
                ["Water"] = (55.00m, 3200, 55.00m),
                ["Sewer"] = (75.00m, 3200, 75.00m),
                ["Trash"] = (30.00m, 2800, 32.00m),
                ["Apartments"] = (120.00m, 280, 135.00m),
                ["Electric"] = (85.00m, 2500, 90.00m),
                ["Gas"] = (45.00m, 2200, 48.00m)
            };

            // Create department models with real expense data
            foreach (var kvp in monthlyExpenses)
            {
                var deptName = kvp.Key;
                var expenses = kvp.Value;

                if (!departmentConfigs.TryGetValue(deptName, out var config))
                {
                    _logger.LogWarning("Unknown department {Department} in expense data - using defaults", deptName);
                    config = (50.00m, 1000, 50.00m);
                }

                var dept = new DepartmentRateModel
                {
                    Department = deptName,
                    MonthlyExpenses = expenses,
                    CurrentCharge = config.CurrentCharge,
                    CustomerCount = config.CustomerCount,
                    StateAverage = config.StateAverage,
                    AiAdjustmentFactor = 1.15m // Default 15% margin
                };

                // Calculate suggested charge: (expenses / customers) * margin, rounded to nearest $0.05
                var calculatedCharge = (expenses / config.CustomerCount) * 1.15m;
                var roundedCharge = Math.Round(calculatedCharge * 20m) / 20m; // Round to nearest $0.05
                dept.SuggestedCharge = roundedCharge;

                Departments.Add(dept);
                _logger.LogDebug("Loaded {Department}: Expenses=${Expenses:F2}, Customers={Customers}, Suggested=${Suggested:F2}",
                    deptName, expenses, config.CustomerCount, roundedCharge);
            }

            _logger.LogInformation("Successfully loaded {Count} departments with real expense data", Departments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading real department data - falling back to sample data");
            LoadSampleDepartmentData();
        }
    }

    /// <summary>
    /// Sample population is disabled in production. This method clears any temporary
    /// department collections and logs a warning instead of populating hard-coded values.
    /// </summary>
    private void LoadSampleDepartmentData()
    {
        _logger.LogWarning("LoadSampleDepartmentData called: sample data disabled. Ensure department service is configured for real data.");
        Departments.Clear();
        Benchmarks.Clear();

        ErrorMessage = "Production data unavailable; sample population disabled.";
        StatusText = "No production department data loaded";
        LastUpdated = DateTime.Now;
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
    /// Design-time initializer is disabled. Clears collections and signals no production data.
    /// </summary>
    private void InitializeSampleData()
    {
        _logger.LogWarning("InitializeSampleData called: sample data disabled in this build.");
        Departments.Clear();
        Benchmarks.Clear();
        CalculateTotals();
        ErrorMessage = "Design/sample data disabled; no production data loaded.";
        StatusText = "No production data loaded";
        LastUpdated = DateTime.Now;
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
