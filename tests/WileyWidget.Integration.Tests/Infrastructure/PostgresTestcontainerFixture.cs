using System;
using System.Threading.Tasks;
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
                var container = new TestcontainersBuilder<PostgreSqlTestcontainer>()
                    .WithDatabase(new PostgreSqlTestcontainerConfiguration
                    {
                        Database = "wileywidget_test",
                        Username = "postgres",
                        Password = "postgres"
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
    }
}
