using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Interface for AI services providing insights and analysis
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// Get AI insights for the provided context and question
        /// </summary>
        Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyze data and provide insights
        /// </summary>
        Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Review application areas and provide recommendations
        /// </summary>
        Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate mock data suggestions
        /// </summary>
        Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get AI insights along with a status code and machine-friendly error code when applicable.
        /// This allows the UI to distinguish network/auth/rate-limit errors from valid responses.
        /// </summary>
        Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate an API key by performing a lightweight request against the provider using the supplied key.
        /// This allows the UI to check arbitrary keys entered by the user without changing runtime configuration.
        /// </summary>
        Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update the runtime API key used by the service (updates internal HttpClient headers).
        /// Useful after rotating/persisting a new key so the running service uses it immediately.
        /// </summary>
        Task UpdateApiKeyAsync(string newApiKey);

        /// <summary>
        /// Sends a raw prompt to the AI provider and returns a structured response result.
        /// Implementations may return additional context in AIResponseResult.
        /// </summary>
        Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a user message with conversation history to the AI provider and returns a ChatResponse.
        /// This method handles the full chat flow including tool call detection and resolution.
        /// Conversation history is maintained by the caller.
        /// </summary>
        Task<ChatResponse> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory, CancellationToken ct = default);

        /// <summary>
        /// Executes a tool call and returns the result.
        /// Tool calls are detected by the AI provider when sending messages with available tools.
        /// </summary>
        Task<ToolCallResult> ExecuteToolCallAsync(ToolCall toolCall, CancellationToken ct = default);

        /// <summary>
        /// Get tool definitions for function calling integration.
        /// Returns a list of tool definitions that can be sent to the AI provider.
        /// </summary>
        List<object> GetToolDefinitions();

        /// <summary>
        /// Enhanced GetInsightsAsync with xAI agentic tool calling support.
        /// Implements the official xAI tool calling pattern with automatic server-side execution.
        /// Reference: https://docs.x.ai/docs/guides/tools/overview
        /// </summary>
        /// <param name="context">Context for the AI query</param>
        /// <param name="question">User question</param>
        /// <param name="tools">Optional: Client-side tool definitions. If null, uses GetToolDefinitions()</param>
        /// <param name="includeServerSideTools">Include xAI server-side tools (web_search, x_search)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<string> GetInsightsWithToolsAsync(
            string context,
            string question,
            List<object>? tools = null,
            bool includeServerSideTools = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream AI responses with real-time tool call notifications.
        /// Yields chunks as they arrive from the xAI API.
        /// </summary>
        /// <param name="context">Context for the AI query</param>
        /// <param name="question">User question</param>
        /// <param name="onChunk">Callback invoked for each content chunk</param>
        /// <param name="onToolCall">Callback invoked when tool call is detected</param>
        /// <param name="previousResponseId">Optional: Previous response ID for multi-turn conversations</param>
        /// <param name="cancellationToken">Cancellation token</param>
        IAsyncEnumerable<StreamChunk> StreamInsightsAsync(
            string context,
            string question,
            string? previousResponseId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute client-side tool call using IAIAssistantService integration.
        /// Returns structured result that can be sent back to xAI for completion.
        /// </summary>
        Task<ToolCallResult> ExecuteClientToolAsync(ToolCall toolCall, CancellationToken cancellationToken = default);

        /// <summary>
        /// Upload documents to xAI collections for RAG functionality.
        /// Reference: https://docs.x.ai/docs/guides/tools/advanced-usage#collections-search
        /// </summary>
        /// <param name="collectionName">Name of the collection to create/update</param>
        /// <param name="documents">Documents to upload (text content with metadata)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CollectionUploadResult> UploadToCollectionAsync(
            string collectionName,
            IEnumerable<CollectionDocument> documents,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Search xAI collections using natural language query.
        /// Returns relevant document chunks with similarity scores.
        /// </summary>
        Task<CollectionSearchResult> SearchCollectionAsync(
            string collectionName,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Typed result for AI responses that includes status and machine code for UI handling
    /// </summary>
    public record AIResponseResult(string Content, int HttpStatusCode = 200, string? ErrorCode = null, string? RawErrorBody = null);

    /// <summary>
    /// Response from a chat message sent to the AI provider.
    /// Includes the content and any extracted insights or tool calls.
    /// </summary>
    public record ChatResponse(
        string Content,
        AIInsight[]? Insights = null,
        ToolCall[]? ToolCalls = null
    );

    /// <summary>
    /// Streaming chunk from xAI API (SSE format)
    /// </summary>
    public record StreamChunk(
        string Type,  // "content_delta", "tool_call", "done"
        string? Content = null,
        ToolCall? ToolCall = null,
        string? ResponseId = null,
        Dictionary<string, int>? Usage = null
    );

    /// <summary>
    /// Document for xAI collections upload
    /// </summary>
    public record CollectionDocument(
        string Id,
        string Content,
        Dictionary<string, string>? Metadata = null
    );

    /// <summary>
    /// Result from collection upload operation
    /// </summary>
    public record CollectionUploadResult(
        string CollectionId,
        int DocumentsUploaded,
        string Status,
        string? ErrorMessage = null
    );

    /// <summary>
    /// Result from collection search operation
    /// </summary>
    public record CollectionSearchResult(
        string Query,
        CollectionMatch[] Matches,
        int TotalMatches
    );

    /// <summary>
    /// Single match from collection search
    /// </summary>
    public record CollectionMatch(
        string DocumentId,
        string Content,
        double Score,
        Dictionary<string, string>? Metadata = null
    );
}
