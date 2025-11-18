using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// IDistributedCache wrapper implementing ICacheService using JSON serialization.
    /// Useful when running in scaled environments with a distributed cache (Redis, SQL, etc.).
    /// </summary>
    public class DistributedCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<DistributedCacheService>? _logger;

        public DistributedCacheService(IDistributedCache distributedCache, ILogger<DistributedCacheService>? logger = null)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key)) return null;
            var bytes = await _distributedCache.GetAsync(key);
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                return JsonSerializer.Deserialize<T>(bytes);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to deserialize cache entry for key {Key}", key);
                return null;
            }
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

        public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
        {
            var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
            await SetAsync(key, value, options);
        }

        public async Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class
        {
            if (string.IsNullOrEmpty(key) || value == null) return;

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            var cacheOptions = new DistributedCacheEntryOptions();
            if (options?.AbsoluteExpirationRelativeToNow != null)
                cacheOptions.SetAbsoluteExpiration(options.AbsoluteExpirationRelativeToNow.Value);
            if (options?.SlidingExpiration != null)
                cacheOptions.SetSlidingExpiration(options.SlidingExpiration.Value);

            await _distributedCache.SetAsync(key, bytes, cacheOptions);
        }

        public Task RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.CompletedTask;
            return _distributedCache.RemoveAsync(key);
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var val = await _distributedCache.GetAsync(key);
            return val != null && val.Length > 0;
        }

        public Task ClearAllAsync()
        {
            _logger?.LogWarning("DistributedCacheService: ClearAllAsync called but is not supported for distributed caches in a generic way.");
            throw new NotSupportedException("ClearAllAsync is not supported for DistributedCacheService. Use provider-specific flush or key-prefixing.");
        }
    }
}
