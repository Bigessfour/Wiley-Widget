#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace WileyWidget.Infrastructure.Logging;

/// <summary>
/// Base interface for WPF middleware components
/// </summary>
public interface IWpfMiddleware
{
    Task ExecuteAsync(WpfMiddlewareContext context, Func<Task> next);
}

/// <summary>
/// Context object passed through the middleware pipeline
/// </summary>
public class WpfMiddlewareContext
{
    public string OperationName { get; set; } = string.Empty;
    public object? Input { get; set; }
    public object? Output { get; set; }
    public Exception? Exception { get; set; }
    public System.Collections.Generic.Dictionary<string, object> Properties { get; } = new();
    public bool Handled { get; set; }
}

/// <summary>
/// Logging middleware for WPF operations
/// </summary>
public class LoggingMiddleware : IWpfMiddleware
{
    public async Task ExecuteAsync(WpfMiddlewareContext context, Func<Task> next)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Log.Information("Starting WPF operation: {OperationName}", context.OperationName);

            await next();

            stopwatch.Stop();

            if (context.Exception != null)
            {
                Log.Error(context.Exception, "WPF operation failed: {OperationName} ({ElapsedMs}ms)",
                         context.OperationName, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                Log.Information("WPF operation completed: {OperationName} ({ElapsedMs}ms)",
                               context.OperationName, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            context.Exception = ex;
            throw;
        }
    }
}
