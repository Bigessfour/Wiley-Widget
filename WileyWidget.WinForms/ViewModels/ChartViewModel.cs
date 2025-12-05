using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
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
    public partial class ChartViewModel
    {
        private readonly ILogger<ChartViewModel> _logger;
        private readonly IChartService _chartService;
        private readonly IMainDashboardService _dashboardService;

        public ObservableCollection<ChartSeries> RevenueTrendSeries { get; } = new();
        public ObservableCollection<ChartSeries> ExpenditureColumnSeries { get; } = new();
        public ObservableCollection<ChartSeries> BudgetStackedSeries { get; } = new();
        public ObservableCollection<ChartSeries> ProportionPieSeries { get; } = new();

        public ObservableCollection<ChartDataPoint> LineChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> PieChartData { get; } = new();

        public int SelectedYear { get; set; } = DateTime.UtcNow.Year;
        public string SelectedCategory { get; set; } = "All Categories";

        public ChartViewModel(ILogger<ChartViewModel> logger, IChartService chartService, IMainDashboardService dashboardService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));

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

        public async Task LoadChartsAsync(int? year = null, string? category = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieve real production data via the ChartService and DashboardService
                var selectedYear = year ?? SelectedYear;
                var selectedCategory = category ?? SelectedCategory;

                var monthlyPoints = (await _chartService.GetMonthlyTotalsAsync(selectedYear, cancellationToken)).ToList();
                var start = new DateTime(selectedYear, 1, 1);
                var end = new DateTime(selectedYear, 12, 31);
                var breakdownPoints = (await _chartService.GetCategoryBreakdownAsync(start, end, selectedCategory, cancellationToken)).ToList();
                var dashboard = await _dashboardService.LoadDashboardDataAsync(cancellationToken);

                // Clear existing data
                RevenueTrendSeries.Clear();
                ExpenditureColumnSeries.Clear();
                BudgetStackedSeries.Clear();
                ProportionPieSeries.Clear();
                LineChartData.Clear();
                PieChartData.Clear();

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

                var revenueSeries = new ChartSeries("Revenue Trends", ChartSeriesType.Line);
                for (int i = 0; i < monthlyPoints.Count; i++)
                {
                    var dataPoint = monthlyPoints[i];
                    revenueSeries.Points.Add(new ChartPoint(i + 1, dataPoint.Value));
                }
                RevenueTrendSeries.Add(revenueSeries);

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
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "Chart load was canceled");
                // Silently return — cancellations are expected during shutdown/rapid navigation
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoadChartsAsync failed");
                throw;
            }
        }
    }
}
