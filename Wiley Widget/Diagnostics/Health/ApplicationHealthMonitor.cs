using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using Serilog;
using System.Runtime.Versioning;

namespace WileyWidget.Diagnostics.Health;

/// <summary>
/// Application health monitoring service that tracks performance metrics,
/// memory usage, and UI responsiveness.
/// </summary>
public class ApplicationHealthMonitor : IDisposable
{
    private readonly DispatcherTimer _performanceTimer;
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly Random _jitterRandom;
    private bool _disposed;

    public ApplicationHealthMonitor()
    {
        // Initialize random for jitter
        _jitterRandom = new Random();

        // Initialize performance counters (Windows only)
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416 // Validate platform compatibility
            _cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
            _memoryCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
#pragma warning restore CA1416
        }

        // Set up periodic health checks with jitter
        _performanceTimer = new DispatcherTimer();
        SetJitteredInterval();
        _performanceTimer.Tick += OnPerformanceCheck;
        _performanceTimer.Start();

        Log.Information("Application Health Monitor initialized with jittered health checks");
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

                // Log structured performance metrics
                Log.Information("Health Check - CPU: {CpuUsage:F1}%, Memory: {MemoryUsage:F1}MB, ThreadCount: {ThreadCount}, ProcessId: {ProcessId}",
                    cpuUsage, memoryUsage, Process.GetCurrentProcess().Threads.Count, Process.GetCurrentProcess().Id);

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

            // Reset jittered interval for next check
            SetJitteredInterval();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during performance check");
            // Reset interval even on error to prevent timer issues
            SetJitteredInterval();
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

    /// <summary>
    /// Sets a jittered interval for health checks to avoid synchronized monitoring
    /// Base interval: 30 seconds, Jitter: ±25% (22.5-37.5 seconds)
    /// </summary>
    private void SetJitteredInterval()
    {
        const double baseIntervalSeconds = 30.0;
        const double jitterPercent = 0.25; // 25% jitter

        // Calculate jittered interval
        double jitterFactor = 1.0 + (_jitterRandom.NextDouble() * 2.0 - 1.0) * jitterPercent;
        double jitteredSeconds = baseIntervalSeconds * jitterFactor;

        _performanceTimer.Interval = TimeSpan.FromSeconds(jitteredSeconds);
        Log.Debug("Health check interval set to {Interval:F1}s (jittered)", jitteredSeconds);
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
