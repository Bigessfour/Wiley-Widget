using System.Threading.Tasks;
using Npgsql;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;
using Xunit.Sdk;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class PostgresConnectivityTests : IClassFixture<PostgresTestcontainerFixture>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public PostgresConnectivityTests(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Postgres_Container_ConnectsAndExecutesSql()
        {
            if (!_fixture.DockerAvailable)
                return; // Docker not available on this environment - skip the live Postgres check

            await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS test_items (id SERIAL PRIMARY KEY, name TEXT NOT NULL); INSERT INTO test_items (name) VALUES ('foo');";
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM test_items;";
                var count = (long)await cmd.ExecuteScalarAsync();
                Assert.Equal(1, count);
            }

            // Reset using Respawn helper
            await DatabaseHelpers.ResetDatabaseAsync(_fixture.ConnectionString);

            // Re-open to observe reset
            await conn.CloseAsync();
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM test_items;";
                var count = (long)await cmd.ExecuteScalarAsync();
                Assert.Equal(0, count);
            }
        }
    }
}
