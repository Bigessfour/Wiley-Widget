using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.Tests.Helpers;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Templates
{
    /// <summary>
    /// Template unit test class for ViewModels.
    /// Copy this template for each new ViewModel and customize as needed.
    ///
    /// REQUIRED TEST COVERAGE:
    /// 1. ✅ Constructor validation (valid + null arguments)
    /// 2. ✅ Property change notifications
    /// 3. ✅ Command execution (success/failure/cancellation)
    /// 4. ✅ Service interaction (mocked dependencies)
    /// 5. ✅ Error handling
    /// 6. ✅ Disposal (if IDisposable)
    /// 7. ✅ Comprehensive reflection-based validation
    ///
    /// INSTRUCTIONS:
    /// 1. Copy this file and rename: [YourViewModel]Tests.cs
    /// 2. Replace TViewModel with your actual ViewModel class
    /// 3. Replace TMockService with your service interfaces
    /// 4. Add specific test cases for your ViewModel's behavior
    /// 5. Run ViewModelValidator.ValidateViewModel() in constructor test
    /// </summary>
    public sealed class ViewModelTestTemplate : IDisposable
    {
        // 🔧 TODO: Replace these with your actual types
        // private readonly Mock<ILogger<TViewModel>> _mockLogger;
        // private readonly Mock<IYourService> _mockYourService;
        // private readonly TViewModel _viewModel;

        // Example for MainViewModel:
        private readonly Mock<ILogger<MainViewModel>> _mockLogger;
        private readonly Mock<WileyWidget.Services.Abstractions.IMainDashboardService> _mockDashboardService;
        private readonly Mock<WileyWidget.Services.IAILoggingService> _mockAiLoggingService;
        private readonly MainViewModel _viewModel;

        public ViewModelTestTemplate()
        {
            // 🔧 TODO: Initialize your mocks
            _mockLogger = new Mock<ILogger<MainViewModel>>();
            _mockDashboardService = new Mock<WileyWidget.Services.Abstractions.IMainDashboardService>();
            _mockAiLoggingService = new Mock<WileyWidget.Services.IAILoggingService>();

            // Setup default responses for services
            _mockDashboardService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WileyWidget.Services.Abstractions.DashboardDto(
                    100000m, 75000m, 25000m, 10, 5, DateTime.Now.ToString("g")));

            // 🔧 TODO: Construct your ViewModel
            _viewModel = new MainViewModel(_mockLogger.Object, _mockDashboardService.Object, _mockAiLoggingService.Object);
        }

        #region 1. Constructor Tests

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Critical")]
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            // Arrange & Act
            var viewModel = new MainViewModel(_mockLogger.Object, _mockDashboardService.Object, _mockAiLoggingService.Object);

            // Assert
            viewModel.Should().NotBeNull();
            viewModel.Title.Should().NotBeNullOrEmpty();

            // ✅ CRITICAL: Run comprehensive reflection-based validation
            var validationResult = ViewModelValidator.ValidateViewModel(viewModel);
            validationResult.ThrowIfInvalid(); // Fails test if validation errors found
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Critical")]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new MainViewModel(null!, _mockDashboardService.Object, _mockAiLoggingService.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Critical")]
        public void Constructor_WithNullService_ShouldThrowArgumentNullException()
        {
            // 🔧 TODO: Test each service parameter for null
            Action act = () => new MainViewModel(_mockLogger.Object, null!, _mockAiLoggingService.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        #endregion

        #region 2. Property Change Notification Tests

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public void Property_WhenChanged_ShouldRaisePropertyChanged()
        {
            // 🔧 TODO: Test each observable property
            // Example:
            ViewModelValidator.AssertPropertyRaisesPropertyChanged(
                _viewModel,
                nameof(MainViewModel.Title),
                "New Title");
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public void NumericProperty_WhenChanged_ShouldRaisePropertyChanged()
        {
            // Example for numeric properties:
            ViewModelValidator.AssertPropertyRaisesPropertyChanged(
                _viewModel,
                nameof(MainViewModel.TotalBudget),
                50000m);
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public void BooleanProperty_WhenChanged_ShouldRaisePropertyChanged()
        {
            // Example for boolean properties:
            ViewModelValidator.AssertPropertyRaisesPropertyChanged(
                _viewModel,
                nameof(MainViewModel.IsLoading),
                true);
        }

        #endregion

        #region 3. Command Execution Tests

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Critical")]
        public async Task Command_WhenExecuted_ShouldCompleteSuccessfully()
        {
            // 🔧 TODO: Test each command
            // Example:
            await ViewModelValidator.AssertCommandExecutesAsync(
                _viewModel,
                nameof(MainViewModel.LoadDataCommand),
                expectedCanExecute: true);

            // Verify service was called
            _mockDashboardService.Verify(
                x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public async Task AsyncCommand_WithCancellation_ShouldHandleGracefully()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            _mockDashboardService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            cts.Cancel();
            var command = _viewModel.LoadDataCommand;
            await command.ExecuteAsync(null);

            // Assert - should not throw, should handle cancellation
            _viewModel.ErrorMessage.Should().BeNullOrEmpty();
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public void Command_CanExecute_ShouldReturnCorrectValue()
        {
            // 🔧 TODO: Test CanExecute logic
            // Example:
            _viewModel.LoadDataCommand.CanExecute(null).Should().BeTrue();

            // If command should be disabled during loading:
            // _viewModel.IsLoading = true;
            // _viewModel.LoadDataCommand.CanExecute(null).Should().BeFalse();
        }

        #endregion

        #region 4. Service Interaction Tests

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Critical")]
        public async Task LoadData_ShouldCallServiceAndUpdateProperties()
        {
            // Arrange
            var expectedData = new WileyWidget.Services.Abstractions.DashboardDto(
                TotalBudget: 200000m,
                TotalActual: 150000m,
                Variance: 50000m,
                ActiveAccountCount: 20,
                TotalDepartments: 8,
                LastUpdateTime: "2025-12-08 10:00 AM");

            _mockDashboardService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedData);

            // Act
            await _viewModel.LoadDataCommand.ExecuteAsync(null);

            // Assert
            _viewModel.TotalBudget.Should().Be(expectedData.TotalBudget);
            _viewModel.TotalActual.Should().Be(expectedData.TotalActual);
            _viewModel.Variance.Should().Be(expectedData.Variance);
            _viewModel.ActiveAccountCount.Should().Be(expectedData.ActiveAccountCount);

            _mockDashboardService.Verify(
                x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public async Task ServiceCall_WhenFails_ShouldHandleGracefully()
        {
            // Arrange
            _mockDashboardService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Service unavailable"));

            // Act
            await _viewModel.LoadDataCommand.ExecuteAsync(null);

            // Assert
            _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region 5. Error Handling Tests

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public async Task Command_WhenExceptionThrown_ShouldSetErrorMessage()
        {
            // Arrange
            var expectedError = "Database connection failed";
            _mockDashboardService.Setup(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(expectedError));

            // Act
            await _viewModel.LoadDataCommand.ExecuteAsync(null);

            // Assert
            _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Medium")]
        public async Task Command_AfterError_ShouldAllowRetry()
        {
            // Arrange - first call fails
            _mockDashboardService.SetupSequence(x => x.LoadDashboardDataAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("First attempt failed"))
                .ReturnsAsync(new WileyWidget.Services.Abstractions.DashboardDto(100000m, 75000m, 25000m, 10, 5, DateTime.Now.ToString("g")));

            // Act - first attempt
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
            _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();

            // Act - retry
            await _viewModel.LoadDataCommand.ExecuteAsync(null);

            // Assert - should succeed on retry
            _viewModel.ErrorMessage.Should().BeNullOrEmpty();
        }

        #endregion

        #region 6. Disposal Tests (if IDisposable)

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Critical")]
        public void Dispose_ShouldExecuteWithoutException()
        {
            // Only if ViewModel implements IDisposable
            if (_viewModel is IDisposable disposable)
            {
                ViewModelValidator.AssertViewModelDisposesCorrectly(disposable);
            }
        }

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "High")]
        public void Dispose_WhenCalledMultipleTimes_ShouldBeIdempotent()
        {
            // Only if ViewModel implements IDisposable
            if (_viewModel is IDisposable disposable)
            {
                // Act & Assert - multiple dispose calls should be safe
                Action act = () =>
                {
                    disposable.Dispose();
                    disposable.Dispose();
                    disposable.Dispose();
                };

                act.Should().NotThrow();
            }
        }

        #endregion

        #region 7. Comprehensive Validation

        [Fact]
        [Trait("Category", "ViewModel")]
        [Trait("Priority", "Critical")]
        public void ViewModel_ShouldPassComprehensiveValidation()
        {
            // This test uses reflection to validate the entire ViewModel implementation
            var result = ViewModelValidator.ValidateViewModel(_viewModel);

            // Display all findings
            if (result.Errors.Count > 0)
            {
                var errorMessage = $"Validation errors:\n{string.Join("\n", result.Errors)}";
                throw new InvalidOperationException(errorMessage);
            }

            if (result.Warnings.Count > 0)
            {
                // Log warnings but don't fail test
                Console.WriteLine($"Validation warnings:\n{string.Join("\n", result.Warnings)}");
            }

            result.IsValid.Should().BeTrue("ViewModel should pass all validation rules");
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            (_viewModel as IDisposable)?.Dispose();
        }

        #endregion
    }
}
