#nullable enable

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;

namespace WileyWidget.Configuration;

/// <summary>
/// Service for monitoring application health and providing health status
/// </summary>
public class HealthMonitoringService
{
    private readonly HealthCheckService _healthCheckService;

    public HealthMonitoringService(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
    }

    /// <summary>
    /// Performs a comprehensive health check of all registered services
    /// </summary>
    public async Task<HealthReport> CheckHealthAsync()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();

            Log.Information("Health check completed - Status: {Status}, Duration: {Duration}ms",
                           healthReport.Status, healthReport.TotalDuration.TotalMilliseconds);

            // Log individual check results
            foreach (var entry in healthReport.Entries)
            {
                if (entry.Value.Status == HealthStatus.Unhealthy)
                {
                    Log.Error("Health check failed for {CheckName}: {Description}",
                             entry.Key, entry.Value.Description);
                }
                else if (entry.Value.Status == HealthStatus.Degraded)
                {
                    Log.Warning("Health check degraded for {CheckName}: {Description}",
                               entry.Key, entry.Value.Description);
                }
            }

            return healthReport;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Health check service failed");
            throw;
        }
    }

    /// <summary>
    /// Gets a summary of the current health status
    /// </summary>
    public async Task<HealthStatusSummary> GetHealthSummaryAsync()
    {
        var report = await CheckHealthAsync();

        var summary = new HealthStatusSummary
        {
            OverallStatus = report.Status,
            TotalDuration = report.TotalDuration,
            CheckCount = report.Entries.Count,
            HealthyCount = report.Entries.Count(e => e.Value.Status == HealthStatus.Healthy),
            DegradedCount = report.Entries.Count(e => e.Value.Status == HealthStatus.Degraded),
            UnhealthyCount = report.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy),
            CheckResults = report.Entries.ToDictionary(
                e => e.Key,
                e => new HealthCheckResult
                {
                    Status = e.Value.Status,
                    Description = e.Value.Description,
                    Duration = e.Value.Duration
                })
        };

        return summary;
    }

    /// <summary>
    /// Checks if the application is in a healthy state
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        var report = await CheckHealthAsync();
        return report.Status == HealthStatus.Healthy;
    }
}

/// <summary>
/// Summary of health check results
/// </summary>
public class HealthStatusSummary
{
    public HealthStatus OverallStatus { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int CheckCount { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public Dictionary<string, HealthCheckResult> CheckResults { get; set; } = new();
}

/// <summary>
/// Individual health check result
/// </summary>
public class HealthCheckResult
{
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
}
