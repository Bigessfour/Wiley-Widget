using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Services;

/// <summary>
/// Health check service that integrates Microsoft.Extensions.Diagnostics.HealthChecks
/// with the custom WileyWidget health check models
/// </summary>
public class HealthCheckService
{
    // Resolve the Microsoft HealthCheckService per-scope using IServiceProvider to avoid
    // capturing a scoped service in a singleton. The Microsoft HealthCheckService is
    // registered by the health checks system and may be scoped; resolving it from a
    // created scope prevents "Cannot resolve scoped service from root provider" errors.
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly HealthCheckConfiguration _config;

    public HealthCheckService(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthCheckService> logger,
        HealthCheckConfiguration config)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Performs comprehensive health checks and returns a custom HealthCheckReport
    /// </summary>
    public async Task<Models.HealthCheckReport> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<Models.HealthCheckResult>();

        try
        {
            // Run Microsoft health checks. Resolve the Microsoft HealthCheckService from a
            // newly-created scope so any scoped dependencies it needs are available.
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport microsoftReport;
            using (var scope = _scopeFactory.CreateScope())
            {
                var microsoftHealth = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
                microsoftReport = await microsoftHealth.CheckHealthAsync(cancellationToken);
            }

            // Convert Microsoft health check results to custom format
            foreach (var entry in microsoftReport.Entries)
            {
                var customResult = ConvertToCustomResult(entry.Key, entry.Value);
                results.Add(customResult);
            }

            // Run custom health checks
            var customResults = await RunCustomHealthChecksAsync(cancellationToken);
            results.AddRange(customResults);

            var report = new Models.HealthCheckReport
            {
                OverallStatus = ConvertMicrosoftStatus(microsoftReport.Status),
                TotalDuration = TimeSpan.Zero, // Duration not directly available in HealthReport
                Timestamp = DateTime.UtcNow,
                Results = results
            };

            stopwatch.Stop();
            _logger.LogInformation("Health check completed in {Duration}ms with status {Status}",
                stopwatch.ElapsedMilliseconds, report.OverallStatus);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return new Models.HealthCheckReport
            {
                OverallStatus = Models.HealthStatus.Unhealthy,
                TotalDuration = stopwatch.Elapsed,
                Timestamp = DateTime.UtcNow,
                Results = new List<Models.HealthCheckResult>
                {
                    Models.HealthCheckResult.Unhealthy("HealthCheckService", $"Health check failed: {ex.Message}", ex, stopwatch.Elapsed)
                }
            };
        }
    }

    private Task<List<Models.HealthCheckResult>> RunCustomHealthChecksAsync(CancellationToken cancellationToken)
    {
        var results = new List<Models.HealthCheckResult>();
        // Add custom health check tasks here as needed
        return Task.FromResult(results);
    }

    private Models.HealthCheckResult ConvertToCustomResult(string serviceName, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReportEntry microsoftEntry)
    {
        var status = ConvertMicrosoftStatus(microsoftEntry.Status);
        var description = microsoftEntry.Description ?? $"{serviceName} health check";

        if (status == Models.HealthStatus.Healthy)
        {
            return Models.HealthCheckResult.Healthy(serviceName, description, TimeSpan.Zero);
        }
        else if (status == Models.HealthStatus.Degraded)
        {
            return Models.HealthCheckResult.Degraded(serviceName, description, TimeSpan.Zero);
        }
        else
        {
            return Models.HealthCheckResult.Unhealthy(serviceName, description, microsoftEntry.Exception, TimeSpan.Zero);
        }
    }

    private Models.HealthStatus ConvertMicrosoftStatus(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus microsoftStatus)
    {
        return microsoftStatus switch
        {
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => Models.HealthStatus.Healthy,
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => Models.HealthStatus.Degraded,
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy => Models.HealthStatus.Unhealthy,
            _ => Models.HealthStatus.Unhealthy
        };
    }
}
