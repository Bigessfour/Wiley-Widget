using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Tests.WinUI.Services;

/// <summary>
/// Robust unit tests for QuickBooksService - Non-Whitewash Implementation
/// Covers: Happy path, error handling, edge cases
/// </summary>
[Trait("Category", "Unit")]
public class QuickBooksServiceTests
{
    private readonly Mock<ILogger<QuickBooksService>> _mockLogger;
    
    public QuickBooksServiceTests()
    {
        _mockLogger = new Mock<ILogger<QuickBooksService>>();
    }

    #region SyncInvoicesAsync Tests

    [Fact]
    public async Task SyncInvoicesAsync_ValidData_SyncsAndReturnsCount()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        var testInvoices = new List<QuickBooksInvoice>
        {
            new() { Id = "INV-001", Amount = 100.50m, CustomerName = "Test Customer 1", Date = DateTime.UtcNow },
            new() { Id = "INV-002", Amount = 250.75m, CustomerName = "Test Customer 2", Date = DateTime.UtcNow }
        };
        
        mockApiClient
            .Setup(x => x.GetInvoicesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(testInvoices);

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.SyncInvoicesAsync();

        // Assert
        result.Should().NotBeNull();
        result.SyncedCount.Should().Be(2);
        result.Errors.Should().BeEmpty();
        result.Success.Should().BeTrue();
        
        mockApiClient.Verify(
            x => x.GetInvoicesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), 
            Times.Once,
            "Should call API exactly once"
        );
    }

    [Fact]
    public async Task SyncInvoicesAsync_ApiFails_ReportsErrorGracefully()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        mockApiClient
            .Setup(x => x.GetInvoicesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new HttpRequestException("Network timeout - Unable to reach QuickBooks API"));

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.SyncInvoicesAsync();

        // Assert
        result.Should().NotBeNull();
        result.SyncedCount.Should().Be(0);
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Network timeout");
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once,
            "Should log error when API call fails"
        );
    }

    [Fact]
    public async Task SyncInvoicesAsync_EmptyResponse_SkipsProcessingAndWarns()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        mockApiClient
            .Setup(x => x.GetInvoicesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<QuickBooksInvoice>());

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.SyncInvoicesAsync();

        // Assert
        result.Should().NotBeNull();
        result.SyncedCount.Should().Be(0);
        result.Success.Should().BeTrue("Empty result is not an error");
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].Should().Contain("No invoices found");
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once,
            "Should log warning for empty result"
        );
    }

    [Fact]
    public async Task SyncInvoicesAsync_NullInvoiceInList_SkipsNullAndContinues()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        var testInvoices = new List<QuickBooksInvoice>
        {
            new() { Id = "INV-001", Amount = 100m, CustomerName = "Valid", Date = DateTime.UtcNow },
            null!, // Null invoice
            new() { Id = "INV-003", Amount = 300m, CustomerName = "Also Valid", Date = DateTime.UtcNow }
        };
        
        mockApiClient
            .Setup(x => x.GetInvoicesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(testInvoices);

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.SyncInvoicesAsync();

        // Assert
        result.SyncedCount.Should().Be(2, "Should skip null invoice but process others");
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].Should().Contain("null");
    }

    #endregion

    #region OAuth Tests

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsAccessToken()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        var expectedToken = "test_access_token_xyz";
        
        mockApiClient
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new QuickBooksAuthResult 
            { 
                AccessToken = expectedToken,
                RefreshToken = "test_refresh_token",
                ExpiresIn = 3600,
                Success = true
            });

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.AuthenticateAsync("test_client_id", "test_secret");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be(expectedToken);
        result.ExpiresIn.Should().Be(3600);
        
        mockApiClient.Verify(x => x.GetAccessTokenAsync("test_client_id", "test_secret"), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        mockApiClient
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid client credentials"));

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.AuthenticateAsync("bad_id", "bad_secret");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid client credentials");
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidRefreshToken_ReturnsNewAccessToken()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        var newAccessToken = "new_access_token_abc";
        
        mockApiClient
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new QuickBooksAuthResult
            {
                AccessToken = newAccessToken,
                RefreshToken = "new_refresh_token",
                ExpiresIn = 3600,
                Success = true
            });

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.RefreshTokenAsync("old_refresh_token");

        // Assert
        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be(newAccessToken);
        result.RefreshToken.Should().NotBe("old_refresh_token");
    }

    #endregion

    #region Connection Tests

    [Fact]
    public async Task TestConnectionAsync_ValidConnection_ReturnsSuccess()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        mockApiClient
            .Setup(x => x.TestConnectionAsync())
            .ReturnsAsync(true);

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.TestConnectionAsync();

        // Assert
        result.Should().BeTrue();
        mockApiClient.Verify(x => x.TestConnectionAsync(), Times.Once);
    }

    [Fact]
    public async Task TestConnectionAsync_NetworkFailure_ReturnsFalse()
    {
        // Arrange
        var mockApiClient = new Mock<IQuickBooksApiClient>();
        mockApiClient
            .Setup(x => x.TestConnectionAsync())
            .ThrowsAsync(new HttpRequestException("Cannot reach QuickBooks servers"));

        var service = new QuickBooksService(mockApiClient.Object, _mockLogger.Object);

        // Act
        var result = await service.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once
        );
    }

    #endregion
}

// Mock DTOs for testing
public record QuickBooksInvoice
{
    public string Id { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
}

public record QuickBooksAuthResult
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public bool Success { get; init; }
    public string Error { get; init; } = string.Empty;
}

public record SyncResult
{
    public int SyncedCount { get; init; }
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
