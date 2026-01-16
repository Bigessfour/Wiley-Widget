using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.ViewModels.Tests
{
    public class AnalyticsViewModelTests
    {
        [Fact]
        public async Task PerformExploratoryAnalysis_PopulatesAvailableEntitiesAndIncludesAllEntities()
        {
            // Arrange
            var mockAnalytics = new Mock<IAnalyticsService>();
            var mockLogger = new Mock<ILogger<AnalyticsViewModel>>();

            mockAnalytics.Setup(m => m.PerformExploratoryAnalysisAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BudgetAnalysisResult
                {
                    CategoryBreakdown = new Dictionary<string, decimal> { { "Revenue", 1000m } },
                    TopVariances = new List<VarianceAnalysis>(),
                    TrendData = new TrendAnalysis { MonthlyTrends = new List<MonthlyTrend>() },
                    Insights = new List<string> { "Sample insight" },
                    AvailableEntities = new List<string> { "Sanitation District", "Town Utility" }
                });

            mockAnalytics.Setup(m => m.RunRateScenarioAsync(It.IsAny<RateScenarioParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RateScenarioResult());

            mockAnalytics.Setup(m => m.GenerateReserveForecastAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReserveForecastResult());

            var vm = new AnalyticsViewModel(mockAnalytics.Object, mockLogger.Object);

            // Act
            await vm.PerformAnalysisCommand.ExecuteAsync(null);

            // Assert
            vm.AvailableEntities.Should().NotBeNull();
            vm.AvailableEntities.Should().Contain("All Entities");
            vm.AvailableEntities.Should().Contain("Sanitation District");
            vm.AvailableEntities.Should().Contain("Town Utility");
        }

        [Fact]
        public async Task SelectedEntityChange_ReRunsAnalysis_CallsAnalyticsWithSelectedEntity()
        {
            // Arrange
            var mockAnalytics = new Mock<IAnalyticsService>();
            var mockLogger = new Mock<ILogger<AnalyticsViewModel>>();

            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            mockAnalytics.Setup(m => m.PerformExploratoryAnalysisAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BudgetAnalysisResult())
                .Callback<DateTime, DateTime, string?, CancellationToken>((_, _, ent, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(ent))
                    {
                        tcs.TrySetResult(ent);
                    }
                });

            mockAnalytics.Setup(m => m.RunRateScenarioAsync(It.IsAny<RateScenarioParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RateScenarioResult());

            mockAnalytics.Setup(m => m.GenerateReserveForecastAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReserveForecastResult());

            var vm = new AnalyticsViewModel(mockAnalytics.Object, mockLogger.Object);

            // Act - change the selected entity and wait for the analytics service to be called
            vm.SelectedEntity = "Sanitation District";

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            completedTask.Should().Be(tcs.Task, "PerformExploratoryAnalysisAsync should be invoked when SelectedEntity changes");

            // Assert
            var entityArg = await tcs.Task;
            entityArg.Should().Be("Sanitation District");
        }
    }
}
