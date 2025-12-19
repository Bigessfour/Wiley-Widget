using Microsoft.Extensions.Logging;
using System;

namespace WileyWidget.WinForms.Logging;

/// <summary>
/// Null object pattern for ILogger to prevent null reference exceptions in error handlers.
/// This ensures that even if DI fails to provide a logger, logging calls won't crash the application.
/// </summary>
/// <typeparam name="T">Type being logged</typeparam>
public sealed class NullLogger<T> : ILogger<T>
{
    /// <summary>
    /// Singleton instance to avoid allocations
    /// </summary>
    public static readonly NullLogger<T> Instance = new();

    private NullLogger() { }

    /// <summary>
    /// No-op scope - returns singleton null scope
    /// </summary>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <summary>
    /// Always returns false - no logging is enabled
    /// </summary>
    public bool IsEnabled(LogLevel logLevel) => false;

    /// <summary>
    /// No-op log method - silently ignores all log calls
    /// </summary>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        // No-op: silently ignore all log calls to prevent crashes
        // Note: In debug builds, consider writing to console to detect missing loggers
#if DEBUG
        if (logLevel >= LogLevel.Error)
        {
            Console.WriteLine($"[NullLogger<{typeof(T).Name}>] {logLevel}: {formatter(state, exception)}");
        }
#endif
    }

    /// <summary>
    /// Null scope implementation for BeginScope
    /// </summary>
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }
}
