/// <summary>
/// Example file demonstrating WW0002 analyzer violations and fixes.
/// 
/// Copy this file to src/WileyWidget.Services/ or anywhere in the codebase
/// to see WW0002 warnings appear in the IDE (if analyzer is activated).
/// 
/// This file is for REFERENCE ONLY and demonstrates what triggers the rule.
/// </summary>

#nullable disable

using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;

namespace WileyWidget.Analyzers.Examples
{
    // NOTE: This is an EXAMPLE file showing WW0002 violations.
    // DO NOT uncomment these methods - they exist only to demonstrate the analyzer.
    // Real production code should follow the COMPLIANT patterns below.

    public class WW0002_ViolationExamples
    {
        private readonly IMemoryCache _cache;

        public WW0002_ViolationExamples(IMemoryCache cache) => _cache = cache;

        // ❌ VIOLATION 1: No initializer, no Size property
        /*
        public void ViolationExample1()
        {
            var options = new MemoryCacheEntryOptions();
            _cache.Set("key", "value", options);  // WW0002 warning
        }
        */

        // ❌ VIOLATION 2: Initializer without Size property
        /*
        public void ViolationExample2()
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            _cache.Set("key", "value", options);  // WW0002 warning
        }
        */

        // ❌ VIOLATION 3: Multiple properties but no Size
        /*
        public void ViolationExample3()
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                SlidingExpiration = TimeSpan.FromMinutes(15),
                Priority = CacheItemPriority.Normal
            };
            _cache.Set("key", "value", options);  // WW0002 warning - Size missing!
        }
        */
    }

    public class WW0002_CompliantExamples
    {
        private readonly IMemoryCache _cache;

        public WW0002_CompliantExamples(IMemoryCache cache) => _cache = cache;

        // ✅ COMPLIANT 1: Explicit Size property (most common)
        public void CompliantExample1()
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,  // <-- Explicit Size
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            _cache.Set("key", "value", options);  // ✓ OK
        }

        // ✅ COMPLIANT 2: Full initialization with Size
        public void CompliantExample2()
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,  // <-- Always include Size
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                SlidingExpiration = TimeSpan.FromMinutes(15),
                Priority = CacheItemPriority.Normal
            };
            _cache.Set("key", "value", options);  // ✓ OK
        }

        // ✅ COMPLIANT 3: Dynamic Size based on content
        public void CompliantExample3(object value, long sizeInUnits)
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = sizeInUnits,  // <-- Dynamic Size based on content
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            _cache.Set("key", value, options);  // ✓ OK
        }

        // ✅ COMPLIANT 4: Size calculated from value
        public void CompliantExample4(Dictionary<string, object> data)
        {
            var estimatedSize = Math.Max(1, data.Count / 10);  // 1 unit per 10 entries
            var options = new MemoryCacheEntryOptions
            {
                Size = estimatedSize,  // <-- Calculated Size
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            _cache.Set("dictionary_key", data, options);  // ✓ OK
        }

        // ✅ COMPLIANT 5: Size with priority and callbacks
        public void CompliantExample5()
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,  // <-- Explicit Size
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.High,
                PostEvictionCallback = (key, value, reason, state) =>
                {
                    Console.WriteLine($"Cache entry {key} evicted: {reason}");
                }
            };
            _cache.Set("important_key", "important_value", options);  // ✓ OK
        }
    }

    public class WW0002_MemoryCacheServiceExample
    {
        private readonly IMemoryCache _cache;

        public WW0002_MemoryCacheServiceExample(IMemoryCache cache) => _cache = cache;

        // ✅ RECOMMENDED: Use MemoryCacheService for automatic Size handling
        // The MemoryCacheService.MapOptions() method handles Size defaults automatically
        public void RecommendedPattern()
        {
            // Instead of creating MemoryCacheEntryOptions directly,
            // use MemoryCacheService which ensures Size is always set:
            //
            // await _cacheService.GetOrSetAsync(key, factory, cacheEntryOptions);
            //
            // The service will:
            // 1. Accept CacheEntryOptions (custom wrapper)
            // 2. Call MapOptions() which defaults Size to 1 if not specified
            // 3. Create MemoryCacheEntryOptions with Size always set
            // 4. Never violate WW0002

            var options = new MemoryCacheEntryOptions
            {
                Size = 1  // If using direct cache, always set this
            };
        }
    }

    // ============================================================================
    // RULE SUMMARY FOR WW0002
    // ============================================================================
    // When MemoryCache has SizeLimit configured (e.g., SizeLimit = 1024):
    //
    // MUST DO:   Always set Size property on MemoryCacheEntryOptions
    // EXAMPLE:   new MemoryCacheEntryOptions { Size = 1, ... }
    //
    // MUST NOT:  Create MemoryCacheEntryOptions without Size property
    // EXAMPLE:   new MemoryCacheEntryOptions { } ← VIOLATION
    //
    // DEFAULT:   If unsure, use Size = 1 (recommended for most entries)
    //
    // REFERENCE: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory
    // ============================================================================
}
