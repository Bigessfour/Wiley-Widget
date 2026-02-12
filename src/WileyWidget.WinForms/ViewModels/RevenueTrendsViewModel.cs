using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Business.Models;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for Revenue Trends panel displaying monthly revenue data over time.
/// Provides observable properties for chart and grid display with async data loading.
/// </summary>
public partial class RevenueTrendsViewModel : ViewModelBase, IDisposable
{
    private readonly IAccountsRepository _accountsRepository;
    private CancellationTokenSource? _loadCancellationTokenSource;

    /// <summary>
    /// Collection of monthly revenue data for chart and grid display.
    /// </summary>
    public ObservableCollection<RevenueMonthlyData> MonthlyData { get; } = new();

    /// <summary>
    /// Total revenue across all months in the dataset.
    /// </summary>
    [ObservableProperty]
    private decimal _totalRevenue;

    /// <summary>
    /// Average monthly revenue.
    /// </summary>
    [ObservableProperty]
    private decimal _averageRevenue;

    /// <summary>
    /// Highest monthly revenue in the dataset.
    /// </summary>
    [ObservableProperty]
    private decimal _peakRevenue;

    /// <summary>
    /// Month with lowest revenue.
    /// </summary>
    [ObservableProperty]
    private decimal _lowestRevenue;

    /// <summary>
    /// Growth rate percentage from first to last month.
    /// </summary>
    [ObservableProperty]
    private decimal _growthRate;

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
    /// Start date for revenue data filtering.
    /// </summary>
    [ObservableProperty]
    private DateTime _startDate = DateTime.Now.AddYears(-1);

    /// <summary>
    /// End date for revenue data filtering.
    /// </summary>
    [ObservableProperty]
    private DateTime _endDate = DateTime.Now;

    /// <summary>
    /// Command to refresh revenue data asynchronously.
    /// </summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Initializes a new instance with required dependencies.
    /// </summary>
    /// <param name="accountsRepository">Repository for accounts/revenue data access</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    public RevenueTrendsViewModel(
        IAccountsRepository accountsRepository,
        ILogger<RevenueTrendsViewModel> logger)
        : base(logger)
    {
        _accountsRepository = accountsRepository ?? throw new ArgumentNullException(nameof(accountsRepository));

        RefreshCommand = new AsyncRelayCommand(
            LoadDataAsync,
            () => !IsLoading);

        Logger.LogDebug("RevenueTrendsViewModel initialized");
    }

#if DEBUG
    /// <summary>
    /// Parameterless constructor for design-time/fallback scenarios.
    /// Only included in DEBUG builds to prevent accidental fallback usage in production.
    /// </summary>
    public RevenueTrendsViewModel()
        : this(new FallbackAccountsRepository(), NullLogger<RevenueTrendsViewModel>.Instance)
    {
    }
#endif

    /// <summary>
    /// Loads monthly revenue data asynchronously with proper cancellation support.
    /// Thread-safe and UI-friendly (updates collections on UI thread context).
    /// </summary>
    public async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        // Cancel any previous load operation
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _loadCancellationTokenSource.Token);
        var effectiveToken = linkedCts.Token;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            Logger.LogInformation("Loading revenue trends data from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                StartDate, EndDate);

            // Fetch revenue data grouped by month
            var revenueData = await LoadMonthlyRevenueAsync(StartDate, EndDate, effectiveToken);

            if (effectiveToken.IsCancellationRequested)
            {
                Logger.LogDebug("Revenue trends data load was cancelled");
                return;
            }

            // Clear and repopulate monthly data collection
            MonthlyData.Clear();
            decimal totalRevenue = 0m;
            decimal peakRevenue = 0m;
            decimal lowestRevenue = decimal.MaxValue;
            decimal? firstMonthRevenue = null;
            decimal? lastMonthRevenue = null;

            foreach (var monthData in revenueData)
            {
                MonthlyData.Add(monthData);

                totalRevenue += monthData.Revenue;

                if (monthData.Revenue > peakRevenue)
                    peakRevenue = monthData.Revenue;

                if (monthData.Revenue < lowestRevenue)
                    lowestRevenue = monthData.Revenue;

                firstMonthRevenue ??= monthData.Revenue;
                lastMonthRevenue = monthData.Revenue;
            }

            // Update summary properties
            TotalRevenue = totalRevenue;
            AverageRevenue = MonthlyData.Count > 0 ? totalRevenue / MonthlyData.Count : 0m;
            PeakRevenue = peakRevenue;
            LowestRevenue = lowestRevenue == decimal.MaxValue ? 0m : lowestRevenue;

            // Calculate growth rate
            if (firstMonthRevenue.HasValue && lastMonthRevenue.HasValue && firstMonthRevenue.Value != 0)
            {
                GrowthRate = ((lastMonthRevenue.Value - firstMonthRevenue.Value) / firstMonthRevenue.Value) * 100m;
            }
            else
            {
                GrowthRate = 0m;
            }

            LastUpdated = DateTime.Now;

            Logger.LogInformation(
                "Revenue trends loaded: {MonthCount} months, Total: {TotalRevenue:C}, Growth: {GrowthRate:F2}%",
                MonthlyData.Count, TotalRevenue, GrowthRate);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Revenue trends data load was cancelled");
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load revenue trends data");
            ErrorMessage = $"Failed to load revenue data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads monthly revenue data from the repository, aggregated by month.
    /// </summary>
    private async Task<RevenueMonthlyData[]> LoadMonthlyRevenueAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        // Delegate aggregation to repository; repository must return real production data.
        var aggregates = await _accountsRepository.GetMonthlyRevenueAsync(startDate, endDate, cancellationToken);

        if (aggregates == null || aggregates.Count == 0)
            return Array.Empty<RevenueMonthlyData>();

        var result = aggregates
            .OrderBy(a => a.Month)
            .Select(a => new RevenueMonthlyData
            {
                Month = a.Month,
                Revenue = a.Amount,
                TransactionCount = a.TransactionCount
            })
            .ToArray();

        return result;
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
/// Represents monthly revenue data for chart and grid display.
/// </summary>
public class RevenueMonthlyData
{
    /// <summary>
    /// Month start date (first day of month).
    /// </summary>
    public required DateTime Month { get; init; }

    /// <summary>
    /// Display-friendly month label (e.g., "Jan 2025").
    /// </summary>
    public string MonthLabel => Month.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Total revenue for the month.
    /// </summary>
    public required decimal Revenue { get; init; }

    /// <summary>
    /// Number of transactions contributing to revenue.
    /// </summary>
    public required int TransactionCount { get; init; }

    /// <summary>
    /// Average transaction value for the month.
    /// </summary>
    public decimal AverageTransactionValue =>
        TransactionCount > 0 ? Revenue / TransactionCount : 0m;
}

/// <summary>
/// Fallback repository for design-time/testing scenarios.
/// </summary>
internal class FallbackAccountsRepository : IAccountsRepository
{
    public Task<IReadOnlyList<WileyWidget.Models.MunicipalAccount>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        // Sample/fallback data disabled. Return empty list so production data must be provided by repository.
        _ = cancellationToken; // explicit discard
        return Task.FromResult<IReadOnlyList<WileyWidget.Models.MunicipalAccount>>(Array.Empty<WileyWidget.Models.MunicipalAccount>());
    }

    public Task<IReadOnlyList<WileyWidget.Models.MunicipalAccount>> GetAccountsByFundAsync(WileyWidget.Models.MunicipalFundType fundType, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WileyWidget.Models.MunicipalAccount>>(new List<WileyWidget.Models.MunicipalAccount>());

    public Task<IReadOnlyList<WileyWidget.Models.MunicipalAccount>> GetAccountsByTypeAsync(WileyWidget.Models.AccountType accountType, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WileyWidget.Models.MunicipalAccount>>(new List<WileyWidget.Models.MunicipalAccount>());

    public Task<IReadOnlyList<WileyWidget.Models.MunicipalAccount>> GetAccountsByFundAndTypeAsync(WileyWidget.Models.MunicipalFundType fundType, WileyWidget.Models.AccountType accountType, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WileyWidget.Models.MunicipalAccount>>(new List<WileyWidget.Models.MunicipalAccount>());

    public Task<WileyWidget.Models.MunicipalAccount?> GetAccountByIdAsync(int accountId, CancellationToken cancellationToken = default)
        => Task.FromResult<WileyWidget.Models.MunicipalAccount?>(null);

    public Task<IReadOnlyList<WileyWidget.Models.MunicipalAccount>> SearchAccountsAsync(string searchTerm, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WileyWidget.Models.MunicipalAccount>>(new List<WileyWidget.Models.MunicipalAccount>());

    public Task<IReadOnlyList<MonthlyRevenueAggregate>> GetMonthlyRevenueAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MonthlyRevenueAggregate>>(Array.Empty<MonthlyRevenueAggregate>());
}
