using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    public static readonly ActivitySource ActivitySource = new("WileyWidget");
    public static readonly string ServiceName = "wiley-widget";
    public static readonly string ServiceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    public SigNozTelemetryService(ILogger<SigNozTelemetryService> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
    /// </summary>
    public void RecordException(Exception exception, params (string key, object? value)[] additionalTags)
    {
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
        try
        {
            // ActivitySource does not require explicit dispose in all runtimes, but keep method for symmetry.
            _logger.LogDebug("Disposing telemetry fallback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing telemetry fallback");
        }
    }
}
