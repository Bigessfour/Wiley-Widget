using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SyncfusionMcpServer.Models;

namespace SyncfusionMcpServer.Services;

/// <summary>
/// Service for analyzing Syncfusion components for best practices and issues
/// </summary>
public class ComponentAnalyzerService
{
    private readonly ILogger<ComponentAnalyzerService> _logger;
    private readonly XamlParsingService _xamlParser;

    public ComponentAnalyzerService(ILogger<ComponentAnalyzerService> logger, XamlParsingService xamlParser)
    {
        _logger = logger;
        _xamlParser = xamlParser;
    }

    public async Task<DataGridAnalysisResult> AnalyzeDataGridAsync(string xamlPath, bool checkBinding, bool checkPerformance)
    {
        var result = new DataGridAnalysisResult { IsValid = true };

        try
        {
            if (!File.Exists(xamlPath))
            {
                result.IsValid = false;
                result.Errors.Add($"XAML file not found: {xamlPath}");
                return result;
            }

            var content = await File.ReadAllTextAsync(xamlPath);
            var doc = XDocument.Parse(content);

            // Find all SfDataGrid elements
            var dataGrids = doc.Descendants()
                .Where(e => e.Name.LocalName == "SfDataGrid" ||
                           e.Name.LocalName.StartsWith("SfDataGrid", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (dataGrids.Count == 0)
            {
                result.Warnings.Add("No SfDataGrid components found in XAML");
                return result;
            }

            foreach (var dataGrid in dataGrids)
            {
                // Analyze columns
                var columns = dataGrid.Descendants()
                    .Where(e => e.Name.LocalName.EndsWith("Column", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                result.TotalColumns = columns.Count;

                if (checkBinding)
                {
                    result.BindingIssues.AddRange(AnalyzeBindings(dataGrid, columns));
                }

                if (checkPerformance)
                {
                    result.PerformanceIssues.AddRange(AnalyzePerformance(dataGrid));
                }

                result.BestPracticeViolations.AddRange(CheckBestPractices(dataGrid, columns));

                // Check virtualization
                var allowsScrolling = dataGrid.Attribute("AllowsScrolling")?.Value;
                result.VirtualizationEnabled = allowsScrolling != "False";
            }

            _logger.LogInformation("Analyzed {Count} DataGrid(s) in {Path}", dataGrids.Count, xamlPath);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, ex, "Error analyzing DataGrid: {Path}", xamlPath);
            result.IsValid = false;
            result.Errors.Add($"Analysis error: {ex.Message}");
        }

        return result;
    }

    private List<BindingIssue> AnalyzeBindings(XElement dataGrid, List<XElement> columns)
    {
        var issues = new List<BindingIssue>();

        foreach (var column in columns)
        {
            var mappingName = column.Attribute("MappingName")?.Value;
            var headerText = column.Attribute("HeaderText")?.Value ?? mappingName;

            if (string.IsNullOrEmpty(mappingName))
            {
                issues.Add(new BindingIssue
                {
                    Column = headerText ?? "Unknown",
                    Issue = "Missing MappingName property",
                    Severity = "error",
                    Suggestion = "Add MappingName attribute to bind to data property"
                });
                continue;
            }

            // Check for currency/decimal columns without converters
            if (mappingName.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
                mappingName.Contains("Price", StringComparison.OrdinalIgnoreCase) ||
                mappingName.Contains("Total", StringComparison.OrdinalIgnoreCase))
            {
                var format = column.Attribute("Format")?.Value;
                if (string.IsNullOrEmpty(format))
                {
                    issues.Add(new BindingIssue
                    {
                        Column = mappingName,
                        Issue = "Missing format for currency column",
                        Severity = "warning",
                        Suggestion = "Add Format=\"C\" or Format=\"C2\" for currency display"
                    });
                }
            }

            // Check for date columns without format
            if (mappingName.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
                mappingName.Contains("Time", StringComparison.OrdinalIgnoreCase))
            {
                var format = column.Attribute("Format")?.Value;
                if (string.IsNullOrEmpty(format))
                {
                    issues.Add(new BindingIssue
                    {
                        Column = mappingName,
                        Issue = "Missing format for date column",
                        Severity = "warning",
                        Suggestion = "Add Format=\"MM/dd/yyyy\" or appropriate date format"
                    });
                }
            }
        }

        return issues;
    }

    private List<PerformanceIssue> AnalyzePerformance(XElement dataGrid)
    {
        var issues = new List<PerformanceIssue>();

        // Check AutoGenerateColumns
        var autoGenerate = dataGrid.Attribute("AutoGenerateColumns")?.Value;
        if (autoGenerate == "True")
        {
            issues.Add(new PerformanceIssue
            {
                Issue = "AutoGenerateColumns enabled",
                Impact = "Slower initialization and less control over column configuration",
                Recommendation = "Define columns explicitly for better performance and control"
            });
        }

        // Check for row height
        var rowHeight = dataGrid.Attribute("RowHeight")?.Value;
        if (string.IsNullOrEmpty(rowHeight))
        {
            issues.Add(new PerformanceIssue
            {
                Issue = "RowHeight not specified",
                Impact = "May cause layout recalculation overhead",
                Recommendation = "Set explicit RowHeight for consistent performance"
            });
        }

        // Check AllowsScrolling
        var allowsScrolling = dataGrid.Attribute("AllowsScrolling")?.Value;
        if (allowsScrolling == "False")
        {
            issues.Add(new PerformanceIssue
            {
                Issue = "Scrolling disabled",
                Impact = "Virtualization won't work, all rows rendered",
                Recommendation = "Enable AllowsScrolling for large datasets"
            });
        }

        return issues;
    }

    private List<BestPracticeViolation> CheckBestPractices(XElement dataGrid, List<XElement> columns)
    {
        var violations = new List<BestPracticeViolation>();

        // Check SelectionMode
        var selectionMode = dataGrid.Attribute("SelectionMode")?.Value;
        if (string.IsNullOrEmpty(selectionMode))
        {
            violations.Add(new BestPracticeViolation
            {
                Issue = "SelectionMode not explicitly set",
                Recommendation = "Explicitly set SelectionMode (Single, Multiple, Extended, None)",
                DocumentationLink = "https://help.syncfusion.com/winui/datagrid/selection"
            });
        }

        // Check for column width
        var columnsWithoutWidth = columns.Where(c =>
            string.IsNullOrEmpty(c.Attribute("Width")?.Value) &&
            string.IsNullOrEmpty(c.Attribute("MinWidth")?.Value))
            .Count();

        if (columnsWithoutWidth > 0)
        {
            violations.Add(new BestPracticeViolation
            {
                Issue = $"{columnsWithoutWidth} column(s) without Width or MinWidth",
                Recommendation = "Set Width or MinWidth for consistent column sizing",
                DocumentationLink = "https://help.syncfusion.com/winui/datagrid/columns"
            });
        }

        // Check for ItemsSource binding
        var itemsSource = dataGrid.Attribute("ItemsSource")?.Value;
        if (string.IsNullOrEmpty(itemsSource))
        {
            violations.Add(new BestPracticeViolation
            {
                Issue = "ItemsSource not bound",
                Recommendation = "Bind ItemsSource to ViewModel collection for MVVM pattern"
            });
        }

        return violations;
    }
}
