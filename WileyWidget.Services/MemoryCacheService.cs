using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Production-ready IMemoryCache wrapper that implements ICacheService.
    /// - Supports GetAsync, GetOrCreateAsync, SetAsync (TTL and options), RemoveAsync, ExistsAsync
    /// - Uses System.Text.Json for deep cloning when necessary
    /// - Maps CacheEntryOptions to MemoryCacheEntryOptions
    /// </summary>
    public class MemoryCacheService : ICacheService, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheService>? _logger;

        public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService>? logger = null)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult<T?>(null);

            if (_memoryCache.TryGetValue(key, out var obj) && obj is T typed)
            {
                _logger?.LogDebug("MemoryCacheService: GET hit for key {Key}", key);
                // Deep clone to avoid returning a mutable reference
                var cloned = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(typed));
                return Task.FromResult(cloned);
            }

            _logger?.LogDebug("MemoryCacheService: GET miss for key {Key}", key);

            return Task.FromResult<T?>(null);
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null) where T : class
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            if (_memoryCache.TryGetValue(key, out var existing) && existing is T existingT)
            {
                _logger?.LogDebug("MemoryCacheService: GetOrCreate - returning cached value for {Key}", key);
                return existingT;
            }

            var value = await factory();
            if (value != null)
            {
                var memOptions = MapOptions(options);
                _memoryCache.Set(key, value, memOptions);
                _logger?.LogDebug("MemoryCacheService: GetOrCreate - cached new value for {Key}", key);
            }

            return value;
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
        {
            var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
            return SetAsync(key, value, options);
        }

        public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class
        {
            if (string.IsNullOrEmpty(key)) return Task.CompletedTask;
            if (value == null) return Task.CompletedTask;

            var memOptions = MapOptions(options);
            _memoryCache.Set(key, value, memOptions);
            _logger?.LogDebug("MemoryCacheService: SET key {Key} (TTL={Ttl})", key, options?.AbsoluteExpirationRelativeToNow);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.CompletedTask;
            _memoryCache.Remove(key);
            _logger?.LogDebug("MemoryCacheService: REMOVE key {Key}", key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult(false);
            var exists = _memoryCache.TryGetValue(key, out _);
            _logger?.LogDebug("MemoryCacheService: EXISTS key {Key} => {Exists}", key, exists);
            return Task.FromResult(exists);
        }

        public Task ClearAllAsync()
        {
            try
            {
                // Try to leverage concrete MemoryCache implementation if available.
                if (_memoryCache is MemoryCache concrete)
                {
                    concrete.Compact(1.0);
                    _logger?.LogInformation("MemoryCacheService: Cleared all entries via MemoryCache.Compact(1.0)");
                    return Task.CompletedTask;
                }

                // If we don't have the concrete type, fall back to a no-op but log a warning.
                _logger?.LogWarning("MemoryCacheService: IMemoryCache is not MemoryCache; ClearAllAsync is a no-op.");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MemoryCacheService: Failed to ClearAllAsync");
                throw;
            }
        }

        private static MemoryCacheEntryOptions MapOptions(CacheEntryOptions? options)
        {
            var mem = new MemoryCacheEntryOptions();
            if (options != null)
            {
                if (options.AbsoluteExpirationRelativeToNow.HasValue)
                    mem.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;

                if (options.SlidingExpiration.HasValue)
                    mem.SlidingExpiration = options.SlidingExpiration;

                if (options.Size.HasValue)
                    mem.Size = options.Size.Value;
            }

            return mem;
        }

        protected virtual void Dispose(bool disposing)
        {
            // IMemoryCache is typically registered as Singleton and disposed by the DI container.
            // We don't own it, so don't dispose it here. Implement the dispose pattern to
            // allow derived types to override if needed.
            if (disposing)
            {
                // no-op
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
