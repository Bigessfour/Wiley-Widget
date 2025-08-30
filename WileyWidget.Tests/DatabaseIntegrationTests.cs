using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive database integration tests for enterprise repository operations
/// Tests actual database interactions with in-memory database for isolation
/// </summary>
public class DatabaseIntegrationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly EnterpriseRepository _repository;
    private bool _disposed = false;

    public DatabaseIntegrationTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);
        _repository = new EnterpriseRepository(_context);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region Repository CRUD Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllEnterprises()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise { Name = "Water Dept", CurrentRate = 2.50m, MonthlyExpenses = 10000m, CitizenCount = 5000 },
            new Enterprise { Name = "Sewer Dept", CurrentRate = 3.00m, MonthlyExpenses = 15000m, CitizenCount = 6000 },
            new Enterprise { Name = "Trash Dept", CurrentRate = 1.50m, MonthlyExpenses = 8000m, CitizenCount = 4000 }
        };

        await _context.Enterprises.AddRangeAsync(enterprises);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count());
        Assert.Contains(result, e => e.Name == "Water Dept");
        Assert.Contains(result, e => e.Name == "Sewer Dept");
        Assert.Contains(result, e => e.Name == "Trash Dept");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise",
            CurrentRate = 2.00m,
            MonthlyExpenses = 5000m,
            CitizenCount = 3000
        };

        await _context.Enterprises.AddAsync(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(enterprise.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(enterprise.Id, result.Id);
        Assert.Equal("Test Enterprise", result.Name);
        Assert.Equal(2.00m, result.CurrentRate);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForNonExistentId()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_AddsEnterpriseToDatabase()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "New Enterprise",
            CurrentRate = 4.00m,
            MonthlyExpenses = 20000m,
            CitizenCount = 8000
        };

        // Act
        var result = await _repository.AddAsync(enterprise);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Enterprise", result.Name);
        Assert.True(result.Id > 0); // Should have been assigned an ID

        // Verify in database
        var fromDb = await _context.Enterprises.FindAsync(result.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("New Enterprise", fromDb.Name);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Original Name",
            CurrentRate = 2.00m,
            MonthlyExpenses = 10000m,
            CitizenCount = 5000
        };

        await _context.Enterprises.AddAsync(enterprise);
        await _context.SaveChangesAsync();

        // Modify enterprise
        enterprise.Name = "Updated Name";
        enterprise.CurrentRate = 2.50m;
        enterprise.MonthlyExpenses = 12000m;

        // Act
        var result = await _repository.UpdateAsync(enterprise);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal(2.50m, result.CurrentRate);
        Assert.Equal(12000m, result.MonthlyExpenses);

        // Verify in database
        var fromDb = await _context.Enterprises.FindAsync(enterprise.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("Updated Name", fromDb.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEnterpriseFromDatabase()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Enterprise to Delete",
            CurrentRate = 1.00m,
            MonthlyExpenses = 5000m,
            CitizenCount = 2000
        };

        await _context.Enterprises.AddAsync(enterprise);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(enterprise.Id);

        // Assert
        var fromDb = await _context.Enterprises.FindAsync(enterprise.Id);
        Assert.Null(fromDb);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task Repository_CalculatesRevenueCorrectly()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Revenue Test",
            CurrentRate = 5.00m,
            MonthlyExpenses = 10000m,
            CitizenCount = 3000
        };

        await _context.Enterprises.AddAsync(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var fromDb = await _repository.GetByIdAsync(enterprise.Id);

        // Assert
        Assert.NotNull(fromDb);
        Assert.Equal(15000m, fromDb.MonthlyRevenue); // 5.00 * 3000
    }

    [Fact]
    public async Task Repository_CalculatesBalanceCorrectly()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Balance Test",
            CurrentRate = 4.00m,
            MonthlyExpenses = 10000m,
            CitizenCount = 2500
        };

        await _context.Enterprises.AddAsync(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var fromDb = await _repository.GetByIdAsync(enterprise.Id);

        // Assert
        Assert.NotNull(fromDb);
        Assert.Equal(0m, fromDb.MonthlyBalance); // (4.00 * 2500) - 10000 = 0
    }

    [Fact]
    public async Task Repository_HandlesCalculatedPropertiesCorrectly()
    {
        // Arrange
        var surplusEnterprise = new Enterprise
        {
            Name = "Surplus Enterprise",
            CurrentRate = 3.00m,
            MonthlyExpenses = 5000m,
            CitizenCount = 3000
        };

        var deficitEnterprise = new Enterprise
        {
            Name = "Deficit Enterprise",
            CurrentRate = 1.00m,
            MonthlyExpenses = 5000m,
            CitizenCount = 3000
        };

        await _context.Enterprises.AddRangeAsync(surplusEnterprise, deficitEnterprise);
        await _context.SaveChangesAsync();

        // Act
        var surplusFromDb = await _repository.GetByIdAsync(surplusEnterprise.Id);
        var deficitFromDb = await _repository.GetByIdAsync(deficitEnterprise.Id);

        // Assert
        Assert.NotNull(surplusFromDb);
        Assert.NotNull(deficitFromDb);

        // Test calculated properties
        Assert.Equal(9000m, surplusFromDb.MonthlyRevenue);    // 3.00 * 3000
        Assert.Equal(4000m, surplusFromDb.MonthlyBalance);    // 9000 - 5000
        Assert.Equal(3000m, deficitFromDb.MonthlyRevenue);    // 1.00 * 3000
        Assert.Equal(-2000m, deficitFromDb.MonthlyBalance);   // 3000 - 5000
        Assert.Equal(1.67m, Math.Round(surplusFromDb.BreakEvenRate, 2)); // 5000 / 3000
    }

    #endregion

    #region Data Validation Tests

    [Fact]
    public void Repository_ValidatesRequiredFields()
    {
        // Arrange - Try to add enterprise with missing required fields
        var invalidEnterprise = new Enterprise
        {
            // Name is required but not set
            CurrentRate = 1.00m,
            MonthlyExpenses = 1000m,
            CitizenCount = 1000
        };

        // Act - Validate the model
        var validationContext = new ValidationContext(invalidEnterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(invalidEnterprise, validationContext, validationResults, true);

        // Assert - Should have validation errors
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Repository_EnforcesRateConstraints()
    {
        // Arrange - Try to add enterprise with invalid rate
        var invalidEnterprise = new Enterprise
        {
            Name = "Invalid Rate Test",
            CurrentRate = -1.00m, // Invalid: negative rate
            MonthlyExpenses = 1000m,
            CitizenCount = 1000
        };

        // Act - Validate the model
        var validationContext = new ValidationContext(invalidEnterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(invalidEnterprise, validationContext, validationResults, true);

        // Assert - Should have validation errors
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.ErrorMessage.Contains("Rate"));
    }

    [Fact]
    public void Repository_EnforcesCitizenCountConstraints()
    {
        // Arrange - Try to add enterprise with invalid citizen count
        var invalidEnterprise = new Enterprise
        {
            Name = "Invalid Citizen Count Test",
            CurrentRate = 1.00m,
            MonthlyExpenses = 1000m,
            CitizenCount = -1 // Invalid: negative citizen count
        };

        // Act - Validate the model
        var validationContext = new ValidationContext(invalidEnterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(invalidEnterprise, validationContext, validationResults, true);

        // Assert - Should have validation errors
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.ErrorMessage.Contains("Rate"));
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Repository_HandlesLargeDatasetEfficiently()
    {
        // Arrange - Create 100 enterprises
        var enterprises = new List<Enterprise>();
        for (int i = 0; i < 100; i++)
        {
            enterprises.Add(new Enterprise
            {
                Name = $"Enterprise {i}",
                CurrentRate = 2.00m + (i * 0.01m),
                MonthlyExpenses = 5000m + (i * 100m),
                CitizenCount = 1000 + i
            });
        }

        await _context.Enterprises.AddRangeAsync(enterprises);
        await _context.SaveChangesAsync();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository.GetAllAsync();
        stopwatch.Stop();

        // Assert
        Assert.Equal(100, result.Count());
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should complete within 1 second
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task Repository_HandlesConcurrentReads()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Concurrency Test",
            CurrentRate = 2.00m,
            MonthlyExpenses = 5000m,
            CitizenCount = 3000
        };

        await _context.Enterprises.AddAsync(enterprise);
        await _context.SaveChangesAsync();

        // Act - Perform multiple concurrent reads
        var tasks = new List<Task<Enterprise>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_repository.GetByIdAsync(enterprise.Id));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.NotNull(result));
        Assert.All(results, result => Assert.Equal("Concurrency Test", result.Name));
    }

    #endregion
}

/// <summary>
/// Test-specific DbContext for isolated testing
/// </summary>
public class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Ensure we don't override the test database configuration
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
        }
    }
}
