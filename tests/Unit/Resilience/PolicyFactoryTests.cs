using System.Net.Http;
using Polly;
using Polly.Registry;
using Unit.Resilience.TestHelpers;
using Xunit;

namespace Unit.Resilience
{
    public class PolicyFactoryTests
    {
        [Fact]
        public void CreateDefaultPolicyRegistry_ContainsExpectedKeys()
        {
            // Act
            PolicyRegistry registry = TestPolicyFactory.CreateDefaultPolicyRegistry();

            // Assert
            Assert.True(registry.ContainsKey("JitteredRetry"));
            Assert.True(registry.ContainsKey("DefaultCircuitBreaker"));

            var retry = registry.Get<IAsyncPolicy<HttpResponseMessage>>("JitteredRetry");
            var cb = registry.Get<IAsyncPolicy<HttpResponseMessage>>("DefaultCircuitBreaker");

            Assert.NotNull(retry);
            Assert.NotNull(cb);
        }
    }
}
