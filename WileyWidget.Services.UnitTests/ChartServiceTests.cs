using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using WileyWidget.Data;
using WileyWidget.Data.Services;
using WileyWidget.Models;
using WileyWidget.Abstractions.Models;
using Xunit;

public class ChartServiceTests : IDisposable
{
    private readonly AppDbContext _context;

    public ChartServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context?.Dispose();
        }
    }

    [Fact]
    public async Task GetMonthlyTotalsAsync_Returns12Months_WithZerosForMissing()
    {
        // Arrange: Seed data for Jan (1) and Mar (3)
        _context.Transactions.AddRange(
            new Transaction { TransactionDate = new DateTime(2025, 1, 1), Amount = 100 },
            new Transaction { TransactionDate = new DateTime(2025, 3, 1), Amount = 200 });
        await _context.SaveChangesAsync();
        var service = new ChartService(_context);

        // Act
        var result = await service.GetMonthlyTotalsAsync(2025);

        // Assert
        result.Should().HaveCount(12);
        result.First(p => p.Category == "Jan").Value.Should().Be(100);
        result.First(p => p.Category == "Feb").Value.Should().Be(0);
        result.First(p => p.Category == "Mar").Value.Should().Be(200);
    }

    [Fact]
    public async Task GetMonthlyTotalsAsync_FiltersByYear()
    {
        // Arrange: Data in 2024 and 2025
        _context.Transactions.Add(new Transaction { TransactionDate = new DateTime(2024, 1, 1), Amount = 50 });
        _context.Transactions.Add(new Transaction { TransactionDate = new DateTime(2025, 1, 1), Amount = 100 });
        await _context.SaveChangesAsync();
        var service = new ChartService(_context);

        // Act
        var result = await service.GetMonthlyTotalsAsync(2025);

        // Assert
        result.First(p => p.Category == "Jan").Value.Should().Be(100);
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_GroupsByDepartment_WithSums()
    {
        // Arrange: Seed transactions with departments
        var dept1 = new Department { Name = "Ops" };
        var dept2 = new Department { Name = "Sales" };
        _context.Transactions.AddRange(
            new Transaction { TransactionDate = DateTime.UtcNow, Amount = 100, BudgetEntry = new BudgetEntry { Department = dept1 } },
            new Transaction { TransactionDate = DateTime.UtcNow, Amount = 200, BudgetEntry = new BudgetEntry { Department = dept1 } },
            new Transaction { TransactionDate = DateTime.UtcNow, Amount = 150, BudgetEntry = new BudgetEntry { Department = dept2 } });
        await _context.SaveChangesAsync();
        var service = new ChartService(_context);
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await service.GetCategoryBreakdownAsync(start, end);

        // Assert
        result.Should().HaveCount(2);
        result.First(p => p.Category == "Ops").Value.Should().Be(300);
        result.First(p => p.Category == "Sales").Value.Should().Be(150);
        result.Should().BeInDescendingOrder(p => p.Value);
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_HandlesUnassigned()
    {
        // Arrange: Transaction with budget entry linked to department with null name
        var testDate = new DateTime(2025, 1, 15);
        var municipalAccount = new MunicipalAccount { Name = "Test Account" };
        var department = new Department { Name = "(Unassigned)" }; // Department with name "(Unassigned)"
        _context.MunicipalAccounts.Add(municipalAccount);
        _context.Departments.Add(department);
        await _context.SaveChangesAsync(); // Save to get IDs
        var budgetEntry = new BudgetEntry {
            Description = "Test Entry",
            AccountNumber = "100",
            FiscalYear = 2025,
            DepartmentId = department.Id,
            MunicipalAccountId = municipalAccount.Id
        };
        _context.BudgetEntries.Add(budgetEntry);
        _context.Transactions.Add(new Transaction { TransactionDate = testDate, Amount = 100, BudgetEntryId = budgetEntry.Id });
        await _context.SaveChangesAsync();
        var service = new ChartService(_context);

        // Act
        var result = await service.GetCategoryBreakdownAsync(testDate.AddDays(-1), testDate.AddDays(1));

        // Assert
        result.Should().HaveCount(1);
        result.First().Category.Should().Be("(Unassigned)");
        result.First().Value.Should().Be(100);
    }    [Fact]
    public async Task GetCategoryBreakdownAsync_FiltersByDateRange()
    {
        // Arrange: Transactions in/out of range
        var baseDate = new DateTime(2025, 1, 15);
        var municipalAccount = new MunicipalAccount { Name = "Test Account" };
        _context.MunicipalAccounts.Add(municipalAccount);
        var budgetEntry = new BudgetEntry {
            Description = "Test Entry",
            AccountNumber = "100",
            FiscalYear = 2025,
            DepartmentId = 1, // Valid department
            MunicipalAccountId = municipalAccount.Id
        };
        var department = new Department { Name = "Test Dept" };
        _context.Departments.Add(department);
        _context.BudgetEntries.Add(budgetEntry);
        _context.Transactions.Add(new Transaction { TransactionDate = baseDate.AddDays(-2), Amount = 50, BudgetEntryId = budgetEntry.Id }); // Out
        _context.Transactions.Add(new Transaction { TransactionDate = baseDate, Amount = 100, BudgetEntryId = budgetEntry.Id }); // In
        await _context.SaveChangesAsync();
        var service = new ChartService(_context);
        var start = baseDate.AddDays(-1);
        var end = baseDate.AddDays(1);

        // Act
        var result = await service.GetCategoryBreakdownAsync(start, end);

        // Assert
        result.Sum(p => p.Value).Should().Be(100);
    }
}
