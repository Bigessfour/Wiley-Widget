using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Xunit;

namespace WileyWidget.Integration.Tests.Infrastructure
{
    public sealed class PostgresTestcontainerFixture : IAsyncLifetime
    {
        public PostgreSqlTestcontainer? Container { get; private set; }
        public string ConnectionString => Container?.ConnectionString ?? string.Empty;
        public bool DockerAvailable { get; private set; } = true;

        public async Task InitializeAsync()
        {
            try
            {
                // Load credentials from environment to avoid hard-coded secrets (GitGuardian safe).
                // If `POSTGRES_TEST_PASSWORD` is not provided, a cryptographically-random runtime-only password will be generated.
                var dbName = Environment.GetEnvironmentVariable("POSTGRES_TEST_DATABASE") ?? "wileywidget_test";
                var user = Environment.GetEnvironmentVariable("POSTGRES_TEST_USERNAME") ?? "postgres";
                var password = Environment.GetEnvironmentVariable("POSTGRES_TEST_PASSWORD");
                if (string.IsNullOrWhiteSpace(password))
                {
                    // Fallback to a randomly generated strong password for local runs/CI when a secret isn't provided.
                    // Use cryptographically-secure randomness to avoid static prefixes that can trigger secrets scanners.
                    password = GenerateSecureTestPassword();
                }

                var container = new TestcontainersBuilder<PostgreSqlTestcontainer>()
                    .WithDatabase(new PostgreSqlTestcontainerConfiguration
                    {
                        Database = dbName,
                        Username = user,
                        Password = password
                    })
                    .WithImage("postgres:15-alpine")
                    .WithCleanUp(true)
                    .WithAutoRemove(true)
                    .Build();

                await container.StartAsync();
                Container = container;
            }
            catch (Exception)
            {
                // Docker/testcontainers not available - mark and allow tests to skip gracefully
                DockerAvailable = false;
            }
        }

        public async Task DisposeAsync()
        {
            if (DockerAvailable && Container != null)
            {
                try
                {
                    await Container.StopAsync();
                }
                catch { /* best-effort */ }

                try
                {
                    await Container.DisposeAsync();
                }
                catch { /* best-effort */ }
            }
        }

        /// <summary>
        /// Generate a cryptographically-secure, runtime-only password for ephemeral Postgres test containers.
        /// This password is not stored or committed anywhere.
        /// </summary>
        internal static string GenerateSecureTestPassword()
        {
            // 32 bytes -> 64 hex chars; hex is safe for env vars and pass-through to Testcontainers/Postgres.
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes);
        }
    }
}
