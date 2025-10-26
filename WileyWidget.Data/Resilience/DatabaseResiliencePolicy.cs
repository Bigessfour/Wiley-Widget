using WileyWidget.Models;
#nullable enable
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System;
using System.Threading.Tasks;
using Serilog;

namespace WileyWidget.Data.Resilience;

/// <summary>
/// Provides Polly-based resilience policies for database operations
/// Handles transient failures from Azure SQL, authentication timeouts, network issues,
/// and implements comprehensive resilience patterns using Polly v8
/// </summary>
public static class DatabaseResiliencePolicy
{
    /// <summary>
    /// Retry policy for authentication/transient failures when connecting to SQL Server.
    /// Retries 3 times with exponential backoff (500ms, 1s, 2s) using Polly v8 syntax.
    /// </summary>
    public static AsyncRetryPolicy DatabaseAuthRetryPolicy { get; } = Policy
        .Handle<SqlException>(ex => IsTransientError(ex))
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1)),
            onRetryAsync: async (exception, timeSpan, retryCount, context) =>
            {
                Log.Warning(exception,
                    "Azure authentication/transient error on attempt {RetryCount}. Retrying in {RetryDelayMs}ms",
                    retryCount, timeSpan.TotalMilliseconds);
                await Task.CompletedTask;
            });

    /// <summary>
    /// Retry policy for database operation timeouts using Polly v8
    /// Retries 2 times with linear backoff (1s, 2s)
    /// </summary>
    public static AsyncRetryPolicy DatabaseTimeoutRetryPolicy { get; } = Policy
        .Handle<TimeoutException>()
        .Or<SqlException>(ex => ex.Number == -2) // Timeout error number
        .WaitAndRetryAsync(
            retryCount: 2,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryAttempt),
            onRetryAsync: async (exception, timeSpan, retryCount, context) =>
            {
                Log.Warning(exception,
                    "Database timeout on attempt {RetryCount}. Retrying in {RetryDelaySeconds}s",
                    retryCount, timeSpan.TotalSeconds);
                await Task.CompletedTask;
            });

    /// <summary>
    /// Retry policy for EF Core concurrency conflicts using Polly v8
    /// Retries once immediately
    /// </summary>
    public static AsyncRetryPolicy ConcurrencyRetryPolicy { get; } = Policy
        .Handle<DbUpdateConcurrencyException>()
        .RetryAsync(
            retryCount: 1,
            onRetryAsync: async (exception, retryCount) =>
            {
                Log.Warning(exception, "Concurrency conflict detected. Retrying once.");
                await Task.CompletedTask;
            });

    /// <summary>
    /// Circuit breaker policy for database operations to prevent cascade failures
    /// </summary>
    public static AsyncCircuitBreakerPolicy DatabaseCircuitBreakerPolicy { get; } = Policy
        .Handle<SqlException>(ex => IsTransientError(ex))
        .Or<TimeoutException>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (exception, breakDelay) =>
                Log.Warning(exception, "Database circuit breaker opened for {Duration}", breakDelay),
            onReset: () => Log.Information("Database circuit breaker reset"),
            onHalfOpen: () => Log.Information("Database circuit breaker half-open"));

    /// <summary>
    /// Timeout policy for database operations to prevent hanging queries
    /// </summary>
    public static AsyncTimeoutPolicy DatabaseTimeoutPolicy { get; } = Policy
        .TimeoutAsync(
            timeout: TimeSpan.FromSeconds(30),
            timeoutStrategy: TimeoutStrategy.Pessimistic,
            onTimeoutAsync: async (context, timeout, task, exception) =>
            {
                Log.Warning(exception, "Database operation timed out after {TimeoutSeconds}s", timeout.TotalSeconds);
                await Task.CompletedTask;
            });

    /// <summary>
    /// Combined policy for all database operations using Polly v8 PolicyWrap
    /// Wraps authentication, timeout, concurrency, circuit breaker, and timeout policies
    /// </summary>
    public static AsyncPolicy CombinedDatabasePolicy { get; } = Policy.WrapAsync(
        DatabaseCircuitBreakerPolicy,
        DatabaseTimeoutPolicy,
        Policy.WrapAsync(
            DatabaseAuthRetryPolicy,
            DatabaseTimeoutRetryPolicy,
            ConcurrencyRetryPolicy
        ));

    /// <summary>
    /// Resilience pipeline for read operations (lighter resilience)
    /// </summary>
    public static AsyncPolicy ReadOperationPolicy { get; } = Policy.WrapAsync(
        DatabaseTimeoutPolicy,
        Policy.Handle<SqlException>(ex => IsTransientError(ex))
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(250 * retryAttempt),
                onRetryAsync: async (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning(exception, "Read operation failed (attempt {RetryCount}). Retrying in {RetryDelayMs}ms",
                        retryCount, timeSpan.TotalMilliseconds);
                    await Task.CompletedTask;
                }));

    /// <summary>
    /// Resilience pipeline for write operations (stricter resilience)
    /// </summary>
    public static AsyncPolicy WriteOperationPolicy { get; } = Policy.WrapAsync(
        DatabaseCircuitBreakerPolicy,
        DatabaseTimeoutPolicy,
        Policy.WrapAsync(
            DatabaseAuthRetryPolicy,
            ConcurrencyRetryPolicy,
            Policy.Handle<SqlException>(ex => IsTransientError(ex))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1)),
                    onRetryAsync: async (exception, timeSpan, retryCount, context) =>
                    {
                        Log.Warning(exception, "Write operation failed (attempt {RetryCount}). Retrying in {RetryDelayMs}ms",
                            retryCount, timeSpan.TotalMilliseconds);
                        await Task.CompletedTask;
                    })));

    /// <summary>
    /// Determines if a SQL exception is transient (retryable)
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
    /// Executes a database operation with combined resilience policy
    /// </summary>
    public static Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation)
    {
        return CombinedDatabasePolicy.ExecuteAsync(operation);
    }

    /// <summary>
    /// Executes a void database operation with combined resilience policy
    /// </summary>
    public static Task ExecuteAsync(Func<Task> operation)
    {
        return CombinedDatabasePolicy.ExecuteAsync(operation);
    }

    /// <summary>
    /// Executes a read operation with lighter resilience policy
    /// </summary>
    public static Task<TResult> ExecuteReadAsync<TResult>(Func<Task<TResult>> operation)
    {
        return ReadOperationPolicy.ExecuteAsync(operation);
    }

    /// <summary>
    /// Executes a write operation with stricter resilience policy
    /// </summary>
    public static Task<TResult> ExecuteWriteAsync<TResult>(Func<Task<TResult>> operation)
    {
        return WriteOperationPolicy.ExecuteAsync(operation);
    }

    /// <summary>
    /// Executes a void write operation with stricter resilience policy
    /// </summary>
    public static Task ExecuteWriteAsync(Func<Task> operation)
    {
        return WriteOperationPolicy.ExecuteAsync(operation);
    }
}
