using System.Threading;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Concurrent;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Telemetry;

/// <summary>
/// Lightweight SigNoz telemetry service fallback that does not depend on OpenTelemetry SDK types.
/// This implementation provides the same surface used by the rest of the application but
/// intentionally avoids SDK-level initialization to keep the project buildable when OpenTelemetry
/// package versions drift. For environments that require full OpenTelemetry integration,
/// replace with a richer implementation behind the ITelemetryService abstraction.
/// </summary>
public class SigNozTelemetryService : IDisposable, ITelemetryService
{
    private readonly ILogger<SigNozTelemetryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentQueue<TelemetryLog> _telemetryQueue = new();
    private readonly System.Timers.Timer _flushTimer;

    public static readonly ActivitySource ActivitySource = new("WileyWidget");
    public static readonly string ServiceName = "wiley-widget";
    public static readonly string ServiceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    public SigNozTelemetryService(ILogger<SigNozTelemetryService> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Set up periodic flush timer (every 30 seconds)
        _flushTimer = new System.Timers.Timer(30000);
        _flushTimer.Elapsed += async (sender, e) =>
        {
            try
            {
                await FlushTelemetryLogsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in telemetry flush timer");
            }
        };
        _flushTimer.Start();
    }

    /// <summary>
    /// No-op initialize. Logs startup info so telemetry startup remains observable.
    /// </summary>
    public void Initialize()
    {
        var sigNozEndpoint = _configuration["SigNoz:Endpoint"] ?? "http://localhost:4317";
        var environment = _configuration["Environment"] ?? "development";
        _logger.LogInformation("Telemetry (sigNoz fallback) initialized. Endpoint={Endpoint}, Environment={Environment}", sigNozEndpoint, environment);

        using var a = ActivitySource.StartActivity("signoz.telemetry.initialized");
        a?.SetTag("service.name", ServiceName);
        a?.SetTag("service.version", ServiceVersion);
        a?.SetTag("environment", environment);
    }

    /// <summary>
    /// Create a new Activity for tracing; callers should dispose it when finished.
    /// </summary>
    public Activity? StartActivity(string operationName, params (string key, object? value)[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var activity = ActivitySource.StartActivity(operationName);
        if (activity != null)
        {
            foreach (var (k, v) in tags)
            {
                activity.SetTag(k, v?.ToString());
            }
        }

        return activity;
    }

    /// <summary>
    /// Record an exception into the current activity and the application logger.
    /// Also buffers the exception for periodic DB logging.
    /// </summary>
    public void RecordException(Exception exception, params (string key, object? value)[] additionalTags)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(additionalTags);
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag("exception.type", exception.GetType().Name);
            activity.SetTag("exception.message", exception.Message);
            activity.SetTag("exception.stacktrace", exception.StackTrace);

            foreach (var (k, v) in additionalTags)
            {
                activity.SetTag(k, v?.ToString());
            }
        }

        // Buffer for DB logging
        var telemetryLog = new TelemetryLog
        {
            EventType = "Exception",
            Message = exception.Message,
            Details = System.Text.Json.JsonSerializer.Serialize(new
            {
                Type = exception.GetType().Name,
                AdditionalTags = additionalTags
            }),
            StackTrace = exception.StackTrace,
            CorrelationId = Activity.Current?.Id,
            Timestamp = DateTime.UtcNow,
            User = Environment.UserName,
            SessionId = Guid.NewGuid().ToString() // Could be improved to track sessions
        };
        _telemetryQueue.Enqueue(telemetryLog);

        _logger.LogError(exception, "Exception recorded in telemetry fallback");
    }

    /// <summary>
    /// Basic connectivity validation (no network calls in fallback implementation).
    /// </summary>
    public bool ValidateConnectivity()
    {
        _logger.LogDebug("Telemetry fallback connectivity check (no-op)");
        return true;
    }

    /// <summary>
    /// Flushes buffered telemetry logs to the database using a fresh context.
    /// </summary>
    private async Task FlushTelemetryLogsAsync(CancellationToken cancellationToken = default)
    {
        if (_telemetryQueue.IsEmpty)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var context = factory.CreateDbContext();

            var logs = new List<TelemetryLog>();
            while (_telemetryQueue.TryDequeue(out var log))
            {
                logs.Add(log);
            }

            if (logs.Any())
            {
                await context.TelemetryLogs.AddRangeAsync(logs);
                await context.SaveChangesAsync();
                _logger.LogDebug("Flushed {Count} telemetry logs to database", logs.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing telemetry logs to database");
            // Re-queue failed logs? For simplicity, log and continue
        }
    }

    /// <summary>
    /// Returns a minimal telemetry status object for diagnostics.
    /// </summary>
    public object GetTelemetryStatus()
    {
        return new
        {
            ServiceName,
            ServiceVersion,
            Endpoint = _configuration["SigNoz:Endpoint"] ?? "http://localhost:4317",
            Environment = _configuration["Environment"] ?? "development",
            TracingEnabled = true
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        try
        {
            _flushTimer?.Stop();
            _flushTimer?.Dispose();

            // Final flush on dispose
            FlushTelemetryLogsAsync().GetAwaiter().GetResult(); // Synchronous wait for final flush

            // ActivitySource does not require explicit dispose in all runtimes, but keep method for symmetry.
            _logger.LogDebug("Disposing telemetry fallback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing telemetry fallback");
        }
    }
}
