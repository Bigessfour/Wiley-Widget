using System;
using System.Threading.Tasks;
using Xunit;
using Npgsql;
using WileyWidget.Integration.Tests.Infrastructure;

namespace WileyWidget.Integration.Tests.Postgres
{
    public class PostgresPasswordSanityTests
    {
        [Fact]
        public async Task Postgres_Uses_ProvidedOrGenerates_SecurePassword()
        {
            var envPassword = Environment.GetEnvironmentVariable("POSTGRES_TEST_PASSWORD");
            var fixture = new PostgresTestcontainerFixture();
            await fixture.InitializeAsync();
            try
            {
                if (!fixture.DockerAvailable)
                {
                    return; // Docker not available on this environment - skip the live Postgres check
                }

                var usedPassword = fixture.Password;
                Assert.False(string.IsNullOrWhiteSpace(usedPassword), "The fixture must supply a non-empty password");

                if (!string.IsNullOrWhiteSpace(envPassword))
                {
                    Assert.Equal(envPassword, usedPassword);
                }
                else
                {
                    // Generated password should have sufficient entropy (we expect hex-encoded 32 bytes -> 64 chars)
                    Assert.True(usedPassword.Length >= 32, "Generated password should have sufficient entropy");
                    Assert.Matches("^[0-9A-Fa-f]+$", usedPassword);
                    Assert.NotEqual("postgres", usedPassword, StringComparer.OrdinalIgnoreCase);
                }

                // Confirm we can connect using the connection string
                await using var conn = new NpgsqlConnection(fixture.ConnectionString);
                await conn.OpenAsync();
                Assert.True(conn.FullState.HasFlag(System.Data.ConnectionState.Open));
            }
            finally
            {
                await fixture.DisposeAsync();
            }
        }
    }
}
