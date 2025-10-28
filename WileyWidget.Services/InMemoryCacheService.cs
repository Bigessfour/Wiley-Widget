using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Lightweight in-memory cache implementation for local development and tests.
    /// Not intended for production-scale distributed caching.
    /// </summary>
    public class InMemoryCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, (object Value, DateTime? Expires)> _store = new();

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            if (key == null) return Task.FromResult<T?>(null);

            if (_store.TryGetValue(key, out var entry))
            {
                if (entry.Expires.HasValue && entry.Expires.Value < DateTime.UtcNow)
                {
                    // expired
                    _store.TryRemove(key, out _);
                    return Task.FromResult<T?>(null);
                }

                return Task.FromResult(entry.Value as T);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
        {
            if (key == null) return Task.CompletedTask;
            DateTime? expires = null;
            if (ttl.HasValue)
            {
                expires = DateTime.UtcNow.Add(ttl.Value);
            }

            _store[key] = (value!, expires);
            return Task.CompletedTask;
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null) where T : class
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var existing = await GetAsync<T>(key);
            if (existing != null) return existing;

            var value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, options);
            }

            return value;
        }

        public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class
        {
            if (key == null) return Task.CompletedTask;

            DateTime? expires = null;
            if (options?.AbsoluteExpirationRelativeToNow.HasValue == true)
            {
                expires = DateTime.UtcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }

            _store[key] = (value!, expires);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.CompletedTask;
            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult(false);
            var exists = _store.TryGetValue(key, out var entry) && (!entry.Expires.HasValue || entry.Expires.Value >= DateTime.UtcNow);
            return Task.FromResult(exists);
        }

        public Task ClearAllAsync()
        {
            _store.Clear();
            return Task.CompletedTask;
        }
    }
}
