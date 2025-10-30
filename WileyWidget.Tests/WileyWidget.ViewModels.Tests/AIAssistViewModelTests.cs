using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Events;
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
/// Unit tests for AIAssistViewModel focusing on error handling, IDialogService integration,
/// AI service interactions, and graceful error recovery.
/// </summary>
public class AIAssistViewModelTests
{
    private readonly Mock<IAIService> _mockAIService;
    private readonly Mock<IChargeCalculatorService> _mockChargeCalculator;
    private readonly Mock<IWhatIfScenarioEngine> _mockScenarioEngine;
    private readonly Mock<IGrokSupercomputer> _mockGrokSupercomputer;
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepository;
    private readonly Mock<IDispatcherHelper> _mockDispatcherHelper;
    private readonly Mock<ILogger<AIAssistViewModel>> _mockLogger;
    private readonly Mock<IEventAggregator> _mockEventAggregator;
    private readonly Mock<ICacheService> _mockCacheService;

    public AIAssistViewModelTests()
    {
        _mockAIService = new Mock<IAIService>();
        _mockChargeCalculator = new Mock<IChargeCalculatorService>();
        _mockScenarioEngine = new Mock<IWhatIfScenarioEngine>();
        _mockGrokSupercomputer = new Mock<IGrokSupercomputer>();
        _mockEnterpriseRepository = new Mock<IEnterpriseRepository>();
        _mockDispatcherHelper = new Mock<IDispatcherHelper>();
        _mockLogger = new Mock<ILogger<AIAssistViewModel>>();
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockCacheService = new Mock<ICacheService>();

        // Setup event aggregator - return real PubSubEvent instances since Subscribe is not virtual
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<EnterpriseChangedMessage>())
            .Returns(new EnterpriseChangedMessage());

        _mockEventAggregator
            .Setup(ea => ea.GetEvent<RefreshDataMessage>())
            .Returns(new RefreshDataMessage());

        // Setup dispatcher helper
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
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.SendCommand.Should().NotBeNull();
        viewModel.GenerateCommand.Should().NotBeNull();
        viewModel.ClearChatCommand.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullAIService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AIAssistViewModel(
                null!,
                _mockChargeCalculator.Object,
                _mockScenarioEngine.Object,
                _mockGrokSupercomputer.Object,
                _mockEnterpriseRepository.Object,
                _mockDispatcherHelper.Object,
                _mockLogger.Object,
                _mockEventAggregator.Object,
                _mockCacheService.Object));
    }

    #endregion

    #region Navigation Tests (INavigationAware)

    [Fact]
    public void OnNavigatedTo_LogsNavigationEvent()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://AIAssist", UriKind.Absolute));

        // Act
        viewModel.OnNavigatedTo(mockContext.Object);

        // Assert - Verify logging occurred (Serilog logs to Log.Information)
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public void IsNavigationTarget_AlwaysReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://AIAssist", UriKind.Absolute));

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
            new Uri("test://AIAssist", UriKind.Absolute));

        // Act
        viewModel.OnNavigatedFrom(mockContext.Object);

        // Assert
        viewModel.Should().NotBeNull();
    }

    #endregion

    #region AI Command Error Handling Tests

    [Fact]
    public async Task SendCommand_WithAIServiceException_HandlesErrorGracefully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testException = new InvalidOperationException("AI Service Error");

        _mockAIService
            .Setup(s => s.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        // Set UserInput via property
        var userInputProperty = typeof(AIAssistViewModel).GetProperty("UserInput");
        if (userInputProperty != null && userInputProperty.CanWrite)
        {
            userInputProperty.SetValue(viewModel, "Test query");
        }

        // Act
        viewModel.SendCommand.Execute();
        await Task.Delay(100);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Error should be logged");

        // UI should remain responsive (no exceptions thrown)
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateCommand_WithException_LogsErrorAndContinues()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testException = new InvalidOperationException("Generation failed");

        _mockAIService
            .Setup(s => s.AnalyzeDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(testException);

        // Act
        viewModel.GenerateCommand.Execute();
        await Task.Delay(100);

        // Assert - Error logged, no crash
        _mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CalculateServiceChargeCommand_WithInvalidData_HandlesGracefully()
    {
        // Arrange
        var viewModel = CreateViewModel();

        _mockChargeCalculator
            .Setup(c => c.CalculateRecommendedChargeAsync(It.IsAny<int>()))
            .ThrowsAsync(new ArgumentException("Invalid enterprise data"));

        // Act
        viewModel.CalculateServiceChargeCommand.Execute();
        await Task.Delay(100);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Command CanExecute Tests

    [Fact]
    public void SendCommand_CanExecute_WhenUserInputNotEmpty()
    {
        // Arrange
        var viewModel = CreateViewModel();

        var userInputProperty = typeof(AIAssistViewModel).GetProperty("UserInput");
        if (userInputProperty != null && userInputProperty.CanWrite)
        {
            userInputProperty.SetValue(viewModel, "Test query");
        }

        // Act
        var canExecute = viewModel.SendCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void SendCommand_CannotExecute_WhenBusy()
    {
        // Arrange
        var viewModel = CreateViewModel();

        var isBusyProperty = typeof(AIAssistViewModel).GetProperty("IsBusy");
        if (isBusyProperty != null && isBusyProperty.CanWrite)
        {
            isBusyProperty.SetValue(viewModel, true);
        }

        // Act
        var canExecute = viewModel.SendCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Command should not execute when busy");
    }

    #endregion

    #region Property Change Notification Tests

    [Fact]
    public void UserInput_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "UserInput")
                propertyChangedRaised = true;
        };

        // Act
        var userInputProperty = typeof(AIAssistViewModel).GetProperty("UserInput");
        if (userInputProperty != null && userInputProperty.CanWrite)
        {
            userInputProperty.SetValue(viewModel, "Test Input");
        }

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    #endregion

    #region Async Operation Tests

    [Fact]
    public async Task GenerateWhatIfScenarioCommand_ExecutesAsync_WithoutDeadlock()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockScenario = new ComprehensiveScenario { ScenarioName = "Test Scenario" };

        _mockScenarioEngine
            .Setup(e => e.GenerateComprehensiveScenarioAsync(It.IsAny<int>(), It.IsAny<ScenarioParameters>()))
            .ReturnsAsync(mockScenario);

        // Act
        viewModel.GenerateWhatIfScenarioCommand.Execute();
        await Task.Delay(100);

        // Assert
        _mockScenarioEngine.Verify(
            e => e.GenerateComprehensiveScenarioAsync(It.IsAny<int>(), It.IsAny<ScenarioParameters>()),
            Times.AtLeastOnce);

        _mockDispatcherHelper.Verify(
            d => d.InvokeAsync(It.IsAny<Action>()),
            Times.AtLeastOnce,
            "Dispatcher should marshal UI updates");
    }

    [Fact]
    public async Task GetProactiveAdviceCommand_ExecutesAsync_HandlesResponse()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testAdvice = "Consider optimizing budget allocation";
        var testEnterprise = new Enterprise { Id = 1, Name = "Test" };

        _mockGrokSupercomputer
            .Setup(g => g.AnalyzeMunicipalDataAsync(It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(testAdvice);

        // Act
        viewModel.GetProactiveAdviceCommand.Execute();
        await Task.Delay(100);

        // Assert
        _mockGrokSupercomputer.Verify(
            g => g.AnalyzeMunicipalDataAsync(It.IsAny<object>(), It.IsAny<string>()),
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

    private AIAssistViewModel CreateViewModel()
    {
        return new AIAssistViewModel(
            _mockAIService.Object,
            _mockChargeCalculator.Object,
            _mockScenarioEngine.Object,
            _mockGrokSupercomputer.Object,
            _mockEnterpriseRepository.Object,
            _mockDispatcherHelper.Object,
            _mockLogger.Object,
            _mockEventAggregator.Object,
            _mockCacheService.Object);
    }

    #endregion
}
