using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// Provides custom application metrics for SigNoz monitoring.
/// Tracks memory usage, error rates, and application-specific performance metrics.
/// </summary>
public class ApplicationMetricsService : IDisposable
{
    private readonly ILogger<ApplicationMetricsService> _logger;
    private readonly Meter _meter;
    private readonly System.Threading.Timer _memoryMonitorTimer;
    
    // Counters
    private readonly Counter<long> _errorCounter;
    private readonly Counter<long> _operationCounter;
    private readonly Counter<long> _moduleInitCounter;
    
    // Histograms
    private readonly Histogram<double> _operationDuration;
    private readonly Histogram<double> _gcDuration;
    
    // ObservableGauges (polled metrics)
    private long _lastGcMemory;
    private long _lastWorkingSet;
    private long _lastPrivateMemory;
    private int _lastGen0Collections;
    private int _lastGen1Collections;
    private int _lastGen2Collections;

    public ApplicationMetricsService(ILogger<ApplicationMetricsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create meter for WileyWidget application metrics
        _meter = new Meter("WileyWidget.Application", "1.0.0");
        
        // Initialize counters
        _errorCounter = _meter.CreateCounter<long>(
            "wiley.errors.total",
            description: "Total number of errors by type and severity");
        
        _operationCounter = _meter.CreateCounter<long>(
            "wiley.operations.total",
            description: "Total number of operations by type");
        
        _moduleInitCounter = _meter.CreateCounter<long>(
            "wiley.modules.initialized",
            description: "Number of modules initialized (success/failure)");
        
        // Initialize histograms
        _operationDuration = _meter.CreateHistogram<double>(
            "wiley.operation.duration",
            unit: "ms",
            description: "Duration of operations in milliseconds");
        
        _gcDuration = _meter.CreateHistogram<double>(
            "wiley.gc.duration",
            unit: "ms",
            description: "Garbage collection duration");
        
        // Observable gauges for memory metrics
        _meter.CreateObservableGauge(
            "wiley.memory.gc_bytes",
            () => _lastGcMemory,
            unit: "bytes",
            description: "Current GC heap memory in bytes");
        
        _meter.CreateObservableGauge(
            "wiley.memory.working_set_bytes",
            () => _lastWorkingSet,
            unit: "bytes",
            description: "Current working set memory in bytes");
        
        _meter.CreateObservableGauge(
            "wiley.memory.private_bytes",
            () => _lastPrivateMemory,
            unit: "bytes",
            description: "Current private memory in bytes");
        
        _meter.CreateObservableGauge(
            "wiley.gc.gen0_collections",
            () => _lastGen0Collections,
            description: "Total Gen 0 garbage collections");
        
        _meter.CreateObservableGauge(
            "wiley.gc.gen1_collections",
            () => _lastGen1Collections,
            description: "Total Gen 1 garbage collections");
        
        _meter.CreateObservableGauge(
            "wiley.gc.gen2_collections",
            () => _lastGen2Collections,
            description: "Total Gen 2 garbage collections");
        
        // Memory pressure gauge
        _meter.CreateObservableGauge(
            "wiley.memory.pressure_percent",
            () => CalculateMemoryPressure(),
            unit: "%",
            description: "Memory pressure as percentage of available memory");
        
        // Start memory monitoring timer (every 10 seconds)
        _memoryMonitorTimer = new System.Threading.Timer(
            UpdateMemoryMetrics,
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10));
        
        _logger.LogInformation("ApplicationMetricsService initialized - tracking memory and performance metrics");
    }

    /// <summary>
    /// Records an error occurrence with metadata.
    /// </summary>
    public void RecordError(string errorType, string severity, string? source = null)
    {
        var tags = new TagList
        {
            { "error.type", errorType },
            { "error.severity", severity }
        };
        
        if (!string.IsNullOrEmpty(source))
        {
            tags.Add("error.source", source);
        }
        
        _errorCounter.Add(1, tags);
        
        _logger.LogDebug("Recorded error metric: {Type} / {Severity} from {Source}",
            errorType, severity, source ?? "unknown");
    }

    /// <summary>
    /// Records an operation with duration.
    /// </summary>
    public void RecordOperation(string operationType, double durationMs, bool success, string? context = null)
    {
        var tags = new TagList
        {
            { "operation.type", operationType },
            { "operation.success", success }
        };
        
        if (!string.IsNullOrEmpty(context))
        {
            tags.Add("operation.context", context);
        }
        
        _operationCounter.Add(1, tags);
        _operationDuration.Record(durationMs, tags);
        
        _logger.LogDebug("Recorded operation metric: {Type} - {Duration}ms - Success: {Success}",
            operationType, durationMs, success);
    }

    /// <summary>
    /// Records module initialization result.
    /// </summary>
    public void RecordModuleInitialization(string moduleName, bool success, double durationMs)
    {
        var tags = new TagList
        {
            { "module.name", moduleName },
            { "module.success", success }
        };
        
        _moduleInitCounter.Add(1, tags);
        _operationDuration.Record(durationMs, new TagList
        {
            { "operation.type", "module_init" },
            { "module.name", moduleName }
        });
        
        _logger.LogInformation("Module {Module} initialization: {Success} in {Duration}ms",
            moduleName, success ? "SUCCESS" : "FAILED", durationMs);
    }

    /// <summary>
    /// Records GC collection metrics.
    /// </summary>
    public void RecordGarbageCollection(int generation, double durationMs, long memoryBefore, long memoryAfter)
    {
        var tags = new TagList
        {
            { "gc.generation", generation }
        };
        
        _gcDuration.Record(durationMs, tags);
        
        var memoryFreed = memoryBefore - memoryAfter;
        _logger.LogDebug("GC Gen{Gen} completed in {Duration}ms, freed {Freed:N0} bytes",
            generation, durationMs, memoryFreed);
    }

    /// <summary>
    /// Forces an immediate update of memory metrics.
    /// Call this before critical operations to get current state.
    /// </summary>
    public void UpdateMemoryMetricsNow()
    {
        UpdateMemoryMetrics(null);
    }

    /// <summary>
    /// Gets current memory statistics for logging/diagnostics.
    /// </summary>
    public object GetMemoryStatistics()
    {
        UpdateMemoryMetrics(null);
        
        return new
        {
            GcMemoryBytes = _lastGcMemory,
            WorkingSetBytes = _lastWorkingSet,
            PrivateMemoryBytes = _lastPrivateMemory,
            Gen0Collections = _lastGen0Collections,
            Gen1Collections = _lastGen1Collections,
            Gen2Collections = _lastGen2Collections,
            MemoryPressurePercent = CalculateMemoryPressure()
        };
    }

    private void UpdateMemoryMetrics(object? state)
    {
        try
        {
            // Update GC memory
            _lastGcMemory = GC.GetTotalMemory(forceFullCollection: false);
            
            // Update process memory
            using var process = Process.GetCurrentProcess();
            _lastWorkingSet = process.WorkingSet64;
            _lastPrivateMemory = process.PrivateMemorySize64;
            
            // Update GC collection counts
            _lastGen0Collections = GC.CollectionCount(0);
            _lastGen1Collections = GC.CollectionCount(1);
            _lastGen2Collections = GC.CollectionCount(2);
            
            // Log warning if memory pressure is high
            var pressure = CalculateMemoryPressure();
            if (pressure > 80)
            {
                _logger.LogWarning("HIGH MEMORY PRESSURE: {Pressure:F1}% - GC: {GC:N0} bytes, Working Set: {WS:N0} bytes",
                    pressure, _lastGcMemory, _lastWorkingSet);
            }
            else if (pressure > 60)
            {
                _logger.LogInformation("Elevated memory usage: {Pressure:F1}% - GC: {GC:N0} bytes",
                    pressure, _lastGcMemory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update memory metrics");
        }
    }

    private double CalculateMemoryPressure()
    {
        try
        {
            // Calculate memory pressure as percentage
            // Using GC memory as baseline (adjust threshold as needed)
            const long targetMemoryBytes = 512 * 1024 * 1024; // 512MB target
            var pressure = (_lastGcMemory / (double)targetMemoryBytes) * 100;
            return Math.Min(pressure, 100); // Cap at 100%
        }
        catch
        {
            return 0;
        }
    }

    // Add a protected virtual Dispose(bool) method
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            _memoryMonitorTimer?.Dispose();
            _meter?.Dispose();
            _logger.LogInformation("ApplicationMetricsService disposed");
        }
        // No unmanaged resources to clean up
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
