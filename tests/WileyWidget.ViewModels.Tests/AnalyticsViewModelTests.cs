using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
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
            var mockBudgetRepo = new Mock<IBudgetRepository>();
            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            var mockLogger = new Mock<ILogger<AnalyticsViewModel>>();

            var start = new DateTime(DateTime.Now.Year - 1, 7, 1);
            var end = new DateTime(DateTime.Now.Year, 6, 30);

            mockAnalytics.Setup(m => m.PerformExploratoryAnalysisAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                .ReturnsAsync(new BudgetAnalysisResult
                {
                    CategoryBreakdown = new Dictionary<string, decimal> { { "Revenue", 1000m } },
                    TopVariances = new List<VarianceAnalysis>(),
                    TrendData = new TrendAnalysis { MonthlyTrends = new List<MonthlyTrend>() },
                    Insights = new List<string> { "Sample insight" }
                });

            var entries = new List<BudgetEntry>
            {
                new BudgetEntry { Id = 1, Fund = new Fund { Name = "Sanitation District" }, StartPeriod = start, EndPeriod = end },
                new BudgetEntry { Id = 2, Fund = new Fund { Name = "Town Utility" }, StartPeriod = start, EndPeriod = end }
            };

            mockBudgetRepo.Setup(m => m.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries);

            mockEnterpriseRepo.Setup(m => m.GetAllAsync()).ReturnsAsync(new List<Enterprise> { new Enterprise { Name = "Town Utility" } });

            var vm = new AnalyticsViewModel(mockAnalytics.Object, mockLogger.Object, mockBudgetRepo.Object, mockEnterpriseRepo.Object);

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
            var mockBudgetRepo = new Mock<IBudgetRepository>();
            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            var mockLogger = new Mock<ILogger<AnalyticsViewModel>>();

            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            mockAnalytics.Setup(m => m.PerformExploratoryAnalysisAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                .ReturnsAsync(new BudgetAnalysisResult())
                .Callback<DateTime, DateTime, string?>((sd, ed, ent) => tcs.TrySetResult(ent));

            mockBudgetRepo.Setup(m => m.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<BudgetEntry>());

            mockEnterpriseRepo.Setup(m => m.GetAllAsync()).ReturnsAsync(new List<Enterprise>());

            var vm = new AnalyticsViewModel(mockAnalytics.Object, mockLogger.Object, mockBudgetRepo.Object, mockEnterpriseRepo.Object);

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
