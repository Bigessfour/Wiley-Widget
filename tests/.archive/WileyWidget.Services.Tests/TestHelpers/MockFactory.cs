using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Tests.TestHelpers;

/// <summary>
/// Factory class for creating commonly used mocks in tests.
/// Provides consistent setup and reduces test code duplication.
/// </summary>
public static class MockFactory
{
    /// <summary>
    /// Creates a mock ILogger that captures log messages for verification.
    /// </summary>
    public static Mock<ILogger<T>> CreateLogger<T>()
    {
        var mockLogger = new Mock<ILogger<T>>();
        mockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Verifiable();

        return mockLogger;
    }

    /// <summary>
    /// Creates a mock IDashboardService with configurable behavior.
    /// </summary>
    public static Mock<IDashboardService> CreateDashboardService(
        IEnumerable<WileyWidget.Services.Abstractions.DashboardItem>? items = null,
        bool shouldThrow = false,
        Exception? exception = null)
    {
        var mockService = new Mock<IDashboardService>();

        if (shouldThrow)
        {
            mockService.Setup(s => s.GetDashboardItemsAsync())
                .ThrowsAsync(exception ?? new Exception("Service error"));
        }
        else
        {
            var defaultItems = items ?? new List<WileyWidget.Services.Abstractions.DashboardItem>
            {
                new() { Title = "Test Item 1", Value = "5", Description = "Description 1", Category = "Active" },
                new() { Title = "Test Item 2", Value = "10", Description = "Description 2", Category = "Inactive" }
            };

            mockService.Setup(s => s.GetDashboardItemsAsync())
                .ReturnsAsync(defaultItems);
        }

        return mockService;
    }

    /// <summary>
    /// Creates a mock HttpMessageHandler for HttpClient testing.
    /// </summary>
    public static Mock<HttpMessageHandler> CreateHttpMessageHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? content = null,
        Dictionary<string, string>? headers = null)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(statusCode);

                if (content != null)
                {
                    response.Content = new StringContent(content);
                }

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        response.Headers.Add(header.Key, header.Value);
                    }
                }

                return response;
            });

        return mockHandler;
    }

    /// <summary>
    /// Creates a mock ISecretVaultService for token storage testing.
    /// </summary>
    public static Mock<ISecretVaultService> CreateSecretVault(
        Dictionary<string, string>? secrets = null)
    {
        var mockVault = new Mock<ISecretVaultService>();
        var defaultSecrets = secrets ?? new Dictionary<string, string>
        {
            ["QBO_CLIENT_ID"] = "test-client-id",
            ["QBO_CLIENT_SECRET"] = "test-client-secret",
            ["QBO_ACCESS_TOKEN"] = "test-access-token",
            ["QBO_REFRESH_TOKEN"] = "test-refresh-token"
        };

        foreach (var secret in defaultSecrets)
        {
            mockVault.Setup(v => v.GetSecretAsync(secret.Key))
                .ReturnsAsync(secret.Value);
        }

        return mockVault;
    }

    /// <summary>
    /// Creates test data for DashboardItem collections.
    /// </summary>
    public static class TestData
    {
        public static List<WileyWidget.Services.Abstractions.DashboardItem> CreateDashboardItems(int count = 2)
        {
            var items = new List<WileyWidget.Services.Abstractions.DashboardItem>();
            for (int i = 1; i <= count; i++)
            {
                items.Add(new WileyWidget.Services.Abstractions.DashboardItem
                {
                    Title = $"Test Item {i}",
                    Value = $"{i * 5}",
                    Description = $"Description {i}",
                    Category = i % 2 == 0 ? "Active" : "Inactive"
                });
            }
            return items;
        }

        public static string CreateQuickBooksResponse(string entityType, int count = 2)
        {
            var entities = new List<object>();

            for (int i = 1; i <= count; i++)
            {
                var entity = entityType switch
                {
                    "Customer" => new { Id = i.ToString(), DisplayName = $"Customer {i}", Active = true },
                    "Invoice" => new { Id = i.ToString(), DocNumber = $"INV-{i:000}", TotalAmt = i * 100.0 },
                    "Account" => new { Id = i.ToString(), Name = $"Account {i}", Type = "Asset", Active = true },
                    _ => new { Id = i.ToString(), Name = $"Entity {i}" }
                };
                entities.Add(entity);
            }

            var response = new
            {
                QueryResponse = new Dictionary<string, object>
                {
                    [entityType] = entities
                }
            };

            return JsonSerializer.Serialize(response);
        }
    }
}

// Extension methods for common test patterns
public static class TestExtensions
{
    /// <summary>
    /// Verifies that a logger was called with a specific message.
    /// </summary>
    public static void VerifyLog<T>(this Mock<ILogger<T>> logger, LogLevel level, string message)
    {
        logger.Verify(x => x.Log(
            level,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().Contains(message)),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that a logger was called with an exception.
    /// </summary>
    public static void VerifyLogError<T>(this Mock<ILogger<T>> logger, Exception exception)
    {
        logger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
