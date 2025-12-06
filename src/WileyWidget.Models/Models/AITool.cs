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
    /// Available MCP-compliant tools
    /// </summary>
    public static readonly AITool[] AvailableTools =
    [
        new AITool(
            "read_file",
            "Read file contents using MCP filesystem",
            JsonSerializer.SerializeToElement(new { path = new { type = "string", description = "File path" } })
        ),
        new AITool(
            "grep_search",
            "Search for patterns in codebase",
            JsonSerializer.SerializeToElement(new { query = new { type = "string", description = "Search query" }, isRegexp = new { type = "boolean", description = "Use regex" } })
        ),
        new AITool(
            "file_search",
            "Search for files by pattern",
            JsonSerializer.SerializeToElement(new { pattern = new { type = "string", description = "File pattern" } })
        ),
        new AITool(
            "semantic_search",
            "Semantic code search across workspace",
            JsonSerializer.SerializeToElement(new { query = new { type = "string", description = "Natural language query" } })
        ),
        new AITool(
            "list_directory",
            "List directory contents",
            JsonSerializer.SerializeToElement(new { path = new { type = "string", description = "Directory path" } })
        ),
        new AITool(
            "get_errors",
            "Get compilation/lint errors",
            JsonSerializer.SerializeToElement(new { filePath = new { type = "string", description = "File path (optional)" } })
        )
    ];
}
