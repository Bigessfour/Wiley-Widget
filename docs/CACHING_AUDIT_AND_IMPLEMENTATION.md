# WileyWidget Caching Audit & Full Implementation Guide

**Date:** January 10, 2026
**Status:** Complete Caching Review Against Microsoft Documentation
**Scope:** IMemoryCache, IDistributedCache, expiration patterns, sizing strategy, monitoring

---

## Executive Summary

WileyWidget has a **robust in-memory caching foundation** that largely adheres to Microsoft's documented patterns, but several enhancements are recommended per official documentation to achieve production-ready caching with proper sizing, expiration strategies, and monitoring.

**Key Findings:**

- ✅ **Singleton IMemoryCache registration:** Correctly configured per Microsoft docs
- ✅ **Abstraction layer (ICacheService):** Proper wrapper pattern
- ✅ **GetOrCreate pattern:** Implements Microsoft-recommended pattern
- ⚠️ **Cache sizing:** Not fully enforced (SizeLimit configured but not consistently applied)
- ⚠️ **Expiration strategy:** Mix of sliding and absolute (needs unified approach)
- ⚠️ **Distributed caching:** Not enabled (needed for multi-server deployment)
- ⚠️ **Cache monitoring:** No metrics/logging of cache efficiency

---

## Part 1: Microsoft Documentation Reference

### 1.1 IMemoryCache Design Principles

**Source:** [Cache in-memory in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0)

**Key Principles from Microsoft:**

> "Code should always have a fallback option to fetch data and **not** depend on a cached value being available."

**WileyWidget Compliance:** ✅ All repositories check cache first, then query DB if miss

- Example: [EnterpriseRepository.GetAllAsync()](src/WileyWidget.Data/EnterpriseRepository.cs#L60-L85)

> "The cache uses a scarce resource, memory. Limit cache growth via expirations and size limits."

**WileyWidget Compliance:** ⚠️ Partial - Has expiration, but needs SizeLimit enforcement

- Current: `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)` ✅
- Missing: Consistent `Size` specification on all entries

> "Use SetSize, Size, and SizeLimit to limit cache size."

**Reference:**

```csharp
// Microsoft Example - https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-setsize-size-and-sizelimit-to-limit-cache-size
if (!_memoryCache.TryGetValue(CacheKeys.Entry, out DateTime cacheValue))
{
    var cacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSize(1);  // <- Mandatory when SizeLimit is set

    _memoryCache.Set(CacheKeys.Entry, cacheValue, cacheEntryOptions);
}
```

### 1.2 Singleton Pattern for Shared Caches

**Source:** [Dependency injection service lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)

**Microsoft Guidance:**

> "When using SetSize, Size, or SizeLimit to limit cache, create a cache singleton for caching. A shared cache is one shared by other frameworks or libraries."

**WileyWidget Implementation:** ✅ CORRECT

```csharp
// File: DependencyInjection.cs (lines 121-132)
services.AddSingleton<IMemoryCache>(sp =>
{
    var options = new MemoryCacheOptions();
    return new MemoryCache(options);
});
```

**Why This Matters:**

- Per Microsoft: "Singleton lifetime services must be thread safe"
- WileyWidget uses `IMemoryCache` internally, which is thread-safe ✅
- If registered as Scoped/Transient, would dispose during request cleanup ❌

### 1.3 Expiration Patterns

**Source:** [Microsoft Caching Documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-imemorycache)

**Three Pattern Options:**

#### Pattern 1: Sliding Expiration Only

```csharp
// Risk: Item never expires if repeatedly accessed
cacheEntryOptions.SlidingExpiration = TimeSpan.FromSeconds(3);
```

**Recommendation:** ❌ Don't use alone

#### Pattern 2: Absolute Expiration Only

```csharp
// Safe: Item always expires at fixed time
cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
```

**Recommendation:** ✅ Good for data with known stale threshold

#### Pattern 3: Combined (RECOMMENDED)

```csharp
// Best: Absolute cap + sliding refresh
cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(3);
cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20);
// Item expires after 20s absolute OR 3s of inactivity (whichever first)
```

**Reference:**

> "The preceding code guarantees the data won't be cached longer than the absolute time."
> — Microsoft Docs

**WileyWidget Current Usage:**

- EnterpriseRepository: `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)` ✅ Safe
- MemoryCacheService MapOptions: Default `TimeSpan.FromHours(1)` (line 154)

**Recommendation:** Implement Pattern 3 for high-traffic caches

---

## Part 2: Current Implementation Review

### 2.1 Singleton Registration Analysis

**File:** [src/WileyWidget.WinForms/Configuration/DependencyInjection.cs](src/WileyWidget.WinForms/Configuration/DependencyInjection.cs#L121-L132)

**Current Code:**

```csharp
services.AddSingleton<IMemoryCache>(sp =>
{
    var options = new MemoryCacheOptions();
    return new MemoryCache(options);
});
```

**Assessment:** ✅ **COMPLIANT WITH MICROSOFT DOCS**

**Evidence:**

- Singleton lifetime: Per Microsoft, required for shared caches
- No SizeLimit: Allows app-controlled expiration (valid pattern per docs)
- Direct instantiation: Ensures container management and automatic disposal

**Note:** No SizeLimit set here is intentional:

> "If SizeLimit isn't set, the cache grows without bound."
> — Microsoft Docs

WileyWidget relies on entry-level expirations, not container-level sizing.

### 2.2 Service Wrapper Analysis

**File:** [src/WileyWidget.Services/MemoryCacheService.cs](src/WileyWidget.Services/MemoryCacheService.cs)

**Current Implementation:**

```csharp
public class MemoryCacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache _memoryCache;

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        CacheEntryOptions? options = null) where T : class
    {
        // COMPLIANT: GetOrCreate pattern from Microsoft
        if (_memoryCache.TryGetValue(key, out var existing) && existing is T existingT)
        {
            return existingT;  // Cache hit
        }

        var value = await factory();  // Cache miss - compute value
        if (value != null)
        {
            var memOptions = MapOptions(options);
            _memoryCache.Set(key, value, memOptions);
        }
        return value;
    }
}
```

**Assessment:** ✅ **IMPLEMENTS MICROSOFT PATTERN**

**Reference from Microsoft Docs:**

```csharp
public async Task OnGetCacheGetOrCreateAsync()
{
    var cachedValue = await _memoryCache.GetOrCreateAsync(
        CacheKeys.Entry,
        cacheEntry =>
        {
            cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(3);
            return Task.FromResult(DateTime.Now);
        });
}
```

### 2.3 Cache Sizing Strategy Analysis

**Current State:** ⚠️ **INCOMPLETE**

**File:** [src/WileyWidget.Services/MemoryCacheService.cs#L136-L160](src/WileyWidget.Services/MemoryCacheService.cs#L136-L160)

```csharp
private static MemoryCacheEntryOptions MapOptions(CacheEntryOptions? options)
{
    var mem = new MemoryCacheEntryOptions();

    // Issue: Size defaulting to 1 when no SizeLimit is set
    if (options != null)
    {
        mem.Size = options.Size ?? 1;  // ← Size set even when parent has no SizeLimit
    }
    else
    {
        mem.Size = 1;  // ← Default applied
    }

    return mem;
}
```

**Microsoft Guidance on Sizing:**

> "If the cache size limit is set, all entries must specify size."
> "If no cache size limit is set, the cache size set on the entry is ignored."
> — Microsoft Docs

**Current Architecture:**

- Container (DependencyInjection.cs): No SizeLimit ❌ (allows unbounded growth)
- Entries (MemoryCacheService): All have Size=1 ✅ (prepared for container-level limit)

**Recommendation:** Add container-level SizeLimit to prevent unbounded growth

---

## Part 3: Full Implementation - Production-Ready Caching

### 3.1 Enhanced DependencyInjection Registration

**Microsoft Documentation Applied:**

- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-setsize-size-and-sizelimit-to-limit-cache-size
- https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes

**Implementation:**

```csharp
// FILE: src/WileyWidget.WinForms/Configuration/DependencyInjection.cs (Enhancement)

/// <summary>
/// Register IMemoryCache as Singleton with size limits per Microsoft documentation:
/// "Create a cache singleton for caching"
/// - https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0
///
/// SizeLimit enforces: All entries must specify Size when limit is set.
/// Strategy: 1024 units total (unitless per Microsoft), each entry 1-5 units.
/// Eviction order: Expired → Priority → LRU → AbsoluteExpiration → SlidingExpiration
/// </summary>
private static IServiceCollection AddMemoryCacheWithSizeLimit(this IServiceCollection services)
{
    services.AddSingleton<IMemoryCache>(sp =>
    {
        return new MemoryCache(new MemoryCacheOptions
        {
            // Per Microsoft: "The units are arbitrary"
            // Using 1024 units total allows ~200-300 typical cache entries (1-5 units each)
            SizeLimit = 1024,

            // Future: CompactionPercentage controls how much to evict (default 25%)
            CompactionPercentage = 0.25
        });
    });

    return services;
}
```

**How This Derives from Documentation:**

Microsoft states:

> "The following code creates a unitless fixed size MemoryCache accessible by dependency injection...
> If SizeLimit isn't set, the cache grows without bound."

By setting `SizeLimit = 1024`, we follow the documented pattern and prevent unbounded growth.

### 3.2 Enhanced MemoryCacheService with All Patterns

**Microsoft Patterns Applied:**

- TryGetValue + Set (Basic pattern)
- GetOrCreate (Recommended for cache-aside)
- Combined expiration (Sliding + Absolute)
- PostEvictionCallback (Eviction notifications)
- Priority enforcement (NeverRemove for critical data)

```csharp
// FILE: src/WileyWidget.Services/MemoryCacheService.cs (ENHANCED)

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Production-ready IMemoryCache wrapper implementing Microsoft-documented patterns.
    ///
    /// Patterns Implemented:
    /// 1. TryGetValue + Set (Basic cache-aside) - https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-imemorycache
    /// 2. GetOrCreate (Recommended) - https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.cacheextensions.getorcreate
    /// 3. Combined Expiration (Sliding + Absolute) - See "Use IMemoryCache" section
    /// 4. PostEvictionCallback - Eviction notifications per https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#memorycacheentryoptions
    /// 5. Size enforcement - All entries must specify Size when SizeLimit is set
    ///
    /// Features:
    /// - Deep cloning via JsonSerializer to prevent reference mutations
    /// - Automatic fallback when value is null (not re-cached)
    /// - Configurable expiration: sliding, absolute, or combined
    /// - Priority-based eviction (NeverRemove for critical entries)
    /// - ClearAllAsync support via MemoryCache.Compact(1.0)
    /// </summary>
    public class MemoryCacheService : ICacheService, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger? _logger;

        /// <summary>
        /// Initialize with a singleton IMemoryCache instance.
        /// Per Microsoft: "The container is responsible for cleanup of types it creates"
        /// So we don't dispose _memoryCache here.
        /// </summary>
        public MemoryCacheService(IMemoryCache memoryCache, ILogger? logger = null)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger?.ForContext<MemoryCacheService>();
        }

        /// <summary>
        /// Pattern 1: TryGetValue - Basic cache read with deep clone.
        /// Microsoft: "TryGetValue to check if a time is in the cache"
        /// </summary>
        public Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult<T?>(null);

            try
            {
                if (_memoryCache.TryGetValue(key, out var obj) && obj is T typed)
                {
                    _logger?.Debug("MemoryCacheService: GET hit for key {Key}", key);

                    // Deep clone to prevent external mutations affecting cached state
                    // This is safe even for deserialization failures (returns null gracefully)
                    var cloned = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(typed));
                    return Task.FromResult(cloned);
                }

                _logger?.Debug("MemoryCacheService: GET miss for key {Key}", key);
                return Task.FromResult<T?>(null);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "MemoryCacheService: Error retrieving {Key}", key);
                return Task.FromResult<T?>(null);
            }
        }

        /// <summary>
        /// Pattern 2: GetOrCreate (Recommended by Microsoft)
        /// Microsoft: "GetOrCreate and GetOrCreateAsync are extension methods...
        /// These methods extend the capability of IMemoryCache"
        ///
        /// Flow:
        /// 1. Check cache (hit returns immediately)
        /// 2. Cache miss: Call factory to compute value
        /// 3. Set in cache if value is not null (fail-safe pattern)
        /// 4. Return value
        /// </summary>
        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<Task<T>> factory,
            CacheEntryOptions? options = null) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "Cache key cannot be null or empty");
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory function cannot be null");

            try
            {
                // Cache hit path
                if (_memoryCache.TryGetValue(key, out var existing) && existing is T existingT)
                {
                    _logger?.Debug("MemoryCacheService: GetOrCreate - cache hit for {Key}", key);
                    return existingT;
                }

                // Cache miss path: compute via factory
                _logger?.Debug("MemoryCacheService: GetOrCreate - cache miss, invoking factory for {Key}", key);
                var value = await factory();

                // Only cache non-null values (fail-safe: if factory returns null, don't pollute cache)
                if (value != null)
                {
                    var memOptions = MapOptions(options);
                    _memoryCache.Set(key, value, memOptions);
                    _logger?.Debug("MemoryCacheService: GetOrCreate - cached new value for {Key}", key);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "MemoryCacheService: Error in GetOrCreateAsync for {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Set with TimeSpan overload (convenience for simple TTL cases)
        /// </summary>
        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
        {
            var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
            return SetAsync(key, value, options);
        }

        /// <summary>
        /// Set with full CacheEntryOptions
        /// Supports: Absolute, Sliding, or Combined expiration
        /// </summary>
        public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class
        {
            if (string.IsNullOrEmpty(key)) return Task.CompletedTask;

            try
            {
                var memOptions = MapOptions(options);
                _memoryCache.Set(key, value, memOptions);
                _logger?.Debug("MemoryCacheService: SET key {Key} with options {@Options}", key, new
                {
                    memOptions.AbsoluteExpirationRelativeToNow,
                    memOptions.SlidingExpiration,
                    memOptions.Size,
                    memOptions.Priority
                });
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "MemoryCacheService: Error setting {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Remove a cache entry explicitly
        /// </summary>
        public Task RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.CompletedTask;

            try
            {
                _memoryCache.Remove(key);
                _logger?.Debug("MemoryCacheService: REMOVE key {Key}", key);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "MemoryCacheService: Error removing {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Check if key exists without retrieving value
        /// </summary>
        public Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult(false);

            try
            {
                var exists = _memoryCache.TryGetValue(key, out _);
                _logger?.Debug("MemoryCacheService: EXISTS key {Key} => {Exists}", key, exists);
                return Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "MemoryCacheService: Error checking {Key}", key);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Clear all cache entries.
        /// Per Microsoft: "MemoryCache.Compact attempts to remove the specified percentage of the cache"
        /// https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#memorycachecompact
        ///
        /// Usage: _cache.Compact(0.25) removes 25% of entries
        /// We use 1.0 (100%) to clear all
        /// </summary>
        public Task ClearAllAsync()
        {
            try
            {
                if (_memoryCache is MemoryCache concrete)
                {
                    // Compact(1.0) removes 100% of entries
                    concrete.Compact(1.0);
                    _logger?.Information("MemoryCacheService: Cleared all entries via MemoryCache.Compact(1.0)");
                    return Task.CompletedTask;
                }

                // Fallback if we don't have concrete type
                _logger?.Warning("MemoryCacheService: IMemoryCache is not MemoryCache; ClearAllAsync is a no-op.");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "MemoryCacheService: Failed to ClearAllAsync");
                throw;
            }
        }

        /// <summary>
        /// Convert our CacheEntryOptions to MemoryCacheEntryOptions.
        ///
        /// Patterns Implemented:
        /// 1. Absolute expiration: "won't be cached longer than the absolute time"
        /// 2. Sliding expiration: "reset time if accessed"
        /// 3. Combined: "guarantees the data won't be cached longer than the absolute time"
        ///    (and will evict if not accessed within sliding interval)
        ///
        /// Size: CRITICAL when SizeLimit is set on the parent cache
        /// - Per Microsoft: "If the cache size limit is set, all entries must specify size"
        /// - We default to 1 unit if not specified
        /// </summary>
        private static MemoryCacheEntryOptions MapOptions(CacheEntryOptions? options)
        {
            var mem = new MemoryCacheEntryOptions();

            // Defaults when no options provided
            if (options == null)
            {
                // Safe defaults: 1 hour absolute expiration
                mem.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                mem.Size = 1;
                mem.Priority = CacheItemPriority.Normal;
                return mem;
            }

            // Map absolute expiration
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                mem.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;
            }

            // Map sliding expiration
            if (options.SlidingExpiration.HasValue)
            {
                mem.SlidingExpiration = options.SlidingExpiration;
            }

            // Size: CRITICAL
            // Per Microsoft: "all entries must specify size when SizeLimit is set"
            mem.Size = options.Size ?? 1;

            // Priority: for eviction ordering
            mem.Priority = options.Priority ?? CacheItemPriority.Normal;

            // PostEvictionCallback: optional notification when entry evicted
            if (options.PostEvictionCallback != null)
            {
                mem.RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    options.PostEvictionCallback?.Invoke(key?.ToString() ?? "", value, reason.ToString());
                });
            }

            return mem;
        }

        protected virtual void Dispose(bool disposing)
        {
            // Per Microsoft DI documentation:
            // "Services should never be disposed by code that resolved the service from the container"
            // The container disposes the IMemoryCache automatically
            if (disposing)
            {
                // No-op: we don't own the IMemoryCache instance
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
```

### 3.3 Repository Cache Usage Pattern

**Microsoft Documentation Applied:**

- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-imemorycache
- Cache guidelines: "Code should always have a fallback option"

**Enhanced Example:**

```csharp
// FILE: src/WileyWidget.Data/EnterpriseRepository.cs (ENHANCED)

public async Task<IEnumerable<Enterprise>> GetAllAsync()
{
    const string cacheKey = "enterprises:all";

    try
    {
        // Pattern: GetOrCreate - Microsoft recommended
        // Per Docs: "GetOrCreate... extends the capability of IMemoryCache"
        var enterprises = await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () => await FetchEnterprisesFromDbAsync(),
            new CacheEntryOptions
            {
                // Combined expiration pattern (Microsoft recommended)
                // Sliding: Reset on each access (max 5 min if frequently used)
                // Absolute: Hard cap of 30 min even if accessed continuously
                SlidingExpiration = TimeSpan.FromMinutes(5),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),

                // Size: Required by SizeLimit (1024 total units in cache)
                // Each enterprise ~5 units (object graph estimate)
                Size = 5,

                // Priority: Keep this in cache longer (less likely to evict)
                Priority = CacheItemPriority.High,

                // Notification when evicted
                PostEvictionCallback = (key, value, reason) =>
                {
                    _logger.LogInformation(
                        "Enterprise cache evicted: key={Key}, reason={Reason}",
                        key, reason);
                }
            });

        return enterprises ?? Enumerable.Empty<Enterprise>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to retrieve enterprises");
        throw;
    }
}

private async Task<IEnumerable<Enterprise>> FetchEnterprisesFromDbAsync()
{
    // Fallback pattern: Code has fallback option per Microsoft docs
    using var context = await _contextFactory.CreateDbContextAsync();
    return await context.Enterprises
        .Where(e => !e.DeletedAt.HasValue)
        .ToListAsync();
}
```

### 3.4 Distributed Caching Option (Future)

**For multi-server deployments:**

```csharp
// FILE: Example distributed cache configuration (NOT YET IMPLEMENTED)
// Per: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0

// For development:
services.AddDistributedMemoryCache();

// For production (example with Redis):
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisCache");
    options.InstanceName = "WileyWidget:";
});

// Usage remains the same via IDistributedCache interface
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var json = await _cache.GetStringAsync(key);
        return json == null ? null : JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(
        string key, T value,
        DistributedCacheEntryOptions? options = null) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, json, options ?? new());
    }
}
```

---

## Part 4: Cache Monitoring & Metrics

### 4.1 Logging Integration

**File:** [src/WileyWidget.Services/MemoryCacheService.cs](src/WileyWidget.Services/MemoryCacheService.cs#L1-50)

**Current State:** ✅ **EXCELLENT**

- Uses Serilog structured logging
- Logs cache hit/miss at DEBUG level
- Logs all operations for troubleshooting

**Enhanced Metrics Example:**

```csharp
// FILE: src/WileyWidget.Services/CacheMetricsService.cs (NEW)

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace WileyWidget.Services
{
    /// <summary>
    /// Tracks cache performance metrics per Microsoft caching best practices.
    /// Provides hit rate, miss rate, and eviction statistics.
    /// </summary>
    public class CacheMetricsService
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, CacheStats> _stats = new();
        private readonly object _lock = new();

        public CacheMetricsService(ILogger logger)
        {
            _logger = logger?.ForContext<CacheMetricsService>();
        }

        public void RecordHit(string key)
        {
            lock (_lock)
            {
                if (!_stats.ContainsKey(key))
                    _stats[key] = new CacheStats(key);

                _stats[key].Hits++;
            }
        }

        public void RecordMiss(string key)
        {
            lock (_lock)
            {
                if (!_stats.ContainsKey(key))
                    _stats[key] = new CacheStats(key);

                _stats[key].Misses++;
            }
        }

        public void RecordEviction(string key, string reason)
        {
            lock (_lock)
            {
                if (_stats.ContainsKey(key))
                {
                    _stats[key].Evictions++;
                    _stats[key].LastEvictionReason = reason;
                }
            }
        }

        public CacheMetrics GetMetrics()
        {
            lock (_lock)
            {
                var totalHits = _stats.Values.Sum(s => s.Hits);
                var totalMisses = _stats.Values.Sum(s => s.Misses);
                var total = totalHits + totalMisses;

                return new CacheMetrics
                {
                    TotalHits = totalHits,
                    TotalMisses = totalMisses,
                    HitRate = total == 0 ? 0 : (double)totalHits / total,
                    TotalEvictions = _stats.Values.Sum(s => s.Evictions),
                    KeyCount = _stats.Count
                };
            }
        }

        private class CacheStats
        {
            public string Key { get; set; }
            public long Hits { get; set; }
            public long Misses { get; set; }
            public long Evictions { get; set; }
            public string? LastEvictionReason { get; set; }

            public CacheStats(string key) => Key = key;
        }
    }

    public class CacheMetrics
    {
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public double HitRate { get; set; }
        public long TotalEvictions { get; set; }
        public int KeyCount { get; set; }
    }
}
```

---

## Part 5: Cache Invalidation Patterns

### 5.1 Tag-Based Invalidation (Advanced)

**Microsoft Reference:**

> "Use CancellationChangeToken allows multiple cache entries to be evicted as a group"
> — https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#cache-dependencies

**Implementation Example:**

```csharp
// FILE: src/WileyWidget.Services/CacheInvalidationService.cs (NEW)

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace WileyWidget.Services
{
    /// <summary>
    /// Cache invalidation service implementing Microsoft's CancellationChangeToken pattern.
    /// Allows related cache entries to be evicted together when one is invalidated.
    ///
    /// Per Microsoft: "A CancellationChangeToken is added to the cached item.
    /// When Cancel is called on the CancellationTokenSource, both cache entries are evicted"
    ///
    /// Reference: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#cache-dependencies
    /// </summary>
    public class CacheInvalidationService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        private readonly Dictionary<string, CancellationTokenSource> _tokenSources = new();
        private readonly object _lock = new();

        public CacheInvalidationService(IMemoryCache cache, ILogger logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger?.ForContext<CacheInvalidationService>();
        }

        /// <summary>
        /// Create a cache invalidation tag.
        /// All entries added with this tag will be evicted when InvalidateTag is called.
        /// </summary>
        public string CreateTag(string tagName)
        {
            lock (_lock)
            {
                if (_tokenSources.ContainsKey(tagName))
                {
                    _logger?.Debug("Tag '{Tag}' already exists", tagName);
                    return tagName;
                }

                _tokenSources[tagName] = new CancellationTokenSource();
                _logger?.Information("Created cache invalidation tag: {Tag}", tagName);
                return tagName;
            }
        }

        /// <summary>
        /// Register a cache entry with a tag.
        /// When InvalidateTag(tag) is called, this entry will be evicted.
        /// </summary>
        public void RegisterWithTag<T>(
            string key, T value,
            string tag,
            MemoryCacheEntryOptions options)
            where T : class
        {
            lock (_lock)
            {
                if (!_tokenSources.TryGetValue(tag, out var tokenSource))
                {
                    _logger?.Warning("Tag '{Tag}' not found; entry will not be tagged", tag);
                    return;
                }

                // Add expiration token to invalidate when tag is cancelled
                options.AddExpirationToken(
                    new CancellationChangeToken(tokenSource.Token));

                _cache.Set(key, value, options);
                _logger?.Debug("Registered cache entry {Key} with tag {Tag}", key, tag);
            }
        }

        /// <summary>
        /// Invalidate all entries associated with a tag.
        /// </summary>
        public void InvalidateTag(string tag)
        {
            lock (_lock)
            {
                if (!_tokenSources.TryGetValue(tag, out var tokenSource))
                {
                    _logger?.Debug("Tag '{Tag}' not found", tag);
                    return;
                }

                tokenSource.Cancel();
                tokenSource.Dispose();
                _tokenSources.Remove(tag);

                _logger?.Information("Invalidated cache tag: {Tag}", tag);
            }
        }
    }
}
```

**Usage Example:**

```csharp
// In a service:
var invalidationSvc = new CacheInvalidationService(_cache, _logger);

// Create tag group
invalidationSvc.CreateTag("enterprises");

// Register entries with tag
invalidationSvc.RegisterWithTag(
    "enterprises:all",
    enterprises,
    "enterprises",
    new MemoryCacheEntryOptions {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        Size = 5
    });

// Later, when enterprise data changes:
invalidationSvc.InvalidateTag("enterprises");  // Evicts all "enterprises:*" entries
```

---

## Part 6: Summary & Implementation Checklist

### Compliance Status

| Feature                | Status       | Reference                              |
| ---------------------- | ------------ | -------------------------------------- |
| Singleton IMemoryCache | ✅ Compliant | DependencyInjection.cs L121            |
| GetOrCreate Pattern    | ✅ Compliant | MemoryCacheService.GetOrCreateAsync    |
| Expiration Strategy    | ⚠️ Partial   | Need combined pattern on all           |
| Size Enforcement       | ⚠️ Partial   | Entries prepared, need container limit |
| Fallback on Cache Miss | ✅ Compliant | All repositories check DB if miss      |
| Thread Safety          | ✅ Compliant | IMemoryCache is thread-safe            |
| Logging                | ✅ Excellent | Serilog integration throughout         |

### Implementation Tasks

**Tier 1 (Immediate):**

- [ ] Add SizeLimit=1024 to DependencyInjection.cs cache registration
- [ ] Update all repository cache entries to use combined expiration pattern
- [ ] Document cache key naming convention (e.g., "entities:all", "entities:{id}")

**Tier 2 (High Priority):**

- [ ] Implement CacheInvalidationService for tag-based eviction
- [ ] Add CacheMetricsService for monitoring hit/miss rates
- [ ] Create cache invalidation strategy for data mutations (Create/Update/Delete)

**Tier 3 (Future - Scalability):**

- [ ] Implement IDistributedCache for multi-server deployments
- [ ] Add Redis integration for production
- [ ] Implement cache warming strategies for hot data

---

## References

1. **Microsoft Cache in-memory in ASP.NET Core**
   - https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0

2. **Microsoft Distributed Caching**
   - https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0

3. **Microsoft Dependency Injection Service Lifetimes**
   - https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes

4. **MemoryCacheEntryOptions API**
   - https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycacheentryoptions?view=net-10.0

5. **MemoryCache.Compact Method**
   - https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycache.compact?view=net-10.0

---

**Document Version:** 1.0
**Last Updated:** January 10, 2026
**Maintained By:** GitHub Copilot with Microsoft Documentation Reference
