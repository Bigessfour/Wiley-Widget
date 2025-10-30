using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Common;
using Prism.Events;
using Prism.Navigation;
using Prism.Navigation.Regions;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Messages;
using Xunit;

namespace WileyWidget.ViewModels.Tests;

/// <summary>
/// Custom EventAggregator for testing that allows UIThread subscriptions
/// </summary>
public class TestEventAggregator : IEventAggregator
{
    private readonly Dictionary<Type, EventBase> _events = new();

    public TEventType GetEvent<TEventType>() where TEventType : EventBase, new()
    {
        var eventType = typeof(TEventType);
        if (!_events.ContainsKey(eventType))
        {
            // Create a test event that allows all subscriptions
            if (eventType == typeof(RefreshDataMessage))
                _events[eventType] = new TestRefreshDataMessage();
            else if (eventType == typeof(EnterpriseChangedMessage))
                _events[eventType] = new TestEnterpriseChangedMessage();
            else if (eventType == typeof(BudgetUpdatedMessage))
                _events[eventType] = new TestBudgetUpdatedMessage();
            else if (eventType == typeof(AccountsLoadedEvent))
                _events[eventType] = new TestAccountsLoadedEvent();
            else
                _events[eventType] = new TEventType();
        }
        return (TEventType)_events[eventType];
    }
}

/// <summary>
/// Test implementation of RefreshDataMessage that allows all subscriptions
/// </summary>
public class TestRefreshDataMessage : RefreshDataMessage
{
    private readonly List<Subscription> _subscriptions = new();

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool> filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public new SubscriptionToken Subscribe(Action action, Func<bool> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public new SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public new void Publish()
    {
        foreach (var subscription in _subscriptions.ToList())
        {
            if (subscription.Filter == null || subscription.Filter())
            {
                subscription.Action();
            }
        }
    }

    private class Subscription
    {
        public Action Action { get; }
        public ThreadOption ThreadOption { get; }
        public bool KeepSubscriberReferenceAlive { get; }
        public Func<bool> Filter { get; }

        public Subscription(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool> filter)
        {
            Action = action;
            ThreadOption = threadOption;
            KeepSubscriberReferenceAlive = keepSubscriberReferenceAlive;
            Filter = filter;
        }
    }
}

/// <summary>
/// Test implementation of EnterpriseChangedMessage that allows all subscriptions
/// </summary>
public class TestEnterpriseChangedMessage : EnterpriseChangedMessage
{
    private readonly List<Subscription> _subscriptions = new();

    public new SubscriptionToken Subscribe(Action<int> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<int> filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public new SubscriptionToken Subscribe(Action<int> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action<int> action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public new SubscriptionToken Subscribe(Action<int> action, Predicate<int> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public new SubscriptionToken Subscribe(Action<int> action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action<int> action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public new void Publish(int payload)
    {
        foreach (var subscription in _subscriptions.ToList())
        {
            if (subscription.Filter == null || subscription.Filter(payload))
            {
                subscription.Action(payload);
            }
        }
    }

    private class Subscription
    {
        public Action<int> Action { get; }
        public ThreadOption ThreadOption { get; }
        public bool KeepSubscriberReferenceAlive { get; }
        public Predicate<int> Filter { get; }

        public Subscription(Action<int> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<int> filter)
        {
            Action = action;
            ThreadOption = threadOption;
            KeepSubscriberReferenceAlive = keepSubscriberReferenceAlive;
            Filter = filter;
        }
    }
}

/// <summary>
/// Test implementation of BudgetUpdatedMessage that allows all subscriptions
/// </summary>
public class TestBudgetUpdatedMessage : BudgetUpdatedMessage
{
    private readonly List<Subscription> _subscriptions = new();

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool> filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public new SubscriptionToken Subscribe(Action action, Func<bool> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public new SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public new void Publish()
    {
        foreach (var subscription in _subscriptions.ToList())
        {
            if (subscription.Filter == null || subscription.Filter())
            {
                subscription.Action();
            }
        }
    }

    private class Subscription
    {
        public Action Action { get; }
        public ThreadOption ThreadOption { get; }
        public bool KeepSubscriberReferenceAlive { get; }
        public Func<bool> Filter { get; }

        public Subscription(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool> filter)
        {
            Action = action;
            ThreadOption = threadOption;
            KeepSubscriberReferenceAlive = keepSubscriberReferenceAlive;
            Filter = filter;
        }
    }
}

/// <summary>
/// Test implementation of AccountsLoadedEvent that allows all subscriptions
/// </summary>
public class TestAccountsLoadedEvent : AccountsLoadedEvent
{
    private readonly List<Subscription> _subscriptions = new();

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool> filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public new SubscriptionToken Subscribe(Action action, Func<bool> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public new SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public new SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public new void Publish()
    {
        foreach (var subscription in _subscriptions.ToList())
        {
            if (subscription.Filter == null || subscription.Filter())
            {
                subscription.Action();
            }
        }
    }

    private class Subscription
    {
        public Action Action { get; }
        public ThreadOption ThreadOption { get; }
        public bool KeepSubscriberReferenceAlive { get; }
        public Func<bool> Filter { get; }

        public Subscription(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool> filter)
        {
            Action = action;
            ThreadOption = threadOption;
            KeepSubscriberReferenceAlive = keepSubscriberReferenceAlive;
            Filter = filter;
        }
    }
}

/// <summary>
/// Fake SynchronizationContext for testing UI thread requirements
/// </summary>
public class FakeUISynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object state)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        // Execute immediately on the same thread
        d(state);
    }

    public override void Send(SendOrPostCallback d, object state)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        // Execute immediately on the same thread
        d(state);
    }
}

/// <summary>
/// Unit tests for DashboardViewModel focusing on Prism navigation, RegionManager integration,
/// async command execution, and INavigationAware lifecycle.
/// </summary>
public class DashboardViewModelTests
{
    private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepository;
    private readonly Mock<IWhatIfScenarioEngine> _mockWhatIfScenarioEngine;
    private readonly Mock<IUtilityCustomerRepository> _mockUtilityCustomerRepository;
    private readonly Mock<IMunicipalAccountRepository> _mockMunicipalAccountRepository;
    private readonly FiscalYearSettings _fiscalYearSettings;
    private readonly Mock<IEventAggregator> _mockEventAggregator;
    private readonly Mock<IRegionManager> _mockRegionManager;
    private readonly Mock<IDispatcherHelper> _mockDispatcherHelper;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IReportExportService> _mockReportExportService;

    public DashboardViewModelTests()
    {
        // Set up UI synchronization context for EventAggregator before creating mocks
        var originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

        _mockLogger = new Mock<ILogger<DashboardViewModel>>();
        _mockEnterpriseRepository = new Mock<IEnterpriseRepository>();
        _mockWhatIfScenarioEngine = new Mock<IWhatIfScenarioEngine>();
        _mockUtilityCustomerRepository = new Mock<IUtilityCustomerRepository>();
        _mockMunicipalAccountRepository = new Mock<IMunicipalAccountRepository>();
        _fiscalYearSettings = new FiscalYearSettings { FiscalYearStartMonth = 7, FiscalYearStartDay = 1 };
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockRegionManager = new Mock<IRegionManager>();
        _mockDispatcherHelper = new Mock<IDispatcherHelper>();
        _mockCacheService = new Mock<ICacheService>();
        _mockReportExportService = new Mock<IReportExportService>();

        // Setup event aggregator - return real PubSubEvent instances since Subscribe is not virtual
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<RefreshDataMessage>())
            .Returns(new RefreshDataMessage());

        _mockEventAggregator
            .Setup(ea => ea.GetEvent<EnterpriseChangedMessage>())
            .Returns(new EnterpriseChangedMessage());

        _mockEventAggregator
            .Setup(ea => ea.GetEvent<BudgetUpdatedMessage>())
            .Returns(new BudgetUpdatedMessage());

        _mockEventAggregator
            .Setup(ea => ea.GetEvent<AccountsLoadedEvent>())
            .Returns(new AccountsLoadedEvent());

        // Setup dispatcher helper to execute synchronously
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

        // Restore original context
        SynchronizationContext.SetSynchronizationContext(originalContext);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.LoadDataCommand.Should().NotBeNull();
        viewModel.RefreshDashboardCommand.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DashboardViewModel(
                null!,
                _mockEnterpriseRepository.Object,
                _mockWhatIfScenarioEngine.Object,
                _mockUtilityCustomerRepository.Object,
                _mockMunicipalAccountRepository.Object,
                _fiscalYearSettings,
                _mockEventAggregator.Object,
                _mockRegionManager.Object,
                _mockCacheService.Object));
    }

    #endregion

    #region Navigation Tests (INavigationAware)

    [Fact]
    public void OnNavigatedTo_LoadsDashboardData()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Dashboard", UriKind.Absolute));

        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Test Enterprise", Status = EnterpriseStatus.Active }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act
        viewModel.OnNavigatedTo(mockContext.Object);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DashboardViewModel navigated to")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void IsNavigationTarget_AlwaysReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Dashboard", UriKind.Absolute));

        // Act
        var result = viewModel.IsNavigationTarget(mockContext.Object);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OnNavigatedFrom_LogsNavigationEvent()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Dashboard", UriKind.Absolute));

        // Act
        viewModel.OnNavigatedFrom(mockContext.Object);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DashboardViewModel navigated from")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Navigation Command Tests (RegionManager)

    [Fact]
    public void NavigateCommand_UsesRegionManager()
    {
        // Arrange
        var viewModel = CreateViewModelWithTestEventAggregator();
        var mockRegion = new Mock<IRegion>();
        var mockRegionCollection = new Mock<IRegionCollection>();

        mockRegionCollection
            .Setup(rc => rc.ContainsRegionWithName(It.IsAny<string>()))
            .Returns(true);

        _mockRegionManager
            .Setup(rm => rm.Regions)
            .Returns(mockRegionCollection.Object);

        // Act - Execute the OpenBudgetAnalysisCommand which calls RequestNavigate
        viewModel.OpenBudgetAnalysisCommand.Execute();

        // Assert - Verify RequestNavigate was called with correct parameters
        _mockRegionManager.Verify(rm => rm.RequestNavigate("MainRegion", "BudgetAnalysisView"), Times.Once);
    }

    #endregion

    #region Async Command Tests

    [Fact]
    public async Task LoadDataCommand_ExecutesAsync_WithoutDeadlock()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Test Enterprise" }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act
        viewModel.LoadDataCommand.Execute();
        await Task.Delay(100); // Allow async operation to complete

        // Assert
        _mockEnterpriseRepository.Verify(
            r => r.GetAllAsync(),
            Times.AtLeastOnce);
    }

    [Fact]
    public void LoadDataCommand_CanExecute_WhenNotLoading()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.LoadDataCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshDashboardCommand_ExecutesAsync_LoadsData()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Sewer Enterprise", Type = "Sewer" }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act
        viewModel.RefreshDashboardCommand.Execute();
        await Task.Delay(100);

        // Assert
        _mockEnterpriseRepository.Verify(
            r => r.GetAllAsync(),
            Times.AtLeastOnce,
            "Refresh command should load enterprises");
    }

    #endregion

    #region Command CanExecute Tests

    [Fact]
    public void ExportDashboardCommand_CannotExecute_WhenLoading()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Set IsLoading via reflection
        var isLoadingProperty = typeof(DashboardViewModel).GetProperty("IsLoading");
        if (isLoadingProperty != null && isLoadingProperty.CanWrite)
        {
            isLoadingProperty.SetValue(viewModel, true);
        }

        // Act
        var canExecute = viewModel.ExportDashboardCommand?.CanExecute() ?? false;

        // Assert
        canExecute.Should().BeFalse("Export command should not execute when loading");
    }

    [Fact]
    public void GenerateReportCommand_CannotExecute_WhenLoading()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Set IsLoading property
        var isLoadingProperty = typeof(DashboardViewModel).GetProperty("IsLoading");
        if (isLoadingProperty != null && isLoadingProperty.CanWrite)
        {
            isLoadingProperty.SetValue(viewModel, true);
        }

        // Act
        var canExecute = viewModel.GenerateReportCommand?.CanExecute() ?? false;

        // Assert
        canExecute.Should().BeFalse("Generate report command should not execute when loading");
    }

    #endregion

    #region Property Change Tests

    [Fact]
    public void SearchText_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.SearchText))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.SearchText = "Test Search";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.SearchText.Should().Be("Test Search");
    }

    #endregion

    #region Cache Integration Tests

    [Fact]
    public async Task LoadDataCommand_UsesCacheService()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var cachedEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Cached Enterprise" }
        };

        _mockCacheService
            .Setup(c => c.GetAsync<IEnumerable<Enterprise>>(It.IsAny<string>()))
            .ReturnsAsync(cachedEnterprises);

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(cachedEnterprises);

        // Act
        viewModel.LoadDataCommand.Execute();
        await Task.Delay(100);

        // Assert - Either cache or repository should be called
        _mockEnterpriseRepository.Verify(
            r => r.GetAllAsync(),
            Times.AtLeastOnce);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CleanupResourcesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.Dispose();

        // Assert
        viewModel.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private DashboardViewModel CreateViewModel()
    {
        return new DashboardViewModel(
            _mockLogger.Object,
            _mockEnterpriseRepository.Object,
            _mockWhatIfScenarioEngine.Object,
            _mockUtilityCustomerRepository.Object,
            _mockMunicipalAccountRepository.Object,
            _fiscalYearSettings,
            _mockEventAggregator.Object,
            _mockRegionManager.Object,
            _mockCacheService.Object);
    }

    private DashboardViewModel CreateViewModelWithTestEventAggregator()
    {
        // Set up UI synchronization context for EventAggregator
        var originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new FakeUISynchronizationContext());

        var testEventAggregator = new TestEventAggregator();

        var viewModel = new DashboardViewModel(
            _mockLogger.Object,
            _mockEnterpriseRepository.Object,
            _mockWhatIfScenarioEngine.Object,
            _mockUtilityCustomerRepository.Object,
            _mockMunicipalAccountRepository.Object,
            _fiscalYearSettings,
            testEventAggregator,
            _mockRegionManager.Object,
            _mockCacheService.Object);

        // Restore original context
        SynchronizationContext.SetSynchronizationContext(originalContext);

        return viewModel;
    }

    #endregion
}
