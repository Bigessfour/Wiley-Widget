using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.E2ETests
{
    public class ChatPanelViewModelIntegrationTests
    {
        [Fact]
        public async Task ProcessMessageAsync_WithValidXaiKey_ProvidesResponse()
        {
            var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? Environment.GetEnvironmentVariable("Grok:ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith("xai-", StringComparison.OrdinalIgnoreCase))
            {
                // No valid xAI API key in environment; skip this integration test.
                return;
            }

            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var grok = new GrokAgentService(config, logger: null);

            // Validate API key before proceeding - skip test if validation fails to avoid intermittent network/invalid-key failures
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var (validated, validationMessage) = await grok.ValidateApiKeyAsync(cts.Token);
            if (!validated)
            {
                // Key exists but is not valid in this environment - skip the integration test
                return;
            }
            // Minimal fake implementations for dependencies
            using var aiService = new FakeAIService();
            var convRepo = new InMemoryConversationRepository();

            using var viewModel = new ChatPanelViewModel(grok, aiService, convRepo, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatPanelViewModel>.Instance);

            // Act: send test message via the generated command
            viewModel.InputText = "Testing. Just say hi and hello world and nothing else.";
            await viewModel.SendMessageCommand.ExecuteAsync(null);

            // Assert: there is an AI message containing 'hi' and 'hello world'
            var aiMsg = viewModel.Messages.LastOrDefault(m => !m.IsUser);
            Assert.NotNull(aiMsg);
            var text = aiMsg.Message?.ToLowerInvariant() ?? string.Empty;
            Assert.True(text.Contains("hi", StringComparison.OrdinalIgnoreCase) && text.Contains("hello world", StringComparison.OrdinalIgnoreCase), $"AI response did not contain expected text: {aiMsg.Message}");
        }

        // Minimal fake AI service used for tests
        private sealed class FakeAIService : IAIService, IDisposable
        {
            public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default) => Task.FromResult(new AIResponseResult(string.Empty, 200));
            public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task UpdateApiKeyAsync(string newApiKey) => Task.CompletedTask;
            public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default) => Task.FromResult(new AIResponseResult(string.Empty, 200));
            public Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default) => Task.FromResult(new AIResponseResult(string.Empty, 200));
            public Task<string> SendMessageAsync(string message, object conversationHistory) => Task.FromResult(string.Empty);
            public void Dispose() { }
        }

        private class InMemoryConversationRepository : IConversationRepository
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _store = new();
            public Task DeleteConversationAsync(string conversationId) { _store.TryRemove(conversationId, out _); return Task.CompletedTask; }
            public Task<object?> GetConversationAsync(string id) => Task.FromResult(_store.TryGetValue(id, out var o) ? o : null as object);
            public Task<List<object>> GetConversationsAsync(int skip, int limit) => Task.FromResult(_store.Values.Skip(skip).Take(limit).ToList());
            public Task SaveConversationAsync(object conversation) { var id = Guid.NewGuid().ToString(); _store[id] = conversation; return Task.CompletedTask; }
        }
    }
}
