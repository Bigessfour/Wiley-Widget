using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Service for executing xAI tool calls via Python bridge and direct C# execution.
/// Supports both filesystem tools (via Python) and Wiley Widget operations (direct repository access).
/// Implements MCP-compliant tool execution with Polly resilience patterns.
/// </summary>
public partial class AIAssistantService : IAIAssistantService
{
    private readonly ILogger<AIAssistantService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _pythonExecutablePath;
    private readonly string _toolExecutorScriptPath;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly int _toolExecutionTimeoutSeconds;

    // Repository dependencies for Wiley Widget operations
    private readonly IEnterpriseRepository? _enterpriseRepository;
    private readonly IBudgetRepository? _budgetRepository;
    private readonly IAuditRepository? _auditRepository;
    private readonly IWileyWidgetContextService? _contextService;

    [GeneratedRegex(@"(read|grep|search|list|get errors?)\s+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ToolDetectionRegex();

    /// <summary>
    /// Constructor with dependency injection for both Python tools and Wiley Widget operations
    /// </summary>
    public AIAssistantService(
        ILogger<AIAssistantService> logger,
        IConfiguration configuration,
        IEnterpriseRepository? enterpriseRepository = null,
        IBudgetRepository? budgetRepository = null,
        IAuditRepository? auditRepository = null,
        IWileyWidgetContextService? contextService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _enterpriseRepository = enterpriseRepository;
        _budgetRepository = budgetRepository;
        _auditRepository = auditRepository;
        _contextService = contextService;

        // Resolve paths relative to workspace root
        var workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
        _toolExecutorScriptPath = Path.Combine(workspaceRoot, @"scripts\tools\xai_tool_executor.py");
        _pythonExecutablePath = configuration["AI:PythonExecutable"] ?? "python";

        // Get timeout from configuration with fallback
        _toolExecutionTimeoutSeconds = configuration.GetValue<int>("AI:ToolExecutionTimeoutSeconds", 30);

        // Get max concurrent executions from configuration
        var maxConcurrentExecutions = configuration.GetValue<int>("AI:MaxConcurrentToolCalls", 5);
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentExecutions, maxConcurrentExecutions);

        if (!File.Exists(_toolExecutorScriptPath))
        {
            _logger.LogWarning("Tool executor script not found at {Path}. Python tools will fail.", _toolExecutorScriptPath);
        }

        _logger.LogInformation(
            "AIAssistantService initialized: Timeout={Timeout}s, MaxConcurrent={MaxConcurrent}, " +
            "Repositories={HasRepos}, Python={PythonPath}",
            _toolExecutionTimeoutSeconds,
            maxConcurrentExecutions,
            enterpriseRepository != null && budgetRepository != null,
            _pythonExecutablePath);
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
            _logger.LogDebug("Tool execution canceled before starting: {ToolName}", toolCall.Name);
            return ToolCallResult.Error(toolCall.Id, "Operation canceled");
        }

        try
        {
            _logger.LogInformation("Executing tool {ToolName} with ID {ToolCallId}", toolCall.Name, toolCall.Id);

            // Route to appropriate executor based on tool type
            return toolCall.Name switch
            {
                // Python bridge tools (filesystem operations)
                "read_file" or "grep_search" or "semantic_search" or "list_directory" or "get_errors" or "get_git_changes"
                    => await ExecutePythonToolAsync(toolCall, cancellationToken),

                // Wiley Widget operation tools (direct C# execution)
                "get_enterprise_details" => await ExecuteGetEnterpriseDetailsAsync(toolCall, cancellationToken),
                "run_budget_analysis" => await ExecuteRunBudgetAnalysisAsync(toolCall, cancellationToken),
                "get_current_ui_state" => await ExecuteGetCurrentUIStateAsync(toolCall, cancellationToken),
                "search_audit_trail" => await ExecuteSearchAuditTrailAsync(toolCall, cancellationToken),
                "list_enterprises" => await ExecuteListEnterprisesAsync(toolCall, cancellationToken),

                _ => ToolCallResult.Error(toolCall.Id, $"Unknown tool: {toolCall.Name}. Available tools: read_file, grep_search, semantic_search, list_directory, get_errors, get_enterprise_details, run_budget_analysis, search_audit_trail")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing tool {ToolName}", toolCall.Name);
            return ToolCallResult.Error(toolCall.Id, $"Unexpected error: {ex.Message}");
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Execute tool via Python bridge (filesystem operations)
    /// </summary>
    private async Task<ToolCallResult> ExecutePythonToolAsync(ToolCall toolCall, CancellationToken cancellationToken)
    {
        if (!File.Exists(_toolExecutorScriptPath))
        {
            return ToolCallResult.Error(toolCall.Id, $"Python tool executor not found at {_toolExecutorScriptPath}");
        }

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

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_toolExecutionTimeoutSeconds), cancellationToken);
        var processTask = Task.Run(() => process.WaitForExit(), cancellationToken);

        var completedTask = await Task.WhenAny(processTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill timed-out process for tool {ToolName}", toolCall.Name);
            }

            var errorMessage = $"Tool execution timed out after {_toolExecutionTimeoutSeconds} seconds";
            _logger.LogError("{ErrorMessage} for tool {ToolName}", errorMessage, toolCall.Name);
            return ToolCallResult.Error(toolCall.Id, errorMessage);
        }

        var output = outputBuilder.ToString();
        var errors = errorBuilder.ToString();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Python tool {ToolName} failed with exit code {ExitCode}. Stderr: {Errors}",
                toolCall.Name, process.ExitCode, errors);
            return ToolCallResult.Error(toolCall.Id, string.IsNullOrWhiteSpace(errors) ? $"Process exited with code {process.ExitCode}" : errors);
        }

        if (!string.IsNullOrWhiteSpace(errors))
        {
            _logger.LogWarning("Python tool {ToolName} completed but wrote to stderr: {Errors}",
                toolCall.Name, errors);
        }

        _logger.LogInformation("Python tool {ToolName} executed successfully ({OutputLength} bytes)",
            toolCall.Name, output.Length);
        return ToolCallResult.Success(toolCall.Id, output);
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

    #region Wiley Widget Operation Tools

    /// <summary>
    /// Get detailed information about a specific enterprise
    /// </summary>
    private async Task<ToolCallResult> ExecuteGetEnterpriseDetailsAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (_enterpriseRepository == null)
        {
            return ToolCallResult.Error(toolCall.Id, "Enterprise repository not available. This tool requires database access.");
        }

        try
        {
            var enterpriseId = toolCall.Arguments.TryGetValue("enterprise_id", out var idObj)
                ? Convert.ToInt32(idObj)
                : 0;

            var enterpriseName = toolCall.Arguments.TryGetValue("enterprise_name", out var nameObj)
                ? nameObj?.ToString()
                : null;

            Enterprise? enterprise = null;

            if (enterpriseId > 0)
            {
                enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
            }
            else if (!string.IsNullOrWhiteSpace(enterpriseName))
            {
                var allEnterprises = await _enterpriseRepository.GetAllAsync();
                enterprise = allEnterprises.FirstOrDefault(e =>
                    e.Name?.Equals(enterpriseName, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (enterprise == null)
            {
                return ToolCallResult.Error(toolCall.Id,
                    $"Enterprise not found. Searched by: {(enterpriseId > 0 ? $"ID={enterpriseId}" : $"Name='{enterpriseName}'")}");
            }

            var result = JsonSerializer.Serialize(new
            {
                enterprise.Id,
                enterprise.Name,
                enterprise.Type,
                enterprise.Status,
                enterprise.Description,
                enterprise.CurrentRate,
                enterprise.MonthlyRevenue,
                enterprise.MonthlyExpenses,
                NetMonthly = enterprise.MonthlyRevenue - enterprise.MonthlyExpenses,
                enterprise.CreatedDate,
                enterprise.ModifiedDate
            }, new JsonSerializerOptions { WriteIndented = true });

            _logger.LogInformation("Retrieved enterprise details for: {EnterpriseName} (ID: {EnterpriseId})",
                enterprise.Name, enterprise.Id);

            return ToolCallResult.Success(toolCall.Id, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting enterprise details");
            return ToolCallResult.Error(toolCall.Id, $"Database error: {ex.Message}");
        }
    }

    /// <summary>
    /// Run budget analysis for a fiscal year
    /// </summary>
    private async Task<ToolCallResult> ExecuteRunBudgetAnalysisAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (_budgetRepository == null)
        {
            return ToolCallResult.Error(toolCall.Id, "Budget repository not available. This tool requires database access.");
        }

        try
        {
            var fiscalYear = toolCall.Arguments.TryGetValue("fiscal_year", out var yearObj)
                ? Convert.ToInt32(yearObj)
                : DateTime.Now.Year;

            var department = toolCall.Arguments.TryGetValue("department", out var deptObj)
                ? deptObj?.ToString()
                : null;

            var startDate = new DateTime(fiscalYear, 1, 1);
            var endDate = new DateTime(fiscalYear, 12, 31);

            var summary = await _budgetRepository.GetBudgetSummaryAsync(startDate, endDate);

            var result = new StringBuilder();
            result.AppendLine($"Budget Analysis for Fiscal Year {fiscalYear}\");
            result.AppendLine(\"═══════════════════════════════════════\");
            result.AppendLine();\n            result.AppendLine($\"Analysis Date: {summary.AnalysisDate:yyyy-MM-dd HH:mm:ss}\");
            result.AppendLine($\"Period: {summary.BudgetPeriod ?? \"N/A\"}\");
            result.AppendLine();
            result.AppendLine($\"Total Budgeted: ${summary.TotalBudgeted:N2}\");
            result.AppendLine($\"Total Actual:   ${summary.TotalActual:N2}\");
            result.AppendLine($\"Total Variance: ${summary.TotalVariance:N2} ({summary.TotalVariancePercentage:N2}%)\");
            result.AppendLine();

            if (!string.IsNullOrWhiteSpace(department))
            {
                var deptSummary = summary.DepartmentSummaries
                    .FirstOrDefault(d => d.DepartmentName?.Contains(department, StringComparison.OrdinalIgnoreCase) == true);

                if (deptSummary != null)
                {
                    result.AppendLine($\"Department: {deptSummary.DepartmentName}\");
                    result.AppendLine($\"  Budgeted: ${deptSummary.Budgeted:N2}\");
                    result.AppendLine($\"  Actual:   ${deptSummary.Actual:N2}\");
                    result.AppendLine($\"  Variance: ${deptSummary.Variance:N2}\");
                }
                else
                {
                    result.AppendLine($\"Department '{department}' not found in budget summary.\");
                }
            }
            else
            {
                result.AppendLine(\"Top 5 Departments by Variance:\");
                var topVariances = summary.DepartmentSummaries
                    .OrderByDescending(d => Math.Abs(d.Variance))
                    .Take(5);

                foreach (var dept in topVariances)
                {
                    var status = dept.Variance < 0 ? \"⚠️ Over\" : \"✅ Under\";
                    result.AppendLine($\"  {status} {dept.DepartmentName}: ${Math.Abs(dept.Variance):N2}\");
                }
            }

            _logger.LogInformation(\"Budget analysis completed for fiscal year {FiscalYear}\", fiscalYear);
            return ToolCallResult.Success(toolCall.Id, result.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, \"Error running budget analysis\");
            return ToolCallResult.Error(toolCall.Id, $\"Analysis error: {ex.Message}\");
        }
    }

    /// <summary>
    /// Get current UI state and recent user actions
    /// </summary>
    private async Task<ToolCallResult> ExecuteGetCurrentUIStateAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (_contextService == null)
        {
            return ToolCallResult.Error(toolCall.Id, \"Context service not available.\");
        }

        try
        {
            var contextInfo = await _contextService.GetOperationalContextAsync();

            _logger.LogInformation(\"Retrieved current UI state\");
            return ToolCallResult.Success(toolCall.Id, contextInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, \"Error getting UI state\");
            return ToolCallResult.Error(toolCall.Id, $\"Context error: {ex.Message}\");
        }
    }

    /// <summary>
    /// Search audit trail for specific actions
    /// </summary>
    private async Task<ToolCallResult> ExecuteSearchAuditTrailAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (_auditRepository == null)
        {
            return ToolCallResult.Error(toolCall.Id, \"Audit repository not available. This tool requires database access.\");
        }

        try
        {
            var entityType = toolCall.Arguments.TryGetValue(\"entity_type\", out var etObj)
                ? etObj?.ToString()
                : null;

            var action = toolCall.Arguments.TryGetValue(\"action\", out var actObj)
                ? actObj?.ToString()
                : null;

            var startDate = toolCall.Arguments.TryGetValue(\"start_date\", out var sdObj)
                ? DateTime.Parse(sdObj?.ToString() ?? DateTime.Now.AddDays(-7).ToString(\"yyyy-MM-dd\"))
                : DateTime.Now.AddDays(-7);

            var endDate = toolCall.Arguments.TryGetValue(\"end_date\", out var edObj)
                ? DateTime.Parse(edObj?.ToString() ?? DateTime.Now.ToString(\"yyyy-MM-dd\"))
                : DateTime.Now;

            var auditEntries = await _auditRepository.GetAuditTrailAsync(startDate, endDate);
            var filtered = auditEntries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                filtered = filtered.Where(a => a.EntityType?.Contains(entityType, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                filtered = filtered.Where(a => a.Action?.Contains(action, StringComparison.OrdinalIgnoreCase) == true);
            }

            var results = filtered.Take(50).ToList();

            var result = new StringBuilder();
            result.AppendLine($\"Audit Trail Search Results ({results.Count} entries)\");
            result.AppendLine(\"═══════════════════════════════════════════════\");
            result.AppendLine($\"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}\");

            if (!string.IsNullOrWhiteSpace(entityType))
                result.AppendLine($\"Entity Type Filter: {entityType}\");
            if (!string.IsNullOrWhiteSpace(action))
                result.AppendLine($\"Action Filter: {action}\");

            result.AppendLine();

            foreach (var entry in results)
            {
                result.AppendLine($\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} | {entry.Action} | {entry.EntityType} (ID: {entry.EntityId}) | User: {entry.UserId}\");
            }

            if (results.Count == 50)
            {
                result.AppendLine();
                result.AppendLine(\"(Limited to 50 most recent entries)\");
            }

            _logger.LogInformation(\"Audit trail search returned {Count} entries\", results.Count);
            return ToolCallResult.Success(toolCall.Id, result.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, \"Error searching audit trail\");
            return ToolCallResult.Error(toolCall.Id, $\"Search error: {ex.Message}\");
        }
    }

    /// <summary>
    /// List all enterprises with summary information
    /// </summary>
    private async Task<ToolCallResult> ExecuteListEnterprisesAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (_enterpriseRepository == null)
        {
            return ToolCallResult.Error(toolCall.Id, \"Enterprise repository not available. This tool requires database access.\");
        }

        try
        {
            var includeInactive = toolCall.Arguments.TryGetValue(\"include_inactive\", out var inactiveObj) &&
                                  Convert.ToBoolean(inactiveObj);

            var enterprises = await _enterpriseRepository.GetAllAsync();
            var filtered = includeInactive
                ? enterprises
                : enterprises.Where(e => e.Status == EnterpriseStatus.Active);

            var enterpriseList = filtered.OrderBy(e => e.Name).ToList();

            var result = new StringBuilder();
            result.AppendLine($\"Enterprises ({enterpriseList.Count} total)\");
            result.AppendLine(\"══════════════════════════════════════════════════════\");
            result.AppendLine();

            foreach (var ent in enterpriseList)
            {
                var netCashFlow = ent.MonthlyRevenue - ent.MonthlyExpenses;
                var cashFlowIndicator = netCashFlow >= 0 ? \"✅\" : \"⚠️\";

                result.AppendLine($\"{cashFlowIndicator} {ent.Name} (ID: {ent.Id})\");
                result.AppendLine($\"   Type: {ent.Type} | Status: {ent.Status}\");
                result.AppendLine($\"   Revenue: ${ent.MonthlyRevenue:N2}/mo | Expenses: ${ent.MonthlyExpenses:N2}/mo\");
                result.AppendLine($\"   Net Cash Flow: ${netCashFlow:N2}/mo\");
                result.AppendLine();
            }

            _logger.LogInformation(\"Listed {Count} enterprises\", enterpriseList.Count);
            return ToolCallResult.Success(toolCall.Id, result.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, \"Error listing enterprises\");
            return ToolCallResult.Error(toolCall.Id, $\"Database error: {ex.Message}\");
        }
    }

    #endregion

    // Note: use canonical SemaphoreSlim.WaitAsync(cancellationToken) and handle OperationCanceledException
    // at the call sites where cancellation is expected to be swallowed.
}
