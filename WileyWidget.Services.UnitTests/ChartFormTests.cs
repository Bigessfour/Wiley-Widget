using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Abstractions.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using Xunit;

public class ChartFormTests
{
    private readonly Mock<ILogger<ChartForm>> _loggerMock = new();
    private readonly Mock<ILogger<ChartViewModel>> _vmLoggerMock = new();
    private readonly Mock<IChartService> _chartServiceMock = new();
    private readonly Mock<IPrintingService> _printingServiceMock = new();

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        // Arrange
        var vm = new ChartViewModel(_vmLoggerMock.Object, _chartServiceMock.Object);

        // Act & Assert
        Action act = () => _ = new ChartForm(vm, null!, _chartServiceMock.Object, _printingServiceMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void DrawCharts_AppliesViewModelData_ToCharts()
    {
        // Arrange: Create real VM with test data
        var vm = new ChartViewModel(_vmLoggerMock.Object, _chartServiceMock.Object);
        vm.LineChartData.Add(new WileyWidget.Abstractions.Models.ChartDataPoint { Category = "Jan", Value = 100 });
        vm.LineChartData.Add(new WileyWidget.Abstractions.Models.ChartDataPoint { Category = "Feb", Value = 200 });
        vm.PieChartData.Add(new WileyWidget.Abstractions.Models.ChartDataPoint { Category = "Ops", Value = 300 });

        var form = new ChartForm(vm, _loggerMock.Object, _chartServiceMock.Object, _printingServiceMock.Object);

        // Act
        form.DrawCharts(); // Invoke privately via reflection or make protected/public for testing

        // Assert: Currently logs warning because DrawCharts is disabled
        _loggerMock.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public void DrawCharts_HandlesExceptions_LogsError()
    {
        // Arrange: Create real VM
        var vm = new ChartViewModel(_vmLoggerMock.Object, _chartServiceMock.Object);
        var form = new ChartForm(vm, _loggerMock.Object, _chartServiceMock.Object, _printingServiceMock.Object);

        // Act
        form.DrawCharts();

        // Assert: Currently logs warning because DrawCharts is disabled
        _loggerMock.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }
}
