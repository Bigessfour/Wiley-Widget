using System;
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
        Task<ToolCallResult> ExecuteToolAsync(ToolCall toolCall);
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
        Task<MunicipalAccount?> GetAccountAsync(int id);
        Task<MunicipalAccount[]> GetAllAccountsAsync();
        Task SaveAccountAsync(MunicipalAccount account);
    }

    /// <summary>
    /// Repository for conversation history.
    /// </summary>
    public interface IConversationRepository
    {
        Task SaveConversationAsync(object conversation);
        Task<object?> GetConversationAsync(string id);
    }

    /// <summary>
    /// Service for AI context extraction.
    /// </summary>
    public interface IAIContextExtractionService
    {
        Task<string> ExtractContextAsync(string input);
    }

    /// <summary>
    /// Repository for activity logging.
    /// </summary>
    public interface IActivityLogRepository
    {
        Task LogActivityAsync(string activity, string details);
    }

    /// <summary>
    /// Service for PDF export operations.
    /// </summary>
    public interface IPdfExportService
    {
        Task ExportToPdfAsync(object data, string filePath);
    }

    /// <summary>
    /// Conversation history model stub.
    /// </summary>
    public class ConversationHistory
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
