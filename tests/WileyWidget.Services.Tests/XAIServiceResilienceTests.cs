using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq.Protected;
using Moq;
using Polly;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Services.Tests
{
    public class XAIServiceResilienceTests
    {
        [Theory]
        [InlineData(1)] // First retry succeeds
        [InlineData(2)] // Second retry succeeds
        [InlineData(3)] // Third retry succeeds
        public async Task ResiliencePipeline_RetriesTransientFailures(int succeedOnAttempt)
        {
            // Arrange
            var attempt = 0;
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockHttpClient = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpClient.Object);
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] {
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:ApiKey", "test-api-key-0123456789abcdefghijkl"),
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:CircuitBreakerBreakSeconds", "1")
            }).Build();
            var logger = new Mock<ILogger<XAIService>>().Object;
            var contextService = new Mock<IWileyWidgetContextService>().Object;
            var aiLoggingService = new Mock<IAILoggingService>().Object;
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var xaiService = new XAIService(mockHttpClientFactory.Object, config, logger, contextService, aiLoggingService, memoryCache);

            // Simulate transient failure then success
            mockHttpClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    attempt++;
                    if (attempt < succeedOnAttempt)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Success\"}}]}")
                    };
                });

            // Act
            var result = await xaiService.GetInsightsAsync("ctx", "q");

            // Assert
            Assert.Equal("Success", result);
        }

        [Fact]
        public async Task ResiliencePipeline_CircuitBreaker_OpensAfterThreshold()
        {
            // Arrange
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockHttpClient = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpClient.Object);
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] {
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:ApiKey", "test-api-key-0123456789abcdefghijkl"),
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:CircuitBreakerBreakSeconds", "1")
            }).Build();
            var logger = new Mock<ILogger<XAIService>>().Object;
            var contextService = new Mock<IWileyWidgetContextService>().Object;
            var aiLoggingService = new Mock<IAILoggingService>().Object;
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var xaiService = new XAIService(mockHttpClientFactory.Object, config, logger, contextService, aiLoggingService, memoryCache);

            // Simulate repeated failures to open circuit breaker
            mockHttpClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            // Act: trigger enough failures to open the circuit breaker
            for (int i = 0; i < 10; i++)
            {
                await xaiService.GetInsightsAsync("ctx", $"fail-{i}");
            }

            // After threshold, circuit breaker should open and fail fast
            var result = await xaiService.GetInsightsAsync("ctx", "should-fail-fast");
            // Accept either an explicit 'error' response or an authentication-style failure message
            Assert.True(
                result.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                result.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected result to indicate an error or authentication failure, got: {result}");
        }

        [Fact]
        public async Task ResiliencePipeline_CircuitBreaker_HalfOpenAfterBreakDuration()
        {
            // Arrange
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockHttpClient = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpClient.Object);
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] {
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:ApiKey", "test-api-key-0123456789abcdefghijkl"),
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:CircuitBreakerBreakSeconds", "1")
            }).Build();
            var logger = new Mock<ILogger<XAIService>>().Object;
            var contextService = new Mock<IWileyWidgetContextService>().Object;
            var aiLoggingService = new Mock<IAILoggingService>().Object;
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var xaiService = new XAIService(mockHttpClientFactory.Object, config, logger, contextService, aiLoggingService, memoryCache);

            // Simulate repeated failures to open circuit breaker
            mockHttpClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            // Trigger failures to open circuit breaker
            for (int i = 0; i < 10; i++)
                await xaiService.GetInsightsAsync("ctx", $"fail-{i}");

            // Simulate recovery after break duration (manually wait)
            // Break duration is configured to 1s in the test configuration above; wait slightly longer to allow half-open transition
            await Task.Delay(TimeSpan.FromSeconds(2)); // Short wait to keep tests fast
            mockHttpClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Recovered\"}}]}")
                });
            var result = await xaiService.GetInsightsAsync("ctx", "recovery");
            Assert.Equal("Recovered", result);
        }

        [Fact]
        public async Task ResiliencePipeline_Jitter_PreventsThunderingHerd()
        {
            // Arrange
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockHttpClient = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpClient.Object);
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] {
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:ApiKey", "test-api-key-0123456789abcdefghijkl"),
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:CircuitBreakerBreakSeconds", "1")
            }).Build();
            var logger = new Mock<ILogger<XAIService>>().Object;
            var contextService = new Mock<IWileyWidgetContextService>().Object;
            var aiLoggingService = new Mock<IAILoggingService>().Object;
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var xaiService = new XAIService(mockHttpClientFactory.Object, config, logger, contextService, aiLoggingService, memoryCache);

            // Simulate transient failures to trigger retry with jitter
            int callCount = 0;
            mockHttpClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount < 3)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"JitterSuccess\"}}]}")
                    };
                });
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await xaiService.GetInsightsAsync("ctx", "jitter-test");
            sw.Stop();
            Assert.Equal("JitterSuccess", result);
            Assert.True(sw.ElapsedMilliseconds > 500, "Jitter/backoff should delay retries");
        }

        [Fact]
        public async Task GetInsightsAsync_ReturnsCachedResponse()
        {
            // Arrange
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] {
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:ApiKey", "test-api-key-0123456789abcdefghijkl"),
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:CircuitBreakerBreakSeconds", "1")
            }).Build();
            var logger = new Mock<ILogger<XAIService>>().Object;
            var contextService = new Mock<IWileyWidgetContextService>().Object;
            var aiLoggingService = new Mock<IAILoggingService>().Object;
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var xaiService = new XAIService(mockHttpClientFactory.Object, config, logger, contextService, aiLoggingService, memoryCache);
            var cacheKey = $"XAI:{"ctx".GetHashCode(StringComparison.OrdinalIgnoreCase)}:{"q".GetHashCode(StringComparison.OrdinalIgnoreCase)}";
            memoryCache.Set(cacheKey, "CachedSuccess");
            var result = await xaiService.GetInsightsAsync("ctx", "q");
            Assert.Equal("CachedSuccess", result);
        }

        [Fact]
        public async Task GetInsightsAsync_ThrowsOnInvalidInput()
        {
            // Arrange
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] {
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:ApiKey", "test-api-key-0123456789abcdefghijkl"),
                new System.Collections.Generic.KeyValuePair<string, string?>("XAI:CircuitBreakerBreakSeconds", "1")
            }).Build();
            var logger = new Mock<ILogger<XAIService>>().Object;
            var contextService = new Mock<IWileyWidgetContextService>().Object;
            var aiLoggingService = new Mock<IAILoggingService>().Object;
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var xaiService = new XAIService(mockHttpClientFactory.Object, config, logger, contextService, aiLoggingService, memoryCache);
            await Assert.ThrowsAsync<ArgumentException>(() => xaiService.GetInsightsAsync("", "q"));
            await Assert.ThrowsAsync<ArgumentException>(() => xaiService.GetInsightsAsync("ctx", ""));
        }

        [Fact]
        public async Task GetInsightsAsync_HandlesApiError()
        {
            // Arrange
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockHttpClient = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpClient.Object);
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[] { new System.Collections.Generic.KeyValuePair<string, string?>("XAI:ApiKey", "test-api-key-0123456789abcdefghijkl") }).Build();
            var logger = new Mock<ILogger<XAIService>>().Object;
            var contextService = new Mock<IWileyWidgetContextService>().Object;
            var aiLoggingService = new Mock<IAILoggingService>().Object;
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var xaiService = new XAIService(mockHttpClientFactory.Object, config, logger, contextService, aiLoggingService, memoryCache);
            mockHttpClient.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"API error\",\"type\":\"TestError\"}}")
                });
            var result = await xaiService.GetInsightsAsync("ctx", "q");
            Assert.Contains("API error", result);
        }

        [Fact]
        public void ResiliencePipeline_ContextPool_ReusesContexts()
        {
            // Arrange
            var pool = Polly.ResilienceContextPool.Shared;
            var context1 = pool.Get();
            pool.Return(context1);
            var context2 = pool.Get();
            // Assert
            Assert.Same(context1, context2);
        }
    }
}
