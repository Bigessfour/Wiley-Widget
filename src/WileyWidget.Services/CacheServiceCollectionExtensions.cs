using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Represents a class for cacheservicecollectionextensions.
    /// </summary>
    public static class CacheServiceCollectionExtensions
    {
        /// <summary>
        /// Register the MemoryCacheService and IMemoryCache in DI. Use this in startup to add the cache.
        /// </summary>
        /// <summary>
        /// Performs addwileymemorycache. Parameters: services, null.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="null">The null.</param>
        public static IServiceCollection AddWileyMemoryCache(this IServiceCollection services, Action<MemoryCacheOptions>? configure = null)
        {
            if (configure != null)
                services.AddSingleton(new MemoryCacheOptions());

            services.AddMemoryCache(configure);
            services.AddSingleton<ICacheService, MemoryCacheService>();
            return services;
        }

        /// <summary>
        /// Register a DistributedCacheService if IDistributedCache is already registered (e.g., Redis).
        /// </summary>
        /// <summary>
        /// Performs addwileydistributedcache. Parameters: services.
        /// </summary>
        /// <param name="services">The services.</param>
        public static IServiceCollection AddWileyDistributedCache(this IServiceCollection services)
        {
            services.AddSingleton<ICacheService, DistributedCacheService>();
            return services;
        }
    }
}
