using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.Services.Tests;

public class DashboardServiceTests
{
    private readonly Mock<ILogger<DashboardService>> _mockLogger;
    private readonly Mock<IDashboardRepository> _mockRepository;
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _mockLogger = new Mock<ILogger<DashboardService>>();
        _mockRepository = new Mock<IDashboardRepository>();

        // Setup mock repository with test data
        _mockRepository.Setup(r => r.GetDashboardMetricsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<DashboardMetric>
            {
                new DashboardMetric { Name = "Total Budget", Value = 2500000, Unit = "USD" },
                new DashboardMetric { Name = "Total Revenue", Value = 2000000, Unit = "USD" },
                new DashboardMetric { Name = "Total Expenses", Value = 1750000, Unit = "USD" },
                new DashboardMetric { Name = "Net Position", Value = 250000, Unit = "USD" },
                new DashboardMetric { Name = "Active Accounts", Value = 10, Unit = "Count" }
            });

        _mockRepository.Setup(r => r.GetTotalBudgetAsync(It.IsAny<string>())).ReturnsAsync(2500000m);
        _mockRepository.Setup(r => r.GetTotalRevenueAsync(It.IsAny<string>())).ReturnsAsync(2000000m);
        _mockRepository.Setup(r => r.GetTotalExpensesAsync(It.IsAny<string>())).ReturnsAsync(1750000m);

        _service = new DashboardService(_mockLogger.Object, _mockRepository.Object);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsExpectedSummary()
    {
        // Act
        var summary = await _service.GetDashboardSummaryAsync();

        // Assert: values match mocked data
        Assert.Equal("Town of Wiley", summary.MunicipalityName);
        Assert.NotEmpty(summary.Metrics);
        Assert.True(summary.TotalBudget > 0);
        Assert.True(summary.TotalRevenue > 0);
        Assert.True(summary.TotalExpenses > 0);
    }

    [Fact]
    public async Task RefreshDashboardAsync_LogsRefresh()
    {
        // Act
        await _service.RefreshDashboardAsync();

        // Assert: verify logger saw the refresh completion message
        _mockLogger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Dashboard data refreshed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ), Times.Once);
    }
}
