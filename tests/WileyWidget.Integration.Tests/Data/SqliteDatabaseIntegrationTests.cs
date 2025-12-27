using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using WileyWidget.TestUtilities;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Data;
using WileyWidget.Models.Entities;

namespace WileyWidget.Integration.Tests.Data
{
    public class DatabaseIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public async Task AppDbContext_CanCreateAndDelete_WithSqlite()
        {
            // Arrange
            var options = TestHelpers.CreateSqliteInMemoryOptions(out SqliteConnection conn);

            try
            {
                await using var context = new AppDbContext(options);

                // Act
                var canConnect = await context.Database.CanConnectAsync();

                // Assert
                canConnect.Should().BeTrue();
            }
            finally
            {
                conn?.Dispose();
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task BudgetRepository_GetByFiscalYear_ReturnsSeededEntries()
        {
            // Arrange
            var options = TestHelpers.CreateInMemoryOptions();

            await using (var context = new AppDbContext(options))
            {
                // Seed department and fund for FK references
                var dept = new Department { Id = 1, Name = "Public Works", DepartmentCode = "DPW" };
                context.Departments.Add(dept);
                var fund = new Fund { Id = 1, FundCode = "100-GEN", Name = "General", Type = FundType.GeneralFund };
                context.Funds.Add(fund);

                var be = new BudgetEntry
                {
                    Id = 1,
                    AccountNumber = "100",
                    Description = "Test Budget",
                    FiscalYear = 2026,
                    DepartmentId = dept.Id,
                    FundId = fund.Id,
                    BudgetedAmount = 1000m,
                    ActualAmount = 200m,
                    StartPeriod = DateTime.UtcNow.AddMonths(-1),
                    EndPeriod = DateTime.UtcNow.AddMonths(1),
                    MunicipalAccountId = 0 // not used in this test
                };
                context.BudgetEntries.Add(be);
                await context.SaveChangesAsync();
            }

            var factory = new AppDbContextFactory(options);
            var repo = new BudgetRepository(factory, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var results = await repo.GetByFiscalYearAsync(2026);

            // Assert
            results.Should().ContainSingle().Which.AccountNumber.Should().Be("100");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task BudgetRepository_GetPagedAsync_ReturnsCorrectPaging()
        {
            // Arrange
            var options = TestHelpers.CreateInMemoryOptions();

            await using (var context = new AppDbContext(options))
            {
                var dept = new Department { Id = 1, Name = "Dep", DepartmentCode = "D1" };
                context.Departments.Add(dept);
                var fund = new Fund { Id = 1, FundCode = "F1", Name = "Fund1", Type = FundType.GeneralFund };
                context.Funds.Add(fund);

                for (int i = 1; i <= 30; i++)
                {
                    context.BudgetEntries.Add(new BudgetEntry
                    {
                        AccountNumber = i.ToString("D3"),
                        Description = $"Entry {i}",
                        FiscalYear = 2026,
                        DepartmentId = dept.Id,
                        FundId = fund.Id,
                        BudgetedAmount = i * 10m,
                        MunicipalAccountId = 0
                    });
                }

                await context.SaveChangesAsync();
            }

            var factory = new AppDbContextFactory(options);
            var repo = new BudgetRepository(factory, new MemoryCache(new MemoryCacheOptions()));

            // Act
            var (items, total) = await repo.GetPagedAsync(pageNumber: 2, pageSize: 10, fiscalYear: 2026);

            // Assert
            total.Should().Be(30);
            items.Should().HaveCount(10);
            items.First().AccountNumber.Should().Be("011"); // page 2 starts at 11 (0-indexed behavior check)
        }
    }
}
