using System;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for logging telemetry events, errors, and user interactions to the database.
/// </summary>
public interface ITelemetryLogService
{
    /// <summary>
    /// Logs an error event with optional stack trace.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="details">Optional JSON-serialized error details</param>
    /// <param name="stackTrace">Optional stack trace</param>
    /// <param name="correlationId">Optional correlation ID for tracking related events</param>
    /// <param name="user">Optional username</param>
    /// <param name="sessionId">Optional session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogErrorAsync(
        string message,
        string? details = null,
        string? stackTrace = null,
        string? correlationId = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an exception with full details.
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="message">Optional additional message</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="user">Optional username</param>
    /// <param name="sessionId">Optional session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogExceptionAsync(
        Exception exception,
        string? message = null,
        string? correlationId = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a general event (info, warning, etc.).
    /// </summary>
    /// <param name="eventType">Type of event (e.g., "Info", "Warning", "UserAction")</param>
    /// <param name="message">Event message</param>
    /// <param name="details">Optional JSON-serialized event details</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <param name="user">Optional username</param>
    /// <param name="sessionId">Optional session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogEventAsync(
        string eventType,
        string message,
        string? details = null,
        string? correlationId = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a user action/interaction.
    /// </summary>
    /// <param name="action">Action description</param>
    /// <param name="details">Optional JSON-serialized action details</param>
    /// <param name="user">Optional username</param>
    /// <param name="sessionId">Optional session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogUserActionAsync(
        string action,
        string? details = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);
}
