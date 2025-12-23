using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class MigrationsPostgresTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public MigrationsPostgresTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact, Trait("Category", "Integration")]
        public async Task Database_Migrations_ApplySuccessfully()
        {
            if (!_fixture.DockerAvailable) return;

            var connStr = _fixture.ConnectionString;

            var options = new DbContextOptionsBuilder<WileyWidget.Data.AppDbContext>()
                .UseNpgsql(connStr)
                .Options;

            await using var ctx = new WileyWidget.Data.AppDbContext(options);
            // Ensure a clean DB
            await DatabaseHelpers.ResetDatabaseAsync(connStr);

            // Apply migrations
            await ctx.Database.MigrateAsync();

            // Check that migrations table has entries
            var applied = ctx.Database.GetAppliedMigrations();
            Assert.True(applied.Any(), "Migrations should have been applied and at least one entry present in __EFMigrationsHistory");
        }
    }
}
