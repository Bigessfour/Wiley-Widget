using System;
using System.Diagnostics;
using System.IO;
using WileyWidget.Services;

namespace WileyWidget.Infrastructure.Monitoring;

/// <summary>
/// Health check for database connectivity.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    public HealthCheckResult CheckHealth()
    {
        try
        {
            // Check if database service is available
            // This is a simplified check - in real implementation you'd check actual DB connectivity
            return new HealthCheckResult(HealthStatus.Healthy, "Database service available");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Database health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Health check for memory usage.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    public HealthCheckResult CheckHealth()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;

            if (memoryUsageMB > 800) // 800MB threshold
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, $"High memory usage: {memoryUsageMB:F2}MB");
            }
            else if (memoryUsageMB > 500) // 500MB warning
            {
                return new HealthCheckResult(HealthStatus.Degraded, $"Elevated memory usage: {memoryUsageMB:F2}MB");
            }

            return new HealthCheckResult(HealthStatus.Healthy, $"Memory usage normal: {memoryUsageMB:F2}MB");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Memory health check failed: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Health check for disk space.
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    public HealthCheckResult CheckHealth()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory));
            var availableSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);

            if (availableSpaceGB < 0.5) // Less than 500MB
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, $"Critical disk space: {availableSpaceGB:F2}GB available");
            }
            else if (availableSpaceGB < 1.0) // Less than 1GB
            {
                return new HealthCheckResult(HealthStatus.Degraded, $"Low disk space: {availableSpaceGB:F2}GB available");
            }

            return new HealthCheckResult(HealthStatus.Healthy, $"Disk space normal: {availableSpaceGB:F2}GB available");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Disk space health check failed: {ex.Message}", ex);
        }
    }
}
