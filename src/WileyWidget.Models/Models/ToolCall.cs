using System.Collections.Generic;

namespace WileyWidget.Models;

/// <summary>
/// Represents a tool invocation request for xAI tool execution.
/// Matches Python ToolCall dataclass from xai_tool_executor.py
/// </summary>
public record ToolCall(
    string Id,
    string Name,
    Dictionary<string, object> Arguments,
    string ToolType = "client_side_tool"
)
{
    /// <summary>
    /// Creates a new tool call with a generated ID
    /// </summary>
    public static ToolCall Create(string name, Dictionary<string, object> arguments, string toolType = "client_side_tool")
    {
        return new ToolCall(
            Id: $"call_{Guid.NewGuid():N}",
            Name: name,
            Arguments: arguments,
            ToolType: toolType
        );
    }
}

/// <summary>
/// Represents the result of a tool execution
/// </summary>
public record ToolCallResult(
    string ToolCallId,
    string Content,
    bool IsError = false,
    string? ErrorMessage = null
)
{
    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static ToolCallResult Success(string toolCallId, string content)
    {
        return new ToolCallResult(toolCallId, content, IsError: false);
    }

    /// <summary>
    /// Creates an error result
    /// </summary>
    public static ToolCallResult Error(string toolCallId, string errorMessage)
    {
        return new ToolCallResult(toolCallId, string.Empty, IsError: true, ErrorMessage: errorMessage);
    }
}
