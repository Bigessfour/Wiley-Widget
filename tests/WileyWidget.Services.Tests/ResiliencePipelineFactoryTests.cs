using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using WileyWidget.Services.Resilience;
using Polly;

namespace WileyWidget.Services.Tests
{
    public class ResiliencePipelineFactoryTests
    {
        [Fact]
        public async Task CreateDefaultHttpPipeline_RetriesOn429AndSucceeds()
        {
            // Arrange - small delays so test runs quickly
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[] {
                    new KeyValuePair<string, string?>("Resilience:RetryBaseDelayMs", "1"),
                    new KeyValuePair<string, string?>("Resilience:MaxRetryAttempts", "2"),
                    new KeyValuePair<string, string?>("Resilience:RateLimitPermitLimit", "50"),
                    new KeyValuePair<string, string?>("Resilience:CircuitBreakerBreakSeconds", "1"),
                    new KeyValuePair<string, string?>("Resilience:CircuitBreakerMinimumThroughput", "2"),
                    new KeyValuePair<string, string?>("XAI:TimeoutSeconds", "1")
                })
                .Build();

            var logger = new NullLogger<ResiliencePipelineFactory>();
            var factory = new ResiliencePipelineFactory(config, logger, null);

            var pipeline = factory.CreateDefaultHttpPipeline("TestClient");

            var callCount = 0;

            // Simulate first two calls returning 429, then success on third
            Func<ResilienceContext, ValueTask<HttpResponseMessage>> action = _ =>
            {
                var attempt = Interlocked.Increment(ref callCount);

                if (attempt <= 2)
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    // Add Retry-After header (small delta so unit test is quick)
                    resp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(5));
                    return ValueTask.FromResult(resp);
                }

                return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            };

            // Act
            var result = await pipeline.ExecuteAsync(action, ResilienceContextPool.Shared.Get(CancellationToken.None));

            // Assert
            result.IsSuccessStatusCode.Should().BeTrue();
            callCount.Should().BeGreaterOrEqualTo(3);
        }

        [Fact]
        public async Task CreateDefaultHttpPipeline_ShortCircuitOnServerError_ByCircuitBreaker()
        {
            // Arrange - force many failures to trip circuit breaker quickly
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[] {
                    new KeyValuePair<string, string?>("Resilience:RetryBaseDelayMs", "1"),
                    new KeyValuePair<string, string?>("Resilience:MaxRetryAttempts", "1"),
                    new KeyValuePair<string, string?>("Resilience:RateLimitPermitLimit", "50"),
                    new KeyValuePair<string, string?>("Resilience:CircuitBreakerBreakSeconds", "1"),
                    new KeyValuePair<string, string?>("Resilience:CircuitBreakerMinimumThroughput", "2"),
                    new KeyValuePair<string, string?>("XAI:TimeoutSeconds", "1")
                })
                .Build();

            var logger = new NullLogger<ResiliencePipelineFactory>();
            var factory = new ResiliencePipelineFactory(config, logger, null);
            var pipeline = factory.CreateDefaultHttpPipeline("CBClient");

            var callCount = 0;

            Func<ResilienceContext, ValueTask<HttpResponseMessage>> action = _ =>
            {
                Interlocked.Increment(ref callCount);
                // Always return a server error
                return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            };

            // Act & Assert - repeatedly call pipeline until the circuit breaker opens (deterministic with MinimumThroughput=1)
            var circuitBroken = false;
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    await pipeline.ExecuteAsync(action, ResilienceContextPool.Shared.Get(CancellationToken.None));
                }
                catch (Polly.CircuitBreaker.BrokenCircuitException)
                {
                    circuitBroken = true;
                    break;
                }
                catch (Exception)
                {
                    // Ignore other exceptions - we only care when the circuit opens
                }
            }

            circuitBroken.Should().BeTrue("Circuit breaker should open after repeated server errors");

            // Should have attempted at least once
            callCount.Should().BeGreaterOrEqualTo(1);
        }
    }
}
