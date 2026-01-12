# WW0002 Analyzer Summary

## Summary

I have successfully created a Roslyn analyzer rule **WW0002** to prevent future violations of missing `Size` property on `MemoryCacheEntryOptions` when `SizeLimit` is configured on `MemoryCache`.

### 📋 Rule Details

**Rule ID**: `WW0002`
**Title**: MemoryCacheEntryOptions missing required Size property
**Severity**: Warning (default, configurable)
**Category**: Caching

### 🎯 What It Detects

The analyzer flags two violation patterns:

1. **No initializer**: `new MemoryCacheEntryOptions()`
2. **Initializer without Size**: `new MemoryCacheEntryOptions { /* no Size */ }`

### ✅ Compliant Code

```csharp
// Pattern 1: Default Size (most common)
var options = new MemoryCacheEntryOptions
{
    Size = 1,
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};

// Pattern 2: Dynamic Size
var options = new MemoryCacheEntryOptions
{
    Size = value.EstimatedSize,
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};

// Pattern 3: Using MemoryCacheService (Recommended)
await _cacheService.GetOrSetAsync(key, factory, cacheOptions);
```

### 📁 Files Created

| File                               | Location       | Purpose                                        |
| ---------------------------------- | -------------- | ---------------------------------------------- |
| MemoryCacheSizeRequiredAnalyzer.cs | eng/analyzers/ | Core analyzer implementation (C# class)        |
| AnalyzerResources.cs               | eng/analyzers/ | Localization strings for all analyzers         |
| WW0002_RULE_DOCUMENTATION.md       | eng/analyzers/ | Comprehensive rule documentation with examples |
| WW0002_EXAMPLES.cs                 | eng/analyzers/ | Practical violation and fix examples           |
| WW0002_QUICK_REFERENCE.md          | eng/analyzers/ | Quick reference guide                          |

### 📝 Files Modified

| File                                 | Changes                             |
| ------------------------------------ | ----------------------------------- |
| AnalyzerReleases.Unshipped.md (root) | Added WW0002 release notes          |
| AnalyzerReleases.Unshipped.md (eng)  | Added WW0002 release notes          |
| .editorconfig                        | Added WW0002 severity configuration |

### 🔗 Implementation Details

**Location**: `eng/analyzers/MemoryCacheSizeRequiredAnalyzer.cs`

The analyzer:

- Registers for `ObjectCreationExpression` syntax nodes
- Checks if object type contains "MemoryCacheEntryOptions"
- Scans initializer for `Size` property assignment
- Reports `WW0002` diagnostic if `Size` is missing

**Resource Strings**: `eng/analyzers/AnalyzerResources.cs`

Provides localized messages:

- Title: "MemoryCacheEntryOptions missing required Size property"
- Message: Explains that Size must be set when SizeLimit is configured
- Description: Links to Microsoft documentation with specific guidance

**EditorConfig**: `.editorconfig`

```editorconfig
# Enforce Size property on MemoryCacheEntryOptions (WW0002)
dotnet_diagnostic.WW0002.severity = warning
```

### 📚 Context: Why This Rule Exists

WileyWidget configures `IMemoryCache` with `SizeLimit = 1024` in [src/WileyWidget.WinForms/Configuration/DependencyInjection.cs](../src/WileyWidget.WinForms/Configuration/DependencyInjection.cs):

```csharp
services.AddSingleton<IMemoryCache>(sp =>
{
    var options = new MemoryCacheOptions
    {
        SizeLimit = 1024  // Prevent unbounded cache growth
    };
    return new MemoryCache(options);
});
```

Per Microsoft:

> "An entry won't be cached if the sum of the cached entry sizes exceeds the value specified by SizeLimit. If no cache size limit is set, the cache size set on the entry is ignored."

[Reference: Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory#use-setsize-size-and-sizelimit-to-limit-cache-size)

### ✅ Validation

- ✓ Build successful with no compilation errors
- ✓ All analyzer classes compile correctly
- ✓ EditorConfig properly configured
- ✓ Release notes updated
- ✓ Documentation complete with examples

### 🚀 Next Steps

1. The rule is ready to use - no additional configuration needed
2. IDE may require restart to recognize the analyzer
3. Existing violations can be found by searching for `new MemoryCacheEntryOptions` without explicit `Size`
4. Future violations will be automatically detected by the analyzer

### 📖 Documentation Files

For more information, see:

- **Quick Reference**: [eng/analyzers/WW0002_QUICK_REFERENCE.md](./eng/analyzers/WW0002_QUICK_REFERENCE.md)
- **Full Documentation**: [eng/analyzers/WW0002_RULE_DOCUMENTATION.md](./eng/analyzers/WW0002_RULE_DOCUMENTATION.md)
- **Code Examples**: [eng/analyzers/WW0002_EXAMPLES.cs](./eng/analyzers/WW0002_EXAMPLES.cs)
- **Implementation Details**: [docs/WW0002_ANALYZER_IMPLEMENTATION.md](./docs/WW0002_ANALYZER_IMPLEMENTATION.md)
