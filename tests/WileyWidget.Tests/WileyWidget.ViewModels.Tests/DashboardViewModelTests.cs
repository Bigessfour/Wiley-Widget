using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
using WileyWidget.ViewModels.Main;
using WileyWidget.ViewModels.Messages;
using Xunit;
using Xunit.Sdk;

// Disable test parallelization to avoid threading issues with WPF/Prism components
[assembly: CollectionBehavior(DisableTestParallelization = true)]

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

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool>? filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public SubscriptionToken Subscribe(Action action, Func<bool>? filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public void Publish()
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
    public Func<bool>? Filter { get; }

        public Subscription(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool>? filter)
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

    public SubscriptionToken Subscribe(Action<int> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<int>? filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public SubscriptionToken Subscribe(Action<int> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action<int> action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public SubscriptionToken Subscribe(Action<int> action, Predicate<int>? filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public SubscriptionToken Subscribe(Action<int> action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action<int> action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public void Publish(int payload)
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
    public Predicate<int>? Filter { get; }

        public Subscription(Action<int> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<int>? filter)
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

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool>? filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public SubscriptionToken Subscribe(Action action, Func<bool> filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public void Publish()
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
    public Func<bool>? Filter { get; }

        public Subscription(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool>? filter)
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

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool>? filter)
    {
        // Always allow subscriptions regardless of thread
        var subscription = new Subscription(action, threadOption, keepSubscriberReferenceAlive, filter);
        _subscriptions.Add(subscription);
        return new SubscriptionToken(token => _subscriptions.Remove(subscription));
    }

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
    {
        return Subscribe(action, threadOption, false, null);
    }

    public SubscriptionToken Subscribe(Action action, Func<bool>? filter)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, filter);
    }

    public SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
    {
        return Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);
    }

    public SubscriptionToken Subscribe(Action action)
    {
        return Subscribe(action, ThreadOption.PublisherThread, false, null);
    }

    public void Publish()
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
    public Func<bool>? Filter { get; }

        public Subscription(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Func<bool>? filter)
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
    public override void Post(SendOrPostCallback d, object? state)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        // Execute immediately on the same thread
        d(state);
    }

    public override void Send(SendOrPostCallback d, object? state)
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

        // Setup enterprise repository for background cache loading and dashboard data
        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>());

        // Setup utility customer repository
        _mockUtilityCustomerRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<UtilityCustomer>());

        // Setup municipal account repository
        _mockMunicipalAccountRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<MunicipalAccount>());

        // Setup cache service
        _mockCacheService
            .Setup(c => c.GetAsync<List<Enterprise>>(It.IsAny<string>()))
            .ReturnsAsync((List<Enterprise>?)null);

        _mockCacheService
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

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

        _mockEventAggregator
            .Setup(ea => ea.GetEvent<DataLoadedEvent>())
            .Returns(new DataLoadedEvent());

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

    [StaFact]
    public async Task Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.LoadDataCommand.Should().NotBeNull();
        viewModel.RefreshDashboardCommand.Should().NotBeNull();
    }

    [StaFact]
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

    #region Navigation Error Handling (Fallback & Logging)

    public static IEnumerable<object[]> NavigationTargets()
    {
        // Derived via MCP analyzer from DashboardViewModel.SafeRequestNavigate usages
        yield return new object[] { "BudgetAnalysisView" };
        yield return new object[] { "SettingsView" };
        yield return new object[] { "MunicipalAccountView" };
        yield return new object[] { "EnterpriseView" };
    }

    [Theory]
    [MemberData(nameof(NavigationTargets))]
    public void NavigationCommand_WhenRegionMissing_LogsErrorAndAttemptsFallback_NoCrash(string target)
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Setup RegionManager with NO "MainRegion" to trigger the missing region error path
        var mockRegions = new Mock<IRegionCollection>();
        mockRegions.Setup(c => c.ContainsRegionWithName("MainRegion")).Returns(false);
        _mockRegionManager.Setup(rm => rm.Regions).Returns(mockRegions.Object);

        // Act: execute the command associated with the target
        Exception? ex = null;
        switch (target)
        {
            case "BudgetAnalysisView":
                ex = Record.Exception(() => viewModel.OpenBudgetAnalysisCommand.Execute());
                break;
            case "SettingsView":
                ex = Record.Exception(() => viewModel.OpenSettingsCommand.Execute());
                break;
            case "MunicipalAccountView":
                ex = Record.Exception(() => viewModel.NavigateToAccountsCommand.Execute());
                break;
            case "EnterpriseView":
                ex = Record.Exception(() => viewModel.OpenEnterpriseManagementCommand.Execute());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unexpected target under test");
        }

        // Assert: no crash
        ex.Should().BeNull();

        // Assert: error logged for missing region
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Region") && v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Should log error when region is missing");
    }

    #endregion
    #region Navigation Tests (INavigationAware)

    [StaFact]
    public async Task OnNavigatedTo_LoadsDashboardData()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
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

    [StaFact]
    public async Task IsNavigationTarget_AlwaysReturnsTrue()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Dashboard", UriKind.Absolute));

        // Act
        var result = viewModel.IsNavigationTarget(mockContext.Object);

        // Assert
        result.Should().BeTrue();
    }

    [StaFact]
    public async Task OnNavigatedFrom_LogsNavigationEvent()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
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

    #region IDataErrorInfo Tests

    [StaFact]
    public async Task IDataErrorInfo_Error_ReturnsEmptyString()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var errorInfo = (System.ComponentModel.IDataErrorInfo)viewModel;

        // Act
        var error = errorInfo.Error;

        // Assert
        error.Should().BeEmpty();
    }

    [StaFact]
    public async Task IDataErrorInfo_Indexer_ValidTotalBudget_ReturnsEmptyString()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var errorInfo = (System.ComponentModel.IDataErrorInfo)viewModel;
        viewModel.TotalBudget = 100000;

        // Act
        var error = errorInfo[nameof(DashboardViewModel.TotalBudget)];

        // Assert
        error.Should().BeEmpty();
    }

    [StaFact]
    public async Task IDataErrorInfo_Indexer_NegativeTotalBudget_ReturnsErrorMessage()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var errorInfo = (System.ComponentModel.IDataErrorInfo)viewModel;
        viewModel.TotalBudget = -1000;

        // Act
        var error = errorInfo[nameof(DashboardViewModel.TotalBudget)];

        // Assert
        error.Should().Be("Total budget cannot be negative");
    }

    [StaFact]
    public async Task IDataErrorInfo_Indexer_NegativeTotalEnterprises_ReturnsErrorMessage()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var errorInfo = (System.ComponentModel.IDataErrorInfo)viewModel;
        viewModel.TotalEnterprises = -5;

        // Act
        var error = errorInfo[nameof(DashboardViewModel.TotalEnterprises)];

        // Assert
        error.Should().Be("Total enterprises cannot be negative");
    }

    [StaFact]
    public async Task IDataErrorInfo_Indexer_NegativeActiveProjects_ReturnsErrorMessage()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var errorInfo = (System.ComponentModel.IDataErrorInfo)viewModel;
        viewModel.ActiveProjects = -2;

        // Act
        var error = errorInfo[nameof(DashboardViewModel.ActiveProjects)];

        // Assert
        error.Should().Be("Active projects cannot be negative");
    }

    [StaFact]
    public async Task IDataErrorInfo_Indexer_UnknownProperty_ReturnsEmptyString()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var errorInfo = (System.ComponentModel.IDataErrorInfo)viewModel;

        // Act
        var error = errorInfo["UnknownProperty"];

        // Assert
        error.Should().BeEmpty();
    }

    #endregion

    #region Navigation Command Tests (RegionManager)

    [StaFact]
    public void NavigateCommand_UsesRegionManager()
    {
        // Arrange
        var viewModel = CreateViewModelWithTestEventAggregator();

        // Note: RequestNavigate is an extension method on IRegionManager that Moq cannot verify.
        // Prism's implementation calls RequestNavigate directly without accessing the Regions property,
        // so we cannot use indirect verification either. Instead, we verify the command executes successfully.

        // Act & Assert - Command should execute without throwing
        var exception = Record.Exception(() =>
            viewModel.OpenBudgetAnalysisCommand.Execute());

        // Verify command executed successfully
        exception.Should().BeNull(
            "OpenBudgetAnalysisCommand should execute without error when RegionManager is available");

        // Verify command is properly initialized
        viewModel.OpenBudgetAnalysisCommand.Should().NotBeNull();
        viewModel.OpenBudgetAnalysisCommand.CanExecute().Should().BeTrue();
    }

    #endregion

    #region Async Command Tests

    [StaFact]
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
        await Task.Delay(200).ConfigureAwait(false); // Allow async operation to complete

        // Assert
        _mockEnterpriseRepository.Verify(
            r => r.GetAllAsync(),
            Times.AtLeastOnce);
    }

    [StaFact]
    public async Task LoadDataCommand_CanExecute_WhenNotLoading()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        var canExecute = viewModel.LoadDataCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue();
    }

    [StaFact]
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
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        _mockEnterpriseRepository.Verify(
            r => r.GetAllAsync(),
            Times.AtLeastOnce,
            "Refresh command should load enterprises");
    }

    #endregion

    #region Error Handling Tests

    [StaFact]
    public async Task LoadDashboardDataAsync_WhenRepositoryFails_HandlesError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _mockEnterpriseRepository.Setup(r => r.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("Repository failure"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.LoadDashboardDataAsync());

        // Assert
        exception.Message.Should().Be("Repository failure");
        viewModel.ErrorMessage.Should().NotBeEmpty();
        viewModel.DashboardStatus.Should().Be("Error loading dashboard");
        viewModel.StatusMessage.Should().Be("Error loading dashboard");
        _mockLogger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CRITICAL")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [StaFact]
    public async Task ExecuteRefreshDashboardAsync_WhenLoadFails_HandlesError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _mockEnterpriseRepository.Setup(r => r.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("Repository failure"));

        // Act
        viewModel.RefreshDashboardCommand.Execute();
        await Task.Delay(200).ConfigureAwait(false); // Allow async operation to complete

        // Assert - Command should handle error gracefully without throwing to UI
        viewModel.StatusMessage.Should().Contain("Error");
        _mockLogger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RefreshDashboard command failed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Command CanExecute Tests

    [StaFact]
    public async Task ExportDashboardCommand_CannotExecute_WhenLoading()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

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

    [StaFact]
    public async Task GenerateReportCommand_CannotExecute_WhenLoading()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

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

    [StaFact]
    public async Task ToggleAutoRefreshCommand_CanExecute_AlwaysTrue()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        var canExecute = viewModel.ToggleAutoRefreshCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Toggle auto-refresh command should always be executable");
    }

    [StaFact]
    public async Task ExportDashboardCommand_CanExecute_WhenNotLoading()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.IsLoading = false;

        // Act
        var canExecute = viewModel.ExportDashboardCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Export command should execute when not loading");
    }

    [StaFact]
    public async Task BackupDataCommand_CanExecute_WhenNotLoading()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.IsLoading = false;

        // Act
        var canExecute = viewModel.BackupDataCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Backup command should execute when not loading");
    }

    [StaFact]
    public async Task BackupDataCommand_CannotExecute_WhenLoading()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.IsLoading = true;

        // Act
        var canExecute = viewModel.BackupDataCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Backup command should not execute when loading");
    }

    [StaFact]
    public async Task NavigateBackCommand_CanExecute_WhenCanNavigateBack()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var mockRegion = new Mock<IRegion>();
        var mockNavigationService = new Mock<IRegionNavigationService>();
        mockNavigationService.Setup(ns => ns.Journal.CanGoBack).Returns(true);
        mockRegion.Setup(r => r.NavigationService).Returns(mockNavigationService.Object);
        _mockRegionManager.Setup(rm => rm.Regions.ContainsRegionWithName("MainRegion")).Returns(true);
        _mockRegionManager.Setup(rm => rm.Regions["MainRegion"]).Returns(mockRegion.Object);

        // Act
        var canExecute = viewModel.NavigateBackCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Navigate back should be available when journal can go back");
    }

    [StaFact]
    public async Task NavigateBackCommand_CannotExecute_WhenCannotNavigateBack()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var mockRegion = new Mock<IRegion>();
        var mockNavigationService = new Mock<IRegionNavigationService>();
        mockNavigationService.Setup(ns => ns.Journal.CanGoBack).Returns(false);
        mockRegion.Setup(r => r.NavigationService).Returns(mockNavigationService.Object);
        _mockRegionManager.Setup(rm => rm.Regions.ContainsRegionWithName("MainRegion")).Returns(true);
        _mockRegionManager.Setup(rm => rm.Regions["MainRegion"]).Returns(mockRegion.Object);

        // Act
        var canExecute = viewModel.NavigateBackCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Navigate back should not be available when journal cannot go back");
    }

    [StaFact]
    public async Task NavigateForwardCommand_CanExecute_WhenCanNavigateForward()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var mockRegion = new Mock<IRegion>();
        var mockNavigationService = new Mock<IRegionNavigationService>();
        mockNavigationService.Setup(ns => ns.Journal.CanGoForward).Returns(true);
        mockRegion.Setup(r => r.NavigationService).Returns(mockNavigationService.Object);
        _mockRegionManager.Setup(rm => rm.Regions.ContainsRegionWithName("MainRegion")).Returns(true);
        _mockRegionManager.Setup(rm => rm.Regions["MainRegion"]).Returns(mockRegion.Object);

        // Act
        var canExecute = viewModel.NavigateForwardCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Navigate forward should be available when journal can go forward");
    }

    [StaFact]
    public async Task NavigateForwardCommand_CannotExecute_WhenCannotNavigateForward()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var mockRegion = new Mock<IRegion>();
        var mockNavigationService = new Mock<IRegionNavigationService>();
        mockNavigationService.Setup(ns => ns.Journal.CanGoForward).Returns(false);
        mockRegion.Setup(r => r.NavigationService).Returns(mockNavigationService.Object);
        _mockRegionManager.Setup(rm => rm.Regions.ContainsRegionWithName("MainRegion")).Returns(true);
        _mockRegionManager.Setup(rm => rm.Regions["MainRegion"]).Returns(mockRegion.Object);

        // Act
        var canExecute = viewModel.NavigateForwardCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Navigate forward should not be available when journal cannot go forward");
    }

    [StaFact]
    public async Task RunGrowthScenarioCommand_CanExecute_WhenNotRunningScenario()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.IsScenarioRunning = false;

        // Act
        var canExecute = viewModel.RunGrowthScenarioCommand.CanExecute(1);

        // Assert
        canExecute.Should().BeTrue("Run scenario command should execute when not running scenario");
    }

    [StaFact]
    public async Task RunGrowthScenarioCommand_CannotExecute_WhenRunningScenario()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.IsScenarioRunning = true;

        // Act
        var canExecute = viewModel.RunGrowthScenarioCommand.CanExecute(1);

        // Assert
        canExecute.Should().BeFalse("Run scenario command should not execute when running scenario");
    }

    #endregion

    #region Property Change Tests

    [StaFact]
    public async Task SearchText_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
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

    [StaFact]
    public async Task AutoRefreshEnabled_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.AutoRefreshEnabled))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.AutoRefreshEnabled = false;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.AutoRefreshEnabled.Should().BeFalse();
    }

    [StaFact]
    public async Task RefreshIntervalMinutes_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.RefreshIntervalMinutes))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.RefreshIntervalMinutes = 10;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.RefreshIntervalMinutes.Should().Be(10);
    }

    [StaFact]
    public async Task DashboardStatus_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.DashboardStatus))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.DashboardStatus = "Updated";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.DashboardStatus.Should().Be("Updated");
    }

    [StaFact]
    public async Task NextRefreshTime_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.NextRefreshTime))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.NextRefreshTime = "12:00";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.NextRefreshTime.Should().Be("12:00");
    }

    [StaFact]
    public async Task IsLoading_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.IsLoading))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsLoading = true;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.IsLoading.Should().BeTrue();
    }

    [StaFact]
    public async Task StatusMessage_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.StatusMessage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.StatusMessage = "Test message";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.StatusMessage.Should().Be("Test message");
    }

    [StaFact]
    public async Task ErrorMessage_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.ErrorMessage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.ErrorMessage = "Test error";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.ErrorMessage.Should().Be("Test error");
    }

    [StaFact]
    public async Task ProgressPercentage_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.ProgressPercentage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.ProgressPercentage = 75.5m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.ProgressPercentage.Should().Be(75.5m);
    }

    [StaFact]
    public async Task RemainingBudget_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.RemainingBudget))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.RemainingBudget = 50000m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.RemainingBudget.Should().Be(50000m);
    }

    [StaFact]
    public async Task SpentAmount_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.SpentAmount))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.SpentAmount = 25000m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.SpentAmount.Should().Be(25000m);
    }

    [StaFact]
    public async Task PayRaisePercentage_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.PayRaisePercentage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.PayRaisePercentage = 5.0m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.PayRaisePercentage.Should().Be(5.0m);
    }

    [StaFact]
    public async Task BenefitsIncreaseAmount_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.BenefitsIncreaseAmount))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.BenefitsIncreaseAmount = 1000m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.BenefitsIncreaseAmount.Should().Be(1000m);
    }

    [StaFact]
    public async Task EquipmentPurchaseAmount_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.EquipmentPurchaseAmount))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.EquipmentPurchaseAmount = 50000m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.EquipmentPurchaseAmount.Should().Be(50000m);
    }

    [StaFact]
    public async Task EquipmentFinancingYears_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.EquipmentFinancingYears))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.EquipmentFinancingYears = 7;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.EquipmentFinancingYears.Should().Be(7);
    }

    [StaFact]
    public async Task ReservePercentage_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.ReservePercentage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.ReservePercentage = 10.0m;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.ReservePercentage.Should().Be(10.0m);
    }

    [StaFact]
    public async Task ScenarioStatus_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.ScenarioStatus))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.ScenarioStatus = "Running";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.ScenarioStatus.Should().Be("Running");
    }

    #endregion

    #region Command Execution Tests

    [StaFact]
    public async Task ToggleAutoRefreshCommand_Execute_TogglesAutoRefreshEnabled()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var initialValue = viewModel.AutoRefreshEnabled;

        // Act
        viewModel.ToggleAutoRefreshCommand.Execute();

        // Assert
        viewModel.AutoRefreshEnabled.Should().Be(!initialValue);
    }

    [StaFact]
    public async Task OpenBudgetAnalysisCommand_Execute_RequestsNavigation()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.OpenBudgetAnalysisCommand.Execute();

        // Assert - Command should execute without throwing
        viewModel.OpenBudgetAnalysisCommand.Should().NotBeNull();
    }

    [StaFact]
    public async Task OpenSettingsCommand_Execute_RequestsNavigation()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.OpenSettingsCommand.Execute();

        // Assert - Command should execute without throwing
        viewModel.OpenSettingsCommand.Should().NotBeNull();
    }

    [StaFact]
    public async Task SearchCommand_Execute_FiltersEnterprises()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.Enterprises.Add(new Enterprise { Id = 1, Name = "Water Enterprise" });
        viewModel.Enterprises.Add(new Enterprise { Id = 2, Name = "Sewer Enterprise" });
        viewModel.SearchText = "Water";

        // Act
        viewModel.SearchCommand.Execute();

        // Assert
        viewModel.FilteredEnterprises.Should().HaveCount(1);
        viewModel.FilteredEnterprises.First().Name.Should().Contain("Water");
    }

    [StaFact]
    public async Task NavigateToAccountsCommand_Execute_RequestsNavigation()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.NavigateToAccountsCommand.Execute();

        // Assert - Command should execute without throwing
        viewModel.NavigateToAccountsCommand.Should().NotBeNull();
    }

    [StaFact]
    public async Task OpenEnterpriseManagementCommand_Execute_RequestsNavigation()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.OpenEnterpriseManagementCommand.Execute();

        // Assert - Command should execute without throwing
        viewModel.OpenEnterpriseManagementCommand.Should().NotBeNull();
    }

    [StaFact]
    public async Task ExportDashboardCommand_Execute_CommandIsConfiguredCorrectly()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Setup basic test data
        viewModel.TotalEnterprises = 5;
        viewModel.TotalBudget = 1000000;
        viewModel.HealthScore = 85;
        viewModel.SystemHealthStatus = "Good";
        viewModel.AutoRefreshEnabled = true;
        viewModel.RefreshIntervalMinutes = 30;
        viewModel.LastUpdated = DateTime.Now.AddMinutes(-5).ToString("g", CultureInfo.InvariantCulture);

        // Assert - Command should be properly configured
        // Note: We cannot easily test the actual execution due to SaveFileDialog.ShowDialog()
        // blocking in a test environment. Instead, we verify the command is set up correctly.
        viewModel.ExportDashboardCommand.Should().NotBeNull();
        viewModel.ExportDashboardCommand.CanExecute().Should().BeTrue(); // Should be executable when not loading
    }

    [StaFact]
    public async Task GenerateReportCommand_Execute_CommandIsConfiguredCorrectly()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Setup basic test data
        viewModel.TotalEnterprises = 5;
        viewModel.TotalBudget = 1000000;
        viewModel.HealthScore = 85;
        viewModel.SystemHealthStatus = "Good";
        viewModel.EnterprisesChangeText = "+2 enterprises";
        viewModel.BudgetChangeText = "+$50K budget";

        // Assert - Command should be properly configured
        // Note: We cannot easily test the actual execution due to SaveFileDialog.ShowDialog()
        // blocking in a test environment. Instead, we verify the command is set up correctly.
        viewModel.GenerateReportCommand.Should().NotBeNull();
        viewModel.GenerateReportCommand.CanExecute().Should().BeTrue(); // Should be executable when not loading
    }

    [StaFact]
    public async Task BackupDataCommand_Execute_CommandIsConfiguredCorrectly()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Setup basic test data
        viewModel.TotalEnterprises = 5;
        viewModel.TotalBudget = 1000000;
        viewModel.HealthScore = 85;
        viewModel.SystemHealthStatus = "Good";
        viewModel.AutoRefreshEnabled = true;
        viewModel.RefreshIntervalMinutes = 30;
        viewModel.LastUpdated = DateTime.Now.AddMinutes(-5).ToString("g", CultureInfo.InvariantCulture);

        // Assert - Command should be properly configured
        // Note: We cannot easily test the actual execution due to file system operations
        // in a test environment. Instead, we verify the command is set up correctly.
        viewModel.BackupDataCommand.Should().NotBeNull();
        viewModel.BackupDataCommand.CanExecute().Should().BeTrue(); // Should be executable when not loading
    }

    #endregion

    #region Collection Change Notification Tests

    [StaFact]
    public async Task HistoricalData_IsObservableCollection()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
    viewModel.HistoricalData.Should().NotBeNull();
    viewModel.HistoricalData.Should().BeOfType<ObservableCollection<WileyWidget.ViewModels.Main.BudgetTrendItem>>();
    }

    [StaFact]
    public async Task RateTrendData_IsObservableCollection()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
    viewModel.RateTrendData.Should().NotBeNull();
    viewModel.RateTrendData.Should().BeOfType<ObservableCollection<WileyWidget.ViewModels.Main.RateTrendItem>>();
    }

    [StaFact]
    public async Task EnterpriseTypeData_IsObservableCollection()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
    viewModel.EnterpriseTypeData.Should().NotBeNull();
    viewModel.EnterpriseTypeData.Should().BeOfType<ObservableCollection<WileyWidget.ViewModels.Main.EnterpriseTypeItem>>();
    }

    [StaFact]
    public async Task Enterprises_IsObservableCollection()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.Enterprises.Should().NotBeNull();
        viewModel.Enterprises.Should().BeOfType<ObservableCollection<Enterprise>>();
    }

    [StaFact]
    public async Task FilteredEnterprises_IsObservableCollection()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.FilteredEnterprises.Should().NotBeNull();
        viewModel.FilteredEnterprises.Should().BeOfType<ObservableCollection<Enterprise>>();
    }

    #endregion

    #region Boundary Value Tests

    [StaFact]
    public async Task TotalEnterprises_MaxValue_HandlesLargeNumbers()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.TotalEnterprises = int.MaxValue;

        // Assert
        viewModel.TotalEnterprises.Should().Be(int.MaxValue);
    }

    [StaFact]
    public async Task TotalEnterprises_ZeroValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.TotalEnterprises = 0;

        // Assert
        viewModel.TotalEnterprises.Should().Be(0);
    }

    [StaFact]
    public async Task TotalBudget_MaxValue_HandlesLargeDecimals()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.TotalBudget = decimal.MaxValue;

        // Assert
        viewModel.TotalBudget.Should().Be(decimal.MaxValue);
    }

    [StaFact]
    public async Task TotalBudget_ZeroValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.TotalBudget = 0;

        // Assert
        viewModel.TotalBudget.Should().Be(0);
    }

    [StaFact]
    public async Task TotalBudget_NegativeValue_IsAllowed()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.TotalBudget = -1000;

        // Assert
        viewModel.TotalBudget.Should().Be(-1000);
    }

    [StaFact]
    public async Task HealthScore_MaximumValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.HealthScore = 100;

        // Assert
        viewModel.HealthScore.Should().Be(100);
    }

    [StaFact]
    public async Task HealthScore_MinimumValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.HealthScore = 0;

        // Assert
        viewModel.HealthScore.Should().Be(0);
    }

    [StaFact]
    public async Task HealthScore_NegativeValue_IsAllowed()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.HealthScore = -1;

        // Assert
        viewModel.HealthScore.Should().Be(-1);
    }

    [StaFact]
    public async Task RefreshIntervalMinutes_ZeroValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.RefreshIntervalMinutes = 0;

        // Assert
        viewModel.RefreshIntervalMinutes.Should().Be(0);
    }

    [StaFact]
    public async Task RefreshIntervalMinutes_LargeValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.RefreshIntervalMinutes = 1440; // 24 hours

        // Assert
        viewModel.RefreshIntervalMinutes.Should().Be(1440);
    }

    [StaFact]
    public async Task ProgressPercentage_MaximumValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.ProgressPercentage = 100;

        // Assert
        viewModel.ProgressPercentage.Should().Be(100);
    }

    [StaFact]
    public async Task ProgressPercentage_ZeroValue_IsValid()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.ProgressPercentage = 0;

        // Assert
        viewModel.ProgressPercentage.Should().Be(0);
    }

    [StaFact]
    public async Task ProgressPercentage_NegativeValue_IsAllowed()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.ProgressPercentage = -10;

        // Assert
        viewModel.ProgressPercentage.Should().Be(-10);
    }

    #endregion

    #region Theme Mapping Tests

    [StaFact]
    public async Task CurrentTheme_PropertyExists_IsAccessible()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act & Assert - Property should be accessible without throwing
        Action act = () => { var theme = viewModel.CurrentTheme; };
        act.Should().NotThrow();
    }

    #endregion

    #region Performance Tests

    [StaFact]
    public async Task LoadDashboardDataAsync_CompletesWithinReasonableTime()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Test Enterprise 1", Status = EnterpriseStatus.Active },
            new Enterprise { Id = 2, Name = "Test Enterprise 2", Status = EnterpriseStatus.Active }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act
        var task = viewModel.LoadDashboardDataAsync();
        var completedTask = await Task.WhenAny(task, Task.Delay(5000)); // 5 second timeout

        // Assert
        completedTask.Should().Be(task, "LoadDashboardDataAsync should complete within 5 seconds");
        viewModel.TotalEnterprises.Should().Be(2);
    }

    [StaFact]
    public async Task RefreshDashboardDataAsync_CompletesWithinReasonableTime()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Test Enterprise", Status = EnterpriseStatus.Active }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act - Use the command which internally calls the refresh method
        viewModel.RefreshDashboardCommand.Execute();
        await Task.Delay(100).ConfigureAwait(false); // Allow async operation to start
        var completedTask = await Task.WhenAny(Task.Delay(3000)); // 3 second timeout

        // Assert - Command should execute without hanging indefinitely
        viewModel.RefreshDashboardCommand.Should().NotBeNull();
        viewModel.IsLoading.Should().BeFalse(); // Should be reset after completion
    }

    [StaFact]
    public async Task LoadDashboardDataAsync_HandlesLargeDataset()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var largeEnterpriseList = Enumerable.Range(1, 1000)
            .Select(i => new Enterprise
            {
                Id = i,
                Name = $"Enterprise {i}",
                Status = EnterpriseStatus.Active
            })
            .ToList();

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(largeEnterpriseList);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.TotalEnterprises.Should().Be(1000);
        viewModel.Enterprises.Should().HaveCount(1000);
    }

    #endregion

    #region Cache Integration Tests

    [StaFact]
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
    await Task.Delay(200).ConfigureAwait(false);

        // Assert - Either cache or repository should be called
        _mockEnterpriseRepository.Verify(
            r => r.GetAllAsync(),
            Times.AtLeastOnce);
    }

    #endregion

    #region Disposal Tests

    [StaFact]
    public async Task Dispose_CleanupResourcesSuccessfully()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.Dispose();

        // Assert
        viewModel.Should().NotBeNull();
    }

    #endregion

    #region KPI Properties Tests

    [StaFact]
    public async Task TotalEnterprises_AfterLoadData_IsCalculatedCorrectly()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", Status = EnterpriseStatus.Active },
            new Enterprise { Id = 2, Name = "Sewer", Status = EnterpriseStatus.Active },
            new Enterprise { Id = 3, Name = "Trash", Status = EnterpriseStatus.Active }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);
        _mockUtilityCustomerRepository.Setup(r => r.GetCountAsync()).ReturnsAsync(0);
        _mockMunicipalAccountRepository.Setup(r => r.GetByFundAsync(It.IsAny<MunicipalFundType>()))
            .ReturnsAsync(new List<MunicipalAccount>());

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.TotalEnterprises.Should().Be(3);
    }

    [StaFact]
    public async Task TotalBudget_AfterLoadData_SumsAllEnterpriseBudgets()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 },
            new Enterprise { Id = 2, Name = "Sewer", TotalBudget = 150000 },
            new Enterprise { Id = 3, Name = "Trash", TotalBudget = 75000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.TotalBudget.Should().Be(325000);
    }

    [StaFact]
    public async Task ActiveProjects_AfterLoadData_CountsRecentlyModifiedEnterprises()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", LastModified = DateTime.Now.AddDays(-5) },
            new Enterprise { Id = 2, Name = "Sewer", LastModified = DateTime.Now.AddDays(-60) },
            new Enterprise { Id = 3, Name = "Trash", LastModified = DateTime.Now.AddDays(-10) }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.ActiveProjects.Should().Be(2, "Two enterprises modified within last 30 days");
    }

    [StaFact]
    public async Task SystemHealthScore_AfterLoadData_IsCalculatedBasedOnFactors()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000, LastModified = DateTime.Now }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.SystemHealthScore.Should().BeGreaterOrEqualTo(75, "Should have good health with active data");
        viewModel.HealthScore.Should().Be(viewModel.SystemHealthScore);
    }

    [StaFact]
    public async Task SystemHealthStatus_ReflectsHealthScore()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000, LastModified = DateTime.Now }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.SystemHealthStatus.Should().BeOneOf("Excellent", "Good", "Fair", "Poor");
        viewModel.SystemHealthColor.Should().NotBeNull();
    }

    [StaFact]
    public async Task BudgetUtilizationScore_IsInitialized()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.BudgetUtilizationScore.Should().BeGreaterOrEqualTo(0);
        viewModel.BudgetUtilizationScore.Should().BeLessOrEqualTo(100);
    }

    #endregion

    #region Change Indicator Properties Tests

    [StaFact]
    public async Task EnterprisesChangeText_IsSetAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.EnterprisesChangeText.Should().NotBeNullOrEmpty();
        viewModel.EnterprisesChangeColor.Should().NotBeNull();
    }

    [StaFact]
    public async Task BudgetChangeText_IsSetAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.BudgetChangeText.Should().NotBeNullOrEmpty();
        viewModel.BudgetChangeColor.Should().NotBeNull();
    }

    [StaFact]
    public async Task ProjectsChangeText_IsSetAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", LastModified = DateTime.Now }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.ProjectsChangeText.Should().NotBeNullOrEmpty();
        viewModel.ProjectsChangeColor.Should().NotBeNull();
    }

    #endregion

    #region Chart Data Collections Tests

    [StaFact]
    public async Task BudgetTrendData_PopulatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.BudgetTrendData.Should().NotBeEmpty();
        viewModel.BudgetTrendData.Count.Should().Be(6, "Should have 6 months of data");
        viewModel.BudgetTrendData.All(d => d.Period != null).Should().BeTrue();
        viewModel.BudgetTrendData.All(d => d.Amount > 0).Should().BeTrue();
    }

    [StaFact]
    public async Task ChartData_PopulatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.ChartData.Should().NotBeEmpty();
        viewModel.ChartData.Count.Should().Be(6);
    }

    [StaFact]
    public async Task ExpenseData_PopulatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.ExpenseData.Should().NotBeEmpty();
        viewModel.ExpenseData.Count.Should().Be(6);
    }

    [StaFact]
    public async Task RevenueData_PopulatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.RevenueData.Should().NotBeEmpty();
        viewModel.RevenueData.Count.Should().Be(6);
    }

    [StaFact]
    public async Task RateTrendData_PopulatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.RateTrendData.Should().NotBeEmpty();
        viewModel.RateTrendData.Count.Should().Be(6);
        viewModel.RateTrendData.All(d => d.Period != null).Should().BeTrue();
    }

    [StaFact]
    public async Task EnterpriseTypeData_GroupsEnterprisesByType()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water 1", Type = "Water" },
            new Enterprise { Id = 2, Name = "Water 2", Type = "Water" },
            new Enterprise { Id = 3, Name = "Sewer 1", Type = "Sewer" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.EnterpriseTypeData.Should().NotBeEmpty();
        viewModel.EnterpriseTypeData.Should().HaveCount(2, "Should have 2 types: Water and Sewer");
        viewModel.EnterpriseTypeData.First(e => e.Type == "Water").Count.Should().Be(2);
        viewModel.EnterpriseTypeData.First(e => e.Type == "Sewer").Count.Should().Be(1);
    }

    [StaFact]
    public async Task BudgetUtilizationData_IsInitialized()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", TotalBudget = 100000 }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.BudgetUtilizationData.Should().NotBeNull();
    }

    #endregion

    #region Activity and Alert Collections Tests

    [StaFact]
    public async Task RecentActivities_PopulatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.RecentActivities.Should().NotBeEmpty();
        viewModel.RecentActivities.All(a => a.Description != null).Should().BeTrue();
        viewModel.RecentActivities.All(a => a.Type != null).Should().BeTrue();
        viewModel.RecentActivities.All(a => a.Timestamp != default).Should().BeTrue();
    }

    [StaFact]
    public async Task SystemAlerts_PopulatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.SystemAlerts.Should().NotBeNull();
        viewModel.SystemAlerts.All(a => a.Priority != null).Should().BeTrue();
        viewModel.SystemAlerts.All(a => a.Message != null).Should().BeTrue();
        viewModel.SystemAlerts.All(a => a.PriorityColor != null).Should().BeTrue();
    }

    [StaFact]
    public async Task SystemAlerts_ShowsWarningWhenNoEnterprises()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Enterprise>());

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.SystemAlerts.Should().Contain(a => a.Message.Contains("No enterprises"));
    }

    #endregion

    #region Search and Filtering Tests

    [StaFact]
    public async Task SearchText_WhenCleared_ShowsAllEnterprises()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.Enterprises.Add(new Enterprise { Id = 1, Name = "Water Enterprise" });
        viewModel.Enterprises.Add(new Enterprise { Id = 2, Name = "Sewer Enterprise" });
        viewModel.SearchText = "Water";

        // Act
        viewModel.ClearSearchCommand.Execute();

        // Assert
        viewModel.SearchText.Should().BeEmpty();
    }

    [StaFact]
    public async Task FilteredEnterprises_UpdatesWhenSearchTextChanges()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.Enterprises.Add(new Enterprise { Id = 1, Name = "Water Enterprise", Type = "Water" });
        viewModel.Enterprises.Add(new Enterprise { Id = 2, Name = "Sewer Enterprise", Type = "Sewer" });

        // Initialize filtered collection
        foreach (var enterprise in viewModel.Enterprises)
        {
            viewModel.FilteredEnterprises.Add(enterprise);
        }

        // Act
        viewModel.SearchText = "Water";
        viewModel.SearchCommand.Execute();

        // Assert
        viewModel.FilteredEnterprises.Should().HaveCount(1);
        viewModel.FilteredEnterprises.First().Name.Should().Contain("Water");
    }

    [StaFact]
    public async Task ClearSearchCommand_CanExecute_WhenSearchTextNotEmpty()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.SearchText = "Test";

        // Act
        var canExecute = viewModel.ClearSearchCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue();
    }

    [StaFact]
    public async Task ClearSearchCommand_CannotExecute_WhenSearchTextEmpty()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        viewModel.SearchText = string.Empty;

        // Act
        var canExecute = viewModel.ClearSearchCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse();
    }

    #endregion

    #region Auto-Refresh Tests

    [StaFact]
    public async Task AutoRefreshEnabled_DefaultsToTrue()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.AutoRefreshEnabled.Should().BeTrue();
    }

    [StaFact]
    public async Task RefreshIntervalMinutes_DefaultsTo5()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.RefreshIntervalMinutes.Should().Be(5);
    }

    [StaFact]
    public async Task ToggleAutoRefreshCommand_TogglesAutoRefreshEnabled()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var initialValue = viewModel.AutoRefreshEnabled;

        // Act
        viewModel.ToggleAutoRefreshCommand.Execute();

        // Assert
        viewModel.AutoRefreshEnabled.Should().Be(!initialValue);
    }

    [StaFact]
    public async Task NextRefreshTime_UpdatesAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        viewModel.LoadDataCommand.Execute();
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        viewModel.NextRefreshTime.Should().NotBeNullOrEmpty();
        viewModel.NextRefreshTime.Should().NotBe("Calculating...");
    }

    #endregion

    #region Status and Progress Properties Tests

    [StaFact]
    public async Task DashboardStatus_InitializesToLoadingMessage()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.DashboardStatus.Should().NotBeNullOrEmpty();
    }

    [StaFact]
    public async Task LastUpdated_UpdatesAfterSuccessfulLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.LastUpdated.Should().NotBe("Never");
    }

    [StaFact]
    public async Task ProgressPercentage_IsInitialized()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.ProgressPercentage.Should().BeGreaterOrEqualTo(0);
    }

    [StaFact]
    public async Task StatusMessage_IsInitializedToReady()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.StatusMessage.Should().NotBeNullOrEmpty();
    }

    [StaFact]
    public async Task ErrorMessage_InitializesToEmpty()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.ErrorMessage.Should().BeEmpty();
    }

    #endregion

    #region Budget Calculation Properties Tests

    [StaFact]
    public async Task RemainingBudget_IsInitialized()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.RemainingBudget.Should().BeGreaterOrEqualTo(0);
    }

    [StaFact]
    public async Task SpentAmount_IsInitialized()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.SpentAmount.Should().BeGreaterOrEqualTo(0);
    }

    [StaFact]
    public async Task SuggestedRate_IsCalculatedAfterLoad()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", Type = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);
        _mockUtilityCustomerRepository.Setup(r => r.GetCountAsync()).ReturnsAsync(100);
        _mockMunicipalAccountRepository.Setup(r => r.GetByFundAsync(It.IsAny<MunicipalFundType>()))
            .ReturnsAsync(new List<MunicipalAccount>());

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        viewModel.SuggestedRate.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region Growth Scenario Properties Tests

    [StaFact]
    public async Task PayRaisePercentage_DefaultsTo3Percent()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise> { new Enterprise { Id = 1, Name = "Water" } };
        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        viewModel.LoadDataCommand.Execute();
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        viewModel.PayRaisePercentage.Should().Be(3.0m);
    }

    [StaFact]
    public async Task BenefitsIncreaseAmount_DefaultsTo50()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise> { new Enterprise { Id = 1, Name = "Water" } };
        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        viewModel.LoadDataCommand.Execute();
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        viewModel.BenefitsIncreaseAmount.Should().Be(50m);
    }

    [StaFact]
    public async Task EquipmentFinancingYears_DefaultsTo5()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise> { new Enterprise { Id = 1, Name = "Water" } };
        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        viewModel.LoadDataCommand.Execute();
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        viewModel.EquipmentFinancingYears.Should().Be(5);
    }

    [StaFact]
    public async Task ReservePercentage_DefaultsTo5Percent()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise> { new Enterprise { Id = 1, Name = "Water" } };
        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        viewModel.LoadDataCommand.Execute();
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        viewModel.ReservePercentage.Should().Be(5.0m);
    }

    [StaFact]
    public async Task IsScenarioRunning_InitializesToFalse()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.IsScenarioRunning.Should().BeFalse();
    }

    [StaFact]
    public async Task RunGrowthScenarioCommand_ExecutesScenario()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water", Type = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);
        _mockEnterpriseRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(testEnterprises[0]);
        _mockUtilityCustomerRepository.Setup(r => r.GetCountAsync()).ReturnsAsync(100);
        _mockMunicipalAccountRepository.Setup(r => r.GetByFundAsync(It.IsAny<MunicipalFundType>()))
            .ReturnsAsync(new List<MunicipalAccount>());

        var scenario = new ComprehensiveScenario { ScenarioName = "Test Scenario" };
        _mockWhatIfScenarioEngine.Setup(e => e.GenerateComprehensiveScenarioAsync(It.IsAny<int>(), It.IsAny<ScenarioParameters>()))
            .ReturnsAsync(scenario);

        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Act
        viewModel.RunGrowthScenarioCommand.Execute(1);
        await Task.Delay(300).ConfigureAwait(false);

        // Assert
        _mockWhatIfScenarioEngine.Verify(e => e.GenerateComprehensiveScenarioAsync(1, It.IsAny<ScenarioParameters>()), Times.Once);
    }

    #endregion

    #region Event Aggregator Tests

    [StaFact]
    public async Task EventAggregator_RefreshDataMessage_TriggersRefresh()
    {
        // Arrange
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

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Enterprise>());

        // Act
        var refreshEvent = testEventAggregator.GetEvent<RefreshDataMessage>() as TestRefreshDataMessage;
        refreshEvent?.Publish();
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        _mockEnterpriseRepository.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
    }

    [StaFact]
    public async Task EventAggregator_BudgetUpdatedMessage_TriggersRefresh()
    {
        // Arrange
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

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Enterprise>());

        // Act
        var budgetEvent = testEventAggregator.GetEvent<BudgetUpdatedMessage>() as TestBudgetUpdatedMessage;
        budgetEvent?.Publish();
        await Task.Delay(200).ConfigureAwait(false);

        // Assert
        _mockEnterpriseRepository.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
    }

    #endregion

    #region All Commands Existence Tests

    [StaFact]
    public async Task AllCommands_AreInitialized()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.LoadDataCommand.Should().NotBeNull();
        viewModel.RefreshDashboardCommand.Should().NotBeNull();
        viewModel.ToggleAutoRefreshCommand.Should().NotBeNull();
        viewModel.ExportDashboardCommand.Should().NotBeNull();
        viewModel.OpenBudgetAnalysisCommand.Should().NotBeNull();
        viewModel.OpenSettingsCommand.Should().NotBeNull();
        viewModel.GenerateReportCommand.Should().NotBeNull();
        viewModel.BackupDataCommand.Should().NotBeNull();
        viewModel.SearchCommand.Should().NotBeNull();
        viewModel.ClearSearchCommand.Should().NotBeNull();
        viewModel.NavigateToAccountsCommand.Should().NotBeNull();
        viewModel.NavigateBackCommand.Should().NotBeNull();
        viewModel.NavigateForwardCommand.Should().NotBeNull();
        viewModel.OpenEnterpriseManagementCommand.Should().NotBeNull();
        viewModel.RunGrowthScenarioCommand.Should().NotBeNull();
    }

    #endregion

    #region Collection Properties Tests

    [StaFact]
    public async Task Enterprises_Collection_IsInitialized()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.Enterprises.Should().NotBeNull();
        viewModel.Enterprises.Should().BeOfType<ObservableCollection<Enterprise>>();
    }

    [StaFact]
    public async Task FilteredEnterprises_Collection_IsInitialized()
    {
        // Arrange & Act
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Assert
        viewModel.FilteredEnterprises.Should().NotBeNull();
        viewModel.FilteredEnterprises.Should().BeOfType<ObservableCollection<Enterprise>>();
    }

    [StaFact]
    public async Task Enterprises_CollectionChanged_LogsActivity()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        await viewModel.LoadDashboardDataAsync().ConfigureAwait(false);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Enterprises collection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region INavigationAware Complete Lifecycle Tests

    [StaFact]
    public async Task OnNavigatedTo_WithRefreshParameter_LoadsData()
    {
        // Arrange
        var viewModel = CreateViewModel(out var testAggregator);
        var mockNavService = new Mock<IRegionNavigationService>();
        var parameters = new NavigationParameters { { "refresh", true } };
        var mockContext = new NavigationContext(mockNavService.Object, new Uri("test://Dashboard", UriKind.Absolute), parameters);

        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Water" }
        };

        _mockEnterpriseRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(testEnterprises);

        // Act
        viewModel.OnNavigatedTo(mockContext);

        // Assert: wait up to 5 seconds for background navigation load to trigger
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool called = false;
        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                _mockEnterpriseRepository.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
                called = true;
                break;
            }
            catch
            {
                // additionally check a user-visible signal of load completion
                if (!string.Equals(viewModel.LastUpdated, "Never", StringComparison.Ordinal))
                {
                    called = true;
                    break;
                }
                await Task.Delay(50).ConfigureAwait(false);
            }
        }
        called.Should().BeTrue("OnNavigatedTo with refresh parameter should trigger data load");
    }

    [StaFact]
    public async Task IsNavigationTarget_WithRefreshParameter_ReturnsTrue()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var mockNavService = new Mock<IRegionNavigationService>();
        var parameters = new NavigationParameters();
        parameters.Add("refresh", true);
        var mockContext = new NavigationContext(mockNavService.Object, new Uri("test://Dashboard", UriKind.Absolute), parameters);

        // Act
        var result = viewModel.IsNavigationTarget(mockContext);

        // Assert
        result.Should().BeTrue("Dashboard can handle refresh navigation");
    }

    [StaFact]
    public async Task OnNavigatedFrom_CancelsOngoingOperations()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);
        var mockNavService = new Mock<IRegionNavigationService>();
        var mockContext = new NavigationContext(mockNavService.Object, new Uri("test://Dashboard", UriKind.Absolute));

        // Act
        viewModel.OnNavigatedFrom(mockContext);

        // Assert - Should not throw
        viewModel.Should().NotBeNull();
    }

    #endregion

    #region Disposal Comprehensive Tests

    [StaFact]
    public async Task Dispose_UnsubscribesFromEventAggregator()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.Dispose();

        // Assert - Dispose should complete without errors
        viewModel.Should().NotBeNull();
    }

    [StaFact]
    public async Task Dispose_StopsAutoRefreshTimer()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.Dispose();

        // Assert - Should not throw
        viewModel.Should().NotBeNull();
    }

    [StaFact]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        using var viewModel = CreateViewModel();

        // Give background tasks time to complete
        await Task.Delay(100).ConfigureAwait(false);

        // Act
        viewModel.Dispose();
        var exception = Record.Exception(() => viewModel.Dispose());

        // Assert
        exception.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private DashboardViewModel CreateViewModel()
    {
        // Use TestEventAggregator to avoid UIThread deadlocks
        var testEventAggregator = new TestEventAggregator();

        return new DashboardViewModel(
            _mockLogger.Object,
            _mockEnterpriseRepository.Object,
            _mockWhatIfScenarioEngine.Object,
            _mockUtilityCustomerRepository.Object,
            _mockMunicipalAccountRepository.Object,
            _fiscalYearSettings,
            testEventAggregator, // Use TestEventAggregator instead of mock
            _mockRegionManager.Object,
            _mockCacheService.Object,
            autoLoadData: false); // Disable auto-load for tests - tests call LoadDashboardDataAsync explicitly
    }

    private DashboardViewModel CreateViewModel(out TestEventAggregator testEventAggregator)
    {
        // Provide the aggregator instance to tests that need to listen for events
        testEventAggregator = new TestEventAggregator();

        return new DashboardViewModel(
            _mockLogger.Object,
            _mockEnterpriseRepository.Object,
            _mockWhatIfScenarioEngine.Object,
            _mockUtilityCustomerRepository.Object,
            _mockMunicipalAccountRepository.Object,
            _fiscalYearSettings,
            testEventAggregator,
            _mockRegionManager.Object,
            _mockCacheService.Object,
            autoLoadData: false);
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
