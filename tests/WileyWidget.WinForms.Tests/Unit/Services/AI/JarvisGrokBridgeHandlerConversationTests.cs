using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Plugins.System;
using WileyWidget.WinForms.Services.AI;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services.AI
{
    public sealed class JarvisGrokBridgeHandlerConversationTests
    {
        [Fact]
        public async Task FollowUpPrompt_IncludesPriorTurnContext()
        {
            var bridge = new ChatBridgeService(Mock.Of<ILogger<ChatBridgeService>>());
            var aiService = new Mock<IAIService>();
            var personalityService = new Mock<IJARVISPersonalityService>();
            var aiLoggingService = new Mock<IAILoggingService>();
            var logger = Mock.Of<ILogger<JarvisGrokBridgeHandler>>();

            var capturedPrompts = new List<string>();
            var responses = new Queue<string>(new[]
            {
                "First response for prior task.",
                "Follow-up response."
            });

            aiService
                .Setup(service => service.StreamResponseAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns((string prompt, string? systemMessage, CancellationToken cancellationToken) =>
                {
                    capturedPrompts.Add(prompt);
                    return StreamSingleChunk(responses.Dequeue());
                });

            personalityService
                .Setup(service => service.GetSystemPrompt())
                .Returns("System prompt");

            using var handler = new JarvisGrokBridgeHandler(
                bridge,
                aiService.Object,
                personalityService.Object,
                aiLoggingService.Object,
                logger);

            await SendPromptAndWaitAsync(bridge, "Prepare utility budget variance summary.");
            await SendPromptAndWaitAsync(bridge, "complete the task in the previous prompt");

            Assert.Equal(2, capturedPrompts.Count);
            Assert.Contains("Current user request: Prepare utility budget variance summary.", capturedPrompts[0], StringComparison.Ordinal);
            Assert.Contains("Conversation context (most recent last):", capturedPrompts[1], StringComparison.Ordinal);
            Assert.Contains("User: Prepare utility budget variance summary.", capturedPrompts[1], StringComparison.Ordinal);
            Assert.Contains("Assistant: First response for prior task.", capturedPrompts[1], StringComparison.Ordinal);
            Assert.Contains("Current user request: complete the task in the previous prompt", capturedPrompts[1], StringComparison.Ordinal);
        }

        [Fact]
        public async Task FollowUpPrompt_UsesBoundedConversationHistory()
        {
            var bridge = new ChatBridgeService(Mock.Of<ILogger<ChatBridgeService>>());
            var aiService = new Mock<IAIService>();
            var personalityService = new Mock<IJARVISPersonalityService>();
            var aiLoggingService = new Mock<IAILoggingService>();
            var logger = Mock.Of<ILogger<JarvisGrokBridgeHandler>>();

            var capturedPrompts = new List<string>();
            var responses = new Queue<string>();
            for (var i = 1; i <= 8; i++)
            {
                responses.Enqueue($"assistant-response-{i}");
            }

            aiService
                .Setup(service => service.StreamResponseAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns((string prompt, string? systemMessage, CancellationToken cancellationToken) =>
                {
                    capturedPrompts.Add(prompt);
                    return StreamSingleChunk(responses.Dequeue());
                });

            personalityService
                .Setup(service => service.GetSystemPrompt())
                .Returns("System prompt");

            using var handler = new JarvisGrokBridgeHandler(
                bridge,
                aiService.Object,
                personalityService.Object,
                aiLoggingService.Object,
                logger);

            for (var i = 1; i <= 8; i++)
            {
                await SendPromptAndWaitAsync(bridge, $"prompt-{i}");
            }

            Assert.Equal(8, capturedPrompts.Count);

            var finalPrompt = capturedPrompts[7];
            Assert.DoesNotContain("User: prompt-1", finalPrompt, StringComparison.Ordinal);
            Assert.Contains("User: prompt-2", finalPrompt, StringComparison.Ordinal);
            Assert.Contains("User: prompt-7", finalPrompt, StringComparison.Ordinal);
            Assert.Contains("Current user request: prompt-8", finalPrompt, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Prompt_ThroughJarvisBridge_LogsSemanticKernelToolExecution()
        {
            var bridge = new ChatBridgeService(Mock.Of<ILogger<ChatBridgeService>>());
            var personalityService = new Mock<IJARVISPersonalityService>();
            var aiLoggingService = new Mock<IAILoggingService>();
            var grokLogger = Mock.Of<ILogger<GrokAgentService>>();
            var bridgeLogger = Mock.Of<ILogger<JarvisGrokBridgeHandler>>();

            personalityService
                .Setup(service => service.GetSystemPrompt())
                .Returns("System prompt");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["XAI:Model"] = "grok-4-1-fast-reasoning",
                    ["XAI:Endpoint"] = "https://api.x.ai/v1"
                })
                .Build();

            var apiKeyProvider = new Mock<IGrokApiKeyProvider>();
            apiKeyProvider.SetupGet(provider => provider.ApiKey).Returns("test-xai-key-1234567890abcdefghij");
            apiKeyProvider.Setup(provider => provider.GetConfigurationSource()).Returns("unit-test");

            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

            var chatCompletionService = new FakeChatCompletionService(
                CreateStreamingChatMessageContent(null, "TimePlugin.GetCurrentLocalTime"),
                CreateStreamingChatMessageContent("The current local time is 11:30 AM."));

            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(chatCompletionService);
            var kernel = kernelBuilder.Build();
            kernel.ImportPluginFromObject(new TimePlugin(), "TimePlugin");

            var aiService = new GrokAgentService(
                apiKeyProvider: apiKeyProvider.Object,
                config: configuration,
                logger: grokLogger,
                httpClientFactory: httpClientFactory.Object,
                aiLoggingService: aiLoggingService.Object);

            SetPrivateField(aiService, "_kernel", kernel);
            SetPrivateField(aiService, "_isInitialized", true);
            SetPrivateField(aiService, "_initializationFailed", false);
            SetPrivateField(aiService, "_skConnectorDisabled", false);

            var receivedChunks = new List<string>();
            bridge.ResponseChunkReceived += (_, args) => receivedChunks.Add(args.Chunk);

            using var handler = new JarvisGrokBridgeHandler(
                bridge,
                aiService,
                personalityService.Object,
                aiLoggingService.Object,
                bridgeLogger);

            await SendPromptAndWaitAsync(bridge, "What time is it right now?");

            Assert.Contains("The current local time is 11:30 AM.", string.Concat(receivedChunks), StringComparison.Ordinal);
            aiLoggingService.Verify(
                log => log.LogToolExecution(
                    It.Is<string>(query => query.Contains("What time is it right now?", StringComparison.Ordinal)),
                    "SemanticKernel",
                    It.Is<IReadOnlyCollection<string>>(tools => tools.Contains("TimePlugin.GetCurrentLocalTime"))),
                Times.AtLeastOnce);
        }

        private static async Task SendPromptAndWaitAsync(ChatBridgeService bridge, string prompt)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnResponseCompleted(object? _, EventArgs __) => completion.TrySetResult(true);

            bridge.ResponseCompleted += OnResponseCompleted;
            try
            {
                await bridge.RequestExternalPromptAsync(prompt);
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                bridge.ResponseCompleted -= OnResponseCompleted;
            }
        }

        private static async IAsyncEnumerable<string> StreamSingleChunk(string content)
        {
            await Task.Yield();
            yield return content;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(target, value);
        }

        private static StreamingChatMessageContent CreateStreamingChatMessageContent(string? content, string? functionCall = null)
        {
            var constructor = typeof(StreamingChatMessageContent)
                .GetConstructors()
                .OrderByDescending(ctor => ctor.GetParameters().Length)
                .First();

            var parameters = constructor.GetParameters();
            var args = new object?[parameters.Length];
            var metadata = functionCall == null
                ? null
                : new Dictionary<string, object?> { ["FunctionCall"] = functionCall };

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                args[i] = parameter.Name switch
                {
                    "content" => content,
                    "metadata" => metadata,
                    _ => CreateDefaultValue(parameter.ParameterType)
                };
            }

            return (StreamingChatMessageContent)constructor.Invoke(args);
        }

        private static object? CreateDefaultValue(Type parameterType)
        {
            var nullableType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

            if (nullableType == typeof(string))
            {
                return null;
            }

            if (typeof(System.Collections.IDictionary).IsAssignableFrom(nullableType))
            {
                return null;
            }

            var assistantProperty = nullableType.GetProperty("Assistant", BindingFlags.Public | BindingFlags.Static);
            if (assistantProperty != null)
            {
                return assistantProperty.GetValue(null);
            }

            var assistantField = nullableType.GetField("Assistant", BindingFlags.Public | BindingFlags.Static);
            if (assistantField != null)
            {
                return assistantField.GetValue(null);
            }

            return nullableType.IsValueType ? Activator.CreateInstance(nullableType) : null;
        }

        private sealed class FakeChatCompletionService(params StreamingChatMessageContent[] chunks) : IChatCompletionService
        {
            private readonly IReadOnlyList<StreamingChatMessageContent> _chunks = chunks;

            public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

            public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
                ChatHistory chatHistory,
                PromptExecutionSettings? executionSettings = null,
                Kernel? kernel = null,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<ChatMessageContent>>(Array.Empty<ChatMessageContent>());
            }

            public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
                ChatHistory chatHistory,
                PromptExecutionSettings? executionSettings = null,
                Kernel? kernel = null,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                foreach (var chunk in _chunks)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                    yield return chunk;
                }
            }
        }
    }
}
