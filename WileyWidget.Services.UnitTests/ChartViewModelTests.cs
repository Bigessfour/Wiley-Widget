using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Business.Interfaces;
using WileyWidget.Abstractions.Models;
using Xunit;

public class ChartViewModelTests
{
    private readonly Mock<ILogger<ChartViewModel>> _loggerMock = new();
    private readonly Mock<IChartService> _serviceMock = new();

    [Fact]
        public void Constructor_ThrowsOnNullArgs()
        {
            // Act & Assert
            Action act = () => _ = new ChartViewModel(null!, _serviceMock.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");

            act = () => _ = new ChartViewModel(_loggerMock.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("chartService");
        }

    [Fact]
    public async Task LoadChartDataAsync_PopulatesCollections_FromService()
    {
        // Arrange
        var monthly = new[] { new ChartDataPoint { Category = "Jan", Value = 100 } };
        var breakdown = new[] { new ChartDataPoint { Category = "Ops", Value = 200 } };
        _serviceMock.Setup(s => s.GetMonthlyTotalsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(monthly);
        _serviceMock.Setup(s => s.GetCategoryBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(breakdown);
        var vm = new ChartViewModel(_loggerMock.Object, _serviceMock.Object);

        // Act
        await vm.LoadChartDataAsync();

        // Assert
        vm.LineChartData.Should().BeEquivalentTo(monthly);
        vm.PieChartData.Should().BeEquivalentTo(breakdown);
        _serviceMock.Verify(s => s.GetMonthlyTotalsAsync(DateTime.UtcNow.Year, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
        public async Task LoadChartDataAsync_HandlesCancellation_WithoutError()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetMonthlyTotalsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
            var vm = new ChartViewModel(_loggerMock.Object, _serviceMock.Object);

            // Act
            Func<Task> act = async () => await vm.LoadChartDataAsync(cancellationToken: new CancellationToken(true));

            // Assert
            await act.Should().NotThrowAsync();
            _loggerMock.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
        }

    [Fact]
        public async Task LoadChartDataAsync_LogsError_OnFailure()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetMonthlyTotalsAsync(It.IsAny<int>(), default)).ThrowsAsync(new InvalidOperationException("DB error"));
            var vm = new ChartViewModel(_loggerMock.Object, _serviceMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadChartDataAsync());
            _loggerMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
}
