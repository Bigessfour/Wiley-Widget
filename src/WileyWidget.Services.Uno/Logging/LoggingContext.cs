using System;
using System.Threading;

namespace WileyWidget.Services.Logging;

/// <summary>
/// Provides correlation IDs and contextual information for structured logging
/// </summary>
public class LoggingContext : IDisposable
{
    private static readonly AsyncLocal<LoggingContext> _current = new();

    /// <summary>
    /// Gets the current logging context for the async flow
    /// </summary>
    public static LoggingContext Current => _current.Value ?? Default;

    /// <summary>
    /// Gets a default logging context when none is active
    /// </summary>
    public static LoggingContext Default { get; } = new LoggingContext(Guid.Empty, "None");

    /// <summary>
    /// Unique identifier for this operation/request
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// Name of the operation being performed
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// When this context was created
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Thread ID where this context was created
    /// </summary>
    public int ThreadId { get; }

    /// <summary>
    /// Parent correlation ID if this is a nested operation
    /// </summary>
    public Guid? ParentCorrelationId { get; }

    private LoggingContext(Guid correlationId, string operationName, Guid? parentCorrelationId = null)
    {
        CorrelationId = correlationId;
        OperationName = operationName;
        ParentCorrelationId = parentCorrelationId;
        StartTime = DateTime.UtcNow;
        ThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    /// <summary>
    /// Creates a new logging context for an operation
    /// </summary>
    /// <param name="operationName">Name of the operation</param>
    /// <returns>Disposable logging context that should be used in a using statement</returns>
    public static LoggingContext BeginOperation(string operationName)
    {
        var parentCorrelationId = _current.Value?.CorrelationId;
        var context = new LoggingContext(Guid.NewGuid(), operationName, parentCorrelationId);
        _current.Value = context;
        return context;
    }

    /// <summary>
    /// Creates a new logging context with a specific correlation ID (for incoming requests)
    /// </summary>
    /// <param name="correlationId">The correlation ID to use</param>
    /// <param name="operationName">Name of the operation</param>
    /// <returns>Disposable logging context</returns>
    public static LoggingContext BeginOperationWithId(Guid correlationId, string operationName)
    {
        var parentCorrelationId = _current.Value?.CorrelationId;
        var context = new LoggingContext(correlationId, operationName, parentCorrelationId);
        _current.Value = context;
        return context;
    }

    /// <summary>
    /// Gets the elapsed time since this context was created
    /// </summary>
    public TimeSpan Elapsed => DateTime.UtcNow - StartTime;

    /// <summary>
    /// Disposes the context and restores the parent context
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the object, releasing managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            if (_current.Value == this)
            {
                _current.Value = null;
            }
        }
        // No unmanaged resources to dispose
    }

    /// <summary>
    /// Finalizer for the LoggingContext class.
    /// </summary>
    ~LoggingContext()
    {
        Dispose(false);
    }

    public override string ToString()
    {
        var parent = ParentCorrelationId.HasValue ? $" (Parent: {ParentCorrelationId.Value:N})" : string.Empty;
        return $"[{OperationName}] CorrelationId: {CorrelationId:N}, Thread: {ThreadId}, Elapsed: {Elapsed.TotalMilliseconds:F2}ms{parent}";
    }
}
