using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Integration.Tests.Infrastructure;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class DataSeedingServicePostgresTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public DataSeedingServicePostgresTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact, Trait("Category", "Integration")]
        public async Task SeedBudgetDataAsync_PopulatesDatabase_WhenEmpty()
        {
            // Skip if Docker/testcontainers not available in this environment
            if (!_fixture.DockerAvailable) return;

            var connStr = _fixture.ConnectionString;

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connStr)
                .Options;

            await using (var ctx = new AppDbContext(options))
            {
                // Ensure schema applied
                await ctx.Database.MigrateAsync();

                // Reset any existing data to ensure a clean start
                await DatabaseHelpers.ResetDatabaseAsync(connStr);

                var svc = new DataSeedingService(ctx, NullLogger<DataSeedingService>.Instance);

                var result = await svc.SeedBudgetDataAsync();

                Assert.True(result.InsertedRecords > 0, "Seeding should insert records into Postgres");
                Assert.True((await ctx.BudgetEntries.CountAsync()) > 0, "BudgetEntries should be populated");

                // Idempotency: subsequent runs should not re-insert
                var second = await svc.SeedBudgetDataAsync();
                Assert.Equal(0, second.InsertedRecords);
            }
        }
    }
}
