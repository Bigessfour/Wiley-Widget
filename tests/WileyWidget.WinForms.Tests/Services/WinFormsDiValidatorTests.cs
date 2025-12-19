using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Services
{
    /// <summary>
    /// Unit tests for WinFormsDiValidator.
    /// Tests the WinForms-specific validation orchestrator that categorizes
    /// services into logical groups (repositories, services, viewmodels, forms).
    /// </summary>
    public class WinFormsDiValidatorTests
    {
        private readonly Mock<IDiValidationService> _mockCoreValidator;
        private readonly Mock<ILogger<WinFormsDiValidator>> _mockLogger;
        private readonly WinFormsDiValidator _validator;

        public WinFormsDiValidatorTests()
        {
            _mockCoreValidator = new Mock<IDiValidationService>();
            _mockLogger = new Mock<ILogger<WinFormsDiValidator>>();
            _validator = new WinFormsDiValidator(_mockCoreValidator.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullCoreValidator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new WinFormsDiValidator(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new WinFormsDiValidator(_mockCoreValidator.Object, null!));
        }

        [Fact]
        public void ValidateCriticalServices_DelegatesToCoreValidator()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var expectedResult = new DiValidationResult { IsValid = true };

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(
                    It.IsAny<IServiceProvider>(),
                    It.IsAny<IEnumerable<Type>>(),
                    "Critical Services"))
                .Returns(expectedResult);

            // Act
            var result = _validator.ValidateCriticalServices(serviceProvider);

            // Assert
            Assert.Same(expectedResult, result);
            _mockCoreValidator.Verify(
                x => x.ValidateServiceCategory(
                    serviceProvider,
                    It.Is<IEnumerable<Type>>(types => types.Any(t => t == typeof(ITelemetryService))),
                    "Critical Services"),
                Times.Once);
        }

        [Fact]
        public void ValidateRepositories_ValidatesAllRepositoryTypes()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var expectedResult = new DiValidationResult { IsValid = true };

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(
                    It.IsAny<IServiceProvider>(),
                    It.IsAny<IEnumerable<Type>>(),
                    "Repositories"))
                .Returns(expectedResult);

            // Act
            var result = _validator.ValidateRepositories(serviceProvider);

            // Assert
            Assert.Same(expectedResult, result);
            _mockCoreValidator.Verify(
                x => x.ValidateServiceCategory(
                    serviceProvider,
                    It.Is<IEnumerable<Type>>(types =>
                        types.Contains(typeof(IAccountsRepository)) &&
                        types.Contains(typeof(IBudgetRepository)) &&
                        types.Contains(typeof(IDepartmentRepository))),
                    "Repositories"),
                Times.Once);
        }

        [Fact]
        public void ValidateServices_ValidatesBusinessServices()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var expectedResult = new DiValidationResult { IsValid = true };

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(
                    It.IsAny<IServiceProvider>(),
                    It.IsAny<IEnumerable<Type>>(),
                    "Business Services"))
                .Returns(expectedResult);

            // Act
            var result = _validator.ValidateServices(serviceProvider);

            // Assert
            Assert.Same(expectedResult, result);
            _mockCoreValidator.Verify(
                x => x.ValidateServiceCategory(
                    serviceProvider,
                    It.Is<IEnumerable<Type>>(types =>
                        types.Contains(typeof(IQuickBooksService)) &&
                        types.Contains(typeof(IDashboardService))),
                    "Business Services"),
                Times.Once);
        }

        [Fact]
        public void ValidateViewModels_ValidatesAllViewModelTypes()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var expectedResult = new DiValidationResult { IsValid = true };

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(
                    It.IsAny<IServiceProvider>(),
                    It.IsAny<IEnumerable<Type>>(),
                    "ViewModels"))
                .Returns(expectedResult);

            // Act
            var result = _validator.ValidateViewModels(serviceProvider);

            // Assert
            Assert.Same(expectedResult, result);
            _mockCoreValidator.Verify(
                x => x.ValidateServiceCategory(
                    serviceProvider,
                    It.Is<IEnumerable<Type>>(types =>
                        types.Contains(typeof(DashboardViewModel)) &&
                        types.Contains(typeof(AccountsViewModel))),
                    "ViewModels"),
                Times.Once);
        }

        [Fact]
        public void ValidateForms_ValidatesAllFormTypes()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var expectedResult = new DiValidationResult { IsValid = true };

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(
                    It.IsAny<IServiceProvider>(),
                    It.IsAny<IEnumerable<Type>>(),
                    "Forms"))
                .Returns(expectedResult);

            // Act
            var result = _validator.ValidateForms(serviceProvider);

            // Assert
            Assert.Same(expectedResult, result);
            _mockCoreValidator.Verify(
                x => x.ValidateServiceCategory(
                    serviceProvider,
                    It.Is<IEnumerable<Type>>(types =>
                        types.Contains(typeof(MainForm)) &&
                        types.Contains(typeof(DashboardForm))),
                    "Forms"),
                Times.Once);
        }

        [Fact]
        public void ValidateAll_CombinesAllCategoryResults()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            var criticalResult = new DiValidationResult
            {
                IsValid = true,
                SuccessMessages = { "Service1", "Service2" }
            };

            var repoResult = new DiValidationResult
            {
                IsValid = true,
                SuccessMessages = { "Repo1" }
            };

            var servicesResult = new DiValidationResult
            {
                IsValid = false,
                SuccessMessages = { "BizService1" },
                Errors = { "BizService2 missing" }
            };

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(It.IsAny<IServiceProvider>(), It.IsAny<IEnumerable<Type>>(), "Critical Services"))
                .Returns(criticalResult);
            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(It.IsAny<IServiceProvider>(), It.IsAny<IEnumerable<Type>>(), "Repositories"))
                .Returns(repoResult);
            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(It.IsAny<IServiceProvider>(), It.IsAny<IEnumerable<Type>>(), "Business Services"))
                .Returns(servicesResult);
            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(It.IsAny<IServiceProvider>(), It.IsAny<IEnumerable<Type>>(), "ViewModels"))
                .Returns(new DiValidationResult { IsValid = true });
            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(It.IsAny<IServiceProvider>(), It.IsAny<IEnumerable<Type>>(), "Forms"))
                .Returns(new DiValidationResult { IsValid = true });

            // Act
            var result = _validator.ValidateAll(serviceProvider);

            // Assert
            Assert.False(result.IsValid); // Should fail due to servicesResult
            Assert.Equal(4, result.SuccessMessages.Count); // 2 + 1 + 1
            Assert.Single(result.Errors); // 1 error from servicesResult
        }

        [Fact]
        public void ValidateAll_WithAllServicesValid_ReturnsValidResult()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(It.IsAny<IServiceProvider>(), It.IsAny<IEnumerable<Type>>(), It.IsAny<string>()))
                .Returns(new DiValidationResult { IsValid = true, SuccessMessages = { "test" } });

            // Act
            var result = _validator.ValidateAll(serviceProvider);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateAll_IncludesValidationDuration()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            _mockCoreValidator
                .Setup(x => x.ValidateServiceCategory(It.IsAny<IServiceProvider>(), It.IsAny<IEnumerable<Type>>(), It.IsAny<string>()))
                .Returns(new DiValidationResult { IsValid = true });

            // Act
            var result = _validator.ValidateAll(serviceProvider);

            // Assert
            Assert.True(result.ValidationDuration > TimeSpan.Zero);
        }

        [Fact]
        public void DiValidationResult_GetSummary_WithValidResult_ShowsSuccessMessage()
        {
            // Arrange
            var result = new DiValidationResult
            {
                IsValid = true,
                SuccessMessages = { "Service1", "Service2", "Service3" },
                ValidationDuration = TimeSpan.FromMilliseconds(123)
            };

            // Act
            var summary = result.GetSummary();

            // Assert
            Assert.Contains("✓", summary, StringComparison.Ordinal);
            Assert.Contains("3 services verified", summary, StringComparison.Ordinal);
            Assert.Contains("123ms", summary, StringComparison.Ordinal);
        }

        [Fact]
        public void DiValidationResult_GetSummary_WithErrors_ShowsFailureMessage()
        {
            // Arrange
            var result = new DiValidationResult
            {
                IsValid = false,
                Errors = { "Error1", "Error2" },
                Warnings = { "Warning1" }
            };

            // Act
            var summary = result.GetSummary();

            // Assert
            Assert.Contains("✗", summary, StringComparison.Ordinal);
            Assert.Contains("2 errors", summary, StringComparison.Ordinal);
            Assert.Contains("1 warnings", summary, StringComparison.Ordinal);
        }
    }
}
