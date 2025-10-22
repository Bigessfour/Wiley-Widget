using System;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace WileyWidget.Services;

/// <summary>
/// Centralized error reporting service for consistent error handling across the application.
/// Provides structured logging, user notifications, and recovery mechanisms.
/// </summary>
public class ErrorReportingService
{
    private static readonly Lazy<ErrorReportingService> _instance = new(() => new ErrorReportingService());
    public static ErrorReportingService Instance => _instance.Value;

    private readonly Dictionary<string, ErrorRecoveryStrategy> _recoveryStrategies = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();

    /// <summary>
    /// When true, user dialogs are suppressed (useful for automated tests and headless runs).
    /// </summary>
    public bool SuppressUserDialogs { get; set; } = false;

    /// <summary>
    /// Raised whenever an error is reported. Useful for tests to observe errors without UI.
    /// </summary>
    public event Action<Exception, string?>? ErrorReported;

    private ErrorReportingService()
    {
        // Register default recovery strategies
        RegisterRecoveryStrategy("Authentication", new RetryWithDelayStrategy(maxRetries: 3, initialDelayMs: 1000));
        RegisterRecoveryStrategy("Network", new RetryWithDelayStrategy(maxRetries: 2, initialDelayMs: 2000));
        RegisterRecoveryStrategy("Database", new RetryWithDelayStrategy(maxRetries: 1, initialDelayMs: 500));
    }

    /// <summary>
    /// Reports an error with structured logging and optional user notification.
    /// </summary>
    public void ReportError(Exception exception, string context = null, bool showToUser = true,
                           LogEventLevel level = LogEventLevel.Error, string correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString();

        // Structured logging with context
        var logContext = Log.ForContext("CorrelationId", correlationId)
                           .ForContext("Context", context ?? "Unknown")
                           .ForContext("ExceptionType", exception.GetType().Name)
                           .ForContext("ExceptionMessage", exception.Message);

        logContext.Write(level, exception, "Error occurred in {Context}: {Message}", context, exception.Message);

        // Notify subscribers (tests, telemetry listeners)
        try { ErrorReported?.Invoke(exception, context); } catch { /* do not fail reporting */ }

        // Show user-friendly dialog if requested and not suppressed
        if (showToUser && !SuppressUserDialogs && Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
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
            var logger = Log.ForContext("TelemetryEvent", eventName);
            if (properties != null)
            {
                // Destructure properties so they remain structured in sinks
                logger = logger.ForContext("Properties", properties, destructureObjects: true);
            }
            logger.Information("Telemetry event: {Event}", eventName);
        }
        catch
        {
            // Best effort only
        }
    }

    /// <summary>
    /// Lightweight telemetry: increments a named counter and logs occasionally.
    /// </summary>
    public long IncrementCounter(string name, long value = 1)
    {
        try
        {
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
            if (SuppressUserDialogs)
            {
                // Respect suppression setting
                return;
            }
            var message = $"An error occurred in {context ?? "the application"}.\n\n" +
                         $"Error: {exception.Message}\n\n" +
                         $"Reference ID: {correlationId}\n\n" +
                         "Would you like to continue? (Some features may not work properly)";

            var result = MessageBox.Show(message, "Application Error",
                                       MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                Log.Information("User chose to exit after error (CorrelationId: {CorrelationId})", correlationId);
                Application.Current?.Shutdown();
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
