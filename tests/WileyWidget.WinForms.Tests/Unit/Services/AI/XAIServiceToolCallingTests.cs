using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services.AI
{
    public sealed class XAIServiceToolCallingTests
    {
        [Fact]
        public async Task SendPromptAsync_IncludesToolsAndToolChoice_WhenEnabled()
        {
            var config = BuildConfiguration(new Dictionary<string, string?>
            {
                ["XAI:Tools:Enabled"] = "true",
                ["XAI:Tools:WebSearch:Enabled"] = "true",
                ["XAI:Tools:CodeExecution:Enabled"] = "true",
                ["XAI:Tools:XSearch:Enabled"] = "false",
                ["XAI:Tools:CollectionsSearch:Enabled"] = "true"
            });

            var handler = new RecordingHandler
            {
                ResponseContent = "{\"output\":[{\"content\":[{\"text\":\"ok\"}]}]}"
            };

            var logger = new Mock<ILogger<XAIService>>();
            using var service = CreateService(config, handler, logger.Object);

            var result = await service.SendPromptAsync("test prompt");

            Assert.Equal(200, result.HttpStatusCode);
            Assert.Single(handler.Requests);

            using var bodyJson = JsonDocument.Parse(handler.Requests[0].Body);
            JsonElement root = bodyJson.RootElement;

            Assert.True(root.TryGetProperty("tools", out var toolsElement));
            Assert.True(root.TryGetProperty("tool_choice", out var toolChoiceElement));
            Assert.Equal("auto", toolChoiceElement.GetString());

            var toolTypes = toolsElement.EnumerateArray()
                .Select(t => t.GetProperty("type").GetString())
                .Where(t => t != null)
                .ToList();

            Assert.Contains("web_search", toolTypes);
            Assert.Contains("code_execution", toolTypes);
            Assert.Contains("collections_search", toolTypes);
            Assert.DoesNotContain("x_search", toolTypes);
        }

        [Fact]
        public async Task SendPromptAsync_OmitsTools_WhenToolsDisabled()
        {
            var config = BuildConfiguration(new Dictionary<string, string?>
            {
                ["XAI:Tools:Enabled"] = "false"
            });

            var handler = new RecordingHandler
            {
                ResponseContent = "{\"output\":[{\"content\":[{\"text\":\"ok\"}]}]}"
            };

            var logger = new Mock<ILogger<XAIService>>();
            using var service = CreateService(config, handler, logger.Object);

            await service.SendPromptAsync("test prompt");

            Assert.Single(handler.Requests);

            using var bodyJson = JsonDocument.Parse(handler.Requests[0].Body);
            JsonElement root = bodyJson.RootElement;

            Assert.False(root.TryGetProperty("tools", out _));
            Assert.False(root.TryGetProperty("tool_choice", out _));
        }

        [Fact]
        public async Task SendPromptAsync_UsesCollectionsSearchSetting_ForFileSearchTool()
        {
            var config = BuildConfiguration(new Dictionary<string, string?>
            {
                ["XAI:Tools:Enabled"] = "true",
                ["XAI:Tools:WebSearch:Enabled"] = "false",
                ["XAI:Tools:CodeExecution:Enabled"] = "false",
                ["XAI:Tools:XSearch:Enabled"] = "false",
                ["XAI:Tools:CollectionsSearch:Enabled"] = "true"
            });

            var handler = new RecordingHandler
            {
                ResponseContent = "{\"output\":[{\"content\":[{\"text\":\"ok\"}]}]}"
            };

            var logger = new Mock<ILogger<XAIService>>();
            using var service = CreateService(config, handler, logger.Object);

            await service.SendPromptAsync("test prompt");

            Assert.Single(handler.Requests);

            using var bodyJson = JsonDocument.Parse(handler.Requests[0].Body);
            JsonElement root = bodyJson.RootElement;
            Assert.True(root.TryGetProperty("tools", out var toolsElement));
            Assert.Single(toolsElement.EnumerateArray());
            Assert.Equal("collections_search", toolsElement[0].GetProperty("type").GetString());
        }

        [Fact]
        public async Task StreamResponseAsync_ParsesToolCalls_AndLogsDetection()
        {
            var config = BuildConfiguration(new Dictionary<string, string?>
            {
                ["XAI:Tools:Enabled"] = "true",
                ["XAI:Tools:WebSearch:Enabled"] = "true",
                ["XAI:Tools:CodeExecution:Enabled"] = "false",
                ["XAI:Tools:XSearch:Enabled"] = "false",
                ["XAI:Tools:CollectionsSearch:Enabled"] = "false"
            });

            const string streamPayload =
                "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"web_search\",\"arguments\":\"{\\\"query\\\":\\\"henry hub natural gas price\\\"}\"}}]}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"Final answer.\"}}]}\n\n" +
                "data: [DONE]\n\n";

            var handler = new RecordingHandler
            {
                MediaType = "text/event-stream",
                ResponseContent = streamPayload
            };

            var logger = new Mock<ILogger<XAIService>>();
            using var service = CreateService(config, handler, logger.Object);

            var chunks = new List<string>();
            await foreach (var chunk in service.StreamResponseAsync("natural gas prompt", "system context", CancellationToken.None))
            {
                chunks.Add(chunk);
            }

            Assert.Contains("Final answer.", chunks);
            Assert.Single(handler.Requests);

            using (var bodyJson = JsonDocument.Parse(handler.Requests[0].Body))
            {
                JsonElement root = bodyJson.RootElement;
                Assert.True(root.TryGetProperty("input", out _));
                Assert.False(root.TryGetProperty("messages", out _));
                Assert.True(root.TryGetProperty("tools", out _));
            }

            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("xAI stream tool call detected", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private static IConfiguration BuildConfiguration(Dictionary<string, string?>? overrides = null)
        {
            var values = new Dictionary<string, string?>
            {
                ["XAI:Enabled"] = "true",
                ["XAI:ApiKey"] = "xai_test_key_12345678901234567890",
                ["XAI:Endpoint"] = "https://api.x.ai/v1",
                ["XAI:Model"] = "grok-4-1-fast-reasoning",
                ["XAI:Temperature"] = "0.3",
                ["XAI:MaxTokens"] = "256",
                ["XAI:TimeoutSeconds"] = "15",
                ["XAI:MaxConcurrentRequests"] = "2"
            };

            if (overrides != null)
            {
                foreach (var kvp in overrides)
                {
                    values[kvp.Key] = kvp.Value;
                }
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        private static XAIService CreateService(IConfiguration config, HttpMessageHandler handler, ILogger<XAIService> logger)
        {
            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory
                .Setup(factory => factory.CreateClient("GrokClient"))
                .Returns(new HttpClient(handler));

            var contextService = new Mock<IWileyWidgetContextService>();
            contextService
                .Setup(service => service.BuildCurrentSystemContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("test-system-context");

            var aiLoggingService = new Mock<IAILoggingService>();
            var cache = new MemoryCache(new MemoryCacheOptions());

            return new XAIService(
                httpClientFactory.Object,
                config,
                logger,
                contextService.Object,
                aiLoggingService.Object,
                cache,
                telemetryService: null,
                jarvisPersonality: null);
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
            public string ResponseContent { get; set; } = "{}";
            public string MediaType { get; set; } = "application/json";
            public List<CapturedRequest> Requests { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                Requests.Add(new CapturedRequest(
                    request.Method.Method,
                    request.RequestUri?.ToString(),
                    body));

                return new HttpResponseMessage(StatusCode)
                {
                    Content = new StringContent(ResponseContent, Encoding.UTF8, MediaType)
                };
            }
        }

        private sealed record CapturedRequest(string Method, string? Uri, string Body);
    }
}
