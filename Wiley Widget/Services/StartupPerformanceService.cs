using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;
using Serilog.Context;

namespace WileyWidget.Services;

/// <summary>
/// Service for monitoring and reporting application startup performance.
/// Tracks metrics and validates against Microsoft performance goals.
/// </summary>
public class StartupPerformanceService
{
    private readonly Dictionary<string, long> _startupMetrics = new();
    private readonly Stopwatch _startupTimer;

    /// <summary>
    /// Initializes a new instance of the StartupPerformanceService.
    /// </summary>
    public StartupPerformanceService()
    {
        _startupTimer = Stopwatch.StartNew();
    }

    /// <summary>
    /// Records the start of core startup.
    /// </summary>
    public void RecordCoreStartupBegin()
    {
        _startupMetrics["CoreStartupBegin"] = _startupTimer.ElapsedMilliseconds;
        Log.Information("🚀 Core startup begin recorded");
    }

    /// <summary>
    /// Records the completion of core startup.
    /// </summary>
    public void RecordCoreStartupComplete()
    {
        _startupMetrics["CoreStartupComplete"] = _startupTimer.ElapsedMilliseconds;
        Log.Information("✅ Core startup complete recorded");
    }

    /// <summary>
    /// Records when the first window is shown.
    /// </summary>
    public void RecordFirstWindowShown()
    {
        _startupMetrics["FirstWindowShown"] = _startupTimer.ElapsedMilliseconds;
        Log.Information("🖼️ First window shown recorded");
    }

    /// <summary>
    /// Records when the main window is rendered.
    /// </summary>
    public void RecordMainWindowRendered()
    {
        _startupMetrics["MainWindowRendered"] = _startupTimer.ElapsedMilliseconds;

        var process = Process.GetCurrentProcess();
        var renderedMemory = GC.GetTotalMemory(true) / 1024 / 1024;
        var renderedThreads = process.Threads.Count;
        var startupTime = _startupTimer.ElapsedMilliseconds;
        var cpuTime = process.TotalProcessorTime.TotalMilliseconds;

        Log.Information("🎯 Window Rendered - Memory: {MemoryMB}MB, Threads: {Threads}, Time: {TimeMs}ms, CPU: {CpuMs}ms",
            renderedMemory, renderedThreads, startupTime, cpuTime);

        ValidatePerformanceGoals(startupTime, renderedMemory);
    }

    /// <summary>
    /// Generates a comprehensive startup report.
    /// </summary>
    /// <returns>The total startup time in milliseconds.</returns>
    public long GenerateStartupReport()
    {
        try
        {
            var totalElapsedMs = _startupTimer.ElapsedMilliseconds;
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
            var threadCount = process.Threads.Count;
            var cpuTime = process.TotalProcessorTime.TotalMilliseconds;

            Log.Information("📊 === FINAL STARTUP REPORT ===");
            Log.Information("⏱️  Total Startup Time: {TotalMs}ms", totalElapsedMs);
            Log.Information("💾 Memory Usage: {MemoryMB}MB", memoryUsageMB);
            Log.Information("🧵 Thread Count: {Threads}", threadCount);
            Log.Information("⚡ CPU Time: {CpuMs}ms", cpuTime);

            // Log individual metrics
            foreach (var metric in _startupMetrics)
            {
                Log.Information("📈 {Metric}: {Time}ms", metric.Key, metric.Value);
            }

            Log.Information("✅ === STARTUP COMPLETE ===");

            return totalElapsedMs;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to generate startup report");
            return _startupTimer.ElapsedMilliseconds;
        }
    }

    /// <summary>
    /// Validates startup performance against Microsoft goals.
    /// </summary>
    /// <param name="startupTimeMs">Total startup time in milliseconds.</param>
    /// <param name="memoryUsageMB">Memory usage in MB.</param>
    private void ValidatePerformanceGoals(long startupTimeMs, long memoryUsageMB)
    {
        try
        {
            var goalsMet = true;

            // Microsoft Performance Goal: Startup time < 15 seconds
            if (startupTimeMs > 15000)
            {
                Log.Warning("⚠️ Microsoft Performance Goal NOT MET: Startup time {StartupTime}ms exceeds 15s limit", startupTimeMs);
                goalsMet = false;
            }
            else
            {
                Log.Information("✅ Microsoft Performance Goal MET: Startup time {StartupTime}ms within 15s limit", startupTimeMs);
            }

            // Microsoft Performance Goal: Memory usage ≤ 300MB
            if (memoryUsageMB > 300)
            {
                Log.Warning("⚠️ Microsoft Performance Goal NOT MET: Memory usage {MemoryUsage}MB exceeds 300MB limit", memoryUsageMB);
                goalsMet = false;
            }
            else
            {
                Log.Information("✅ Microsoft Performance Goal MET: Memory usage {MemoryUsage}MB within 300MB limit", memoryUsageMB);
            }

            if (goalsMet)
            {
                Log.Information("🎉 ALL Microsoft Performance Goals MET - Application startup successful!");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to validate performance goals");
        }
    }

    /// <summary>
    /// Gets the current startup timer.
    /// </summary>
    public Stopwatch StartupTimer => _startupTimer;
}
