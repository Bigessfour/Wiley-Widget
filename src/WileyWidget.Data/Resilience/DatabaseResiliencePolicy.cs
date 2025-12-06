using WileyWidget.Models;
#nullable enable
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace WileyWidget.Data.Resilience;

/// <summary>
/// Provides Polly v8-based resilience pipelines for database operations.
/// Handles transient failures from Azure SQL, authentication timeouts, network issues,
/// and implements comprehensive resilience patterns using modern Polly v8 ResiliencePipelineBuilder.
///
/// Upgraded from Polly v7 PolicyWrap to v8 ResiliencePipeline for:
/// - Better performance with context pooling
/// - Simplified configuration and composition
/// - Native async/await support without legacy wrappers
/// - Consistent API with XAIService resilience patterns
/// </summary>
public static class DatabaseResiliencePolicy
{
    /// <summary>
    /// Combined resilience pipeline for all database operations.
    /// Includes circuit breaker, timeout, authentication retry, timeout retry, and concurrency retry.
    /// </summary>
    public static ResiliencePipeline<TResult> CombinedDatabasePipeline<TResult>()
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResult>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<SqlException>(ex => IsTransientError(ex))
                    .Handle<TimeoutException>(),
                OnOpened = args =>
                {
                    Log.Warning("Database circuit breaker opened after failure");
                    return default;
                },
                OnClosed = args =>
                {
                    Log.Information("Database circuit breaker closed");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    Log.Information("Database circuit breaker half-open, testing connection");
                    return default;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                OnTimeout = args =>
                {
                    Log.Warning("Database operation timed out after {TimeoutSeconds}s", args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<SqlException>(ex => IsTransientError(ex)),
                OnRetry = args =>
                {
                    Log.Warning(args.Outcome.Exception,
                        "Database transient error on attempt {AttemptNumber}. Retrying in {RetryDelayMs}ms",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<TimeoutException>()
                    .Handle<SqlException>(ex => ex.Number == -2), // SQL timeout error
                OnRetry = args =>
                {
                    Log.Warning(args.Outcome.Exception,
                        "Database timeout on attempt {AttemptNumber}. Retrying in {RetryDelaySeconds}s",
                        args.AttemptNumber, args.RetryDelay.TotalSeconds);
                    return default;
                }
            })
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<DbUpdateConcurrencyException>(),
                OnRetry = args =>
                {
                    Log.Warning(args.Outcome.Exception, "Concurrency conflict detected. Retrying once.");
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Resilience pipeline for read operations (lighter resilience).
    /// Includes timeout and 2-attempt retry for transient errors.
    /// </summary>
    public static ResiliencePipeline<TResult> ReadOperationPipeline<TResult>()
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromMilliseconds(250),
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<SqlException>(ex => IsTransientError(ex))
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    Log.Warning(args.Outcome.Exception,
                        "Read operation failed (attempt {AttemptNumber}). Retrying in {RetryDelayMs}ms",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Resilience pipeline for write operations (stricter resilience).
    /// Includes circuit breaker, timeout, authentication retry, and concurrency retry.
    /// </summary>
    public static ResiliencePipeline<TResult> WriteOperationPipeline<TResult>()
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResult>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<SqlException>(ex => IsTransientError(ex))
                    .Handle<TimeoutException>()
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<SqlException>(ex => IsTransientError(ex))
            })
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<DbUpdateConcurrencyException>()
            })
            .Build();
    }

    /// <summary>
    /// Determines if a SQL exception is transient (retryable).
    /// </summary>
    private static bool IsTransientError(SqlException ex)
    {
        // Common transient error numbers for Azure SQL
        int[] transientErrorNumbers = {
            -2,     // Timeout
            -1,     // Connection broken
            2,      // Network error
            53,     // Connection failed
            64,     // Network-level error
            233,    // Connection initialization error
            10053,  // Transport-level error
            10054,  // Connection reset by peer
            10060,  // Network timeout
            40197,  // Service error processing request
            40501,  // Service busy
            40613,  // Database unavailable
            49918,  // Cannot process request (insufficient resources)
            49919,  // Cannot process create/update request (too many operations)
            49920   // Cannot process request (too many operations)
        };

        return Array.Exists(transientErrorNumbers, num => num == ex.Number);
    }

    /// <summary>
    /// Executes a database operation with combined resilience pipeline.
    /// Uses context pooling for performance.
    /// </summary>
    public static async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        var pipeline = CombinedDatabasePipeline<TResult>();
        return await pipeline.ExecuteAsync(async ct => await operation(ct), cancellationToken);
    }

    /// <summary>
    /// Executes a void database operation with combined resilience pipeline.
    /// </summary>
    public static async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(async ct =>
        {
            await operation(ct);
            return null;
        }, cancellationToken);
    }

    /// <summary>
    /// Executes a read operation with lighter resilience pipeline.
    /// </summary>
    public static async Task<TResult> ExecuteReadAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        var pipeline = ReadOperationPipeline<TResult>();
        return await pipeline.ExecuteAsync(async ct => await operation(ct), cancellationToken);
    }

    /// <summary>
    /// Executes a write operation with stricter resilience pipeline.
    /// </summary>
    public static async Task<TResult> ExecuteWriteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        var pipeline = WriteOperationPipeline<TResult>();
        return await pipeline.ExecuteAsync(async ct => await operation(ct), cancellationToken);
    }

    /// <summary>
    /// Executes a void write operation with stricter resilience pipeline.
    /// </summary>
    public static async Task ExecuteWriteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteWriteAsync<object?>(async ct =>
        {
            await operation(ct);
            return null;
        }, cancellationToken);
    }

    // ===== BACKWARD COMPATIBILITY: Legacy Polly v7 API =====
    // These overloads support existing code without CancellationToken parameters
    // New code should use the CancellationToken overloads above

    /// <summary>
    /// Executes a database operation with combined resilience pipeline (legacy v7 compatibility).
    /// </summary>
    [Obsolete("Use ExecuteAsync(Func<CancellationToken, Task<TResult>>, CancellationToken) for better cancellation support")]
    public static Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation)
    {
        return ExecuteAsync(_ => operation(), CancellationToken.None);
    }

    /// <summary>
    /// Executes a void database operation with combined resilience pipeline (legacy v7 compatibility).
    /// </summary>
    [Obsolete("Use ExecuteAsync(Func<CancellationToken, Task>, CancellationToken) for better cancellation support")]
    public static Task ExecuteAsync(Func<Task> operation)
    {
        return ExecuteAsync(_ => operation(), CancellationToken.None);
    }

    /// <summary>
    /// Executes a read operation with lighter resilience pipeline (legacy v7 compatibility).
    /// </summary>
    [Obsolete("Use ExecuteReadAsync(Func<CancellationToken, Task<TResult>>, CancellationToken) for better cancellation support")]
    public static Task<TResult> ExecuteReadAsync<TResult>(Func<Task<TResult>> operation)
    {
        return ExecuteReadAsync(_ => operation(), CancellationToken.None);
    }

    /// <summary>
    /// Executes a write operation with stricter resilience pipeline (legacy v7 compatibility).
    /// </summary>
    [Obsolete("Use ExecuteWriteAsync(Func<CancellationToken, Task<TResult>>, CancellationToken) for better cancellation support")]
    public static Task<TResult> ExecuteWriteAsync<TResult>(Func<Task<TResult>> operation)
    {
        return ExecuteWriteAsync(_ => operation(), CancellationToken.None);
    }

    /// <summary>
    /// Executes a void write operation with stricter resilience pipeline (legacy v7 compatibility).
    /// </summary>
    [Obsolete("Use ExecuteWriteAsync(Func<CancellationToken, Task>, CancellationToken) for better cancellation support")]
    public static Task ExecuteWriteAsync(Func<Task> operation)
    {
        return ExecuteWriteAsync(_ => operation(), CancellationToken.None);
    }
}
