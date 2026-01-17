using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for Department Summary panel displaying key metrics and department drill-down grid.
/// Provides observable properties for summary metrics and async data loading with proper cancellation support.
/// </summary>
public partial class DepartmentSummaryViewModel : ViewModelBase, IDisposable
{
    private readonly IDepartmentRepository _departmentRepository;
    private CancellationTokenSource? _loadCancellationTokenSource;

    /// <summary>
    /// Collection of department metrics for grid display.
    /// </summary>
    public ObservableCollection<DepartmentMetric> Metrics { get; } = new();

    /// <summary>
    /// Total budget across all departments.
    /// </summary>
    [ObservableProperty]
    private decimal _totalBudget;

    /// <summary>
    /// Total actual spending across all departments.
    /// </summary>
    [ObservableProperty]
    private decimal _totalActual;

    /// <summary>
    /// Variance between total budget and total actual (positive = under budget, negative = over budget).
    /// </summary>
    [ObservableProperty]
    private decimal _variance;

    /// <summary>
    /// Percentage variance relative to total budget.
    /// </summary>
    [ObservableProperty]
    private decimal _variancePercent;

    /// <summary>
    /// Number of departments over budget.
    /// </summary>
    [ObservableProperty]
    private int _departmentsOverBudget;

    /// <summary>
    /// Number of departments under budget.
    /// </summary>
    [ObservableProperty]
    private int _departmentsUnderBudget;

    /// <summary>
    /// Indicates whether data is currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Error message to display if data loading fails.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Timestamp of last successful data load.
    /// </summary>
    [ObservableProperty]
    private DateTime _lastUpdated;

    /// <summary>
    /// Command to load department data asynchronously.
    /// </summary>
    public IAsyncRelayCommand LoadDataCommand { get; }

    /// <summary>
    /// Initializes a new instance with required dependencies.
    /// </summary>
    /// <param name="departmentRepository">Repository for department data access</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    public DepartmentSummaryViewModel(
        IDepartmentRepository departmentRepository,
        ILogger<DepartmentSummaryViewModel> logger)
        : base(logger)
    {
        _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));

        LoadDataCommand = new AsyncRelayCommand(
            LoadDataAsync,
            () => !IsLoading);

        Logger.LogDebug("DepartmentSummaryViewModel initialized");
    }

    /// <summary>
    /// Parameterless constructor for design-time/fallback scenarios.
    /// Uses NullLogger and throws on data operations.
    /// </summary>
    public DepartmentSummaryViewModel()
        : this(new FallbackDepartmentRepository(), NullLogger<DepartmentSummaryViewModel>.Instance)
    {
    }

    /// <summary>
    /// Loads department summary data asynchronously with proper cancellation support.
    /// Thread-safe and UI-friendly (updates collections on UI thread context).
    /// </summary>
    public async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {

        // Cancel any previous load operation
        if (_loadCancellationTokenSource != null)
        {
            await _loadCancellationTokenSource.CancelAsync();
            _loadCancellationTokenSource.Dispose();
        }
        _loadCancellationTokenSource = new CancellationTokenSource();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _loadCancellationTokenSource.Token);
        CancellationToken effectiveCancellationToken = linkedCts.Token;


        try
        {
            IsLoading = true;
            ErrorMessage = null;

            Logger.LogInformation("Loading department summary data");

            // Fetch departments from repository
            var departments = await _departmentRepository.GetAllAsync(effectiveCancellationToken);

            if (effectiveCancellationToken.IsCancellationRequested)
            {
                Logger.LogDebug("Department summary data load was cancelled");
                return;
            }

            // Clear and repopulate metrics collection
            Metrics.Clear();
            decimal totalBudget = 0m;
            decimal totalActual = 0m;
            int overBudgetCount = 0;
            int underBudgetCount = 0;

            foreach (var dept in departments)
            {
                // Calculate department-level metrics from budget entries
                var budgeted = dept.BudgetEntries.Sum(b => b.BudgetedAmount);
                var actual = dept.BudgetEntries.Sum(b => b.ActualAmount);
                var variance = budgeted - actual;
                var variancePercent = budgeted != 0 ? (variance / budgeted) * 100m : 0m;
                var isOverBudget = actual > budgeted;

                var metric = new DepartmentMetric
                {
                    DepartmentId = dept.Id,
                    DepartmentName = dept.Name,
                    DepartmentCode = dept.DepartmentCode ?? dept.Name,
                    BudgetedAmount = budgeted,
                    ActualAmount = actual,
                    Variance = variance,
                    VariancePercent = variancePercent,
                    IsOverBudget = isOverBudget
                };

                Metrics.Add(metric);

                // Accumulate totals
                totalBudget += budgeted;
                totalActual += actual;

                if (isOverBudget)
                    overBudgetCount++;
                else
                    underBudgetCount++;
            }

            // Update summary properties
            TotalBudget = totalBudget;
            TotalActual = totalActual;
            Variance = totalBudget - totalActual;
            VariancePercent = totalBudget != 0 ? (Variance / totalBudget) * 100m : 0m;
            DepartmentsOverBudget = overBudgetCount;
            DepartmentsUnderBudget = underBudgetCount;
            LastUpdated = DateTime.Now;

            Logger.LogInformation(
                "Department summary loaded: {DepartmentCount} departments, Total Budget: {TotalBudget:C}, Total Actual: {TotalActual:C}",
                Metrics.Count, TotalBudget, TotalActual);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Department summary data load was cancelled");
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load department summary data");
            ErrorMessage = $"Failed to load department data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Disposes of resources used by the ViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of resources used by the ViewModel.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up managed resources if needed
            _loadCancellationTokenSource?.Dispose();
            _loadCancellationTokenSource = null;
        }
        // Clean up unmanaged resources if any
    }
}

/// <summary>
/// Represents a single department's budget metrics for grid display.
/// </summary>
public class DepartmentMetric
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercent { get; set; }
    public bool IsOverBudget { get; set; }
}

/// <summary>
/// Fallback repository for design-time/testing scenarios.
/// </summary>
internal class FallbackDepartmentRepository : IDepartmentRepository
{
    public Task<IEnumerable<Department>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Return sample data for design-time preview
        var sampleDepartments = new List<Department>
        {
            new Department { Id = 1, Name = "Sales", DepartmentCode = "SALES" },
            new Department { Id = 2, Name = "Marketing", DepartmentCode = "MKTG" },
            new Department { Id = 3, Name = "IT", DepartmentCode = "IT" }
        };

        return Task.FromResult<IEnumerable<Department>>(sampleDepartments);
    }

    public Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult<Department?>(null);

    public Task<Department?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult<Department?>(null);

    public Task AddAsync(Department department, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<IEnumerable<Department>> GetRootDepartmentsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Department>>(new List<Department>());

    public Task<IEnumerable<Department>> GetChildDepartmentsAsync(int parentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Department>>(new List<Department>());

    public Task<(IEnumerable<Department> Items, int TotalCount)> GetPagedAsync(int pageNumber = 1, int pageSize = 50, string? sortBy = null, bool sortDescending = false, CancellationToken cancellationToken = default)
        => Task.FromResult((Items: Enumerable.Empty<Department>(), TotalCount: 0));
}
