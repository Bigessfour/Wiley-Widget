using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using System.Threading.RateLimiting;
using Polly.RateLimiting;
using Polly.Timeout;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace WileyWidget.Services.Resilience
{
    public class ResiliencePipelineFactory : IResiliencePipelineFactory
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResiliencePipelineFactory> _logger;
        private readonly WileyWidget.Services.Telemetry.ApplicationMetricsService? _metricsService;

        public ResiliencePipelineFactory(IConfiguration configuration, ILogger<ResiliencePipelineFactory> logger, WileyWidget.Services.Telemetry.ApplicationMetricsService? metricsService = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsService = metricsService;
        }

        public ResiliencePipeline<HttpResponseMessage> CreateDefaultHttpPipeline(string clientName)
        {
            // Read defaults from configuration where possible
            var timeoutSeconds = double.Parse(_configuration["XAI:TimeoutSeconds"] ?? "15", System.Globalization.CultureInfo.InvariantCulture);
            var baseDelayMs = int.Parse(_configuration["Resilience:RetryBaseDelayMs"] ?? "500", System.Globalization.CultureInfo.InvariantCulture);
            var maxRetry = int.Parse(_configuration["Resilience:MaxRetryAttempts"] ?? "3", System.Globalization.CultureInfo.InvariantCulture);
            var rateLimit = int.Parse(_configuration["Resilience:RateLimitPermitLimit"] ?? "50", System.Globalization.CultureInfo.InvariantCulture);
            var circuitBreakerBreakSeconds = int.Parse(_configuration["Resilience:CircuitBreakerBreakSeconds"] ?? "60", System.Globalization.CultureInfo.InvariantCulture);
            var circuitBreakerFailureRatio = double.Parse(_configuration["Resilience:CircuitBreakerFailureRatio"] ?? "0.5", System.Globalization.CultureInfo.InvariantCulture);
            var circuitBreakerMinimumThroughput = int.Parse(_configuration["Resilience:CircuitBreakerMinimumThroughput"] ?? "5", System.Globalization.CultureInfo.InvariantCulture);
            var circuitBreakerSamplingSeconds = int.Parse(_configuration["Resilience:CircuitBreakerSamplingSeconds"] ?? "30", System.Globalization.CultureInfo.InvariantCulture);

            // Clamp to valid values expected by the underlying Polly validator
            // MinimumThroughput requires at least 2
            if (circuitBreakerMinimumThroughput < 2) circuitBreakerMinimumThroughput = 2;
            // FailureRatio must be [0.0, 1.0]
            circuitBreakerFailureRatio = Math.Max(0.0, Math.Min(1.0, circuitBreakerFailureRatio));
            if (circuitBreakerSamplingSeconds < 1) circuitBreakerSamplingSeconds = 30; // default

            var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = rateLimit,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }))
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                    OnTimeout = args =>
                    {
                        _logger.LogError("{Client} - HTTP timeout after {Timeout}s", clientName, args.Timeout.TotalSeconds);
                        return ValueTask.CompletedTask;
                    }
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    FailureRatio = circuitBreakerFailureRatio,
                    SamplingDuration = TimeSpan.FromSeconds(circuitBreakerSamplingSeconds),
                    MinimumThroughput = circuitBreakerMinimumThroughput,
                    BreakDuration = TimeSpan.FromSeconds(circuitBreakerBreakSeconds),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                        .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>(),
                    OnOpened = args =>
                    {
                        _logger.LogError("{Client} - Circuit breaker opened", clientName);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        _logger.LogInformation("{Client} - Circuit breaker closed", clientName);
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        _logger.LogInformation("{Client} - Circuit breaker half-open", clientName);
                        return ValueTask.CompletedTask;
                    }
                })
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = maxRetry,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromMilliseconds(baseDelayMs),
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                        .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
                        .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
                        .HandleResult(r => r.StatusCode == HttpStatusCode.BadGateway)
                        .HandleResult(r => r.StatusCode == HttpStatusCode.ServiceUnavailable)
                        .HandleResult(r => r.StatusCode == HttpStatusCode.GatewayTimeout)
                        .Handle<HttpRequestException>(),
                    OnRetry = args =>
                    {
                        try
                        {
                            var response = args.Outcome.Result;
                            if (response?.Headers?.RetryAfter != null)
                            {
                                var retryAfter = response.Headers.RetryAfter;
                                TimeSpan desiredDelay = TimeSpan.Zero;
                                if (retryAfter.Delta.HasValue)
                                {
                                    desiredDelay = retryAfter.Delta.Value;
                                }
                                else if (retryAfter.Date.HasValue)
                                {
                                    desiredDelay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                                }

                                if (desiredDelay < TimeSpan.Zero) desiredDelay = TimeSpan.Zero;

                                var computedDelay = args.RetryDelay;
                                var additional = desiredDelay - computedDelay;
                                if (additional > TimeSpan.Zero)
                                {
                                    _logger.LogInformation("{Client} - Respecting Retry-After: waiting additional {Additional}ms before next retry", clientName, (long)additional.TotalMilliseconds);
                                    // Return a ValueTask that completes after the additional delay
                                    _metricsService?.RecordAiRetry(clientName);
                                    return new ValueTask(Task.Delay(additional));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "{Client} - Error while handling Retry-After header in OnRetry", clientName);
                        }

                        var attempt = args.AttemptNumber + 1;
                        var status = args.Outcome.Result?.StatusCode.ToString() ?? args.Outcome.Exception?.GetType().Name ?? "Unknown";
                        _logger.LogWarning("{Client} - retry {Attempt}/{Max} due to {Status}", clientName, attempt, maxRetry, status);

                        // Record retry metric for observability (model unknown at this level)
                        _metricsService?.RecordAiRetry(clientName);

                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            _logger.LogDebug("Created resilience pipeline for {Client} (maxRetry={MaxRetry}, timeout={Timeout}s)", clientName, maxRetry, timeoutSeconds);

            return pipeline;
        }
    }
}
