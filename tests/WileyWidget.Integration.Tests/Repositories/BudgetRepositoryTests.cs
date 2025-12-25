using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Integration.Tests.Shared;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Business.Interfaces;
using Xunit;

namespace WileyWidget.Integration.Tests.Repositories
{
    public class BudgetRepositoryTests : IntegrationTestBase
    {
        public BudgetRepositoryTests() : base()
        {
        }

        [Fact]
        public async Task GetByFiscalYearAsync_ReturnsEntries_WithDepartmentAndFund()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Public Works" };
            var fund = new Fund { FundCode = "100-General", Name = "General Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("100", 2099, dept, fund, 1234.56m);

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = (await repo.GetByFiscalYearAsync(2099)).ToList();

            // Assert
            result.Should().HaveCount(1);
            var single = result.Single();
            single.AccountNumber.Should().Be("100");
            single.BudgetedAmount.Should().Be(1234.56m);
            single.Department.Should().NotBeNull();
            single.Fund.Should().NotBeNull();
            single.Department!.Name.Should().Be("Public Works");
            single.Fund!.Name.Should().Be("General Fund");
        }

        [Fact]
        public async Task GetByDepartmentAsync_ReturnsOnlyMatchingDepartment()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var deptA = new Department { Name = "A Dept" };
            var deptB = new Department { Name = "B Dept" };
            var fund = new Fund { FundCode = "200-Other", Name = "Other Fund", Type = FundType.GeneralFund };

            db.Departments.AddRange(deptA, deptB);
            db.Add(fund);

            var beA = CreateBudgetEntry("200", 2099, deptA, fund, 100m);
            var beB = CreateBudgetEntry("201", 2099, deptB, fund, 200m);

            db.BudgetEntries.AddRange(beA, beB);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = (await repo.GetByDepartmentAsync(deptA.Id)).ToList();

            // Assert
            result.Should().ContainSingle()
                .Which.Department.Should().NotBeNull();
            result.Single().Department.Id.Should().Be(deptA.Id);
        }

        [Fact]
        public async Task GetDataStatisticsAsync_ReturnsCountsAndDates()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Stats Dept" };
            var fund = new Fund { FundCode = "400-Stat", Name = "Stats Fund", Type = FundType.GeneralFund };

            db.Departments.Add(dept);
            db.Add(fund);

            var fiscalYear = 2099;
            DateTime now = DateTime.UtcNow;

            for (int i = 1; i <= 7; i++)
            {
                var be = CreateBudgetEntry((400 + i).ToString(CultureInfo.InvariantCulture), fiscalYear, dept, fund, i * 50m);
                be.CreatedAt = now.AddMinutes(i);
                db.BudgetEntries.Add(be);
            }

            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var (totalRecords, oldest, newest) = await repo.GetDataStatisticsAsync(fiscalYear);

            // Assert
            totalRecords.Should().Be(7);
            oldest.Should().HaveValue();
            newest.Should().HaveValue();
            oldest.Value.Should().BeBefore(newest.Value);
        }

        [Fact]
        public async Task GetByFundAndFiscalYearAsync_ReturnsMatchingEntries()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Fund Dept" };
            var fund = new Fund { FundCode = "500-Fund", Name = "Test Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("500", 2099, dept, fund, 1000m);

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = await repo.GetByFundAndFiscalYearAsync(fund.Id, 2099);

            // Assert
            result.Should().ContainSingle();
            result.Single().FundId.Should().Be(fund.Id);
        }

        [Fact]
        public async Task GetByDepartmentAndFiscalYearAsync_ReturnsMatchingEntries()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Dept FY" };
            var fund = new Fund { FundCode = "600-Dept", Name = "Dept Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("600", 2099, dept, fund, 2000m);

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = await repo.GetByDepartmentAndFiscalYearAsync(dept.Id, 2099);

            // Assert
            result.Should().ContainSingle();
            result.Single().DepartmentId.Should().Be(dept.Id);
        }

        [Fact]
        public async Task GetSewerBudgetEntriesAsync_ReturnsSewerEntries()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Sewer Dept" };
            var fund = new Fund { FundCode = "700-Sewer", Name = "Sewer Fund", Type = FundType.EnterpriseFund };

            var entry = CreateBudgetEntry("700", 2099, dept, fund, 3000m);

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = await repo.GetSewerBudgetEntriesAsync(2099);

            // Assert
            result.Should().ContainSingle();
            result.Single().Fund.Type.Should().Be(FundType.EnterpriseFund);
        }

        [Fact]
        public async Task GetBudgetSummaryAsync_EnterpriseVariant_ReturnsSummary()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Enterprise Dept" };
            var fund = new Fund { FundCode = "800-Ent", Name = "Enterprise Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("800", 2099, dept, fund, 4000m);
            entry.ActualAmount = 1000m;

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act - query the fiscal year range used for seeded entries
            var result = await repo.GetBudgetSummaryAsync(new DateTime(2099, 1, 1), new DateTime(2099, 12, 31));

            // Assert
            result.TotalBudgeted.Should().BeGreaterThan(0);
            result.TotalActual.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetVarianceAnalysisAsync_ReturnsAnalysis()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Variance Dept" };
            var fund = new Fund { FundCode = "900-Var", Name = "Variance Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("900", 2099, dept, fund, 5000m);
            entry.ActualAmount = 2000m;

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = await repo.GetVarianceAnalysisAsync(new DateTime(2099, 1, 1), new DateTime(2099, 12, 31));

            // Assert
            result.Should().NotBeNull();
            // Assuming it returns some analysis data
        }

        [Fact]
        public async Task GetYearEndSummaryAsync_ReturnsSummary()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "YearEnd Dept" };
            var fund = new Fund { FundCode = "1000-YE", Name = "YearEnd Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("1000", 2099, dept, fund, 6000m);
            entry.ActualAmount = 3000m;

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = await repo.GetYearEndSummaryAsync(2099);

            // Assert
            result.Should().NotBeNull();
            // Assuming it returns year-end data
        }

        [Fact]
        public async Task GetFundAllocationsAsync_ReturnsAllocations()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Alloc Dept" };
            var fund = new Fund { FundCode = "1100-Alloc", Name = "Alloc Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("1100", 2099, dept, fund, 7000m);

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = await repo.GetFundAllocationsAsync(new DateTime(2099, 1, 1), new DateTime(2099, 12, 31));

            // Assert
            result.Should().NotBeNull();
            // Assuming it returns allocation data
        }

        [Fact]
        public async Task GetDepartmentBreakdownAsync_ReturnsBreakdown()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Breakdown Dept" };
            var fund = new Fund { FundCode = "1200-BD", Name = "Breakdown Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("1200", 2099, dept, fund, 8000m);

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act
            var result = await repo.GetDepartmentBreakdownAsync(new DateTime(2099, 1, 1), new DateTime(2099, 12, 31));

            // Assert
            result.Should().NotBeNull();
            // Assuming it returns breakdown data
        }

        [Fact]
        public async Task GetByDateRangeAsync_ReturnsEntriesInRange()
        {
            // Arrange
            await ResetDatabaseAsync();
            var db = GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Date Dept" };
            var fund = new Fund { FundCode = "1300-Date", Name = "Date Fund", Type = FundType.GeneralFund };

            var entry = CreateBudgetEntry("1300", 2099, dept, fund, 9000m);

            db.Departments.Add(dept);
            db.Add(fund);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            var repo = GetRequiredService<IBudgetRepository>();

            // Act - query the fiscal year range used for seeded entries
            var result = await repo.GetByDateRangeAsync(new DateTime(2099, 1, 1), new DateTime(2099, 12, 31));

            // Assert
            result.Should().NotBeEmpty();
        }

        private BudgetEntry CreateBudgetEntry(string account, int fiscalYear, Department dept, Fund fund, decimal amount = 0m)
        {
            return new BudgetEntry
            {
                AccountNumber = account,
                Description = $"Desc {account}",
                BudgetedAmount = amount,
                FiscalYear = fiscalYear,
                StartPeriod = new DateTime(fiscalYear, 1, 1),
                EndPeriod = new DateTime(fiscalYear, 12, 31),
                Department = dept,
                Fund = fund,
            };
        }
    }
}
