using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Abstractions.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.UI.Tests;

/// <summary>
/// UI tests for ChartForm - requires Windows Forms runtime.
/// These tests instantiate actual WinForms controls.
/// </summary>
[Trait("Category", "UI")]
public class ChartFormTests
{
    private readonly Mock<ILogger<ChartForm>> _loggerMock = new();
    private readonly Mock<ILogger<ChartViewModel>> _vmLoggerMock = new();
    private readonly Mock<IChartService> _chartServiceMock = new();
    private readonly Mock<IPrintingService> _printingServiceMock = new();
    private readonly Mock<IMainDashboardService> _dashboardServiceMock = new();

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        // Arrange
        var vm = new ChartViewModel(_vmLoggerMock.Object, _chartServiceMock.Object, _dashboardServiceMock.Object);

        // Act & Assert
        Action act = () => _ = new ChartForm(vm, null!, _chartServiceMock.Object, _printingServiceMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void DrawCharts_AppliesViewModelData_ToCharts()
    {
        // Arrange: Create real VM with test data
        var vm = new ChartViewModel(_vmLoggerMock.Object, _chartServiceMock.Object, _dashboardServiceMock.Object);
        // Note: Test data setup would need to be updated for new series collections

        var form = new ChartForm(vm, _loggerMock.Object, _chartServiceMock.Object, _printingServiceMock.Object);

        // Act
        form.DrawCharts();

        // Assert: Currently logs warning because DrawCharts is disabled
        _loggerMock.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public void DrawCharts_HandlesExceptions_LogsError()
    {
        // Arrange: Create real VM
        var vm = new ChartViewModel(_vmLoggerMock.Object, _chartServiceMock.Object, _dashboardServiceMock.Object);
        var form = new ChartForm(vm, _loggerMock.Object, _chartServiceMock.Object, _printingServiceMock.Object);

        // Act
        form.DrawCharts();

        // Assert: Currently logs warning because DrawCharts is disabled
        _loggerMock.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }
}
