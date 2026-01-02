using Xunit;
using Moq;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Business.Services;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Tests.TestHelpers;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public sealed class GrokRecommendationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<GrokRecommendationService>> _logger = new();
        private readonly Mock<IMemoryCache> _cache = new();

        public GrokRecommendationServiceTests()
        {
            // Setup cache mock to handle TryGetValue and Set calls
#pragma warning disable CS8600, CS8601 // Possible null reference assignment and conversion
            _cache.Setup(c => c.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny!)).Returns(false);
            var mockCacheEntry = new Mock<ICacheEntry>();
            _cache.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);
#pragma warning restore CS8600, CS8601
        }

        public void Dispose()
        {
        }

        private static IConfiguration BuildConfiguration(bool enableXai, string? apiKey = null, string? endpoint = null)
        {
            var dic = new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = enableXai ? "true" : "false",
                ["XAI:ApiKey"] = apiKey,
                ["XAI:Endpoint"] = endpoint ?? "https://api.x.ai/v1/chat/completions"
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dic).Build();
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_ReturnsRuleBased_WhenGrokDisabled()
        {
            // Arrange
            var config = BuildConfiguration(enableXai: false);
            var httpFactory = new Mock<IHttpClientFactory>();

            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, _cache.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m,
                ["Sewer"] = 68000m,
                ["Trash"] = 28000m
            };

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(expenses, 15.0m);

            // Assert - rule-based factors apply (1.15 base + department variance)
            result.AdjustmentFactors.Should().ContainKey("Water");
            result.AdjustmentFactors["Water"].Should().Be(1.15m);
            result.AdjustmentFactors.Should().ContainKey("Sewer");
            result.AdjustmentFactors["Sewer"].Should().Be(1.17m);
            result.AdjustmentFactors.Should().ContainKey("Trash");
            result.AdjustmentFactors["Trash"].Should().Be(1.10m);
            result.FromGrokApi.Should().BeFalse();
            result.Explanation.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_ParsesGrokResponse_WhenGrokReturnsValidJson()
        {
            // Arrange
            var apiKey = "testkey-abc";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            // Mock HTTP handler to capture authorization header and return a valid Grok-style response
            AuthenticationHeaderValue? capturedAuth = null;
            var responseBody = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                factors = new Dictionary<string, decimal>
                                {
                                    ["Water"] = 1.15m,
                                    ["Sewer"] = 1.17m,
                                    ["Trash"] = 1.05m
                                },
                                explanation = "Test explanation for rate adjustments."
                            })
                        }
                    }
                }
            });

            using var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                capturedAuth = req.Headers.Authorization;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                });
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, _cache.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m,
                ["Sewer"] = 68000m,
                ["Trash"] = 28000m
            };

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(expenses, 15.0m);

            // Assert
            capturedAuth.Should().NotBeNull();
            capturedAuth!.Scheme.Should().Be("Bearer");
            capturedAuth.Parameter.Should().Be(apiKey);

            result.AdjustmentFactors.Should().ContainKey("Water");
            result.AdjustmentFactors["Water"].Should().Be(1.15m);
            result.AdjustmentFactors.Should().ContainKey("Sewer");
            result.AdjustmentFactors["Sewer"].Should().Be(1.17m);
            result.AdjustmentFactors.Should().ContainKey("Trash");
            result.AdjustmentFactors["Trash"].Should().Be(1.05m);
            result.FromGrokApi.Should().BeTrue();
            result.Explanation.Should().Be("Test explanation for rate adjustments.");
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_FallsBackToRuleBased_WhenGrokReturnsInvalidContent()
        {
            // Arrange
            var config = BuildConfiguration(enableXai: true, apiKey: "abc", endpoint: "http://localhost/grok");

            var responseBody = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "Not a JSON object"
                        }
                    }
                }
            });

            using var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseBody, HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, _cache.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m,
                ["Sewer"] = 68000m
            };

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(expenses, 15.0m);

            // Assert - should fallback to rule-based
            result.AdjustmentFactors.Should().ContainKey("Water");
            result.AdjustmentFactors["Water"].Should().Be(1.15m);
            result.AdjustmentFactors.Should().ContainKey("Sewer");
            result.AdjustmentFactors["Sewer"].Should().Be(1.17m);
            result.FromGrokApi.Should().BeFalse();
        }

        [Fact]
        public async Task GetRecommendationExplanationAsync_ReturnsGrokContent_WhenGrokRespondsWithText()
        {
            // Arrange
            var apiKey = "key-123";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            var explanationText = "These adjustments are necessary due to infrastructure and treatment costs.";

            var responseBody = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = explanationText
                        }
                    }
                }
            });

            using var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseBody, HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, _cache.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m
            };

            // Act
            var explanation = await svc.GetRecommendationExplanationAsync(expenses, 15.0m);

            // Assert
            explanation.Should().Be(explanationText);
        }

        [Fact]
        public async Task GetRecommendationExplanationAsync_CachesExplanation_AndAvoidsDuplicateApiCalls()
        {
            // Arrange
            var apiKey = "integration-expl-key";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            var explanationText = "Integration explanation from Grok";

            var responseBody = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = explanationText
                        }
                    }
                }
            });

            var callCount = 0;
            using var handler = new FakeHttpMessageHandler(async (req, ct) =>
            {
                System.Threading.Interlocked.Increment(ref callCount);
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                });
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Use a real memory cache for integration-style verification
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, memoryCache);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m
            };

            // Use reflection to compute the cache key (private method)
            var keyMethod = typeof(GrokRecommendationService).GetMethod("GenerateCacheKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyMethod.Should().NotBeNull();
            var baseKey = (string)keyMethod!.Invoke(svc, new object[] { expenses, 15.0m })!;
            var explanationCacheKey = $"rec_expl_{baseKey}";

            // Ensure cache is empty before call
            memoryCache.TryGetValue(explanationCacheKey, out object? pre);
            pre.Should().BeNull();

            // Act - first call (should hit HTTP once)
            var explanation = await svc.GetRecommendationExplanationAsync(expenses, 15.0m);

            // Assert
            explanation.Should().Be(explanationText);
            callCount.Should().Be(1);
            memoryCache.TryGetValue(explanationCacheKey, out object? cached).Should().BeTrue();
            cached.Should().Be(explanationText);

            // Act - second call should return cached content, no additional HTTP call
            var explanation2 = await svc.GetRecommendationExplanationAsync(expenses, 15.0m);
            explanation2.Should().Be(explanationText);
            callCount.Should().Be(1);
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_SetsCacheEntry_WhenGrokReturnsValidResponse()
        {
            // Arrange
            var apiKey = "integration-key";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            var responseBody = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                factors = new Dictionary<string, decimal>
                                {
                                    ["Water"] = 1.20m,
                                    ["Sewer"] = 1.18m
                                },
                                explanation = "Integration explanation"
                            })
                        }
                    }
                }
            });

            using var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                });
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Use a real memory cache for integration-style verification
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, memoryCache);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m,
                ["Sewer"] = 68000m
            };

            // Use reflection to compute the cache key (private method)
            var keyMethod = typeof(GrokRecommendationService).GetMethod("GenerateCacheKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyMethod.Should().NotBeNull();
            var cacheKey = (string)keyMethod!.Invoke(svc, new object[] { expenses, 15.0m })!;

            // Ensure cache is empty before call
            memoryCache.TryGetValue(cacheKey, out object? pre);
            pre.Should().BeNull();

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(expenses, 15.0m);

            // Assert - cache entry was set and matches returned result
            memoryCache.TryGetValue(cacheKey, out object? cachedObj).Should().BeTrue();
            cachedObj.Should().NotBeNull();
            var cachedResult = (RecommendationResult)cachedObj!;
            cachedResult.AdjustmentFactors.Should().BeEquivalentTo(result.AdjustmentFactors);
            cachedResult.Explanation.Should().Be(result.Explanation);

            // Act - clear cache
            svc.ClearCache();

            // Assert - cache entry was removed
            memoryCache.TryGetValue(cacheKey, out object? afterClear).Should().BeFalse();
        }

        [Fact]
        public async Task ClearCache_RemovesCachedEntries()
        {
            // Arrange
            var apiKey = "integration-key";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            var responseBody = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                factors = new Dictionary<string, decimal>
                                {
                                    ["Water"] = 1.20m,
                                    ["Sewer"] = 1.18m
                                },
                                explanation = "Integration explanation"
                            })
                        }
                    }
                }
            });

            using var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                });
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Use a real memory cache for integration-style verification
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, memoryCache);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m,
                ["Sewer"] = 68000m
            };

            // Use reflection to compute the cache key (private method)
            var keyMethod = typeof(GrokRecommendationService).GetMethod("GenerateCacheKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyMethod.Should().NotBeNull();
            var cacheKey = (string)keyMethod!.Invoke(svc, new object[] { expenses, 15.0m })!;

            // Ensure cache is empty before call
            memoryCache.TryGetValue(cacheKey, out object? pre);
            pre.Should().BeNull();

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(expenses, 15.0m);

            // Assert - cache entry was set and matches returned result
            memoryCache.TryGetValue(cacheKey, out object? cachedObj).Should().BeTrue();
            cachedObj.Should().NotBeNull();
            var cachedResult = (RecommendationResult)cachedObj!;
            cachedResult.AdjustmentFactors.Should().BeEquivalentTo(result.AdjustmentFactors);
            cachedResult.Explanation.Should().Be(result.Explanation);

            // Act - clear cache
            svc.ClearCache();

            // Assert - cache entry was removed
            memoryCache.TryGetValue(cacheKey, out object? afterClear).Should().BeFalse();
        }
        [Fact]
        public void Constructor_UsesOptions_ToSetCacheDuration()
        {
            // Arrange
            var config = BuildConfiguration(enableXai: false);
            var httpFactory = new Mock<IHttpClientFactory>();
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var opts = Microsoft.Extensions.Options.Options.Create(new WileyWidget.Business.Configuration.GrokRecommendationOptions { CacheDuration = TimeSpan.FromMinutes(42) });

            // Act
            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, memoryCache, opts);

            // Assert - private field _cacheDuration should equal configured value
            var field = typeof(GrokRecommendationService).GetField("_cacheDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.Should().NotBeNull();
            var val = (TimeSpan)field!.GetValue(svc)!;
            val.Should().Be(TimeSpan.FromMinutes(42));
        }

        [Fact]
        public async Task GetRecommendationExplanationAsync_FallsBackToRuleBased_WhenGrokFails()
        {
            // Arrange
            var apiKey = "key-456";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            // Response with explicit null content to force fallback
            var responseBodyNull = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = (string?)null
                        }
                    }
                }
            });

            using var handlerNull = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseBodyNull, HttpStatusCode.OK));
            using var httpClientNull = new HttpClient(handlerNull) { BaseAddress = new Uri("http://localhost") };
            var httpFactoryNull = new Mock<IHttpClientFactory>();
            httpFactoryNull.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClientNull);

            var svcNull = new GrokRecommendationService(_logger.Object, config, httpFactoryNull.Object, _cache.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m,
                ["Sewer"] = 68000m
            };

            // Act - null content
            var explanationNull = await svcNull.GetRecommendationExplanationAsync(expenses, 15.0m);

            // Debug: output explanation to test logs
            Console.WriteLine($"DEBUG: explanationNull=<{explanationNull ?? "(null)"}> (len={(explanationNull?.Length ?? 0)})");

            // Assert - fallback should be rule-based explanation (starts with "Based on your monthly expenses")
            explanationNull.Should().StartWith("Based on your monthly expenses");
            explanationNull.Should().Contain("$" + (expenses["Water"] + expenses["Sewer"]).ToString("N2", CultureInfo.InvariantCulture));

            // Also test empty string content scenario
            var responseBodyEmpty = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = ""
                        }
                    }
                }
            });

            using var handlerEmpty = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseBodyEmpty, HttpStatusCode.OK));
            using var httpClientEmpty = new HttpClient(handlerEmpty) { BaseAddress = new Uri("http://localhost") };
            var httpFactoryEmpty = new Mock<IHttpClientFactory>();
            httpFactoryEmpty.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClientEmpty);

            var svcEmpty = new GrokRecommendationService(_logger.Object, config, httpFactoryEmpty.Object, _cache.Object);

            // Act - empty string content
            var explanationEmpty = await svcEmpty.GetRecommendationExplanationAsync(expenses, 15.0m);

            // Assert - also falls back
            explanationEmpty.Should().StartWith("Based on your monthly expenses");
            explanationEmpty.Should().Contain("$" + (expenses["Water"] + expenses["Sewer"]).ToString("N2", CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task GetRecommendationExplanationAsync_SetsCacheEntry_WhenGrokReturnsValidResponse()
        {
            // Arrange
            var apiKey = "expl-cache-key";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            var explanationText = "These adjustments are necessary due to infrastructure and treatment costs.";

            var responseBody = JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = explanationText
                        }
                    }
                }
            });

            using var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(responseBody, HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, memoryCache);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m
            };

            // Compute explanation cache key via private GenerateCacheKey
            var keyMethod = typeof(GrokRecommendationService).GetMethod("GenerateCacheKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyMethod.Should().NotBeNull();
            var cacheKey = (string)keyMethod!.Invoke(svc, new object[] { expenses, 15.0m })!;
            var explanationCacheKey = "rec_expl_" + cacheKey;

            // Ensure cache empty
            memoryCache.TryGetValue(explanationCacheKey, out object? pre).Should().BeFalse();

            // Act
            var explanation = await svc.GetRecommendationExplanationAsync(expenses, 15.0m);

            // Assert
            explanation.Should().Be(explanationText);
            memoryCache.TryGetValue(explanationCacheKey, out object? cachedObj).Should().BeTrue();
            cachedObj.Should().BeOfType<string>();
            (cachedObj as string).Should().NotBeNull();
            (cachedObj as string).Should().Be(explanationText);
        }

        [Fact]
        public async Task GetRecommendedAdjustmentFactorsAsync_ReturnsCachedResult_WhenCacheHit()
        {
            // Arrange
            var apiKey = "testkey-abc";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            var cachedFactors = new Dictionary<string, decimal> { ["Water"] = 1.23m, ["Sewer"] = 1.15m };
            var cachedResult = new RecommendationResult(cachedFactors, "cached explanation", false, "cache", Array.Empty<string>());

            var cacheMock = new Mock<IMemoryCache>();
            object? cachedObj = cachedResult;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type
            cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out cachedObj)).Returns(true);
#pragma warning restore CS8600

            var httpFactory = new Mock<IHttpClientFactory>();
            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, cacheMock.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m
            };

            // Act
            var result = await svc.GetRecommendedAdjustmentFactorsAsync(expenses, 15.0m);

            // Assert
            result.Should().BeEquivalentTo(cachedResult);
            httpFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetRecommendationExplanationAsync_ReturnsCachedExplanation_WhenCacheHit()
        {
            // Arrange
            var apiKey = "key-789";
            var config = BuildConfiguration(enableXai: true, apiKey: apiKey, endpoint: "http://localhost/grok");

            var cacheMock = new Mock<IMemoryCache>();
            string cachedExplanation = "Cached explanation text";
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type
            object? cachedObj = cachedExplanation;
            _ = cachedObj;
            cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out cachedObj)).Returns(true);
#pragma warning restore CS8600

            var httpFactory = new Mock<IHttpClientFactory>();
            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, cacheMock.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m
            };

            // Act
            var explanation = await svc.GetRecommendationExplanationAsync(expenses, 15.0m);

            // Assert
            explanation.Should().Be(cachedExplanation);
            httpFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void GenerateRuleBasedExplanation_ShouldReturnNonEmptyText()
        {
            // Arrange
            var config = BuildConfiguration(enableXai: false);
            var httpFactory = new Mock<IHttpClientFactory>();
            var svc = new GrokRecommendationService(_logger.Object, config, httpFactory.Object, _cache.Object);

            var expenses = new Dictionary<string, decimal>
            {
                ["Water"] = 45000m,
                ["Sewer"] = 68000m
            };

            // Act - invoke private method via reflection
            var method = typeof(GrokRecommendationService).GetMethod("GenerateRuleBasedExplanation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Should().NotBeNull();
            var invokeResult = method!.Invoke(svc, new object[] { expenses, 15.0m });
            invokeResult.Should().NotBeNull();
            var result = (string)invokeResult!;

            // Assert
            result.Should().NotBeNullOrWhiteSpace();
            result.Should().StartWith("Based on your monthly expenses");
        }
    }
}
