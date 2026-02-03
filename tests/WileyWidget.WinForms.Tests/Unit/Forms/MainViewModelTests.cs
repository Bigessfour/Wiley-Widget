using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class MainViewModelTests
    {
        [Fact]
        public async Task LoadDataCommand_PopulatesTotalsAndMetrics()
        {
            // Arrange
            var dashboardItems = new List<DashboardItem>
            {
                new DashboardItem { Title = "Total Budget", Category = "budget", Value = "1000" },
                new DashboardItem { Title = "Total Actual", Category = "actual", Value = "800" },
                new DashboardItem { Title = "Variance", Category = "variance", Value = "200" }
            };

            var dashboardMock = new Mock<IDashboardService>();
            dashboardMock.Setup(d => d.GetDashboardDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dashboardItems);

            var loggerMock = new Mock<ILogger<MainViewModel>>();
            var aiLoggerMock = new Mock<IAILoggingService>();
            var quickBooksMock = new Mock<IQuickBooksService>();
            var globalSearchMock = new Mock<IGlobalSearchService>();

            var vm = new MainViewModel(loggerMock.Object, dashboardMock.Object, aiLoggerMock.Object, quickBooksMock.Object, globalSearchMock.Object);

            // Act - invoke private LoadDataAsync via reflection
            var mi = typeof(MainViewModel).GetMethod("LoadDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task)mi.Invoke(vm, new object[] { CancellationToken.None })!;
            await task;

            // Assert
            vm.TotalBudget.Should().Be(1000m);
            vm.TotalActual.Should().Be(800m);
            vm.Variance.Should().Be(200m);
            vm.Metrics.Should().NotBeNull();
            vm.Metrics.Count.Should().BeGreaterOrEqualTo(2);
        }

        [Fact]
        public async Task VarianceCalculations_ComputePercentage_And_StatusColorAssigned()
        {
            // Arrange: over-budget scenario
            var dashboardItems = new List<DashboardItem>
            {
                new DashboardItem { Title = "Total Budget", Category = "budget", Value = "1000" },
                new DashboardItem { Title = "Total Actual", Category = "actual", Value = "1150" },
                new DashboardItem { Title = "Variance", Category = "variance", Value = "-150" }
            };

            var dashboardMock = new Mock<IDashboardService>();
            dashboardMock.Setup(d => d.GetDashboardDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dashboardItems);

            var loggerMock = new Mock<ILogger<MainViewModel>>();
            var aiLoggerMock = new Mock<IAILoggingService>();
            var quickBooksMock = new Mock<IQuickBooksService>();
            var globalSearchMock = new Mock<IGlobalSearchService>();

            var vm = new MainViewModel(loggerMock.Object, dashboardMock.Object, aiLoggerMock.Object, quickBooksMock.Object, globalSearchMock.Object);

            // Act
            var mi = typeof(MainViewModel).GetMethod("LoadDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task)mi.Invoke(vm, new object[] { CancellationToken.None })!;
            await task;

            // Expected percentage
            var expectedPct = Math.Abs(vm.Variance / (vm.TotalBudget == 0 ? 1 : vm.TotalBudget)) * 100m;

            // Assert
            vm.VariancePercentage.Should().BeApproximately(expectedPct, 0.0001m);
            // Status color should be assigned to one of the semantic values
            var allowed = new[] { "Green", "Orange", "Red" };
            allowed.Should().Contain(vm.VarianceStatusColor);
        }
    }
}