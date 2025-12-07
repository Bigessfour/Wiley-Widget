using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Service for executing xAI tool calls via Python bridge to xai_tool_executor.py
/// Implements MCP-compliant tool execution with Polly resilience patterns
/// </summary>
public partial class AIAssistantService : IAIAssistantService
{
    private readonly ILogger<AIAssistantService> _logger;
    private readonly string _pythonExecutablePath;
    private readonly string _toolExecutorScriptPath;
    private readonly SemaphoreSlim _concurrencySemaphore;

    [GeneratedRegex(@"(read|grep|search|list|get errors?)\s+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ToolDetectionRegex();

    public AIAssistantService(ILogger<AIAssistantService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Resolve paths relative to workspace root
        var workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
        _toolExecutorScriptPath = Path.Combine(workspaceRoot, @"scripts\tools\xai_tool_executor.py");
        _pythonExecutablePath = "python"; // Assume python in PATH; could be configured

        _concurrencySemaphore = new SemaphoreSlim(5, 5); // Limit concurrent tool executions

        if (!File.Exists(_toolExecutorScriptPath))
        {
            _logger.LogWarning("Tool executor script not found at {Path}", _toolExecutorScriptPath);
        }
    }

    /// <inheritdoc />
    public async Task<ToolCallResult> ExecuteToolAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        if (toolCall == null) throw new ArgumentNullException(nameof(toolCall));

        try
        {
            await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown or explicit cancellation - handle gracefully
            _logger.LogDebug("Tool execution canceled before starting: {ToolName}", toolCall.Name);
            return ToolCallResult.Error(toolCall.Id, "Operation canceled");
        }
        try
        {
            _logger.LogInformation("Executing tool {ToolName} with ID {ToolCallId}", toolCall.Name, toolCall.Id);

            var toolCallJson = FormatToolCallJson(toolCall);

            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonExecutablePath,
                Arguments = $"\"{_toolExecutorScriptPath}\" --tool-call \"{EscapeJsonForCmdLine(toolCallJson)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // TODO: Extract timeout to IConfiguration - currently hard-coded 30 seconds
            // Suggested appsettings.json: "AI": { "ToolExecutionTimeoutSeconds": 30 }
            const int timeoutSeconds = 30;
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
            var processTask = Task.Run(() => process.WaitForExit(), cancellationToken);

            var completedTask = await Task.WhenAny(processTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill(entireProcessTree: true);
                var errorMessage = $"Tool execution timed out after {timeoutSeconds} seconds";
                _logger.LogError(errorMessage);
                return ToolCallResult.Error(toolCall.Id, errorMessage);
            }

            var output = outputBuilder.ToString();
            var errors = errorBuilder.ToString();

            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(errors))
            {
                _logger.LogError("Tool execution failed with exit code {ExitCode}. Errors: {Errors}",
                    process.ExitCode, errors);
                return ToolCallResult.Error(toolCall.Id, errors);
            }

            _logger.LogInformation("Tool {ToolName} executed successfully", toolCall.Name);
            return ToolCallResult.Success(toolCall.Id, output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);
            return ToolCallResult.Error(toolCall.Id, ex.Message);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    /// <inheritdoc />
    public ToolCall? ParseInputForTool(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var match = ToolDetectionRegex().Match(input);
        if (!match.Success)
            return null;

        var action = match.Groups[1].Value.ToLowerInvariant();
        var argument = match.Groups[2].Value.Trim();

        var toolName = action switch
        {
            "read" => "read_file",
            "grep" => "grep_search",
            "search" => "semantic_search",
            "list" => "list_directory",
            "get errors" or "errors" => "get_errors",
            _ => null
        };

        if (toolName == null)
            return null;

        var arguments = new Dictionary<string, object>();

        switch (toolName)
        {
            case "read_file":
                arguments["path"] = argument;
                break;
            case "grep_search":
                arguments["query"] = argument;
                arguments["isRegexp"] = false;
                break;
            case "semantic_search":
                arguments["query"] = argument;
                break;
            case "list_directory":
                arguments["path"] = argument;
                break;
            case "get_errors":
                if (!string.IsNullOrWhiteSpace(argument))
                    arguments["filePath"] = argument;
                break;
        }

        return ToolCall.Create(toolName, arguments);
    }

    /// <inheritdoc />
    public IReadOnlyList<AITool> GetAvailableTools()
    {
        return AITool.AvailableTools;
    }

    /// <inheritdoc />
    public string FormatToolCallJson(ToolCall toolCall)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(new
        {
            id = toolCall.Id,
            name = toolCall.Name,
            arguments = toolCall.Arguments,
            toolType = toolCall.ToolType
        }, options);
    }

    private static string EscapeJsonForCmdLine(string json)
    {
        // Escape for Windows command line (double quotes)
        return json.Replace("\"", "\\\"");
    }

    // Note: use canonical SemaphoreSlim.WaitAsync(cancellationToken) and handle OperationCanceledException
    // at the call sites where cancellation is expected to be swallowed.
}
