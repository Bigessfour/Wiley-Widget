using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace Unit.Resilience
{
    public class ResilienceBehaviorTests
    {
        [Fact]
        public async Task RetryPolicy_RetriesOnTransientFailures()
        {
            int attempts = 0;

            // Create a test policy with zero delay so test runs fast
            var retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(3, _ => TimeSpan.Zero);

            Func<Task<HttpResponseMessage>> action = async () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            var result = await retryPolicy.ExecuteAsync(action);

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task CircuitBreaker_OpensAfterThreshold()
        {
            // Create a circuit-breaker that opens after 2 failures and stays open briefly
            var cb = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .CircuitBreakerAsync(2, TimeSpan.FromSeconds(1));

            // First two calls fail
            Task<HttpResponseMessage> failing() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            await cb.ExecuteAsync(failing);
            await cb.ExecuteAsync(failing);

            // Next call should throw a BrokenCircuitException (generic in this Polly version)
            await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(async () => await cb.ExecuteAsync(failing));

            // Wait for break to reset
            await Task.Delay(1100);

            // After break duration, circuit should allow calls again
            var ok = await cb.ExecuteAsync(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
    }
}
