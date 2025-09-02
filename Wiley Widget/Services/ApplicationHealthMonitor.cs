using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using Serilog;
using System.Runtime.Versioning;

namespace WileyWidget.Services;

/// <summary>
/// Application health monitoring service that tracks performance metrics,
/// memory usage, and UI responsiveness.
/// </summary>
public class ApplicationHealthMonitor : IDisposable
{
    private readonly DispatcherTimer _performanceTimer;
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private bool _disposed;

    public ApplicationHealthMonitor()
    {
        // Initialize performance counters (Windows only)
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416 // Validate platform compatibility
            _cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
            _memoryCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
#pragma warning restore CA1416
        }

        // Set up periodic health checks
        _performanceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // Check every 30 seconds
        };
        _performanceTimer.Tick += OnPerformanceCheck;
        _performanceTimer.Start();

        Log.Information("Application Health Monitor initialized");
    }

    private void OnPerformanceCheck(object sender, EventArgs e)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416 // Validate platform compatibility
                var cpuUsage = _cpuCounter.NextValue();
                var memoryUsage = _memoryCounter.NextValue() / 1024 / 1024; // Convert to MB
#pragma warning restore CA1416

                // Log performance metrics
                Log.Information("Performance Check - CPU: {CpuUsage:F1}%, Memory: {MemoryUsage:F1}MB",
                    cpuUsage, memoryUsage);

                // Check for high resource usage
                if (cpuUsage > 80)
                {
                    Log.Warning("High CPU usage detected: {CpuUsage:F1}%", cpuUsage);
                }

                if (memoryUsage > 500) // 500MB threshold
                {
                    Log.Warning("High memory usage detected: {MemoryUsage:F1}MB", memoryUsage);
                    GC.Collect(); // Suggest garbage collection
                    Log.Information("Garbage collection suggested due to high memory usage");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during performance check");
        }
    }

    /// <summary>
    /// Logs layout performance metrics for UI optimization
    /// </summary>
    public void LogLayoutPerformance(string elementName, TimeSpan layoutTime)
    {
        Log.Information("Layout Performance - {ElementName}: {LayoutTime}ms",
            elementName, layoutTime.TotalMilliseconds);

        if (layoutTime.TotalMilliseconds > 100) // 100ms threshold
        {
            Log.Warning("Slow layout detected for {ElementName}: {LayoutTime}ms",
                elementName, layoutTime.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Monitors UI thread responsiveness
    /// </summary>
    public void CheckUIResponsiveness()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var startTime = DateTime.Now;

        dispatcher.BeginInvoke(() =>
        {
            var responseTime = DateTime.Now - startTime;
            Log.Information("UI Response Time: {ResponseTime}ms", responseTime.TotalMilliseconds);

            if (responseTime.TotalMilliseconds > 50) // 50ms threshold
            {
                Log.Warning("UI responsiveness issue detected: {ResponseTime}ms", responseTime.TotalMilliseconds);
            }
        });
    }

    /// <summary>
    /// Logs application startup performance
    /// </summary>
    public void LogStartupComplete(TimeSpan startupTime)
    {
        Log.Information("Application startup completed in {StartupTime}ms", startupTime.TotalMilliseconds);

        if (startupTime.TotalSeconds > 10) // 10 second threshold
        {
            Log.Warning("Slow application startup detected: {StartupTime}s", startupTime.TotalSeconds);
        }
    }

    /// <summary>
    /// Registers a health check
    /// </summary>
    public void RegisterHealthCheck(IHealthCheck healthCheck)
    {
        // Implementation for registering health checks
        Log.Information("Health check registered: {HealthCheckType}", healthCheck.GetType().Name);
    }

    /// <summary>
    /// Gets the current health status
    /// </summary>
    public HealthStatus GetCurrentStatus()
    {
        // Return a basic health status
        return new HealthStatus { IsHealthy = true, Message = "Application is healthy" };
    }

    /// <summary>
    /// Disposes the object
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the object
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _performanceTimer.Stop();
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416 // Validate platform compatibility
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
#pragma warning restore CA1416
            }
        }

        _disposed = true;
        Log.Information("Application Health Monitor disposed");
    }
}

/// <summary>
/// Interface for health checks
/// </summary>
public interface IHealthCheck
{
    string Name { get; }
    HealthStatus Check();
}

/// <summary>
/// Health status
/// </summary>
public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public string Message { get; set; }
}
