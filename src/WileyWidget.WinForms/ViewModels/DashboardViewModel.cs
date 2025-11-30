using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Data;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// Represents a single metric displayed on the dashboard.
    /// </summary>
    public class DashboardMetric
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public double ChangePercent { get; set; }
        public bool IsPositiveChange => ChangePercent >= 0;
    }

    /// <summary>
    /// ViewModel for the Dashboard view providing real-time financial metrics,
    /// budget summaries, and key performance indicators for municipal accounts.
    /// Implements full MVVM pattern with async data loading and error handling.
    /// </summary>
    public partial class DashboardViewModel : ObservableRecipient
    {
        private readonly ILogger<DashboardViewModel>? _logger;
        private readonly IDbContextFactory<AppDbContext>? _dbContextFactory;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        [ObservableProperty]
        private string title = "Financial Dashboard";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<DashboardMetric> metrics = new();

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private decimal totalExpenditure;

        [ObservableProperty]
        private decimal remainingBudget;

        [ObservableProperty]
        private int activeAccountCount;

        [ObservableProperty]
        private int departmentCount;

        [ObservableProperty]
        private DateTime lastRefreshed = DateTime.Now;

        /// <summary>
        /// Initializes a new instance of DashboardViewModel with DI dependencies.
        /// </summary>
        public DashboardViewModel(
            ILogger<DashboardViewModel>? logger = null,
            IDbContextFactory<AppDbContext>? dbContextFactory = null)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;

            LoadDashboardCommand = new AsyncRelayCommand(LoadDashboardAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);

            // Fire initial load
            _ = LoadDashboardAsync();
        }

        /// <summary>Command to load dashboard data from database.</summary>
        public IAsyncRelayCommand LoadDashboardCommand { get; }

        /// <summary>Command to refresh all dashboard metrics.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>
        /// Loads all dashboard metrics from the database asynchronously.
        /// </summary>
        private async Task LoadDashboardAsync(CancellationToken ct = default)
        {
            if (!await _loadLock.WaitAsync(0)) return; // Skip if already loading

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _logger?.LogInformation("Loading dashboard metrics");

                if (_dbContextFactory == null)
                {
                    // Fallback to sample data when no DB context available
                    LoadSampleData();
                    return;
                }

                if (ct.IsCancellationRequested) return;
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

                // Load account summaries
                var accountStats = await db.MunicipalAccounts
                    .GroupBy(a => 1)
                    .Select(g => new
                    {
                        TotalBudget = g.Sum(a => a.BudgetAmount),
                        TotalBalance = g.Sum(a => a.Balance),
                        ActiveCount = g.Count(a => a.IsActive),
                        TotalCount = g.Count()
                    })
                    .FirstOrDefaultAsync(ct);

                if (accountStats != null)
                {
                    TotalBudget = accountStats.TotalBudget;
                    TotalExpenditure = accountStats.TotalBalance;
                    RemainingBudget = accountStats.TotalBudget - accountStats.TotalBalance;
                    ActiveAccountCount = accountStats.ActiveCount;
                }

                // Load department count
                DepartmentCount = await db.Departments.CountAsync(ct);

                // Build metrics collection
                var newMetrics = new ObservableCollection<DashboardMetric>
                {
                    new() { Name = "Total Budget", Value = (double)TotalBudget, Category = "Financial", Icon = "💰" },
                    new() { Name = "Expenditures", Value = (double)TotalExpenditure, Category = "Financial", Icon = "📊" },
                    new() { Name = "Remaining", Value = (double)RemainingBudget, Category = "Financial", Icon = "✅" },
                    new() { Name = "Active Accounts", Value = ActiveAccountCount, Category = "Accounts", Icon = "📋" },
                    new() { Name = "Departments", Value = DepartmentCount, Category = "Organization", Icon = "🏢" }
                };

                Metrics = newMetrics;
                LastRefreshed = DateTime.Now;
                _logger?.LogInformation("Dashboard loaded: {Count} metrics", Metrics.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load dashboard metrics");
                ErrorMessage = $"Unable to load dashboard: {ex.Message}";
                LoadSampleData();
            }
            finally
            {
                IsLoading = false;
                _loadLock.Release();
            }
        }

        /// <summary>
        /// Refreshes dashboard data, forcing a full reload.
        /// </summary>
        private async Task RefreshAsync(CancellationToken ct = default)
        {
            _logger?.LogInformation("Refreshing dashboard");
            await LoadDashboardAsync(ct);
        }

        /// <summary>
        /// Loads sample/fallback data when database is unavailable.
        /// </summary>
        private void LoadSampleData()
        {
            TotalBudget = 1_500_000m;
            TotalExpenditure = 1_125_000m;
            RemainingBudget = 375_000m;
            ActiveAccountCount = 42;
            DepartmentCount = 8;

            Metrics = new ObservableCollection<DashboardMetric>
            {
                new() { Name = "Total Budget", Value = 1_500_000, Category = "Financial", Icon = "💰", ChangePercent = 5.2 },
                new() { Name = "Expenditures", Value = 1_125_000, Category = "Financial", Icon = "📊", ChangePercent = -2.1 },
                new() { Name = "Remaining", Value = 375_000, Category = "Financial", Icon = "✅", ChangePercent = 12.5 },
                new() { Name = "Active Accounts", Value = 42, Category = "Accounts", Icon = "📋" },
                new() { Name = "Departments", Value = 8, Category = "Organization", Icon = "🏢" }
            };

            LastRefreshed = DateTime.Now;
        }
    }
}
