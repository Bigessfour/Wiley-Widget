using System;
using System.Diagnostics;

namespace WileyWidget.Infrastructure;

/// <summary>
/// Consolidated performance metrics for application startup and runtime monitoring.
/// Provides a centralized location for tracking and reporting performance data.
/// </summary>
public class StartupMetrics
{
    /// <summary>
    /// Gets or sets the total application startup time in milliseconds.
    /// </summary>
    public long TotalStartupTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the configuration loading time in milliseconds.
    /// </summary>
    public long ConfigurationLoadTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the Serilog logger configuration time in milliseconds.
    /// </summary>
    public long LoggerConfigurationTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the service registration time in milliseconds.
    /// </summary>
    public long ServiceRegistrationTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the database initialization time in milliseconds.
    /// </summary>
    public long DatabaseInitializationTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the theme initialization time in milliseconds.
    /// </summary>
    public long ThemeInitializationTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the main window creation time in milliseconds.
    /// </summary>
    public long MainWindowCreationTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the memory usage at startup in MB.
    /// </summary>
    public long StartupMemoryUsageMB { get; set; }

    /// <summary>
    /// Gets or sets the CPU time used during startup in milliseconds.
    /// </summary>
    public long StartupCpuTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when startup began.
    /// </summary>
    public DateTime StartupStartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when startup completed.
    /// </summary>
    public DateTime StartupEndTime { get; set; }

    /// <summary>
    /// Gets the total startup duration as a TimeSpan.
    /// </summary>
    public TimeSpan TotalStartupDuration => StartupEndTime - StartupStartTime;

    /// <summary>
    /// Creates a new instance of StartupMetrics with current timestamp.
    /// </summary>
    public StartupMetrics()
    {
        StartupStartTime = DateTime.UtcNow;
        var process = Process.GetCurrentProcess();
        StartupMemoryUsageMB = process.WorkingSet64 / 1024 / 1024;
        StartupCpuTimeMs = (long)process.TotalProcessorTime.TotalMilliseconds;
    }

    /// <summary>
    /// Marks the startup as complete and records the end time.
    /// </summary>
    public void MarkStartupComplete()
    {
        StartupEndTime = DateTime.UtcNow;
        TotalStartupTimeMs = (long)TotalStartupDuration.TotalMilliseconds;
    }

    /// <summary>
    /// Records the time taken for a specific startup phase.
    /// </summary>
    /// <param name="phaseName">The name of the startup phase.</param>
    /// <param name="durationMs">The duration in milliseconds.</param>
    public void RecordPhaseTime(string phaseName, long durationMs)
    {
        switch (phaseName.ToLowerInvariant())
        {
            case "configuration":
                ConfigurationLoadTimeMs = durationMs;
                break;
            case "logger":
                LoggerConfigurationTimeMs = durationMs;
                break;
            case "services":
                ServiceRegistrationTimeMs = durationMs;
                break;
            case "database":
                DatabaseInitializationTimeMs = durationMs;
                break;
            case "theme":
                ThemeInitializationTimeMs = durationMs;
                break;
            case "mainwindow":
                MainWindowCreationTimeMs = durationMs;
                break;
        }
    }

    /// <summary>
    /// Returns a formatted string representation of the startup metrics.
    /// </summary>
    public override string ToString()
    {
        return $"Startup Metrics: Total={TotalStartupTimeMs}ms, Memory={StartupMemoryUsageMB}MB, CPU={StartupCpuTimeMs}ms";
    }
}
