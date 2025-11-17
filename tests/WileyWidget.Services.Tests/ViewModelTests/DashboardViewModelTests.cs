using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Navigation.Regions;
using Xunit;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinUI.ViewModels.Main;

namespace WileyWidget.Services.Tests.ViewModelTests;

/// <summary>
/// Comprehensive unit tests for DashboardViewModel.
/// Tests command execution, property notifications, navigation lifecycle, and error handling.
/// </summary>
public class DashboardViewModelTests
{
    private readonly Mock<IDashboardService> _mockDashboardService;
    private readonly Mock<IRegionManager> _mockRegionManager;
    private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;

    public DashboardViewModelTests()
    {
        _mockDashboardService = MockFactory.CreateDashboardService();
        _mockRegionManager = MockFactory.CreateRegionManager();
        _mockLogger = MockFactory.CreateLogger<DashboardViewModel>();
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Title.Should().Be("Dashboard");
        viewModel.IsLoading.Should().BeFalse();
        viewModel.DashboardItems.Should().NotBeNull();
        viewModel.DashboardItems.Should().BeEmpty();
        viewModel.LoadDashboardCommand.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadDashboardCommand_ExecutesSuccessfully()
    {
        // Arrange
        var testItems = MockFactory.TestData.CreateDashboardItems(2);
        _mockDashboardService.Setup(s => s.GetDashboardItemsAsync())
            .ReturnsAsync(testItems);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        viewModel.DashboardItems.Should().HaveCount(2);
        viewModel.DashboardItems[0].Title.Should().Be("Test Item 1");
        viewModel.DashboardItems[1].Count.Should().Be(10);
        viewModel.IsLoading.Should().BeFalse();

        _mockDashboardService.Verify(s => s.GetDashboardItemsAsync(), Times.Once);
        _mockLogger.VerifyLog(LogLevel.Information, "Dashboard loaded successfully");
    }

    [Fact]
    public async Task LoadDashboardCommand_WithServiceError_HandlesGracefully()
    {
        // Arrange
        var expectedException = new Exception("Service unavailable");
        _mockDashboardService.Setup(s => s.GetDashboardItemsAsync())
            .ThrowsAsync(expectedException);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsLoading.Should().BeFalse();
        viewModel.DashboardItems.Should().BeEmpty();

        _mockLogger.VerifyLog(LogLevel.Error, "Failed to load dashboard");
        _mockLogger.VerifyLogError(expectedException);
    }

    [Fact]
    public async Task LoadDashboardCommand_WithEmptyResult_ClearsCollection()
    {
        // Arrange
        var emptyItems = new List<DashboardItem>();
        _mockDashboardService.Setup(s => s.GetDashboardItemsAsync())
            .ReturnsAsync(emptyItems);

        var viewModel = CreateViewModel();

        // Pre-populate with some items
        viewModel.DashboardItems.Add(new DashboardItem { Title = "Existing Item" });

        // Act
        await viewModel.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        viewModel.DashboardItems.Should().BeEmpty();
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadDashboardCommand_SetsIsLoadingCorrectly()
    {
        // Arrange
        var tcs = new TaskCompletionSource<List<DashboardItem>>();
        _mockDashboardService.Setup(s => s.GetDashboardItemsAsync())
            .Returns(tcs.Task);

        var viewModel = CreateViewModel();
        var loadingStates = new List<bool>();

        // Monitor IsLoading property changes
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(DashboardViewModel.IsLoading))
            {
                loadingStates.Add(viewModel.IsLoading);
            }
        };

        // Act - Start the command
        var commandTask = viewModel.LoadDashboardCommand.ExecuteAsync(null);

        // Assert - Should be loading
        loadingStates.Should().Contain(true);

        // Complete the service call
        tcs.SetResult(new List<DashboardItem>());
        await commandTask;

        // Assert - Should not be loading anymore
        viewModel.IsLoading.Should().BeFalse();
        loadingStates.Should().Contain(false);
    }

    [Fact]
    public async Task LoadDashboardCommand_CancelsOnDispose()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<List<DashboardItem>>();
        _mockDashboardService.Setup(s => s.GetDashboardItemsAsync())
            .Returns(tcs.Task);

        var viewModel = CreateViewModel();

        // Act - Start command and immediately dispose
        var commandTask = viewModel.LoadDashboardCommand.ExecuteAsync(null);
        cts.Cancel();

        // Complete the task (normally this would be cancelled)
        tcs.SetResult(new List<DashboardItem>());

        // Assert - Command should complete without throwing
        await commandTask;
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnNavigatedTo_TriggersLoadCommand()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var navigationContext = new Mock<NavigationContext>().Object;

        // Act
        viewModel.OnNavigatedTo(navigationContext);

        // Assert
        _mockDashboardService.Verify(s => s.GetDashboardItemsAsync(), Times.Once);
        _mockLogger.VerifyLog(LogLevel.Information, "Navigated to Dashboard");
    }

    [Fact]
    public void IsNavigationTarget_ReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var navigationContext = new Mock<NavigationContext>().Object;

        // Act
        var result = viewModel.IsNavigationTarget(navigationContext);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OnNavigatedFrom_LogsNavigation()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var navigationContext = new Mock<NavigationContext>().Object;

        // Act
        viewModel.OnNavigatedFrom(navigationContext);

        // Assert
        _mockLogger.VerifyLog(LogLevel.Information, "Navigated from Dashboard");
    }

    [Fact]
    public void Title_PropertyChange_RaisesNotification()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        string? changedPropertyName = null;

        viewModel.PropertyChanged += (sender, args) =>
        {
            propertyChangedRaised = true;
            changedPropertyName = args.PropertyName;
        };

        // Act
        viewModel.Title = "New Dashboard Title";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        changedPropertyName.Should().Be(nameof(DashboardViewModel.Title));
        viewModel.Title.Should().Be("New Dashboard Title");
    }

    [Fact]
    public void DashboardItems_AddItem_RaisesCollectionChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var collectionChangedRaised = false;
        NotifyCollectionChangedAction? action = null;

        viewModel.DashboardItems.CollectionChanged += (sender, args) =>
        {
            collectionChangedRaised = true;
            action = args.Action;
        };

        // Act
        viewModel.DashboardItems.Add(new DashboardItem { Title = "New Item" });

        // Assert
        collectionChangedRaised.Should().BeTrue();
        action.Should().Be(NotifyCollectionChangedAction.Add);
        viewModel.DashboardItems.Should().HaveCount(1);
        viewModel.DashboardItems[0].Title.Should().Be("New Item");
    }

    [Fact]
    public void Constructor_WithNullService_ThrowsException()
    {
        // Arrange & Act & Assert
        Action act = () => new DashboardViewModel(
            _mockLogger.Object,
            _mockRegionManager.Object,
            null!); // Null service

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dashboardService");
    }

    private DashboardViewModel CreateViewModel()
    {
        return new DashboardViewModel(
            _mockLogger.Object,
            _mockRegionManager.Object,
            _mockDashboardService.Object);
    }
}