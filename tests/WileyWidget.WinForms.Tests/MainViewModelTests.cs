#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    /// <summary>
    /// Baseline unit tests for <see cref="MainViewModel"/>.
    /// Verifies that stub implementations execute without exceptions
    /// and handle cancellation appropriately.
    /// </summary>
    public sealed class MainViewModelTests
    {
        private readonly Mock<ILogger<MainViewModel>> _mockLogger;
        private readonly Mock<IMainDashboardService> _mockDashboardService;
        private readonly Mock<WileyWidget.Services.IAILoggingService> _mockAiLoggingService;

        public MainViewModelTests()
        {
            _mockLogger = new Mock<ILogger<MainViewModel>>();
            _mockDashboardService = new Mock<IMainDashboardService>();
            _mockAiLoggingService = new Mock<WileyWidget.Services.IAILoggingService>();

            // Setup default dashboard data response
            _mockDashboardService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DashboardDto(100000m, 75000m, 25000m, 10, 5, DateTime.Now.ToString("g")));
        }

        [Fact]
        public void Constructor_WithValidLogger_ShouldSucceed()
        {
            // Act
            var viewModel = new MainViewModel(_mockLogger.Object, _mockDashboardService.Object, _mockAiLoggingService.Object);

            // Assert
            viewModel.Should().NotBeNull();
            viewModel.Title.Should().Be("Wiley Widget — WinForms + .NET 9");
            viewModel.LoadDataCommand.Should().NotBeNull();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MainViewModel constructed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new MainViewModel(null!, _mockDashboardService.Object, _mockAiLoggingService.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public async Task InitializeAsync_ShouldCompleteWithoutException()
        {
            // Arrange
            var viewModel = new MainViewModel(_mockLogger.Object, _mockDashboardService.Object, _mockAiLoggingService.Object);

            // Act
            Func<Task> act = async () => await viewModel.InitializeAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task InitializeAsync_WithCancellationToken_ShouldComplete()
        {
            // Arrange
            var viewModel = new MainViewModel(_mockLogger.Object, _mockDashboardService.Object, _mockAiLoggingService.Object);
            using var cts = new CancellationTokenSource();

            // Act
            Func<Task> act = async () => await viewModel.InitializeAsync(cts.Token);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task InitializeAsync_WithCanceledToken_ShouldHandleGracefully()
        {
            // Arrange
            // Setup service to throw OperationCanceledException when cancellation is requested
            var mockService = new Mock<IMainDashboardService>();
            mockService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var viewModel = new MainViewModel(_mockLogger.Object, mockService.Object, _mockAiLoggingService.Object);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await viewModel.InitializeAsync(cts.Token);

            // Assert
            await act.Should().NotThrowAsync();

            // Verify cancellation was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("canceled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task LoadDataCommand_Execute_ShouldCompleteWithoutException()
        {
            // Arrange
            var viewModel = new MainViewModel(_mockLogger.Object, _mockDashboardService.Object, _mockAiLoggingService.Object);

            // Act
            Func<Task> act = async () =>
            {
                if (viewModel.LoadDataCommand.CanExecute(null))
                {
                    viewModel.LoadDataCommand.Execute(null);
                    // Give async command time to complete
                    await Task.Delay(200);
                }
            };

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public void Title_Property_ShouldBeObservable()
        {
            // Arrange
            var viewModel = new MainViewModel(_mockLogger.Object, _mockDashboardService.Object, _mockAiLoggingService.Object);
            var propertyChangedRaised = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.Title))
                    propertyChangedRaised = true;
            };

            // Act
            viewModel.Title = "Updated Title";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            viewModel.Title.Should().Be("Updated Title");
        }
    }
}
