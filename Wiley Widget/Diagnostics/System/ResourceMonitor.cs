using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog.Context;

namespace WileyWidget.Diagnostics.ResourceMonitor;

/// <summary>
/// Resource monitoring system for tracking memory, CPU, and disk usage.
/// Helps identify resource leaks and performance issues.
/// </summary>
public sealed class ResourceMonitor : IDisposable
{
    private readonly Timer _resourceCheckTimer;
    private long _lastMemoryUsage;
    private double _lastCpuUsage;

    private bool _disposed;

    public ResourceMonitor()
    {
        _resourceCheckTimer = new Timer(MonitorResources, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private void MonitorResources(object state)
    {
        if (_disposed) return;

        try
        {
            var process = Process.GetCurrentProcess();

            // Memory monitoring
            var currentMemory = process.WorkingSet64;
            var memoryDelta = currentMemory - _lastMemoryUsage;

            // CPU monitoring (simplified)
            var currentCpu = process.TotalProcessorTime.TotalMilliseconds;

            // Log resource usage
            using (LogContext.PushProperty("MemoryUsageMB", currentMemory / 1024 / 1024))
            using (LogContext.PushProperty("MemoryDeltaMB", memoryDelta / 1024 / 1024))
            {
                Serilog.Log.Information("📊 Resource Usage: Memory={MemoryUsageMB:F2}MB, Delta={MemoryDeltaMB:+F2}MB, CPU={CpuTime:F2}ms");
            }

            // Alert on high memory usage
            if (currentMemory > 500 * 1024 * 1024) // 500MB
            {
                Serilog.Log.Warning("🚨 High memory usage detected: {MemoryUsageMB:F2}MB", currentMemory / 1024 / 1024);
            }

            _lastMemoryUsage = currentMemory;
            _lastCpuUsage = currentCpu;

            // Disk space monitoring
            MonitorDiskSpace();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Resource monitoring failed");
        }
    }

    private void MonitorDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory));
            var availableSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var totalSpaceGB = drive.TotalSize / (1024.0 * 1024 * 1024);

            using (LogContext.PushProperty("AvailableDiskGB", availableSpaceGB))
            using (LogContext.PushProperty("TotalDiskGB", totalSpaceGB))
            {
                Serilog.Log.Debug("💾 Disk Space: {AvailableDiskGB:F2}GB available of {TotalDiskGB:F2}GB total");
            }

            // Alert on low disk space
            if (availableSpaceGB < 1.0) // Less than 1GB
            {
                Serilog.Log.Warning("🚨 Low disk space: {AvailableDiskGB:F2}GB available", availableSpaceGB);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Disk space monitoring failed");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _resourceCheckTimer?.Dispose();
            }
            _disposed = true;
        }
    }
}
