using System.Threading.Tasks;
using Npgsql;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;
using WileyWidget.Data;
using Microsoft.EntityFrameworkCore;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class MigrationsApplyTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;
        public MigrationsApplyTests(PostgresTestcontainerFixture fixture) { _fixture = fixture; }

        [Fact, Trait("Category", "Integration")]
        public async Task Migrations_ApplyToPostgres_DbSchemaCreated()
        {
            if (!_fixture.DockerAvailable) return;

            var connStr = _fixture.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connStr)
                .Options;

            await using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
            }

            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND lower(table_name)='budgetentries';";
                var result = await cmd.ExecuteScalarAsync();
                Assert.NotNull(result);
                var count = Convert.ToInt64(result.ToString(), System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(count > 0, "BudgetEntries table should exist after applying migrations");
            }

            await DatabaseHelpers.ResetDatabaseAsync(connStr);
        }
    }
}
