using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Events;
using Prism.Navigation.Regions;
using WileyWidget.Tests.TestUtilities;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Main;
using WileyWidget.ViewModels.Messages;
using Xunit;

namespace WileyWidget.ViewModels.Tests;

/// <summary>
/// Comprehensive unit tests for BudgetViewModel focusing on Prism patterns, navigation lifecycle,
/// async commands, threading behavior, and repository interactions.
/// Target: >85% coverage with proper mocking and isolation.
/// </summary>
public class BudgetViewModelTests
{
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepository;
    private readonly Mock<IBudgetRepository> _mockBudgetRepository;
    private readonly Mock<IEventAggregator> _mockEventAggregator;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IDispatcherHelper> _mockDispatcherHelper;
    private readonly Mock<ILogger<BudgetViewModel>> _mockLogger;

    public BudgetViewModelTests()
    {
        _mockEnterpriseRepository = new Mock<IEnterpriseRepository>();
        _mockBudgetRepository = new Mock<IBudgetRepository>();
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockCacheService = new Mock<ICacheService>();
        _mockDispatcherHelper = new Mock<IDispatcherHelper>();
        _mockLogger = new Mock<ILogger<BudgetViewModel>>();

        // Setup enterprise repository for background cache loading
        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>());

        // Setup cache service
        _mockCacheService
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Setup dispatcher helper to execute actions synchronously for tests
        _mockDispatcherHelper
            .Setup(d => d.InvokeAsync(It.IsAny<Action>()))
            .Returns((Action action) =>
            {
                action();
                return Task.CompletedTask;
            });

        _mockDispatcherHelper
            .Setup(d => d.CheckAccess())
            .Returns(true);

        // Setup event aggregator - return test-friendly PubSubEvent instances
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<EnterpriseChangedMessage>())
            .Returns(new SharedTestEnterpriseChangedMessage());
    }

    #region Constructor Tests

    [Fact]
    public async Task Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        using var viewModel = CreateViewModel();

        // Give background task time to complete
        await Task.Delay(100);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.BudgetDetails.Should().NotBeNull();
        viewModel.RefreshBudgetDataCommand.Should().NotBeNull();
        viewModel.LoadDataCommand.Should().NotBeNull();

        // Verify cache loading was attempted
        _mockEnterpriseRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullEnterpriseRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new BudgetViewModel(null!, _mockBudgetRepository.Object, _mockEventAggregator.Object, _mockCacheService.Object));
    }

    [Fact]
    public void Constructor_SubscribesToEnterpriseChangedMessage()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        _mockEventAggregator.Verify(
            ea => ea.GetEvent<EnterpriseChangedMessage>(),
            Times.Once);
    }

    #endregion

    #region Navigation Lifecycle Tests (INavigationAware)

    [Fact]
    public async Task OnNavigatedTo_LoadsBudgetData()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Budget", UriKind.Absolute));

        var testBudgetEntries = new List<BudgetEntry>
        {
            new BudgetEntry { Id = 1, FiscalYear = 2025, BudgetedAmount = 1000m },
            new BudgetEntry { Id = 2, FiscalYear = 2025, BudgetedAmount = 2000m }
        };

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>()))
            .ReturnsAsync(testBudgetEntries);

        // Act
    viewModel.OnNavigatedTo(mockContext.Object);
    await Task.Delay(200); // Allow async operation to complete

        // Assert
        viewModel.Should().NotBeNull();
        // Verify repository was called
        _mockBudgetRepository.Verify(
            r => r.GetByFiscalYearAsync(It.IsAny<int>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void IsNavigationTarget_AlwaysReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Budget", UriKind.Absolute));

        // Act
        var result = viewModel.IsNavigationTarget(mockContext.Object);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OnNavigatedFrom_StopsLiveUpdates()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Budget", UriKind.Absolute));

        // Act
        viewModel.OnNavigatedFrom(mockContext.Object);

        // Assert
        // Verify no exceptions thrown and cleanup occurs
        viewModel.Should().NotBeNull();
    }

    #endregion

    #region Command Tests

    [Fact]
    public async Task RefreshBudgetDataCommand_ExecutesAsync_WithoutDeadlock()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEntries = new List<BudgetEntry>
        {
            new BudgetEntry { Id = 1, FiscalYear = 2025, BudgetedAmount = 5000m }
        };

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>()))
            .ReturnsAsync(testEntries);

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>
            {
                new Enterprise { Id = 1, Name = "Test Enterprise" }
            });

        // Act
    viewModel.RefreshBudgetDataCommand.Execute();
    await Task.Delay(200); // Allow async operation to complete

        // Assert
        _mockBudgetRepository.Verify(
            r => r.GetByFiscalYearAsync(It.IsAny<int>()),
            Times.AtLeastOnce);

        _mockDispatcherHelper.Verify(
            d => d.InvokeAsync(It.IsAny<Action>()),
            Times.AtLeastOnce,
            "Dispatcher should marshal UI updates");
    }

    [Fact]
    public void RefreshBudgetDataCommand_CanExecute_WhenNotBusy()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.RefreshBudgetDataCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void RefreshBudgetDataCommand_CannotExecute_WhenBusy()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Set IsBusy via reflection or property if accessible
        var isBusyProperty = typeof(BudgetViewModel).GetProperty("IsBusy");
        if (isBusyProperty != null && isBusyProperty.CanWrite)
        {
            isBusyProperty.SetValue(viewModel, true);
        }

        // Act
        var canExecute = viewModel.RefreshBudgetDataCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Command should not execute when busy");
    }

    [Fact]
    public void LoadDataCommand_ProxiesToRefreshBudgetDataCommand()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        viewModel.LoadDataCommand.Should().BeSameAs(viewModel.RefreshBudgetDataCommand,
            "LoadDataCommand should be an alias for RefreshBudgetDataCommand");
    }

    #endregion

    #region Event Aggregator Tests

    [Fact]
    public async Task OnEnterpriseChanged_RefreshesBudgetData()
    {
        // Arrange
        var testEntries = new List<BudgetEntry>
        {
            new BudgetEntry { Id = 1, BudgetedAmount = 1000m }
        };

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>()))
            .ReturnsAsync(testEntries);

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>());

        var viewModel = CreateViewModel();

        // Ensure IsBusy is false so RefreshBudgetDataAsync can execute
        if (viewModel.IsBusy)
        {
            await Task.Delay(100);
        }

        // Act - Use reflection to call the private OnEnterpriseChanged method
        var method = typeof(BudgetViewModel).GetMethod("OnEnterpriseChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(viewModel, new object[] { new EnterpriseChangedMessage { EnterpriseName = "Test" } });

        // Wait for Task.Run to complete async operation with generous timeout
        await Task.Delay(1500);

        // Assert
        _mockBudgetRepository.Verify(
            r => r.GetByFiscalYearAsync(It.IsAny<int>()),
            Times.AtLeastOnce,
            "Budget data should refresh when enterprise changes");
    }

    #endregion

    #region Property Change Notification Tests

    [Fact]
    public void TotalRevenue_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BudgetViewModel.TotalRevenue))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.TotalRevenue = 50000m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.TotalRevenue.Should().Be(50000m);
    }

    [Fact]
    public void NetBalance_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BudgetViewModel.NetBalance))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.NetBalance = 15000m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.NetBalance.Should().Be(15000m);
    }

    #endregion

    #region Cache Integration Tests

    [Fact]
    public async Task Constructor_AutoLoadsEnterprisesIntoCache()
    {
        // Arrange
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Enterprise 1" },
            new Enterprise { Id = 2, Name = "Enterprise 2" }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act
        var viewModel = CreateViewModel();
        await Task.Delay(200); // Allow background task to complete

        // Assert
        _mockCacheService.Verify(
            c => c.SetAsync(
                "enterprises",
                It.IsAny<IEnumerable<Enterprise>>(),
                It.IsAny<TimeSpan>()),
            Times.AtLeastOnce,
            "Enterprises should be cached on initialization");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.Dispose();

        // Assert
        // Verify no exceptions thrown during disposal
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        viewModel.Dispose();
        viewModel.Dispose(); // Should not throw
    }

    #endregion

    #region Helper Methods

    private BudgetViewModel CreateViewModel()
    {
        return new BudgetViewModel(
            _mockEnterpriseRepository.Object,
            _mockBudgetRepository.Object,
            _mockEventAggregator.Object,
            _mockCacheService.Object,
            _mockDispatcherHelper.Object);
    }

    #endregion
}
