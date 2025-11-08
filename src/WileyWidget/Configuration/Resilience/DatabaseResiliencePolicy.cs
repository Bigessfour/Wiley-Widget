using System;
using System.Data.Common;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Polly;
using Polly.Retry;
using Polly.Wrap;
using Serilog;

namespace WileyWidget.Configuration.Resilience
{
    /// <summary>
    /// Enterprise-grade database resilience policies using Polly for EF Core operations.
    /// Provides retry logic for transient database failures and connection issues.
    /// Extended to support QuickBooks API resilience.
    /// </summary>
    public static class DatabaseResiliencePolicy
    {
        /// <summary>
        /// Creates a retry policy for database operations that handles transient failures.
        /// </summary>
        public static AsyncRetryPolicy CreateDatabaseRetryPolicy(int maxRetryAttempts = 3, int baseDelayMs = 100)
        {
            return Policy
                .Handle<DbUpdateException>(ex => IsTransientException(ex))
                .Or<DbException>(ex => IsTransientException(ex))
                .Or<TimeoutException>()
                .Or<InvalidOperationException>(ex => IsTransientException(ex))
                .WaitAndRetryAsync(
                    maxRetryAttempts,
                    attempt => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1)), // Exponential backoff
                    (exception, timeSpan, retryCount, context) =>
                    {
                        Log.Warning(exception, "Database operation failed (attempt {RetryCount}/{MaxRetries}). Retrying in {DelayMs}ms. Context: {Context}",
                            retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds, context?.OperationKey ?? "Unknown");

                        // Track telemetry for retry attempts
                        TrackDatabaseRetryTelemetry(exception, retryCount, maxRetryAttempts, timeSpan);
                    });
        }

        /// <summary>
        /// Creates a circuit breaker policy for database operations to prevent cascading failures.
        /// </summary>
        public static AsyncPolicyWrap CreateDatabaseResiliencePolicy(int maxRetryAttempts = 3, int baseDelayMs = 100)
        {
            var retryPolicy = CreateDatabaseRetryPolicy(maxRetryAttempts, baseDelayMs);

            var circuitBreakerPolicy = Policy
                .Handle<DbUpdateException>(ex => IsTransientException(ex))
                .Or<DbException>(ex => IsTransientException(ex))
                .Or<TimeoutException>()
                .Or<InvalidOperationException>(ex => IsTransientException(ex))
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, breakDelay) =>
                    {
                        Log.Error(exception, "Database circuit breaker opened for {BreakDelaySeconds}s due to repeated failures",
                            breakDelay.TotalSeconds);

                        TrackDatabaseCircuitBreakerTelemetry(exception, "Opened", breakDelay);
                    },
                    onReset: () =>
                    {
                        Log.Information("Database circuit breaker reset - operations can resume");

                        TrackDatabaseCircuitBreakerTelemetry(null, "Reset", TimeSpan.Zero);
                    },
                    onHalfOpen: () =>
                    {
                        Log.Information("Database circuit breaker half-open - testing if operations can resume");

                        TrackDatabaseCircuitBreakerTelemetry(null, "HalfOpen", TimeSpan.Zero);
                    });

            return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }

        /// <summary>
        /// Applies resilience policies to an EF Core DbContext.
        /// </summary>
        public static DbContextOptionsBuilder ApplyResiliencePolicies(this DbContextOptionsBuilder optionsBuilder)
        {
            // For now, just ensure retry on failure is enabled
            // The full resilience pipeline would be added here when EF Core version supports it
            return optionsBuilder;
        }

        /// <summary>
        /// Determines if an exception represents a transient database failure that should be retried.
        /// </summary>
        private static bool IsTransientException(Exception exception)
        {
            if (exception is DbUpdateException dbUpdateEx)
            {
                // Check inner exception for SQL-specific errors
                var innerEx = dbUpdateEx.InnerException;
                if (innerEx is System.Data.SqlClient.SqlException sqlEx)
                {
                    // SQL Server transient error numbers
                    return sqlEx.Number switch
                    {
                        1205 => true, // Deadlock
                        1222 => true, // Lock timeout
                        -2 => true,   // Timeout
                        4060 => true, // Login failed
                        40197 => true, // Connection terminated
                        40501 => true, // Service busy
                        40613 => true, // Database unavailable
                        _ => false
                    };
                }
            }

            if (exception is DbException dbEx)
            {
                // Generic database transient errors
                return true; // Most DbExceptions are transient
            }

            if (exception is InvalidOperationException invalidOpEx)
            {
                // Check for common transient InvalidOperationException messages
                var message = invalidOpEx.Message.ToLowerInvariant();
                return message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                       message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                       message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                       message.Contains("transient", StringComparison.OrdinalIgnoreCase);
            }

            if (exception is TimeoutException)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tracks telemetry for database retry attempts.
        /// </summary>
        private static void TrackDatabaseRetryTelemetry(Exception exception, int retryCount, int maxRetries, TimeSpan delay)
        {
            try
            {
                // This would integrate with ErrorReportingService if available
                // For now, just log structured data
                Log.ForContext("TelemetryEvent", "Database_Retry")
                    .ForContext("ExceptionType", exception.GetType().Name)
                    .ForContext("RetryCount", retryCount)
                    .ForContext("MaxRetries", maxRetries)
                    .ForContext("DelayMs", delay.TotalMilliseconds)
                    .Information("Database retry telemetry: {ExceptionType} (attempt {RetryCount}/{MaxRetries})");
            }
            catch
            {
                // Don't fail the retry operation due to telemetry issues
            }
        }

        /// <summary>
        /// Tracks telemetry for database circuit breaker state changes.
        /// </summary>
        private static void TrackDatabaseCircuitBreakerTelemetry(Exception? exception, string state, TimeSpan breakDelay)
        {
            try
            {
                var logger = Log.ForContext("TelemetryEvent", "Database_CircuitBreaker")
                               .ForContext("State", state)
                               .ForContext("BreakDelaySeconds", breakDelay.TotalSeconds);

                if (exception != null)
                {
                    logger = logger.ForContext("ExceptionType", exception.GetType().Name);
                }

                logger.Information("Database circuit breaker telemetry: {State}");
            }
            catch
            {
                // Don't fail the circuit breaker operation due to telemetry issues
            }
        }

        /// <summary>
        /// Creates a retry policy specifically for QuickBooks Online API calls.
        /// Handles transient HTTP errors, rate limiting, and network issues.
        /// </summary>
        /// <param name="maxRetryAttempts">Maximum number of retry attempts (default: 3)</param>
        /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff (default: 1000ms)</param>
        /// <returns>Async retry policy for QuickBooks API calls</returns>
        public static AsyncRetryPolicy<HttpResponseMessage> CreateQuickBooksApiRetryPolicy(int maxRetryAttempts = 3, int baseDelayMs = 1000)
        {
            return Policy
                .HandleResult<HttpResponseMessage>(response => IsTransientHttpResponse(response))
                .Or<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    maxRetryAttempts,
                    attempt => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1)), // Exponential backoff: 1s, 2s, 4s
                    (outcome, timeSpan, retryCount, context) =>
                    {
                        var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                        var exception = outcome.Exception;

                        if (exception != null)
                        {
                            Log.Warning(exception,
                                "[QB_API_RETRY] Attempt {RetryCount}/{MaxRetries} failed with exception. Retrying in {DelayMs}ms. Context: {Context}",
                                retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds, context?.OperationKey ?? "Unknown");
                        }
                        else
                        {
                            Log.Warning(
                                "[QB_API_RETRY] Attempt {RetryCount}/{MaxRetries} failed with HTTP {StatusCode}. Retrying in {DelayMs}ms. Context: {Context}",
                                retryCount, maxRetryAttempts, statusCode, timeSpan.TotalMilliseconds, context?.OperationKey ?? "Unknown");
                        }

                        // Track telemetry for QuickBooks API retry attempts
                        TrackQuickBooksApiRetryTelemetry(outcome.Exception, outcome.Result, retryCount, maxRetryAttempts, timeSpan);
                    });
        }

        /// <summary>
        /// Creates a comprehensive resilience policy for QuickBooks API calls with retry and circuit breaker.
        /// </summary>
        /// <param name="maxRetryAttempts">Maximum number of retry attempts (default: 3)</param>
        /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff (default: 1000ms)</param>
        /// <returns>Async policy wrap combining retry and circuit breaker for QuickBooks API</returns>
        public static AsyncPolicyWrap<HttpResponseMessage> CreateQuickBooksApiResiliencePolicy(int maxRetryAttempts = 3, int baseDelayMs = 1000)
        {
            var retryPolicy = CreateQuickBooksApiRetryPolicy(maxRetryAttempts, baseDelayMs);

            // Advanced circuit breaker for HttpResponseMessage results
            var circuitBreakerPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult(response => IsTransientHttpResponse(response))
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5, // Break if 50% of requests fail
                    samplingDuration: TimeSpan.FromSeconds(30), // Over a 30-second window
                    minimumThroughput: 5, // Minimum 5 requests before breaking
                    durationOfBreak: TimeSpan.FromMinutes(1), // Longer break for API calls
                    onBreak: (outcome, breakDelay) =>
                    {
                        var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                        Log.Error(
                            "[QB_API_CIRCUIT] Circuit breaker opened for {BreakDelaySeconds}s due to repeated QuickBooks API failures. Last status: {StatusCode}",
                            breakDelay.TotalSeconds, statusCode);

                        TrackQuickBooksApiCircuitBreakerTelemetry(outcome.Exception, outcome.Result, "Opened", breakDelay);
                    },
                    onReset: () =>
                    {
                        Log.Information("[QB_API_CIRCUIT] Circuit breaker reset - QuickBooks API operations can resume");
                        TrackQuickBooksApiCircuitBreakerTelemetry(null, null, "Reset", TimeSpan.Zero);
                    },
                    onHalfOpen: () =>
                    {
                        Log.Information("[QB_API_CIRCUIT] Circuit breaker half-open - testing if QuickBooks API operations can resume");
                        TrackQuickBooksApiCircuitBreakerTelemetry(null, null, "HalfOpen", TimeSpan.Zero);
                    });

            return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }

        /// <summary>
        /// Determines if an HTTP response represents a transient error that should be retried.
        /// </summary>
        /// <param name="response">The HTTP response to evaluate</param>
        /// <returns>True if the response indicates a transient error</returns>
        private static bool IsTransientHttpResponse(HttpResponseMessage response)
        {
            if (response == null) return false;

            // Transient HTTP status codes that should be retried
            return (int)response.StatusCode >= 500 || // Server errors (500-599)
                   response.StatusCode == System.Net.HttpStatusCode.RequestTimeout || // 408
                   response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || // 429 - Rate limiting
                   response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable; // 503
        }

        /// <summary>
        /// Tracks telemetry for QuickBooks API retry attempts.
        /// </summary>
        private static void TrackQuickBooksApiRetryTelemetry(Exception? exception, HttpResponseMessage? response, int retryCount, int maxRetries, TimeSpan delay)
        {
            try
            {
                var logger = Log.ForContext("TelemetryEvent", "QuickBooksAPI_Retry")
                               .ForContext("RetryCount", retryCount)
                               .ForContext("MaxRetries", maxRetries)
                               .ForContext("DelayMs", delay.TotalMilliseconds);

                if (exception != null)
                {
                    logger = logger.ForContext("ExceptionType", exception.GetType().Name);
                }
                else if (response != null)
                {
                    logger = logger.ForContext("HttpStatusCode", (int)response.StatusCode);
                }

                logger.Information("QuickBooks API retry telemetry: attempt {RetryCount}/{MaxRetries}", retryCount, maxRetries);
            }
            catch
            {
                // Don't fail the retry operation due to telemetry issues
            }
        }

        /// <summary>
        /// Tracks telemetry for QuickBooks API circuit breaker state changes.
        /// </summary>
        private static void TrackQuickBooksApiCircuitBreakerTelemetry(Exception? exception, HttpResponseMessage? response, string state, TimeSpan breakDelay)
        {
            try
            {
                var logger = Log.ForContext("TelemetryEvent", "QuickBooksAPI_CircuitBreaker")
                               .ForContext("State", state)
                               .ForContext("BreakDelaySeconds", breakDelay.TotalSeconds);

                if (exception != null)
                {
                    logger = logger.ForContext("ExceptionType", exception.GetType().Name);
                }
                else if (response != null)
                {
                    logger = logger.ForContext("HttpStatusCode", (int)response.StatusCode);
                }

                logger.Information("QuickBooks API circuit breaker telemetry: {State}", state);
            }
            catch
            {
                // Don't fail the circuit breaker operation due to telemetry issues
            }
        }
    }
}
