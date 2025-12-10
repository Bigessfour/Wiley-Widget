using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// No-op AI service used in development/testing when API keys are not configured.
/// Prevents startup failures by providing predictable stub responses.
/// </summary>
public class NullAIService : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] AI insights are disabled in development. Configure XAI_API_KEY to enable.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] Data analysis is disabled in development.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] Review is disabled in development.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] Mock data generation is disabled in development.");

    public Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Dev Stub] AI insights are disabled in development. Configure XAI_API_KEY to enable.", 200));

    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("Dev stub: validation not available in development.", 403, "AuthFailure", null));

    // Adapter overload to satisfy the Abstractions interface which declares
    // ValidateApiKeyAsync(string) without a CancellationToken parameter.
    public Task<AIResponseResult> ValidateApiKeyAsync(string apiKey)
        => ValidateApiKeyAsync(apiKey, CancellationToken.None);

    public Task UpdateApiKeyAsync(string newApiKey)
    {
        // No-op in dev stub
        return Task.CompletedTask;
    }

    public Task<AIResponseResult> SendPromptAsync(string prompt, System.Threading.CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponseResult("[Dev Stub] AI prompt sending is disabled in development. Configure XAI_API_KEY to enable.", 403, "AuthFailure", null));

    public Task<ChatResponse> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory, CancellationToken ct = default)
        => Task.FromResult(new ChatResponse("[Dev Stub] Chat is disabled in development. Configure XAI_API_KEY to enable."));

    public Task<ToolCallResult> ExecuteToolCallAsync(ToolCall toolCall, CancellationToken ct = default)
        => Task.FromResult(ToolCallResult.Error(toolCall.Id, "Dev stub: tool execution is disabled in development."));

    public List<object> GetToolDefinitions()
        => new List<object>();

    public Task<string> GetInsightsWithToolsAsync(string context, string question, List<object>? tools, bool useTools, CancellationToken cancellationToken = default)
        => Task.FromResult("[Dev Stub] AI insights with tools are disabled in development. Configure XAI_API_KEY to enable.");

    public async IAsyncEnumerable<StreamChunk> StreamInsightsAsync(string context, string question, string? model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Ensure this async iterator contains an await to avoid CS1998 and to behave like a true async stream
        await Task.Yield();
        yield return new StreamChunk("content_delta", "[Dev Stub] Streaming insights are disabled in development.");
    }

    public Task<ToolCallResult> ExecuteClientToolAsync(ToolCall toolCall, CancellationToken ct = default)
        => Task.FromResult(ToolCallResult.Error(toolCall.Id, "Dev stub: client tool execution is disabled in development."));

    public Task<CollectionUploadResult> UploadToCollectionAsync(string collectionName, IEnumerable<CollectionDocument> documents, CancellationToken cancellationToken = default)
        => Task.FromResult(new CollectionUploadResult(collectionName, 0, "Dev stub: upload disabled in development."));

    public Task<CollectionSearchResult> SearchCollectionAsync(string collectionName, string query, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult(new CollectionSearchResult(query, Array.Empty<CollectionMatch>(), 0));
}
