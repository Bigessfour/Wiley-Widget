using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Handlers;
using SyncfusionMcpServer.Services;

namespace SyncfusionMcpServer;

/// <summary>
/// MCP Server for Syncfusion WinUI component validation and analysis.
/// Implements the Model Context Protocol over stdio for GitHub Copilot integration.
/// </summary>
public class Program
{
    private static IServiceProvider? _serviceProvider;
    private static ILogger<Program>? _logger;

    public static async Task Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<Program>>();

        _logger!.LogInformation("Syncfusion MCP Server starting...");

        try
        {
            await RunServerAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MCP server");
            Environment.Exit(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Services
        services.AddSingleton<XamlParsingService>();
        services.AddSingleton<ComponentAnalyzerService>();
        services.AddSingleton<ThemeValidationService>();
        services.AddSingleton<LicenseService>();
        services.AddSingleton<ReportGeneratorService>();

        // Handlers
        services.AddSingleton<ThemeValidationHandler>();
        services.AddSingleton<DataGridAnalysisHandler>();
        services.AddSingleton<LicenseCheckHandler>();
        services.AddSingleton<XamlParserHandler>();
        services.AddSingleton<ReportGeneratorHandler>();
    }

    private static async Task RunServerAsync()
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        _logger!.LogInformation("MCP Server ready, waiting for requests...");

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
            {
                break;
            }

            try
            {
                var request = JsonSerializer.Deserialize<McpRequest>(line, jsonOptions);
                if (request == null)
                {
                    continue;
                }

                var response = await HandleRequestAsync(request);
                var responseJson = JsonSerializer.Serialize(response, jsonOptions);
                await writer.WriteLineAsync(responseJson);
            }
            catch (Exception ex)
            {
                _logger!.LogError(ex, "Error processing request: {Line}", line);
                var errorResponse = new McpResponse
                {
                    Id = 0,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = $"Internal error: {ex.Message}"
                    }
                };
                var errorJson = JsonSerializer.Serialize(errorResponse, jsonOptions);
                await writer.WriteLineAsync(errorJson);
            }
        }
    }

    private static async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        _logger!.LogDebug("Handling request: {Method}", request.Method);

        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolCallAsync(request),
            _ => new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}"
                }
            }
        };
    }

    private static McpResponse HandleInitialize(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new
                {
                    name = "syncfusion-mcp-server",
                    version = "1.0.0"
                },
                capabilities = new
                {
                    tools = new { }
                }
            }
        };
    }

    private static McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new List<object>
        {
            new
            {
                name = "syncfusion_validate_theme",
                description = "Validate SfSkinManager theme configuration and transitions",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["themeName"] = new { type = "string", description = "Theme name (e.g., 'FluentDark', 'FluentLight')" },
                        ["targetAssembly"] = new { type = "string", description = "Theme assembly name" },
                        ["appXamlPath"] = new { type = "string", description = "Path to App.xaml.cs file" }
                    },
                    required = new[] { "themeName" }
                }
            },
            new
            {
                name = "syncfusion_analyze_datagrid",
                description = "Analyze SfDataGrid configurations for best practices and binding issues",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["xamlPath"] = new { type = "string", description = "Path to XAML file containing SfDataGrid" },
                        ["checkBinding"] = new { type = "boolean", description = "Check for binding issues" },
                        ["checkPerformance"] = new { type = "boolean", description = "Check for performance issues" }
                    },
                    required = new[] { "xamlPath" }
                }
            },
            new
            {
                name = "syncfusion_check_license",
                description = "Verify Syncfusion license configuration and status",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["licenseKey"] = new { type = "string", description = "License key (optional, reads from environment)" },
                        ["expectedVersion"] = new { type = "string", description = "Expected Syncfusion version" }
                    }
                }
            },
            new
            {
                name = "syncfusion_parse_xaml",
                description = "Parse and validate Syncfusion-specific XAML syntax and components",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["xamlPath"] = new { type = "string", description = "Path to XAML file" },
                        ["validateBindings"] = new { type = "boolean", description = "Validate binding paths" },
                        ["checkNamespaces"] = new { type = "boolean", description = "Check namespace declarations" }
                    },
                    required = new[] { "xamlPath" }
                }
            },
            new
            {
                name = "syncfusion_generate_report",
                description = "Generate comprehensive validation report for CI/CD",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["projectPath"] = new { type = "string", description = "Path to project root" },
                        ["includeThemes"] = new { type = "boolean", description = "Include theme validation" },
                        ["includeComponents"] = new { type = "boolean", description = "Include component analysis" },
                        ["outputFormat"] = new { type = "string", description = "Output format (json/xml)" }
                    },
                    required = new[] { "projectPath" }
                }
            }
        };

        return new McpResponse
        {
            Id = request.Id,
            Result = new { tools }
        };
    }

    private static async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        if (request.Params?.Arguments == null)
        {
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32602,
                    Message = "Invalid params: missing arguments"
                }
            };
        }

        var toolName = request.Params.Name;
        var arguments = request.Params.Arguments;

        try
        {
            var result = toolName switch
            {
                "syncfusion_validate_theme" => (object)await _serviceProvider!
                    .GetRequiredService<ThemeValidationHandler>()
                    .HandleAsync(arguments.Value),

                "syncfusion_analyze_datagrid" => (object)await _serviceProvider!
                    .GetRequiredService<DataGridAnalysisHandler>()
                    .HandleAsync(arguments.Value),

                "syncfusion_check_license" => (object)await _serviceProvider!
                    .GetRequiredService<LicenseCheckHandler>()
                    .HandleAsync(arguments.Value),

                "syncfusion_parse_xaml" => (object)await _serviceProvider!
                    .GetRequiredService<XamlParserHandler>()
                    .HandleAsync(arguments.Value),

                "syncfusion_generate_report" => (object)await _serviceProvider!
                    .GetRequiredService<ReportGeneratorHandler>()
                    .HandleAsync(arguments.Value),

                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(result, new JsonSerializerOptions
                            {
                                WriteIndented = true,
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            })
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger!.Log(LogLevel.Error, ex, "Error executing tool: {ToolName}", toolName);
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Tool execution error: {ex.Message}"
                }
            };
        }
    }
}

// MCP Protocol Models
public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public McpParams? Params { get; set; }
}

public class McpParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}

public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
