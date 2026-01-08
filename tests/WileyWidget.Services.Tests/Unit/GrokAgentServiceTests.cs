using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services.AI;
using Xunit;

namespace WileyWidget.Services.Tests.Unit
{
    public class GrokAgentServiceTests
    {
        [Fact]
        public async Task AutoSelectModelAsync_UsesDiscoveryService_WhenAvailable()
        {
#pragma warning disable CA2000 // Mock objects not disposed in test; acceptable for unit test scope
            var mockDiscovery = new Mock<IXaiModelDiscoveryService>();
            var descriptor = new XaiModelDescriptor("grok-4-0709", new[] { "grok-4" }, null, null, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "1");
            mockDiscovery.Setup(s => s.ChooseBestModelAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(descriptor);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Grok:Model", "grok-4" } }).Build();

            var service = new GrokAgentService(config, logger: null, httpClientFactory: null, modelDiscoveryService: mockDiscovery.Object);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var selected = await service.AutoSelectModelAsync(cts.Token);

            selected.Should().Be(descriptor.Id);
            mockDiscovery.Verify(s => s.ChooseBestModelAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AutoSelectModelAsync_FallsBackToModelsEndpoint_WhenDiscoveryUnavailable()
        {
#pragma warning disable CA2000 // Handler and client not disposed in test; acceptable for unit test scope
            var handler = new FakeMessageHandler((request, ct) =>
            {
                if (request.Method == HttpMethod.Get && (request.RequestUri?.AbsolutePath?.EndsWith("/models", StringComparison.Ordinal) ?? false))
                {
                    var json = "{\"data\":[{\"id\":\"grok-4-0709\"}]}";
                    var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                    return Task.FromResult(resp);
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/v1/") };
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Grok:Model", "grok-4" } }).Build();

            var service = new GrokAgentService(config, logger: null, httpClientFactory: mockFactory.Object, modelDiscoveryService: null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var selected = await service.AutoSelectModelAsync(cts.Token);

            selected.Should().Be("grok-4-0709");
        }

        [Fact]
        public async Task RunAgentAsync_FallsBackToSimple_WhenKernelNotInitialized()
        {
#pragma warning disable CA2000 // Handler and client not disposed in test; acceptable for unit test scope
            var handler = new FakeMessageHandler((request, ct) =>
            {
                if (request.Method == HttpMethod.Post && (request.RequestUri?.AbsolutePath?.EndsWith("/chat/completions", StringComparison.Ordinal) ?? false))
                {
                    var json = "{\"choices\":[{\"message\":{\"content\":\"hello from simple\"}}]}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/v1/") };
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Grok:ApiKey", "testkey" }, { "Grok:Model", "grok-4" } }).Build();

            var service = new GrokAgentService(config, logger: null, httpClientFactory: mockFactory.Object, modelDiscoveryService: null);

            var resp = await service.RunAgentAsync("hello");
            resp.Should().Be("hello from simple");
        }

        [Fact]
        public async Task RunAgentAsync_UsesAgentAndPlugins_WhenKernelInitialized()
        {
#pragma warning disable CA2000 // Handler and client not disposed in test; acceptable for unit test scope
            var handler = new FakeMessageHandler((request, ct) =>
            {
                if (request.Method == HttpMethod.Post && (request.RequestUri?.AbsolutePath?.EndsWith("/chat/completions", StringComparison.Ordinal) ?? false))
                {
                    var json = "{\"choices\":[{\"message\":{\"content\":\"hello from agent\"}}]}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/v1/") };
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Grok:ApiKey", "testkey" }, { "Grok:Model", "grok-4" } }).Build();

            var service = new GrokAgentService(config, logger: null, httpClientFactory: mockFactory.Object, modelDiscoveryService: null);

            // Initialize the kernel and auto-register plugins from the assembly
            await service.InitializeAsync(CancellationToken.None);

            service.IsInitialized.Should().BeTrue();
            service.Kernel.Should().NotBeNull();
            service.Kernel!.Plugins.Should().NotBeEmpty();

            var pluginNames = service.Kernel!.Plugins.Select(p => p.Name).ToList();
            pluginNames.Any(n => n.IndexOf("Echo", StringComparison.OrdinalIgnoreCase) >= 0).Should().BeTrue("EchoPlugin should be auto-registered");

            var resp = await service.RunAgentAsync("hello");
            resp.Should().Be("hello from agent");
        }

        [Fact]
        public async Task GetStreamingResponseAsync_SetsAcceptHeaderAndParsesSseChunks()
        {
#pragma warning disable CA2000 // Handler and client not disposed in test; acceptable for unit test scope
            var sseContent =
                "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\" world\"}}]}\n\n" +
                "data: [DONE]\n\n";

            var handler = new FakeMessageHandler((request, ct) =>
            {
                // Ensure Accept header includes text/event-stream
                (request.Headers.Accept?.Any(h => string.Equals(h.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase)) ?? false).Should().BeTrue();
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream")
                };
                return Task.FromResult(resp);
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/v1/") };
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Grok:ApiKey", "testkey" }, { "Grok:Model", "grok-4" } }).Build();

            var service = new GrokAgentService(config, logger: null, httpClientFactory: mockFactory.Object, modelDiscoveryService: null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await service.GetStreamingResponseAsync("say hi", "sys", ct: cts.Token);

            result.Should().Be("Hello world");
        }

        [Fact]
        public async Task GetSimpleResponse_OmitsPresencePenalty_ForReasoningModel()
        {
#pragma warning disable CA2000 // Handler and client not disposed in test; acceptable for unit test scope
            var handler = new FakeMessageHandler((request, ct) =>
            {
                var body = request.Content?.ReadAsStringAsync().Result ?? string.Empty;
                body.Should().NotContain("presence_penalty");
                var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/v1/") };
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Grok:ApiKey", "testkey" }, { "Grok:Model", "grok-4-1-fast-reasoning" }, { "Grok:DefaultPresencePenalty", "0.5" } }).Build();

            var service = new GrokAgentService(config, logger: null, httpClientFactory: mockFactory.Object, modelDiscoveryService: null);

            var resp = await service.GetSimpleResponse("hello");
            resp.Should().Be("ok");
        }

        [Fact]
        public async Task GetSimpleResponse_IncludesPresencePenalty_ForNonReasoningModel()
        {
#pragma warning disable CA2000 // Handler and client not disposed in test; acceptable for unit test scope
            var handler = new FakeMessageHandler((request, ct) =>
            {
                var body = request.Content?.ReadAsStringAsync().Result ?? string.Empty;
                body.Should().Contain("presence_penalty");
                var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            });

            var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.x.ai/v1/") };
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Grok:ApiKey", "testkey" }, { "Grok:Model", "grok-4" }, { "Grok:DefaultPresencePenalty", "0.5" } }).Build();

            var service = new GrokAgentService(config, logger: null, httpClientFactory: mockFactory.Object, modelDiscoveryService: null);

            var resp = await service.GetSimpleResponse("hello");
            resp.Should().Be("ok");
        }

        private class FakeMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public FakeMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => _handler(request, cancellationToken);
        }
    }
}
