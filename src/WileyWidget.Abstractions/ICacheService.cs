using System;
using System.Threading.Tasks;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Cache entry configuration used by the cache service implementations.
    /// Mirrors common options from Microsoft.Extensions.Caching.Memory but keeps the abstraction free of framework types.
    /// </summary>
    public class CacheEntryOptions
    {
        /// <summary>
        /// Absolute expiration relative to now.
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        /// <summary>
        /// Sliding expiration window.
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Logical size of the cache entry; used by size-limited caches.
        /// </summary>
        public long? Size { get; set; }
    }

    /// <summary>
    /// Simple cache abstraction used by ViewModels to reduce repeated DB hits in E2E and UI flows.
    /// Implementations may wrap IMemoryCache or IDistributedCache.
    /// Designed to be safe for production: supports expirations, GetOrCreate patterns, removal and existence checks.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Get a cached value by key. Returns null if not present.
        /// </summary>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Get or create a cached value. If the key does not exist, the factory will produce the value which will be cached using the provided options.
        /// </summary>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null) where T : class;

        /// <summary>
        /// Set a cached value. Backwards-compatible TTL overload.
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class;

        /// <summary>
        /// Set a cached value with rich options.
        /// </summary>
        Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class;

        /// <summary>
        /// Remove a cached entry by key.
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Determines whether a key exists in the cache.
        /// </summary>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Clear all entries from the cache. Implementations that cannot support a global clear
        /// (for example, some distributed caches) may throw NotSupportedException.
        /// </summary>
        Task ClearAllAsync();
    }
}
