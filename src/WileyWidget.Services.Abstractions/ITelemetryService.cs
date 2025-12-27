using System;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Minimal telemetry abstraction used by lower layers to report exceptions and diagnostic data.
/// Kept intentionally small to avoid circular project references.
/// </summary>
/// <summary>
/// Represents a interface for itelemetryservice.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Record an exception with optional key/value tags for richer context.
    /// </summary>
    void RecordException(Exception exception, params (string key, object? value)[] additionalTags);
}
