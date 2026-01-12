# Cache Implementation Application Guide

**How Microsoft Documentation Informed Each Fix**

---

## Fix 1: Adding SizeLimit to Singleton Cache Registration

### Microsoft Documentation Source

**URL:** https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-setsize-size-and-sizelimit-to-limit-cache-size

**Exact Quote:**

> "If SizeLimit isn't set, the cache grows without bound. The ASP.NET Core runtime doesn't trim the cache when system memory is low. Apps must be architected to:
>
> 1. Limit cache growth.
> 2. Call Compact or Remove when available memory is limited."

**Problem Identified:**

- WileyWidget currently has no SizeLimit (grows without bound)
- Memory pressure not managed by runtime

**Solution - Before:**

```csharp
// src/WileyWidget.WinForms/Configuration/DependencyInjection.cs (lines 121-132)
services.AddSingleton<IMemoryCache>(sp =>
{
    var options = new MemoryCacheOptions();
    return new MemoryCache(options);
});
```

**Solution - After:**

```csharp
services.AddSingleton<IMemoryCache>(sp =>
{
    var options = new MemoryCacheOptions
    {
        // Microsoft: "The units are arbitrary"
        // 1024 allows ~200-300 typical entries (1-5 units each)
        SizeLimit = 1024
    };
    return new MemoryCache(options);
});
```

**How Documentation Drove This:**
The Microsoft example shows:

```csharp
public class MyMemoryCache
{
    public MemoryCache Cache { get; private set; }
    public MyMemoryCache()
    {
        Cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1024  // <- This example directly shows the pattern
        });
    }
}
```

We applied this exact pattern to our Singleton registration.

---

## Fix 2: Implement Combined Expiration Strategy

### Microsoft Documentation Source

**URL:** https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-imemorycache

**Exact Quote (with examples):**

> "A cached item set with only a sliding expiration is at risk of never expiring. If the cached item is repeatedly accessed within the sliding expiration interval, the item never expires. Combine a sliding expiration with an absolute expiration to guarantee the item expires."

**And:**

> "The following code gets or creates a cached item with both sliding and absolute expiration:
>
> ```csharp
> var cachedValue = _memoryCache.GetOrCreate(
>     CacheKeys.CallbackEntry,
>     cacheEntry =>
>     {
>         cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(3);
>         cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20);
>         return DateTime.Now;
>     });
> ```
>
> The preceding code guarantees the data won't be cached longer than the absolute time."

**Problem Identified:**

- Current usage: `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)` ✅ Safe
- Missing: Sliding pattern for frequently-used data

**Solution Applied:**

```csharp
// Before (safe but not optimized):
var cacheEntryOptions = new MemoryCacheEntryOptions()
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
    Size = 1
};

// After (Microsoft recommended combined pattern):
var cacheEntryOptions = new MemoryCacheEntryOptions()
{
    // Sliding: Reset on each access (max 5 min if frequently accessed)
    SlidingExpiration = TimeSpan.FromMinutes(5),
    // Absolute: Hard cap of 30 min even if continuously accessed
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
    // Size: Required when SizeLimit is set (per Microsoft)
    Size = 5,
    // Priority: Keep this longer (less likely to evict)
    Priority = CacheItemPriority.High
};
```

**Why Each Property:**

1. **SlidingExpiration** - From Microsoft: "Keep in cache for this time, reset time if accessed"
2. **AbsoluteExpirationRelativeToNow** - From Microsoft: "Guarantees the data won't be cached longer than the absolute time"
3. **Size** - From Microsoft: "If the cache size limit is set, all entries must specify size"
4. **Priority** - From Microsoft: "Items by priority. Lowest priority items are removed first"

---

## Fix 3: Ensure All Entries Have Size When SizeLimit is Set

### Microsoft Documentation Source

**URL:** https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-setsize-size-and-sizelimit-to-limit-cache-size

**Exact Quote:**

> "If the cache size limit is set, all entries must specify size. The ASP.NET Core runtime doesn't limit cache size based on memory pressure. It's up to the developer to limit cache size."

**And:**

> "An entry won't be cached if the sum of the cached entry sizes exceeds the value specified by SizeLimit. If no cache size limit is set, the cache size set on the entry is ignored."

**Problem Identified:**

- Current MapOptions already sets Size=1 ✅
- But documentation emphasizes this MUST be done for ALL entries when parent has SizeLimit

**Solution Applied:**

```csharp
// src/WileyWidget.Services/MemoryCacheService.cs (MapOptions method)

private static MemoryCacheEntryOptions MapOptions(CacheEntryOptions? options)
{
    var mem = new MemoryCacheEntryOptions();

    if (options != null)
    {
        // All properties mapped from CacheEntryOptions...

        // CRITICAL: Size must be specified when parent SizeLimit is set
        // Per Microsoft: "all entries must specify size"
        mem.Size = options.Size ?? 1;  // <- Default to 1 if not specified
    }
    else
    {
        // No options: safe defaults
        mem.Size = 1;  // <- Always set Size
        mem.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
    }

    return mem;
}
```

**Reasoning:**
Microsoft's example code shows:

```csharp
var cacheEntryOptions = new MemoryCacheEntryOptions()
    .SetSize(1);  // <- Always set Size when using SizeLimit

_myMemoryCache.Cache.Set(CacheKeys.Entry, cacheValue, cacheEntryOptions);
```

We follow this by defaulting Size to 1 if caller doesn't specify.

---

## Fix 4: Use GetOrCreate Pattern (Already Implemented ✅)

### Microsoft Documentation Source

**URL:** https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#use-imemorycache

**Exact Quote:**

> "GetOrCreate, GetOrCreateAsync, and Get are extension methods in the CacheExtensions class. These methods extend the capability of IMemoryCache."

**And Microsoft's recommended example:**

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

**WileyWidget Implementation (ALREADY CORRECT):**

```csharp
// src/WileyWidget.Services/MemoryCacheService.cs
public async Task<T> GetOrCreateAsync<T>(
    string key,
    Func<Task<T>> factory,
    CacheEntryOptions? options = null) where T : class
{
    // Check cache (hit)
    if (_memoryCache.TryGetValue(key, out var existing) && existing is T existingT)
    {
        return existingT;
    }

    // Compute via factory (miss)
    var value = await factory();
    if (value != null)
    {
        var memOptions = MapOptions(options);
        _memoryCache.Set(key, value, memOptions);  // Cache it
    }

    return value;
}
```

**How This Matches Microsoft:**

1. ✅ Uses TryGetValue to check cache
2. ✅ Calls factory if miss
3. ✅ Sets in cache with options
4. ✅ Returns value
5. ✅ Supports configurable expiration via options

---

## Fix 5: Implement PostEvictionCallback for Monitoring

### Microsoft Documentation Source

**URL:** https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#memorycacheentryoptions

**Exact Quote:**

> "Sets a PostEvictionDelegate that gets called after the entry is evicted from the cache. The callback is run on a different thread from the code that removes the item from the cache."

**Microsoft Example:**

```csharp
public IActionResult CreateCallbackEntry()
{
    var cacheEntryOptions = new MemoryCacheEntryOptions()
        .SetPriority(CacheItemPriority.NeverRemove)
        .RegisterPostEvictionCallback(callback: EvictionCallback, state: this);

    _cache.Set(CacheKeys.CallbackEntry, DateTime.Now, cacheEntryOptions);

    return RedirectToAction("GetCallbackEntry");
}

private static void EvictionCallback(object key, object value,
    EvictionReason reason, object state)
{
    var message = $"Entry was evicted. Reason: {reason}.";
    ((HomeController)state)._cache.Set(CacheKeys.CallbackMessage, message);
}
```

**WileyWidget Application:**

```csharp
// src/WileyWidget.Data/EnterpriseRepository.cs
var enterprises = await _cacheService.GetOrCreateAsync(
    cacheKey,
    async () => await FetchEnterprisesFromDbAsync(),
    new CacheEntryOptions
    {
        // ...expiration options...

        // Notification when evicted (Microsoft pattern)
        PostEvictionCallback = (key, value, reason) =>
        {
            _logger.LogInformation(
                "Enterprise cache evicted: key={Key}, reason={Reason}",
                key, reason);
        }
    });
```

**Why This Matters (per Microsoft):**

> "The callback is run on a different thread from the code that removes the item from the cache"

This allows async cleanup or re-warming of cache without blocking the main request.

---

## Fix 6: Implement Cache Invalidation via CancellationChangeToken

### Microsoft Documentation Source

**URL:** https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0#cache-dependencies

**Exact Quote:**

> "A CancellationChangeToken is added to the cached item. When Cancel is called on the CancellationTokenSource, both cache entries are evicted... Using a CancellationTokenSource allows multiple cache entries to be evicted as a group."

**Microsoft Example:**

```csharp
public IActionResult CreateDependentEntries()
{
    var cts = new CancellationTokenSource();
    _cache.Set(CacheKeys.DependentCTS, cts);

    using (var entry = _cache.CreateEntry(CacheKeys.Parent))
    {
        entry.Value = DateTime.Now;
        entry.RegisterPostEvictionCallback(DependentEvictionCallback, this);

        _cache.Set(
            CacheKeys.Child,
            DateTime.Now,
            new CancellationChangeToken(cts.Token));
    }

    return RedirectToAction("GetDependentEntries");
}

public IActionResult RemoveChildEntry()
{
    var cancellationTokenSource = _cache.Get<CancellationTokenSource>(
        CacheKeys.DependentCTS);

    cancellationTokenSource.Cancel();  // <- Evicts all related entries
    return RedirectToAction("GetDependentEntries");
}
```

**WileyWidget Application (Proposed):**

```csharp
// Invalidate all "enterprise:*" entries when data changes
public async Task CreateEnterpriseAsync(Enterprise enterprise)
{
    // Persist to DB
    await _dbContext.Enterprises.AddAsync(enterprise);
    await _dbContext.SaveChangesAsync();

    // Invalidate related caches (Microsoft pattern)
    _cacheInvalidationService.InvalidateTag("enterprises");
}
```

**Implementation Reference:**
See [CacheInvalidationService in CACHING_AUDIT_AND_IMPLEMENTATION.md](CACHING_AUDIT_AND_IMPLEMENTATION.md#51-tag-based-invalidation-advanced)

---

## Summary: Documentation → Implementation Chain

| Microsoft Documented Pattern                  | WileyWidget Application                   | File Location                          |
| --------------------------------------------- | ----------------------------------------- | -------------------------------------- |
| Singleton IMemoryCache registration           | `services.AddSingleton<IMemoryCache>`     | DependencyInjection.cs L121            |
| SizeLimit to prevent unbounded growth         | `SizeLimit = 1024`                        | DependencyInjection.cs (proposed)      |
| All entries must have Size when SizeLimit set | `Size = options.Size ?? 1`                | MemoryCacheService.cs L148             |
| GetOrCreate pattern (recommended)             | `GetOrCreateAsync<T>`                     | MemoryCacheService.cs L65              |
| Combined expiration (Sliding + Absolute)      | Both set in CacheEntryOptions             | MemoryCacheService.cs, repositories    |
| PostEvictionCallback for monitoring           | `PostEvictionCallback = (k,v,r) => {...}` | Repositories (proposed enhancement)    |
| CancellationChangeToken for invalidation      | `CacheInvalidationService`                | CacheInvalidationService.cs (proposed) |
| TryGetValue + Set basic pattern               | `GetAsync<T>`                             | MemoryCacheService.cs L39              |
| Code must have fallback on cache miss         | All repositories check DB if cache miss   | Data/\*.Repository.cs                  |

---

**Key Takeaway:**

Every implementation in WileyWidget that differs from or enhances the baseline can be traced directly to a specific Microsoft documentation quote or example code snippet. This ensures maximum compliance with Microsoft's tested, supported patterns and best practices.
