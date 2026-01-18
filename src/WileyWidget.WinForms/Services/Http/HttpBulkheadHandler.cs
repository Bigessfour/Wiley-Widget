using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services.Http
{
    /// <summary>
    /// Application-level bulkhead implementation using SemaphoreSlim to limit concurrent HTTP requests.
    /// Prevents overload by queuing excess requests and releasing them as ongoing requests complete.
    /// </summary>
    public class HttpBulkheadHandler : DelegatingHandler
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger<HttpBulkheadHandler> _logger;
        private readonly string _clientName;

        /// <summary>
        /// Creates a new bulkhead handler.
        /// </summary>
        /// <param name="maxConcurrentRequests">Maximum concurrent requests allowed through this bulkhead</param>
        /// <param name="clientName">Name of the HTTP client for logging (e.g., "GrokClient", "QuickBooksClient")</param>
        /// <param name="logger">Optional logger for diagnostic output</param>
        public HttpBulkheadHandler(int maxConcurrentRequests, string clientName, ILogger<HttpBulkheadHandler>? logger = null)
        {
            _semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
            _clientName = clientName;
            _logger = logger ?? new NullLogger<HttpBulkheadHandler>();
        }

        /// <summary>
        /// Sends an HTTP request through the bulkhead, queuing if necessary.
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var currentCount = _semaphore.CurrentCount;
            if (currentCount <= 0)
            {
                _logger.LogInformation(
                    "[{ClientName}] Bulkhead full - queuing request. Current concurrent: {MaxConcurrent}",
                    _clientName,
                    _semaphore.CurrentCount);
            }

            // Wait for a slot in the bulkhead
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                _logger.LogDebug("[{ClientName}] Request proceeding through bulkhead. Concurrent: {Current}/{Max}",
                    _clientName,
                    _semaphore.CurrentCount,
                    _semaphore.CurrentCount + 1); // +1 for the current request

                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Release the slot for the next request
                _semaphore.Release();
                _logger.LogDebug("[{ClientName}] Request completed, bulkhead slot released", _clientName);
            }
        }

        /// <summary>
        /// Releases the semaphore resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _semaphore?.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Null logger implementation for cases where logging is not needed.
    /// </summary>
    internal class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
