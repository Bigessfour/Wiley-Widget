using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;
using SyncfusionMcpServer.Services;

namespace SyncfusionMcpServer.Handlers;

public class DataGridAnalysisHandler
{
    private readonly ComponentAnalyzerService _service;
    private readonly ILogger<DataGridAnalysisHandler> _logger;

    public DataGridAnalysisHandler(ComponentAnalyzerService service, ILogger<DataGridAnalysisHandler> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<DataGridAnalysisResult> HandleAsync(JsonElement arguments)
    {
        var xamlPath = arguments.GetProperty("xamlPath").GetString();
        var checkBinding = arguments.TryGetProperty("checkBinding", out var cb) && cb.GetBoolean();
        var checkPerformance = arguments.TryGetProperty("checkPerformance", out var cp) && cp.GetBoolean();

        if (string.IsNullOrEmpty(xamlPath))
        {
            return new DataGridAnalysisResult
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

        _logger.LogInformation("Analyzing DataGrid in: {Path}", xamlPath);
        return await _service.AnalyzeDataGridAsync(xamlPath, checkBinding, checkPerformance);
    }
}
