#nullable enable

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.Services.Tests.ServiceTests;

/// <summary>
/// Tests for BudgetRepository - Phase 3 of test generation
/// Coverage Target: 70%+ (currently 0%)
/// High CRAP Methods: ApplySorting (930), GetByFiscalYearAsync (812), GetByDateRangeAsync (702)
/// </summary>
public class BudgetRepositoryTests : IDisposable
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ITelemetryService _telemetryService;
    private readonly DbContextOptions<AppDbContext> _options;

    public BudgetRepositoryTests()
    {
        // Setup InMemory database with shared options
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // Unique DB per test
            .Options;

        _contextFactory = new TestDbContextFactory(_options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _telemetryService = new MockTelemetryService();
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContextFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new BudgetRepository(null!, _cache, _telemetryService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("contextFactory");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new BudgetRepository(_contextFactory, null!, _telemetryService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    #endregion

    #region Query Tests - Basic

    [Fact]
    public async Task GetByFiscalYearAsync_WithValidYear_ReturnsBudgetEntries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedTestData();

        // Act
        var result = await repository.GetByFiscalYearAsync(2026);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(be => be.FiscalYear == 2026).Should().BeTrue();
    }

    [Fact]
    public async Task GetByFiscalYearAsync_UsesCacheOnSecondCall_DoesNotQueryDatabase()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedTestData();

        // Act - First call populates cache
        var result1 = await repository.GetByFiscalYearAsync(2026);

        // Modify database after first call
        await using (var context = _contextFactory.CreateDbContext())
        {
            var newEntry = CreateTestBudgetEntry(fiscalYear: 2026);
            context.BudgetEntries.Add(newEntry);
            await context.SaveChangesAsync();
        }

        // Second call should use cache (not see new entry)
        var result2 = await repository.GetByFiscalYearAsync(2026);

        // Assert
        result1.Count().Should().Be(result2.Count());
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsBudgetEntry()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        var testEntry = await SeedTestData();

        // Act
        var result = await repository.GetByIdAsync(testEntry.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(testEntry.Id);
        result.AccountNumber.Should().Be(testEntry.AccountNumber);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);

        // Act
        var result = await repository.GetByIdAsync(999999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByDateRangeAsync_WithValidRange_ReturnsBudgetEntries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedTestData();
        var startDate = new DateTime(2025, 7, 1);
        var endDate = new DateTime(2026, 6, 30);

        // Act
        var result = await repository.GetByDateRangeAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region Query Tests - Advanced

    [Fact]
    public async Task GetByFundAsync_WithValidFund_ReturnsBudgetEntries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        var testEntry = await SeedTestData();

        // Act
        var result = await repository.GetByFundAsync(testEntry.FundId!.Value);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(be => be.FundId == testEntry.FundId).Should().BeTrue();
    }

    [Fact]
    public async Task GetByDepartmentAsync_WithValidDepartment_ReturnsBudgetEntries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        var testEntry = await SeedTestData();

        // Act
        var result = await repository.GetByDepartmentAsync(testEntry.DepartmentId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(be => be.DepartmentId == testEntry.DepartmentId).Should().BeTrue();
    }

    [Fact]
    public async Task GetByFundAndFiscalYearAsync_WithValidParameters_ReturnsBudgetEntries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        var testEntry = await SeedTestData();

        // Act
        var result = await repository.GetByFundAndFiscalYearAsync(testEntry.FundId!.Value, 2026);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(be => be.FundId == testEntry.FundId && be.FiscalYear == 2026).Should().BeTrue();
    }

    [Fact]
    public async Task GetSewerBudgetEntriesAsync_WithValidYear_ReturnsSewerFundEntries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedSewerFundData();

        // Act
        var result = await repository.GetSewerBudgetEntriesAsync(2026);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(be => be.FundId == 2 && be.FiscalYear == 2026).Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_WithSorting_ReturnsCorrectlySortedAndPagedResults()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedMultipleTestEntries();

        // Act
        var (items, totalCount) = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 5,
            sortBy: "budgetedamount",
            sortDescending: true,
            fiscalYear: 2026);

        // Assert
        items.Should().NotBeNull();
        items.Should().HaveCount(5);
        totalCount.Should().BeGreaterThanOrEqualTo(5);
        items.Should().BeInDescendingOrder(be => be.BudgetedAmount);
    }

    #endregion

    #region CRUD Tests

    [Fact]
    public async Task AddAsync_WithValidBudgetEntry_AddsToDatabase()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        var newEntry = CreateTestBudgetEntry();

        // Act
        await repository.AddAsync(newEntry);

        // Assert
        await using var context = _contextFactory.CreateDbContext();
        var saved = await context.BudgetEntries.FirstOrDefaultAsync(be => be.AccountNumber == newEntry.AccountNumber);
        saved.Should().NotBeNull();
        saved!.BudgetedAmount.Should().Be(newEntry.BudgetedAmount);
    }

    [Fact]
    public async Task AddAsync_WithNullBudgetEntry_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);

        // Act
        Func<Task> act = async () => await repository.AddAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("budgetEntry");
    }

    [Fact]
    public async Task UpdateAsync_WithValidBudgetEntry_UpdatesDatabase()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        var testEntry = await SeedTestData();
        testEntry.BudgetedAmount = 999999.99m;

        // Act
        await repository.UpdateAsync(testEntry);

        // Assert
        await using var context = _contextFactory.CreateDbContext();
        var updated = await context.BudgetEntries.FirstOrDefaultAsync(be => be.Id == testEntry.Id);
        updated.Should().NotBeNull();
        updated!.BudgetedAmount.Should().Be(999999.99m);
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_RemovesBudgetEntry()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        var testEntry = await SeedTestData();

        // Act
        await repository.DeleteAsync(testEntry.Id);

        // Assert
        await using var context = _contextFactory.CreateDbContext();
        var deleted = await context.BudgetEntries.FirstOrDefaultAsync(be => be.Id == testEntry.Id);
        deleted.Should().BeNull();
    }

    #endregion

    #region Aggregation Tests

    [Fact]
    public async Task GetBudgetSummaryAsync_WithValidDateRange_ReturnsVarianceAnalysis()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedTestData();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await repository.GetBudgetSummaryAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.TotalBudgeted.Should().BeGreaterThan(0);
        result.FundSummaries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetDepartmentBreakdownAsync_WithValidDateRange_ReturnsDepartmentSummaries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedTestData();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await repository.GetDepartmentBreakdownAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(d => d.DepartmentName != null).Should().BeTrue();
    }

    [Fact]
    public async Task GetFundAllocationsAsync_WithValidDateRange_ReturnsFundSummaries()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedTestData();
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await repository.GetFundAllocationsAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(f => f.FundName != null).Should().BeTrue();
    }

    [Fact]
    public async Task GetYearEndSummaryAsync_WithValidYear_ReturnsAnnualSummary()
    {
        // Arrange
        var repository = new BudgetRepository(_contextFactory, _cache, _telemetryService);
        await SeedTestData();

        // Act
        var result = await repository.GetYearEndSummaryAsync(2025);

        // Assert
        result.Should().NotBeNull();
        result.BudgetPeriod.Should().Contain("2025");
    }

    #endregion

    #region Helper Methods

    private async Task<BudgetEntry> SeedTestData()
    {
        await using var context = _contextFactory.CreateDbContext();

        var department = new Department { Id = 1, DepartmentCode = "PW", Name = "Public Works" };
        var fund = new Fund { Id = 1, FundCode = "GF", Name = "General Fund", Type = FundType.GeneralFund };

        context.Departments.Add(department);
        context.Funds.Add(fund);
        await context.SaveChangesAsync();

        var entry = CreateTestBudgetEntry(fiscalYear: 2026, fundId: fund.Id, departmentId: department.Id);
        context.BudgetEntries.Add(entry);
        await context.SaveChangesAsync();

        return entry;
    }

    private async Task SeedSewerFundData()
    {
        await using var context = _contextFactory.CreateDbContext();

        var department = new Department { Id = 2, DepartmentCode = "SWR", Name = "Sewer" };
        var fund = new Fund { Id = 2, FundCode = "EF", Name = "Enterprise Fund", Type = FundType.EnterpriseFund };

        context.Departments.Add(department);
        context.Funds.Add(fund);
        await context.SaveChangesAsync();

        var entry = CreateTestBudgetEntry(fiscalYear: 2026, fundId: fund.Id, departmentId: department.Id);
        context.BudgetEntries.Add(entry);
        await context.SaveChangesAsync();
    }

    private async Task SeedMultipleTestEntries()
    {
        await using var context = _contextFactory.CreateDbContext();

        var department = new Department { Id = 3, DepartmentCode = "FIN", Name = "Finance" };
        var fund = new Fund { Id = 3, FundCode = "GF", Name = "General Fund", Type = FundType.GeneralFund };

        context.Departments.Add(department);
        context.Funds.Add(fund);
        await context.SaveChangesAsync();

        for (int i = 1; i <= 10; i++)
        {
            var entry = CreateTestBudgetEntry(
                fiscalYear: 2026,
                fundId: fund.Id,
                departmentId: department.Id,
                budgetedAmount: i * 10000m);
            context.BudgetEntries.Add(entry);
        }
        await context.SaveChangesAsync();
    }

    private BudgetEntry CreateTestBudgetEntry(
        int fiscalYear = 2026,
        int fundId = 1,
        int departmentId = 1,
        decimal budgetedAmount = 50000m)
    {
        return new BudgetEntry
        {
            FiscalYear = fiscalYear,
            AccountNumber = $"{100 + fiscalYear}.{Guid.NewGuid().ToString()[..2]}",
            Description = "Test Budget Entry",
            BudgetedAmount = budgetedAmount,
            ActualAmount = budgetedAmount * 0.8m,
            FundId = fundId,
            DepartmentId = departmentId,
            MunicipalAccountId = 1,
            StartPeriod = new DateTime(fiscalYear - 1, 7, 1),
            EndPeriod = new DateTime(fiscalYear, 6, 30),
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}

/// <summary>
/// Test DbContext factory for InMemory database
/// </summary>
internal class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}

/// <summary>
/// Mock telemetry service for testing
/// </summary>
internal class MockTelemetryService : ITelemetryService
{
    public void RecordException(Exception exception, params (string key, object? value)[] tags) { }
    public void RecordMetric(string metricName, double value, params (string key, object? value)[] tags) { }
}
