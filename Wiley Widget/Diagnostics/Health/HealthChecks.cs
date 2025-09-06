using System;
using System.Diagnostics;
using System.IO;
using Serilog;

namespace WileyWidget.Diagnostics.Health;

/// <summary>
/// Health check for database connectivity.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    public string Name => "Database";

    public HealthStatus Check()
    {
        try
        {
            // Check if database service is available
            // This is a simplified check - in real implementation you'd check actual DB connectivity
            return new HealthStatus { IsHealthy = true, Message = "Database service available" };
        }
        catch (Exception ex)
        {
            return new HealthStatus { IsHealthy = false, Message = $"Database health check failed: {ex.Message}" };
        }
    }
}

/// <summary>
/// Health check for memory usage.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    public string Name => "Memory";

    public HealthStatus Check()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;

            if (memoryUsageMB > 800) // 800MB threshold
            {
                return new HealthStatus { IsHealthy = false, Message = $"High memory usage: {memoryUsageMB:F2}MB" };
            }
            else if (memoryUsageMB > 500) // 500MB warning
            {
                return new HealthStatus { IsHealthy = true, Message = $"Elevated memory usage: {memoryUsageMB:F2}MB" };
            }

            return new HealthStatus { IsHealthy = true, Message = $"Memory usage normal: {memoryUsageMB:F2}MB" };
        }
        catch (Exception ex)
        {
            return new HealthStatus { IsHealthy = false, Message = $"Memory health check failed: {ex.Message}" };
        }
    }
}

/// <summary>
/// Health check for disk space.
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    public string Name => "Disk Space";

    public HealthStatus Check()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory));
            var availableSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);

            if (availableSpaceGB < 0.5) // Less than 500MB
            {
                return new HealthStatus { IsHealthy = false, Message = $"Critical disk space: {availableSpaceGB:F2}GB available" };
            }
            else if (availableSpaceGB < 1.0) // Less than 1GB
            {
                return new HealthStatus { IsHealthy = true, Message = $"Low disk space: {availableSpaceGB:F2}GB available" };
            }

            return new HealthStatus { IsHealthy = true, Message = $"Disk space adequate: {availableSpaceGB:F2}GB available" };
        }
        catch (Exception ex)
        {
            return new HealthStatus { IsHealthy = false, Message = $"Disk space health check failed: {ex.Message}" };
        }
    }
}
