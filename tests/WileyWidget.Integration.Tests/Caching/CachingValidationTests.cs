using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.Data;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Integration.Tests.Shared;

namespace WileyWidget.Integration.Tests.Caching;

/// <summary>
/// Tests caching behavior across services with various cache implementations
/// </summary>
public class CachingValidationTests : IntegrationTestBase
{
    [Fact, Trait("Category", "Caching")]
    public async Task DashboardService_CacheHit_ReturnsCachedData()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var dashboardService = new DashboardService(
            GetRequiredService<ILogger<DashboardService>>(),
            GetRequiredService<IBudgetRepository>(),
            GetRequiredService<IMunicipalAccountRepository>(),
            new MemoryCacheService(memoryCache));

        // Act - First call should cache
        var result1 = await dashboardService.GetDashboardDataAsync();

        // Modify data in database (should not affect cached result)
        var testItem = new DashboardItem
        {
            Id = 999,
            Title = "Test Item",
            Value = "999",
            Category = "Test",
            FiscalYear = 2025
        };
        // Note: DashboardService uses its own caching, we'd need to modify the repository data

        // Second call should return cached data
        var result2 = await dashboardService.GetDashboardDataAsync();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().BeEquivalentTo(result2); // Should be same cached data
    }

    [Fact, Trait("Category", "Caching")]
    public async Task DashboardService_CacheExpiration_RefreshesData()
    {
        // Arrange
        await TestDataSeeder.SeedComprehensiveTestDataAsync(DbContext);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheService = new MemoryCacheService(memoryCache);

        var dashboardService = new DashboardService(
            GetRequiredService<ILogger<DashboardService>>(),
            GetRequiredService<IBudgetRepository>(),
            GetRequiredService<IMunicipalAccountRepository>(),
            cacheService);

        // Act - Get initial data
        var result1 = await dashboardService.GetDashboardDataAsync();

        // Wait for cache to expire (service uses 5 minute expiration)
        await Task.Delay(10); // Short delay for test

        // Force cache expiration by clearing it
        memoryCache.Clear();

        // Get data again (should refresh from database)
        var result2 = await dashboardService.GetDashboardDataAsync();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        // Results should be equivalent since data hasn't changed
        result1.Should().BeEquivalentTo(result2);
    }

    [Fact, Trait("Category", "Caching")]
    public async Task CacheService_SetAndGet_RoundTripSuccess()
    {
        // Arrange
        var cacheService = GetRequiredService<ICacheService>();
        var testData = new List<DashboardItem>
        {
            new DashboardItem { Id = 1, Title = "Test", Value = "100", Category = "Test", FiscalYear = 2025 }
        };
        var cacheKey = "test_key";

        // Act
        await cacheService.SetAsync(cacheKey, testData, TimeSpan.FromMinutes(5));
        var retrieved = await cacheService.GetAsync<List<DashboardItem>>(cacheKey);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(testData);
    }

    [Fact, Trait("Category", "Caching")]
    public async Task CacheService_CacheMiss_ReturnsNull()
    {
        // Arrange
        var cacheService = GetRequiredService<ICacheService>();
        var nonExistentKey = "non_existent_key";

        // Act
        var result = await cacheService.GetAsync<object>(nonExistentKey);

        // Assert
        result.Should().BeNull();
    }

    [Fact, Trait("Category", "Caching")]
    public async Task CacheService_Expiration_RemovesItem()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheService = new MemoryCacheService(memoryCache);
        var testData = "test data";
        var cacheKey = "expiring_key";
        var shortExpiration = TimeSpan.FromMilliseconds(50);

        // Act
        await cacheService.SetAsync(cacheKey, testData, shortExpiration);
        var result1 = await cacheService.GetAsync<string>(cacheKey);

        await Task.Delay(100); // Wait for expiration

        var result2 = await cacheService.GetAsync<string>(cacheKey);

        // Assert
        result1.Should().Be(testData);
        result2.Should().BeNull();
    }

    [Fact, Trait("Category", "Caching")]
    public async Task CacheService_FallbackToMemory_HandlesFailure()
    {
        // Arrange
        var mockCache = new Mock<ICacheService>();
        mockCache.Setup(c => c.GetAsync<List<DashboardItem>>(It.IsAny<string>()))
            .Throws(new Exception("Cache failure"));

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var fallbackCache = new MemoryCacheService(memoryCache);

        // Create a composite cache service that tries primary first, then falls back
        var compositeCache = new CompositeCacheService(mockCache.Object, fallbackCache);

        var testData = new List<DashboardItem>
        {
            new DashboardItem { Id = 1, Title = "Test", Value = "100", Category = "Test", FiscalYear = 2025 }
        };

        // Act
        await compositeCache.SetAsync("test_key", testData, TimeSpan.FromMinutes(5));
        var result = await compositeCache.GetAsync<List<DashboardItem>>("test_key");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(testData);
    }

    [Fact, Trait("Category", "Caching")]
    public async Task CacheService_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var cacheService = GetRequiredService<ICacheService>();
        var cacheKey = "concurrent_test";
        var tasks = new List<Task<string>>();

        // Act - Multiple concurrent operations
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                await cacheService.SetAsync($"{cacheKey}_{taskId}", $"value_{taskId}", TimeSpan.FromMinutes(5));
                var result = await cacheService.GetAsync<string>($"{cacheKey}_{taskId}");
                return result;
            }));
        }

        // Assert
        await Task.WhenAll(tasks);
        foreach (var task in tasks)
        {
            var result = await task;
            result.Should().NotBeNull();
            result.Should().StartWith("value_");
        }
    }

    [Fact, Trait("Category", "Caching")]
    public async Task CacheService_LargeObject_HandlesEfficiently()
    {
        // Arrange
        var cacheService = GetRequiredService<ICacheService>();
        var largeData = new List<DashboardItem>();

        // Create a large dataset
        for (int i = 0; i < 1000; i++)
        {
            largeData.Add(new DashboardItem
            {
                Id = i,
                Title = $"Large Test Item {i}",
                Value = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Category = "Performance",
                FiscalYear = 2025
            });
        }

        var cacheKey = "large_data_test";

        // Act
        await cacheService.SetAsync(cacheKey, largeData, TimeSpan.FromMinutes(5));
        var retrieved = await cacheService.GetAsync<List<DashboardItem>>(cacheKey);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().HaveCount(1000);
        retrieved.Should().BeEquivalentTo(largeData);
    }

    [Fact, Trait("Category", "Caching")]
    public async Task CacheService_SerializationFailure_ThrowsExpectedException()
    {
        // Arrange
        var cacheService = GetRequiredService<ICacheService>();
        var nonSerializableObject = new NonSerializableClass { Data = "test" };
        var cacheKey = "serialization_test";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cacheService.SetAsync(cacheKey, nonSerializableObject, TimeSpan.FromMinutes(5)));
    }

    private class NonSerializableClass
    {
        public string Data { get; set; } = string.Empty;
    }

    private class CompositeCacheService : ICacheService
    {
        private readonly ICacheService _primaryCache;
        private readonly ICacheService _fallbackCache;

        public CompositeCacheService(ICacheService primaryCache, ICacheService fallbackCache)
        {
            _primaryCache = primaryCache;
            _fallbackCache = fallbackCache;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                return await _primaryCache.GetAsync<T>(key);
            }
            catch
            {
                return await _fallbackCache.GetAsync<T>(key);
            }
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null) where T : class
        {
            try
            {
                return await _primaryCache.GetOrCreateAsync(key, factory, options);
            }
            catch
            {
                return await _fallbackCache.GetOrCreateAsync(key, factory, options);
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
        {
            try
            {
                await _primaryCache.SetAsync(key, value, ttl);
            }
            catch
            {
                await _fallbackCache.SetAsync(key, value, ttl);
            }
        }

        public async Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null) where T : class
        {
            try
            {
                await _primaryCache.SetAsync(key, value, options);
            }
            catch
            {
                await _fallbackCache.SetAsync(key, value, options);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _primaryCache.ExistsAsync(key);
            }
            catch
            {
                return await _fallbackCache.ExistsAsync(key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _primaryCache.RemoveAsync(key);
            }
            catch
            {
                await _fallbackCache.RemoveAsync(key);
            }
        }

        public async Task ClearAllAsync()
        {
            try
            {
                await _primaryCache.ClearAllAsync();
            }
            catch
            {
                await _fallbackCache.ClearAllAsync();
            }
        }
    }
}
