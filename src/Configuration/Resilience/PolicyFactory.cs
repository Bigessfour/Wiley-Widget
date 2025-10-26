using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

namespace WileyWidget.Configuration.Resilience
{
    public static class PolicyFactory
    {
        public static IAsyncPolicy<HttpResponseMessage> CreateJitteredRetryPolicy(ILogger? logger = null)
        {
            // Jittered retry: 3 attempts with exponential backoff + random jitter
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1)) +
                        TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                    onRetryAsync: async (outcome, timespan, retryCount, context) =>
                    {
                        try
                        {
                            var status = outcome.Result?.StatusCode.ToString() ?? "Exception";
                            logger?.LogWarning("HTTP request failed (attempt {RetryCount}/{MaxRetries}). Status: {Status}. Delaying {Delay}ms",
                                retryCount, 3, status, timespan.TotalMilliseconds);
                        }
                        catch { }
                        await Task.CompletedTask;
                    });
        }

        public static IAsyncPolicy<HttpResponseMessage> CreateDefaultCircuitBreakerPolicy(ILogger? logger = null)
        {
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .CircuitBreakerAsync(
                    failureThreshold: 5,
                    durationOfBreak: TimeSpan.FromSeconds(60),
                    onBreak: (outcome, breakDelay) =>
                    {
                        try { logger?.LogWarning(outcome.Exception, "Circuit breaker opened for {Duration}", breakDelay); } catch { }
                    },
                    onReset: () => { try { logger?.LogInformation("Circuit breaker reset"); } catch { } },
                    onHalfOpen: () => { try { logger?.LogInformation("Circuit breaker half-open"); } catch { } });
        }

        public static PolicyRegistry CreateDefaultPolicyRegistry(ILogger? logger = null)
        {
            var registry = new PolicyRegistry();
            registry.Add("JitteredRetry", CreateJitteredRetryPolicy(logger));
            registry.Add("DefaultCircuitBreaker", CreateDefaultCircuitBreakerPolicy(logger));
            return registry;
        }
    }
}
