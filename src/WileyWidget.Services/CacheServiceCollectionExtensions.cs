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
            services.AddMemoryCache(configure ?? (options => { }));
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
