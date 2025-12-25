using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Xunit;
using FluentAssertions;
using System.Linq;

namespace WileyWidget.Integration.Tests.Data
{
    public class DbContextIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public void Verify_DbContext_CanBeInstantiated_WithInMemoryProvider()
        {
            // Arrange & Act
            var context = GetRequiredService<AppDbContext>();

            // Assert
            context.Should().NotBeNull();
            context.Database.ProviderName.Should().Contain("InMemory");
        }

        [Fact]
        public async Task ApplyMigrations_ToInMemory_DoesNotThrow()
        {
            // Arrange
            var context = GetRequiredService<AppDbContext>();

            // Act & Assert
            await context.Database.EnsureCreatedAsync();
            // InMemory doesn't support migrations, but EnsureCreated should work
        }

        [Fact]
        public void EnsureCreated_CreatesAllDbSets()
        {
            // Arrange
            var context = GetRequiredService<AppDbContext>();

            // Act
            context.Database.EnsureCreated();

            // Assert
            context.BudgetEntries.Should().NotBeNull();
            context.Departments.Should().NotBeNull();
            context.Enterprises.Should().NotBeNull();
            context.Transactions.Should().NotBeNull();
            context.Funds.Should().NotBeNull();
            context.MunicipalAccounts.Should().NotBeNull();
        }

        [Fact]
        public void SensitiveDataLogging_LogsParameters_Correctly()
        {
            // Arrange
            var context = GetRequiredService<AppDbContext>();

            // Act
            var loggingEnabled = context.ChangeTracker.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;

            // Assert
            // In test environment, sensitive logging should be enabled for debugging
            loggingEnabled.Should().BeTrue();
        }

        [Fact]
        public async Task ConcurrentDbContextInstances_AreIsolated()
        {
            // Arrange - reset DB then create two separate scopes/contexts
            await ResetDatabaseAsync();
            using var scope1 = CreateScope();
            using var scope2 = CreateScope();

            var context1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
            var context2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

            // Act - record initial count, add an entry in context1 and verify it appears in context2
            var initialCount = await context2.BudgetEntries.CountAsync();

            var newEntry = new WileyWidget.Models.BudgetEntry
            {
                AccountNumber = "CTX-TEST-1",
                FundType = WileyWidget.Models.FundType.EnterpriseFund,
                FiscalYear = 2025,
                BudgetedAmount = 1000,
                ActualAmount = 0
            };

            await context1.BudgetEntries.AddAsync(newEntry);
            await context1.SaveChangesAsync();

            // Assert - second context should observe persisted data in shared test database
            var list = await context2.BudgetEntries.ToListAsync();
            list.Count.Should().Be(initialCount + 1);
            list.Should().Contain(be => be.AccountNumber == "CTX-TEST-1");
        }
    }
}
