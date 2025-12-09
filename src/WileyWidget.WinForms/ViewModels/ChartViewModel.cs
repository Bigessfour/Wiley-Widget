using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using WileyWidget.Business.Interfaces;
using WileyWidget.Abstractions.Models;
using WileyWidget.WinForms.Forms;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for chart data binding using Syncfusion.Windows.Forms.Chart (v31.2.16).
    /// Reference: https://help.syncfusion.com/windowsforms/chart/overview
    /// Provides data for bar/line/area charts and pie charts using ObservableCollection for data binding.
    /// </summary>
    public partial class ChartViewModel : ObservableObject
    {
        private readonly ILogger<ChartViewModel> _logger;
        private readonly IChartService _chartService;
        private readonly IMainDashboardService _dashboardService;

        /// <summary>
        /// Revenue trend series data for line charts.
        /// </summary>
        public ObservableCollection<ChartSeries> RevenueTrendSeries { get; } = new();

        /// <summary>
        /// Expenditure column series data.
        /// </summary>
        public ObservableCollection<ChartSeries> ExpenditureColumnSeries { get; } = new();

        /// <summary>
        /// Budget stacked series data.
        /// </summary>
        public ObservableCollection<ChartSeries> BudgetStackedSeries { get; } = new();

        /// <summary>
        /// Proportion pie series data.
        /// </summary>
        public ObservableCollection<ChartSeries> ProportionPieSeries { get; } = new();

        /// <summary>
        /// Line chart data points.
        /// </summary>
        public ObservableCollection<ChartDataPoint> LineChartData { get; } = new();

        /// <summary>
        /// Pie chart data points.
        /// </summary>
        public ObservableCollection<ChartDataPoint> PieChartData { get; } = new();

        /// <summary>
        /// Selected year for filtering chart data.
        /// </summary>
        [ObservableProperty]
        private int selectedYear = DateTime.UtcNow.Year;

        /// <summary>
        /// Selected category for filtering chart data.
        /// </summary>
        [ObservableProperty]
        private string selectedCategory = "All Categories";

        /// <summary>
        /// Selected start date for filtering chart data.
        /// </summary>
        [ObservableProperty]
        private DateTime selectedStartDate = new DateTime(DateTime.UtcNow.Year, 1, 1);

        /// <summary>
        /// Selected end date for filtering chart data.
        /// </summary>
        [ObservableProperty]
        private DateTime selectedEndDate = new DateTime(DateTime.UtcNow.Year, 12, 31);

        /// <summary>
        /// Selected chart type (Line, Bar, etc.).
        /// </summary>
        [ObservableProperty]
        private string selectedChartType = "Line";

        /// <summary>
        /// Initializes a new instance of the <see cref="ChartViewModel"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for the ViewModel.</param>
        /// <param name="chartService">Service for chart data operations.</param>
        /// <param name="dashboardService">Service for dashboard data operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public ChartViewModel(ILogger<ChartViewModel> logger, IChartService chartService, IMainDashboardService dashboardService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(chartService);
            ArgumentNullException.ThrowIfNull(dashboardService);

            _logger = logger;
            _chartService = chartService;
            _dashboardService = dashboardService;

            try
            {
                // initial lightweight construction performed without heavy work
                _logger.LogInformation("ChartViewModel constructed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChartViewModel constructor failed");
                throw;
            }
        }

        /// <summary>
        /// Loads chart data asynchronously for the specified year and category.
        /// </summary>
        /// <param name="year">Optional year filter. Defaults to current year.</param>
        /// <param name="category">Optional category filter. Defaults to "All Categories".</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when year is out of valid range.</exception>
        /// <exception cref="ArgumentException">Thrown when category is null or empty.</exception>
        public async Task LoadChartsAsync(int? year = null, string? category = null, CancellationToken cancellationToken = default)
        {
            // Input validation
            var selectedYear = year ?? SelectedYear;
            if (selectedYear < 2000 || selectedYear > DateTime.UtcNow.Year + 10)
            {
                throw new ArgumentOutOfRangeException(nameof(year), $"Year must be between 2000 and {DateTime.UtcNow.Year + 10}");
            }

            var selectedCategory = category ?? SelectedCategory;
            if (string.IsNullOrWhiteSpace(selectedCategory))
            {
                throw new ArgumentException("Category cannot be null or empty", nameof(category));
            }

            try
            {
                _logger.LogInformation("Loading charts for year {Year}, category {Category}, date range {StartDate} to {EndDate}",
                    selectedYear, selectedCategory, SelectedStartDate, SelectedEndDate);

                // If the operation was already canceled, return early without calling services
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Chart load canceled before service calls for year {Year}, category {Category}", selectedYear, selectedCategory);
                    return;
                }

                // Retrieve real production data via the ChartService and DashboardService
                var monthlyPoints = (await _chartService.GetMonthlyTotalsAsync(selectedYear, cancellationToken).ConfigureAwait(false)).ToList();
                var breakdownPoints = (await _chartService.GetCategoryBreakdownAsync(SelectedStartDate, SelectedEndDate, selectedCategory, cancellationToken).ConfigureAwait(false)).ToList();
                var dashboard = await _dashboardService.LoadDashboardDataAsync(cancellationToken).ConfigureAwait(false);

                // Validate data integrity
                if (monthlyPoints == null)
                {
                    throw new InvalidOperationException("Monthly points data is null");
                }
                if (breakdownPoints == null)
                {
                    throw new InvalidOperationException("Breakdown points data is null");
                }
                if (dashboard == null)
                {
                    throw new InvalidOperationException("Dashboard data is null");
                }

                // Clear existing data (batch operation for better performance)
                RevenueTrendSeries.Clear();
                ExpenditureColumnSeries.Clear();
                BudgetStackedSeries.Clear();
                ProportionPieSeries.Clear();
                LineChartData.Clear();
                PieChartData.Clear();

                // Populate chart data collections
                PopulateChartDataCollections(monthlyPoints, breakdownPoints);

                // Create and populate chart series
                CreateChartSeries(monthlyPoints, breakdownPoints, dashboard);

                _logger.LogInformation("Successfully loaded charts with {MonthlyCount} monthly points and {BreakdownCount} breakdown points",
                    monthlyPoints.Count, breakdownPoints.Count);
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogDebug(oce, "Chart load was canceled for year {Year}, category {Category}", selectedYear, selectedCategory);
                // Silently return - cancellations are expected during shutdown/rapid navigation
                return;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid arguments provided for chart loading");
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Data integrity issue during chart loading");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoadChartsAsync failed for year {Year}, category {Category}", selectedYear, selectedCategory);
                throw;
            }
        }

        /// <summary>
        /// Populates the chart data collections from the service data points.
        /// </summary>
        private void PopulateChartDataCollections(IEnumerable<ChartDataPoint> monthlyPoints, IEnumerable<ChartDataPoint> breakdownPoints)
        {
            foreach (var point in monthlyPoints)
            {
                LineChartData.Add(new ChartDataPoint
                {
                    Category = point.Category,
                    Value = point.Value
                });
            }

            foreach (var point in breakdownPoints)
            {
                PieChartData.Add(new ChartDataPoint
                {
                    Category = point.Category,
                    Value = point.Value
                });
            }
        }

        /// <summary>
        /// Creates and populates chart series for Syncfusion charts.
        /// </summary>
        private void CreateChartSeries(IReadOnlyList<ChartDataPoint> monthlyPoints, IReadOnlyList<ChartDataPoint> breakdownPoints, DashboardDto dashboard)
        {
            // Revenue Trends: Line Chart
            var revenueSeries = new ChartSeries("Revenue Trends", ChartSeriesType.Line);
            for (int i = 0; i < monthlyPoints.Count; i++)
            {
                revenueSeries.Points.Add(new ChartPoint(i + 1, monthlyPoints[i].Value));
            }
            RevenueTrendSeries.Add(revenueSeries);

            // Expenditures: Column Chart
            var expSeries = new ChartSeries("Expenditures", ChartSeriesType.Column);
            for (int i = 0; i < breakdownPoints.Count; i++)
            {
                expSeries.Points.Add(new ChartPoint(i + 1, breakdownPoints[i].Value));
            }
            ExpenditureColumnSeries.Add(expSeries);

            // Budget Overview: Stacked Column (demo data, as dept data not available)
            var stackedSeries1 = new ChartSeries("Fund 1", ChartSeriesType.StackingColumn);
            stackedSeries1.Points.Add(new ChartPoint(1, (double)dashboard.TotalBudget * 0.25));
            stackedSeries1.Points.Add(new ChartPoint(2, (double)dashboard.TotalBudget * 0.3));
            var stackedSeries2 = new ChartSeries("Fund 2", ChartSeriesType.StackingColumn);
            stackedSeries2.Points.Add(new ChartPoint(1, (double)dashboard.TotalBudget * 0.2));
            stackedSeries2.Points.Add(new ChartPoint(2, (double)dashboard.TotalBudget * 0.25));
            BudgetStackedSeries.Add(stackedSeries1);
            BudgetStackedSeries.Add(stackedSeries2);

            // Proportions: Pie Chart
            var pieSeries = new ChartSeries("Budget Split", ChartSeriesType.Pie);
            pieSeries.Points.Add(new ChartPoint(1, (double)dashboard.TotalBudget));
            pieSeries.Points.Add(new ChartPoint(2, (double)dashboard.TotalActual));
            ProportionPieSeries.Add(pieSeries);
        }
    }
}
