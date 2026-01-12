using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    public static class CacheServiceCollectionExtensions
    {
        /// <summary>
        /// Register the MemoryCacheService and IMemoryCache in DI. Use this in startup to add the cache.
        /// </summary>
        public static IServiceCollection AddWileyMemoryCache(this IServiceCollection services, Action<MemoryCacheOptions>? configure = null)
        {
            // CRITICAL: Do NOT call services.AddMemoryCache() here. It registers IMemoryCache with default lifetime
            // which conflicts with the singleton registration in DependencyInjection.cs (line 121-132).
            // Per Microsoft docs (https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory):
            // "Create a cache singleton for caching" - prevents premature disposal during DI scope cleanup.
            // Multiple registrations of same type cause last-wins behavior, breaking the singleton contract.

            if (configure != null)
                services.AddSingleton(new MemoryCacheOptions());

            // Register only the cache service wrapper, not the underlying IMemoryCache
            // The wrapper will receive the singleton IMemoryCache from DependencyInjection.cs
            services.AddSingleton<ICacheService, MemoryCacheService>();
            return services;
        }

        /// <summary>
        /// Register a DistributedCacheService if IDistributedCache is already registered (e.g., Redis).
        /// </summary>
        public static IServiceCollection AddWileyDistributedCache(this IServiceCollection services)
        {
            services.AddSingleton<ICacheService, DistributedCacheService>();
            return services;
        }
    }
}
