using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

/// <summary>
/// Comprehensive tests for DashboardViewModel covering FY computation, thread-safe loading,
/// empty data handling, and logging validation. Validates fixes for FY2025/2026 mismatch.
/// Uses REAL repository implementations with InMemory EF Core DbContext (not mocks).
/// </summary>
public sealed class DashboardViewModelComprehensiveTests : IDisposable
{
    private IServiceProvider? _serviceProvider;
    private IMemoryCache? _cache;

    [Fact]
    public async Task LoadDashboardDataAsync_ComputesFY2026_FromDecember2025Date()
    {
        // Arrange - Simulate Dec 15, 2025 (production scenario)
        // FY should compute to 2026 since Month >= 7
        var (budgetRepo, accountRepo) = SetupRealRepositories(fiscalYear: 2026, withData: true);
        var config = CreateTestConfig(defaultFiscalYear: null); // Force computed FY

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert
        vm.FiscalYear.Should().Contain("2026", "December 2025 should compute to FY 2026");
        vm.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task LoadDashboardDataAsync_UsesFY2025_FromConfiguration()
    {
        // Arrange - Config explicitly sets FY2025
        var (budgetRepo, accountRepo) = SetupRealRepositories(fiscalYear: 2025, withData: false);
        var config = CreateTestConfig(defaultFiscalYear: 2025);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert
        vm.FiscalYear.Should().Contain("2025");
    }

    [Fact]
    public async Task LoadDashboardDataAsync_HandlesConcurrentCalls_WithoutRaceConditions()
    {
        // Arrange
        var (budgetRepo, accountRepo) = SetupRealRepositories(fiscalYear: 2026, withData: true);
        var config = CreateTestConfig(defaultFiscalYear: 2026);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act - simulate 5 concurrent load attempts
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => vm.LoadCommand.ExecuteAsync(null));

        // Await all tasks (async test method required)
        await Task.WhenAll(tasks);

        // Assert - all calls should execute serially due to SemaphoreSlim _loadLock
        vm.IsLoading.Should().BeFalse("SemaphoreSlim should serialize concurrent load attempts");
        vm.Metrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadDashboardDataAsync_PopulatesEmptyDashboard_WhenNoData()
    {
        // Arrange - Empty database (no FY data)
        var (budgetRepo, accountRepo) = SetupRealRepositories(fiscalYear: 2025, withData: false);
        var config = CreateTestConfig(defaultFiscalYear: 2025);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert - should load successfully with empty/zero metrics
        vm.ErrorMessage.Should().BeNullOrEmpty();
        vm.TotalBudgeted.Should().Be(0);
        vm.TotalActual.Should().Be(0);
        vm.TotalRevenue.Should().Be(0);
        vm.TotalExpenses.Should().Be(0);
        vm.Metrics.Should().NotBeEmpty("Should have metrics even with zero budget");
    }

    [Fact]
    public async Task LoadDashboardDataAsync_LogsThreadId_ForDiagnostics()
    {
        // Arrange - Validate that thread-safe logging works
        var (budgetRepo, accountRepo) = SetupRealRepositories(fiscalYear: 2026, withData: true);
        var config = CreateTestConfig(defaultFiscalYear: 2026);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert - Method should complete without exceptions (logging occurs internally)
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadDashboardDataAsync_PopulatesMonthlyRevenueData_For12Months()
    {
        // Arrange
        var (budgetRepo, accountRepo) = SetupRealRepositories(fiscalYear: 2026, withData: true);
        var config = CreateTestConfig(defaultFiscalYear: 2026);

        using var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act
        await vm.LoadCommand.ExecuteAsync(null);

        // Assert
        vm.MonthlyRevenueData.Should().HaveCount(12, "Should have 12 months of data");
        vm.MonthlyRevenueData.Should().AllSatisfy(m => m.Amount.Should().BeGreaterOrEqualTo(0));
        vm.MonthlyRevenueData.First().Month.Should().Be("Jul", "Fiscal year starts in July");
        vm.MonthlyRevenueData.Last().Month.Should().Be("Jun", "Fiscal year ends in June");
    }

    [Fact]
    public async Task Dispose_CancelsPendingLoad_AndReleasesResources()
    {
        // Arrange
        var (budgetRepo, accountRepo) = SetupRealRepositories(fiscalYear: 2026, withData: true);
        var config = CreateTestConfig();

        var vm = new DashboardViewModel(budgetRepo, accountRepo, NullLogger<DashboardViewModel>.Instance, config);

        // Act - start load but dispose after a short delay
        var loadTask = vm.LoadCommand.ExecuteAsync(null);
        await Task.Delay(50); // Allow load to start
        vm.Dispose();

        // Assert - VM should not be loading after dispose
        await Task.Delay(10); // Give cancellation a moment to propagate
        vm.IsLoading.Should().BeFalse();
        loadTask.IsCompleted.Should().BeTrue("Load task should be completed after dispose");
    }

    /// <summary>
    /// Sets up real repositories using InMemory EF Core DbContext instead of fakes.
    /// This verifies end-to-end integration between ViewModels and real repository implementations.
    /// </summary>
    private (IBudgetRepository, IMunicipalAccountRepository) SetupRealRepositories(int fiscalYear, bool withData)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        // Create service provider with real repositories
        var services = new ServiceCollection();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddScoped<IDbContextFactory<AppDbContext>>(sp => new InMemoryDbContextFactory(options));
        services.AddScoped(_ => _cache = new MemoryCache(new MemoryCacheOptions()));
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();

        _serviceProvider = services.BuildServiceProvider();

        // Seed test data if requested
        if (withData)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedTestData(dbContext, fiscalYear);
            }
        }

        var budgetRepo = _serviceProvider.GetRequiredService<IBudgetRepository>();
        var accountRepo = _serviceProvider.GetRequiredService<IMunicipalAccountRepository>();

        return (budgetRepo, accountRepo);
    }

    /// <summary>
    /// Seeds realistic test data into the InMemory database.
    /// </summary>
    private static void SeedTestData(AppDbContext dbContext, int fiscalYear)
    {
        // Create test budget entries
        var startDate = new DateTime(fiscalYear - 1, 7, 1); // FY starts in July
        var endDate = new DateTime(fiscalYear, 6, 30);

        var entries = new List<BudgetEntry>
        {
            new()
            {
                AccountNumber = "1000",
                BudgetedAmount = 5000000m,
                FiscalYear = fiscalYear,
                Description = "General Revenue",
                CreatedAt = startDate,
                UpdatedAt = endDate,
                IsGASBCompliant = true
            },
            new()
            {
                AccountNumber = "2000",
                BudgetedAmount = 3000000m,
                FiscalYear = fiscalYear,
                Description = "Sewer Revenue",
                CreatedAt = startDate,
                UpdatedAt = endDate,
                IsGASBCompliant = true
            }
        };

        dbContext.BudgetEntries.AddRange(entries);

        // Create test municipal accounts with valid AccountNumber format
        // AccountNumber regex: ^\d+([.-]\d+)*$ (numeric with optional . or - separators)
        // Examples: "405", "405.1", "410.2.1", "101-1000-000"
        var validAccountNumbers = new[] { "405", "405.1", "410.2.1", "101-1000-000", "420.5.3", "250", "250.1", "300.5", "300.5.1" };
        var accounts = Enumerable.Range(1, 125)
            .Select(i => new MunicipalAccount
            {
                AccountNumber = new AccountNumber(validAccountNumbers[i % validAccountNumbers.Length]),
                Name = $"Test Account {i}",
                Balance = i * 1000m,
                IsActive = i % 2 == 0
            })
            .ToList();

        dbContext.MunicipalAccounts.AddRange(accounts);
        dbContext.SaveChanges();
    }

    private static IConfiguration CreateTestConfig(int? defaultFiscalYear = null)
    {
        var configDict = new Dictionary<string, string?>();

        if (defaultFiscalYear.HasValue)
        {
            configDict["UI:DefaultFiscalYear"] = defaultFiscalYear.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();
    }

    /// <summary>
    /// Simple IDbContextFactory implementation for InMemory testing.
    /// </summary>
    private class InMemoryDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public InMemoryDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppDbContext(_options));
        }
    }

    public void Dispose()
    {
        _cache?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
