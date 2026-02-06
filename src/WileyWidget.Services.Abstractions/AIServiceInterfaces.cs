using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service for AI tool execution and parsing.
    /// </summary>
    public interface IAIAssistantService
    {
        ToolCall? ParseInputForTool(string input);
        Task<ToolCallResult> ExecuteToolAsync(ToolCall toolCall, CancellationToken cancellationToken = default);
        ToolDefinition[] GetAvailableTools();
    }

    /// <summary>
    /// Represents a tool call parsed from user input.
    /// </summary>
    public class ToolCall
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string ToolType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of a tool execution.
    /// </summary>
    public class ToolCallResult
    {
        public bool IsError { get; set; }
        public string? Content { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Definition of an available tool.
    /// </summary>
    public class ToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for AI personality management.
    /// </summary>
    public interface IAIPersonalityService
    {
        string CurrentPersonality { get; }
        void SetPersonality(string personality);
    }

    /// <summary>
    /// Service for financial insights and analysis.
    /// </summary>
    public interface IFinancialInsightsService
    {
        Task<string> GetInsightsAsync(string query, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Service for account data operations.
    /// </summary>
    public interface IAccountService
    {
        Task<MunicipalAccount?> GetAccountAsync(int id, CancellationToken cancellationToken = default);
        Task<MunicipalAccount[]> GetAllAccountsAsync(CancellationToken cancellationToken = default);
        Task<SaveAccountResult> SaveAccountAsync(MunicipalAccount account, CancellationToken cancellationToken = default);
        IEnumerable<string> ValidateAccount(MunicipalAccount account);
    }

    public class SaveAccountResult
    {
        public bool Success { get; set; }
        public IEnumerable<string>? ValidationErrors { get; set; }
    }

    /// <summary>
    /// Repository for conversation history.
    /// </summary>
    public interface IConversationRepository
    {
        Task SaveConversationAsync(object conversation, CancellationToken cancellationToken = default);
        Task<object?> GetConversationAsync(string id, CancellationToken cancellationToken = default);
        Task<List<object>> GetConversationsAsync(int skip, int limit, CancellationToken cancellationToken = default);
        Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Service for AI context extraction.
    /// </summary>
    public interface IAIContextExtractionService
    {
        Task<string> ExtractContextAsync(string input, CancellationToken cancellationToken = default);
        Task ExtractEntitiesAsync(string message, string conversationId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Repository for activity logging.
    /// </summary>
    public interface IActivityLogRepository
    {
        Task LogActivityAsync(string activity, string details, CancellationToken cancellationToken = default);
        Task LogActivityAsync(ActivityLog activityLog, CancellationToken cancellationToken = default);
    }

    public class ActivityLog
    {
        public string ActivityType { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string Severity { get; set; } = "Information";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service for PDF export operations.
    /// </summary>
    public interface IPdfExportService
    {
        Task ExportToPdfAsync(object data, string filePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Conversation history model stub.
    /// </summary>
    public class ConversationHistory
    {
        public string Id { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MessagesJson { get; set; } = string.Empty;
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Provides centralized, production-ready API key management for xAI Grok.
    /// Supports Microsoft-recommended configuration hierarchy (User Secrets → Environment Variables → appsettings.json).
    ///
    /// CONFIGURATION HIERARCHY (highest to lowest priority):
    /// 1. User Secrets: dotnet user-secrets set "XAI:ApiKey" "your-key-here"
    /// 2. Environment Variables (any scope):
    ///    - Recommended: XAI__ApiKey (double underscore per Microsoft convention)
    ///    - Legacy: XAI_API_KEY (single underscore - still supported)
    /// 3. appsettings.json: "XAI": { "ApiKey": "..." } (lowest priority - DO NOT use for secrets!)
    ///
    /// Usage:
    /// 1. Set API key via user secrets: dotnet user-secrets set "XAI:ApiKey" "your-key"
    /// 2. Inject IGrokApiKeyProvider into services
    /// 3. Access key via provider.ApiKey property
    /// </summary>
    public interface IGrokApiKeyProvider
    {
        /// <summary>Gets the API key masked for safe logging (shows first 4 + last 4 chars).</summary>
        string? MaskedApiKey { get; }

        /// <summary>Gets the full API key (private - use only for API calls to Grok endpoint).</summary>
        string? ApiKey { get; }

        /// <summary>Indicates whether the API key has been validated against Grok endpoint at startup.</summary>
        bool IsValidated { get; }

        /// <summary>Indicates whether the API key came from user secrets (secure source) vs. environment or config.</summary>
        bool IsFromUserSecrets { get; }

        /// <summary>Validates the API key by making a test request to xAI Grok /v1/chat/completions endpoint.</summary>
        /// <returns>Tuple of (IsValid, ValidationMessage) for diagnostics.</returns>
        Task<(bool Success, string Message)> ValidateAsync();

        /// <summary>Gets detailed configuration source information for logging and diagnostics.</summary>
        /// <returns>String describing where the API key was loaded from (User Secrets / Environment / Config).</returns>
        string GetConfigurationSource();
    }
}
