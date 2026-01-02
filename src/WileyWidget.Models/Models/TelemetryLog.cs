using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents a telemetry log entry for exceptions and events
/// </summary>
public class TelemetryLog
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty; // Exception, Event, Metric, etc.
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; } // JSON serialized additional data
    public string? StackTrace { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? User { get; set; }
    public string? SessionId { get; set; }
}
