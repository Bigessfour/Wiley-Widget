using System.Threading.Tasks;
using Npgsql;
using Respawn;
using Xunit;
using WileyWidget.Integration.Tests.Infrastructure;

namespace WileyWidget.Integration.Tests.Postgres
{
    [Collection("Postgres Integration")]
    public class PostgresComposeConnectivityTests
    {
        private readonly DockerComposeFixture _fixture;

        public PostgresComposeConnectivityTests(DockerComposeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Postgres_Compose_ConnectsAndExecutesSql()
        {
            // If the compose-managed Postgres host isn't resolvable, skip this test (compose not started locally).
            try
            {
                var addrs = System.Net.Dns.GetHostAddresses("db-postgres");
                if (addrs == null || addrs.Length == 0) return;
            }
            catch
            {
                return; // Host not resolvable -> compose likely not running locally
            }

            // DockerComposeFixture ensures Postgres is ready before tests run.
            var connStr = "Host=db-postgres;Database=WileyWidget_Test;Username=wiley;Password=WileyTestPassw0rd!;Pooling=false";

            // Try connecting (this will succeed when CI started Postgres via scripts/docker/start-postgres.ps1)
            await using var conn = new NpgsqlConnection(connStr);
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
            await DatabaseHelpers.ResetDatabaseAsync(connStr);

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
