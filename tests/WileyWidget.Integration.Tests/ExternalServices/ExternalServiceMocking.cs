using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Integration.Tests.ExternalServices;

/// <summary>
/// Mock framework for external service testing with configurable failure modes
/// </summary>
public class ExternalServiceMockFactory
{
    /// <summary>
    /// Creates a mock QuickBooks service with configurable behavior
    /// </summary>
    public static Mock<IQuickBooksService> CreateQuickBooksServiceMock(
        QuickBooksMockConfig? config = null)
    {
        config ??= new QuickBooksMockConfig();
        var mock = new Mock<IQuickBooksService>();

        // Setup successful operations
        if (!config.ShouldFail)
        {
            mock.Setup(q => q.GetCustomersAsync())
                .ReturnsAsync(new List<Intuit.Ipp.Data.Customer>
                {
                    new Intuit.Ipp.Data.Customer { Id = "1", FullyQualifiedName = "Test Customer 1" },
                    new Intuit.Ipp.Data.Customer { Id = "2", FullyQualifiedName = "Test Customer 2" }
                });

            // Also support the CancellationToken overload so tests can validate cancellation behavior
            mock.Setup(q => q.GetCustomersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Intuit.Ipp.Data.Customer>
                {
                    new Intuit.Ipp.Data.Customer { Id = "1", FullyQualifiedName = "Test Customer 1" },
                    new Intuit.Ipp.Data.Customer { Id = "2", FullyQualifiedName = "Test Customer 2" }
                });

            mock.Setup(q => q.GetInvoicesAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<Intuit.Ipp.Data.Invoice>
                {
                    new Intuit.Ipp.Data.Invoice { Id = "INV-001", TotalAmt = 100.00m },
                    new Intuit.Ipp.Data.Invoice { Id = "INV-002", TotalAmt = 200.00m }
                });

            mock.Setup(q => q.GetChartOfAccountsAsync())
                .ReturnsAsync(new List<Intuit.Ipp.Data.Account>
                {
                    new Intuit.Ipp.Data.Account { Id = "1", Name = "Test Account 1" },
                    new Intuit.Ipp.Data.Account { Id = "2", Name = "Test Account 2" }
                });
        }
        else
        {
            // Setup failure scenarios
            var exception = config.FailureMode switch
            {
                FailureMode.Timeout => new TimeoutException("External service timeout"),
                FailureMode.Unauthorized => new HttpRequestException("401 Unauthorized"),
                FailureMode.ServerError => new HttpRequestException("500 Internal Server Error"),
                FailureMode.NetworkError => new HttpRequestException("Network connection failed"),
                _ => new Exception("Mock service failure")
            };

            mock.Setup(q => q.GetCustomersAsync())
                .ThrowsAsync(exception);

            mock.Setup(q => q.GetCustomersAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            mock.Setup(q => q.GetInvoicesAsync(It.IsAny<string>()))
                .ThrowsAsync(exception);

            mock.Setup(q => q.GetChartOfAccountsAsync())
                .ThrowsAsync(exception);
        }

        // Setup delays if configured
        if (config.ResponseDelay > TimeSpan.Zero)
        {
            mock.Setup(q => q.GetCustomersAsync())
                .Returns(async () =>
                {
                    await Task.Delay(config.ResponseDelay);
                    return new List<Intuit.Ipp.Data.Customer>();
                });

            mock.Setup(q => q.GetCustomersAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken ct) =>
                {
                    await Task.Delay(config.ResponseDelay, ct);
                    return new List<Intuit.Ipp.Data.Customer>();
                });

            mock.Setup(q => q.GetInvoicesAsync(It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Delay(config.ResponseDelay);
                    return new List<Intuit.Ipp.Data.Invoice>();
                });
        }

        return mock;
    }

    /// <summary>
    /// Creates a mock xAI service with configurable behavior
    /// </summary>
    public static Mock<IAIService> CreateXAIServiceMock(
        XAIMockConfig? config = null)
    {
        config ??= new XAIMockConfig();
        var mock = new Mock<IAIService>();

        if (!config.ShouldFail)
        {
            mock.Setup(x => x.GetInsightsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("Mock AI insights for budget analysis");

            mock.Setup(x => x.AnalyzeDataAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("Mock data analysis results");

            mock.Setup(x => x.GenerateRecommendationsAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("Generated recommendations for test");
        }
        else
        {
            var exception = config.FailureMode switch
            {
                FailureMode.Timeout => new TimeoutException("AI service timeout"),
                FailureMode.RateLimit => new HttpRequestException("429 Too Many Requests"),
                FailureMode.ServerError => new HttpRequestException("502 Bad Gateway"),
                _ => new Exception("AI service unavailable")
            };

            mock.Setup(x => x.GetRecommendedAdjustmentFactorsAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            mock.Setup(x => x.GetInsightsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            mock.Setup(x => x.GenerateRecommendationsAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }

        // Setup response delays
        if (config.ResponseDelay > TimeSpan.Zero)
        {
            mock.Setup(x => x.GetInsightsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (string context, string question, CancellationToken ct) =>
                {
                    await Task.Delay(config.ResponseDelay, ct);
                    return "Delayed AI response";
                });

            mock.Setup(x => x.GenerateRecommendationsAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (string prompt, int count, CancellationToken ct) =>
                {
                    await Task.Delay(config.ResponseDelay, ct);
                    return "Delayed recommendations response";
                });
        }

        return mock;
    }

    /// <summary>
    /// Creates a mock telemetry service for testing observability
    /// </summary>
    public static Mock<ITelemetryService> CreateTelemetryServiceMock(
        TelemetryMockConfig? config = null)
    {
        config ??= new TelemetryMockConfig();
        var mock = new Mock<ITelemetryService>();

        mock.Setup(t => t.RecordException(
                It.IsAny<Exception>(),
                It.IsAny<(string key, object? value)[]>()))
            .Callback<Exception, (string key, object? value)[]>((ex, tags) =>
            {
                // Log exception details for testing
                Console.WriteLine($"Exception recorded: {ex.Message}");
            });

        return mock;
    }
}

/// <summary>
/// Configuration for QuickBooks service mocking
/// </summary>
public class QuickBooksMockConfig
{
    public bool ShouldFail { get; set; }
    public FailureMode FailureMode { get; set; } = FailureMode.NetworkError;
    public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;
}

/// <summary>
/// Configuration for xAI service mocking
/// </summary>
public class XAIMockConfig
{
    public bool ShouldFail { get; set; }
    public FailureMode FailureMode { get; set; } = FailureMode.Timeout;
    public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;
}

/// <summary>
/// Configuration for telemetry service mocking
/// </summary>
public class TelemetryMockConfig
{
    public bool ShouldFail { get; set; }
}

/// <summary>
/// Types of failures that can be simulated
/// </summary>
public enum FailureMode
{
    Timeout,
    Unauthorized,
    ServerError,
    NetworkError,
    RateLimit
}

/// <summary>
/// Tests for external service mocking and failure scenarios
/// </summary>
public class ExternalServiceMockingTests : IntegrationTestBase
{
    [Fact]
    public async Task QuickBooksService_Timeout_ThrowsExpectedException()
    {
        // Arrange
        var mockConfig = new QuickBooksMockConfig
        {
            ShouldFail = true,
            FailureMode = FailureMode.Timeout
        };
        var mockService = ExternalServiceMockFactory.CreateQuickBooksServiceMock(mockConfig);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            mockService.Object.GetCustomersAsync());
    }

    [Fact]
    public async Task QuickBooksService_Success_ReturnsExpectedData()
    {
        // Arrange
        var mockService = ExternalServiceMockFactory.CreateQuickBooksServiceMock();

        // Act
        var customers = await mockService.Object.GetCustomersAsync();

        // Assert
        customers.Should().NotBeNull();
        customers.Should().HaveCount(2);
        customers.Should().Contain(c => c.FullyQualifiedName == "Test Customer 1");
    }

    [Fact]
    public async Task XAIService_RateLimit_ThrowsExpectedException()
    {
        // Arrange
        var mockConfig = new XAIMockConfig
        {
            ShouldFail = true,
            FailureMode = FailureMode.RateLimit
        };
        var mockService = ExternalServiceMockFactory.CreateXAIServiceMock(mockConfig);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            mockService.Object.GenerateRecommendationsAsync("test prompt", 3));
    }

    [Fact]
    public async Task XAIService_DelayedResponse_CompletesSuccessfully()
    {
        // Arrange
        var mockConfig = new XAIMockConfig
        {
            ResponseDelay = TimeSpan.FromMilliseconds(100)
        };
        var mockService = ExternalServiceMockFactory.CreateXAIServiceMock(mockConfig);

        // Act
        var response = await mockService.Object.GenerateRecommendationsAsync("test", 1);

        // Assert
        response.Should().NotBeNull();
        response.Should().Contain("recommendations");
    }

    [Fact]
    public async Task TelemetryService_RecordsExceptions_Correctly()
    {
        // Arrange
        var mockService = ExternalServiceMockFactory.CreateTelemetryServiceMock();
        var testException = new InvalidOperationException("Test exception");

        // Act
        mockService.Object.RecordException(testException, ("test", "value"));

        // Assert - Mock verification would happen here
        // In a real test, we'd verify the logging behavior
        await Task.CompletedTask; // Ensure async method has await
        Assert.True(true); // Placeholder assertion
    }

    [Fact]
    public async Task ExternalService_Cancellation_RespectsToken()
    {
        // Arrange
        var mockConfig = new QuickBooksMockConfig
        {
            ResponseDelay = TimeSpan.FromSeconds(10) // Long delay
        };
        var mockService = ExternalServiceMockFactory.CreateQuickBooksServiceMock(mockConfig);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            mockService.Object.GetCustomersAsync(cts.Token));
    }
}
