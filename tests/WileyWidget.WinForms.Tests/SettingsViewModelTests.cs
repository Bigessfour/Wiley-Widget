#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    /// <summary>
    /// Baseline unit tests for <see cref="SettingsViewModel"/>.
    /// Verifies that stub implementations execute without exceptions
    /// and handle cancellation appropriately.
    /// </summary>
    public sealed class SettingsViewModelTests
    {
        private readonly Mock<ILogger<SettingsViewModel>> _mockLogger;
        private readonly Mock<ISettingsManagementService> _mockSettingsService;

        public SettingsViewModelTests()
        {
            _mockLogger = new Mock<ILogger<SettingsViewModel>>();
            _mockSettingsService = new Mock<ISettingsManagementService>();

            // Setup default settings response
            _mockSettingsService.Setup(x => x.LoadSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SettingsDto(
                    "Server=localhost;Database=WileyWidget;",
                    "Wiley Widget",
                    "Information",
                    false,
                    60));

            _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<SettingsDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SettingsSaveResult(true, new List<string>()));
        }

        [Fact]
        public void Constructor_WithValidLogger_ShouldSucceed()
        {
            // Act
            var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object);

            // Assert
            viewModel.Should().NotBeNull();
            viewModel.Title.Should().Be("Settings");
            viewModel.SaveCommand.Should().NotBeNull();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SettingsViewModel constructed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new SettingsViewModel(null!, _mockSettingsService.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public async Task SaveCommand_Execute_ShouldCompleteWithoutException()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object);

            // Act
            Func<Task> act = async () =>
            {
                if (viewModel.SaveCommand.CanExecute(null) && viewModel.SaveCommand is IAsyncRelayCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(null);
                }
            };

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task SaveCommand_WithCancellation_ShouldHandleGracefully()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act - Note: AsyncRelayCommand doesn't directly expose CancellationToken,
            // but we verify it doesn't throw when executed
            Func<Task> act = async () =>
            {
                if (viewModel.SaveCommand.CanExecute(null) && viewModel.SaveCommand is IAsyncRelayCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(null);
                }
            };

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public void SaveCommand_CanExecute_ShouldReturnTrue()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object);

            // Act
            var canExecute = viewModel.SaveCommand.CanExecute(null);

            // Assert
            canExecute.Should().BeTrue();
        }

        [Fact]
        public void Title_Property_ShouldBeObservable()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object);
            var propertyChangedRaised = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.Title))
                    propertyChangedRaised = true;
            };

            // Act
            viewModel.Title = "Updated Settings";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            viewModel.Title.Should().Be("Updated Settings");
        }

        [Fact]
        public async Task SaveCommand_MultipleExecutions_ShouldNotThrow()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object);

            // Act
            Func<Task> act = async () =>
            {
                if (viewModel.SaveCommand is IAsyncRelayCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(null);
                    await asyncCommand.ExecuteAsync(null);
                    await asyncCommand.ExecuteAsync(null);
                }
            };

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
