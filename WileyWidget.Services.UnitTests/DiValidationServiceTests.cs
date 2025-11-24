using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions;
using Xunit;

namespace WileyWidget.Services.UnitTests
{
    /// <summary>
    /// Unit tests for DiValidationService.
    /// Tests DI validation logic without requiring full application context.
    /// 
    /// Test coverage:
    /// - Core service validation (happy path and failures)
    /// - Full registration scanning (with mocked container)
    /// - Error reporting and suggested fixes
    /// - Edge cases (empty container, missing assemblies, generic types)
    /// </summary>
    public class DiValidationServiceTests
    {
        private readonly Mock<ILogger<DiValidationService>> _mockLogger;

        public DiValidationServiceTests()
        {
            _mockLogger = new Mock<ILogger<DiValidationService>>();
        }

        [Fact]
        public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new DiValidationService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new DiValidationService(services, null!));
        }

        [Fact]
        public void ValidateCoreServices_AllServicesRegistered_ReturnsTrue()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Register all core services
            services.AddSingleton<ISettingsService>(Mock.Of<ISettingsService>());
            services.AddSingleton<ISecretVaultService>(Mock.Of<ISecretVaultService>());
            services.AddSingleton<IQuickBooksService>(Mock.Of<IQuickBooksService>());
            services.AddSingleton<IAIService>(Mock.Of<IAIService>());
            services.AddSingleton<ITelemetryService>(Mock.Of<ITelemetryService>());
            services.AddSingleton<IAuditService>(Mock.Of<IAuditService>());
            services.AddSingleton<ICacheService>(Mock.Of<ICacheService>());
            // UI services removed - not available in test context without WinUI reference

            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            // Act
            var result = validator.ValidateCoreServices();

            // Assert
            Assert.True(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All core services validated")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ValidateCoreServices_MissingCriticalService_ReturnsFalse()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Register some but not all core services (missing ISettingsService)
            services.AddSingleton<ISecretVaultService>(Mock.Of<ISecretVaultService>());
            services.AddSingleton<IQuickBooksService>(Mock.Of<IQuickBooksService>());

            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            // Act
            var result = validator.ValidateCoreServices();

            // Assert
            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Core service missing")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void ValidateRegistrations_EmptyContainer_ReportsAllServicesMissing()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            // Use the abstractions assembly which has known interfaces
            var assemblies = new[] { typeof(ISettingsService).Assembly };

            // Act
            var report = validator.ValidateRegistrations(assemblies);

            // Assert
            Assert.NotNull(report);
            Assert.True(report.MissingServices.Count > 0, "Should find missing services in empty container");
            Assert.Equal(0, report.ResolvedServices.Count);
            Assert.False(report.IsFullyValid);
        }

        [Fact]
        public void ValidateRegistrations_WithRegisteredServices_ReportsCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService>(Mock.Of<ISettingsService>());
            services.AddSingleton<IAuditService>(Mock.Of<IAuditService>());
            services.AddSingleton<ICacheService>(Mock.Of<ICacheService>());

            // Note: UI services (INavigationService, IDialogService) are validated at runtime when available

            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            var assemblies = new[] { 
                typeof(ISettingsService).Assembly,
                typeof(ICacheService).Assembly 
            };

            // Act
            var report = validator.ValidateRegistrations(assemblies);

            // Assert
            Assert.NotNull(report);
            Assert.Contains("WileyWidget.Services.ISettingsService", report.ResolvedServices);
            Assert.Contains("WileyWidget.Services.IAuditService", report.ResolvedServices);
            Assert.Contains("WileyWidget.Abstractions.ICacheService", report.ResolvedServices);
            
            // Should have some missing (not all services registered)
            Assert.True(report.MissingServices.Count > 0);
            Assert.True(report.TotalServices > 3);
        }

        [Fact]
        public void ValidateRegistrations_WithScopedServices_ResolvesCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Register scoped service (simulating DbContext pattern)
            services.AddScoped<ISettingsService>(sp => Mock.Of<ISettingsService>());

            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            var assemblies = new[] { typeof(ISettingsService).Assembly };

            // Act
            var report = validator.ValidateRegistrations(assemblies);

            // Assert
            Assert.Contains("WileyWidget.Services.ISettingsService", report.ResolvedServices);
        }

        [Fact]
        public void ValidateRegistrations_ReturnsReportWithStatistics()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService>(Mock.Of<ISettingsService>());

            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            var assemblies = new[] { typeof(ISettingsService).Assembly };

            // Act
            var report = validator.ValidateRegistrations(assemblies);

            // Assert
            Assert.NotNull(report);
            Assert.True(report.TotalServices > 0);
            Assert.True(report.ValidationSuccessRate >= 0 && report.ValidationSuccessRate <= 100);
            Assert.NotEmpty(report.GetSummary());
        }

        [Fact]
        public void ValidateRegistrations_WithResolutionError_RecordsError()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Register service with a dependency that will fail
            services.AddSingleton<ISettingsService>(sp => 
            {
                // This will throw when trying to resolve
                throw new InvalidOperationException("Simulated dependency failure");
            });

            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            var assemblies = new[] { typeof(ISettingsService).Assembly };

            // Act
            var report = validator.ValidateRegistrations(assemblies);

            // Assert
            Assert.NotNull(report);
            // Should have recorded the error
            var settingsError = report.Errors.FirstOrDefault(e => 
                e.ServiceType.Contains("ISettingsService"));
            Assert.NotNull(settingsError);
            Assert.Contains("Simulated dependency failure", settingsError.ErrorMessage);
        }

        [Fact]
        public void GetDiscoveredServiceInterfaces_ReturnsKnownInterfaces()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            var assemblies = new[] { typeof(ISettingsService).Assembly };

            // Act
            var discoveredInterfaces = validator.GetDiscoveredServiceInterfaces(assemblies).ToList();

            // Assert
            Assert.NotEmpty(discoveredInterfaces);
            Assert.Contains(discoveredInterfaces, name => name.Contains("ISettingsService"));
            Assert.Contains(discoveredInterfaces, name => name.Contains("IAuditService"));
            Assert.Contains(discoveredInterfaces, name => name.Contains("ITelemetryService"));
        }

        [Fact]
        public void GetDiscoveredServiceInterfaces_ExcludesFrameworkInterfaces()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            var assemblies = new[] { typeof(ISettingsService).Assembly };

            // Act
            var discoveredInterfaces = validator.GetDiscoveredServiceInterfaces(assemblies).ToList();

            // Assert
            // Should not include framework interfaces
            Assert.DoesNotContain(discoveredInterfaces, name => name.Contains("IEnumerable"));
            Assert.DoesNotContain(discoveredInterfaces, name => name.Contains("ICollection"));
            Assert.DoesNotContain(discoveredInterfaces, name => name.Contains("IDisposable"));
        }

        [Fact]
        public void DiValidationReport_GetSummary_ReturnsFormattedString()
        {
            // Arrange
            var report = new DiValidationReport
            {
                ResolvedServices = { "Service1", "Service2", "Service3" },
                MissingServices = { "Service4" },
                Errors = { new DiValidationError { ServiceType = "Service5", ErrorMessage = "Error" } }
            };

            // Act
            var summary = report.GetSummary();

            // Assert
            Assert.Contains("3/4 resolved", summary);
            Assert.Contains("75.0%", summary);
            Assert.Contains("1 missing", summary);
            Assert.Contains("1 errors", summary);
        }

        [Fact]
        public void DiValidationReport_IsFullyValid_ReturnsTrueWhenNoIssues()
        {
            // Arrange
            var report = new DiValidationReport
            {
                ResolvedServices = { "Service1", "Service2" },
                MissingServices = { },
                Errors = { }
            };

            // Act & Assert
            Assert.True(report.IsFullyValid);
        }

        [Fact]
        public void DiValidationReport_IsFullyValid_ReturnsFalseWithMissingServices()
        {
            // Arrange
            var report = new DiValidationReport
            {
                ResolvedServices = { "Service1" },
                MissingServices = { "Service2" },
                Errors = { }
            };

            // Act & Assert
            Assert.False(report.IsFullyValid);
        }

        [Fact]
        public void DiValidationReport_IsFullyValid_ReturnsFalseWithErrors()
        {
            // Arrange
            var report = new DiValidationReport
            {
                ResolvedServices = { "Service1", "Service2" },
                MissingServices = { },
                Errors = { new DiValidationError { ServiceType = "Service1", ErrorMessage = "Error" } }
            };

            // Act & Assert
            Assert.False(report.IsFullyValid);
        }

        [Fact]
        public void DiValidationError_ToString_IncludesSuggestedFix()
        {
            // Arrange
            var error = new DiValidationError
            {
                ServiceType = "ITestService",
                ErrorMessage = "Not registered",
                SuggestedFix = "Add services.AddSingleton<ITestService, TestService>()"
            };

            // Act
            var result = error.ToString();

            // Assert
            Assert.Contains("ITestService", result);
            Assert.Contains("Not registered", result);
            Assert.Contains("Add services.AddSingleton", result);
        }

        [Fact]
        public void ValidateRegistrations_WithNullAssemblies_UsesDefaultAssemblies()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var validator = new DiValidationService(serviceProvider, _mockLogger.Object);

            // Act
            var report = validator.ValidateRegistrations(null); // null assemblies

            // Assert
            Assert.NotNull(report);
            // Should have scanned default assemblies and found services
            Assert.True(report.TotalServices > 0);
        }

        [Theory]
        [InlineData(0, 100.0)]  // No services = 100% (edge case)
        [InlineData(5, 100.0)]  // 5 resolved, 0 missing = 100%
        [InlineData(3, 60.0)]   // 3 resolved, 2 missing = 60%
        [InlineData(1, 25.0)]   // 1 resolved, 3 missing = 25%
        public void DiValidationReport_ValidationSuccessRate_CalculatesCorrectly(int resolvedCount, double expectedRate)
        {
            // Arrange
            var report = new DiValidationReport();
            
            for (int i = 0; i < resolvedCount; i++)
            {
                report.ResolvedServices.Add($"Service{i}");
            }

            // Calculate how many missing to achieve total
            int totalForRate = resolvedCount == 0 ? 0 : (int)(resolvedCount / (expectedRate / 100.0));
            int missingCount = totalForRate - resolvedCount;

            for (int i = 0; i < missingCount; i++)
            {
                report.MissingServices.Add($"MissingService{i}");
            }

            // Act
            var actualRate = report.ValidationSuccessRate;

            // Assert
            Assert.Equal(expectedRate, actualRate, precision: 1);
        }
    }
}
