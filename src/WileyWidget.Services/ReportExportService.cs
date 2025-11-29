using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using Polly;
using WileyWidget.Services.Startup;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of report export service using open-source libraries.
/// Uses ClosedXML for Excel and PdfSharpCore for PDF generation.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly ILogger<ReportExportService> _logger;
    private readonly FileIoPipelineHolder? _fileIoPipeline;
    private readonly IPdfService _pdfService;

    public ReportExportService(ILogger<ReportExportService> logger, IPdfService pdfService, FileIoPipelineHolder? fileIoPipeline = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
        _fileIoPipeline = fileIoPipeline;
    }

    /// <summary>
    /// Exports data to PDF format using PdfSharpCore
    /// </summary>
    public async Task ExportToPdfAsync(object data, string filePath)
    {
        // Use IPdfService to create a simple table-based PDF
        byte[] bytes = await _pdfService.CreatePdfAsync(builder =>
        {
            if (data is IEnumerable<object> enumerable)
            {
                var list = enumerable.ToList();
                if (!list.Any())
                {
                    builder.AddPage();
                    builder.DrawText("No data", 50, 50);
                    return Task.CompletedTask;
                }

                var props = list.First().GetType().GetProperties().Where(p => p.CanRead).ToList();
                var rows = new List<IEnumerable<string>> { props.Select(p => p.Name) };
                rows.AddRange(list.Select(item => props.Select(p => p.GetValue(item)?.ToString() ?? string.Empty)));

                builder.AddPage();
                builder.AddTable(rows, 40, 80);
            }
            else
            {
                var props = data.GetType().GetProperties().Where(p => p.CanRead).ToList();
                var rows = props.Select(p => new[] { p.Name, p.GetValue(data)?.ToString() ?? string.Empty }.AsEnumerable());
                builder.AddPage();
                builder.AddTable(rows, 40, 80);
            }

            return Task.CompletedTask;
        });

        if (_fileIoPipeline?.Pipeline != null)
        {
            _fileIoPipeline.Pipeline.ExecuteAsync<bool>(ctx =>
            {
                File.WriteAllBytes(filePath, bytes);
                return new ValueTask<bool>(true);
            }, ResilienceContextPool.Shared.Get()).GetAwaiter().GetResult();
        }
        else
        {
            File.WriteAllBytes(filePath, bytes);
        }
    }

    /// <summary>
    /// Exports data to Excel format using ClosedXML
    /// </summary>
    public async Task ExportToExcelAsync(object data, string filePath)
    {
        await Task.Run(() =>
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Data");
                int rowIndex = 1;

                // Handle different data types
                if (data is IEnumerable<object> enumerableData)
                {
                    // Tabular data
                    var items = enumerableData.ToList();
                    if (items.Any())
                    {
                        // Get properties from first item
                        var properties = items.First().GetType().GetProperties()
                            .Where(p => p.CanRead)
                            .ToArray();

                        // Add headers
                        for (int i = 0; i < properties.Length; i++)
                        {
                            worksheet.Cell(rowIndex, i + 1).Value = properties[i].Name;
                            worksheet.Cell(rowIndex, i + 1).Style.Font.Bold = true;
                            worksheet.Cell(rowIndex, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
                        }
                        rowIndex++;

                        // Add data rows
                        foreach (var item in items)
                        {
                            for (int i = 0; i < properties.Length; i++)
                            {
                                var value = properties[i].GetValue(item);
                                if (value != null)
                                {
                                    worksheet.Cell(rowIndex, i + 1).Value = value.ToString();
                                }
                            }
                            rowIndex++;
                        }

                        // Auto-fit columns
                        worksheet.Columns().AdjustToContents();
                    }
                }
                else
                {
                    // Single object - display properties
                    var properties = data.GetType().GetProperties()
                        .Where(p => p.CanRead)
                        .ToArray();

                    foreach (var prop in properties)
                    {
                        worksheet.Cell(rowIndex, 1).Value = prop.Name;
                        worksheet.Cell(rowIndex, 1).Style.Font.Bold = true;
                        worksheet.Cell(rowIndex, 2).Value = prop.GetValue(data)?.ToString() ?? "";
                        rowIndex++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();
                }

                // Save the workbook
                if (_fileIoPipeline?.Pipeline != null)
                {
                    _fileIoPipeline.Pipeline.ExecuteAsync<bool>(ctx => { workbook.SaveAs(filePath); return new ValueTask<bool>(true); }, ResilienceContextPool.Shared.Get()).GetAwaiter().GetResult();
                }
                else
                {
                    workbook.SaveAs(filePath);
                }
            }
        });
    }

    /// <summary>
    /// Exports data to CSV format
    /// </summary>
    public async Task ExportToCsvAsync(IEnumerable<object> data, string filePath)
    {
        await Task.Run(() =>
        {
            var items = data.ToList();
            if (!items.Any()) return;

            if (_fileIoPipeline?.Pipeline != null)
            {
                // Execute CSV write inside file I/O pipeline
                _fileIoPipeline.Pipeline.ExecuteAsync(ctx =>
                {
                    using (var writer = new StreamWriter(filePath))
                    {
                        // Get properties from first item
                        var properties = items.First().GetType().GetProperties()
                            .Where(p => p.CanRead)
                                        .ToArray();

                        // Write headers
                        var headers = string.Join(",", properties.Select(p => EscapeCsvValue(p.Name)));
                        writer.WriteLine(headers);

                        // Write data rows
                        foreach (var item in items)
                        {
                            var values = properties.Select(p =>
                    {
                        var value = p.GetValue(item)?.ToString() ?? "";
                        return EscapeCsvValue(value);
                    });
                            var line = string.Join(",", values);
                            writer.WriteLine(line);
                        }
                    }

                    return new ValueTask<bool>(true);
                }, ResilienceContextPool.Shared.Get()).GetAwaiter().GetResult();
            }
            else
            {
                using (var writer = new StreamWriter(filePath))
                {
                    // Get properties from first item
                    var properties = items.First().GetType().GetProperties()
                        .Where(p => p.CanRead)
                        .ToArray();

                    // Write headers
                    var headers = string.Join(",", properties.Select(p => EscapeCsvValue(p.Name)));
                    writer.WriteLine(headers);

                    // Write data rows
                    foreach (var item in items)
                    {
                        var values = properties.Select(p =>
                        {
                            var value = p.GetValue(item)?.ToString() ?? "";
                            return EscapeCsvValue(value);
                        });
                        var line = string.Join(",", values);
                        writer.WriteLine(line);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Gets supported export formats
    /// </summary>
    public IEnumerable<string> GetSupportedFormats()
    {
        return new[] { "PDF", "Excel", "CSV" };
    }

    /// <summary>
    /// Exports a ComplianceReport to a well-formatted PDF document using PdfSharpCore.
    /// </summary>
    public async Task ExportComplianceReportToPdfAsync(ComplianceReport report, string filePath)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        var rows = new List<IEnumerable<string>>
        {
            new[] { "Field", "Value" }
        };

        rows.Add(new[] { "Enterprise ID", report.EnterpriseId.ToString() });
        rows.Add(new[] { "Generated", report.GeneratedDate.ToString("yyyy-MM-dd HH:mm") });
        rows.Add(new[] { "Overall Status", report.OverallStatus.ToString() });
        rows.Add(new[] { "Compliance Score", report.ComplianceScore.ToString("F2") });

        if (report.Violations != null && report.Violations.Any())
        {
            rows.Add(new[] { "Violations", report.Violations.Count().ToString() });
            foreach (var v in report.Violations)
            {
                rows.Add(new[] { $"Violation ({v.Severity})", $"{v.Regulation}: {v.Description}" });
            }
        }
        else
        {
            rows.Add(new[] { "Violations", "None" });
        }

        if (report.Recommendations != null && report.Recommendations.Any())
        {
            rows.Add(new[] { "Recommendations", string.Join("; ", report.Recommendations) });
        }
        else
        {
            rows.Add(new[] { "Recommendations", "None" });
        }

        var bytes = await _pdfService.CreatePdfAsync(builder =>
        {
            builder.AddPage();
            builder.AddTable(rows, 40, 60);
            return Task.CompletedTask;
        });

        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>
    /// Exports a ComplianceReport to an Excel workbook using ClosedXML.
    /// </summary>
    public async Task ExportComplianceReportToExcelAsync(ComplianceReport report, string filePath)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        await Task.Run(() =>
        {
            using (var workbook = new XLWorkbook())
            {
                // Summary sheet
                var wsSummary = workbook.Worksheets.Add("Summary");
                wsSummary.Cell(1, 1).Value = "Compliance Report";
                wsSummary.Cell(1, 1).Style.Font.Bold = true;
                wsSummary.Cell(1, 1).Style.Font.FontSize = 16;

                wsSummary.Cell(2, 1).Value = "Enterprise ID";
                wsSummary.Cell(2, 2).Value = report.EnterpriseId;

                wsSummary.Cell(3, 1).Value = "Generated";
                wsSummary.Cell(3, 2).Value = report.GeneratedDate;
                wsSummary.Cell(3, 2).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";

                wsSummary.Cell(4, 1).Value = "Overall Status";
                wsSummary.Cell(4, 2).Value = report.OverallStatus.ToString();

                wsSummary.Cell(5, 1).Value = "Compliance Score";
                wsSummary.Cell(5, 2).Value = report.ComplianceScore;
                wsSummary.Cell(5, 2).Style.NumberFormat.Format = "0.00";

                wsSummary.Columns().AdjustToContents();

                // Violations sheet
                var wsViolations = workbook.Worksheets.Add("Violations");
                wsViolations.Cell(1, 1).Value = "Regulation";
                wsViolations.Cell(1, 2).Value = "Description";
                wsViolations.Cell(1, 3).Value = "Severity";
                wsViolations.Cell(1, 4).Value = "Corrective Action";

                // Style headers
                wsViolations.Row(1).Style.Font.Bold = true;
                wsViolations.Row(1).Style.Fill.BackgroundColor = XLColor.LightBlue;

                int row = 2;
                if (report.Violations != null)
                {
                    foreach (var v in report.Violations)
                    {
                        wsViolations.Cell(row, 1).Value = v.Regulation ?? string.Empty;
                        wsViolations.Cell(row, 2).Value = v.Description ?? string.Empty;
                        wsViolations.Cell(row, 3).Value = v.Severity.ToString();
                        wsViolations.Cell(row, 4).Value = v.CorrectiveAction ?? string.Empty;
                        row++;
                    }
                }
                wsViolations.Columns().AdjustToContents();

                // Recommendations sheet
                var wsReco = workbook.Worksheets.Add("Recommendations");
                wsReco.Cell(1, 1).Value = "Recommendation";
                wsReco.Row(1).Style.Font.Bold = true;
                wsReco.Row(1).Style.Fill.BackgroundColor = XLColor.LightBlue;

                int rRow = 2;
                if (report.Recommendations != null)
                {
                    foreach (var rec in report.Recommendations)
                    {
                        wsReco.Cell(rRow, 1).Value = rec;
                        rRow++;
                    }
                }
                wsReco.Columns().AdjustToContents();

                // Save workbook
                workbook.SaveAs(filePath);
            }
        });
    }

    /// <summary>
    /// Escapes CSV values that contain commas, quotes, or newlines
    /// </summary>
    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(",", StringComparison.Ordinal) ||
            value.Contains("\"", StringComparison.Ordinal) ||
            value.Contains("\n", StringComparison.Ordinal) ||
            value.Contains("\r", StringComparison.Ordinal))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
        return value;
    }
}
