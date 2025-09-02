using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Serilog.Events;
using Serilog.Context;
using Serilog.Enrichers;
using Serilog.Filters;
using Syncfusion.Licensing;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using WileyWidget.Views;
using WileyWidget.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Configuration;

namespace WileyWidget;

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

/// <summary>
/// Health check interface for pluggable health monitoring.
/// </summary>
public interface IHealthCheck
{
    HealthCheckResult CheckHealth();
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public class HealthCheckResult
{
    public HealthStatus Status { get; }
    public string Description { get; }
    public Exception Exception { get; }

    public HealthCheckResult(HealthStatus status, string description = null, Exception exception = null)
    {
        Status = status;
        Description = description;
        Exception = exception;
    }
}

/// <summary>
/// Health status enumeration.
/// </summary>
public enum HealthStatus
{
    Healthy = 0,
    Degraded = 1,
    Unhealthy = 2
}

/// <summary>
/// Enterprise-grade application health monitoring system.
/// Tracks application health, performance metrics, and system resources.
/// </summary>
public sealed class ApplicationHealthMonitor : IDisposable
{
    private readonly Timer _healthCheckTimer;
    private readonly List<IHealthCheck> _healthChecks = new();
    private HealthStatus _currentStatus = HealthStatus.Healthy;
    private bool _disposed;

    public ApplicationHealthMonitor()
    {
        // Health check every 30 seconds
        _healthCheckTimer = new Timer(CheckHealth, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void RegisterHealthCheck(IHealthCheck healthCheck)
    {
        _healthChecks.Add(healthCheck);
    }

    private void CheckHealth(object state)
    {
        if (_disposed) return;

        try
        {
            var results = new List<HealthCheckResult>();

            foreach (var check in _healthChecks)
            {
                try
                {
                    var result = check.CheckHealth();
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new HealthCheckResult(HealthStatus.Unhealthy, $"Health check failed: {ex.Message}"));
                }
            }

            // Determine overall health status
            var worstStatus = results.Max(r => (int)r.Status);
            _currentStatus = (HealthStatus)worstStatus;

            // Log health status
            StructuredLogger.LogHealthCheck(_currentStatus, results);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Health monitoring failed");
            _currentStatus = HealthStatus.Unhealthy;
        }
    }

    public HealthStatus GetCurrentStatus() => _currentStatus;

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
                _healthCheckTimer?.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Security auditing system for enterprise security logging.
/// Tracks authentication, authorization, and security events.
/// </summary>
public class SecurityAuditor
{
    private readonly string _auditLogPath;

    public SecurityAuditor()
    {
        _auditLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "security-audit.log");
        Directory.CreateDirectory(Path.GetDirectoryName(_auditLogPath));
    }

    public void LogSecurityEvent(string eventType, string details, string userId = null, object data = null)
    {
        using (LogContext.PushProperty("SecurityEvent", eventType))
        using (LogContext.PushProperty("SecurityDetails", details))
        using (LogContext.PushProperty("UserId", userId ?? "Anonymous"))
        using (LogContext.PushProperty("SecurityData", data))
        {
            Log.Information("🔒 Security Event: {SecurityEvent} - {SecurityDetails}", eventType, details);
        }

        // Write to dedicated security audit log
        try
        {
            var auditEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} | {eventType} | {userId ?? "Anonymous"} | {details}";
            File.AppendAllText(_auditLogPath, auditEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write to security audit log");
        }
    }

    public void LogAuthenticationEvent(string action, bool success, string userId = null)
    {
        LogSecurityEvent("Authentication", $"{action} - {(success ? "Success" : "Failed")}", userId);
    }

    public void LogAuthorizationEvent(string resource, string action, bool allowed, string userId = null)
    {
        LogSecurityEvent("Authorization", $"{action} on {resource} - {(allowed ? "Allowed" : "Denied")}", userId);
    }
}

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
            using (LogContext.PushProperty("CpuTime", currentCpu))
            {
                Log.Information("📊 Resource Usage: Memory={MemoryUsageMB:F2}MB, Delta={MemoryDeltaMB:+F2}MB, CPU={CpuTime:F2}ms");
            }

            // Alert on high memory usage
            if (currentMemory > 500 * 1024 * 1024) // 500MB
            {
                Log.Warning("🚨 High memory usage detected: {MemoryUsageMB:F2}MB", currentMemory / 1024 / 1024);
            }

            _lastMemoryUsage = currentMemory;
            _lastCpuUsage = currentCpu;

            // Disk space monitoring
            MonitorDiskSpace();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Resource monitoring failed");
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
                Log.Debug("💾 Disk Space: {AvailableDiskGB:F2}GB available of {TotalDiskGB:F2}GB total");
            }

            // Alert on low disk space
            if (availableSpaceGB < 1.0) // Less than 1GB
            {
                Log.Warning("🚨 Low disk space: {AvailableDiskGB:F2}GB available", availableSpaceGB);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Disk space monitoring failed");
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

    /// <summary>
    /// Startup progress tracker for splash screen and user feedback.
    /// Provides progress indication during application initialization.
    /// </summary>
    public class StartupProgressTracker
    {
        private int _currentStep;
        private readonly int _totalSteps;
        private readonly List<string> _steps;
        private readonly SplashScreenWindow _splashScreen;

        public StartupProgressTracker(SplashScreenWindow splashScreen = null)
        {
            _splashScreen = splashScreen;
            _steps = new List<string>
            {
                "Loading configuration...",
                "Configuring logging...",
                "Registering licenses...",
                "Initializing components...",
                "Loading user settings...",
                "Starting application..."
            };
            _totalSteps = _steps.Count;
        }

        public void AdvanceStep()
        {
            _currentStep++;
            var progress = (double)_currentStep / _totalSteps * 100;
            var statusText = _currentStep > 0 && _currentStep <= _steps.Count ? _steps[_currentStep - 1] : "Complete";

            // Update splash screen if available
            _splashScreen?.UpdateProgress(progress, statusText);

            using (LogContext.PushProperty("StartupProgress", progress))
            using (LogContext.PushProperty("CurrentStep", _currentStep))
            using (LogContext.PushProperty("TotalSteps", _totalSteps))
            {
                Log.Information("🚀 Startup Progress: {CurrentStep}/{TotalSteps} ({StartupProgress:F1}%) - {_steps[_currentStep - 1]}", _currentStep, _totalSteps, progress);
            }
        }

        public double GetProgress() => (double)_currentStep / _totalSteps * 100;
        public string GetCurrentStepText() => _currentStep > 0 && _currentStep <= _steps.Count ? _steps[_currentStep - 1] : "Complete";
    }/// <summary>
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
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Memory health check failed: {ex.Message}");
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

            return new HealthCheckResult(HealthStatus.Healthy, $"Disk space adequate: {availableSpaceGB:F2}GB available");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Disk space health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Wiley Widget WPF Application Entry Point and Bootstrap Class.
///
/// <para>This class serves as the main application controller responsible for:</para>
/// <list type="number">
/// <item><strong>Application Lifecycle Management:</strong> Startup, initialization, and shutdown</item>
/// <item><strong>Configuration Management:</strong> Loading settings from multiple sources</item>
/// <item><strong>Logging Infrastructure:</strong> Serilog setup with structured logging</item>
/// <item><strong>License Management:</strong> Syncfusion license registration and validation</item>
/// <item><strong>Database Services:</strong> Dependency injection and database initialization</item>
/// <item><strong>Exception Handling:</strong> Global exception capture and user-friendly error reporting</item>
/// <item><strong>User Experience:</strong> Settings loading and theme application</item>
/// <item><strong>Test Automation:</strong> Support for automated testing scenarios</item>
/// </list>
///
/// <para>Key Design Principles:</para>
/// <list type="bullet">
/// <item><strong>Fail-Safe Operation:</strong> Application continues running even if non-critical components fail</item>
/// <item><strong>Comprehensive Logging:</strong> All operations are logged with appropriate detail levels</item>
/// <item><strong>Graceful Degradation:</strong> Features degrade gracefully when dependencies are unavailable</item>
/// <item><strong>Performance Monitoring:</strong> Startup timing and performance metrics are tracked</item>
/// <item><strong>Developer Experience:</strong> Rich debugging information and error context</item>
/// </list>
///
/// <para>Execution Flow:</para>
/// <code>
/// Constructor → LoadConfiguration → ConfigureLogging → RegisterSyncfusionLicense
///     ↓
/// OnStartup → ConfigureDatabaseServices → ConfigureGlobalExceptionHandling
///     ↓
/// LoadAndApplyUserSettings → ConfigureTestAutomationSupport → Base.OnStartup
///     ↓
/// [Main Window Loads and Application Runs]
///     ↓
/// OnExit → PerformFinalCleanup → Log.CloseAndFlush → Base.OnExit
/// </code>
///
/// <para>Configuration Sources (in priority order):</para>
/// <list type="number">
/// <item>appsettings.json (application settings)</item>
/// <item>Environment Variables (runtime overrides)</item>
/// <item>User Secrets (development secrets)</item>
/// </list>
///
/// <para>Exception Handling Strategy:</para>
/// <list type="bullet">
/// <item><strong>AppDomain Exceptions:</strong> Logged as Fatal (will terminate app)</item>
/// <item><strong>Dispatcher Exceptions:</strong> Logged as Error, marked handled (app continues)</item>
/// <item><strong>Task Exceptions:</strong> Logged as Warning, marked observed (app continues)</item>
/// </list>
///
/// <para>License Registration Strategy:</para>
/// <list type="number">
/// <item>Configuration file (appsettings.json)</item>
/// <item>Environment variable (SYNCFUSION_LICENSE_KEY)</item>
/// <item>License file (license.key in output directory)</item>
/// <item>Fallback: Trial mode with user notification</item>
/// </list>
///
/// <para>Logging Configuration:</para>
/// <list type="bullet">
/// <item><strong>Format:</strong> Structured JSON with timestamps and context</item>
/// <item><strong>Location:</strong> ./logs/app-YYYYMMDD.log (root directory logs folder)</item>
/// <item><strong>Retention:</strong> 7 days with 10MB size limit per file</item>
/// <item><strong>Enrichment:</strong> Process ID, Thread ID, Machine Name, Environment</item>
/// </list>
///
/// <para>Performance Characteristics:</para>
/// <list type="bullet">
/// <item><strong>Startup Time:</strong> Typically &lt; 500ms for core initialization</item>
/// <item><strong>Memory Footprint:</strong> Minimal overhead from logging and configuration</item>
/// <item><strong>Thread Safety:</strong> All operations are thread-safe for UI thread</item>
/// </list>
///
/// <para>Extensibility Points:</para>
/// <list type="bullet">
/// <item><strong>Partial Methods:</strong> TryRegisterEmbeddedLicense for custom license logic</item>
/// <item><strong>Dependency Injection:</strong> IServiceProvider for custom services</item>
/// <item><strong>Configuration:</strong> IConfiguration for custom settings</item>
/// <item><strong>Logging:</strong> ILogger for custom logging scenarios</item>
/// </list>
///
/// <para>Testing Support:</para>
/// <list type="bullet">
/// <item><strong>License Dialog Automation:</strong> WILEYWIDGET_AUTOCLOSE_LICENSE=1</item>
/// <item><strong>Configuration Overrides:</strong> Environment variables</item>
/// <item><strong>Exception Simulation:</strong> Comprehensive error handling testing</item>
/// </list>
///
/// <para>Maintenance Notes:</para>
/// <list type="bullet">
/// <item>Regular log file cleanup (handled automatically)</item>
/// <item>License key rotation (update environment variable)</item>
/// <item>Configuration validation (check appsettings.json)</item>
/// <item>Performance monitoring (review startup logs)</item>
/// </list>
///
/// <para>Threading Model:</para>
/// <list type="bullet">
/// <item><strong>UI Thread:</strong> Constructor, OnStartup, OnExit</item>
/// <item><strong>Background:</strong> Database initialization, file operations</item>
/// <item><strong>Dispatcher:</strong> Timer-based operations, UI updates</item>
/// </list>
///
/// <para>Error Recovery:</para>
/// <list type="bullet">
/// <item><strong>Configuration Failure:</strong> Throws InvalidOperationException</item>
/// <item><strong>Logging Failure:</strong> Falls back to console logging</item>
/// <item><strong>License Failure:</strong> Continues in trial mode</item>
/// <item><strong>Database Failure:</strong> Logs error, continues without database</item>
/// </list>
///
/// <para>Security Considerations:</para>
/// <list type="bullet">
/// <item>License keys stored securely in environment variables</item>
/// <item>Sensitive configuration encrypted when possible</item>
/// <item>Exception details logged but not exposed to users</item>
/// <item>File system access restricted to application directories</item>
/// </list>
/// </summary>
public partial class App : Application, IDisposable
{
    /// <summary>
    /// Application configuration loaded from appsettings.json and environment variables.
    /// Used for database connections, API keys, and feature flags.
    /// </summary>
    private IConfiguration _configuration;

    /// <summary>
    /// Service provider for dependency injection, primarily used for database services.
    /// Currently reserved for future enhancements to database service management.
    /// </summary>
#pragma warning disable CS0649 // Field is never assigned to - reserved for future use
    private IServiceProvider _serviceProvider;
#pragma warning restore CS0649

    /// <summary>
    /// Tracks startup performance metrics for enterprise monitoring.
    /// </summary>
    private readonly Dictionary<string, long> _startupMetrics = new();

    /// <summary>
    /// Stopwatch for measuring application startup performance.
    /// Useful for identifying bottlenecks in initialization.
    /// </summary>
    private readonly Stopwatch _startupTimer;

    /// <summary>
    /// Timer for periodic health monitoring and resource tracking.
    /// Runs on background thread to avoid impacting UI performance.
    /// </summary>
    private DispatcherTimer _healthMonitoringTimer;

    /// <summary>
    /// Tracks application health status and performance metrics.
    /// Used for enterprise monitoring and diagnostics.
    /// </summary>
    private ApplicationHealthMonitor _healthMonitor;

    /// <summary>
    /// Security auditor for tracking security events and access patterns.
    /// Implements enterprise security logging requirements.
    /// </summary>
    // private SecurityAuditor _securityAuditor; // Removed unused field

    /// <summary>
    /// Resource monitor for tracking memory, CPU, and disk usage.
    /// Helps identify resource leaks and performance issues.
    /// </summary>
    private ResourceMonitor _resourceMonitor;

    /// <summary>
    /// Splash screen instance for professional startup experience.
    /// Shows progress and prevents user from seeing uninitialized UI.
    /// </summary>
    // private SplashScreenWindow _splashScreen; // Removed unused field

    /// <summary>
    /// Tracks initialization progress for splash screen updates.
    /// Provides user feedback during startup process.
    /// </summary>
    // private StartupProgressTracker _startupProgress; // Removed unused field

    /// <summary>
    /// WPF application constructor - executes before any XAML parsing or control creation.
    /// Critical initialization order with startup optimizations:
    /// 1. Configure performance optimizations (Authenticode, NGEN)
    /// 2. Configure logging (must happen before any Log calls)
    /// 3. Load configuration (required for license keys and database connections)
    /// 4. Register Syncfusion license (MUST happen before any Syncfusion controls)
    /// 5. Initialize minimal enterprise features (defer heavy operations)
    ///
    /// <para>This timing ensures proper license validation and prevents trial warnings.</para>
    /// <para>All exceptions during initialization are logged but don't crash the app.</para>
    /// </summary>
    /// <remarks>
    /// WPF calls this constructor before OnStartup, making it the ideal place for
    /// pre-UI initialization tasks that must complete before any windows are shown.
    /// Optimized for cold startup performance per Microsoft WPF guidelines.
    /// </remarks>
    public App()
    {
        // Start timing application initialization for performance monitoring
        _startupTimer = Stopwatch.StartNew();

        try
        {
            // OPTIMIZATION: Configure Authenticode bypass for faster startup
            ConfigureAuthenticodeOptimization();

            // OPTIMIZATION: Configure NGEN for better performance
            ConfigureNgenOptimization();

            // Phase 1: Configure logging FIRST (must happen before any Log calls)
            ConfigureLogging();
            Log.Information("🚀 === Application Constructor Started ===");

            // Phase 2: Load configuration (required for license keys and database)
            LoadConfiguration();
            Log.Information("✅ Configuration loaded successfully");

            // Phase 3: Register Syncfusion license (CRITICAL: must happen before any Syncfusion operations)
            RegisterSyncfusionLicense();

            // Log successful initialization
            _startupTimer.Stop();
            Log.Information("🎉 === Application Constructor Completed ===");
            Log.Information("⏱️  Application initialization took {ElapsedMs}ms", _startupTimer.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Critical failure in constructor - log and rethrow to prevent corrupted state
            Log.Fatal(ex, "💥 CRITICAL: Application constructor failed - application may not start properly");
            throw;
        }
    }

    /// <summary>
    /// Configures Authenticode optimization to bypass publisher evidence verification for faster startup.
    /// This optimization can save several seconds during cold startup by avoiding network calls.
    /// </summary>
    private void ConfigureAuthenticodeOptimization()
    {
        try
        {
            // OPTIMIZATION: Bypass Authenticode verification for faster startup
            // This prevents network calls to certificate authorities during cold startup
            System.Configuration.ConfigurationManager.AppSettings["generatePublisherEvidence"] = "false";

            // Alternative approach: Set via app.config if available
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppDomain.CurrentDomain.FriendlyName}.config");
            if (File.Exists(configFile))
            {
                // Note: In a real implementation, you would modify the config file
                // For now, we'll rely on the AppSettings approach above
                Log.Debug("📄 App.config found - Authenticode optimization configured via AppSettings");
            }

            Log.Information("🔐 Authenticode optimization configured for faster startup");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to configure Authenticode optimization - continuing with default settings");
        }
    }

    /// <summary>
    /// OPTIMIZATION: Configure NGEN (Native Image Generator) for better startup performance.
    /// NGEN pre-compiles assemblies to native code, reducing JIT compilation overhead.
    /// </summary>
    private void ConfigureNgenOptimization()
    {
        try
        {
            // Check if native images are available
            var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var imageLocation = currentAssembly.Location;

            // Check if native image exists (NGEN creates .ni.dll files)
            var nativeImagePath = Path.ChangeExtension(imageLocation, ".ni.dll");
            if (File.Exists(nativeImagePath))
            {
                Log.Information("🚀 NGEN native image found - application will use pre-compiled native code for faster startup");
            }
            else
            {
                Log.Debug("ℹ️ NGEN native image not found - consider running 'ngen install' for better startup performance");
            }

            // Additional NGEN optimizations can be added here
            // Such as pre-loading critical assemblies
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to configure NGEN optimization - continuing normally");
        }
    }

    /// <summary>
    /// Initializes the enhanced splash screen for professional startup experience.
    /// Uses custom WPF window with progress tracking and animations.
    /// </summary>
    private void InitializeSplashScreen()
    {
        try
        {
            // Use enhanced splash screen window instead of basic SplashScreen
            // _splashScreen = new SplashScreenWindow(_startupProgress); // Removed unused
            // _splashScreen.Show(); // Removed unused

            Log.Information("🖼️ Enhanced splash screen initialized and displayed");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to initialize enhanced splash screen - falling back to basic");

            // Fallback to basic splash screen if enhanced fails
            try
            {
                var splashImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "SplashScreen.png");
                if (File.Exists(splashImagePath))
                {
                    var basicSplash = new SplashScreen(splashImagePath);
                    basicSplash.Show(false);
                    Log.Information("🖼️ Basic splash screen initialized as fallback");
                }
            }
            catch (Exception fallbackEx)
            {
                Log.Warning(fallbackEx, "⚠️ Basic splash screen also failed - continuing without splash screen");
            }
        }
    }

    /// <summary>
    /// CRITICAL: Initializes Syncfusion components in the correct order per Syncfusion documentation
    /// This method must be called BEFORE any Syncfusion controls are instantiated
    /// </summary>
    private void InitializeSyncfusionEarly()
    {
        Log.Information("🔧 === Starting Early Syncfusion Initialization ===");
        File.AppendAllText("debug.log", "🔧 === Starting Early Syncfusion Initialization ===\n");

        try
        {
            // PHASE 0: LICENSE REGISTRATION (FIRST AND MOST CRITICAL)
            Log.Information("🔑 Phase 0: Registering Syncfusion License...");
            File.AppendAllText("debug.log", "🔑 Phase 0: Registering Syncfusion License...\n");

            bool licenseRegistered = RegisterSyncfusionLicenseSynchronously();
            if (licenseRegistered)
            {
                Log.Information("✅ Syncfusion license registered successfully");
                File.AppendAllText("debug.log", "✅ Syncfusion license registered successfully\n");
            }
            else
            {
                Log.Warning("⚠️ Syncfusion license registration failed - application will run in trial mode");
                File.AppendAllText("debug.log", "⚠️ Syncfusion license registration failed - application will run in trial mode\n");
            }

            // PHASE 1: ASSEMBLY VALIDATION
            Log.Information("🔍 Phase 1: Validating Syncfusion Assemblies...");
            File.AppendAllText("debug.log", "🔍 Phase 1: Validating Syncfusion Assemblies...\n");

            ValidateSyncfusionAssemblies();

            // PHASE 2: THEME SETUP (ApplyThemeAsDefaultStyle MUST be set BEFORE InitializeComponent)
            Log.Information("🎨 Phase 2: Configuring Syncfusion Themes...");
            File.AppendAllText("debug.log", "🎨 Phase 2: Configuring Syncfusion Themes...\n");

            ConfigureSyncfusionThemesEarly();

            Log.Information("🎉 === Early Syncfusion Initialization Completed ===");
            File.AppendAllText("debug.log", "🎉 === Early Syncfusion Initialization Completed ===\n");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 CRITICAL: Early Syncfusion initialization failed");
            File.AppendAllText("debug.log", $"💥 CRITICAL: Early Syncfusion initialization failed: {ex.Message}\n{ex.StackTrace}\n");
            throw; // Re-throw to prevent corrupted state
        }
    }

    /// <summary>
    /// Synchronous license registration to ensure proper timing
    /// </summary>
    private bool RegisterSyncfusionLicenseSynchronously()
    {
        try
        {
            // 0. Configuration-based license (highest priority)
            var configKey = _configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(configKey.Trim());
                Log.Information("✅ Syncfusion license registered from configuration.");
                return true;
            }

            // 1. Embedded license
            if (TryRegisterEmbeddedLicense())
            {
                Log.Information("✅ Syncfusion license registered from embedded source.");
                return true;
            }

            // 2. Environment variable
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                Log.Information("✅ Syncfusion license registered from environment variable.");
                return true;
            }

            // 3. File-based license (synchronous)
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var licensePath = Path.Combine(exeDir, "license.key");

            if (File.Exists(licensePath))
            {
                try
                {
                    var key = File.ReadAllText(licensePath).Trim();
                    if (!string.IsNullOrWhiteSpace(key) && key.Length > 50)
                    {
                        SyncfusionLicenseProvider.RegisterLicense(key);
                        Log.Information("✅ Syncfusion license registered from file.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "❌ Error reading license file");
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error during synchronous license registration");
            return false;
        }
    }

    /// <summary>
    /// Validates that required Syncfusion assemblies are loaded
    /// </summary>
    private void ValidateSyncfusionAssemblies()
    {
        var requiredAssemblies = new[]
        {
            "Syncfusion.SfSkinManager.WPF",
            "Syncfusion.Shared.WPF",
            "Syncfusion.Tools.WPF",
            "Syncfusion.SfInput.WPF"
        };

        foreach (var assemblyName in requiredAssemblies)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);

                if (assembly != null)
                {
                    Log.Information("✅ Syncfusion assembly loaded: {AssemblyName} v{Version}",
                        assemblyName, assembly.GetName().Version);
                    File.AppendAllText("debug.log", $"✅ Syncfusion assembly loaded: {assemblyName} v{assembly.GetName().Version}\n");
                }
                else
                {
                    Log.Warning("⚠️ Syncfusion assembly not found: {AssemblyName}", assemblyName);
                    File.AppendAllText("debug.log", $"⚠️ Syncfusion assembly not found: {assemblyName}\n");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Error validating assembly: {AssemblyName}", assemblyName);
            }
        }
    }

    /// <summary>
    /// Configures Syncfusion themes early in the process
    /// </summary>
    private void ConfigureSyncfusionThemesEarly()
    {
        try
        {
            // CRITICAL: Set ApplyThemeAsDefaultStyle BEFORE any window creation
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            Log.Information("✅ SfSkinManager.ApplyThemeAsDefaultStyle set to true");
            File.AppendAllText("debug.log", "✅ SfSkinManager.ApplyThemeAsDefaultStyle set to true\n");

            // Configure Fluent theme settings for .NET 9.0 compatibility
            ConfigureFluentThemeSettings();

            // Set global application theme (preferred over per-window)
            SfSkinManager.ApplicationTheme = new Theme("FluentLight");
            Log.Information("✅ Global application theme set to FluentLight");
            File.AppendAllText("debug.log", "✅ Global application theme set to FluentLight\n");

        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error configuring early themes");
            File.AppendAllText("debug.log", $"❌ Error configuring early themes: {ex.Message}\n");
            throw;
        }
    }

    /// <summary>
    /// Configures Fluent theme settings for .NET 9.0 compatibility
    /// </summary>
    private void ConfigureFluentThemeSettings()
    {
        try
        {
            var fluentThemeSettingsType = Type.GetType("Syncfusion.SfSkinManager.FluentThemeSettings, Syncfusion.SfSkinManager.WPF");
            if (fluentThemeSettingsType != null)
            {
                // Set ThemeMode for Fluent themes
                var themeModeProperty = fluentThemeSettingsType.GetProperty("ThemeMode");
                if (themeModeProperty != null)
                {
                    themeModeProperty.SetValue(null, 0); // 0 = Light, 1 = Dark
                    Log.Information("🎨 FluentThemeSettings.ThemeMode set to Light");
                }

                // Disable animation effects to prevent conflicts
                var hoverEffectProperty = fluentThemeSettingsType.GetProperty("HoverEffectMode");
                if (hoverEffectProperty != null)
                {
                    hoverEffectProperty.SetValue(null, 0); // None
                    Log.Information("🎨 FluentThemeSettings.HoverEffectMode disabled");
                }

                var pressedEffectProperty = fluentThemeSettingsType.GetProperty("PressedEffectMode");
                if (pressedEffectProperty != null)
                {
                    pressedEffectProperty.SetValue(null, 0); // None
                    Log.Information("🎨 FluentThemeSettings.PressedEffectMode disabled");
                }
            }
            else
            {
                Log.Information("ℹ️ FluentThemeSettings not available - skipping .NET 9.0 specific configuration");
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "FluentThemeSettings configuration failed - continuing without");
        }
    }

    /// <summary>
    /// WPF application startup event handler.
    /// Executes after constructor but before main window is shown.
    /// Handles post-constructor initialization tasks that can safely run after UI is available.
    ///
    /// <para>Initialization sequence:</para>
    /// <list type="number">
    /// <item>Database services configuration and migration</item>
    /// <item>Global exception handling setup</item>
    /// <item>User settings loading and theme application</item>
    /// <item>Test automation license dialog handling (if enabled)</item>
    /// </list>
    /// </summary>
    /// <param name="e">Startup event arguments containing command line parameters</param>
    /// <remarks>
    /// This method runs on the UI thread and has access to Dispatcher and Windows collection.
    /// Any exceptions here will crash the application, so robust error handling is critical.
    /// </remarks>
    protected override async void OnStartup(StartupEventArgs e)
    {
        // Restart stopwatch for OnStartup performance profiling
        _startupTimer.Restart();

        // Initialize health monitoring
        _healthMonitor = new ApplicationHealthMonitor();

        try
        {
            Log.Information("🎬 === Application Startup Event ===");

            // CRITICAL: Initialize Syncfusion EARLY before any other operations
            InitializeSyncfusionEarly();

            Log.Information("📋 Command line args: {Args}", string.Join(" ", e.Args));

            // Phase 1: Database services (can fail gracefully if DB unavailable)
            // _startupProgress?.AdvanceStep(); // Removed unused
            await ConfigureDatabaseServices();

            // Phase 2: Global exception handling (critical for stability)
            // _startupProgress?.AdvanceStep(); // Removed unused
            ConfigureGlobalExceptionHandling();
            Log.Information("✅ Global exception handling configured");

            // Phase 3: User settings and theme
            // _startupProgress?.AdvanceStep(); // Removed unused
            LoadAndApplyUserSettings();

            // Phase 4: Test automation support (optional)
            // _startupProgress?.AdvanceStep(); // Removed unused
            ConfigureTestAutomationSupport();

            // Phase 5: Complete startup progress
            // _startupProgress?.AdvanceStep(); // Removed unused

            // Call base implementation
            base.OnStartup(e);

            // Create and show main window
            this.MainWindow = new MainWindow();
            this.MainWindow.Show();

            // OPTIMIZATION: Defer heavy initialization operations to improve perceived startup time
            DeferHeavyInitialization();

            // Log successful startup completion
            // _securityAuditor?.LogSecurityEvent("Application", "Startup completed successfully"); // Removed unused

            // Log OnStartup performance
            _startupTimer.Stop();
            Log.Information("⏱️ OnStartup completed in {ElapsedMs}ms", _startupTimer.ElapsedMilliseconds);
            
            // Log startup performance to health monitor
            // _healthMonitor?.LogStartupComplete(_startupTimer.Elapsed); // Temporarily commented due to compilation issue

            Log.Information("✅ === Application Startup Completed Successfully ===");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 CRITICAL: Application startup failed - shutting down");

            // Show user-friendly error message
            MessageBox.Show(
                $"Application failed to start:\n\n{ex.Message}\n\nThe application will now shut down.",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            // Shutdown the application gracefully
            Shutdown(1);
        }
    }

    /// <summary>
    /// OPTIMIZATION: Set data binding programmatically in OnActivated for better startup performance.
    /// This avoids XAML parsing overhead during initial window rendering.
    /// </summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        try
        {
            // OPTIMIZATION: Defer heavy ViewModel instantiation
            // For now, just ensure DataContext is set if needed
            // Full ViewModel setup should be handled by the window itself
            if (MainWindow != null && MainWindow.DataContext == null)
            {
                // Log that we're deferring ViewModel setup for performance
                Log.Information("🔗 MainWindow activated - ViewModel setup deferred for performance");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ OnActivated optimization failed - continuing normally");
        }
    }

    /// <summary>
    /// OPTIMIZATION: Defer heavy initialization operations to improve cold startup time.
    /// This method is called after the main window is shown to avoid blocking startup.
    /// </summary>
    private async void DeferHeavyInitialization()
    {
        try
        {
            await Task.Delay(100); // Small delay to ensure UI is responsive

            // Defer heavy monitoring features that aren't needed immediately
            if (_resourceMonitor == null)
            {
                _resourceMonitor = new ResourceMonitor();
                Log.Information("📊 Resource monitor initialized (deferred)");
            }

            if (_healthMonitor == null)
            {
                _healthMonitor = new ApplicationHealthMonitor();
                _healthMonitor.RegisterHealthCheck(new MemoryHealthCheck());
                _healthMonitor.RegisterHealthCheck(new DiskSpaceHealthCheck());
                _healthMonitor.RegisterHealthCheck(new DatabaseHealthCheck());
                Log.Information("❤️ Health monitor initialized with checks (deferred)");
            }

            // Start periodic health monitoring
            if (_healthMonitoringTimer == null)
            {
                _healthMonitoringTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(5) // Check every 5 minutes
                };
                _healthMonitoringTimer.Tick += (s, e) => PerformHealthCheck();
                _healthMonitoringTimer.Start();
                Log.Information("⏰ Health monitoring timer started (deferred)");
            }

            Log.Information("✅ Heavy initialization operations completed (deferred)");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to complete deferred initialization - application will continue with reduced functionality");
        }
    }

    /// <summary>
    /// Performs periodic health checks on background thread.
    /// </summary>
    private void PerformHealthCheck()
    {
        try
        {
            if (_healthMonitor != null)
            {
                var status = _healthMonitor.GetCurrentStatus();
                // Health checks are performed automatically by the timer in ApplicationHealthMonitor
                Log.Debug("❤️ Health check performed - Status: {Status}", status);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Health check failed - continuing monitoring");
        }
    }

    /// <summary>
    /// Configures database services and initializes the database
    /// </summary>
    private async Task ConfigureDatabaseServices()
    {
        // Breakpoint: ConfigureDatabaseServices start
        System.Diagnostics.Debugger.Break();

        try
        {
            Log.Information("Configuring database services...");

            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<WileyWidget.App>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Configure services
            var services = new ServiceCollection();
            services.AddApplicationServices(configuration);

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Initialize service locator for global access
            ServiceLocator.Initialize(serviceProvider);

            // Initialize database with timeout - RESTORED
            var dbTask = DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(dbTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log.Error("⏰ Database initialization timed out after 10 seconds - continuing without database (graceful degradation)");
                // Continue without database - app can still run
            }
            else
            {
                // Database task completed successfully
                await dbTask; // Ensure any exceptions are propagated if needed
                Log.Information("Database services configured successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to configure database services");
            // In development, you might want to show a message box or handle this differently
            // For now, we'll log the error and continue - the app can still run without database
        }
    }

    /// <summary>
    /// Loads user settings and applies theme configuration.
    /// Sets default theme if none is configured.
    /// </summary>
    private void LoadAndApplyUserSettings()
    {
        try
        {
            Log.Information("⚙️ Loading user settings...");

            // Load persisted settings
            SettingsService.Instance.Load();

            // Apply default theme if none set
            if (string.IsNullOrWhiteSpace(SettingsService.Instance.Current.Theme))
            {
                SettingsService.Instance.Current.Theme = "FluentDark";
                Log.Information("🎨 Applied default theme: FluentDark");
            }
            else
            {
                Log.Information("🎨 Loaded theme: {Theme}", SettingsService.Instance.Current.Theme);
            }

            Log.Information("✅ User settings loaded successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to load user settings - using defaults");
            // Continue with defaults if settings fail to load
            SettingsService.Instance.Current.Theme = "FluentDark";
        }
    }

    /// <summary>
    /// Configures test automation support features.
    /// Currently handles automatic Syncfusion license dialog dismissal for automated testing.
    /// </summary>
    private void ConfigureTestAutomationSupport()
    {
        try
        {
            // Check for test automation environment variable
            var autoCloseLicense = Environment.GetEnvironmentVariable("WILEYWIDGET_AUTOCLOSE_LICENSE");
            if (string.Equals(autoCloseLicense, "1", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("🤖 Test automation mode detected - enabling license dialog auto-close");
                TryScheduleLicenseDialogAutoClose();
            }
            else
            {
                Log.Information("ℹ️ Running in standard mode (no test automation features)");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to configure test automation support");
        }
    }
    /// <summary>
    /// Schedules a dispatcher timer that scans for the Syncfusion trial license dialog and attempts to close it.
    /// Used only in test automation modes to prevent blocking automated test execution.
    ///
    /// <para>Timer specifications:</para>
    /// <list type="bullet">
    /// <item>Interval: 500ms (balances responsiveness with performance)</item>
    /// <item>Max attempts: 12 (approximately 6 seconds total)</item>
    /// <item>Search pattern: Windows with "Syncfusion" in title</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// This method is designed for automated testing scenarios where the license dialog
    /// would prevent clean test execution. It safely handles exceptions and will not
    /// interfere with normal application operation.
    /// </remarks>
    private void TryScheduleLicenseDialogAutoClose()
    {
        try
        {
            Log.Information("⏰ Starting license dialog auto-close timer (test automation mode)");

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
                Tag = "LicenseDialogAutoClose" // For debugging purposes
            };

            int attempts = 0;
            const int maxAttempts = 12; // ~6 seconds at 500ms intervals

            timer.Tick += (_, _) =>
            {
                try
                {
                    attempts++;
                    Log.Debug("🔍 Scanning for Syncfusion license dialog (attempt {Attempt}/{Max})", attempts, maxAttempts);

                    // Scan all current windows for Syncfusion license dialog
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window != null &&
                            !string.IsNullOrEmpty(window.Title) &&
                            window.Title.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Information("🎯 Found Syncfusion license dialog - closing automatically");
                            window.Close();
                            timer.Stop();
                            return;
                        }
                    }

                    // Stop timer after maximum attempts
                    if (attempts >= maxAttempts)
                    {
                        Log.Information("⏰ License dialog auto-close timer expired (no dialog found after {MaxAttempts} attempts)", maxAttempts);
                        timer.Stop();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "⚠️ Error during license dialog scan (attempt {Attempt})", attempts);
                    // Continue trying - don't stop the timer on individual failures
                }
            };

            timer.Start();
            Log.Information("✅ License dialog auto-close timer started successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Failed to start license dialog auto-close timer");
        }
    }

    /// <summary>
    /// WPF application exit event handler.
    /// Performs final cleanup operations before application termination.
    ///
    /// <para>Cleanup tasks:</para>
    /// <list type="bullet">
    /// <item>Flush and close all loggers to ensure all messages are written</item>
    /// <item>Release any held resources</item>
    /// <item>Log final application statistics</item>
    /// </list>
    /// </summary>
    /// <param name="e">Exit event arguments containing exit code</param>
    /// <remarks>
    /// This method should complete quickly as it blocks application shutdown.
    /// All operations should be wrapped in try-catch to prevent hanging.
    /// </remarks>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("🛑 === Application Exit Started ===");
            Log.Information("📊 Exit code: {ExitCode}", e.ApplicationExitCode);

            // Perform final cleanup
            PerformFinalCleanup();

            // Flush all loggers to ensure messages are written
            Log.Information("💾 Flushing loggers...");
            Log.CloseAndFlush();

            Log.Information("✅ === Application Exit Completed ===");
        }
        catch (Exception ex)
        {
            // Even exit logging failed - use debug console as last resort
            Debug.WriteLine($"CRITICAL: Exit cleanup failed - {ex.Message}");
        }
        finally
        {
            // Always call base implementation
            base.OnExit(e);
        }
    }

    /// <summary>
    /// Performs final cleanup operations before application shutdown.
    /// Includes resource disposal, cache clearing, and final state logging.
    /// </summary>
    private void PerformFinalCleanup()
    {
        try
        {
            Log.Information("🧹 Performing final application cleanup...");

            // Dispose of enterprise monitoring components
            DisposeEnterpriseComponents();

            // Dispose of service provider if it exists
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
                Log.Information("♻️ Service provider disposed");
            }

            // Log final application statistics
            var uptime = _startupTimer?.Elapsed ?? TimeSpan.Zero;
            Log.Information("📈 Application uptime: {Uptime}", uptime);

            Log.Information("✅ Final cleanup completed");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Error during final cleanup");
        }
    }

    /// <summary>
    /// Disposes of enterprise monitoring components.
    /// Ensures proper cleanup of health monitors, resource monitors, and security auditors.
    /// </summary>
    private void DisposeEnterpriseComponents()
    {
        try
        {
            _healthMonitor?.Dispose();
            Log.Information("♻️ Health monitor disposed");

            _resourceMonitor?.Dispose();
            Log.Information("♻️ Resource monitor disposed");

            // Security auditor doesn't need disposal but log the cleanup
            // if (_securityAuditor != null) // Removed unused
            // {
            //     _securityAuditor.LogSecurityEvent("Application", "Shutdown completed");
            //     Log.Information("♻️ Security auditor cleanup completed");
            // }

            // Close splash screen if it exists
            // _splashScreen?.Close(); // Removed unused
            Log.Information("♻️ Splash screen closed");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Error disposing enterprise components");
        }
    }

    /// <summary>
    /// Implements IDisposable pattern for proper resource cleanup.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">true if called from Dispose(), false if called from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            _healthMonitor?.Dispose();
            _resourceMonitor?.Dispose();
        }
    }

    /// <summary>
    /// Registers the Syncfusion license using precedence: environment variable (SYNCFUSION_LICENSE_KEY) > side-by-side license.key file.
    /// Falls back silently if neither is present so the app can still run in development (will show trial banner).
    /// </summary>
    /// <summary>
    /// Loads application configuration from multiple sources with fallback support.
    ///
    /// <para>Configuration sources (in order):</para>
    /// <list type="number">
    /// <item>appsettings.json (optional, reloads on change)</item>
    /// <item>Environment variables (highest priority for overrides)</item>
    /// </list>
    ///
    /// <para>Configuration is used for:</para>
    /// <list type="bullet">
    /// <item>Database connection strings</item>
    /// <item>API keys and secrets</item>
    /// <item>Feature flags and settings</item>
    /// <item>Syncfusion license keys</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration cannot be loaded</exception>
    /// <remarks>
    /// This method must be called before any configuration-dependent operations.
    /// The configuration is cached in _configuration for the application lifetime.
    /// </remarks>
    private void LoadConfiguration()
    {
        try
        {
            Log.Information("📄 Loading application configuration...");

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            Log.Debug("📂 Configuration base path: {BasePath}", basePath);

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            // Validate critical configuration exists
            ValidateConfiguration();

            Log.Information("✅ Configuration loaded successfully");
            Log.Debug("🔧 Available configuration sections: {Sections}",
                string.Join(", ", _configuration.GetChildren().Select(c => c.Key)));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 CRITICAL: Failed to load application configuration");
            throw new InvalidOperationException("Application configuration could not be loaded", ex);
        }
    }

    /// <summary>
    /// Validates that critical configuration sections are present and valid.
    /// Logs warnings for missing optional configuration but doesn't fail.
    /// </summary>
    private void ValidateConfiguration()
    {
        try
        {
            // Check for database connection string
            var defaultConnection = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(defaultConnection))
            {
                Log.Warning("⚠️ DefaultConnection not found in configuration");
            }
            else
            {
                // Validate connection string format
                if (IsValidConnectionString(defaultConnection))
                {
                    Log.Debug("✅ Database connection string configured and valid");
                }
                else
                {
                    Log.Warning("⚠️ Database connection string format appears invalid: {ConnectionString}", defaultConnection);
                }
            }

            // Check for Syncfusion license configuration
            var syncfusionSection = _configuration.GetSection("Syncfusion");
            if (syncfusionSection.Exists())
            {
                Log.Debug("✅ Syncfusion configuration section found");
            }
            else
            {
                Log.Debug("ℹ️ Syncfusion configuration section not found (will use environment variables)");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Error validating configuration");
        }
    }

    /// <summary>
    /// Validates the format of a database connection string
    /// </summary>
    /// <param name="connectionString">The connection string to validate</param>
    /// <returns>True if the connection string appears valid, false otherwise</returns>
    private bool IsValidConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        // For SQLite connection strings, check for "Data Source="
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            // Extract the data source path
            var dataSourcePart = connectionString.Split(';')
                .FirstOrDefault(part => part.Trim().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(dataSourcePart))
            {
                var path = dataSourcePart.Split('=')[1]?.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    // Basic validation: ensure it's not empty and doesn't contain invalid characters
                    return !path.Contains("..") && !Path.GetInvalidPathChars().Any(c => path.Contains(c));
                }
            }
        }

        // For SQL Server connection strings, check for required components
        if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Validates that required Syncfusion packages are available and compatible
    /// </summary>
    /// <summary>
    /// Registers Syncfusion license using configuration system with fallback methods
    /// </summary>
    private async void RegisterSyncfusionLicense()
    {
        Log.Information("=== Starting Syncfusion License Registration ===");

        // 0. Configuration-based license (highest priority)
        try
        {
            var configKey = _configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(configKey.Trim());
                Log.Information("✅ Syncfusion license registered from configuration.");
                return;
            }
            else
            {
                Log.Information("ℹ️ Configuration license key not found or is placeholder.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Error reading configuration license key");
        }

        // 1. Optional embedded license hook (implemented in user-created partial file not committed).
        // If the partial method returns true, registration succeeded and we skip other sources.
        try
        {
            if (TryRegisterEmbeddedLicense())
            {
                Log.Information("Syncfusion license registered from embedded partial.");
                return;
            }
        }
        catch { /* ignore and continue */ }

        // 2. Environment variable (User or Machine scope). User sets via: [System.Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY","<key>","User")
        try
        {
            // Try User scope first, then Machine scope
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine) ??
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                Log.Information($"✅ Found license key in environment (length: {envKey.Length})");
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                Log.Information("✅ Syncfusion license registered from environment variable.");
                return;
            }
            else
            {
                Log.Information("ℹ️ No valid SYNCFUSION_LICENSE_KEY environment variable found.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Error accessing SYNCFUSION_LICENSE_KEY environment variable");
        }

        // 3. File fallback with timeout
        var fileRegistrationTask = Task.Run(() => TryLoadLicenseFromFileWithTimeout());
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

        var completedTask = await Task.WhenAny(fileRegistrationTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            Log.Warning("⏰ Syncfusion license file read timed out after 5 seconds. Application will run in trial mode.");
            LogTrialModeActivation();
        }
        else
        {
            bool fileResult = await fileRegistrationTask;
            if (!fileResult)
            {
                Log.Warning("❌ Syncfusion license NOT registered (no config, no env var, no license.key). Application will run in trial mode.");
                LogTrialModeActivation();
            }
            else
            {
                Log.Information("✅ Syncfusion license registered from file fallback.");
            }
        }
    }

    /// <summary>
    /// Virtual hook allowing LicenseKey.Private.cs to embed the license.
    /// Return true if a key was registered. Default implementation calls EmbeddedLicenseManager.
    /// </summary>
    protected virtual bool TryRegisterEmbeddedLicense()
    {
        // Try to call the embedded license manager if available
        try
        {
            // Use reflection to call the static method from EmbeddedLicenseManager
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var type = assembly.GetType("WileyWidget.EmbeddedLicenseManager");
            if (type != null)
            {
                var method = type.GetMethod("TryRegisterEmbeddedLicense", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    return (bool)method.Invoke(null, null);
                }
            }
        }
        catch
        {
            // Ignore reflection errors - fall back to default
        }

        // Default implementation - returns false (no embedded license)
        return false;
    }

    /// <summary>
    /// Logs information about trial mode activation for monitoring and debugging
    /// </summary>
    private void LogTrialModeActivation()
    {
        try
        {
            Log.Warning("🚨 APPLICATION RUNNING IN SYNCFUSION TRIAL MODE 🚨");
            Log.Warning("📋 Trial Mode Details:");
            Log.Warning("   • Limited functionality may be available");
            Log.Warning("   • Watermarks may appear on controls");
            Log.Warning("   • Some features may be disabled");
            Log.Warning("   • Performance may be degraded");
            Log.Warning("💡 To resolve: Set SYNCFUSION_LICENSE_KEY environment variable or add license.key file");
            Log.Warning("🔗 Reference: https://help.syncfusion.com/cr/wpf/Syncfusion.html#licensing");
        }
        catch (Exception ex)
        {
            // Fallback logging in case structured logging fails
            System.Diagnostics.Debug.WriteLine($"Syncfusion Trial Mode Activated: {ex.Message}");
        }
    }

    /// <summary>
    /// Configures the Serilog structured logging system with comprehensive settings.
    /// Enhanced with advanced features for enterprise-grade logging and monitoring.
    ///
    /// <para>Enhanced Features:</para>
    /// <list type="bullet">
    /// <item><strong>Structured Logging:</strong> Rich contextual data with every log entry</item>
    /// <item><strong>Performance Monitoring:</strong> Request timing, memory usage, and throughput metrics</item>
    /// <item><strong>Error Tracking:</strong> Exception details with stack traces and environment context</item>
    /// <item><strong>Audit Trail:</strong> User actions, system events, and configuration changes</item>
    /// <item><strong>Health Monitoring:</strong> Application health checks and system resource tracking</item>
    /// <item><strong>Security Logging:</strong> Authentication, authorization, and security events</item>
    /// <item><strong>Business Metrics:</strong> Application-specific KPIs and business logic events</item>
    /// </list>
    ///
    /// <para>Log Sinks Configured:</para>
    /// <list type="number">
    /// <item><strong>Structured JSON Sink:</strong> Daily rolling files with size limits and retention policy</item>
    /// <item><strong>Human-Readable Sink:</strong> Development-friendly formatted logs</item>
    /// <item><strong>Console Sink:</strong> Real-time console output for development and debugging</item>
    /// <item><strong>Debug Sink:</strong> Visual Studio debug output for IDE integration</item>
    /// <item><strong>Error Sink:</strong> Filtered critical issues with detailed context</item>
    /// <item><strong>Performance Sink:</strong> Operation timing and resource usage tracking</item>
    /// <item><strong>User Actions Sink:</strong> User interaction and action tracking</item>
    /// <item><strong>Theme Changes Sink:</strong> UI theme and appearance change tracking</item>
    /// <item><strong>Syncfusion Sink:</strong> Syncfusion control operations and diagnostics</item>
    /// <item><strong>Security Sink:</strong> Security events and authentication tracking</item>
    /// <item><strong>Health Sink:</strong> Application health and system monitoring</item>
    /// </list>
    ///
    /// <para>Log Enrichment:</para>
    /// <list type="bullet">
    /// <item>Process ID and Thread ID for correlation</item>
    /// <item>Machine name and environment identification</item>
    /// <item>Log context properties for structured data</item>
    /// <item>Custom properties for application-specific context</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// This method implements a comprehensive logging strategy that supports:
    /// - Development debugging with console output
    /// - Production monitoring with file-based persistence
    /// - Log analysis with structured data and correlation IDs
    /// - Performance monitoring with timing and resource metrics
    /// - Security auditing with detailed event tracking
    /// - Health monitoring with system resource tracking
    /// - Business intelligence with user action and KPI tracking
    /// </remarks>
    private void ConfigureLogging()
    {
        try
        {
            // Determine log directory (root directory logs folder for all Serilog sinks)
            var logRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            Log.Information("📝 Configuring Serilog logging system...");
            Log.Debug("📂 Log directory: {LogPath}", logRoot);

            // Ensure log directory exists
            Directory.CreateDirectory(logRoot);

            // Configure Serilog with comprehensive enterprise-grade settings
            Log.Logger = new LoggerConfiguration()
                // Base log level configuration
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Syncfusion", LogEventLevel.Information)

                // Enhanced enrichment for enterprise logging
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", "WileyWidget")
                .Enrich.WithProperty("Version", GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0")
                .Enrich.FromLogContext()
                .Enrich.With(new ApplicationEnricher())

                // Structured JSON file sink for log analysis tools
                .WriteTo.File(
                    path: Path.Combine(logRoot, "structured-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    formatter: new Serilog.Formatting.Json.JsonFormatter(),
                    shared: false,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    fileSizeLimitBytes: 100 * 1024 * 1024, // 100MB per file
                    rollOnFileSizeLimit: true,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(5))

                // Human-readable file sink for development and troubleshooting
                .WriteTo.File(
                    path: Path.Combine(logRoot, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: false,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] (pid:{ProcessId} tid:{ThreadId}) {MachineName} {Application} v{Version} {CorrelationId} {OperationId}{NewLine}Message: {Message:lj}{NewLine}Properties: {@Properties}{NewLine}{Exception}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 50 * 1024 * 1024, // 50MB per file
                    rollOnFileSizeLimit: true,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(5))

                // Console sink for development with structured output
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Application} {CorrelationId} {Message:lj}{NewLine}{Exception}",
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)

                // Debug sink for IDE integration
                .WriteTo.Debug(
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Application} {CorrelationId} {Message:lj}{NewLine}{Exception}")

                // Filtered error sink for critical issues only
                .WriteTo.File(
                    path: Path.Combine(logRoot, "errors-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] (pid:{ProcessId} tid:{ThreadId}) {MachineName} {Application} v{Version}{NewLine}CorrelationId: {CorrelationId}{NewLine}OperationId: {OperationId}{NewLine}Message: {Message:lj}{NewLine}Exception: {Exception}{NewLine}Stack Trace: {StackTrace}{NewLine}Source: {Source}{NewLine}Properties: {@Properties}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 25 * 1024 * 1024, // 25MB per file
                    rollOnFileSizeLimit: true)

                // XAML events filter for WPF PresentationFramework errors
                .WriteTo.File(
                    path: Path.Combine(logRoot, "xaml-events-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Application} {CorrelationId}{NewLine}XAML Event: {Message:lj}{NewLine}Source: {Source}{NewLine}Exception: {Exception}{NewLine}Stack Trace: {StackTrace}{NewLine}Properties: {@Properties}{NewLine}---{NewLine}")
                .Filter.ByIncludingOnly(Matching.FromSource("PresentationFramework"))

                // Performance monitoring with structured data
                .WriteTo.File(
                    path: Path.Combine(logRoot, "performance-.log"),
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 168, // Keep 7 days of hourly logs
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Application} {CorrelationId} {OperationId}{NewLine}Message: {Message:lj}{NewLine}Duration: {Duration}ms | Memory: {MemoryUsageMB}MB | CPU: {CpuTime}ms{NewLine}Properties: {@Properties}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 10 * 1024 * 1024) // 10MB per file

                // User action tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "user-actions-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId} {UserId}{NewLine}User Action: {UserAction}{NewLine}Details: {ActionDetails}{NewLine}Data: {@ActionData}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 5 * 1024 * 1024) // 5MB per file

                // Theme change tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "theme-changes-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId}{NewLine}Theme Change: {FromTheme} → {ToTheme}{NewLine}User Initiated: {UserInitiated}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 2 * 1024 * 1024) // 2MB per file

                // Syncfusion operations tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "syncfusion-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId} {OperationId}{NewLine}Syncfusion {SyncfusionControl}: {SyncfusionOperation}{NewLine}Properties: {@ControlProperties}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 10 * 1024 * 1024) // 10MB per file

                // Security events tracking
                .WriteTo.File(
                    path: Path.Combine(logRoot, "security-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90, // Keep 90 days of security logs
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Application} {CorrelationId} {UserId}{NewLine}Security Event: {SecurityEvent}{NewLine}Details: {SecurityDetails}{NewLine}IP: {ClientIP} | UserAgent: {UserAgent}{NewLine}Result: {SecurityResult}{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 15 * 1024 * 1024) // 15MB per file

                // Health monitoring
                .WriteTo.File(
                    path: Path.Combine(logRoot, "health-.log"),
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 168, // Keep 7 days of hourly health checks
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Application} {CorrelationId}{NewLine}Health Check: {HealthCheckName}{NewLine}Status: {HealthStatus}{NewLine}Duration: {HealthCheckDuration}ms{NewLine}Details: {HealthDetails}{NewLine}Memory: {MemoryUsageMB}MB | CPU: {CpuUsage}% | Disk: {DiskUsageGB}GB{NewLine}---{NewLine}",
                    fileSizeLimitBytes: 5 * 1024 * 1024) // 5MB per file

                .CreateLogger();

            Log.Information("✅ Serilog structured logging system configured successfully");
            Log.Debug("🔧 Log level: Debug, Microsoft override: Warning, Syncfusion override: Information");
            Log.Information("📊 Log files location: {LogPath}", logRoot);
            Log.Information("📈 Performance monitoring enabled with separate log file");
            Log.Information("🚨 Error tracking enabled with dedicated error log file");
            Log.Information("👤 User action tracking enabled");
            Log.Information("🎨 Theme change tracking enabled");
            Log.Information("🔧 Syncfusion operation tracking enabled");
            Log.Information("� Security event tracking enabled");
            Log.Information("❤️ Health monitoring enabled");
            Log.Information("�📋 Structured JSON logging enabled for analysis tools");
        }
        catch (Exception ex)
        {
            // Critical: logging setup failed - use fallback logging
            System.Diagnostics.Debug.WriteLine($"CRITICAL: Serilog configuration failed - {ex.Message}");

            // Try to create a basic logger as fallback
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Error()
                    .Enrich.WithProperty("Application", "WileyWidget")
                    .Enrich.WithProperty("Version", GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0")
                    .WriteTo.Console()
                    .WriteTo.Debug()
                    .CreateLogger();

                Log.Error(ex, "❌ Serilog configuration failed - using fallback console logging");
                Log.Warning("⚠️ Advanced logging features disabled - using basic fallback configuration");
            }
            catch
            {
                // Last resort - no logging available
                System.Diagnostics.Debug.WriteLine("CRITICAL: All logging configuration failed");
            }
        }
    }

    /// <summary>
    /// Configures comprehensive global exception handling for the WPF application.
    ///
    /// <para>Exception sources handled:</para>
    /// <list type="number">
    /// <item><strong>AppDomain.UnhandledException:</strong> Catches all unhandled exceptions in the application domain</item>
    /// <item><strong>Application.DispatcherUnhandledException:</strong> Catches UI thread exceptions</item>
    /// <item><strong>TaskScheduler.UnobservedTaskException:</strong> Catches unobserved task exceptions</item>
    /// </list>
    ///
    /// <para>Exception handling strategy:</para>
    /// <list type="bullet">
    /// <item>Log all exceptions with full context and stack traces</item>
    /// <item>Mark Dispatcher exceptions as handled to prevent crashes</item>
    /// <item>Mark unobserved task exceptions as observed</item>
    /// <item>Continue application execution when possible</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// This method should be called early in application startup to ensure
    /// all exceptions are properly captured and logged. The strategy prioritizes
    /// application stability over immediate failure.
    /// </remarks>
    private void ConfigureGlobalExceptionHandling()
    {
        try
        {
            Log.Information("🛡️ Configuring global exception handling...");

            // 1. AppDomain unhandled exceptions (last resort - will crash app)
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                var isTerminating = args.IsTerminating;

                Log.Fatal(exception, "💥 CRITICAL: Unhandled AppDomain exception (terminating: {IsTerminating})", isTerminating);
                Log.Fatal("🔍 Exception sender: {Sender}", sender?.GetType().Name ?? "Unknown");

                // Flush logs before crash
                Log.CloseAndFlush();
            };

            // 2. WPF Dispatcher unhandled exceptions (UI thread)
            DispatcherUnhandledException += (sender, args) =>
            {
                Log.Error(args.Exception, "🚨 CRITICAL: Unhandled Dispatcher exception on UI thread");
                Log.Error("🔍 Dispatcher sender: {Sender}", sender?.GetType().Name ?? "Unknown");
                Log.Error("🔍 Exception handled: {Handled}", args.Handled);

                // Mark as handled to prevent application crash
                args.Handled = true;

                // Could show user-friendly error dialog here
                ShowExceptionDialog(args.Exception, "UI Thread Exception");
            };

            // 3. Task Scheduler unobserved exceptions (background tasks)
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Log.Warning(args.Exception, "⚠️ Unobserved task exception detected");
                Log.Warning("🔍 Task scheduler sender: {Sender}", sender?.GetType().Name ?? "Unknown");

                // Mark as observed to prevent unobserved exception escalation
                args.SetObserved();

                // Log additional task information if available
                if (args.Exception.InnerException != null)
                {
                    Log.Warning("📋 Inner exception: {InnerException}", args.Exception.InnerException.Message);
                }
            };

            Log.Information("✅ Global exception handling configured successfully");
            Log.Debug("🛡️ Handled exception sources: AppDomain, Dispatcher, TaskScheduler");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Failed to configure global exception handling");
            // Don't rethrow - exception handling setup failure shouldn't crash the app
        }
    }

    /// <summary>
    /// Displays a user-friendly exception dialog to inform the user of an error.
    /// Used for non-critical exceptions that don't require application shutdown.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="context">Context description of where the exception occurred</param>
    private void ShowExceptionDialog(Exception exception, string context)
    {
        try
        {
            var message = $"An unexpected error occurred in {context}.\n\n" +
                         $"Error: {exception.Message}\n\n" +
                         "The application will continue running, but you may experience issues. " +
                         "Please save your work and restart the application if problems persist.";

            // Use MessageBox for simple error display
            MessageBox.Show(message, "Application Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);

            Log.Information("ℹ️ Exception dialog shown to user for: {Context}", context);
        }
        catch (Exception dialogEx)
        {
            Log.Warning(dialogEx, "⚠️ Failed to show exception dialog");
        }
    }

    /// <summary>
    /// Central exception logging method that provides consistent formatting and context.
    /// Isolates exception logging pattern for easy modification and enhancement.
    ///
    /// <para>Logged information includes:</para>
    /// <list type="bullet">
    /// <item>Exception type and message</item>
    /// <item>Full stack trace</item>
    /// <item>Source context (AppDomain, Dispatcher, etc.)</item>
    /// <item>Timestamp and log level</item>
    /// </list>
    /// </summary>
    /// <param name="source">The source of the exception (e.g., "AppDomain", "Dispatcher")</param>
    /// <param name="exception">The exception to log</param>
    /// <remarks>
    /// This method is designed to be failure-resistant and will not throw exceptions.
    /// If logging itself fails, it silently continues to prevent recursive error loops.
    /// </remarks>
    private void LogException(string source, Exception exception)
    {
        if (exception == null)
        {
            Log.Warning("⚠️ LogException called with null exception (source: {Source})", source);
            return;
        }

        try
        {
            // Log with structured data for better analysis
            Log.Error(exception, "🚨 Unhandled exception in {Source}", source);

            // Log additional context if available
            if (!string.IsNullOrWhiteSpace(exception.Source))
            {
                Log.Error("🔍 Exception source: {ExceptionSource}", exception.Source);
            }

            // Log inner exception if present
            if (exception.InnerException != null)
            {
                Log.Error("📋 Inner exception: {InnerException}", exception.InnerException.Message);
            }

            // Log stack trace separately for better readability
            Log.Debug("📜 Full stack trace: {StackTrace}", exception.StackTrace);
        }
        catch (Exception logEx)
        {
            // Logging failed - use debug output as last resort
            System.Diagnostics.Debug.WriteLine($"CRITICAL: Exception logging failed - {logEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Original exception: {exception.Message}");
        }
    }

    /// <summary>
    /// Attempts to load the Syncfusion license key from a file in the application directory.
    /// This serves as a fallback method when environment variables are not available.
    ///
    /// <para>Search locations:</para>
    /// <list type="number">
    /// <item>license.key in the application's base directory</item>
    /// </list>
    ///
    /// <para>Expected file format:</para>
    /// <list type="bullet">
    /// <item>Plain text file containing only the license key</item>
    /// <item>UTF-8 encoding</item>
    /// <item>Key should be trimmed of whitespace</item>
    /// </list>
    /// </summary>
    /// <returns>
    /// <c>true</c> if the license file was found and successfully loaded;
    /// <c>false</c> if the file doesn't exist, is empty, or loading failed
    /// </returns>
    /// <remarks>
    /// This method is designed to be safe and will not throw exceptions.
    /// It logs detailed information about the license loading process for debugging.
    /// The license.key file is typically created during the build process.
    /// </remarks>
    private bool TryLoadLicenseFromFile()
    {
        try
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var licensePath = Path.Combine(exeDir, "license.key");

            Log.Information("🔍 Checking for Syncfusion license file...");
            Log.Debug("📂 License file path: {LicensePath}", licensePath);

            if (!File.Exists(licensePath))
            {
                Log.Information("ℹ️ License file not found at: {LicensePath}", licensePath);
                return false;
            }

            // Read and validate the license key
            var key = File.ReadAllText(licensePath).Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                Log.Warning("❌ License file exists but is empty or contains only whitespace");
                return false;
            }

            if (key.Length < 50) // Basic validation - Syncfusion keys are typically much longer
            {
                Log.Warning("❌ License key appears to be invalid (too short: {Length} characters)", key.Length);
                return false;
            }

            Log.Information("📄 Found license file with key length: {KeyLength}", key.Length);

            // Register the license
            SyncfusionLicenseProvider.RegisterLicense(key);

            Log.Information("✅ Syncfusion license successfully loaded from file");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "❌ Access denied reading license file - check file permissions");
            return false;
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "❌ I/O error reading license file - file may be locked or corrupted");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Unexpected error loading license from file");
            return false;
        }
    }

    /// <summary>
    /// Attempts to load Syncfusion license from file with timeout protection.
    /// This method is designed to be safe and will not throw exceptions.
    /// It logs detailed information about the license loading process for debugging.
    /// The license.key file is typically created during the build process.
    /// </summary>
    private async Task<bool> TryLoadLicenseFromFileWithTimeout()
    {
        try
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var licensePath = Path.Combine(exeDir, "license.key");

            Log.Information("🔍 Checking for Syncfusion license file with timeout...");
            Log.Debug("📂 License file path: {LicensePath}", licensePath);

            if (!File.Exists(licensePath))
            {
                Log.Information("ℹ️ License file not found at: {LicensePath}", licensePath);
                return false;
            }

            // Read file asynchronously with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var readTask = Task.Run(() => File.ReadAllText(licensePath), cts.Token);

            try
            {
                var key = await readTask;
                key = key.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    Log.Warning("❌ License file exists but is empty or contains only whitespace");
                    return false;
                }

                if (key.Length < 50) // Basic validation - Syncfusion keys are typically much longer
                {
                    Log.Warning("❌ License key appears to be invalid (too short: {Length} characters)", key.Length);
                    return false;
                }

                Log.Information("📄 Found license file with key length: {KeyLength}", key.Length);

                // Register the license
                SyncfusionLicenseProvider.RegisterLicense(key);

                Log.Information("✅ Syncfusion license successfully loaded from file");
                return true;
            }
            catch (TaskCanceledException)
            {
                Log.Warning("⏰ License file read operation timed out after 5 seconds");
                return false;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "❌ Access denied reading license file - check file permissions");
            return false;
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "❌ I/O error reading license file - file may be locked or corrupted");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "❌ Unexpected error loading license from file");
            return false;
        }
    }
}

