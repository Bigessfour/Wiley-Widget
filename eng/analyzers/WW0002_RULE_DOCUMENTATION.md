# Roslyn Analyzer Rule WW0002: MemoryCacheEntryOptions Size Property

## Rule Summary

**Rule ID:** WW0002
**Title:** MemoryCacheEntryOptions missing required Size property
**Severity:** Warning
**Category:** Caching
**Enabled by Default:** Yes

## Description

This analyzer detects when `MemoryCacheEntryOptions` objects are created without explicitly setting the `Size` property. The `Size` property is critical when the underlying `MemoryCache` is configured with a `SizeLimit`.

### Why This Matters

Per Microsoft documentation:

> "An entry won't be cached if the sum of the cached entry sizes exceeds the value specified by `SizeLimit`. If no cache size limit is set, the cache size set on the entry is ignored."

[Source: Microsoft Learn - Use SetSize, Size, and SizeLimit to limit cache size](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory#use-setsize-size-and-sizelimit-to-limit-cache-size)

In WileyWidget, the `MemoryCache` is registered as a singleton with `SizeLimit = 1024`:

```csharp
// From DependencyInjection.cs
services.AddSingleton<IMemoryCache>(sp =>
{
    var options = new MemoryCacheOptions
    {
        SizeLimit = 1024  // <-- SizeLimit is configured
    };
    return new MemoryCache(options);
});
```

When `SizeLimit` is configured, **every** `MemoryCacheEntryOptions` object must explicitly set `Size`. Otherwise:

- The entry may be silently rejected from cache
- Memory usage may not be correctly accounted for eviction
- Unexpected `InvalidOperationException` may occur

## Examples

### ❌ VIOLATION: Missing Size Property

```csharp
// Example 1: Default initialization (no Size)
var options = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};
cache.Set(key, value, options);  // WW0002: Missing Size

// Example 2: No initializer at all
var options = new MemoryCacheEntryOptions();
cache.Set(key, value, options);  // WW0002: Missing Size

// Example 3: Partial initialization (no Size)
var options = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
    SlidingExpiration = TimeSpan.FromMinutes(15),
    Priority = CacheItemPriority.Normal
    // Size is missing!
};
cache.Set(key, value, options);  // WW0002: Missing Size
```

### ✅ COMPLIANT: Explicit Size Property

```csharp
// Example 1: Explicitly set Size
var options = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
    Size = 1  // <-- Explicit Size
};
cache.Set(key, value, options);  // ✓ OK

// Example 2: Size with multiple properties
var options = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
    SlidingExpiration = TimeSpan.FromMinutes(15),
    Priority = CacheItemPriority.Normal,
    Size = 1  // <-- Explicit Size
};
cache.Set(key, value, options);  // ✓ OK

// Example 3: Dynamic Size based on content
var options = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
    Size = value.EstimatedSizeInUnits  // <-- Explicit Size (calculated)
};
cache.Set(key, value, options);  // ✓ OK

// Example 4: Use MemoryCacheService helper (recommended)
// The MemoryCacheService.MapOptions() method automatically sets Size = 1 if not specified
var cacheOptions = new CacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
    // Size = null -> defaults to 1 in MapOptions
};
_cacheService.Set(key, value, cacheOptions);  // ✓ OK - MemoryCacheService handles it
```

## Recommended Patterns

### Pattern 1: Default Size (Most Common)

```csharp
var options = new MemoryCacheEntryOptions
{
    Size = 1,  // Default: 1 unit per entry
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};
cache.Set(key, value, options);
```

### Pattern 2: Size Based on Content Type

```csharp
var options = new MemoryCacheEntryOptions
{
    Size = value is DataSet dataSet ? dataSet.Tables.Count : 1,
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};
cache.Set(key, value, options);
```

### Pattern 3: Using MemoryCacheService (Recommended)

WileyWidget provides `MemoryCacheService` which automatically handles Size:

```csharp
private readonly MemoryCacheService _cacheService;

public async Task<T> GetAsync<T>(string key, Func<Task<T>> factory)
{
    var cacheOptions = new CacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        Size = null  // MemoryCacheService will default to 1
    };

    return await _cacheService.GetOrSetAsync(key, factory, cacheOptions);
}
```

The `MemoryCacheService.MapOptions()` method ensures Size is always set:

```csharp
private static MemoryCacheEntryOptions MapOptions(CacheEntryOptions? options)
{
    var mem = new MemoryCacheEntryOptions();

    if (options != null)
    {
        // ... set expiration, priority, etc...

        // CRITICAL: Always set Size when cache has SizeLimit configured
        // Default to 1 unit if not specified
        mem.Size = options.Size ?? 1;  // <-- Size is always set
    }

    return mem;
}
```

## How to Suppress the Warning

If you have a legitimate use case where Size should not be set, use a suppression:

```csharp
#pragma warning disable WW0002
var options = new MemoryCacheEntryOptions();
#pragma warning restore WW0002
```

Or in `.editorconfig`:

```editorconfig
# Disable WW0002 in specific files
[src/MySpecialFile.cs]
dotnet_diagnostic.WW0002.severity = none
```

## Implementation Details

### What Triggers the Rule

The analyzer flags:

1. `new MemoryCacheEntryOptions()` with no initializer
2. `new MemoryCacheEntryOptions { ... }` where no `Size` property is set in the initializer

### What the Rule Checks

- ✓ Detects object creations of type `MemoryCacheEntryOptions`
- ✓ Scans initializers for the `Size` property assignment
- ✓ Reports warning if `Size` is missing

### What the Rule Does NOT Check

- ✗ Does not verify if SizeLimit is actually configured (conservative approach - always require Size)
- ✗ Does not validate the Size value itself (could be 0, negative, etc.)
- ✗ Does not check `cache.Set()` calls with `cacheEntryOptions` parameters (catches the object creation instead)

## References

- [Microsoft: Memory Caching in ASP.NET Core - Use SetSize, Size, and SizeLimit](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory#use-setsize-size-and-sizelimit-to-limit-cache-size)
- [MemoryCacheEntryOptions Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycacheentryoptions?view=net-10.0)
- [MemoryCache SizeLimit Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.memory.memorycacheoptions?view=net-10.0)

## Files

- **Analyzer:** [eng/analyzers/MemoryCacheSizeRequiredAnalyzer.cs](./MemoryCacheSizeRequiredAnalyzer.cs)
- **Resources:** [eng/analyzers/AnalyzerResources.cs](./AnalyzerResources.cs)
- **Release Notes:** [AnalyzerReleases.Unshipped.md](../../../AnalyzerReleases.Unshipped.md)
- **EditorConfig:** [.editorconfig](../../../.editorconfig)
