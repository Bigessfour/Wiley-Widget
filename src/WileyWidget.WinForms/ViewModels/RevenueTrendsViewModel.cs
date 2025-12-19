using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for Revenue Trends panel displaying monthly revenue data over time.
/// Provides observable properties for chart and grid display with async data loading.
/// </summary>
public partial class RevenueTrendsViewModel : ViewModelBase
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

    /// <summary>
    /// Parameterless constructor for design-time/fallback scenarios.
    /// </summary>
    public RevenueTrendsViewModel()
        : this(new FallbackAccountsRepository(), NullLogger<RevenueTrendsViewModel>.Instance)
    {
    }

    /// <summary>
    /// Loads monthly revenue data asynchronously with proper cancellation support.
    /// Thread-safe and UI-friendly (updates collections on UI thread context).
    /// </summary>
    public async Task LoadDataAsync()
    {
        // Cancel any previous load operation
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _loadCancellationTokenSource.Token;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            Logger.LogInformation("Loading revenue trends data from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                StartDate, EndDate);

            // Fetch revenue data grouped by month
            var revenueData = await LoadMonthlyRevenueAsync(StartDate, EndDate, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
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
        // Get all accounts with transactions in the date range
        var accounts = await _accountsRepository.GetAllAccountsAsync(cancellationToken);

        // Group by month and sum revenue
        var monthlyGroups = new Dictionary<DateTime, decimal>();

        // Generate monthly buckets
        var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);
        var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

        while (currentMonth <= endMonth)
        {
            // In a real implementation, you would query transactions for this month
            // For now, generate sample data based on account balances
            // Since we don't have CreatedDate, we'll simulate monthly distribution
            var monthlyRevenue = accounts.Any()
                ? accounts.Sum(a => a.Balance) * 0.1m / accounts.Count // Distribute across accounts
                : 0m;

            monthlyGroups[currentMonth] = monthlyRevenue;
            currentMonth = currentMonth.AddMonths(1);
        }

        // Convert to array
        var result = monthlyGroups
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new RevenueMonthlyData
            {
                Month = kvp.Key,
                Revenue = kvp.Value,
                TransactionCount = 0 // Would be populated from actual transaction data
            })
            .ToArray();

        return result;
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
    public DateTime Month { get; set; }

    /// <summary>
    /// Display-friendly month label (e.g., "Jan 2025").
    /// </summary>
    public string MonthLabel => Month.ToString("MMM yyyy");

    /// <summary>
    /// Total revenue for the month.
    /// </summary>
    public decimal Revenue { get; set; }

    /// <summary>
    /// Number of transactions contributing to revenue.
    /// </summary>
    public int TransactionCount { get; set; }

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
        // Return sample data for design-time preview
        var sampleAccounts = new List<WileyWidget.Models.MunicipalAccount>
        {
            new WileyWidget.Models.MunicipalAccount { Id = 1, AccountNumber = new WileyWidget.Models.AccountNumber { Value = "1000" }, Name = "General Fund", Balance = 50000m },
            new WileyWidget.Models.MunicipalAccount { Id = 2, AccountNumber = new WileyWidget.Models.AccountNumber { Value = "2000" }, Name = "Revenue Account", Balance = 75000m },
            new WileyWidget.Models.MunicipalAccount { Id = 3, AccountNumber = new WileyWidget.Models.AccountNumber { Value = "3000" }, Name = "Expense Account", Balance = 60000m }
        };

        return Task.FromResult<IReadOnlyList<WileyWidget.Models.MunicipalAccount>>(sampleAccounts);
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
}
