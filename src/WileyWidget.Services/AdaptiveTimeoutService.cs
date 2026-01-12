using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Service that tracks AI response latencies and calculates adaptive timeouts
/// to optimize performance while preventing premature timeouts
/// </summary>
public class AdaptiveTimeoutService
{
    private readonly ILogger<AdaptiveTimeoutService> _logger;
    private readonly ConcurrentQueue<double> _recentLatencies = new();
    private readonly int _maxSamples;
    private readonly double _baseTimeoutSeconds;
    private readonly double _multiplier;

    /// <summary>
    /// Initializes the adaptive timeout service
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="maxSamples">Maximum number of recent samples to track (default: 100)</param>
    /// <param name="baseTimeoutSeconds">Base timeout in seconds (default: 15)</param>
    /// <param name="multiplier">Multiplier applied to P95 latency (default: 1.5 for safety margin)</param>
    public AdaptiveTimeoutService(
        ILogger<AdaptiveTimeoutService> logger,
        int maxSamples = 100,
        double baseTimeoutSeconds = 15.0,
        double multiplier = 1.5)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxSamples = maxSamples;
        _baseTimeoutSeconds = baseTimeoutSeconds;
        _multiplier = multiplier;
    }

    /// <summary>
    /// Records a successful API call latency for adaptive timeout calculation
    /// </summary>
    /// <param name="latencySeconds">Latency in seconds</param>
    public void RecordLatency(double latencySeconds)
    {
        if (latencySeconds < 0)
        {
            _logger.LogWarning("Negative latency recorded: {Latency}s (ignoring)", latencySeconds);
            return;
        }

        _recentLatencies.Enqueue(latencySeconds);

        // Keep only recent samples
        while (_recentLatencies.Count > _maxSamples)
        {
            _recentLatencies.TryDequeue(out _);
        }

        _logger.LogDebug("Recorded AI latency: {Latency:F2}s (sample count: {Count})",
            latencySeconds, _recentLatencies.Count);
    }

    /// <summary>
    /// Calculates the recommended timeout based on recent latency patterns
    /// </summary>
    /// <returns>Recommended timeout in seconds</returns>
    public double GetRecommendedTimeoutSeconds()
    {
        // If we don't have enough samples, use base timeout
        if (_recentLatencies.Count < 10)
        {
            _logger.LogDebug("Insufficient samples ({Count}/10), using base timeout: {Timeout}s",
                _recentLatencies.Count, _baseTimeoutSeconds);
            return _baseTimeoutSeconds;
        }

        // Calculate P95 latency (95th percentile)
        var sortedLatencies = _recentLatencies.OrderBy(x => x).ToArray();
        var p95Index = (int)Math.Ceiling(sortedLatencies.Length * 0.95) - 1;
        var p95Latency = sortedLatencies[p95Index];

        // Apply multiplier for safety margin
        var recommendedTimeout = p95Latency * _multiplier;

        // Enforce minimum and maximum bounds
        var minTimeout = 5.0;   // Never go below 5 seconds
        var maxTimeout = 60.0;  // Never go above 60 seconds
        recommendedTimeout = Math.Max(minTimeout, Math.Min(maxTimeout, recommendedTimeout));

        _logger.LogDebug(
            "Adaptive timeout calculated: P95={P95:F2}s, Recommended={Timeout:F2}s (samples={Count})",
            p95Latency, recommendedTimeout, _recentLatencies.Count);

        return recommendedTimeout;
    }

    /// <summary>
    /// Gets current timeout statistics
    /// </summary>
    /// <returns>Statistics object containing current metrics</returns>
    public TimeoutStatistics GetStatistics()
    {
        if (_recentLatencies.IsEmpty)
        {
            return new TimeoutStatistics
            {
                SampleCount = 0,
                AverageLatencySeconds = _baseTimeoutSeconds,
                P95LatencySeconds = _baseTimeoutSeconds,
                RecommendedTimeoutSeconds = _baseTimeoutSeconds
            };
        }

        var latencies = _recentLatencies.ToArray();
        var sorted = latencies.OrderBy(x => x).ToArray();

        var average = latencies.Average();
        var p95Index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        var p95 = sorted[p95Index];
        var recommended = GetRecommendedTimeoutSeconds();

        return new TimeoutStatistics
        {
            SampleCount = latencies.Length,
            AverageLatencySeconds = average,
            P95LatencySeconds = p95,
            RecommendedTimeoutSeconds = recommended
        };
    }

    /// <summary>
    /// Resets all tracked latencies (useful for testing or after config changes)
    /// </summary>
    public void Reset()
    {
        _recentLatencies.Clear();
        _logger.LogInformation("Adaptive timeout service reset - all latency samples cleared");
    }
}

/// <summary>
/// Statistics about adaptive timeout calculations
/// </summary>
public class TimeoutStatistics
{
    /// <summary>
    /// Number of latency samples tracked
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Average latency across all samples
    /// </summary>
    public double AverageLatencySeconds { get; init; }

    /// <summary>
    /// 95th percentile latency
    /// </summary>
    public double P95LatencySeconds { get; init; }

    /// <summary>
    /// Recommended timeout value
    /// </summary>
    public double RecommendedTimeoutSeconds { get; init; }
}
