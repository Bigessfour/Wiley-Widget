# WW0002 Quick Reference

## Rule Information

| Property           | Value                                                                                    |
| ------------------ | ---------------------------------------------------------------------------------------- |
| Rule ID            | **WW0002**                                                                               |
| Title              | MemoryCacheEntryOptions missing required Size property                                   |
| Severity           | Warning                                                                                  |
| Category           | Caching                                                                                  |
| File Location      | [eng/analyzers/MemoryCacheSizeRequiredAnalyzer.cs](./MemoryCacheSizeRequiredAnalyzer.cs) |
| Enabled by Default | Yes                                                                                      |

## What It Detects

❌ **VIOLATION**: Creating `MemoryCacheEntryOptions` without explicit `Size` property

```csharp
var options = new MemoryCacheEntryOptions();
cache.Set(key, value, options);  // WW0002 warning
```

✅ **COMPLIANT**: Setting `Size` property

```csharp
var options = new MemoryCacheEntryOptions { Size = 1 };
cache.Set(key, value, options);  // OK
```

## Why This Rule Exists

When `MemoryCache` is configured with `SizeLimit`, every entry must declare its size. Without it:

- Entries may be silently rejected from cache
- Memory accounting for eviction may fail
- `InvalidOperationException` may occur

**Reference**: [Microsoft Learn - Use SetSize, Size, and SizeLimit](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory#use-setsize-size-and-sizelimit-to-limit-cache-size)

## How to Fix

Add `Size` property to your `MemoryCacheEntryOptions`:

### Option 1: Default Size (Recommended)

```csharp
var options = new MemoryCacheEntryOptions
{
    Size = 1,  // Add this line
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};
```

### Option 2: Dynamic Size

```csharp
var options = new MemoryCacheEntryOptions
{
    Size = CalculateSize(value),  // Based on content
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};
```

### Option 3: Use MemoryCacheService (Best Practice)

```csharp
// MemoryCacheService handles Size automatically
await _cacheService.GetOrSetAsync(key, factory, cacheOptions);
```

## Configuration

In `.editorconfig`:

```editorconfig
# Enforce Size property on MemoryCacheEntryOptions (WW0002)
dotnet_diagnostic.WW0002.severity = warning
```

To disable:

```editorconfig
dotnet_diagnostic.WW0002.severity = none
```

## Files Created

- `eng/analyzers/MemoryCacheSizeRequiredAnalyzer.cs` - Analyzer implementation
- `eng/analyzers/AnalyzerResources.cs` - Localization strings
- `eng/analyzers/WW0002_RULE_DOCUMENTATION.md` - Full documentation
- `eng/analyzers/WW0002_EXAMPLES.cs` - Code examples
- `docs/WW0002_ANALYZER_IMPLEMENTATION.md` - Implementation details

## Related Rules

- **WW0001**: Avoid Color.FromArgb usage (theme consistency)

## More Information

- **Full Documentation**: [WW0002_RULE_DOCUMENTATION.md](./WW0002_RULE_DOCUMENTATION.md)
- **Code Examples**: [WW0002_EXAMPLES.cs](./WW0002_EXAMPLES.cs)
- **Implementation**: [WW0002_ANALYZER_IMPLEMENTATION.md](../../docs/WW0002_ANALYZER_IMPLEMENTATION.md)
