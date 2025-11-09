using System;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Configuration for Polly resilience policies used in resource loading.
    /// Follows Microsoft's recommended resilience patterns for production systems.
    /// </summary>
    public class ResiliencePolicyConfiguration
    {
        /// <summary>
        /// Timeout configuration for resource loading operations.
        /// </summary>
        public TimeoutConfiguration Timeout { get; set; } = new()
        {
            Duration = TimeSpan.FromSeconds(60),
            Description = "Timeout for individual resource loading (accounts for cold starts)"
        };

        /// <summary>
        /// Circuit breaker configuration to prevent cascade failures.
        /// </summary>
        public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new()
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromMinutes(2),
            Description = "Opens after 50% failures in 30s window (min 5 requests), stays open 2 minutes"
        };

        /// <summary>
        /// Retry configuration with exponential backoff and jitter.
        /// </summary>
        public RetryConfiguration Retry { get; set; } = new()
        {
            MaxRetryAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            UseJitter = true,
            BackoffType = "Exponential",
            Description = "Retries up to 3 times with exponential backoff and jitter to prevent thundering herd"
        };

        /// <summary>
        /// Gets a human-readable summary of the resilience configuration.
        /// </summary>
        public string GetSummary() =>
            $"Timeout: {Timeout.Duration.TotalSeconds}s | " +
            $"Circuit Breaker: {CircuitBreaker.FailureRatio:P0} failure ratio, {CircuitBreaker.BreakDuration.TotalMinutes}min break | " +
            $"Retry: {Retry.MaxRetryAttempts}x {Retry.BackoffType} backoff with {(Retry.UseJitter ? "jitter" : "no jitter")}";
    }

    public class TimeoutConfiguration
    {
        public TimeSpan Duration { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class CircuitBreakerConfiguration
    {
        public double FailureRatio { get; set; }
        public TimeSpan SamplingDuration { get; set; }
        public int MinimumThroughput { get; set; }
        public TimeSpan BreakDuration { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class RetryConfiguration
    {
        public int MaxRetryAttempts { get; set; }
        public TimeSpan BaseDelay { get; set; }
        public bool UseJitter { get; set; }
        public string BackoffType { get; set; } = "Exponential";
        public string Description { get; set; } = string.Empty;
    }
}
