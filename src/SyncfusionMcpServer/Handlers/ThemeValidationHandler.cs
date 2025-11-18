using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;
using SyncfusionMcpServer.Services;

namespace SyncfusionMcpServer.Handlers;

public class ThemeValidationHandler
{
    private readonly ThemeValidationService _service;
    private readonly ILogger<ThemeValidationHandler> _logger;

    public ThemeValidationHandler(ThemeValidationService service, ILogger<ThemeValidationHandler> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<ThemeValidationResult> HandleAsync(JsonElement arguments)
    {
        var themeName = arguments.GetProperty("themeName").GetString() ?? "FluentDark";
        var targetAssembly = arguments.TryGetProperty("targetAssembly", out var ta) ? ta.GetString() : null;
        var appXamlPath = arguments.TryGetProperty("appXamlPath", out var axp) ? axp.GetString() : null;

        // If appXamlPath not provided, try to find it
        if (string.IsNullOrEmpty(appXamlPath))
        {
            var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ??
                           Directory.GetCurrentDirectory();
            var searchPath = Path.Combine(repoRoot, "src");

            if (Directory.Exists(searchPath))
            {
                var appFiles = Directory.GetFiles(searchPath, "App.xaml.cs", SearchOption.AllDirectories);
                appXamlPath = appFiles.FirstOrDefault();
            }
        }

        _logger.LogInformation("Validating theme: {Theme}", themeName);
        return await _service.ValidateThemeAsync(themeName, targetAssembly, appXamlPath);
    }
}
