using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Services;
using Unit.TestHelpers;

namespace Unit.Services
{
    /// <summary>
    /// Unit tests for XAIService
    /// </summary>
    public class XAIServiceTests : IDisposable
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<XAIService>> _loggerMock;
        private readonly Mock<IWileyWidgetContextService> _contextServiceMock;
        private readonly Mock<IAILoggingService> _aiLoggingServiceMock;
        private readonly MemoryCache _memoryCache;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private HttpClient _httpClient;

        public XAIServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<XAIService>>();
            _contextServiceMock = new Mock<IWileyWidgetContextService>();
            _aiLoggingServiceMock = new Mock<IAILoggingService>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            // Setup configuration
            _configurationMock.Setup(c => c["XAI:ApiKey"]).Returns("test-api-key");
            _configurationMock.Setup(c => c["XAI:BaseUrl"]).Returns("https://api.x.ai/v1/");
            _configurationMock.Setup(c => c["XAI:TimeoutSeconds"]).Returns("15");
            _configurationMock.Setup(c => c["XAI:MaxConcurrentRequests"]).Returns("5");
            _configurationMock.Setup(c => c["XAI:Model"]).Returns("grok-4-0709");

            // Setup context service
            _contextServiceMock.Setup(c => c.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("Test system context");

            // Setup HTTP client
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient("AIServices")).Returns(_httpClient);
        }

        [Fact]
        public async Task GetInsightsAsync_SuccessfulResponse_ReturnsContent()
        {
            // Arrange
            var expectedResponse = "Test AI response";
            SetupHttpResponse(HttpStatusCode.OK, expectedResponse);

            var service = CreateService();

            // Act
            var result = await service.GetInsightsAsync("context", "question");

            // Assert
            Assert.Equal(expectedResponse, result);
            _aiLoggingServiceMock.Verify(l => l.LogQuery(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _aiLoggingServiceMock.Verify(l => l.LogResponse(It.IsAny<string>(), expectedResponse, It.IsAny<long>(), 0), Times.Once);
        }

        [Fact]
        public async Task GetInsightsAsync_CachedResponse_ReturnsCachedContent()
        {
            // Arrange
            var cacheKey = "cached-key";
            var cachedContent = "Cached response";
            _memoryCache.Set(cacheKey, cachedContent);

            var service = CreateService();

            // Act
            var result = await service.GetInsightsAsync("context", "question");

            // Assert
            Assert.Equal(cachedContent, result);
            _httpMessageHandlerMock.VerifyNoOtherCalls(); // Should not make HTTP call
        }

        [Fact]
        public async Task GetInsightsAsync_RetryOnFailure_SucceedsOnRetry()
        {
            // Arrange
            var callCount = 0;
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("\"{\\\"choices\\\":[{\\\"message\\\":{\\\"content\\\":\\\"Retry success\\\"}}]}\"")
                    };
                });

            var service = CreateService();

            // Act
            var result = await service.GetInsightsAsync("context", "question");

            // Assert
            Assert.Equal("Retry success", result);
            Assert.Equal(2, callCount); // Should have retried once
        }

        [Fact]
        public async Task GetInsightsAsync_ApiError_ReturnsErrorMessage()
        {
            // Arrange
            SetupHttpResponse(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Invalid request\"}}");

            var service = CreateService();

            // Act
            var result = await service.GetInsightsAsync("context", "question");

            // Assert
            Assert.Contains("API error", result);
        }

        [Fact]
        public async Task GetInsightsAsync_Timeout_ThrowsException()
        {
            // Arrange
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => service.GetInsightsAsync("context", "question"));
        }

        [Fact]
        public async Task GetInsightsAsync_InvalidApiKey_ThrowsException()
        {
            // Arrange
            _configurationMock.Setup(c => c["XAI:ApiKey"]).Returns("");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateServiceAsync());
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private async Task<XAIService> CreateServiceAsync()
        {
            return await Task.FromResult(CreateService());
        }

        private XAIService CreateService()
        {
            return new XAIService(
                _httpClientFactoryMock.Object,
                _configurationMock.Object,
                _loggerMock.Object,
                _contextServiceMock.Object,
                _aiLoggingServiceMock.Object,
                _memoryCache);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
                _memoryCache?.Dispose();
            }
        }

        ~XAIServiceTests()
        {
            Dispose(false);
        }
    }
}