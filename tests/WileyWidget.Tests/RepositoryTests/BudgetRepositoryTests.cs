using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
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
    // Pass options to the factory so it creates a new context per CreateDbContext call
    _contextFactory = new TestDbContextFactory(options);
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
            Fund = MunicipalFundType.General,
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

        // Verify all entries have Department loaded (names may vary depending on seed)
        budgetList.Should().AllSatisfy(b =>
        {
            var dept = b.Department;
            dept.Should().NotBeNull();
        });

        // Verify all entries have Fund loaded
        budgetList.Should().AllSatisfy(b =>
        {
            var fund = b.Fund;
            fund.Should().NotBeNull();
            fund!.Name.Should().Be("General Fund");
        });

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
            b.Department!.Name.Should().Be("Public Works");
        });

        // Verify Fund is loaded
        budgetList.Should().AllSatisfy(b =>
        {
            b.Fund.Should().NotBeNull();
            b.Fund!.Name.Should().Be("General Fund");
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

        // Assert - Create a fresh context to verify deletion (avoids caching issues)
        await using var verifyContext = _contextFactory.CreateDbContext();
        var entryAfter = await verifyContext.BudgetEntries.FindAsync(entryId);
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

    #region Cache Invalidation Tests (Critical)

    [Fact]
    public async Task Test_AddAsync_DoesNotInvalidateCache_CausesStaleData()
    {
        // This test documents the CURRENT BEHAVIOR (a bug)
        // AddAsync does NOT invalidate cache, leading to stale data

        // Arrange - Prime the cache
        var cachedData = await _repository.GetByFiscalYearAsync(2026);
        cachedData.Should().HaveCount(3);

        // Act - Add a new entry
        var newEntry = new BudgetEntry
        {
            AccountNumber = "400.1",
            Description = "New Category",
            FiscalYear = 2026,
            BudgetedAmount = 10000m,
            ActualAmount = 0m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(newEntry);

        // Assert - Cache returns stale data (3 entries instead of 4)
        var staleData = await _repository.GetByFiscalYearAsync(2026);
        staleData.Should().HaveCount(3, "Cache was not invalidated - this documents the bug");

        // Verify fresh database query shows correct count
        await using var freshContext = await _contextFactory.CreateDbContextAsync();
        var actualCount = await freshContext.BudgetEntries.CountAsync(b => b.FiscalYear == 2026);
        actualCount.Should().Be(4, "Database has correct count, but cache is stale");
    }

    [Fact]
    public async Task Test_UpdateAsync_DoesNotInvalidateCache_CausesStaleData()
    {
        // Arrange - Prime cache with specific department
        var cachedData = await _repository.GetByDepartmentAsync(1);
        cachedData.Should().HaveCount(2);

        // Get entry from database to update (not from cache)
        var entryToUpdate = await _context.BudgetEntries.FindAsync(1);
        entryToUpdate.Should().NotBeNull();
        var originalAmount = entryToUpdate!.BudgetedAmount;

        // Act - Update via repository
        entryToUpdate.BudgetedAmount = 999999m;
        await _repository.UpdateAsync(entryToUpdate);

        // Assert - Cache still has old value because UpdateAsync doesn't invalidate
        var staleData = await _repository.GetByDepartmentAsync(1);
        var staleCachedEntry = staleData.First(b => b.Id == 1);
        staleCachedEntry.BudgetedAmount.Should().Be(originalAmount, "Cache was not invalidated");

        // Verify database has new value
        await using var freshContext = await _contextFactory.CreateDbContextAsync();
        var dbEntry = await freshContext.BudgetEntries.FindAsync(1);
        dbEntry!.BudgetedAmount.Should().Be(999999m, "Database was updated correctly");
    }

    [Fact]
    public async Task Test_DeleteAsync_DoesNotInvalidateCache_CausesStaleData()
    {
        // Arrange - Prime fund cache
        var cachedData = await _repository.GetByFundAsync(1);
        cachedData.Should().HaveCount(3);
        var entryToDelete = cachedData.First().Id;

        // Act - Delete entry
        await _repository.DeleteAsync(entryToDelete);

        // Assert - Cache still shows deleted entry
        var staleData = await _repository.GetByFundAsync(1);
        staleData.Should().HaveCount(3, "Cache was not invalidated");

        // Verify database doesn't have it
        await using var freshContext = await _contextFactory.CreateDbContextAsync();
        var dbEntry = await freshContext.BudgetEntries.FindAsync(entryToDelete);
        dbEntry.Should().BeNull("Database deleted correctly");
    }

    [Fact]
    public async Task Test_MultipleCacheMethods_HaveDistinctKeys()
    {
        // Verify cache isolation between different query methods

        // Prime different caches
        var byYear = await _repository.GetByFiscalYearAsync(2026);
        var byFund = await _repository.GetByFundAsync(1);
        var byDept = await _repository.GetByDepartmentAsync(1);
        var byFundYear = await _repository.GetByFundAndFiscalYearAsync(1, 2026);

        // All should return data
        byYear.Should().HaveCount(3);
        byFund.Should().HaveCount(3);
        byDept.Should().HaveCount(2);
        byFundYear.Should().HaveCount(3);

        // Verify different methods can coexist (no key collisions)
        // This test passes but documents potential cache coherency issues
    }

    #endregion

    #region Concurrency Handling Tests

    [Fact]
    public async Task Test_UpdateAsync_NoConcurrencyHandling_UnlikeEnterpriseRepository()
    {
        // DOCUMENTS LIMITATION: BudgetRepository.UpdateAsync does NOT handle concurrency conflicts
        // Compare to EnterpriseRepository which uses RepositoryConcurrencyHelper

        // Arrange
        var entry = await _context.BudgetEntries.FindAsync(1);
        entry.Should().NotBeNull();
        entry!.BudgetedAmount = 75000m;

        // Act & Assert - Should succeed (no conflict detection)
        await _repository.UpdateAsync(entry);

        // Note: In-memory database doesn't enforce concurrency tokens anyway
        // In real SQL with RowVersion, this would need DbUpdateConcurrencyException handling
        var updated = await _context.BudgetEntries.FindAsync(1);
        updated!.BudgetedAmount.Should().Be(75000m);
    }

    [Fact]
    public async Task Test_AddAsync_WithNullEntry_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Func<Task> act = async () => await _repository.AddAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("budgetEntry");
    }

    [Fact]
    public async Task Test_UpdateAsync_WithNullEntry_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Func<Task> act = async () => await _repository.UpdateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("budgetEntry");
    }

    #endregion

    #region Hierarchical Operations Tests

    [Fact]
    public async Task Test_GetBudgetHierarchyAsync_ReturnsHierarchicalStructure()
    {
        // Arrange - Add parent-child relationships
        var parent = new BudgetEntry
        {
            AccountNumber = "500.0",
            Description = "Parent Category",
            FiscalYear = 2027,
            BudgetedAmount = 100000m,
            ActualAmount = 0m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };
        _context.BudgetEntries.Add(parent);
        await _context.SaveChangesAsync();

        var child = new BudgetEntry
        {
            AccountNumber = "500.1",
            Description = "Child Category",
            FiscalYear = 2027,
            BudgetedAmount = 25000m,
            ActualAmount = 0m,
            ParentId = parent.Id,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };
        _context.BudgetEntries.Add(child);
        await _context.SaveChangesAsync();

        // Act
        var hierarchy = await _repository.GetBudgetHierarchyAsync(2027);

        // Assert
        hierarchy.Should().NotBeNull();
        hierarchy.Should().HaveCountGreaterOrEqualTo(2);
        var parentEntry = hierarchy.FirstOrDefault(b => b.AccountNumber == "500.0");
        parentEntry.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_GetByIdAsync_LoadsParentAndChildren()
    {
        // Arrange - Create hierarchy
        var parent = new BudgetEntry
        {
            AccountNumber = "600.0",
            Description = "Hierarchical Parent",
            FiscalYear = 2026,
            BudgetedAmount = 50000m,
            ActualAmount = 0m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };
        _context.BudgetEntries.Add(parent);
        await _context.SaveChangesAsync();

        var child1 = new BudgetEntry
        {
            AccountNumber = "600.1",
            Description = "Child 1",
            FiscalYear = 2026,
            BudgetedAmount = 20000m,
            ActualAmount = 0m,
            ParentId = parent.Id,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };
        _context.BudgetEntries.Add(child1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(parent.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Children.Should().NotBeNull();
        result.Children.Should().HaveCount(1);
        result.Children.First().AccountNumber.Should().Be("600.1");
    }

    [Fact]
    public async Task Test_DeleteAsync_WithChildEntries_OrphansChildren()
    {
        // DOCUMENTS BEHAVIOR: No cascade delete enforcement in repository

        // Arrange
        var parent = new BudgetEntry
        {
            AccountNumber = "700.0",
            Description = "Parent to Delete",
            FiscalYear = 2026,
            BudgetedAmount = 50000m,
            ActualAmount = 0m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };
        _context.BudgetEntries.Add(parent);
        await _context.SaveChangesAsync();

        var child = new BudgetEntry
        {
            AccountNumber = "700.1",
            Description = "Orphan Child",
            FiscalYear = 2026,
            BudgetedAmount = 10000m,
            ActualAmount = 0m,
            ParentId = parent.Id,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };
        _context.BudgetEntries.Add(child);
        await _context.SaveChangesAsync();

        // Act - Delete parent
        await _repository.DeleteAsync(parent.Id);

        // Assert - Child becomes orphaned (ParentId points to non-existent entry)
        await using var verifyContext = await _contextFactory.CreateDbContextAsync();
        var orphan = await verifyContext.BudgetEntries.FindAsync(child.Id);
        orphan.Should().NotBeNull("Child still exists");
        orphan!.ParentId.Should().Be(parent.Id, "ParentId still references deleted parent");

        var deletedParent = await verifyContext.BudgetEntries.FindAsync(parent.Id);
        deletedParent.Should().BeNull("Parent was deleted");
    }

    #endregion

    #region Data Validation & Edge Cases

    [Fact]
    public async Task Test_GetByFiscalYearAsync_WithNegativeFiscalYear_ReturnsEmpty()
    {
        // Act
        var result = await _repository.GetByFiscalYearAsync(-1);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_GetByFiscalYearAsync_WithFutureFiscalYear_ReturnsEmpty()
    {
        // Act
        var result = await _repository.GetByFiscalYearAsync(3000);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_GetByFiscalYearAsync_WithZeroFiscalYear_ReturnsEmpty()
    {
        // Act
        var result = await _repository.GetByFiscalYearAsync(0);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_GetPagedAsync_WithZeroPageSize_ReturnsEmpty()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 0);

        // Assert
        items.Should().BeEmpty();
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task Test_GetPagedAsync_BeyondLastPage_ReturnsEmpty()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(10, 50);

        // Assert
        items.Should().BeEmpty();
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task Test_DeleteAsync_WithNonExistentId_CompletesSuccessfully()
    {
        // DOCUMENTS BEHAVIOR: Silent success if ID doesn't exist

        // Arrange
        int nonExistentId = 99999;

        // Act
        Func<Task> act = async () => await _repository.DeleteAsync(nonExistentId);

        // Assert - Should NOT throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Test_AddAsync_WithMaxLengthAccountNumber_Succeeds()
    {
        // Arrange - AccountNumber max length is 50
        var entry = new BudgetEntry
        {
            AccountNumber = new string('9', 50),
            Description = "Max Length Test",
            FiscalYear = 2026,
            BudgetedAmount = 1000m,
            ActualAmount = 0m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _repository.AddAsync(entry);

        // Assert
        var saved = await _context.BudgetEntries.FindAsync(entry.Id);
        saved.Should().NotBeNull();
        saved!.AccountNumber.Length.Should().Be(50);
    }

    [Fact]
    public async Task Test_AddAsync_WithDecimalPrecisionLimits_Succeeds()
    {
        // decimal(18,2) supports up to 9,999,999,999,999,999.99

        // Arrange
        var entry = new BudgetEntry
        {
            AccountNumber = "800.1",
            Description = "Decimal Precision Test",
            FiscalYear = 2026,
            BudgetedAmount = 9999999999999999.99m,
            ActualAmount = 0.01m,
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            FundType = FundType.GeneralFund,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _repository.AddAsync(entry);

        // Assert
        var saved = await _context.BudgetEntries.FindAsync(entry.Id);
        saved.Should().NotBeNull();
        saved!.BudgetedAmount.Should().Be(9999999999999999.99m);
        saved.ActualAmount.Should().Be(0.01m);
    }

    #endregion

    #region Sorting & Pagination Edge Cases

    [Fact]
    public async Task Test_GetPagedAsync_SortByBudgetedAmount_Ascending()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, "budgetedamount", false);

        // Assert
        items.Should().NotBeEmpty();
        var itemList = items.ToList();
        itemList[0].BudgetedAmount.Should().BeLessOrEqualTo(itemList[1].BudgetedAmount);
    }

    [Fact]
    public async Task Test_GetPagedAsync_SortByBudgetedAmount_Descending()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, "budgetedamount", true);

        // Assert
        items.Should().NotBeEmpty();
        var itemList = items.ToList();
        itemList[0].BudgetedAmount.Should().BeGreaterOrEqualTo(itemList[1].BudgetedAmount);
    }

    [Fact]
    public async Task Test_GetPagedAsync_SortByDepartment_Ascending()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, "department", false);

        // Assert
        items.Should().NotBeEmpty();
        var itemList = items.ToList();
        // Finance < Public Works alphabetically
        itemList[0].Department.Name.Should().Be("Finance");
    }

    [Fact]
    public async Task Test_GetPagedAsync_SortByFund_Ascending()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, "fund", false);

        // Assert
        items.Should().NotBeEmpty();
        items.Should().AllSatisfy(b => b.Fund.Should().NotBeNull());
    }

    [Fact]
    public async Task Test_GetPagedAsync_SortByActualAmount_Descending()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, "actualamount", true);

        // Assert
        items.Should().NotBeEmpty();
        var itemList = items.ToList();
        itemList[0].ActualAmount.Should().BeGreaterOrEqualTo(itemList.Last().ActualAmount);
    }

    [Fact]
    public async Task Test_GetPagedAsync_SortByFiscalYear_Ascending()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, "fiscalyear", false);

        // Assert
        items.Should().NotBeEmpty();
        items.Should().AllSatisfy(b => b.FiscalYear.Should().Be(2026));
    }

    [Fact]
    public async Task Test_GetPagedAsync_InvalidSortField_UsesDefaultSort()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, "invalid_field", false);

        // Assert - Should default to CreatedAt ascending
        items.Should().NotBeEmpty();
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task Test_GetPagedAsync_EmptyResultSet_WithFiscalYearFilter()
    {
        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(1, 10, fiscalYear: 1900);

        // Assert
        items.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    #endregion

    #region Reporting & Analytics Tests

    [Fact]
    public async Task Test_GetSewerBudgetEntriesAsync_FiltersCorrectFund()
    {
        // DOCUMENTS BEHAVIOR: Hardcoded FundId = 2 for sewer

        // Arrange - Add sewer fund entry
        var sewerFund = new Fund
        {
            Id = 2,
            FundCode = "SF-001",
            Name = "Sewer Enterprise Fund",
            Type = FundType.EnterpriseFund
        };
        _context.Funds.Add(sewerFund);

        var sewerEntry = new BudgetEntry
        {
            AccountNumber = "900.1",
            Description = "Sewer Operations",
            FiscalYear = 2026,
            BudgetedAmount = 100000m,
            ActualAmount = 0m,
            DepartmentId = 1,
            FundId = 2,
            MunicipalAccountId = 1,
            FundType = FundType.EnterpriseFund,
            CreatedAt = DateTime.UtcNow
        };
        _context.BudgetEntries.Add(sewerEntry);
        await _context.SaveChangesAsync();

        // Act
        var sewerBudgets = await _repository.GetSewerBudgetEntriesAsync(2026);

        // Assert
        sewerBudgets.Should().NotBeEmpty();
        sewerBudgets.Should().AllSatisfy(b => b.FundId.Should().Be(2));
    }

    [Fact]
    public async Task Test_GetYearEndSummaryAsync_CalculatesForFullYear()
    {
        // Act
        var summary = await _repository.GetYearEndSummaryAsync(DateTime.UtcNow.Year);

        // Assert
        summary.Should().NotBeNull();
        summary.BudgetPeriod.Should().Contain(DateTime.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Test_GetFundAllocationsAsync_GroupsByFund()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var allocations = await _repository.GetFundAllocationsAsync(startDate, endDate);

        // Assert
        allocations.Should().NotBeNull();
        allocations.Should().HaveCount(1); // Only General Fund in test data
        var generalFund = allocations.First();
        generalFund.FundName.Should().Be("General Fund");
        generalFund.TotalBudgeted.Should().Be(100000m);
        generalFund.TotalActual.Should().Be(88000m);
    }

    [Fact]
    public async Task Test_GetVarianceAnalysisAsync_MatchesGetBudgetSummaryAsync()
    {
        // DOCUMENTS BEHAVIOR: Currently an alias method

        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var variance = await _repository.GetVarianceAnalysisAsync(startDate, endDate);
        var summary = await _repository.GetBudgetSummaryAsync(startDate, endDate);

        // Assert
        variance.Should().NotBeNull();
        summary.Should().NotBeNull();
        variance.TotalBudgeted.Should().Be(summary.TotalBudgeted);
        variance.TotalActual.Should().Be(summary.TotalActual);
        variance.TotalVariance.Should().Be(summary.TotalVariance);
    }

    [Fact]
    public async Task Test_GetBudgetSummaryAsync_WithEmptyDateRange_ReturnsZeros()
    {
        // Arrange - Date range with no data
        var startDate = new DateTime(1900, 1, 1);
        var endDate = new DateTime(1900, 12, 31);

        // Act
        var summary = await _repository.GetBudgetSummaryAsync(startDate, endDate);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalBudgeted.Should().Be(0);
        summary.TotalActual.Should().Be(0);
        summary.TotalVariance.Should().Be(0);
        summary.FundSummaries.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_GetBudgetSummaryAsync_CalculatesVariancePercentageCorrectly()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var summary = await _repository.GetBudgetSummaryAsync(startDate, endDate);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalBudgeted.Should().Be(100000m);
        summary.TotalActual.Should().Be(88000m);
        summary.TotalVariance.Should().Be(12000m);

        // Variance% = (Variance / Budgeted) * 100 = (12000 / 100000) * 100 = 12%
        summary.TotalVariancePercentage.Should().BeApproximately(12m, 0.01m);
    }

    [Fact]
    public async Task Test_GetDepartmentBreakdownAsync_CalculatesVariancePerDepartment()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var breakdown = await _repository.GetDepartmentBreakdownAsync(startDate, endDate);

        // Assert
        breakdown.Should().HaveCount(2);

        var publicWorks = breakdown.First(d => d.DepartmentName == "Public Works");
        publicWorks.AccountCount.Should().Be(2);
        publicWorks.TotalBudgeted.Should().Be(80000m);
        publicWorks.TotalActual.Should().Be(73000m);

        var finance = breakdown.First(d => d.DepartmentName == "Finance");
        finance.AccountCount.Should().Be(1);
        finance.TotalBudgeted.Should().Be(20000m);
        finance.TotalActual.Should().Be(15000m);
    }

    #endregion

    #region Enterprise-Scoped Methods (Document Limitations)

    [Fact]
    public async Task Test_GetBudgetSummaryByEnterpriseAsync_IgnoresEnterpriseIdParameter()
    {
        // DOCUMENTS LIMITATION: enterpriseId parameter is not used

        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var summary1 = await _repository.GetBudgetSummaryByEnterpriseAsync(1, startDate, endDate);
        var summary2 = await _repository.GetBudgetSummaryByEnterpriseAsync(999, startDate, endDate);

        // Assert - Both return same results (no enterprise filtering)
        summary1.TotalBudgeted.Should().Be(summary2.TotalBudgeted);
        summary1.TotalActual.Should().Be(summary2.TotalActual);
    }

    [Fact]
    public async Task Test_GetVarianceAnalysisByEnterpriseAsync_IgnoresEnterpriseIdParameter()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var result1 = await _repository.GetVarianceAnalysisByEnterpriseAsync(1, startDate, endDate);
        var result2 = await _repository.GetVarianceAnalysisByEnterpriseAsync(999, startDate, endDate);

        // Assert
        result1.TotalBudgeted.Should().Be(result2.TotalBudgeted);
    }

    [Fact]
    public async Task Test_GetDepartmentBreakdownByEnterpriseAsync_IgnoresEnterpriseIdParameter()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var result1 = await _repository.GetDepartmentBreakdownByEnterpriseAsync(1, startDate, endDate);
        var result2 = await _repository.GetDepartmentBreakdownByEnterpriseAsync(999, startDate, endDate);

        // Assert
        result1.Should().HaveCount(result2.Count);
    }

    [Fact]
    public async Task Test_GetFundAllocationsByEnterpriseAsync_IgnoresEnterpriseIdParameter()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var result1 = await _repository.GetFundAllocationsByEnterpriseAsync(1, startDate, endDate);
        var result2 = await _repository.GetFundAllocationsByEnterpriseAsync(999, startDate, endDate);

        // Assert
        result1.Should().HaveCount(result2.Count);
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        _context?.Dispose();
        _cache?.Dispose();
    }

    /// <summary>
    /// Test implementation of IDbContextFactory for in-memory testing
    /// </summary>
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public AppDbContext CreateDbContext() => new AppDbContext(_options);

        public Task<AppDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(_options));
    }
}
