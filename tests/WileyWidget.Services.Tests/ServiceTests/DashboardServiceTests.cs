using Xunit;
using Moq;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using WileyWidget.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Services.Tests.ServiceTests;

/// <summary>
/// Tests for DashboardService - data aggregation, caching, and dashboard item generation.
/// Validates business logic for budget summaries, fiscal year calculations, and caching behavior.
/// </summary>
public sealed class DashboardServiceTests : IDisposable
{
    private readonly Mock<IBudgetRepository> _mockBudgetRepository;
    private readonly Mock<IMunicipalAccountRepository> _mockAccountRepository;
    private readonly Mock<ILogger<DashboardService>> _mockLogger;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _mockBudgetRepository = new Mock<IBudgetRepository>();
        _mockAccountRepository = new Mock<IMunicipalAccountRepository>();
        _mockLogger = new Mock<ILogger<DashboardService>>();
        _mockCacheService = new Mock<ICacheService>();
        _mockConfiguration = new Mock<IConfiguration>();

        _service = new DashboardService(
            _mockBudgetRepository.Object,
            _mockAccountRepository.Object,
            _mockLogger.Object,
            _mockCacheService.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenBudgetRepositoryIsNull()
    {
        // Act
#pragma warning disable CA1806 // Constructor creates object that is never used - intentional for exception testing
        Action act = () => new DashboardService(
            null!,
            _mockAccountRepository.Object,
            _mockLogger.Object,
            _mockCacheService.Object,
            _mockConfiguration.Object);
#pragma warning restore CA1806

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("budgetRepository");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenAccountRepositoryIsNull()
    {
        // Act
#pragma warning disable CA1806 // Constructor creates object that is never used - intentional for exception testing
        Action act = () => new DashboardService(
            _mockBudgetRepository.Object,
            null!,
            _mockLogger.Object,
            _mockCacheService.Object,
            _mockConfiguration.Object);
#pragma warning restore CA1806

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("accountRepository");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
#pragma warning disable CA1806 // Constructor creates object that is never used - intentional for exception testing
        Action act = () => new DashboardService(
            _mockBudgetRepository.Object,
            _mockAccountRepository.Object,
            null!,
            _mockCacheService.Object);
#pragma warning restore CA1806

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WhenCacheServiceIsNull()
    {
        // Act
#pragma warning disable CA1806 // Constructor creates object that is never used - intentional for exception testing
        Action act = () => new DashboardService(
            _mockBudgetRepository.Object,
            _mockAccountRepository.Object,
            _mockLogger.Object,
            null);
#pragma warning restore CA1806

        // Assert
        act.Should().NotThrow("cache service is optional");
    }

    [Fact]
    public async Task GetDashboardData_ShouldUseCacheOnSubsequentCalls_WhenWithinExpiration()
    {
        // Arrange - Setup mocks for fetch
        _mockBudgetRepository
            .Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestBudgetAnalysis());

        _mockAccountRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MunicipalAccount>());

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry>());

        // Mock cache to always return null initially, then return previously cached items
        var cachedItems = new List<DashboardItem>();
        _mockCacheService
            .Setup(c => c.GetAsync<List<DashboardItem>>("dashboard_items"))
            .ReturnsAsync(() => cachedItems.Count > 0 ? cachedItems : null);

        _mockCacheService
            .Setup(c => c.SetAsync("dashboard_items", It.IsAny<List<DashboardItem>>(), It.IsAny<TimeSpan?>()))
            .Callback<string, List<DashboardItem>, TimeSpan?>((_, items, _) =>
            {
                cachedItems.Clear();
                cachedItems.AddRange(items);
            })
            .Returns(Task.CompletedTask);

        // Act - First call populates cache
        var firstResult = await _service.GetDashboardDataAsync();

        // Act - Second call should use cache
        var secondResult = await _service.GetDashboardDataAsync();

        // Assert
        firstResult.Should().NotBeEmpty();
        secondResult.Should().HaveCount(firstResult.Count());  // Same data from cache

        // Verify repositories were called only once (first call, cache used on second)
        _mockBudgetRepository.Verify(
            r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDashboardData_ShouldFetchFreshData_WhenCacheIsEmpty()
    {
        // Arrange
        _mockCacheService
            .Setup(c => c.GetAsync<List<DashboardItem>>("dashboard_items"))
            .ReturnsAsync((List<DashboardItem>?)null);

        var budgetAnalysis = CreateTestBudgetAnalysis();
        _mockBudgetRepository
            .Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgetAnalysis);

        _mockAccountRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MunicipalAccount>());

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry>());

        // Act
        var result = await _service.GetDashboardDataAsync();

        // Assert
        result.Should().NotBeEmpty();

        // Verify cache was set
        _mockCacheService.Verify(
            c => c.SetAsync("dashboard_items", It.IsAny<List<DashboardItem>>(), It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshDashboard_ShouldClearCache_AndFetchFreshData()
    {
        // Arrange
        var budgetAnalysis = CreateTestBudgetAnalysis();
        _mockBudgetRepository
            .Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgetAnalysis);

        _mockAccountRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MunicipalAccount>());

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry>());

        // Act
        await _service.RefreshDashboardAsync();

        // Assert
        _mockCacheService.Verify(c => c.RemoveAsync("dashboard_items"), Times.Once);
        _mockBudgetRepository.Verify(
            r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDashboardItems_ShouldReturnSameAsGetDashboardData()
    {
        // Arrange - setup minimal mocks
        _mockCacheService
            .Setup(c => c.GetAsync<List<DashboardItem>>("dashboard_items"))
            .ReturnsAsync((List<DashboardItem>?)null);

        var budgetAnalysis = CreateTestBudgetAnalysis();
        _mockBudgetRepository
            .Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgetAnalysis);

        _mockAccountRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MunicipalAccount> { new() { Id = 1 } });

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry>());

        // Act
        var itemsResult = await _service.GetDashboardItemsAsync();
        var dataResult = await _service.GetDashboardDataAsync();

        // Assert - both methods should return equivalent data
        itemsResult.Count().Should().Be(dataResult.Count());
    }

    [Fact]
    public async Task GetDashboardData_ShouldIncludeBudgetSummaryItems_WhenDataExists()
    {
        // Arrange
        _mockCacheService
            .Setup(c => c.GetAsync<List<DashboardItem>>("dashboard_items"))
            .ReturnsAsync((List<DashboardItem>?)null);

        var budgetAnalysis = new BudgetVarianceAnalysis
        {
            TotalBudgeted = 1000000m,
            TotalActual = 850000m,
            TotalVariance = 150000m,
            TotalVariancePercentage = 15m,
            FundSummaries = new List<FundSummary>
            {
                new() { FundName = "General Fund", Budgeted = 500000m, Actual = 450000m, Variance = 50000m }
            }
        };

        _mockBudgetRepository
            .Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgetAnalysis);

        _mockAccountRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MunicipalAccount>());

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry>());

        // Act
        var result = await _service.GetDashboardDataAsync();
        var items = result.ToList();

        // Assert
        items.Should().Contain(i => i.Title == "Total Budget" && i.Value.Contains("1,000,000"));
        items.Should().Contain(i => i.Title == "Total Actual" && i.Value.Contains("850,000"));
        items.Should().Contain(i => i.Title == "Variance");
        items.Should().Contain(i => i.Title.StartsWith("Fund:"));
    }

    [Fact]
    public async Task GetDashboardData_ShouldHandleNullBudgetSummary_Gracefully()
    {
        // Arrange
        _mockCacheService
            .Setup(c => c.GetAsync<List<DashboardItem>>("dashboard_items"))
            .ReturnsAsync((List<DashboardItem>?)null);

        _mockBudgetRepository
            .Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BudgetVarianceAnalysis)null!);

        _mockAccountRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MunicipalAccount> { new() { Id = 1 } });

        _mockBudgetRepository
            .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetEntry>());

        // Act
        var result = await _service.GetDashboardDataAsync();

        // Assert - should still return some items (account count, activity)
        result.Should().NotBeEmpty();
        result.Should().Contain(i => i.Title == "Active Accounts");
    }

    private static BudgetVarianceAnalysis CreateTestBudgetAnalysis()
    {
        return new BudgetVarianceAnalysis
        {
            TotalBudgeted = 1000000m,
            TotalActual = 900000m,
            TotalVariance = 100000m,
            TotalVariancePercentage = 10m,
            FundSummaries = new List<FundSummary>
            {
                new() { FundName = "General", Budgeted = 500000m, Actual = 450000m, Variance = 50000m },
                new() { FundName = "Sewer", Budgeted = 300000m, Actual = 280000m, Variance = 20000m }
            }
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}


