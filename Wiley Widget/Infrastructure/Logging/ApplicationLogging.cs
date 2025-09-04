using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WileyWidget.Infrastructure.Logging;

/// <summary>
/// Custom enricher for structured logging with application-specific context.
/// Provides correlation IDs, operation tracking, and performance metrics.
/// </summary>
public class ApplicationEnricher : Serilog.Core.ILogEventEnricher
{
    private static readonly AsyncLocal<string> _correlationId = new();
    private static readonly AsyncLocal<string> _operationId = new();
    private static readonly AsyncLocal<string> _userId = new();

    /// <summary>
    /// Sets the correlation ID for the current async context.
    /// </summary>
    public static string CorrelationId
    {
        get => _correlationId.Value ?? (_correlationId.Value = Guid.NewGuid().ToString());
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Sets the operation ID for tracking specific operations.
    /// </summary>
    public static string OperationId
    {
        get => _operationId.Value ?? (_operationId.Value = Guid.NewGuid().ToString());
        set => _operationId.Value = value;
    }

    /// <summary>
    /// Sets the user ID for the current context.
    /// </summary>
    public static string UserId
    {
        get => _userId.Value;
        set => _userId.Value = value;
    }

    /// <summary>
    /// Enriches log events with application-specific properties.
    /// </summary>
    public void Enrich(Serilog.Events.LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
    {
        // Add correlation and operation tracking
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", CorrelationId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationId", OperationId));

        // Add user context if available
        if (!string.IsNullOrEmpty(UserId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", UserId));
        }

        // Add performance metrics
        var process = Process.GetCurrentProcess();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryUsageMB", process.WorkingSet64 / 1024 / 1024));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CpuTime", process.TotalProcessorTime.TotalMilliseconds));

        // Add application context
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationContext", "WPF"));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIThread", System.Threading.Thread.CurrentThread.ManagedThreadId));
    }
}

/// <summary>
/// Structured logging helper for consistent log formatting and context.
/// </summary>
public static class StructuredLogger
{
    /// <summary>
    /// Creates a logger with operation context for tracking related log entries.
    /// </summary>
    public static IDisposable BeginOperation(string operationName, string operationId = null)
    {
        var opId = operationId ?? Guid.NewGuid().ToString();
        ApplicationEnricher.OperationId = opId;

        using (LogContext.PushProperty("OperationName", operationName))
        using (LogContext.PushProperty("OperationStart", DateTime.UtcNow))
        {
            Log.Information("🔄 Operation Started: {OperationName}", operationName);
        }

        return new OperationScope(operationName, opId);
    }

    /// <summary>
    /// Logs performance metrics for operations.
    /// </summary>
    public static void LogPerformance(string operationName, TimeSpan duration, long memoryDelta = 0)
    {
        using (LogContext.PushProperty("OperationName", operationName))
        using (LogContext.PushProperty("Duration", duration.TotalMilliseconds))
        using (LogContext.PushProperty("MemoryDeltaMB", memoryDelta / 1024 / 1024))
        {
            Log.Information("⚡ Performance: {OperationName} completed in {Duration:F2}ms", operationName, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Logs user actions with structured context.
    /// </summary>
    public static void LogUserAction(string action, string details = null, object data = null)
    {
        using (LogContext.PushProperty("UserAction", action))
        using (LogContext.PushProperty("ActionDetails", details))
        using (LogContext.PushProperty("ActionData", data))
        {
            Log.Information("👤 User Action: {UserAction}", action);
        }
    }

    /// <summary>
    /// Logs theme changes with structured context.
    /// </summary>
    public static void LogThemeChange(string fromTheme, string toTheme, bool userInitiated = true)
    {
        using (LogContext.PushProperty("ThemeChange", true))
        using (LogContext.PushProperty("FromTheme", fromTheme))
        using (LogContext.PushProperty("ToTheme", toTheme))
        using (LogContext.PushProperty("UserInitiated", userInitiated))
        {
            Log.Information("🎨 Theme Changed: {FromTheme} → {ToTheme}", fromTheme, toTheme);
        }
    }

    /// <summary>
    /// Logs Syncfusion control operations with structured context.
    /// </summary>
    public static void LogSyncfusionOperation(string controlType, string operation, object properties = null)
    {
        using (LogContext.PushProperty("SyncfusionControl", controlType))
        using (LogContext.PushProperty("SyncfusionOperation", operation))
        using (LogContext.PushProperty("ControlProperties", properties))
        {
            Log.Debug("🔧 Syncfusion {ControlType}: {Operation}", controlType, operation);
        }
    }

    /// <summary>
    /// Logs health check results with structured context.
    /// </summary>
    public static void LogHealthCheck(HealthStatus status, List<HealthCheckResult> results)
    {
        using (LogContext.PushProperty("HealthStatus", status.ToString()))
        using (LogContext.PushProperty("HealthCheckCount", results.Count))
        using (LogContext.PushProperty("HealthResults", results))
        {
            var statusEmoji = status switch
            {
                HealthStatus.Healthy => "✅",
                HealthStatus.Degraded => "⚠️",
                HealthStatus.Unhealthy => "❌",
                _ => "❓"
            };

            Log.Information("{StatusEmoji} Health Check: {HealthStatus} ({HealthCheckCount} checks)", statusEmoji, status.ToString(), results.Count);
        }
    }

    /// <summary>
    /// Operation scope for automatic cleanup and completion logging.
    /// </summary>
    private class OperationScope : IDisposable
    {
        private readonly string _operationName;
        private readonly string _operationId;
        private readonly DateTime _startTime;

        public OperationScope(string operationName, string operationId)
        {
            _operationName = operationName;
            _operationId = operationId;
            _startTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            var duration = DateTime.UtcNow - _startTime;

            using (LogContext.PushProperty("OperationName", _operationName))
            using (LogContext.PushProperty("OperationDuration", duration.TotalMilliseconds))
            using (LogContext.PushProperty("OperationCompleted", true))
            {
                Log.Information("✅ Operation Completed: {OperationName} in {Duration:F2}ms", _operationName, duration.TotalMilliseconds);
            }
        }
    }
}
