using System;
using System.Diagnostics;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Minimal telemetry abstraction used by lower layers to report exceptions and diagnostic data.
/// Kept intentionally small to avoid circular project references.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Record an exception with optional key/value tags for richer context.
    /// </summary>
    void RecordException(Exception exception, params (string key, object? value)[] additionalTags);

    /// <summary>
    /// Create a new Activity for tracing; callers should dispose it when finished.
    /// </summary>
    Activity? StartActivity(string operationName, params (string key, object? value)[] tags);
}
