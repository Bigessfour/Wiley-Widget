using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Data;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.ViewModels.Tests.RepositoryTests;

/// <summary>
/// Comprehensive tests for BudgetRepository focusing on EF Core interactions,
/// resilience, concurrency handling, and related entity loading.
/// Tests database configuration updates and repository CRUD operations.
/// </summary>
public class BudgetRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly BudgetRepository _repository;

    public BudgetRepositoryTests()
    {
        // Setup in-memory database with unique name per test instance
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);
        _contextFactory = new TestDbContextFactory(_context);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _repository = new BudgetRepository(_contextFactory, _cache);

        // Seed test data
        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Seed required entities first (Departments and Funds)
        var department1 = new Department { Id = 1, Name = "Public Works", DepartmentCode = "PW" };
        var department2 = new Department { Id = 2, Name = "Finance", DepartmentCode = "FIN" };
        var fund1 = new Fund { Id = 1, FundCode = "GF-001", Name = "General Fund", Type = FundType.GeneralFund };

        _context.Departments.AddRange(department1, department2);
        _context.Funds.Add(fund1);

        // Seed MunicipalAccounts
        var account1 = new MunicipalAccount
        {
            Id = 1,
            Name = "Operating Account",
            Fund = AccountFundType.GeneralFund,
            Type = AccountType.Asset,
            Balance = 10000m,
            BudgetAmount = 15000m,
            DepartmentId = 1
        };

        _context.MunicipalAccounts.Add(account1);
        await _context.SaveChangesAsync();

        // Seed BudgetEntries with proper relationships
        var entry1 = new BudgetEntry
        {
            Id = 1,
            AccountNumber = "100.1",
            Description = "Salaries",
            FiscalYear = 2026,
            BudgetedAmount = 50000m,
            ActualAmount = 45000m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };

        var entry2 = new BudgetEntry
        {
            Id = 2,
            AccountNumber = "100.2",
            Description = "Benefits",
            FiscalYear = 2026,
            BudgetedAmount = 30000m,
            ActualAmount = 28000m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };

        var entry3 = new BudgetEntry
        {
            Id = 3,
            AccountNumber = "200.1",
            Description = "Equipment",
            FiscalYear = 2026,
            BudgetedAmount = 20000m,
            ActualAmount = 15000m,
            DepartmentId = 2,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };

        _context.BudgetEntries.AddRange(entry1, entry2, entry3);
        await _context.SaveChangesAsync();
    }

    #region GetAllBudgets_IncludesRelatedEntities Tests

    [Fact]
    public async Task Test_GetByFiscalYearAsync_IncludesRelatedEntities()
    {
        // Arrange - data already seeded

        // Act
        var budgets = await _repository.GetByFiscalYearAsync(2026);

        // Assert
        budgets.Should().NotBeNull();
        budgets.Should().HaveCount(3);

        var budgetList = budgets.ToList();

        // Verify all entries have Department loaded
        budgetList.Should().AllSatisfy(b => b.Department.Should().NotBeNull());

        // Verify all entries have Fund loaded
        budgetList.Should().AllSatisfy(b => b.Fund.Should().NotBeNull());

        // Verify specific department data
        var publicWorksBudgets = budgetList.Where(b => b.Department.DepartmentCode == "PW").ToList();
        publicWorksBudgets.Should().HaveCount(2);
        publicWorksBudgets.Should().AllSatisfy(b =>
            b.Department.Name.Should().Be("Public Works"));
    }

    [Fact]
    public async Task Test_GetByDepartmentAsync_IncludesRelatedEntities()
    {
        // Arrange
        int departmentId = 1;

        // Act
        var budgets = await _repository.GetByDepartmentAsync(departmentId);

        // Assert
        budgets.Should().NotBeNull();
        budgets.Should().HaveCount(2);

        var budgetList = budgets.ToList();

        // Verify Department is loaded
        budgetList.Should().AllSatisfy(b =>
        {
            b.Department.Should().NotBeNull();
            b.Department.Name.Should().Be("Public Works");
        });

        // Verify Fund is loaded
        budgetList.Should().AllSatisfy(b =>
        {
            b.Fund.Should().NotBeNull();
            b.Fund.Name.Should().Be("General Fund");
        });
    }

    [Fact]
    public async Task Test_GetByDepartmentAndFiscalYearAsync_IncludesRelatedEntities()
    {
        // Arrange
        int departmentId = 1;
        int fiscalYear = 2026;

        // Act
        var budgets = await _repository.GetByDepartmentAndFiscalYearAsync(departmentId, fiscalYear);

        // Assert
        budgets.Should().NotBeNull();
        var budgetList = budgets.ToList();
        budgetList.Should().HaveCount(2);

        // Verify navigation properties are loaded
        budgetList.Should().AllSatisfy(b =>
        {
            b.Department.Should().NotBeNull();
            b.Department.DepartmentCode.Should().Be("PW");
            b.Fund.Should().NotBeNull();
            b.FiscalYear.Should().Be(2026);
        });

        // Verify correct entries are returned
        budgetList.Select(b => b.AccountNumber).Should().Contain(new[] { "100.1", "100.2" });
    }

    #endregion

    #region SaveBudget_HandlesConcurrency Tests

    [Fact]
    public async Task Test_AddAsync_SavesBudgetEntry()
    {
        // Arrange
        var newEntry = new BudgetEntry
        {
            AccountNumber = "300.1",
            Description = "New Equipment",
            FiscalYear = 2026,
            BudgetedAmount = 25000m,
            ActualAmount = 0m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _repository.AddAsync(newEntry);

        // Assert - verify it was saved
        var saved = await _context.BudgetEntries.FindAsync(newEntry.Id);
        saved.Should().NotBeNull();
        saved!.AccountNumber.Should().Be("300.1");
        saved.Description.Should().Be("New Equipment");
        saved.BudgetedAmount.Should().Be(25000m);
    }

    [Fact]
    public async Task Test_UpdateAsync_UpdatesBudgetEntry()
    {
        // Arrange
        var entry = await _context.BudgetEntries.FindAsync(1);
        entry.Should().NotBeNull();

        var originalAmount = entry!.BudgetedAmount;
        entry.BudgetedAmount = 55000m;
        entry.UpdatedAt = DateTime.UtcNow;

        // Act
        await _repository.UpdateAsync(entry);

        // Assert - reload and verify
        _context.Entry(entry).State = EntityState.Detached;
        var updated = await _context.BudgetEntries.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.BudgetedAmount.Should().Be(55000m);
        updated.BudgetedAmount.Should().NotBe(originalAmount);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_DeleteAsync_RemovesBudgetEntry()
    {
        // Arrange
        var entryId = 3;
        var entryBefore = await _context.BudgetEntries.FindAsync(entryId);
        entryBefore.Should().NotBeNull();

        // Act
        await _repository.DeleteAsync(entryId);

        // Assert
        var entryAfter = await _context.BudgetEntries.FindAsync(entryId);
        entryAfter.Should().BeNull();
    }

    [Fact]
    public async Task Test_UpdateAsync_HandlesConcurrencySimulation()
    {
        // Note: In-memory database doesn't support actual concurrency tokens,
        // but we can verify the repository method handles the update correctly
        // In a real SQL database test, you would use two contexts to simulate concurrency

        // Arrange
        var entry = await _context.BudgetEntries.FindAsync(1);
        entry.Should().NotBeNull();

        entry!.BudgetedAmount = 60000m;
        entry.Description = "Updated Salaries";

        // Act - should complete successfully
        await _repository.UpdateAsync(entry);

        // Assert
        _context.Entry(entry).State = EntityState.Detached;
        var updated = await _context.BudgetEntries.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.BudgetedAmount.Should().Be(60000m);
        updated.Description.Should().Be("Updated Salaries");
    }

    [Fact]
    public async Task Test_GetByIdAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        int nonExistentId = 9999;

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Test_GetByIdAsync_ReturnsCorrectEntry()
    {
        // Arrange
        int existingId = 1;

        // Act
        var result = await _repository.GetByIdAsync(existingId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(existingId);
        result.AccountNumber.Should().Be("100.1");
        result.Description.Should().Be("Salaries");
    }

    #endregion

    #region Resilience and Query Tests

    [Fact]
    public async Task Test_GetPagedAsync_ReturnsPaginatedResults()
    {
        // Arrange
        int pageNumber = 1;
        int pageSize = 2;

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

        // Assert
        items.Should().NotBeNull();
        items.Should().HaveCount(2);
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task Test_GetPagedAsync_WithFiscalYearFilter()
    {
        // Arrange
        int pageNumber = 1;
        int pageSize = 10;
        int fiscalYear = 2026;

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize, fiscalYear: fiscalYear);

        // Assert
        items.Should().NotBeNull();
        items.Should().HaveCount(3);
        items.Should().AllSatisfy(b => b.FiscalYear.Should().Be(2026));
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task Test_GetByFundAsync_ReturnsCorrectEntries()
    {
        // Arrange
        int fundId = 1;

        // Act
        var budgets = await _repository.GetByFundAsync(fundId);

        // Assert
        budgets.Should().NotBeNull();
        budgets.Should().HaveCount(3);
        budgets.Should().AllSatisfy(b => b.FundId.Should().Be(fundId));
    }

    [Fact]
    public async Task Test_GetBudgetSummaryAsync_CalculatesCorrectly()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var summary = await _repository.GetBudgetSummaryAsync(startDate, endDate);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalBudgeted.Should().Be(100000m); // 50000 + 30000 + 20000
        summary.TotalActual.Should().Be(88000m);    // 45000 + 28000 + 15000
        summary.TotalVariance.Should().Be(12000m);  // 100000 - 88000
    }

    [Fact]
    public async Task Test_GetDepartmentBreakdownAsync_GroupsByDepartment()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var breakdown = await _repository.GetDepartmentBreakdownAsync(startDate, endDate);

        // Assert
        breakdown.Should().NotBeNull();
        breakdown.Should().HaveCount(2); // Two departments

        var pwDept = breakdown.FirstOrDefault(d => d.DepartmentName == "Public Works");
        pwDept.Should().NotBeNull();
        pwDept!.TotalBudgeted.Should().Be(80000m); // 50000 + 30000
        pwDept.TotalActual.Should().Be(73000m);    // 45000 + 28000

        var finDept = breakdown.FirstOrDefault(d => d.DepartmentName == "Finance");
        finDept.Should().NotBeNull();
        finDept!.TotalBudgeted.Should().Be(20000m);
        finDept.TotalActual.Should().Be(15000m);
    }

    #endregion

    #region Cache Tests

    [Fact]
    public async Task Test_GetByFiscalYearAsync_UsesCaching()
    {
        // Arrange & Act - First call should cache
        var firstCall = await _repository.GetByFiscalYearAsync(2026);

        // Second call should use cache (we can't directly verify cache usage in this test,
        // but we can verify consistent results)
        var secondCall = await _repository.GetByFiscalYearAsync(2026);

        // Assert
        firstCall.Should().NotBeNull();
        secondCall.Should().NotBeNull();
        firstCall.Should().HaveCount(3);
        secondCall.Should().HaveCount(3);

        // Verify same data is returned
        firstCall.Select(b => b.Id).Should().BeEquivalentTo(secondCall.Select(b => b.Id));
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
        _cache?.Dispose();
    }

    /// <summary>
    /// Test implementation of IDbContextFactory for in-memory testing
    /// </summary>
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly AppDbContext _context;

        public TestDbContextFactory(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public AppDbContext CreateDbContext() => _context;

        public Task<AppDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(_context);
    }
}
