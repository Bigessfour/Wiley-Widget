using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.TestUtilities;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Integration.Tests.ExternalServices
{
    public class XAIIntegrationTests
    {
        private static IConfiguration BuildConfig() => new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string,string>("XAI:ApiKey", new string('x', 40)),
                new KeyValuePair<string,string>("XAI:BaseUrl", "https://api.x.ai/v1/"),
                new KeyValuePair<string,string>("XAI:TimeoutSeconds", "10")
            })
            .Build();

        [Fact]
        [Trait("Category", "Integration")]
        public async Task XAIService_GetInsightsAsync_ReturnsContent_OnSuccess()
        {
            // Arrange
            var config = BuildConfig();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var aiLogMock = new Mock<IAILoggingService>();
            var ctxMock = new Mock<IWileyWidgetContextService>();
            ctxMock.Setup(x => x.BuildCurrentSystemContextAsync(default)).ReturnsAsync("system-context");

            var responseJson = "{\"choices\":[{\"message\":{\"content\":\"Insight text\"}}]}";
            var httpClient = TestHelpers.CreateHttpClient((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") }));

            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(x => x.CreateClient("AIServices")).Returns(httpClient);

            var svc = new XAIService(httpFactoryMock.Object, config, new LoggerFactory().CreateLogger<XAIService>(), ctxMock.Object, aiLogMock.Object, memoryCache);

            // Act
            var result = await svc.GetInsightsAsync("ctx", "question");

            // Assert
            result.Should().Be("Insight text");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task XAIService_GetInsightsAsync_ReturnsRateLimitMessage_On429()
        {
            // Arrange
            var config = BuildConfig();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var aiLogMock = new Mock<IAILoggingService>();
            var ctxMock = new Mock<IWileyWidgetContextService>();
            ctxMock.Setup(x => x.BuildCurrentSystemContextAsync(default)).ReturnsAsync("system-context");

            var httpClient = TestHelpers.CreateHttpClient((req, ct) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("Too many requests") }));

            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(x => x.CreateClient("AIServices")).Returns(httpClient);

            var svc = new XAIService(httpFactoryMock.Object, config, new LoggerFactory().CreateLogger<XAIService>(), ctxMock.Object, aiLogMock.Object, memoryCache);

            // Act
            var result = await svc.GetInsightsAsync("ctx", "question");

            // Assert
            result.Should().Be("AI service is rate limiting requests. Please try again shortly.");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task XAIService_ValidateApiKeyAsync_Returns403Result_OnForbidden()
        {
            // Arrange
            var config = BuildConfig();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var aiLogMock = new Mock<IAILoggingService>();
            var ctxMock = new Mock<IWileyWidgetContextService>();
            ctxMock.Setup(x => x.BuildCurrentSystemContextAsync(default)).ReturnsAsync("system-context");

            var httpClient = TestHelpers.CreateHttpClient((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("Forbidden") }));

            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(x => x.CreateClient("AIServices")).Returns(httpClient);

            var svc = new XAIService(httpFactoryMock.Object, config, new LoggerFactory().CreateLogger<XAIService>(), ctxMock.Object, aiLogMock.Object, memoryCache);

            // Act
            var res = await svc.ValidateApiKeyAsync("bad-key");

            // Assert
            res.HttpStatusCode.Should().Be((int)HttpStatusCode.Forbidden);
            res.Content.Should().Contain("Forbidden");
        }
    }
}
