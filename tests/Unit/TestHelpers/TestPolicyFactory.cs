using System;
using System.Net.Http;
using Polly;
using Polly.Registry;

namespace Unit.Resilience.TestHelpers
{
    public static class TestPolicyFactory
    {
        public static IAsyncPolicy<HttpResponseMessage> CreateJitteredRetryPolicy()
        {
            // Small/zero delays so unit tests run fast
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(3, _ => TimeSpan.Zero);
        }

        public static IAsyncPolicy<HttpResponseMessage> CreateDefaultCircuitBreakerPolicy()
        {
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(1));
        }

        public static PolicyRegistry CreateDefaultPolicyRegistry()
        {
            var registry = new PolicyRegistry();
            registry.Add("JitteredRetry", CreateJitteredRetryPolicy());
            registry.Add("DefaultCircuitBreaker", CreateDefaultCircuitBreakerPolicy());
            return registry;
        }
    }
}
