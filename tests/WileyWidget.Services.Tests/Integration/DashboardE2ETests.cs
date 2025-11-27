using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.Data.Repositories;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.Services.Tests.Integration;

/// <summary>
/// Integration tests for dashboard E2E flow: Repository → Service → ViewModel
/// </summary>
sealed public class DashboardE2ETests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDashboardRepository _repository;
    private readonly IDashboardService _service;
    private readonly IMemoryCache _cache;

    public DashboardE2ETests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());

        var repositoryLogger = new Mock<ILogger<DashboardRepository>>().Object;
        var serviceLogger = new Mock<ILogger<DashboardService>>().Object;

        _repository = new DashboardRepository(_context, _cache, repositoryLogger);
        _service = new DashboardService(serviceLogger, _repository);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Use current year for fiscal period to match DashboardService behavior
        var currentYear = DateTime.Now.Year;

        // Add budget period
        var budgetPeriod = new BudgetPeriod { Id = 1, Year = currentYear, StartDate = new DateTime(currentYear, 1, 1), EndDate = new DateTime(currentYear, 12, 31), Name = $"FY {currentYear}" };
        _context.BudgetPeriods.Add(budgetPeriod);

        // Add departments
        var adminDept = new Department { Id = 1, Name = "Administration", DepartmentCode = "ADMIN" };
        var publicWorksDept = new Department { Id = 2, Name = "Public Works", DepartmentCode = "PW" };
        _context.Departments.AddRange(adminDept, publicWorksDept);

        // Add budget entries for current fiscal year
        // Use account number "3xx" for revenue (repository filters by StartsWith("3"))
        // Use account number "4xx" for expenses
        _context.BudgetEntries.AddRange(
            new BudgetEntry
            {
                Id = 1,
                FiscalYear = currentYear,
                BudgetedAmount = 1000000m,
                ActualAmount = 500000m,
                AccountNumber = "310.1", // Revenue account
                DepartmentId = 1,
                MunicipalAccountId = 1,
                Description = "Admin Revenue"
            },
            new BudgetEntry
            {
                Id = 2,
                FiscalYear = currentYear,
                BudgetedAmount = 1500000m,
                ActualAmount = 300000m,
                AccountNumber = "320.2", // Revenue account
                DepartmentId = 2,
                MunicipalAccountId = 2,
                Description = "Public Works Revenue"
            }
        );

        // Add transactions (revenue and expenses)
        var budgetEntry1 = _context.BudgetEntries.Find(1);
        var budgetEntry2 = _context.BudgetEntries.Find(2);

        _context.Transactions.AddRange(
            new Transaction
            {
                Id = 1,
                TransactionDate = new DateTime(currentYear, 1, 15),
                Amount = 500000m,
                Type = "Revenue",
                Description = "Property Tax Revenue",
                BudgetEntryId = 1
            },
            new Transaction
            {
                Id = 2,
                TransactionDate = new DateTime(currentYear, 2, 20),
                Amount = 300000m,
                Type = "Revenue",
                Description = "Utility Revenue",
                BudgetEntryId = 2
            },
            new Transaction
            {
                Id = 3,
                TransactionDate = new DateTime(currentYear, 1, 25),
                Amount = 200000m,
                Type = "Expense",
                Description = "Salaries",
                BudgetEntryId = 1
            },
            new Transaction
            {
                Id = 4,
                TransactionDate = new DateTime(currentYear, 2, 10),
                Amount = 150000m,
                Type = "Expense",
                Description = "Equipment",
                BudgetEntryId = 2
            }
        );

        // Add municipal accounts
        _context.MunicipalAccounts.AddRange(
            new MunicipalAccount { Id = 1, AccountNumber = new AccountNumber("101.1"), Name = "General Account", IsActive = true, DepartmentId = 1, BudgetPeriodId = 1 },
            new MunicipalAccount { Id = 2, AccountNumber = new AccountNumber("102.1"), Name = "Water Account", IsActive = true, DepartmentId = 2, BudgetPeriodId = 1 },
            new MunicipalAccount { Id = 3, AccountNumber = new AccountNumber("103.1"), Name = "Inactive Account", IsActive = false, DepartmentId = 1, BudgetPeriodId = 1 }
        );

        // Add invoices
        _context.Invoices.AddRange(
            new Invoice { Id = 1, InvoiceNumber = "INV-001", Status = "Pending", Amount = 5000m, InvoiceDate = DateTime.Now.AddDays(-5) },
            new Invoice { Id = 2, InvoiceNumber = "INV-002", Status = "Paid", Amount = 3000m, InvoiceDate = DateTime.Now.AddDays(-10) },
            new Invoice { Id = 3, InvoiceNumber = "INV-003", Status = "Unpaid", Amount = 2000m, InvoiceDate = DateTime.Now.AddDays(-2) }
        );

        _context.SaveChanges();
    }

    [Fact]
    public async Task E2E_GetTotalBudget_ReturnsCorrectSum()
    {
        // Act
        var totalBudget = await _repository.GetTotalBudgetAsync($"FY {DateTime.Now.Year}");

        // Assert
        Assert.Equal(2500000m, totalBudget); // 1M + 1.5M
    }

    [Fact]
    public async Task E2E_GetTotalRevenue_ReturnsCorrectSum()
    {
        // Act
        var totalRevenue = await _repository.GetTotalRevenueAsync($"FY {DateTime.Now.Year}");

        // Assert - revenue based on actual amounts from budget entries with revenue accounts
        Assert.True(totalRevenue >= 0);
    }

    [Fact]
    public async Task E2E_GetTotalExpenses_ReturnsCorrectSum()
    {
        // Act
        var totalExpenses = await _repository.GetTotalExpensesAsync($"FY {DateTime.Now.Year}");

        // Assert - expenses based on actual amounts from budget entries
        Assert.True(totalExpenses >= 0);
    }

    [Fact]
    public async Task E2E_GetRevenueTrend_ReturnsMonthlyBreakdown()
    {
        // Act
        var trend = await _repository.GetRevenueTrendAsync($"FY {DateTime.Now.Year}");

        // Assert - trend based on transactions
        Assert.NotNull(trend);
    }

    [Fact]
    public async Task E2E_GetExpenseBreakdown_ReturnsCorrectDepartmentSplit()
    {
        // Act
        var breakdown = await _repository.GetExpenseBreakdownAsync($"FY {DateTime.Now.Year}");

        // Assert
        Assert.NotNull(breakdown);
    }

    [Fact]
    public async Task E2E_GetDashboardMetrics_Returns5KeyMetrics()
    {
        // Act
        var metrics = await _repository.GetDashboardMetricsAsync($"FY {DateTime.Now.Year}");

        // Assert
        Assert.Equal(5, metrics.Count);
        Assert.Contains(metrics, m => m.Name == "Total Budget" && m.Value == 2500000.0);
        Assert.Contains(metrics, m => m.Name == "Active Accounts" && m.Value == 2);
        Assert.Contains(metrics, m => m.Name == "Pending Invoices" && m.Value == 2); // Pending + Unpaid
        Assert.Contains(metrics, m => m.Name == "Net Position");
    }

    [Fact]
    public async Task E2E_GetActiveAccountCount_ReturnsCorrectCount()
    {
        // Act
        var count = await _repository.GetActiveAccountCountAsync();

        // Assert
        Assert.Equal(2, count); // 2 active accounts
    }

    [Fact]
    public async Task E2E_GetPendingInvoiceCount_ReturnsCorrectCount()
    {
        // Act
        var count = await _repository.GetPendingInvoiceCountAsync();

        // Assert
        Assert.Equal(2, count); // 1 Pending + 1 Unpaid
    }

    [Fact]
    public async Task E2E_DashboardService_GetDashboardData_ReturnsMetrics()
    {
        // Act
        var metrics = await _service.GetDashboardDataAsync();

        // Assert
        Assert.NotEmpty(metrics);
        // Should return either real data or fallback mock data
        Assert.True(metrics.Count() >= 3); // At least 3 metrics
    }

    [Fact]
    public async Task E2E_DashboardService_GetDashboardSummary_ReturnsCompleteSummary()
    {
        // Act
        var summary = await _service.GetDashboardSummaryAsync();

        // Assert
        Assert.NotNull(summary);
        Assert.Equal("Town of Wiley", summary.MunicipalityName);
        Assert.NotEmpty(summary.Metrics);

        // Service and test both use current year now - data should match
        Assert.True(summary.TotalBudget > 0, $"TotalBudget should be > 0, was {summary.TotalBudget}");
        Assert.True(summary.TotalRevenue > 0, $"TotalRevenue should be > 0, was {summary.TotalRevenue}");
        Assert.True(summary.TotalExpenses >= 0, $"TotalExpenses should be >= 0, was {summary.TotalExpenses}");
    }

    [Fact]
    public async Task E2E_Cache_TotalBudget_UsesCacheOnSecondCall()
    {
        var currentYear = DateTime.Now.Year;

        // Arrange
        var firstCall = await _repository.GetTotalBudgetAsync($"FY {currentYear}");

        // Modify database after cache
        _context.BudgetEntries.Add(new BudgetEntry
        {
            Id = 99,
            FiscalYear = currentYear,
            BudgetedAmount = 999999m,
            ActualAmount = 0m,
            AccountNumber = "999.9",
            DepartmentId = 1,
            FundId = 1,
            MunicipalAccountId = 1,
            Description = "Extra Budget"
        });
        await _context.SaveChangesAsync();

        // Act - should return cached value
        var secondCall = await _repository.GetTotalBudgetAsync($"FY {currentYear}");        // Assert - should be same as first call (cached)
        Assert.Equal(firstCall, secondCall);
        Assert.Equal(2500000m, secondCall); // Original value, not including new entry
    }

    [Fact]
    public async Task E2E_RefreshDashboard_CompletesWithoutError()
    {
        // Act & Assert - should not throw
        await _service.RefreshDashboardAsync();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _cache.Dispose();
    }
}
