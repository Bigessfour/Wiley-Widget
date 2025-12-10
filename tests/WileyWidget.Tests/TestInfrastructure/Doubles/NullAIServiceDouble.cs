using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Tests.TestInfrastructure.Doubles;

/// <summary>
/// Test double for IAIService - returns predictable stub responses.
/// </summary>
public class NullAIServiceDouble : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] AI insights disabled in integration tests.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] Data analysis disabled in integration tests.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] Review disabled in integration tests.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] Mock data generation disabled in integration tests.");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Test Stub] AI insights disabled in integration tests.", 200));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Test stub: validation not available.", 200, null, null));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey)
        => ValidateApiKeyAsync(apiKey, CancellationToken.None);

    public Task UpdateApiKeyAsync(string newApiKey)
        => Task.CompletedTask;

    public Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Test Stub] AI prompt disabled in integration tests.", 200, null, null));

    public Task<ChatResponse> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory, CancellationToken ct = default)
        => Task.FromResult(new ChatResponse("[Test Stub] Chat message handling disabled in integration tests."));

    public Task<ToolCallResult> ExecuteToolCallAsync(ToolCall toolCall, CancellationToken ct = default)
        => Task.FromResult(ToolCallResult.Success(toolCall.Id, "[Test Stub] Tool execution disabled in integration tests."));

    public List<object> GetToolDefinitions() => new List<object>();

    public Task<string> GetInsightsWithToolsAsync(string context, string question, List<object>? tools = null, bool includeServerSideTools = true, CancellationToken cancellationToken = default)
        => Task.FromResult("[Test Stub] AI insights with tools disabled in integration tests.");

    public async IAsyncEnumerable<StreamChunk> StreamInsightsAsync(string context, string question, string? previousResponseId = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new StreamChunk("content_delta", "[Test Stub] streaming disabled in integration tests.", ResponseId: previousResponseId ?? "stub");
        await Task.CompletedTask;
    }

    public Task<ToolCallResult> ExecuteClientToolAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
        => Task.FromResult(ToolCallResult.Success(toolCall.Id, "[Test Stub] Client-side tool execution disabled in integration tests."));

    public Task<CollectionUploadResult> UploadToCollectionAsync(string collectionName, IEnumerable<CollectionDocument> documents, CancellationToken cancellationToken = default)
    {
        var count = documents?.Count() ?? 0;
        return Task.FromResult(new CollectionUploadResult(collectionName, count, "Stubbed", null));
    }

    public Task<CollectionSearchResult> SearchCollectionAsync(string collectionName, string query, int maxResults = 5, CancellationToken cancellationToken = default)
        => Task.FromResult(new CollectionSearchResult(query, new CollectionMatch[0], 0));
}
