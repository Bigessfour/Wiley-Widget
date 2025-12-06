using System;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Forms;
using WileyWidget.Services;
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
            var series = new ChartSeries("TestSeries", ChartSeriesType.Line);
            // ensure Style is null by default (we rely on SDK default)

            vm.RevenueTrendSeries.Add(series);

            var formLogger = new Mock<ILogger<ChartForm>>();
            var printingService = new Mock<IPrintingService>();

            // Act - construct the form and call DrawCharts
            var form = new ChartForm(vm, formLogger.Object, chartService.Object, printingService.Object);

            var ex = Record.Exception(() => form.DrawCharts());

            // Assert - it should not throw
            Assert.Null(ex);
        }
    }
}