using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace WileyWidget.Configuration.Resilience
{
    public static class PolicyFactory
    {
        // Polly 8.x uses ResiliencePipeline builder pattern
        public static void AddStandardResilienceHandler(IHttpClientBuilder builder, ILogger? logger = null)
        {
            builder.AddResilienceHandler("standard-resilience", (resiliencePipelineBuilder, context) =>
            {
                // Add retry strategy with exponential backoff and jitter
                resiliencePipelineBuilder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(response => (int)response.StatusCode >= 500)
                        .Handle<HttpRequestException>(),
                    OnRetry = args =>
                    {
                        logger?.LogWarning("HTTP request failed (attempt {Attempt}). Delaying {Delay}ms. Outcome: {Outcome}",
                            args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds, args.Outcome);
                        return default;
                    }
                });

                // Add circuit breaker
                resiliencePipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(60),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(response => (int)response.StatusCode >= 500)
                        .Handle<HttpRequestException>(),
                    OnClosed = args =>
                    {
                        logger?.LogInformation("Circuit breaker closed");
                        return default;
                    },
                    OnOpened = args =>
                    {
                        logger?.LogWarning("Circuit breaker opened for {Duration}", args.BreakDuration);
                        return default;
                    },
                    OnHalfOpened = args =>
                    {
                        logger?.LogInformation("Circuit breaker half-open");
                        return default;
                    }
                });

                // Add timeout
                resiliencePipelineBuilder.AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(30),
                    OnTimeout = args =>
                    {
                        logger?.LogWarning("Request timed out after {Timeout}s", args.Timeout.TotalSeconds);
                        return default;
                    }
                });
            });
        }
    }
}
