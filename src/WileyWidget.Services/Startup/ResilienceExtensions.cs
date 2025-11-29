using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;
using Polly.Retry;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Polly.RateLimiting;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Lightweight holders for service-specific pipelines so DI can inject strongly-typed instances.
    /// These simple holder types allow different pipelines sharing the same concrete ResiliencePipeline<T>
    /// to be registered in the DI container without relying on "named registrations".
    /// </summary>
    public sealed class XaiPipelineHolder
    {
        public ResiliencePipeline<HttpResponseMessage>? Pipeline { get; set; }
    }

    public sealed class QuickBooksPipelineHolder
    {
        public ResiliencePipeline<HttpResponseMessage>? Pipeline { get; set; }
    }

    public sealed class FileIoPipelineHolder
    {
        public ResiliencePipeline<bool>? Pipeline { get; set; }
    }

    public static class ResilienceExtensions
    {
        /// <summary>
        /// Register production-grade default resilience pipelines used by Wiley Widget services.
        /// The method intentionally keeps pipeline implementations local (singleton holders) so
        /// services may depend on a concrete, strongly typed holder rather than attempting
        /// named registrations. Configuration keys are optional and have sensible defaults.
        /// </summary>
        public static IServiceCollection AddWileyResiliencePolicies(this IServiceCollection services, IConfiguration configuration, ILoggerFactory? loggerFactory = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var logger = loggerFactory?.CreateLogger("WileyWidget.Resilience") ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            // XAI API pipeline
            try
            {
                var timeoutSeconds = double.Parse(configuration["XAI:TimeoutSeconds"] ?? "15", System.Globalization.CultureInfo.InvariantCulture);
                var circuitBreakerBreakSeconds = int.TryParse(configuration["XAI:CircuitBreakerBreakSeconds"], out var cbBreak) ? cbBreak : 60;
                var maxConcurrent = int.TryParse(configuration["XAI:MaxConcurrentRequests"], out var mc) ? mc : 5;

                var xaiPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                    .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 50,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2
                    }))
                    .AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(timeoutSeconds) })
                    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                    {
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromSeconds(circuitBreakerBreakSeconds)
                    })
                    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromMilliseconds(500),
                        UseJitter = true
                    })
                    .Build();

                services.AddSingleton(new XaiPipelineHolder { Pipeline = xaiPipeline });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create xAI resilience pipeline; services will fallback to local policies if any.");
                // Ensure silhouettes exist for DI consumers
                services.AddSingleton(new XaiPipelineHolder { Pipeline = null });
            }

            // QuickBooks API pipeline (different defaults: smaller rate, higher jitter on retry)
            try
            {
                var qbTimeout = TimeSpan.FromSeconds(int.TryParse(configuration["QuickBooks:TimeoutSeconds"], out var t) ? t : 20);
                var qbRetry = int.TryParse(configuration["QuickBooks:RetryAttempts"], out var ra) ? ra : 3;

                var qbPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                    .AddTimeout(new TimeoutStrategyOptions { Timeout = qbTimeout })
                    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                    {
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        MinimumThroughput = 3,
                        BreakDuration = TimeSpan.FromSeconds(60)
                    })
                    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                    {
                        MaxRetryAttempts = qbRetry,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromMilliseconds(250),
                        UseJitter = true
                    })
                    .Build();

                services.AddSingleton(new QuickBooksPipelineHolder { Pipeline = qbPipeline });

                // File I/O resilience pipeline (used by export services) — tuned for local disk/network share reliability
                var fileIoPipeline = new ResiliencePipelineBuilder<bool>()
                    .AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(30) })
                    .AddRetry(new RetryStrategyOptions<bool>
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromMilliseconds(200),
                        UseJitter = true
                    })
                    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<bool>
                    {
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        MinimumThroughput = 3,
                        BreakDuration = TimeSpan.FromSeconds(60)
                    })
                    .Build();

                services.AddSingleton(new FileIoPipelineHolder { Pipeline = fileIoPipeline });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create QuickBooks resilience pipeline; QuickBooks wrappers will fallback to local policies.");
                services.AddSingleton(new QuickBooksPipelineHolder { Pipeline = null });
            }

            return services;
        }
    }
}
