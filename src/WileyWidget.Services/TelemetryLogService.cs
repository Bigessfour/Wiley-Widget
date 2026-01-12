using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Service for logging telemetry events to the database.
/// Handles errors, events, and user interactions for monitoring and diagnostics.
/// </summary>
public class TelemetryLogService : ITelemetryLogService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<TelemetryLogService> _logger;

    public TelemetryLogService(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<TelemetryLogService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task LogErrorAsync(
        string message,
        string? details = null,
        string? stackTrace = null,
        string? correlationId = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var telemetryLog = new TelemetryLog
            {
                EventType = "Error",
                Message = message,
                Details = details,
                StackTrace = stackTrace,
                CorrelationId = correlationId,
                User = user,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            };

            context.TelemetryLogs.Add(telemetryLog);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Telemetry error logged: {Message}", message);
        }
        catch (Exception ex)
        {
            // Don't throw - telemetry failures shouldn't break the application
            _logger.LogWarning(ex, "Failed to log telemetry error: {Message}", message);
        }
    }

    /// <inheritdoc/>
    public async Task LogExceptionAsync(
        Exception exception,
        string? message = null,
        string? correlationId = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        var errorMessage = message ?? exception.Message;
        var details = $"Exception Type: {exception.GetType().FullName}\nMessage: {exception.Message}";

        if (exception.InnerException != null)
        {
            details += $"\nInner Exception: {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}";
        }

        await LogErrorAsync(
            errorMessage,
            details,
            exception.StackTrace,
            correlationId,
            user,
            sessionId,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LogEventAsync(
        string eventType,
        string message,
        string? details = null,
        string? correlationId = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType cannot be null or empty", nameof(eventType));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var telemetryLog = new TelemetryLog
            {
                EventType = eventType,
                Message = message,
                Details = details,
                CorrelationId = correlationId,
                User = user,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            };

            context.TelemetryLogs.Add(telemetryLog);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Telemetry event logged: {EventType} - {Message}", eventType, message);
        }
        catch (Exception ex)
        {
            // Don't throw - telemetry failures shouldn't break the application
            _logger.LogWarning(ex, "Failed to log telemetry event: {EventType} - {Message}", eventType, message);
        }
    }

    /// <inheritdoc/>
    public async Task LogUserActionAsync(
        string action,
        string? details = null,
        string? user = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be null or empty", nameof(action));

        await LogEventAsync(
            "UserAction",
            action,
            details,
            null,
            user,
            sessionId,
            cancellationToken);
    }
}
