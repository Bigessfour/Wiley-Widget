using System.Text.Json;

namespace WileyWidget.Models;

/// <summary>
/// Represents an xAI tool definition with metadata and parameters
/// </summary>
public record AITool(
    string Name,
    string Description,
    JsonElement Parameters
)
{
    /// <summary>
    /// Available MCP-compliant filesystem tools
    /// Per .vscode/copilot-mcp-rules.md: MUST use mcp_filesystem_* APIs
    /// </summary>
    public static readonly AITool[] AvailableTools =
    [
        new AITool(
            "mcp_filesystem_read_text_file",
            "Read file contents using MCP filesystem API",
            JsonSerializer.SerializeToElement(new { path = new { type = "string", description = "Absolute file path" }, head = new { type = "number", description = "Optional: first N lines" }, tail = new { type = "number", description = "Optional: last N lines" } })
        ),
        new AITool(
            "mcp_filesystem_search_files",
            "Search for files matching pattern using MCP filesystem",
            JsonSerializer.SerializeToElement(new { path = new { type = "string", description = "Directory path" }, pattern = new { type = "string", description = "Search pattern" }, excludePatterns = new { type = "array", description = "Optional: exclude patterns" } })
        ),
        new AITool(
            "mcp_filesystem_list_directory",
            "List directory contents using MCP filesystem",
            JsonSerializer.SerializeToElement(new { path = new { type = "string", description = "Absolute directory path" } })
        ),
        new AITool(
            "mcp_filesystem_read_multiple_files",
            "Read multiple files simultaneously using MCP",
            JsonSerializer.SerializeToElement(new { paths = new { type = "array", description = "Array of absolute file paths" } })
        ),
        new AITool(
            "semantic_search",
            "Semantic code search across workspace",
            JsonSerializer.SerializeToElement(new { query = new { type = "string", description = "Natural language query" } })
        ),
        new AITool(
            "get_errors",
            "Get compilation/lint errors from Problems panel",
            JsonSerializer.SerializeToElement(new { filePaths = new { type = "array", description = "Optional: specific files" } })
        )
    ];
}
