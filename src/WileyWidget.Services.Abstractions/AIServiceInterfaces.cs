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
    /// <summary>
    /// Represents a interface for iaiassistantservice.
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
    /// <summary>
    /// Represents a class for toolcall.
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the arguments.
        /// </summary>
        public string Arguments { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the tooltype.
        /// </summary>
        public string ToolType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of a tool execution.
    /// </summary>
    /// <summary>
    /// Represents a class for toolcallresult.
    /// </summary>
    public class ToolCallResult
    {
        /// <summary>
        /// Gets or sets the iserror.
        /// </summary>
        public bool IsError { get; set; }
        public string? Content { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Definition of an available tool.
    /// </summary>
    /// <summary>
    /// Represents a class for tooldefinition.
    /// </summary>
    public class ToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for AI personality management.
    /// </summary>
    /// <summary>
    /// Represents a interface for iaipersonalityservice.
    /// </summary>
    public interface IAIPersonalityService
    {
        string CurrentPersonality { get; }
        void SetPersonality(string personality);
    }

    /// <summary>
    /// Service for financial insights and analysis.
    /// </summary>
    /// <summary>
    /// Represents a interface for ifinancialinsightsservice.
    /// </summary>
    public interface IFinancialInsightsService
    {
        Task<string> GetInsightsAsync(string query, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Service for account data operations.
    /// </summary>
    /// <summary>
    /// Represents a interface for iaccountservice.
    /// </summary>
    public interface IAccountService
    {
        Task<MunicipalAccount?> GetAccountAsync(int id);
        Task<MunicipalAccount[]> GetAllAccountsAsync();
        Task SaveAccountAsync(MunicipalAccount account);
        Task<SaveAccountResult> SaveAccountAsync(MunicipalAccount account, CancellationToken cancellationToken);
        IEnumerable<string> ValidateAccount(MunicipalAccount account);
    }
    /// <summary>
    /// Represents a class for saveaccountresult.
    /// </summary>

    public class SaveAccountResult
    {
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        public bool Success { get; set; }
        public IEnumerable<string>? ValidationErrors { get; set; }
    }

    /// <summary>
    /// Repository for conversation history.
    /// </summary>
    /// <summary>
    /// Represents a interface for iconversationrepository.
    /// </summary>
    public interface IConversationRepository
    {
        Task SaveConversationAsync(object conversation);
        Task<object?> GetConversationAsync(string id);
        Task<List<object>> GetConversationsAsync(int skip, int limit);
        Task DeleteConversationAsync(string conversationId);
    }

    /// <summary>
    /// Service for AI context extraction.
    /// </summary>
    /// <summary>
    /// Represents a interface for iaicontextextractionservice.
    /// </summary>
    public interface IAIContextExtractionService
    {
        Task<string> ExtractContextAsync(string input);
        Task ExtractEntitiesAsync(string message, string conversationId);
    }

    /// <summary>
    /// Repository for activity logging.
    /// </summary>
    /// <summary>
    /// Represents a interface for iactivitylogrepository.
    /// </summary>
    public interface IActivityLogRepository
    {
        Task LogActivityAsync(string activity, string details);
        Task LogActivityAsync(ActivityLog activityLog);
    }
    /// <summary>
    /// Represents a class for activitylog.
    /// </summary>

    public class ActivityLog
    {
        /// <summary>
        /// Gets or sets the activitytype.
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the activity.
        /// </summary>
        public string Activity { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the details.
        /// </summary>
        public string Details { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the user.
        /// </summary>
        public string User { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the entitytype.
        /// </summary>
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        /// <summary>
        /// Gets or sets the severity.
        /// </summary>
        public string Severity { get; set; } = "Information";
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service for PDF export operations.
    /// </summary>
    /// <summary>
    /// Represents a interface for ipdfexportservice.
    /// </summary>
    public interface IPdfExportService
    {
        Task ExportToPdfAsync(object data, string filePath);
    }

    /// <summary>
    /// Conversation history model stub.
    /// </summary>
    /// <summary>
    /// Represents a class for conversationhistory.
    /// </summary>
    public class ConversationHistory
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the conversationid.
        /// </summary>
        public string ConversationId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the messagesjson.
        /// </summary>
        public string MessagesJson { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the messagecount.
        /// </summary>
        public int MessageCount { get; set; }
        /// <summary>
        /// Gets or sets the createdat.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the updatedat.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
