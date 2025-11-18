using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;
using SyncfusionMcpServer.Services;

namespace SyncfusionMcpServer.Handlers;

public class XamlParserHandler
{
    private readonly XamlParsingService _service;
    private readonly ILogger<XamlParserHandler> _logger;

    public XamlParserHandler(XamlParsingService service, ILogger<XamlParserHandler> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<XamlParsingResult> HandleAsync(JsonElement arguments)
    {
        var xamlPath = arguments.GetProperty("xamlPath").GetString();
        var validateBindings = arguments.TryGetProperty("validateBindings", out var vb) && vb.GetBoolean();
        var checkNamespaces = arguments.TryGetProperty("checkNamespaces", out var cn) && cn.GetBoolean();

        if (string.IsNullOrEmpty(xamlPath))
        {
            return new XamlParsingResult
            {
                IsValid = false,
                Errors = { "xamlPath is required" }
            };
        }

        // Resolve relative paths
        if (!Path.IsPathRooted(xamlPath))
        {
            var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ??
                           Directory.GetCurrentDirectory();
            xamlPath = Path.Combine(repoRoot, xamlPath);
        }

        _logger.LogInformation("Parsing XAML file: {Path}", xamlPath);
        return await _service.ParseXamlFileAsync(xamlPath, validateBindings, checkNamespaces);
    }
}
