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
    /// Message sent when navigation or global state changes.
    /// </summary>
    public record MyEvent(object? Payload);

    /// <summary>
    /// Represents a quick metric displayed in the main overview panel.
    /// </summary>
    public class QuickMetricItem
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string FormattedValue { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Main coordinator ViewModel for navigation, global state, and quick metrics.
    /// Provides the data context for MainForm including title, recent activity,
    /// and high-level financial summaries. Implements full MVVM with async operations.
    /// </summary>
    public partial class MainViewModel : ObservableRecipient
    {
        private readonly ILogger<MainViewModel>? _logger;
        private readonly IDbContextFactory<AppDbContext>? _dbContextFactory;
        private readonly IMessenger _messenger;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        [ObservableProperty]
        private string title = "Wiley Widget — Municipal Finance Manager";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private string? statusMessage = "Ready";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private ObservableCollection<KeyValuePair<string, double>> recentMetrics = new();

        [ObservableProperty]
        private ObservableCollection<QuickMetricItem> quickMetrics = new();

        [ObservableProperty]
        private string currentView = "Dashboard";

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private int totalAccountCount;

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private DateTime lastDataRefresh = DateTime.Now;

        /// <summary>
        /// Initializes a new MainViewModel with optional DI dependencies.
        /// </summary>
        public MainViewModel(
            IMessenger? messenger = null,
            ILogger<MainViewModel>? logger = null,
            IDbContextFactory<AppDbContext>? dbContextFactory = null)
        {
            _messenger = messenger ?? WeakReferenceMessenger.Default;
            _logger = logger;
            _dbContextFactory = dbContextFactory;

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            NavigateCommand = new RelayCommand<string>(NavigateTo);

            // Register for MyEvent messages
            _messenger.Register<MainViewModel, MyEvent>(this, (r, m) => r.HandleEvent(m.Payload));

            // Fire initial load
            _ = LoadDataAsync();
        }

        /// <summary>Command to load all overview data.</summary>
        public IAsyncRelayCommand LoadDataCommand { get; }

        /// <summary>Command to refresh data.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>Command to navigate to a specific view.</summary>
        public IRelayCommand<string> NavigateCommand { get; }

        /// <summary>
        /// Loads summary-level data including quick metrics and connection status.
        /// </summary>
        private async Task LoadDataAsync(CancellationToken ct = default)
        {
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StatusMessage = "Loading data...";
                _logger?.LogInformation("MainViewModel loading overview data");

                if (_dbContextFactory == null)
                {
                    LoadSampleData();
                    IsConnected = false;
                    return;
                }

                if (ct.IsCancellationRequested) return;
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

                // Test connection
                IsConnected = await db.Database.CanConnectAsync();

                if (!IsConnected)
                {
                    StatusMessage = "Database connection unavailable";
                    LoadSampleData();
                    return;
                }

                // Load account summary
                var accountSummary = await db.MunicipalAccounts
                    .GroupBy(a => 1)
                    .Select(g => new
                    {
                        Count = g.Count(),
                        TotalBudget = g.Sum(a => a.BudgetAmount),
                        TotalBalance = g.Sum(a => a.Balance)
                    })
                    .FirstOrDefaultAsync(ct);

                if (accountSummary != null)
                {
                    TotalAccountCount = accountSummary.Count;
                    TotalBudget = accountSummary.TotalBudget;

                    // Build recent metrics for quick overview grid
                    RecentMetrics = new ObservableCollection<KeyValuePair<string, double>>
                    {
                        new("Total Accounts", accountSummary.Count),
                        new("Total Budget", (double)accountSummary.TotalBudget),
                        new("Current Balance", (double)accountSummary.TotalBalance),
                        new("Remaining", (double)(accountSummary.TotalBudget - accountSummary.TotalBalance))
                    };

                    // Build quick metrics with formatting
                    QuickMetrics = new ObservableCollection<QuickMetricItem>
                    {
                        new() { Name = "Accounts", Value = accountSummary.Count, FormattedValue = accountSummary.Count.ToString("N0"), Icon = "📋", Category = "Overview" },
                        new() { Name = "Budget", Value = (double)accountSummary.TotalBudget, FormattedValue = accountSummary.TotalBudget.ToString("C0"), Icon = "💰", Category = "Financial" },
                        new() { Name = "Spent", Value = (double)accountSummary.TotalBalance, FormattedValue = accountSummary.TotalBalance.ToString("C0"), Icon = "📊", Category = "Financial" },
                        new() { Name = "Available", Value = (double)(accountSummary.TotalBudget - accountSummary.TotalBalance), FormattedValue = (accountSummary.TotalBudget - accountSummary.TotalBalance).ToString("C0"), Icon = "✅", Category = "Financial" }
                    };
                }

                LastDataRefresh = DateTime.Now;
                StatusMessage = $"Data loaded at {LastDataRefresh:HH:mm:ss}";
                _logger?.LogInformation("MainViewModel overview data loaded: {Accounts} accounts, {Budget:C} total budget",
                    TotalAccountCount, TotalBudget);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load MainViewModel data");
                ErrorMessage = $"Data load failed: {ex.Message}";
                StatusMessage = "Error loading data";
                LoadSampleData();
            }
            finally
            {
                IsLoading = false;
                _loadLock.Release();
            }

        partial void OnIsLoadingChanged(bool value)
        {
            IsBusy = value;
        }
        }

        /// <summary>
        /// Refreshes all overview data.
        /// </summary>
        private async Task RefreshAsync(CancellationToken ct = default)
        {
            _logger?.LogInformation("Refreshing MainViewModel data");
            await LoadDataAsync(ct);
        }

        /// <summary>
        /// Navigates to the specified view and updates CurrentView.
        /// </summary>
        private void NavigateTo(string? viewName)
        {
            if (string.IsNullOrEmpty(viewName)) return;

            CurrentView = viewName;
            StatusMessage = $"Navigated to {viewName}";
            _logger?.LogInformation("Navigation: {View}", viewName);

            // Broadcast navigation event
            _messenger.Send(new MyEvent(new { NavigatedTo = viewName }));
        }

        /// <summary>
        /// Handles incoming MyEvent messages from other ViewModels.
        /// </summary>
        private void HandleEvent(object? payload)
        {
            _logger?.LogDebug("MainViewModel received event: {Payload}", payload);
            // Handle cross-ViewModel communication here
        }

        /// <summary>
        /// Loads sample data when database is unavailable.
        /// </summary>
        private void LoadSampleData()
        {
            TotalAccountCount = 42;
            TotalBudget = 1_500_000m;

            RecentMetrics = new ObservableCollection<KeyValuePair<string, double>>
            {
                new("Total Accounts", 42),
                new("Total Budget", 1_500_000),
                new("Current Balance", 1_125_000),
                new("Remaining", 375_000)
            };

            QuickMetrics = new ObservableCollection<QuickMetricItem>
            {
                new() { Name = "Accounts", Value = 42, FormattedValue = "42", Icon = "📋", Category = "Overview" },
                new() { Name = "Budget", Value = 1_500_000, FormattedValue = "$1,500,000", Icon = "💰", Category = "Financial" },
                new() { Name = "Spent", Value = 1_125_000, FormattedValue = "$1,125,000", Icon = "📊", Category = "Financial" },
                new() { Name = "Available", Value = 375_000, FormattedValue = "$375,000", Icon = "✅", Category = "Financial" }
            };

            LastDataRefresh = DateTime.Now;
            StatusMessage = "Using sample data (no database connection)";
        }
    }
}

