using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.Services.UnitTests
{
    public class WinFormsDashboardViewModelCtorTests
    {
        [Fact]
        public async Task Constructor_WithServiceThrowing_DoesNotThrowAndSetsErrorMessage()
        {
            var mockService = new Mock<IDashboardService>();
            mockService.Setup(s => s.GetDashboardDataAsync()).ThrowsAsync(new InvalidOperationException("DB exploded"));
            mockService.Setup(s => s.GetDashboardSummaryAsync()).ThrowsAsync(new InvalidOperationException("DB exploded"));

            var mockLogger = new Mock<ILogger<DashboardViewModel>>();

            // Ensure constructor doesn't throw
            DashboardViewModel vm = null!;
            var act = () => vm = new DashboardViewModel(mockService.Object, mockLogger.Object);
            act.Should().NotThrow();

            // Wait for background load to complete (constructor triggers an async load)
            await Task.Delay(250); // small delay to let background task run

            // After failing load, view model should have an error message and not be loading
            vm.IsLoading.Should().BeFalse();
            vm.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Constructor_WithSuccessfulService_LoadsMetrics()
        {
            var mockService = new Mock<IDashboardService>();
            mockService.Setup(s => s.GetDashboardDataAsync()).ReturnsAsync(Array.Empty<Models.DashboardMetric>());
            mockService.Setup(s => s.GetDashboardSummaryAsync()).ReturnsAsync(new Models.DashboardSummary { MunicipalityName = "Town of Wiley" });

            var mockLogger = new Mock<ILogger<DashboardViewModel>>();

            var vm = new DashboardViewModel(mockService.Object, mockLogger.Object);

            // Wait a tiny bit for initial load to complete
            await Task.Delay(250);

            vm.IsLoading.Should().BeFalse();
            vm.ErrorMessage.Should().BeNull();
            vm.Metrics.Should().NotBeNull();
        }
    }
}
