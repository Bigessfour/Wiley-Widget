using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Circuit breaker service to prevent cascading failures when analytics services are unavailable
    /// </summary>
    public interface ICircuitBreakerService
    {
        /// <summary>
        /// Executes an operation with circuit breaker protection
        /// </summary>
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation with circuit breaker protection (fire and forget)
        /// </summary>
        Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current state of the circuit breaker
        /// </summary>
        CircuitState CircuitState { get; }

        /// <summary>
        /// Gets the last exception that caused the circuit to break
        /// </summary>
        Exception? LastException { get; }
    }

    /// <summary>
    /// Circuit breaker implementation using Polly
    /// </summary>
    public class CircuitBreakerService : ICircuitBreakerService
    {
        private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
        private readonly ILogger<CircuitBreakerService> _logger;

        public CircuitBreakerService(ILogger<CircuitBreakerService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _circuitBreaker = Policy
                .Handle<Exception>(ex => !(ex is OperationCanceledException))
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromMinutes(5),
                    onBreak: (ex, breakDelay) =>
                    {
                        _logger.LogWarning(ex, "Circuit breaker opened for {BreakDelay}. Analytics operations will use fallback data.", breakDelay);
                        LastException = ex;
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset. Analytics operations will resume normal operation.");
                        LastException = null;
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open. Testing analytics service availability.");
                    }
                );
        }

        public CircuitState CircuitState => _circuitBreaker.CircuitState;

        public Exception? LastException { get; private set; }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _circuitBreaker.ExecuteAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await operation(cancellationToken);
                });
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open. Analytics operation failed.");
                throw new InvalidOperationException("Analytics service is temporarily unavailable. Using cached data.", ex);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analytics operation failed with exception.");
                throw;
            }
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await operation(cancellationToken);
                });
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open. Analytics operation failed.");
                throw new InvalidOperationException("Analytics service is temporarily unavailable.", ex);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analytics operation failed with exception.");
                throw;
            }
        }
    }
}
