using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;
using SyncfusionMcpServer.Services;

namespace SyncfusionMcpServer.Handlers;

public class ReportGeneratorHandler
{
    private readonly ReportGeneratorService _service;
    private readonly ILogger<ReportGeneratorHandler> _logger;

    public ReportGeneratorHandler(ReportGeneratorService service, ILogger<ReportGeneratorHandler> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<ValidationReport> HandleAsync(JsonElement arguments)
    {
        var projectPath = arguments.GetProperty("projectPath").GetString();
        var includeThemes = arguments.TryGetProperty("includeThemes", out var it) && it.GetBoolean();
        var includeComponents = arguments.TryGetProperty("includeComponents", out var ic) && ic.GetBoolean();
        var outputFormat = arguments.TryGetProperty("outputFormat", out var of) ? of.GetString() : "json";

        if (string.IsNullOrEmpty(projectPath))
        {
            return new ValidationReport
            {
                Summary = new ValidationSummary
                {
                    TotalErrors = 1,
                    OverallSuccess = false
                }
            };
        }

        // Resolve relative paths
        if (!Path.IsPathRooted(projectPath))
        {
            var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ??
                           Directory.GetCurrentDirectory();
            projectPath = Path.Combine(repoRoot, projectPath);
        }

        _logger.LogInformation("Generating validation report for: {Path}", projectPath);
        return await _service.GenerateReportAsync(projectPath, includeThemes, includeComponents, outputFormat ?? "json");
    }
}
