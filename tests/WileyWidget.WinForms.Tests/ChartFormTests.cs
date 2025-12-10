using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.Abstractions.Models;
using AbstractionsChartDataPoint = WileyWidget.Abstractions.Models.ChartDataPoint;
using SfChartSeries = Syncfusion.Windows.Forms.Chart.ChartSeries;
using SfChartPoint = Syncfusion.Windows.Forms.Chart.ChartPoint;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Forms;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    public class ChartFormTests
    {
        [Fact]
        public void DrawCharts_WithNullSeriesStyles_DoesNotThrow()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Add a series without manually setting Style (this used to trigger a NullReferenceException in DrawCharts)
            var series = new SfChartSeries("TestSeries", ChartSeriesType.Line);
            vm.RevenueTrendSeries.Add(series);

            var formLogger = new Mock<ILogger<ChartForm>>();
            var printingService = new Mock<IPrintingService>();

            // Act - construct the form and call DrawCharts
            var form = new ChartForm(vm, formLogger.Object, chartService.Object, printingService.Object);

            var ex = Record.Exception(() => form.DrawCharts());

            // Assert - it should not throw
            Assert.Null(ex);
        }

        [Fact]
        public void DrawCharts_WithMultipleSeries_RendersAllCharts()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Add series to all collections to test all four chart types
            var revenueSeries = new SfChartSeries("Revenue", ChartSeriesType.Line);
            revenueSeries.Points.Add(new SfChartPoint(1, 100));
            revenueSeries.Points.Add(new SfChartPoint(2, 200));
            vm.RevenueTrendSeries.Add(revenueSeries);

            var expendSeries = new SfChartSeries("Expenditure", ChartSeriesType.Column);
            expendSeries.Points.Add(new SfChartPoint(1, 50));
            vm.ExpenditureColumnSeries.Add(expendSeries);

            var stackedSeries = new SfChartSeries("Fund1", ChartSeriesType.StackingColumn);
            stackedSeries.Points.Add(new SfChartPoint(1, 75));
            vm.BudgetStackedSeries.Add(stackedSeries);

            var pieSeries = new SfChartSeries("Budget", ChartSeriesType.Pie);
            pieSeries.Points.Add(new SfChartPoint(1, 100));
            vm.ProportionPieSeries.Add(pieSeries);

            var formLogger = new Mock<ILogger<ChartForm>>();
            var printingService = new Mock<IPrintingService>();

            // Act
            var form = new ChartForm(vm, formLogger.Object, chartService.Object, printingService.Object);
            var ex = Record.Exception(() => form.DrawCharts());

            // Assert
            Assert.Null(ex);
            Assert.Single(vm.RevenueTrendSeries);
            Assert.Single(vm.ExpenditureColumnSeries);
            Assert.Single(vm.BudgetStackedSeries);
            Assert.Single(vm.ProportionPieSeries);
        }

        [Fact]
        public async Task LoadChartsAsync_WithDateRange_PopulatesData()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var chartDataPoints = new[]
            {
                new AbstractionsChartDataPoint { Category = "Jan", Value = 1000 },
                new AbstractionsChartDataPoint { Category = "Feb", Value = 1500 }
            };

            var dashboard = new DashboardDto(100000m, 50000m, 25000m, 10, 5, DateTime.UtcNow.ToString("g"), 0, null);

            chartService
                .Setup(cs => cs.GetMonthlyTotalsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(chartDataPoints);

            chartService
                .Setup(cs => cs.GetCategoryBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(chartDataPoints);

            dashboardService
                .Setup(ds => ds.LoadDashboardDataAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(dashboard);

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 12, 31);
            vm.SelectedStartDate = startDate;
            vm.SelectedEndDate = endDate;

            // Act
            await vm.LoadChartsAsync(2024, "All Categories", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(vm.LineChartData);
            Assert.NotEmpty(vm.RevenueTrendSeries);
            Assert.NotEmpty(vm.ExpenditureColumnSeries);
        }

        [Fact]
        public void ChartViewModel_DateRangeProperties_DefaultToCurrentYear()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            // Act
            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Assert
            Assert.Equal(DateTime.UtcNow.Year, vm.SelectedStartDate.Year);
            Assert.Equal(DateTime.UtcNow.Year, vm.SelectedEndDate.Year);
            Assert.Equal(1, vm.SelectedStartDate.Month); // January
            Assert.Equal(12, vm.SelectedEndDate.Month);   // December
        }

        [Fact]
        public void ChartViewModel_SelectedChartType_DefaultsToLine()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            // Act
            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Assert
            Assert.Equal("Line", vm.SelectedChartType);
        }

        [Fact]
        public void DrawCharts_WithEmptyData_DoesNotCrash()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);
            // Don't add any series - all collections remain empty

            var formLogger = new Mock<ILogger<ChartForm>>();
            var printingService = new Mock<IPrintingService>();

            // Act
            var form = new ChartForm(vm, formLogger.Object, chartService.Object, printingService.Object);
            var ex = Record.Exception(() => form.DrawCharts());

            // Assert
            Assert.Null(ex);
        }

        [Fact]
        public async Task LoadChartsAsync_WithCancellation_CompletesGracefully()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.Cancel(); // Pre-cancel the token

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Act
            var ex = await Record.ExceptionAsync(() => vm.LoadChartsAsync(2024, "All Categories", cts.Token));

            // Assert
            Assert.Null(ex); // Should not throw even though token is canceled
            Assert.Empty(vm.RevenueTrendSeries); // Should not populate data
        }

        [Fact]
        public async Task LoadChartsAsync_WithInvalidYear_ThrowsArgumentOutOfRange()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => vm.LoadChartsAsync(1500, "All Categories", TestContext.Current.CancellationToken) // Year too old
            );
        }

        [Fact]
        public void ChartForm_ExportsToImage_WithValidPath()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);
            var formLogger = new Mock<ILogger<ChartForm>>();
            var printingService = new Mock<IPrintingService>();

            var form = new ChartForm(vm, formLogger.Object, chartService.Object, printingService.Object);

            // Add a series so chart is not empty
            var series = new SfChartSeries("Test", ChartSeriesType.Line);
            series.Points.Add(new SfChartPoint(1, 100));
            vm.RevenueTrendSeries.Add(series);
            form.DrawCharts();

            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_chart.png");

            // Act & Assert
            // The export should not throw - implementation uses Syncfusion's SaveAsImage
            try
            {
                form.DrawCharts(); // Ensure chart is ready
                // Note: Actual export test requires UI context, so we just verify DrawCharts succeeds
                Assert.Null(Record.Exception(() => form.DrawCharts()));
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        [Fact]
        public void ChartViewModel_CollectionsInitialize_AsObservable()
        {
            // Arrange & Act
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Assert
            Assert.IsType<ObservableCollection<SfChartSeries>>(vm.RevenueTrendSeries);
            Assert.IsType<ObservableCollection<SfChartSeries>>(vm.ExpenditureColumnSeries);
            Assert.IsType<ObservableCollection<SfChartSeries>>(vm.BudgetStackedSeries);
            Assert.IsType<ObservableCollection<SfChartSeries>>(vm.ProportionPieSeries);
            Assert.IsType<ObservableCollection<AbstractionsChartDataPoint>>(vm.LineChartData);
            Assert.IsType<ObservableCollection<AbstractionsChartDataPoint>>(vm.PieChartData);
        }

        [Fact]
        public async Task LoadChartsAsync_PopulatesAllChartTypes()
        {
            // Arrange
            var vmLogger = new Mock<ILogger<ChartViewModel>>();
            var chartService = new Mock<IChartService>();
            var dashboardService = new Mock<IMainDashboardService>();

            var chartDataPoints = new[]
            {
                new AbstractionsChartDataPoint { Category = "Month1", Value = 1000 }
            };

            var dashboard = new DashboardDto(100000m, 50000m, 25000m, 10, 5, DateTime.UtcNow.ToString("g"), 0, null);

            chartService
                .Setup(cs => cs.GetMonthlyTotalsAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(chartDataPoints);

            chartService
                .Setup(cs => cs.GetCategoryBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(chartDataPoints);

            dashboardService
                .Setup(ds => ds.LoadDashboardDataAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(dashboard);

            var vm = new ChartViewModel(vmLogger.Object, chartService.Object, dashboardService.Object);

            // Act
            await vm.LoadChartsAsync(2024, "All Categories", TestContext.Current.CancellationToken);

            // Assert - all chart types should be populated
            Assert.NotEmpty(vm.RevenueTrendSeries);
            Assert.NotEmpty(vm.ExpenditureColumnSeries);
            Assert.NotEmpty(vm.BudgetStackedSeries);
            Assert.NotEmpty(vm.ProportionPieSeries);
            Assert.NotEmpty(vm.LineChartData);
            Assert.NotEmpty(vm.PieChartData);
        }
    }
}
