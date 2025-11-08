using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.ViewModels.Tests.RepositoryTests;

/// <summary>
/// Comprehensive tests for EnterpriseRepository focusing on not-found scenarios,
/// soft delete functionality, query resilience, and error handling.
/// Tests proper null handling and enterprise-specific operations.
/// </summary>
public class EnterpriseRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<EnterpriseRepository>> _mockLogger;
    private readonly EnterpriseRepository _repository;

    public EnterpriseRepositoryTests()
    {
        // Setup in-memory database with unique name per test instance
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"EnterpriseTestDb_{Guid.NewGuid()}")
            .Options;

    _context = new AppDbContext(options);
    // Pass options to the factory so it creates a new context per CreateDbContext call
    _contextFactory = new TestDbContextFactory(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<EnterpriseRepository>>();
        _repository = new EnterpriseRepository(_contextFactory, _mockLogger.Object, _cache);

        // Seed test data
        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Seed enterprises
        var enterprise1 = new Enterprise
        {
            Id = 1,
            Name = "Water Department",
            Type = "Water",
            CurrentRate = 50.00m,
            // MonthlyRevenue is calculated (CitizenCount * CurrentRate)
            MonthlyExpenses = 35000m,
            CitizenCount = 1000,
            Status = EnterpriseStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };

        var enterprise2 = new Enterprise
        {
            Id = 2,
            Name = "Sewer Services",
            Type = "Sewer",
            CurrentRate = 47.37m,
            // MonthlyRevenue is calculated
            MonthlyExpenses = 30000m,
            CitizenCount = 950,
            Status = EnterpriseStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };

        var enterprise3 = new Enterprise
        {
            Id = 3,
            Name = "Trash Collection",
            Type = "Trash",
            CurrentRate = 31.25m,
            // MonthlyRevenue is calculated
            MonthlyExpenses = 20000m,
            CitizenCount = 800,
            Status = EnterpriseStatus.Inactive,
            IsDeleted = true, // Soft deleted
            DeletedDate = DateTime.UtcNow,
            DeletedBy = "TestUser",
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            CreatedDate = DateTime.UtcNow.AddMonths(-6)
        };

        _context.Enterprises.AddRange(enterprise1, enterprise2, enterprise3);
        await _context.SaveChangesAsync();
    }

    #region GetEnterpriseById_NotFound Tests

    [Fact]
    public async Task Test_GetEnterpriseById_NotFound()
    {
        // Arrange
        int nonExistentId = 9999;

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Test_GetEnterpriseById_ReturnsNull_NoException()
    {
        // Arrange
        int nonExistentId = 5000;

        // Act
        Func<Task> act = async () => await _repository.GetByIdAsync(nonExistentId);

        // Assert
        await act.Should().NotThrowAsync("Repository should return null, not throw exception");

        var result = await _repository.GetByIdAsync(nonExistentId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Test_GetEnterpriseById_ValidId_ReturnsEnterprise()
    {
        // Arrange
        int existingId = 1;

        // Act
        var result = await _repository.GetByIdAsync(existingId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(existingId);
    result.Name.Should().Be("Water Department");
    result.Type.Should().Be("Water");
    }

    [Fact]
    public async Task Test_GetEnterpriseById_SoftDeleted_ReturnsNull()
    {
        // Arrange
        int softDeletedId = 3; // Trash Collection is soft-deleted

        // Act
        var result = await _repository.GetByIdAsync(softDeletedId);

        // Assert - By default, soft-deleted entities should not be returned
        result.Should().BeNull();
    }

    #endregion

    #region Soft Delete Tests

    [Fact]
    public async Task Test_SoftDeleteAsync_MarksEntityAsDeleted()
    {
        // Arrange
        int enterpriseId = 1;
        var enterpriseBefore = await _context.Enterprises.FindAsync(enterpriseId);
        enterpriseBefore.Should().NotBeNull();
        enterpriseBefore!.IsDeleted.Should().BeFalse();

        // Act
        var result = await _repository.SoftDeleteAsync(enterpriseId);

        // Assert
        result.Should().BeTrue();

        // Reload and verify soft delete
    _context.Entry(enterpriseBefore!).State = EntityState.Detached;
        var enterpriseAfter = await _context.Enterprises.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == enterpriseId);
        enterpriseAfter.Should().NotBeNull();
        enterpriseAfter!.IsDeleted.Should().BeTrue();
        enterpriseAfter.DeletedDate.Should().NotBeNull();
        enterpriseAfter.DeletedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Test_SoftDeleteAsync_NonExistent_ReturnsFalse()
    {
        // Arrange
        int nonExistentId = 9999;

        // Act
        var result = await _repository.SoftDeleteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Test_GetAllAsync_ExcludesSoftDeletedEntities()
    {
        // Act
        var enterprises = await _repository.GetAllAsync();

        // Assert
        enterprises.Should().NotBeNull();
        var enterpriseList = enterprises.ToList();

        // Should only return non-deleted enterprises (2 out of 3)
        enterpriseList.Should().HaveCount(2);
        enterpriseList.Should().AllSatisfy(e => e.IsDeleted.Should().BeFalse());
        enterpriseList.Select(e => e.Name).Should().NotContain("Trash Collection");
    }

    [Fact]
    public async Task Test_GetAllIncludingDeletedAsync_IncludesSoftDeleted()
    {
        // Act
        var enterprises = await _repository.GetAllIncludingDeletedAsync();

        // Assert
        enterprises.Should().NotBeNull();
        var enterpriseList = enterprises.ToList();

        // Should return all enterprises including soft-deleted (all 3)
        enterpriseList.Should().HaveCount(3);

        var softDeleted = enterpriseList.FirstOrDefault(e => e.Name == "Trash Collection");
        softDeleted.Should().NotBeNull();
        softDeleted!.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region CRUD Operations

    [Fact]
    public async Task Test_AddAsync_AddsNewEnterprise()
    {
        // Arrange
        var newEnterprise = new Enterprise
        {
            Name = "Electric Utility",
            Type = "Apartments",
            CurrentRate = 50.00m,
            // MonthlyRevenue is calculated
            MonthlyExpenses = 40000m,
            CitizenCount = 1200,
            Status = EnterpriseStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };

        // Act
        await _repository.AddAsync(newEnterprise);

        // Assert
        newEnterprise.Id.Should().BeGreaterThan(0);

        var saved = await _context.Enterprises.FindAsync(newEnterprise.Id);
        saved.Should().NotBeNull();
    saved!.Name.Should().Be("Electric Utility");
    saved.MonthlyRevenue.Should().Be(1200 * 50.00m);
    }

    [Fact]
    public async Task Test_UpdateAsync_UpdatesEnterprise()
    {
        // Arrange
        var enterprise = await _context.Enterprises.FindAsync(1);
        enterprise.Should().NotBeNull();

    var originalRate = enterprise!.CurrentRate;
    enterprise.CurrentRate = 55.00m;

        // Act
        await _repository.UpdateAsync(enterprise);

    // Assert
    _context.Entry(enterprise).State = EntityState.Detached;
    var updated = await _context.Enterprises.FindAsync(1);
    updated.Should().NotBeNull();
    // CurrentRate should reflect the value we set on the entity
    updated!.CurrentRate.Should().Be(55.00m);
    updated.CurrentRate.Should().NotBe(originalRate);
    updated.MonthlyRevenue.Should().Be(55000m);
    }

    [Fact]
    public async Task Test_DeleteAsync_RemovesEnterprise()
    {
        // Arrange
        int enterpriseId = 2;
        var enterpriseBefore = await _context.Enterprises.FindAsync(enterpriseId);
        enterpriseBefore.Should().NotBeNull();

        // Act
        var result = await _repository.DeleteAsync(enterpriseId);

        // Assert
        result.Should().BeTrue();

        // Detach any local tracking to ensure we read from the database instance
        if (enterpriseBefore != null)
        {
            _context.Entry(enterpriseBefore).State = EntityState.Detached;
        }
        var enterpriseAfter = await _context.Enterprises.FindAsync(enterpriseId);
        enterpriseAfter.Should().BeNull();
    }

    [Fact]
    public async Task Test_DeleteAsync_NonExistent_ReturnsFalse()
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
    public async Task Test_GetByNameAsync_ReturnsCorrectEnterprise()
    {
        // Arrange
        string name = "Water Department";

        // Act
        var result = await _repository.GetByNameAsync(name);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(name);
    result.Type.Should().Be("Water");
    }

    [Fact]
    public async Task Test_GetByNameAsync_NonExistent_ReturnsNull()
    {
        // Arrange
        string nonExistentName = "NonExistent Enterprise";

        // Act
        var result = await _repository.GetByNameAsync(nonExistentName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Test_ExistsByNameAsync_Existing_ReturnsTrue()
    {
        // Arrange
        string existingName = "Sewer Services";

        // Act
        var exists = await _repository.ExistsByNameAsync(existingName);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Test_ExistsByNameAsync_NonExistent_ReturnsFalse()
    {
        // Arrange
        string nonExistentName = "NonExistent";

        // Act
        var exists = await _repository.ExistsByNameAsync(nonExistentName);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Test_GetSummariesAsync_ReturnsEnterpriseSummaries()
    {
        // Act
        var summaries = await _repository.GetSummariesAsync();

        // Assert
        summaries.Should().NotBeNull();
        var summaryList = summaries.ToList();

        // Should return summaries for non-deleted enterprises
        summaryList.Should().HaveCountGreaterOrEqualTo(2);

    var waterSummary = summaryList.FirstOrDefault(s => s.Name == "Water Department");
    waterSummary.Should().NotBeNull();
    // Repository currently returns CurrentRate as stored (50.00m) and calculates MonthlyRevenue
    waterSummary!.CurrentRate.Should().Be(50.00m);
    waterSummary.MonthlyRevenue.Should().Be(50000m);
    }

    [Fact]
    public async Task Test_GetActiveSummariesAsync_ReturnsOnlyActive()
    {
        // Act
        var summaries = await _repository.GetActiveSummariesAsync();

        // Assert
        summaries.Should().NotBeNull();
        var summaryList = summaries.ToList();

    // Should only return active, non-deleted enterprises
    summaryList.Should().HaveCount(2);
    // Repository computes Status as Surplus/Deficit/Break-even based on balances
    summaryList.Should().AllSatisfy(s => s.Status.Should().Be("Surplus"));
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
        totalCount.Should().Be(2); // Only non-deleted enterprises
    }

    [Fact]
    public async Task Test_GetPagedAsync_WithSorting()
    {
        // Arrange
        int pageNumber = 1;
        int pageSize = 10;
        string sortBy = "Name";
        bool sortDescending = false;

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize, sortBy, sortDescending);

        // Assert
        items.Should().NotBeNull();
        var itemsList = items.ToList();
        itemsList.Should().HaveCount(2);

        // Verify sorting
        itemsList[0].Name.Should().Be("Sewer Services");
        itemsList[1].Name.Should().Be("Water Department");
    }

    #endregion

    #region Resilience Tests

    [Fact]
    public async Task Test_GetByIdAsync_MultipleCallsConsistent()
    {
        // Arrange
        int enterpriseId = 1;

        // Act
        var firstCall = await _repository.GetByIdAsync(enterpriseId);
        var secondCall = await _repository.GetByIdAsync(enterpriseId);

        // Assert
        firstCall.Should().NotBeNull();
        secondCall.Should().NotBeNull();
        firstCall!.Id.Should().Be(secondCall!.Id);
        firstCall.Name.Should().Be(secondCall.Name);
    }

    [Fact]
    public async Task Test_GetAllAsync_CachesResults()
    {
        // Arrange & Act
        var firstCall = await _repository.GetAllAsync();
        var secondCall = await _repository.GetAllAsync();

        // Assert
        firstCall.Should().NotBeNull();
        secondCall.Should().NotBeNull();
        firstCall.Should().HaveCount(2);
        secondCall.Should().HaveCount(2);

        // Verify same data
        firstCall.Select(e => e.Id).Should().BeEquivalentTo(secondCall.Select(e => e.Id));
    }

    [Fact]
    public async Task Test_Repository_HandlesNullGracefully()
    {
        // Act - Various null scenarios
        var byIdNull = await _repository.GetByIdAsync(0);
        var byNameNull = await _repository.GetByNameAsync(null!);

        // Assert
        byIdNull.Should().BeNull();
        byNameNull.Should().BeNull();
    }

    #endregion

    #region Enterprise-Specific Business Logic Tests

    [Fact]
    public async Task Test_Enterprise_CalculatesMonthlyBalance()
    {
        // Arrange
        var enterprise = await _repository.GetByIdAsync(1);

        // Assert
        enterprise.Should().NotBeNull();
        var expectedBalance = enterprise!.MonthlyRevenue - enterprise.MonthlyExpenses;
        var actualBalance = enterprise.MonthlyBalance;

        actualBalance.Should().Be(expectedBalance);
        actualBalance.Should().Be(15000m); // 50000 - 35000
    }

    [Fact]
    public async Task Test_Enterprise_ValidatesTypeEnumValues()
    {
        // Arrange
        var waterEnterprise = await _repository.GetByIdAsync(1);
        var sewerEnterprise = await _repository.GetByIdAsync(2);

        // Assert
        waterEnterprise.Should().NotBeNull();
    waterEnterprise!.Type.Should().Be("Water");

        sewerEnterprise.Should().NotBeNull();
    sewerEnterprise!.Type.Should().Be("Sewer");
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
