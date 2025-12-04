using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using System.Threading;
using WileyWidget.Business.Interfaces;
using WileyWidget.Abstractions.Models;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for chart data binding using LiveChartsCore.SkiaSharpView.WinForms (v2.2.0).
    /// Reference: https://livecharts.dev/winforms/2.0.0/
    /// Provides data for line charts and pie charts using ObservableCollection for proper LiveChartsCore data binding.
    /// </summary>
    public class ChartViewModel
    {
        private readonly ILogger<ChartViewModel> _logger;
        private readonly IChartService _chartService;

        public ObservableCollection<ChartDataPoint> LineChartData { get; set; } = new();
        public ObservableCollection<ChartDataPoint> PieChartData { get; set; } = new();

        public int SelectedYear { get; set; } = DateTime.UtcNow.Year;
        public string SelectedCategory { get; set; } = "All Categories";

        public ChartViewModel(ILogger<ChartViewModel> logger, IChartService chartService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));

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

        public async Task LoadChartDataAsync(int? year = null, string? category = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieve real production data via the ChartService
                var selectedYear = year ?? SelectedYear;
                var selectedCategory = category ?? SelectedCategory;

                var monthly = await _chartService.GetMonthlyTotalsAsync(selectedYear, cancellationToken);
                LineChartData.Clear();
                foreach (var p in monthly) LineChartData.Add(p);

                // Category breakdown: for the selected year
                var start = new DateTime(selectedYear, 1, 1);
                var end = new DateTime(selectedYear, 12, 31);
                var breakdown = await _chartService.GetCategoryBreakdownAsync(start, end, selectedCategory, cancellationToken);
                PieChartData.Clear();
                foreach (var p in breakdown) PieChartData.Add(p);
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "Chart load was canceled");
                // Silently return — cancellations are expected during shutdown/rapid navigation
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoadChartDataAsync failed");
                throw;
            }
        }
    }
}
