using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for chart data binding using Syncfusion.Windows.Forms.Chart.
    /// Provides data for bar/line/area charts and pie charts using ObservableCollection for data binding.
    /// </summary>
    public partial class ChartViewModel : ObservableObject
    {
        private readonly ILogger<ChartViewModel> _logger;
        private readonly IDashboardService _dashboardService;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private int selectedYear = DateTime.UtcNow.Year;

        [ObservableProperty]
        private string selectedCategory = "All Categories";

        [ObservableProperty]
    private DateTime selectedStartDate = new(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [ObservableProperty]
    private DateTime selectedEndDate = new(DateTime.UtcNow.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    [ObservableProperty]
    private string selectedChartType = "Line";

    public ObservableCollection<MonthlyRevenue> MonthlyRevenueData { get; } = new();
    public ObservableCollection<(string Category, decimal Value)> PieChartData { get; } = new();
    public ObservableCollection<KeyValuePair<string, decimal>> ChartData { get; } = new();
    public ObservableCollection<ChartDataPoint> LineChartData { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<int> LoadChartsByYearCommand { get; }

    public ChartViewModel(ILogger<ChartViewModel> logger, IDashboardService dashboardService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));

        RefreshCommand = new AsyncRelayCommand(LoadChartDataAsync);
        LoadChartsByYearCommand = new AsyncRelayCommand<int>(async year => await LoadChartsAsync(year));

        _logger.LogInformation("ChartViewModel constructed");
    }

    /// <summary>
        /// Load chart data with optional year filter and cancellation support.
        /// </summary>
        public async Task LoadChartsAsync(int? year = null, string? category = null, CancellationToken cancellationToken = default)
        {
            var yearToLoad = year ?? SelectedYear;
            if (yearToLoad < 2000 || yearToLoad > DateTime.UtcNow.Year + 10)
            {
                ErrorMessage = $"Year must be between 2000 and {DateTime.UtcNow.Year + 10}";
                _logger.LogWarning("Invalid year requested: {Year}", yearToLoad);
                return;
            }

            var categoryToLoad = category ?? SelectedCategory;

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _logger.LogInformation("Loading charts for year {Year}, category {Category}, date range {StartDate} to {EndDate}",
                    yearToLoad, categoryToLoad, SelectedStartDate, SelectedEndDate);

                // Early cancellation check
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Chart load canceled before service calls");
                    return;
                }

                // TODO: Load and filter dashboard data by year/category
                // For now using sample data generation
                await _dashboardService.GetDashboardDataAsync();

                cancellationToken.ThrowIfCancellationRequested();

                // Clear and populate collections
                ChartData.Clear();
                MonthlyRevenueData.Clear();
                PieChartData.Clear();
                LineChartData.Clear();

                // Generate sample data (replace with real service calls in production)
                await GenerateSampleDataAsync(yearToLoad, categoryToLoad, cancellationToken);

                _logger.LogInformation("Successfully loaded chart data");
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogDebug(ex, "Chart load was canceled");
                ErrorMessage = null; // Cancellation is expected, don't show as error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chart data");
                ErrorMessage = $"Failed to load charts: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadChartDataAsync()
        {
            await LoadChartsAsync();
        }

        private async Task GenerateSampleDataAsync(int year, string? category, CancellationToken cancellationToken)
        {
            // Category parameter reserved for future filtering logic
            await Task.Run(() =>
            {
                var random = new Random(year);
                var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

                // Monthly revenue data
                for (int i = 0; i < monthNames.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    MonthlyRevenueData.Add(new MonthlyRevenue
                    {
                        Month = monthNames[i],
                        MonthNumber = i + 1,
                        Amount = (decimal)(random.NextDouble() * 50000 + 50000)
                    });

                    LineChartData.Add(new ChartDataPoint
                    {
                        XValue = monthNames[i],
                        YValue = random.NextDouble() * 50000 + 50000,
                        Label = monthNames[i]
                    });
                }

                // Category breakdown for pie chart
                var categories = new[] { "Admin", "Public Works", "Public Safety", "Parks", "Utilities" };
                foreach (var cat in categories)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var value = (decimal)(random.NextDouble() * 100000 + 50000);
                    PieChartData.Add((cat, value));
                    ChartData.Add(new KeyValuePair<string, decimal>(cat, value));
                }
            }, cancellationToken);
        }
    }
}
