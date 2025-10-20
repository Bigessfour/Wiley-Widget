using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests.UnitTests
{
    public class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public int Calls { get; private set; }

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responder(request));
        }
    }

    public class XAIServiceTests
    {
        private static IConfiguration BuildConfiguration()
        {
            var dict = new System.Collections.Generic.Dictionary<string, string?>()
            {
                ["XAI:ApiKey"] = new string('x', 32),
                ["XAI:BaseUrl"] = "https://api.test.local/",
                ["XAI:TimeoutSeconds"] = "5",
                ["XAI:Model"] = "grok-4-0709",
                ["XAI:MaxConcurrentRequests"] = "3",
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [Fact]
        public async Task GetInsightsAsync_ReturnsContent_WhenApiResponds()
        {
            // Arrange
            var responseObj = new { choices = new[] { new { message = new { content = "Hello" } } } };
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseObj)
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.local/") };
            factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = BuildConfiguration();
            var logger = new NullLogger<XAIService>();
            var contextSvc = new Mock<IWileyWidgetContextService>();
            contextSvc.Setup(c => c.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("system-context");
            var aiLog = new Mock<IAILoggingService>();
            var memory = new MemoryCache(new MemoryCacheOptions());

            var service = new XAIService(factoryMock.Object, config, NullLogger<XAIService>.Instance, contextSvc.Object, aiLog.Object, memory);

            // Act
            var result = await service.GetInsightsAsync("context", "question");

            // Assert
            Assert.Equal("Hello", result);
        }

        [Fact]
        public async Task GetInsightsWithStatusAsync_ReturnsTypedResult_WhenApiReturnsContent()
        {
            // Arrange
            var responseObj = new { choices = new[] { new { message = new { content = "Typed Hello" } } } };
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseObj)
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.local/") };
            factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = BuildConfiguration();
            var contextSvc = new Mock<IWileyWidgetContextService>();
            contextSvc.Setup(c => c.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("system-context");
            var aiLog = new Mock<IAILoggingService>();
            var memory = new MemoryCache(new MemoryCacheOptions());

            var service = new XAIService(factoryMock.Object, config, NullLogger<XAIService>.Instance, contextSvc.Object, aiLog.Object, memory);

            // Act
            var result = await service.GetInsightsWithStatusAsync("ctx", "question");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.HttpStatusCode);
            Assert.Equal("Typed Hello", result.Content);
        }

        [Fact]
        public async Task GetInsightsAsync_RetriesOnRateLimit_ThenSucceeds()
        {
            // Arrange: first response is 429, second is success
            int call = 0;
            var handler = new FakeHandler(req =>
            {
                call++;
                if (call == 1)
                {
                    return new HttpResponseMessage((HttpStatusCode)429)
                    {
                        Content = JsonContent.Create(new { error = (object?)null, choices = Array.Empty<object>() })
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { choices = new[] { new { message = new { content = "Retry Hello" } } } })
                };
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.local/") };
            factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = BuildConfiguration();
            var contextSvc = new Mock<IWileyWidgetContextService>();
            contextSvc.Setup(c => c.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("system-context");
            var aiLog = new Mock<IAILoggingService>();
            var memory = new MemoryCache(new MemoryCacheOptions());

            var service = new XAIService(factoryMock.Object, config, NullLogger<XAIService>.Instance, contextSvc.Object, aiLog.Object, memory);

            // Act
            var result = await service.GetInsightsAsync("context", "question");

            // Assert
            Assert.Equal("Retry Hello", result);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task GetInsightsAsync_ReturnsAuthMessage_On403()
        {
            // Arrange: HTTP 403
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = JsonContent.Create(new { message = "forbidden" })
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.local/") };
            factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = BuildConfiguration();
            var contextSvc = new Mock<IWileyWidgetContextService>();
            contextSvc.Setup(c => c.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("system-context");
            var aiLog = new Mock<IAILoggingService>();
            var memory = new MemoryCache(new MemoryCacheOptions());

            var service = new XAIService(factoryMock.Object, config, NullLogger<XAIService>.Instance, contextSvc.Object, aiLog.Object, memory);

            // Act
            var result = await service.GetInsightsAsync("context", "question");

            // Assert: should return friendly guidance about 403
            Assert.Contains("403", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetInsightsAsync_UsesCache_ForDuplicateRequests()
        {
            // Arrange: single successful response
            var responseObj = new { choices = new[] { new { message = new { content = "CachedValue" } } } };
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseObj)
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.local/") };
            factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = BuildConfiguration();
            var contextSvc = new Mock<IWileyWidgetContextService>();
            contextSvc.Setup(c => c.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("system-context");
            var aiLog = new Mock<IAILoggingService>();
            var memory = new MemoryCache(new MemoryCacheOptions());

            var service = new XAIService(factoryMock.Object, config, NullLogger<XAIService>.Instance, contextSvc.Object, aiLog.Object, memory);

            // Act: call twice
            var first = await service.GetInsightsAsync("ctx", "q");
            var second = await service.GetInsightsAsync("ctx", "q");

            // Assert: same value and only one HTTP call
            Assert.Equal("CachedValue", first);
            Assert.Equal("CachedValue", second);
            Assert.Equal(1, handler.Calls);
        }
    }
}
