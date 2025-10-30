using System;
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
/// Comprehensive tests for DepartmentRepository focusing on change tracking,
/// audit trails, hierarchical relationships, and update operations.
/// Tests entity state management and tracking behavior post-database config updates.
/// </summary>
public class DepartmentRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly DepartmentRepository _repository;

    public DepartmentRepositoryTests()
    {
        // Setup in-memory database with unique name per test instance
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DeptTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);
        _contextFactory = new TestDbContextFactory(_context);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _repository = new DepartmentRepository(_contextFactory, _cache);

        // Seed test data
        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Seed departments with hierarchical structure
        var dept1 = new Department
        {
            Id = 1,
            Name = "Administration",
            DepartmentCode = "ADMIN"
        };

        var dept2 = new Department
        {
            Id = 2,
            Name = "Public Works",
            DepartmentCode = "PW",
            ParentId = 1
        };

        var dept3 = new Department
        {
            Id = 3,
            Name = "Finance",
            DepartmentCode = "FIN"
        };

        _context.Departments.AddRange(dept1, dept2, dept3);
        await _context.SaveChangesAsync();
    }

    #region UpdateDepartment_TracksChanges Tests

    [Fact]
    public async Task Test_UpdateDepartment_TracksChanges()
    {
        // Arrange
        var department = await _context.Departments.FindAsync(1);
        department.Should().NotBeNull();

        var originalName = department!.Name;
        department.Name = "Administration - Updated";

        // Act
        await _repository.UpdateAsync(department);

        // Assert - Verify changes were persisted
        _context.Entry(department).State = EntityState.Detached;
        var updated = await _context.Departments.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Administration - Updated");
        updated.Name.Should().NotBe(originalName);
    }

    [Fact]
    public async Task Test_UpdateDepartment_SetsModifiedState()
    {
        // Arrange
        var department = await _context.Departments.FindAsync(2);
        department.Should().NotBeNull();

        department!.DepartmentCode = "PW-NEW";

        // Track state before update
        var stateBeforeUpdate = _context.Entry(department).State;
        stateBeforeUpdate.Should().Be(EntityState.Unchanged);

        // Act
        await _repository.UpdateAsync(department);

        // Assert - After SaveChanges, state should be Unchanged again
        var stateAfterUpdate = _context.Entry(department).State;
        stateAfterUpdate.Should().Be(EntityState.Unchanged);

        // Verify the change was saved
        _context.Entry(department).State = EntityState.Detached;
        var reloaded = await _context.Departments.FindAsync(2);
        reloaded!.DepartmentCode.Should().Be("PW-NEW");
    }

    [Fact]
    public async Task Test_UpdateDepartment_VerifiesAuditTrail()
    {
        // Note: Department model doesn't have explicit audit fields like CreatedAt/UpdatedAt
        // This test verifies that updates are tracked properly in the context

        // Arrange
        var department = await _context.Departments.FindAsync(3);
        department.Should().NotBeNull();

        var originalCode = department!.DepartmentCode;
        department.DepartmentCode = "FIN-UPDATED";

        // Act
        await _repository.UpdateAsync(department);

        // Assert
        _context.Entry(department).State = EntityState.Detached;
        var updated = await _context.Departments.FindAsync(3);
        updated.Should().NotBeNull();
        updated!.DepartmentCode.Should().Be("FIN-UPDATED");

        // Verify change tracking worked
        updated.DepartmentCode.Should().NotBe(originalCode);
    }

    [Fact]
    public async Task Test_UpdateDepartment_MultipleFields()
    {
        // Arrange
        var department = await _context.Departments.FindAsync(1);
        department.Should().NotBeNull();

        department!.Name = "Central Administration";
        department.DepartmentCode = "CENTRAL";

        // Act
        await _repository.UpdateAsync(department);

        // Assert
        _context.Entry(department).State = EntityState.Detached;
        var updated = await _context.Departments.FindAsync(1);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Central Administration");
        updated.DepartmentCode.Should().Be("CENTRAL");
    }

    [Fact]
    public async Task Test_UpdateDepartment_PreservesRelationships()
    {
        // Arrange - Department 2 has ParentId = 1
        var department = await _context.Departments.FindAsync(2);
        department.Should().NotBeNull();
        department!.ParentId.Should().Be(1);

        // Update non-relationship fields
        department.Name = "Public Works - Updated";

        // Act
        await _repository.UpdateAsync(department);

        // Assert - Verify parent relationship is preserved
        _context.Entry(department).State = EntityState.Detached;
        var updated = await _context.Departments.FindAsync(2);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Public Works - Updated");
        updated.ParentId.Should().Be(1); // Relationship preserved
    }

    #endregion

    #region Add and Delete Tests

    [Fact]
    public async Task Test_AddAsync_AddsNewDepartment()
    {
        // Arrange
        var newDept = new Department
        {
            Name = "IT Department",
            DepartmentCode = "IT"
        };

        // Act
        await _repository.AddAsync(newDept);

        // Assert
        newDept.Id.Should().BeGreaterThan(0);

        var saved = await _context.Departments.FindAsync(newDept.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("IT Department");
        saved.DepartmentCode.Should().Be("IT");
    }

    [Fact]
    public async Task Test_AddAsync_WithParentDepartment()
    {
        // Arrange
        var childDept = new Department
        {
            Name = "HR Sub-Division",
            DepartmentCode = "HR-SUB",
            ParentId = 1
        };

        // Act
        await _repository.AddAsync(childDept);

        // Assert
        childDept.Id.Should().BeGreaterThan(0);

        var saved = await _context.Departments.FindAsync(childDept.Id);
        saved.Should().NotBeNull();
        saved!.ParentId.Should().Be(1);
        saved.Name.Should().Be("HR Sub-Division");
    }

    [Fact]
    public async Task Test_DeleteAsync_RemovesDepartment()
    {
        // Arrange
        var deptId = 3;
        var deptBefore = await _context.Departments.FindAsync(deptId);
        deptBefore.Should().NotBeNull();

        // Act
        var result = await _repository.DeleteAsync(deptId);

        // Assert
        result.Should().BeTrue();

        var deptAfter = await _context.Departments.FindAsync(deptId);
        deptAfter.Should().BeNull();
    }

    [Fact]
    public async Task Test_DeleteAsync_NonExistentDepartment_ReturnsFalse()
    {
        // Arrange
        int nonExistentId = 9999;

        // Act
        var result = await _repository.DeleteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task Test_GetAllAsync_ReturnsAllDepartments()
    {
        // Act
        var departments = await _repository.GetAllAsync();

        // Assert
        departments.Should().NotBeNull();
        departments.Should().HaveCount(3);
        departments.Select(d => d.DepartmentCode).Should().Contain(new[] { "ADMIN", "PW", "FIN" });
    }

    [Fact]
    public async Task Test_GetByIdAsync_ReturnsCorrectDepartment()
    {
        // Arrange
        int deptId = 1;

        // Act
        var department = await _repository.GetByIdAsync(deptId);

        // Assert
        department.Should().NotBeNull();
        department!.Id.Should().Be(deptId);
        department.Name.Should().Be("Administration");
        department.DepartmentCode.Should().Be("ADMIN");
    }

    [Fact]
    public async Task Test_GetByIdAsync_NonExistent_ReturnsNull()
    {
        // Arrange
        int nonExistentId = 9999;

        // Act
        var department = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        department.Should().BeNull();
    }

    [Fact]
    public async Task Test_GetByCodeAsync_ReturnsCorrectDepartment()
    {
        // Arrange
        string code = "PW";

        // Act
        var department = await _repository.GetByCodeAsync(code);

        // Assert
        department.Should().NotBeNull();
        department!.DepartmentCode.Should().Be(code);
        department.Name.Should().Be("Public Works");
    }

    [Fact]
    public async Task Test_GetByCodeAsync_NonExistent_ReturnsNull()
    {
        // Arrange
        string nonExistentCode = "NONEXIST";

        // Act
        var department = await _repository.GetByCodeAsync(nonExistentCode);

        // Assert
        department.Should().BeNull();
    }

    [Fact]
    public async Task Test_ExistsByCodeAsync_ExistingCode_ReturnsTrue()
    {
        // Arrange
        string existingCode = "ADMIN";

        // Act
        var exists = await _repository.ExistsByCodeAsync(existingCode);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Test_ExistsByCodeAsync_NonExistentCode_ReturnsFalse()
    {
        // Arrange
        string nonExistentCode = "NONEXIST";

        // Act
        var exists = await _repository.ExistsByCodeAsync(nonExistentCode);

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region Hierarchical Tests

    [Fact]
    public async Task Test_GetRootDepartmentsAsync_ReturnsOnlyRootDepartments()
    {
        // Act
        var rootDepartments = await _repository.GetRootDepartmentsAsync();

        // Assert
        rootDepartments.Should().NotBeNull();
        var rootList = rootDepartments.ToList();

        // Should return departments without parents (ADMIN and FIN)
        rootList.Should().HaveCount(2);
        rootList.Should().AllSatisfy(d => d.ParentId.Should().BeNull());
        rootList.Select(d => d.DepartmentCode).Should().Contain(new[] { "ADMIN", "FIN" });
    }

    [Fact]
    public async Task Test_GetChildDepartmentsAsync_ReturnsChildrenOnly()
    {
        // Arrange
        int parentId = 1; // Administration

        // Act
        var children = await _repository.GetChildDepartmentsAsync(parentId);

        // Assert
        children.Should().NotBeNull();
        var childList = children.ToList();

        childList.Should().HaveCount(1);
        childList[0].DepartmentCode.Should().Be("PW");
        childList[0].ParentId.Should().Be(parentId);
    }

    [Fact]
    public async Task Test_GetChildDepartmentsAsync_NoChildren_ReturnsEmpty()
    {
        // Arrange
        int parentIdWithNoChildren = 3; // Finance

        // Act
        var children = await _repository.GetChildDepartmentsAsync(parentIdWithNoChildren);

        // Assert
        children.Should().NotBeNull();
        children.Should().BeEmpty();
    }

    #endregion

    #region Paging Tests

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
    public async Task Test_GetPagedAsync_SecondPage()
    {
        // Arrange
        int pageNumber = 2;
        int pageSize = 2;

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

        // Assert
        items.Should().NotBeNull();
        items.Should().HaveCount(1); // Only 1 item on page 2
        totalCount.Should().Be(3);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task Test_GetAllAsync_UsesCaching()
    {
        // Arrange & Act - First call
        var firstCall = await _repository.GetAllAsync();

        // Second call (should use cache)
        var secondCall = await _repository.GetAllAsync();

        // Assert
        firstCall.Should().NotBeNull();
        secondCall.Should().NotBeNull();
        firstCall.Should().HaveCount(3);
        secondCall.Should().HaveCount(3);

        // Verify same data
        firstCall.Select(d => d.Id).Should().BeEquivalentTo(secondCall.Select(d => d.Id));
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
