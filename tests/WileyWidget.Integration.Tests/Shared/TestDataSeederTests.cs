using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Xunit;

namespace WileyWidget.Integration.Tests.Shared
{
    public class TestDataSeederTests : IntegrationTestBase
    {
        [Fact]
        public async Task SeedBudgets_AssignsValidAccountNumbers()
        {
            // Arrange - ensure departments and accounts exist
            using var scope = CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await TestDataSeeder.SeedDepartmentsAsync(db);
            await TestDataSeeder.SeedMunicipalAccountsAsync(db);

            // Act - seeding budgets should not throw and should create valid entries
            var ex = await Record.ExceptionAsync(() => TestDataSeeder.SeedBudgetsAsync(db));
            Assert.Null(ex);

            var budgets = await db.BudgetEntries.ToListAsync();
            Assert.NotEmpty(budgets);

            var regex = new System.Text.RegularExpressions.Regex(@"^\d{3}(\.\d{1,2})?$");
            foreach (var b in budgets)
            {
                Assert.False(string.IsNullOrWhiteSpace(b.AccountNumber));
                Assert.Matches(regex, b.AccountNumber);
            }
        }
    }
}
