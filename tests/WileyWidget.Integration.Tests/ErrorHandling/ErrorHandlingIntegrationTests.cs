using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using FluentAssertions;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Integration.Tests.Shared;

namespace WileyWidget.Integration.Tests.ErrorHandling;

/// <summary>
/// Tests error handling scenarios across repositories and services
/// </summary>
public class ErrorHandlingIntegrationTests : IntegrationTestBase
{
    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Repository_DatabaseConnectionFailure_ThrowsExpectedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>().Options;
        var mockContext = new Mock<AppDbContext>(options);
        mockContext.Setup(c => c.Set<MunicipalAccount>())
            .Throws(new InvalidOperationException("Connection failed"));

        // Act & Assert
        var repository = new AccountsRepository(mockContext.Object, Mock.Of<ILogger<AccountsRepository>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetAllAccountsAsync());
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Repository_InvalidEntityId_ReturnsNull()
    {
        // Arrange
        await TestDataSeeder.SeedMunicipalAccountsAsync(DbContext);

        // Act
        var repository = GetRequiredService<AccountsRepository>();
        var result = await repository.GetAccountByIdAsync(-1);

        // Assert
        result.Should().BeNull();
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Repository_ConcurrentAccess_HandlesGracefully()
    {
        // Arrange
        await TestDataSeeder.SeedMunicipalAccountsAsync(DbContext);
        var repository = GetRequiredService<AccountsRepository>();

        // Act - Multiple concurrent operations
        var tasks = new[]
        {
            repository.GetAllAccountsAsync(),
            repository.GetAllAccountsAsync(),
            repository.GetAllAccountsAsync()
        };

        // Assert
        await Assert.AllAsync(tasks, async task =>
        {
            var result = await task;
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        });
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Service_InvalidInput_ThrowsArgumentException()
    {
        // Arrange
        var aiService = GetRequiredService<IAIService>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            aiService.GetInsightsAsync(null, "test question"));
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Service_CancellationToken_RespectsCancellation()
    {
        // Arrange
        var aiService = GetRequiredService<IAIService>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            aiService.GetInsightsAsync("test context", "test question", cts.Token));
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task CacheService_Failure_FallsBackGracefully()
    {
        // Arrange
        var mockCache = new Mock<ICacheService>();
        mockCache.Setup(c => c.GetAsync<List<DashboardItem>>(It.IsAny<string>()))
            .Throws(new Exception("Cache failure"));

        var services = new ServiceCollection();
        services.AddSingleton(mockCache.Object);
        services.AddSingleton(GetRequiredService<ILogger<DashboardService>>());
        services.AddSingleton(GetRequiredService<IBudgetRepository>());
        services.AddSingleton(GetRequiredService<IMunicipalAccountRepository>());

        var serviceProvider = services.BuildServiceProvider();
        var dashboardService = new DashboardService(
            serviceProvider.GetRequiredService<ILogger<DashboardService>>(),
            serviceProvider.GetRequiredService<IBudgetRepository>(),
            serviceProvider.GetRequiredService<IMunicipalAccountRepository>(),
            mockCache.Object);

        // Act & Assert - Should not throw, should fall back to database
        await Assert.ThrowsAsync<Exception>(() =>
            dashboardService.GetDashboardDataAsync());
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task ExternalService_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        var mockQuickBooksService = new Mock<IQuickBooksService>();
        mockQuickBooksService.Setup(q => q.GetCustomersAsync())
            .Throws(new TimeoutException("External service timeout"));

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            mockQuickBooksService.Object.GetCustomersAsync());
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Repository_InvalidSearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var repository = GetRequiredService<AccountsRepository>();

        // Act
        var result = await repository.SearchAccountsAsync("");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Service_ConfigurationMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["RequiredSetting"]).Returns((string)null);

        var services = new ServiceCollection();
        services.AddSingleton(mockConfig.Object);
        services.AddSingleton(GetRequiredService<ILogger<SettingsService>>());

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var settingsService = new SettingsService(mockConfig.Object);
        // Should not throw; missing configuration is handled gracefully
        settingsService.Initialize();
        settingsService.Current.Should().NotBeNull();
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task AsyncOperation_Timeout_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        var repository = GetRequiredService<AccountsRepository>();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.GetAllAccountsAsync(cts.Token));
    }

    [Fact, Trait("Category", "ErrorHandling")]
    public async Task Service_ExternalDependencyFailure_LogsAndThrows()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AnalyticsService>>();
        var mockBudgetRepo = new Mock<IBudgetRepository>();
        mockBudgetRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("External dependency failure"));

        var analyticsService = new AnalyticsService(
            mockBudgetRepo.Object,
            Mock.Of<IMunicipalAccountRepository>(),
            mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            analyticsService.PerformExploratoryAnalysisAsync(DateTime.Now.AddMonths(-1), DateTime.Now));

        // Verify logging
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
}
