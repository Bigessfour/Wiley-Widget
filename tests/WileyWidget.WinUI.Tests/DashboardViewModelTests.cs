using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinUI.ViewModels.Main;
using Xunit;

namespace WileyWidget.WinUI.Tests;

public class DashboardViewModelTests
{
    private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;

    public DashboardViewModelTests()
    {
        _mockLogger = new Mock<ILogger<DashboardViewModel>>();
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange & Act
        var vm = new DashboardViewModel(_mockLogger.Object);

        // Assert
        vm.Title.Should().Be("Dashboard");
        vm.IsLoading.Should().BeFalse();
        vm.DashboardItems.Should().NotBeNull();
        vm.DashboardItems.Should().BeEmpty();
        vm.LoadDashboardCommand.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadDashboardCommand_ExecutesSuccessfully_AddsItemsAndStopsLoading()
    {
        // Arrange
        var vm = new DashboardViewModel(_mockLogger.Object);

        // Act
        await vm.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        vm.DashboardItems.Should().HaveCount(1);
        vm.DashboardItems[0].Title.Should().Contain("Total Budget");
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadDashboardCommand_SetsIsLoadingDuringExecution()
    {
        // Arrange
        var vm = new DashboardViewModel(_mockLogger.Object);
        var loadingStates = new List<bool>();

        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading))
                loadingStates.Add(vm.IsLoading);
        };

        // Act
        var task = vm.LoadDashboardCommand.ExecuteAsync(null);

        // Immediately after starting the async command, the viewmodel should be loading at some point
        loadingStates.Should().Contain(true);

        await task; // finish

        // Assert final state
        vm.IsLoading.Should().BeFalse();
        loadingStates.Should().Contain(false);
    }
}
