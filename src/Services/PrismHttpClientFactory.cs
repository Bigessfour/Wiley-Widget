using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;

namespace WileyWidget.Services
{
    /// <summary>
    /// Minimal <see cref="IHttpClientFactory"/> implementation that relies on Prism's container
    /// instead of Microsoft.Extensions.DependencyInjection. Each named client is lazily created
    /// on first use and reused for the lifetime of the factory.
    /// </summary>
    public sealed class PrismHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly ConcurrentDictionary<string, Lazy<HttpClient>> _clients;
        private readonly Func<string, HttpClient> _clientBuilder;
        private bool _disposed;

        public PrismHttpClientFactory(Func<string, HttpClient> clientBuilder)
        {
            _clientBuilder = clientBuilder ?? throw new ArgumentNullException(nameof(clientBuilder));
            _clients = new ConcurrentDictionary<string, Lazy<HttpClient>>(StringComparer.OrdinalIgnoreCase);
        }

        public HttpClient CreateClient(string name)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PrismHttpClientFactory));
            }

            var key = string.IsNullOrWhiteSpace(name) ? "Default" : name;
            var lazyClient = _clients.GetOrAdd(key, CreateLazyClient);
            return lazyClient.Value;
        }

        private Lazy<HttpClient> CreateLazyClient(string name)
        {
            return new Lazy<HttpClient>(() =>
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(PrismHttpClientFactory));
                }

                return _clientBuilder(name);
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var lazyClient in _clients.Values)
            {
                if (lazyClient.IsValueCreated)
                {
                    lazyClient.Value.Dispose();
                }
            }

            _clients.Clear();
        }
    }
}
