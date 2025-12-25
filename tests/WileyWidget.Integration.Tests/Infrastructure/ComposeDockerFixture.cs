using System.Threading.Tasks;
using Xunit;

namespace WileyWidget.Integration.Tests.Infrastructure
{
    // Minimal collection definition and fixture to support Postgres compose connectivity tests.
    // This fixture is intentionally lightweight: tests themselves check host resolvability and will skip if
    // Docker Compose is not running locally. The fixture is a no-op initializer/disposer to allow the tests to compile and run.
    [CollectionDefinition("Postgres Integration")]
    public class PostgresIntegrationCollection : ICollectionFixture<DockerComposeFixture>
    {
    }

    public class DockerComposeFixture : IAsyncLifetime
    {
        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
