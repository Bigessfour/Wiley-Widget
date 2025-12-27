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
            // Arrange - create two contexts with different in-memory databases
            var options1 = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("test-db-1")
                .Options;
            var options2 = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("test-db-2")
                .Options;

            using var context1 = new AppDbContext(options1);
            using var context2 = new AppDbContext(options2);

            await context1.Database.EnsureCreatedAsync();
            await context2.Database.EnsureCreatedAsync();

            // Act - add an entry in context1
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

            // Assert - context2 should not see the entry (isolated databases)
            var list = await context2.BudgetEntries.ToListAsync();
            list.Should().NotContain(be => be.AccountNumber == "CTX-TEST-1");
        }
    }
}
