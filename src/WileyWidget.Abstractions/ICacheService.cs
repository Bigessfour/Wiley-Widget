using System.Threading;
using System;
using System.Threading.Tasks;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Cache entry configuration used by the cache service implementations.
    /// Mirrors common options from Microsoft.Extensions.Caching.Memory but keeps the abstraction free of framework types.
    /// Per Microsoft: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory
    /// </summary>
    public class CacheEntryOptions
    {
        /// <summary>
        /// Absolute expiration relative to now.
        /// Per Microsoft: "Guarantees the data won't be cached longer than the absolute time"
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        /// <summary>
        /// Sliding expiration window.
        /// Per Microsoft: "Keep in cache for this time, reset time if accessed"
        /// Combine with AbsoluteExpirationRelativeToNow to prevent indefinite caching.
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Logical size of the cache entry; used by size-limited caches.
        /// Per Microsoft: "If the cache size limit is set, all entries must specify size"
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        /// Cache item priority for eviction order (Low, Normal, High, NeverRemove).
        /// Per Microsoft: "Items by priority. Lowest priority items are removed first"
        /// Used in conjunction with SizeLimit to control which entries are evicted.
        /// Default: Normal
        /// </summary>
        public int Priority { get; set; } = 1; // Microsoft.Extensions.Caching.Memory.CacheItemPriority.Normal = 1

        /// <summary>
        /// Callback invoked when this cache entry is evicted.
        /// Per Microsoft: "The callback is run on a different thread from the code that removes the item from the cache"
        /// Signature: (key: string, value: object, reason: int (EvictionReason), state: object)
        /// Use for monitoring, cleanup, or cache re-warming patterns.
        /// </summary>
        public Action<string, object, int>? PostEvictionCallback { get; set; }
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
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a key exists in the cache.
        /// </summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear all entries from the cache. Implementations that cannot support a global clear
        /// (for example, some distributed caches) may throw NotSupportedException.
        /// </summary>
        Task ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
