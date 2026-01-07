using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Service that routes AI requests based on health check status
/// to prevent cascading failures and improve overall system reliability
/// </summary>
public class HealthBasedRoutingService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthBasedRoutingService> _logger;
    private readonly string _aiHealthCheckName;

    /// <summary>
    /// Initializes the health-based routing service
    /// </summary>
    /// <param name="healthCheckService">Health check service</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="aiHealthCheckName">Name of the AI health check (default: "xai")</param>
    public HealthBasedRoutingService(
        HealthCheckService healthCheckService,
        ILogger<HealthBasedRoutingService> logger,
        string aiHealthCheckName = "xai")
    {
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiHealthCheckName = aiHealthCheckName;
    }

    /// <summary>
    /// Converts custom HealthStatus to Microsoft HealthStatus
    /// </summary>
    private static HealthStatus ConvertToMicrosoftStatus(Models.HealthStatus customStatus)
    {
        return customStatus switch
        {
            Models.HealthStatus.Healthy => HealthStatus.Healthy,
            Models.HealthStatus.Degraded => HealthStatus.Degraded,
            Models.HealthStatus.Unhealthy => HealthStatus.Unhealthy,
            _ => HealthStatus.Unhealthy
        };
    }

    /// <summary>
    /// Checks if AI service is healthy and should receive requests
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if AI service is healthy, false otherwise</returns>
    public async Task<bool> IsAIServiceHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken);

            // Check if specific AI health check exists and is healthy
            var aiResult = healthReport.Results.FirstOrDefault(r => r.ServiceName == _aiHealthCheckName);
            if (aiResult != null)
            {
                var isHealthy = ConvertToMicrosoftStatus(aiResult.Status) == HealthStatus.Healthy;

                if (!isHealthy)
                {
                    _logger.LogWarning(
                        "AI service health check failed: Status={Status}, Description={Description}",
                        aiResult.Status, aiResult.Description);
                }

                return isHealthy;
            }

            // If AI health check doesn't exist, check overall health
            var overallHealthy = ConvertToMicrosoftStatus(healthReport.OverallStatus) == HealthStatus.Healthy;

            if (!overallHealthy)
            {
                _logger.LogWarning(
                    "Overall health check failed: Status={Status}, TotalDuration={Duration}ms",
                    healthReport.OverallStatus, healthReport.TotalDuration.TotalMilliseconds);
            }

            return overallHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception - assuming unhealthy");
            return false; // Fail safe: if health check fails, assume unhealthy
        }
    }

    /// <summary>
    /// Gets detailed health status for AI service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status details</returns>
    public async Task<AIHealthStatus> GetAIHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken);

            var aiResult = healthReport.Results.FirstOrDefault(r => r.ServiceName == _aiHealthCheckName);
            if (aiResult != null)
            {
                return new AIHealthStatus
                {
                    IsHealthy = ConvertToMicrosoftStatus(aiResult.Status) == HealthStatus.Healthy,
                    Status = ConvertToMicrosoftStatus(aiResult.Status),
                    Description = aiResult.Description,
                    Duration = aiResult.Duration,
                    Exception = aiResult.Exception?.Message
                };
            }

            return new AIHealthStatus
            {
                IsHealthy = ConvertToMicrosoftStatus(healthReport.OverallStatus) == HealthStatus.Healthy,
                Status = ConvertToMicrosoftStatus(healthReport.OverallStatus),
                Description = "Overall health status (no AI-specific check found)",
                Duration = healthReport.TotalDuration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI health status");
            return new AIHealthStatus
            {
                IsHealthy = false,
                Status = HealthStatus.Unhealthy,
                Description = "Health check failed",
                Exception = ex.Message
            };
        }
    }

    /// <summary>
    /// Routes an AI request with health check fallback
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="primaryAction">Primary AI service call</param>
    /// <param name="fallbackAction">Fallback action if service is unhealthy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result from primary or fallback action</returns>
    public async Task<T> RouteWithHealthCheckAsync<T>(
        Func<CancellationToken, Task<T>> primaryAction,
        Func<Task<T>> fallbackAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primaryAction);
        ArgumentNullException.ThrowIfNull(fallbackAction);

        var isHealthy = await IsAIServiceHealthyAsync(cancellationToken);

        if (isHealthy)
        {
            try
            {
                _logger.LogDebug("AI service is healthy - routing to primary service");
                return await primaryAction(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Primary AI service call failed - falling back");
                return await fallbackAction();
            }
        }

        _logger.LogWarning("AI service is unhealthy - routing to fallback");
        return await fallbackAction();
    }

    /// <summary>
    /// Routes an AI request with health check and returns status
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="primaryAction">Primary AI service call</param>
    /// <param name="fallbackAction">Fallback action if service is unhealthy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with routing status</returns>
    public async Task<RoutedResult<T>> RouteWithStatusAsync<T>(
        Func<CancellationToken, Task<T>> primaryAction,
        Func<Task<T>> fallbackAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primaryAction);
        ArgumentNullException.ThrowIfNull(fallbackAction);

        var healthStatus = await GetAIHealthStatusAsync(cancellationToken);

        if (healthStatus.IsHealthy)
        {
            try
            {
                _logger.LogDebug("AI service is healthy - routing to primary service");
                var result = await primaryAction(cancellationToken);
                return new RoutedResult<T>
                {
                    Value = result,
                    UsedPrimary = true,
                    HealthStatus = healthStatus
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Primary AI service call failed - falling back");
                var fallbackResult = await fallbackAction();
                return new RoutedResult<T>
                {
                    Value = fallbackResult,
                    UsedPrimary = false,
                    HealthStatus = healthStatus,
                    Error = ex.Message
                };
            }
        }

        _logger.LogWarning("AI service is unhealthy - routing to fallback");
        var finalResult = await fallbackAction();
        return new RoutedResult<T>
        {
            Value = finalResult,
            UsedPrimary = false,
            HealthStatus = healthStatus
        };
    }
}

/// <summary>
/// Detailed health status for AI service
/// </summary>
public class AIHealthStatus
{
    /// <summary>
    /// Whether the service is healthy
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Health status enumeration
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// Description of health status
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Duration of health check
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Exception message if health check failed
    /// </summary>
    public string? Exception { get; init; }
}

/// <summary>
/// Result of routed AI request with metadata
/// </summary>
/// <typeparam name="T">Type of result value</typeparam>
public class RoutedResult<T>
{
    /// <summary>
    /// Result value from primary or fallback action
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// Whether primary service was used (true) or fallback (false)
    /// </summary>
    public bool UsedPrimary { get; init; }

    /// <summary>
    /// Health status at time of routing
    /// </summary>
    public required AIHealthStatus HealthStatus { get; init; }

    /// <summary>
    /// Error message if primary service failed
    /// </summary>
    public string? Error { get; init; }
}
