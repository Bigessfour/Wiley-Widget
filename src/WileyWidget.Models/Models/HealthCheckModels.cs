using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;

namespace WileyWidget.Models;

/// <summary>
/// Represents the overall health status of the application
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Service is healthy and fully operational
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is degraded but still operational
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is unhealthy and may not function properly
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Service is completely unavailable
    /// </summary>
    Unavailable
}

/// <summary>
/// Represents the result of a single health check
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Name of the service being checked
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Current health status
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Description of the health check result
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Exception that occurred during health check (if any)
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Time taken to perform the health check
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Timestamp when the health check was performed
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Additional metadata about the health check
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Tags for categorizing the health check
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Creates a healthy result
    /// </summary>
    public static HealthCheckResult Healthy(string serviceName, string? description = null, TimeSpan? duration = null)
    {
        return new HealthCheckResult
        {
            ServiceName = serviceName,
            Status = HealthStatus.Healthy,
            Description = description ?? $"{serviceName} is healthy",
            Duration = duration ?? TimeSpan.Zero,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a degraded result
    /// </summary>
    public static HealthCheckResult Degraded(string serviceName, string description, TimeSpan? duration = null)
    {
        return new HealthCheckResult
        {
            ServiceName = serviceName,
            Status = HealthStatus.Degraded,
            Description = description,
            Duration = duration ?? TimeSpan.Zero,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an unhealthy result
    /// </summary>
    public static HealthCheckResult Unhealthy(string serviceName, string description, Exception? exception = null, TimeSpan? duration = null)
    {
        return new HealthCheckResult
        {
            ServiceName = serviceName,
            Status = HealthStatus.Unhealthy,
            Description = description,
            Exception = exception,
            Duration = duration ?? TimeSpan.Zero,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an unavailable result
    /// </summary>
    public static HealthCheckResult Unavailable(string serviceName, string description, Exception? exception = null, TimeSpan? duration = null)
    {
        return new HealthCheckResult
        {
            ServiceName = serviceName,
            Status = HealthStatus.Unavailable,
            Description = description,
            Exception = exception,
            Duration = duration ?? TimeSpan.Zero,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Represents the overall health check report for the application
/// </summary>
public class HealthCheckReport
{
    /// <summary>
    /// Overall health status of the application
    /// </summary>
    public HealthStatus OverallStatus { get; set; }

    /// <summary>
    /// Total time taken for all health checks
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Timestamp when the health check report was generated
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Individual health check results
    /// </summary>
    public List<HealthCheckResult> Results { get; set; } = new();

    /// <summary>
    /// Count of healthy services
    /// </summary>
    public int HealthyCount => Results.Count(r => r.Status == HealthStatus.Healthy);

    /// <summary>
    /// Count of degraded services
    /// </summary>
    public int DegradedCount => Results.Count(r => r.Status == HealthStatus.Degraded);

    /// <summary>
    /// Count of unhealthy services
    /// </summary>
    public int UnhealthyCount => Results.Count(r => r.Status == HealthStatus.Unhealthy);

    /// <summary>
    /// Count of unavailable services
    /// </summary>
    public int UnavailableCount => Results.Count(r => r.Status == HealthStatus.Unavailable);

    /// <summary>
    /// Total number of services checked
    /// </summary>
    public int TotalCount => Results.Count;

    /// <summary>
    /// Gets the results filtered by status
    /// </summary>
    public IEnumerable<HealthCheckResult> GetResultsByStatus(HealthStatus status)
    {
        return Results.Where(r => r.Status == status);
    }

    /// <summary>
    /// Gets the results filtered by tags
    /// </summary>
    public IEnumerable<HealthCheckResult> GetResultsByTags(params string[] tags)
    {
        return Results.Where(r => tags.All(tag => r.Tags.Contains(tag)));
    }

    /// <summary>
    /// Determines if the application can start with the current health status
    /// </summary>
    public bool CanStartApplication()
    {
        // Application can start if:
        // - At least critical services are healthy
        // - No more than 50% of services are unhealthy/unavailable
        // - Database is healthy (if present)

        var criticalServices = GetResultsByTags("critical");
        var databaseServices = GetResultsByTags("database");

        bool criticalServicesHealthy = !criticalServices.Any() || criticalServices.All(r => r.Status == HealthStatus.Healthy);
        bool databaseHealthy = !databaseServices.Any() || databaseServices.Any(r => r.Status == HealthStatus.Healthy);
        bool acceptableFailureRate = (double)(UnhealthyCount + UnavailableCount) / TotalCount <= 0.5;

        return criticalServicesHealthy && databaseHealthy && acceptableFailureRate;
    }
}

/// <summary>
/// Circuit breaker for health checks to prevent cascading failures
/// Tuned for startup resilience: 3 failures, 2 minute recovery for critical services
/// </summary>
public class HealthCheckCircuitBreaker
{
    private readonly object _lock = new();
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeoutPeriod;
    private readonly object /* ApplicationMetricsService */? _metricsService;
    private readonly string _serviceName;

    public HealthCheckCircuitBreaker(int failureThreshold = 3, TimeSpan timeoutPeriod = default,
        object /* ApplicationMetricsService */? metricsService = null, string serviceName = "unknown")
    {
        _failureThreshold = failureThreshold;
        _timeoutPeriod = timeoutPeriod == default ? TimeSpan.FromMinutes(2) : timeoutPeriod; // Reduced from 5 minutes for faster recovery
        _metricsService = metricsService;
        _serviceName = serviceName;
    }

    public enum CircuitBreakerState
    {
        Closed,      // Normal operation
        Open,        // Circuit is open, failing fast
        HalfOpen     // Testing if service recovered
    }

    public CircuitBreakerState State => _state;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        lock (_lock)
        {
            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime > _timeoutPeriod)
                {
                    _state = CircuitBreakerState.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }
            }
        }

        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Circuit breaker operation failed");
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        CircuitBreakerState previousState;
        lock (_lock)
        {
            previousState = _state;
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }

        // Record state transition if it changed
        if (previousState != CircuitBreakerState.Closed)
        {
            // _metricsService?.RecordCircuitBreakerTransition(
            //     previousState.ToString(),
            //     CircuitBreakerState.Closed.ToString(),
            //     _serviceName);
        }
    }

    private void OnFailure()
    {
        CircuitBreakerState previousState;
        lock (_lock)
        {
            previousState = _state;
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
            }
        }

        // Record state transition if it changed
        if (previousState != _state)
        {
            // _metricsService?.RecordCircuitBreakerTransition(
            //     previousState.ToString(),
            //     _state.ToString(),
            //     _serviceName);
        }
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

/// <summary>
/// Summary of application health status for UI display
/// </summary>
public class HealthStatusSummary
{
    /// <summary>
    /// Overall health status
    /// </summary>
    public HealthStatus OverallStatus { get; set; }

    /// <summary>
    /// Human-readable description of the health status
    /// </summary>
    public string? StatusDescription { get; set; }

    /// <summary>
    /// When the last health check was performed
    /// </summary>
    public DateTime? LastChecked { get; set; }

    /// <summary>
    /// Total number of services checked
    /// </summary>
    public int ServiceCount { get; set; }

    /// <summary>
    /// Number of healthy services
    /// </summary>
    public int HealthyCount { get; set; }

    /// <summary>
    /// List of issues or problems found
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// Gets the health status as a percentage
    /// </summary>
    public double HealthPercentage => ServiceCount > 0 ? (double)HealthyCount / ServiceCount * 100 : 0;
}

/// <summary>
/// Enhanced health check configuration with resilience settings
/// </summary>
public class HealthCheckConfiguration
{
    /// <summary>
    /// Default timeout for health checks
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for database health checks
    /// </summary>
    public TimeSpan DatabaseTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Timeout for external service health checks
    /// </summary>
    public TimeSpan ExternalServiceTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maximum number of retries for failed health checks
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Delay between retries
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Circuit breaker failure threshold
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 3;

    /// <summary>
    /// Circuit breaker timeout period
    /// </summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to continue with application startup if health checks fail
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

    /// <summary>
    /// Services to skip during health checks
    /// </summary>
    public List<string> SkipServices { get; set; } = new();

    /// <summary>
    /// Services that are critical and must be healthy for app startup
    /// </summary>
    public List<string> CriticalServices { get; set; } = new() { "Database", "Configuration" };

    /// <summary>
    /// Maximum allowed overall unhealthy+unavailable failure rate (0-1) before startup aborts (if ContinueOnFailure is false). Default 0.5 (50%).
    /// </summary>
    public double CriticalFailureRateThreshold { get; set; } = 0.5;

    /// <summary>
    /// If true, only critical services are checked during initial startup; non-critical services run later (e.g., via background task).
    /// </summary>
    public bool DeferNonCriticalChecks { get; set; } = false;
}
