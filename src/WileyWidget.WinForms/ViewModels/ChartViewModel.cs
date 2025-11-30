using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.WinForms.ViewModels
{
    // Lightweight view model that loads simple chart data from the AppDbContext.
    // Uses IDbContextFactory so the view model can create short-lived DbContexts safely from the WinForms DI container.
    // Implements ObservableObject so forms can data-bind and react to property changes.
    using CommunityToolkit.Mvvm.ComponentModel;
    using System.Collections.ObjectModel;

    public partial class ChartViewModel : ObservableObject
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly ILogger<ChartViewModel>? _logger;

        public ChartViewModel(IDbContextFactory<AppDbContext> dbFactory, ILogger<ChartViewModel>? logger = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _logger = logger;
            RefreshCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(LoadChartDataAsync);
        }

        // Chart data as an observable collection so forms can bind to changes
        [ObservableProperty]
        private ObservableCollection<KeyValuePair<string, double>> chartData = new();

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? statusMessage = "Ready";

        // Error message if data loading fails
        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private bool isLoading;

        public async Task LoadChartDataAsync(CancellationToken ct = default)
        {
            _logger?.LogDebug("ChartViewModel.LoadChartDataAsync starting");
            ErrorMessage = null;
            if (ct.IsCancellationRequested) return;
            IsLoading = true;
            try
            {
                StatusMessage = "Loading chart data...";
                await using var db = await _dbFactory.CreateDbContextAsync(ct);

                var query = await db.Departments
                    .Select(d => new
                    {
                        Name = d.Name,
                        Budgeted = d.MunicipalAccounts.Sum(a => (decimal?)a.BudgetAmount) ?? 0m,
                        Actual = d.MunicipalAccounts.Sum(a => (decimal?)a.Balance) ?? 0m
                    })
                    .ToListAsync(ct);

                var dict = query
                    .Select(x => new KeyValuePair<string, double>(string.IsNullOrWhiteSpace(x.Name) ? "Unknown" : x.Name, (double)(x.Actual - x.Budgeted)))
                    .ToList();

                ChartData = new ObservableCollection<KeyValuePair<string, double>>(dict);
                _logger?.LogInformation("Chart data loaded successfully with {Count} points", ChartData?.Count ?? 0);
            }
            catch (Exception ex)
            {
                // Capture error for UI display
                ErrorMessage = $"Unable to load chart data: {ex.Message}";
                _logger?.LogError(ex, "Failed loading chart data");
                ChartData = new ObservableCollection<KeyValuePair<string, double>>();
            }
            finally
            {
                IsLoading = false;
                StatusMessage = "Ready";
                _logger?.LogDebug("ChartViewModel.LoadChartDataAsync finished");
            }

        partial void OnIsLoadingChanged(bool value)
        {
            IsBusy = value;
        }
        }

        // MVVM Command to refresh chart data from UI
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand RefreshCommand { get; }
    }
}
