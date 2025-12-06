using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for executing xAI tool calls and managing AI assistant interactions
/// </summary>
public interface IAIAssistantService
{
    /// <summary>
    /// Execute a tool call asynchronously
    /// </summary>
    System.Threading.Tasks.Task<ToolCallResult> ExecuteToolAsync(ToolCall toolCall, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse user input to detect tool invocation intent
    /// </summary>
    ToolCall? ParseInputForTool(string input);

    /// <summary>
    /// Get available tools
    /// </summary>
    System.Collections.Generic.IReadOnlyList<AITool> GetAvailableTools();

    /// <summary>
    /// Format tool call as JSON for MCP execution
    /// </summary>
    string FormatToolCallJson(ToolCall toolCall);
}
