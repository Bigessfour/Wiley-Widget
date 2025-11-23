using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of report export service
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly ILogger<ReportExportService> _logger;

    public ReportExportService(ILogger<ReportExportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Exports data to PDF format
    /// </summary>
    public async Task ExportToPdfAsync(object data, string filePath)
    {
        // Offload PDF generation to a background thread (CPU/disk-bound)
        await Task.Run(() =>
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Content().Column(column =>
                    {
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

                                // Create table
                                column.Item().Table(table =>
                                {
                                    // Define columns
                                    table.ColumnsDefinition(columns =>
                                    {
                                        for (int i = 0; i < properties.Length; i++)
                                        {
                                            columns.RelativeColumn();
                                        }
                                    });

                                    // Add headers
                                    table.Header(header =>
                                    {
                                        foreach (var prop in properties)
                                        {
                                            header.Cell().Element(cell => cell.Border(1).Padding(5).Text(prop.Name).Bold());
                                        }
                                    });

                                    // Add data rows
                                    foreach (var item in items)
                                    {
                                        foreach (var prop in properties)
                                        {
                                            var value = prop.GetValue(item)?.ToString() ?? "";
                                            table.Cell().Element(cell => cell.Border(1).Padding(5).Text(value));
                                        }
                                    }
                                });
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
                                var value = prop.GetValue(data)?.ToString() ?? "";
                                var text = $"{prop.Name}: {value}";
                                column.Item().Text(text);
                            }
                        }
                    });
                });
            }).GeneratePdf(filePath);
        });
    }

    /// <summary>
    /// Exports data to Excel format
    /// </summary>
    public async Task ExportToExcelAsync(object data, string filePath)
    {
        // Offload Excel generation to a background thread (CPU/disk-bound)
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
                        }
                        rowIndex++;

                        // Add data rows
                        foreach (var item in items)
                        {
                            for (int i = 0; i < properties.Length; i++)
                            {
                                var value = properties[i].GetValue(item)?.ToString() ?? "";
                                worksheet.Cell(rowIndex, i + 1).Value = value;
                            }
                            rowIndex++;
                        }
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
                        worksheet.Cell(rowIndex, 2).Value = prop.GetValue(data)?.ToString() ?? "";
                        rowIndex++;
                    }
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                // Save the workbook
                workbook.SaveAs(filePath);
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
    /// Exports a ComplianceReport to a well-formatted PDF document.
    /// Uses QuestPDF fluent API for document generation
    /// </summary>
    public async Task ExportComplianceReportToPdfAsync(ComplianceReport report, string filePath)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        await Task.Run(() =>
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(column =>
                    {
                        // Title
                        column.Item().Text("Compliance Report").FontSize(18).Bold();
                        column.Item().PaddingVertical(10);

                        // Basic info
                        column.Item().Text($"Enterprise ID: {report.EnterpriseId}");
                        column.Item().Text($"Generated: {report.GeneratedDate:yyyy-MM-dd HH:mm}");
                        column.Item().PaddingVertical(5);

                        // Status
                        column.Item().Text($"Overall Status: {report.OverallStatus}").FontSize(14).Bold();
                        column.Item().Text($"Compliance Score: {report.ComplianceScore}");
                        column.Item().PaddingVertical(10);

                        // Violations
                        column.Item().Text("Violations:").FontSize(14).Bold();
                        if (report.Violations != null && report.Violations.Any())
                        {
                            foreach (var v in report.Violations)
                            {
                                var line = $"[{v.Severity}] {v.Regulation}: {v.Description} | Action: {v.CorrectiveAction}";
                                column.Item().Text("- " + line).PaddingLeft(10);
                            }
                        }
                        else
                        {
                            column.Item().Text("No violations.").PaddingLeft(10);
                        }
                        column.Item().PaddingVertical(10);

                        // Recommendations
                        column.Item().Text("Recommendations:").FontSize(14).Bold();
                        if (report.Recommendations != null && report.Recommendations.Any())
                        {
                            foreach (var r in report.Recommendations)
                            {
                                column.Item().Text("- " + r).PaddingLeft(10);
                            }
                        }
                        else
                        {
                            column.Item().Text("No recommendations provided.").PaddingLeft(10);
                        }
                    });
                });
            }).GeneratePdf(filePath);
        });
    }

    /// <summary>
    /// Exports a ComplianceReport to an Excel workbook with separate sections.
    /// Uses ClosedXML for Excel generation
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
                wsSummary.Cell(2, 1).Value = "Enterprise ID";
                wsSummary.Cell(2, 2).Value = report.EnterpriseId;
                wsSummary.Cell(3, 1).Value = "Generated";
                wsSummary.Cell(3, 2).Value = report.GeneratedDate;
                wsSummary.Cell(4, 1).Value = "Overall Status";
                wsSummary.Cell(4, 2).Value = report.OverallStatus.ToString();
                wsSummary.Cell(5, 1).Value = "Compliance Score";
                wsSummary.Cell(5, 2).Value = report.ComplianceScore;
                wsSummary.Columns().AdjustToContents();

                // Violations sheet
                var wsViolations = workbook.Worksheets.Add("Violations");
                wsViolations.Cell(1, 1).Value = "Regulation";
                wsViolations.Cell(1, 2).Value = "Description";
                wsViolations.Cell(1, 3).Value = "Severity";
                wsViolations.Cell(1, 4).Value = "Corrective Action";
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

                workbook.SaveAs(filePath);
            }
        });
    }

    /// <summary>
    /// Escapes CSV values that contain commas, quotes, or newlines
    /// </summary>
    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(",", StringComparison.Ordinal) || value.Contains("\"", StringComparison.Ordinal) || value.Contains("\n", StringComparison.Ordinal) || value.Contains("\r", StringComparison.Ordinal))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
        return value;
    }
}
