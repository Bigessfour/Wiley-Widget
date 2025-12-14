using System;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WileyWidget.Services.Telemetry;

namespace WileyWidget.Services;

/// <summary>
/// Enterprise-grade error reporting service with SigNoz OpenTelemetry integration.
/// Provides structured logging, user notifications, recovery mechanisms, and comprehensive telemetry.
/// Enhanced with OpenTelemetry spans and metrics for distributed tracing and monitoring.
/// </summary>
public class ErrorReportingService
{
    private readonly Dictionary<string, ErrorRecoveryStrategy> _recoveryStrategies = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, TelemetryEvent> _telemetryEvents = new();
    private readonly ILogger<ErrorReportingService> _logger;
    private readonly Stopwatch _uptimeStopwatch;
    private SigNozTelemetryService? _telemetryService;

    /// <summary>
    /// When true, user dialogs are suppressed (useful for automated tests and headless runs).
    /// </summary>
    public bool SuppressUserDialogs { get; set; } = false;

    /// <summary>
    /// Raised whenever an error is reported. Useful for tests to observe errors without UI.
    /// </summary>
    public event Action<Exception, string?>? ErrorReported;

    /// <summary>
    /// Raised whenever telemetry data is collected.
    /// </summary>
    public event Action<TelemetryEvent>? TelemetryCollected;

    /// <summary>
    /// Primary constructor for DI container resolution.
    /// </summary>
    public ErrorReportingService(ILogger<ErrorReportingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uptimeStopwatch = Stopwatch.StartNew();

        // Register default recovery strategies
        RegisterRecoveryStrategy("Authentication", new RetryWithDelayStrategy(maxRetries: 3, initialDelayMs: 1000));
        RegisterRecoveryStrategy("Network", new RetryWithDelayStrategy(maxRetries: 2, initialDelayMs: 2000));
        RegisterRecoveryStrategy("Database", new RetryWithDelayStrategy(maxRetries: 1, initialDelayMs: 500));

        // Log service initialization
        TrackEvent("ErrorReportingService_Initialized", new Dictionary<string, object>
        {
            ["ServiceVersion"] = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown",
            ["Timestamp"] = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Sets the SigNoz telemetry service for OpenTelemetry integration.
    /// Called during application startup after telemetry service is initialized.
    /// </summary>
    public void SetTelemetryService(SigNozTelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
        _logger.LogInformation("SigNoz telemetry service integrated with ErrorReportingService");
    }

    /// <summary>
    /// Reports an error with structured logging and optional user notification.
    /// </summary>
    public void ReportError(Exception exception, string context = null, bool showToUser = true,
                           LogEventLevel level = LogEventLevel.Error, string correlationId = null)
    {
        if (exception is null) throw new ArgumentNullException(nameof(exception));
        correlationId ??= Guid.NewGuid().ToString();

        // Structured logging with context
        var logContext = Log.ForContext("CorrelationId", correlationId)
                           .ForContext("Context", context ?? "Unknown")
                           .ForContext("ExceptionType", exception.GetType().Name)
                           .ForContext("ExceptionMessage", exception.Message);

        logContext.Write(level, exception, "Error occurred in {Context}: {Message}", context, exception.Message);

        // OpenTelemetry exception recording
        if (_telemetryService != null)
        {
            _telemetryService.RecordException(exception,
                ("error.context", context),
                ("error.correlation_id", correlationId),
                ("error.show_to_user", showToUser));
        }

        // Track telemetry event for error
        TrackEvent("Exception_Occurred", new Dictionary<string, object>
        {
            ["ExceptionType"] = exception.GetType().Name,
            ["ExceptionMessage"] = exception.Message,
            ["Context"] = context ?? "Unknown",
            ["CorrelationId"] = correlationId,
            ["StackTrace"] = exception.StackTrace,
            ["InnerException"] = exception.InnerException?.Message
        });

        // Notify subscribers (tests, telemetry listeners)
        try { ErrorReported?.Invoke(exception, context); } catch { /* do not fail reporting */ }

        // Show user-friendly dialog if requested and not suppressed
        if (showToUser && !SuppressUserDialogs && System.Windows.Application.Current?.Dispatcher != null)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                ShowErrorDialog(exception, context, correlationId));
        }
    }

    /// <summary>
    /// Lightweight telemetry: records an event with optional properties.
    /// Uses structured logging so it can be aggregated by sinks.
    /// </summary>
    public void TrackEvent(string eventName, IDictionary<string, object> properties = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("Event name is required", nameof(eventName));

            var telemetryEvent = new TelemetryEvent
            {
                EventName = eventName,
                Timestamp = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString(),
                Properties = properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>(),
                SessionId = GetSessionId()
            };

            // Add uptime to all events
            telemetryEvent.Metrics["UptimeSeconds"] = _uptimeStopwatch.Elapsed.TotalSeconds;

            // Store telemetry event
            _telemetryEvents[eventName] = telemetryEvent;

            // Notify subscribers
            try { TelemetryCollected?.Invoke(telemetryEvent); } catch { /* do not fail telemetry */ }

            // OpenTelemetry span tracking
            if (_telemetryService != null)
            {
                using var eventSpan = _telemetryService.StartActivity($"event.{eventName}");
                if (eventSpan != null && properties != null)
                {
                    foreach (var prop in properties)
                    {
                        eventSpan.SetTag($"event.{prop.Key}", prop.Value?.ToString());
                    }
                    eventSpan.SetTag("event.uptime_seconds", _uptimeStopwatch.Elapsed.TotalSeconds);
                    eventSpan.SetTag("event.session_id", telemetryEvent.SessionId);
                }
            }

            var logger = Log.ForContext("TelemetryEvent", eventName);
            if (properties != null)
            {
                // Destructure properties so they remain structured in sinks
                logger = logger.ForContext("Properties", properties, destructureObjects: true);
            }
            logger.Information("Telemetry event: {Event}", eventName);

            _logger.LogDebug("Tracked telemetry event: {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking telemetry event: {EventName}", eventName);
        }
    }

    /// <summary>
    /// Tracks a metric with optional properties.
    /// </summary>
    public void TrackMetric(string metricName, double value, IDictionary<string, object> properties = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(metricName)) throw new ArgumentException("Metric name is required", nameof(metricName));

            var telemetryEvent = new TelemetryEvent
            {
                EventName = $"Metric_{metricName}",
                Timestamp = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString(),
                Properties = properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>(),
                SessionId = GetSessionId()
            };

            telemetryEvent.Metrics[metricName] = value;
            telemetryEvent.Metrics["UptimeSeconds"] = _uptimeStopwatch.Elapsed.TotalSeconds;

            // Store telemetry event
            _telemetryEvents[telemetryEvent.EventName] = telemetryEvent;

            // Notify subscribers
            try { TelemetryCollected?.Invoke(telemetryEvent); } catch { /* do not fail telemetry */ }

            var logger = Log.ForContext("TelemetryMetric", metricName)
                           .ForContext("Value", value);
            if (properties != null)
            {
                logger = logger.ForContext("Properties", properties, destructureObjects: true);
            }
            logger.Information("Telemetry metric: {Metric} = {Value}", metricName, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking telemetry metric: {MetricName}", metricName);
        }
    }

    /// <summary>
    /// Tracks a dependency call (database, HTTP, etc.) with duration.
    /// </summary>
    public void TrackDependency(string dependencyName, string commandName, TimeSpan duration,
                               bool success, string dependencyType = "Database")
    {
        try
        {
            var telemetryEvent = new TelemetryEvent
            {
                EventName = $"Dependency_{dependencyName}",
                Timestamp = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString(),
                SessionId = GetSessionId(),
                Duration = duration
            };

            telemetryEvent.Properties["DependencyName"] = dependencyName;
            telemetryEvent.Properties["CommandName"] = commandName;
            telemetryEvent.Properties["DependencyType"] = dependencyType;
            telemetryEvent.Properties["Success"] = success;
            telemetryEvent.Metrics["DurationMs"] = duration.TotalMilliseconds;
            telemetryEvent.Metrics["UptimeSeconds"] = _uptimeStopwatch.Elapsed.TotalSeconds;

            // Store telemetry event
            _telemetryEvents[telemetryEvent.EventName] = telemetryEvent;

            // Notify subscribers
            try { TelemetryCollected?.Invoke(telemetryEvent); } catch { /* do not fail telemetry */ }

            var logger = Log.ForContext("DependencyName", dependencyName)
                           .ForContext("CommandName", commandName)
                           .ForContext("DurationMs", duration.TotalMilliseconds)
                           .ForContext("Success", success);

            if (success)
            {
                logger.Information("Dependency call completed: {Dependency} ({DurationMs}ms)", dependencyName, duration.TotalMilliseconds);
            }
            else
            {
                logger.Warning("Dependency call failed: {Dependency} ({DurationMs}ms)", dependencyName, duration.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking dependency: {DependencyName}", dependencyName);
        }
    }

    /// <summary>
    /// Gets recent telemetry events for analysis.
    /// </summary>
    public IEnumerable<TelemetryEvent> GetRecentTelemetryEvents(int maxEvents = 100)
    {
        return _telemetryEvents.Values
            .OrderByDescending(e => e.Timestamp)
            .Take(maxEvents);
    }

    /// <summary>
    /// Exports telemetry data to JSON for analysis.
    /// </summary>
    public string ExportTelemetryData()
    {
        var exportData = new
        {
            ExportTimestamp = DateTime.UtcNow,
            UptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds,
            Events = GetRecentTelemetryEvents(1000).ToList(),
            Counters = _counters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GetSessionId()
    {
        // Generate a session ID based on process start time for this application instance
        return $"session_{Process.GetCurrentProcess().StartTime.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Lightweight telemetry: increments a named counter and logs occasionally.
    /// </summary>
    public long IncrementCounter(string name, long value = 1)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Counter name is required", nameof(name));
            var current = _counters.AddOrUpdate(name, value, (_, existing) => existing + value);
            // Periodically emit snapshot (every ~100 increments)
            if (current % 100 == 0)
            {
                Log.ForContext("Counter", name)
                   .ForContext("Value", current)
                   .Information("Telemetry counter snapshot {Counter} = {Value}", name, current);
            }
            return current;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Reports a warning with structured logging.
    /// </summary>
    public void ReportWarning(string message, string context = null, string correlationId = null)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        correlationId ??= Guid.NewGuid().ToString();

        Log.ForContext("CorrelationId", correlationId)
            .ForContext("Context", context ?? "Unknown")
            .Warning("Warning in {Context}: {Message}", context, message);
    }

    /// <summary>
    /// Attempts to recover from an error using registered strategies.
    /// </summary>
    public async Task<bool> TryRecoverAsync(Exception exception, string context, Func<Task<bool>> recoveryAction)
    {
        var correlationId = Guid.NewGuid().ToString();

        Log.ForContext("CorrelationId", correlationId)
           .ForContext("Context", context)
           .Information("Attempting error recovery for {Context}", context);

        if (recoveryAction is null) throw new ArgumentNullException(nameof(recoveryAction));

        if (_recoveryStrategies.TryGetValue(context, out var strategy))
        {
            try
            {
                var success = await strategy.ExecuteAsync(recoveryAction);
                if (success)
                {
                    Log.ForContext("CorrelationId", correlationId)
                       .Information("Successfully recovered from error in {Context}", context);
                    return true;
                }
            }
            catch (Exception recoveryEx)
            {
                Log.ForContext("CorrelationId", correlationId)
                   .Error(recoveryEx, "Recovery failed for {Context}", context);
            }
        }

        Log.ForContext("CorrelationId", correlationId)
           .Warning("No recovery strategy available or recovery failed for {Context}", context);
        return false;
    }

    /// <summary>
    /// Registers a recovery strategy for a specific error context.
    /// </summary>
    public void RegisterRecoveryStrategy(string context, ErrorRecoveryStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(context)) throw new ArgumentException("Context is required", nameof(context));
        if (strategy is null) throw new ArgumentNullException(nameof(strategy));

        _recoveryStrategies[context] = strategy;
    }

    /// <summary>
    /// Handles errors with fallback actions.
    /// </summary>
    public async Task<T> HandleWithFallbackAsync<T>(Func<Task<T>> primaryAction,
                                                   Func<Task<T>> fallbackAction,
                                                   string context = null,
                                                   T defaultValue = default)
    {
        if (primaryAction is null) throw new ArgumentNullException(nameof(primaryAction));
        if (fallbackAction is null) throw new ArgumentNullException(nameof(fallbackAction));

        try
        {
            return await primaryAction();
        }
        catch (Exception ex)
        {
            ReportError(ex, context, showToUser: false);

            try
            {
                Log.Information("Attempting fallback action for {Context}", context);
                return await fallbackAction();
            }
            catch (Exception fallbackEx)
            {
                ReportError(fallbackEx, $"{context}_Fallback", showToUser: true);
                return defaultValue;
            }
        }
    }

    /// <summary>
    /// Safely executes an action that should not throw exceptions.
    /// </summary>
    public void SafeExecute(Action action, string context = null, bool logErrors = true)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (logErrors)
            {
                ReportError(ex, context, showToUser: false, level: LogEventLevel.Warning);
            }
        }
    }

    /// <summary>
    /// Safely executes an async action that should not throw exceptions.
    /// </summary>
    public async Task SafeExecuteAsync(Func<Task> action, string context = null, bool logErrors = true)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (logErrors)
            {
                ReportError(ex, context, showToUser: false, level: LogEventLevel.Warning);
            }
        }
    }

    private void ShowErrorDialog(Exception exception, string context, string correlationId)
    {
        try
        {
            if (exception is null) throw new ArgumentNullException(nameof(exception));
            if (SuppressUserDialogs)
            {
                // Respect suppression setting
                return;
            }
            var message = $"An error occurred in {context ?? "the application"}.\n\n" +
                         $"Error: {exception.Message}\n\n" +
                         $"Reference ID: {correlationId}\n\n" +
                         "Would you like to continue? (Some features may not work properly)";

            var result = System.Windows.MessageBox.Show(message, "Application Error",
                                       MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                Log.Information("User chose to exit after error (CorrelationId: {CorrelationId})", correlationId);
                System.Windows.Application.Current?.Shutdown();
            }
            else
            {
                Log.Information("User chose to continue after error (CorrelationId: {CorrelationId})", correlationId);
            }
        }
        catch
        {
            // If dialog fails, at least log it
            Log.Error("Failed to show error dialog for correlation ID {CorrelationId}", correlationId);
        }
    }
}

/// <summary>
/// Base class for error recovery strategies.
/// </summary>
public abstract class ErrorRecoveryStrategy
{
    public abstract Task<bool> ExecuteAsync(Func<Task<bool>> action);
}

/// <summary>
/// Represents a telemetry event for Application Insights-like tracking.
/// </summary>
public class TelemetryEvent
{
    public string EventName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public TimeSpan? Duration { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });
    }
}

/// <summary>
/// Retry strategy with exponential backoff.
/// </summary>
public class RetryWithDelayStrategy : ErrorRecoveryStrategy
{
    private readonly int _maxRetries;
    private readonly int _initialDelayMs;

    public RetryWithDelayStrategy(int maxRetries, int initialDelayMs)
    {
        _maxRetries = maxRetries;
        _initialDelayMs = initialDelayMs;
    }

    public override async Task<bool> ExecuteAsync(Func<Task<bool>> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var delay = _initialDelayMs;

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                Log.Information("Recovery attempt {Attempt}/{MaxRetries}", attempt, _maxRetries);
                var success = await action();
                if (success) return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Recovery attempt {Attempt} failed", attempt);
            }

            if (attempt < _maxRetries)
            {
                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
        }

        return false;
    }
}
