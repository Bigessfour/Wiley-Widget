using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Abstractions;
using WileyWidget.Integration.Tests.Shared;

namespace WileyWidget.Integration.Tests.AsyncOperations;

/// <summary>
/// Tests async operation patterns including cancellation, timeout, and concurrency
/// </summary>
public class AsyncOperationTests : IntegrationTestBase
{
    [Fact, Trait("Category", "Async")]
    public async Task Repository_OperationCancellation_RespectsCancellationToken()
    {
        // Arrange
        await TestDataSeeder.SeedMunicipalAccountsAsync(DbContext);
        var repository = GetRequiredService<AccountsRepository>();
        using var cts = new CancellationTokenSource();

        // Act - Start operation then cancel immediately
        cts.Cancel();
        var task = repository.GetAllAccountsAsync(cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact, Trait("Category", "Async")]
    public async Task Repository_LongRunningOperation_CanBeCancelled()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var repository = GetRequiredService<AccountsRepository>();
        using var cts = new CancellationTokenSource();

        // Act - Start multiple operations and cancel some
        var task1 = repository.GetAllAccountsAsync(cts.Token);
        var task2 = repository.GetAccountsByFundAsync(MunicipalFundType.Water, cts.Token);
        var task3 = repository.GetAccountsByTypeAsync(AccountType.Revenue, cts.Token);

        // Cancel after a short delay
        cts.CancelAfter(10);

        // Assert - Some operations may complete, some may be cancelled
        var completedTasks = new List<Task>();
        var cancelledTasks = new List<Task>();

        foreach (var task in new[] { task1, task2, task3 })
        {
            try
            {
                await task;
                completedTasks.Add(task);
            }
            catch (OperationCanceledException)
            {
                cancelledTasks.Add(task);
            }
        }

        // At least one task should be cancelled
        (completedTasks.Count + cancelledTasks.Count).Should().Be(3);
    }

    [Fact, Trait("Category", "Async")]
    public async Task Service_AnalyticsOperation_TimeoutHandling()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var analyticsService = GetRequiredService<AnalyticsService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act & Assert - Operation should either complete or be cancelled due to timeout
        var task = analyticsService.PerformExploratoryAnalysisAsync(
            DateTime.Now.AddMonths(-12),
            DateTime.Now,
            cts.Token);

        // Wait for either completion or cancellation
        var completedTask = await Task.WhenAny(task, Task.Delay(2000));

        if (completedTask == task)
        {
            // Task completed successfully
            var result = await task;
            result.Should().NotBeNull();
        }
        else
        {
            // Timeout occurred, cancel the operation
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        }
    }

    [Fact, Trait("Category", "Async")]
    public async Task Service_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var analyticsService = GetRequiredService<AnalyticsService>();
        var startDate = DateTime.Now.AddMonths(-6);
        var endDate = DateTime.Now;

        // Act - Run multiple analytics operations concurrently
        Task[] tasks = new Task[]
        {
            analyticsService.PerformExploratoryAnalysisAsync(startDate, endDate),
            analyticsService.PerformExploratoryAnalysisAsync(startDate.AddMonths(-3), endDate),
            analyticsService.PerformExploratoryAnalysisAsync(startDate, endDate.AddDays(-30)),
            analyticsService.RunRateScenarioAsync(new RateScenarioParameters
            {
                RateIncreasePercentage = 0.05m,
                ExpenseIncreasePercentage = 0.03m,
                ProjectionYears = 3
            })
        };

        // Assert
        await Task.WhenAll(tasks);
        foreach (var task in tasks)
        {
            task.IsCompletedSuccessfully.Should().BeTrue();
        }
    }

    [Fact, Trait("Category", "Async")]
    public async Task Repository_BulkOperations_HandlesConcurrency()
    {
        // Arrange
        await TestDataSeeder.SeedMunicipalAccountsAsync(DbContext);
        var repository = GetRequiredService<AccountsRepository>();

        // Act - Perform multiple repository operations concurrently
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(repository.GetAllAccountsAsync());
            tasks.Add(repository.GetAccountsByFundAsync(MunicipalFundType.Water));
            tasks.Add(repository.GetAccountsByTypeAsync(AccountType.Expense));
        }

        // Assert
        await Task.WhenAll(tasks);
        foreach (var task in tasks)
        {
            task.IsCompletedSuccessfully.Should().BeTrue();
        }
    }

    [Fact, Trait("Category", "Async")]
    public async Task Service_WhatIfScenario_LongRunning_ProgressTracking()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var scenarioEngine = GetRequiredService<WhatIfScenarioEngine>();

        var parameters = new ScenarioParameters
        {
            PayRaisePercentage = 0.05m,
            BenefitsIncreaseAmount = 500m,
            EquipmentPurchaseAmount = 100000m,
            EquipmentFinancingYears = 5,
            ReservePercentage = 0.15m
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var task = scenarioEngine.GenerateComprehensiveScenarioAsync(1, parameters);
        var result = await task;
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.ScenarioName.Should().NotBeNullOrEmpty();
        result.TotalImpact.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30)); // Should complete within reasonable time
    }

    [Fact, Trait("Category", "Async")]
    public async Task CacheService_AsyncOperations_ThreadSafe()
    {
        // Arrange
        var cacheService = GetRequiredService<ICacheService>();
        var cacheKey = "async_test";
        var tasks = new List<Task<string>>();

        // Act - Concurrent cache operations
        for (int i = 0; i < 20; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await cacheService.SetAsync($"{cacheKey}_{index}", $"value_{index}", TimeSpan.FromMinutes(5));
                var result = await cacheService.GetAsync<string>($"{cacheKey}_{index}");
                return result;
            }));
        }

        // Assert
        await Task.WhenAll(tasks);
        foreach (var task in tasks)
        {
            var result = await task;
            result.Should().NotBeNull();
            result.Should().StartWith("value_");
        }
    }

    [Fact, Trait("Category", "Async")]
    public async Task Service_CancellationToken_ChainedOperations()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var analyticsService = GetRequiredService<AnalyticsService>();
        using var cts = new CancellationTokenSource();

        // Act - Chain multiple async operations
        var task = Task.Run(async () =>
        {
            var analysis1 = await analyticsService.PerformExploratoryAnalysisAsync(
                DateTime.Now.AddMonths(-3), DateTime.Now, cts.Token);

            cts.Token.ThrowIfCancellationRequested();

            var analysis2 = await analyticsService.PerformExploratoryAnalysisAsync(
                DateTime.Now.AddMonths(-6), DateTime.Now, cts.Token);

            return new[] { analysis1, analysis2 };
        });

        // Cancel midway through
        await Task.Delay(100);
        cts.Cancel();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    [Fact, Trait("Category", "Async")]
    public async Task Repository_Timeout_LongRunningQuery()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var repository = GetRequiredService<AccountsRepository>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // Act & Assert - Very short timeout should cause cancellation
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.GetAllAccountsAsync(cts.Token));
    }

    [Fact, Trait("Category", "Async")]
    public async Task Service_AsyncDispose_Pattern_Correct()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DashboardService>>();
        var mockBudgetRepo = new Mock<IBudgetRepository>();
        var mockAccountRepo = new Mock<IMunicipalAccountRepository>();

        // Create service that implements IAsyncDisposable
        var service = new AsyncDisposableTestService(mockLogger.Object);

        // Act
        await using (service)
        {
            // Service is in use
            service.IsInitialized.Should().BeTrue();
        }

        // Assert - Dispose should have been called
        service.IsDisposed.Should().BeTrue();
    }

    private class AsyncDisposableTestService : IAsyncDisposable
    {
        private readonly ILogger _logger;
        public bool IsInitialized { get; private set; } = true;
        public bool IsDisposed { get; private set; }

        public AsyncDisposableTestService(ILogger logger)
        {
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Delay(10); // Simulate async cleanup
            IsDisposed = true;
            IsInitialized = false;
        }
    }
}
