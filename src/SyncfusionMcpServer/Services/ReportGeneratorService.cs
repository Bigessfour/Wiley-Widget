using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;

namespace SyncfusionMcpServer.Services;

/// <summary>
/// Service for generating comprehensive validation reports
/// </summary>
public class ReportGeneratorService
{
    private readonly ILogger<ReportGeneratorService> _logger;
    private readonly XamlParsingService _xamlParser;
    private readonly ComponentAnalyzerService _componentAnalyzer;
    private readonly ThemeValidationService _themeValidator;
    private readonly LicenseService _licenseService;

    public ReportGeneratorService(
        ILogger<ReportGeneratorService> logger,
        XamlParsingService xamlParser,
        ComponentAnalyzerService componentAnalyzer,
        ThemeValidationService themeValidator,
        LicenseService licenseService)
    {
        _logger = logger;
        _xamlParser = xamlParser;
        _componentAnalyzer = componentAnalyzer;
        _themeValidator = themeValidator;
        _licenseService = licenseService;
    }

    public async Task<ValidationReport> GenerateReportAsync(
        string projectPath,
        bool includeThemes,
        bool includeComponents,
        string outputFormat)
    {
        var report = new ValidationReport
        {
            ProjectPath = projectPath,
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            if (!Directory.Exists(projectPath))
            {
                _logger.LogError("Project path not found: {Path}", projectPath);
                return report;
            }

            // Validate license
            report.LicenseValidation = await _licenseService.ValidateLicenseAsync(null, null);

            // Find all XAML files
            var xamlFiles = Directory.GetFiles(projectPath, "*.xaml", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToList();

            report.Summary.TotalFiles = xamlFiles.Count;

            // Parse all XAML files
            foreach (var xamlFile in xamlFiles)
            {
                try
                {
                    var xamlResult = await _xamlParser.ParseXamlFileAsync(xamlFile, true, true);
                    report.XamlParsingResults.Add(xamlResult);

                    report.Summary.ComponentsAnalyzed += xamlResult.ComponentsFound.Count;
                    report.Summary.TotalErrors += xamlResult.Errors.Count;
                    report.Summary.TotalWarnings += xamlResult.Warnings.Count;

                    // Analyze DataGrids if requested
                    if (includeComponents &&
                        xamlResult.ComponentsFound.Any(c =>
                            c.Type.Contains("DataGrid", StringComparison.OrdinalIgnoreCase)))
                    {
                        var dataGridResult = await _componentAnalyzer.AnalyzeDataGridAsync(
                            xamlFile, true, true);
                        report.DataGridAnalyses.Add(dataGridResult);

                        report.Summary.TotalErrors += dataGridResult.Errors.Count;
                        report.Summary.TotalWarnings += dataGridResult.Warnings.Count;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error analyzing file: {File}", xamlFile);
                    report.Summary.TotalErrors++;
                }
            }

            // Validate themes if requested
            if (includeThemes)
            {
                var appXamlCs = Directory.GetFiles(projectPath, "App.xaml.cs", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (appXamlCs != null)
                {
                    // Check for default theme (FluentDark commonly used)
                    report.ThemeValidation = await _themeValidator.ValidateThemeAsync(
                        "FluentDark", null, appXamlCs);

                    report.Summary.TotalErrors += report.ThemeValidation.Errors.Count;
                    report.Summary.TotalWarnings += report.ThemeValidation.Warnings.Count;
                }
            }

            // Calculate overall success
            report.Summary.OverallSuccess = report.Summary.TotalErrors == 0;

            _logger.LogInformation(
                "Report generated: {Files} files, {Components} components, {Errors} errors, {Warnings} warnings",
                report.Summary.TotalFiles,
                report.Summary.ComponentsAnalyzed,
                report.Summary.TotalErrors,
                report.Summary.TotalWarnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report for: {Path}", projectPath);
        }

        return report;
    }
}
