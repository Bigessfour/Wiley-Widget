using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.Data;
using WileyWidget.Abstractions;
using WileyWidget.Integration.Tests.Shared;
using WileyWidget.Integration.Tests.ExternalServices;

namespace WileyWidget.Integration.Tests.Performance;

/// <summary>
/// Performance benchmark tests for critical services to ensure they handle large datasets efficiently
/// </summary>
public class PerformanceBenchmarkTests : IntegrationTestBase
{
    [Fact, Trait("Category", "Performance")]
    public async Task AnalyticsService_LargeDataset_PerformanceWithinLimits()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);

        // Add additional large dataset
        await AddLargeBudgetDatasetAsync(1000); // 1000 budget entries

        var analyticsService = GetRequiredService<AnalyticsService>();
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = await analyticsService.PerformExploratoryAnalysisAsync(
            DateTime.Now.AddYears(-2),
            DateTime.Now);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "Analytics should complete within 5 seconds for large datasets");
        result.CategoryBreakdown.Should().NotBeEmpty();
        result.TopVariances.Should().HaveCountLessOrEqualTo(10);
    }

    [Fact, Trait("Category", "Performance")]
    public async Task WhatIfScenarioEngine_ComplexScenario_PerformanceTest()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var scenarioEngine = GetRequiredService<WhatIfScenarioEngine>();

        var parameters = new ScenarioParameters
        {
            PayRaisePercentage = 0.08m,
            BenefitsIncreaseAmount = 1500m,
            EquipmentPurchaseAmount = 500000m,
            EquipmentFinancingYears = 7,
            ReservePercentage = 0.20m
        };

        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var result = await scenarioEngine.GenerateComprehensiveScenarioAsync(1, parameters);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3), "Complex scenario should complete within 3 seconds");
        result.TotalImpact.TotalAnnualExpenseIncrease.Should().BeGreaterThan(0);
        result.Projections.Should().HaveCount(5); // 5 year projection
    }

    [Fact, Trait("Category", "Performance")]
    public async Task DashboardService_CachedData_FastRetrieval()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var dashboardService = GetRequiredService<DashboardService>();

        // First call to populate cache
        await dashboardService.GetDashboardDataAsync();

        var stopwatch = new Stopwatch();

        // Act - Second call should use cache
        stopwatch.Start();
        var result = await dashboardService.GetDashboardDataAsync();
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100), "Cached dashboard data should retrieve in under 100ms");
    }

    [Fact, Trait("Category", "Performance")]
    public async Task Repository_BulkOperations_EfficientProcessing()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var repository = GetRequiredService<AccountsRepository>();

        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var allAccounts = await repository.GetAllAccountsAsync();
        var waterAccounts = await repository.GetAccountsByFundAsync(MunicipalFundType.Water);
        var expenseAccounts = await repository.GetAccountsByTypeAsync(AccountType.Expense);
        stopwatch.Stop();

        // Assert
        allAccounts.Should().NotBeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "Bulk repository operations should complete within 2 seconds");
        waterAccounts.Count().Should().BeGreaterThan(0);
        expenseAccounts.Count().Should().BeGreaterThan(0);
    }

    [Fact, Trait("Category", "Performance")]
    public async Task ServiceChargeCalculator_LargeEnterprise_CalculationSpeed()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        var calculator = GetRequiredService<ServiceChargeCalculatorService>();

        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var recommendation = await calculator.CalculateRecommendedChargeAsync(1);
        stopwatch.Stop();

        // Assert
        recommendation.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "Service charge calculation should complete within 2 seconds");
        recommendation.RecommendedRate.Should().BeGreaterThan(0);
    }

    [Fact, Trait("Category", "Performance")]
    public async Task CacheService_ConcurrentAccess_PerformanceUnderLoad()
    {
        // Arrange
        var cacheService = GetRequiredService<ICacheService>();
        const int concurrentOperations = 50;
        var tasks = new List<Task<string>>();
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        for (int i = 0; i < concurrentOperations; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await cacheService.SetAsync($"perf_test_{index}", $"value_{index}", TimeSpan.FromMinutes(5));
                var result = await cacheService.GetAsync<string>($"perf_test_{index}");
                return result;
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "Concurrent cache operations should complete within 5 seconds");
        foreach (var task in tasks)
        {
            var result = await task;
            result.Should().NotBeNull();
            result.Should().StartWith("value_");
        }
    }

    [Fact, Trait("Category", "Performance")]
    public async Task AnalyticsService_MemoryUsage_UnderControl()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        await AddLargeBudgetDatasetAsync(5000); // Very large dataset

        var analyticsService = GetRequiredService<AnalyticsService>();
        var beforeMemory = GC.GetTotalMemory(true);

        // Act
        var result = await analyticsService.PerformExploratoryAnalysisAsync(
            DateTime.Now.AddYears(-3),
            DateTime.Now);

        var afterMemory = GC.GetTotalMemory(false);
        GC.Collect(); // Force garbage collection
        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        result.Should().NotBeNull();
        // Memory usage should not grow excessively (less than 50MB additional)
        (finalMemory - beforeMemory).Should().BeLessThan(50L * 1024 * 1024, "Memory usage should be under 50MB for analytics processing");
    }

    [Fact, Trait("Category", "Performance")]
    public async Task Repository_SearchPerformance_LargeDataset()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);
        await AddLargeAccountDatasetAsync(1000); // 1000 accounts

        var repository = GetRequiredService<AccountsRepository>();
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        var searchResults = await repository.SearchAccountsAsync("water");
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "Account search should complete within 1 second");
        searchResults.Count().Should().BeGreaterThan(0);
    }

    [Fact, Trait("Category", "Performance")]
    public async Task ExternalService_Timeout_PreventsHanging()
    {
        // Arrange
        var mockQuickBooksService = ExternalServiceMockFactory.CreateQuickBooksServiceMock(
            new QuickBooksMockConfig
            {
                ResponseDelay = TimeSpan.FromSeconds(30) // Very slow response
            });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // 2 second timeout
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            mockQuickBooksService.Object.GetCustomersAsync(cts.Token));
        stopwatch.Stop();

        // Assert - Should fail quickly due to timeout, not hang
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3), "Timeout should prevent hanging operations");
    }

    private async Task AddLargeBudgetDatasetAsync(int entryCount)
    {
        var accounts = await DbContext.Set<MunicipalAccount>().ToListAsync();
        var budgetEntries = new List<BudgetEntry>();

        for (int i = 0; i < entryCount; i++)
        {
            var account = accounts[i % accounts.Count];
            var fiscalYear = DateTime.Now.Year - (i % 3); // Spread across 3 years

                budgetEntries.Add(new BudgetEntry
                {
                    // Some seeded accounts may store the account number in the owned
                    // `AccountNumber` object or in the flattened `AccountNumber_Value`.
                    // Use whichever is present, with a safe fallback to a perf id.
                    AccountNumber = account.AccountNumber?.Value ?? account.AccountNumber_Value ?? $"PERF-{i}",
                FiscalYear = fiscalYear,
                StartPeriod = new DateTime(fiscalYear - 1, 7, 1),
                EndPeriod = new DateTime(fiscalYear, 6, 30),
                BudgetedAmount = account.BudgetAmount / 12,
                ActualAmount = (account.BudgetAmount / 12) * (0.8m + (decimal)Random.Shared.NextDouble() * 0.4m),
                Description = $"Performance test entry {i}",
                DepartmentCode = "PERF",
                CreatedAt = DateTime.Now.AddDays(-Random.Shared.Next(365))
            });
        }

        await DbContext.BudgetEntries.AddRangeAsync(budgetEntries);
        await DbContext.SaveChangesAsync();
    }

    private async Task AddLargeAccountDatasetAsync(int accountCount)
    {
        var accounts = new List<MunicipalAccount>();

        for (int i = 0; i < accountCount; i++)
        {
            accounts.Add(new MunicipalAccount
            {
                AccountNumber_Value = $"PERF-{i:0000}",
                Name = $"Performance Test Account {i}",
                Type = i % 2 == 0 ? AccountType.Revenue : AccountType.Expense,
                Fund = (MunicipalFundType)(i % 4),
                FundDescription = $"{(MunicipalFundType)(i % 4)} Fund",
                BudgetAmount = 10000 + (i * 100),
                IsActive = true
            });
        }

        await DbContext.Set<MunicipalAccount>().AddRangeAsync(accounts);
        await DbContext.SaveChangesAsync();
    }
}
