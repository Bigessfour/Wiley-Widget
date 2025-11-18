using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Application metrics service using System.Diagnostics.Metrics
/// Provides observability for startup performance, database operations, and circuit breaker state
/// </summary>
public class ApplicationMetricsService : IDisposable
{
    private readonly Meter _meter;
    private readonly ILogger<ApplicationMetricsService> _logger;

    // Startup metrics
    private readonly Histogram<double> _startupDuration;
    private readonly Counter<long> _startupAttempts;
    private readonly Counter<long> _startupFailures;

    // Database metrics
    private readonly Histogram<double> _migrationDuration;
    private readonly Counter<long> _migrationAttempts;
    private readonly Counter<long> _migrationFailures;
    private readonly Counter<long> _seedingOperations;

    // Circuit breaker metrics
    private readonly Counter<long> _circuitBreakerTransitions;
    private readonly UpDownCounter<long> _circuitBreakerState;

    // Health check metrics
    private readonly Histogram<double> _healthCheckDuration;
    private readonly Counter<long> _healthCheckFailures;

    public ApplicationMetricsService(ILogger<ApplicationMetricsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create meter for this application
        _meter = new Meter("WileyWidget", "1.0.0");

        // Startup metrics
        _startupDuration = _meter.CreateHistogram<double>(
            "app_startup_duration_ms",
            description: "Time taken for application startup");

        _startupAttempts = _meter.CreateCounter<long>(
            "app_startup_attempts_total",
            description: "Total number of application startup attempts");

        _startupFailures = _meter.CreateCounter<long>(
            "app_startup_failures_total",
            description: "Total number of application startup failures");

        // Database metrics
        _migrationDuration = _meter.CreateHistogram<double>(
            "db_migration_duration_ms",
            description: "Time taken for database migration");

        _migrationAttempts = _meter.CreateCounter<long>(
            "db_migration_attempts_total",
            description: "Total number of database migration attempts");

        _migrationFailures = _meter.CreateCounter<long>(
            "db_migration_failures_total",
            description: "Total number of database migration failures");

        _seedingOperations = _meter.CreateCounter<long>(
            "db_seeding_operations_total",
            description: "Total number of database seeding operations");

        // Circuit breaker metrics
        _circuitBreakerTransitions = _meter.CreateCounter<long>(
            "circuit_breaker_transitions_total",
            description: "Total number of circuit breaker state transitions");

        _circuitBreakerState = _meter.CreateUpDownCounter<long>(
            "circuit_breaker_state",
            description: "Current circuit breaker state (0=Closed, 1=Open, 2=HalfOpen)");

        // Health check metrics
        _healthCheckDuration = _meter.CreateHistogram<double>(
            "health_check_duration_ms",
            description: "Time taken for health checks");

        _healthCheckFailures = _meter.CreateCounter<long>(
            "health_check_failures_total",
            description: "Total number of health check failures");

        _logger.LogInformation("Application metrics service initialized");
    }

    /// <summary>
    /// Records startup duration and success/failure
    /// </summary>
    public void RecordStartup(double durationMs, bool success)
    {
        _startupDuration.Record(durationMs);
        _startupAttempts.Add(1);

        if (!success)
        {
            _startupFailures.Add(1);
        }

        _logger.LogInformation("Recorded startup metrics: {DurationMs}ms, Success: {Success}",
            durationMs, success);
    }

    /// <summary>
    /// Records database migration duration and success/failure
    /// </summary>
    public void RecordMigration(double durationMs, bool success)
    {
        _migrationDuration.Record(durationMs);
        _migrationAttempts.Add(1);

        if (!success)
        {
            _migrationFailures.Add(1);
        }

        _logger.LogDebug("Recorded migration metrics: {DurationMs}ms, Success: {Success}",
            durationMs, success);
    }

    /// <summary>
    /// Records database seeding operation
    /// </summary>
    public void RecordSeeding(bool success)
    {
        _seedingOperations.Add(1);

        if (!success)
        {
            _logger.LogWarning("Database seeding failed");
        }
        else
        {
            _logger.LogDebug("Database seeding completed successfully");
        }
    }

    /// <summary>
    /// Records circuit breaker state transition
    /// </summary>
    public void RecordCircuitBreakerTransition(string fromState, string toState, string serviceName)
    {
        _circuitBreakerTransitions.Add(1, new KeyValuePair<string, object>("service", serviceName),
            new KeyValuePair<string, object>("from_state", fromState),
            new KeyValuePair<string, object>("to_state", toState));

        // Update current state (0=Closed, 1=Open, 2=HalfOpen)
        var stateValue = toState switch
        {
            "Closed" => 0L,
            "Open" => 1L,
            "HalfOpen" => 2L,
            _ => 0L
        };
        _circuitBreakerState.Add(stateValue, new KeyValuePair<string, object>("service", serviceName));

        _logger.LogDebug("Circuit breaker {Service} transitioned from {FromState} to {ToState}",
            serviceName, fromState, toState);
    }

    /// <summary>
    /// Records health check duration and success/failure
    /// </summary>
    public void RecordHealthCheck(double durationMs, bool success, string serviceName)
    {
        _healthCheckDuration.Record(durationMs,
            new KeyValuePair<string, object>("service", serviceName));

        if (!success)
        {
            _healthCheckFailures.Add(1, new KeyValuePair<string, object>("service", serviceName));
        }

        _logger.LogDebug("Recorded health check metrics for {Service}: {DurationMs}ms, Success: {Success}",
            serviceName, durationMs, success);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _meter.Dispose();
            _logger.LogInformation("Application metrics service disposed");
        }
    }
}
