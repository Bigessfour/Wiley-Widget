#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;
using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Configuration;

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
    public Dictionary<string, object> Properties { get; } = new();
    public bool Handled { get; set; }
}

/// <summary>
/// Middleware pipeline for WPF applications
/// </summary>
public class WpfMiddlewarePipeline
{
    private readonly List<IWpfMiddleware> _middlewares = new();

    public void AddMiddleware(IWpfMiddleware middleware)
    {
        _middlewares.Add(middleware);
    }

    public async Task ExecuteAsync(WpfMiddlewareContext context)
    {
        var index = 0;

        async Task Next()
        {
            if (index < _middlewares.Count)
            {
                var middleware = _middlewares[index];
                index++;
                await middleware.ExecuteAsync(context, Next);
            }
        }

        await Next();
    }
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

/// <summary>
/// Error handling middleware for WPF operations
/// </summary>
public class ErrorHandlingMiddleware : IWpfMiddleware
{
    public async Task ExecuteAsync(WpfMiddlewareContext context, Func<Task> next)
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            context.Exception = ex;
            context.Handled = true;

            Log.Error(ex, "Unhandled exception in WPF operation: {OperationName}", context.OperationName);

            // Could show user-friendly error dialog here
            // For now, just re-throw to maintain existing behavior
            throw;
        }
    }
}

/// <summary>
/// Performance monitoring middleware for WPF operations
/// </summary>
public class PerformanceMiddleware : IWpfMiddleware
{
    private const int PerformanceThresholdMs = 1000; // 1 second

    public async Task ExecuteAsync(WpfMiddlewareContext context, Func<Task> next)
    {
        var stopwatch = Stopwatch.StartNew();

        await next();

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > PerformanceThresholdMs)
        {
            Log.Warning("Slow WPF operation detected: {OperationName} took {ElapsedMs}ms",
                       context.OperationName, stopwatch.ElapsedMilliseconds);
        }
    }
}

/// <summary>
/// Validation middleware for WPF operations
/// </summary>
public class ValidationMiddleware : IWpfMiddleware
{
    public async Task ExecuteAsync(WpfMiddlewareContext context, Func<Task> next)
    {
        // Pre-operation validation
        if (context.Input != null)
        {
            var validationResults = ValidateObject(context.Input);
            if (validationResults.Any())
            {
                context.Exception = new ValidationException("Input validation failed: " + string.Join(", ", validationResults));
                return;
            }
        }

        await next();

        // Post-operation validation
        if (context.Output != null)
        {
            var validationResults = ValidateObject(context.Output);
            if (validationResults.Any())
            {
                Log.Warning("Output validation warnings for operation {OperationName}: {Warnings}",
                           context.OperationName, string.Join(", ", validationResults));
            }
        }
    }

    private List<string> ValidateObject(object obj)
    {
        var results = new List<string>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(obj);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(obj, context, validationResults, true))
        {
            results.AddRange(validationResults.Select(r => r.ErrorMessage ?? "Unknown validation error"));
        }

        return results;
    }
}

/// <summary>
/// Middleware service for WPF applications
/// </summary>
public class WpfMiddlewareService
{
    private readonly WpfMiddlewarePipeline _pipeline;

    public WpfMiddlewareService()
    {
        _pipeline = new WpfMiddlewarePipeline();

        // Add default middleware
        _pipeline.AddMiddleware(new LoggingMiddleware());
        _pipeline.AddMiddleware(new ErrorHandlingMiddleware());
        _pipeline.AddMiddleware(new PerformanceMiddleware());
        _pipeline.AddMiddleware(new ValidationMiddleware());
    }

    public async Task ExecuteAsync(string operationName, Func<Task> operation, object? input = null)
    {
        var context = new WpfMiddlewareContext
        {
            OperationName = operationName,
            Input = input
        };

        await _pipeline.ExecuteAsync(context);

        if (context.Exception != null && !context.Handled)
        {
            throw context.Exception;
        }
    }

    public async Task<T> ExecuteAsync<T>(string operationName, Func<Task<T>> operation, object? input = null)
    {
        var context = new WpfMiddlewareContext
        {
            OperationName = operationName,
            Input = input
        };

        await _pipeline.ExecuteAsync(context);

        if (context.Exception != null && !context.Handled)
        {
            throw context.Exception;
        }

        return (T)context.Output!;
    }
}
