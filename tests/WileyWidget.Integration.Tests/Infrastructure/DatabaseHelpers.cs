using System.Threading.Tasks;
using Npgsql;

namespace WileyWidget.Integration.Tests.Infrastructure
{
    public static class DatabaseHelpers
    {
        public static async Task ResetDatabaseAsync(string connectionString)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            // Truncate known test tables - keep this lightweight and resilient
            cmd.CommandText = "TRUNCATE TABLE IF EXISTS test_items;";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
