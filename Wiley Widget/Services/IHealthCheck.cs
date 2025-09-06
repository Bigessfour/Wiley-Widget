using System;

namespace WileyWidget.Services;

/// <summary>
/// Health check interface for pluggable health monitoring.
/// </summary>
public interface IHealthCheck
{
    HealthCheckResult CheckHealth();
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public class HealthCheckResult
{
    public HealthStatus Status { get; }
    public string Description { get; }
    public Exception Exception { get; }

    public HealthCheckResult(HealthStatus status, string description = null, Exception exception = null)
    {
        Status = status;
        Description = description;
        Exception = exception;
    }
}

/// <summary>
/// Health status enumeration.
/// </summary>
public enum HealthStatus
{
    Healthy = 0,
    Degraded = 1,
    Unhealthy = 2
}
