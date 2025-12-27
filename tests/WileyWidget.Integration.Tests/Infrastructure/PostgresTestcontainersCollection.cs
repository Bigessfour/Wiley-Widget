using Xunit;

namespace WileyWidget.Integration.Tests.Infrastructure
{
    /// <summary>
    /// xUnit collection definition for sharing a single PostgresTestcontainerFixture
    /// across multiple test classes to reduce container startup overhead.
    /// </summary>
    [CollectionDefinition("Postgres Testcontainers")]
    public class PostgresTestcontainersCollection : ICollectionFixture<PostgresTestcontainerFixture>
    {
        // Marker class for xUnit collection fixture
    }
}
