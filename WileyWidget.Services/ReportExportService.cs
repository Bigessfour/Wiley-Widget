using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.XlsIO;
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
            using (var document = new PdfDocument())
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Set up font
                var font = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

                float yPosition = 10;

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
                        float xPosition = 10;
                        foreach (var prop in properties)
                        {
                            graphics.DrawString(prop.Name, font, PdfBrushes.Black, xPosition, yPosition);
                            xPosition += 100; // Fixed column width
                        }
                        yPosition += 20;

                        // Add data rows
                        foreach (var item in items)
                        {
                            xPosition = 10;
                            foreach (var prop in properties)
                            {
                                var value = prop.GetValue(item)?.ToString() ?? "";
                                graphics.DrawString(value, font, PdfBrushes.Black, xPosition, yPosition);
                                xPosition += 100;
                            }
                            yPosition += 15;

                            // Start new page if needed
                            if (yPosition > page.GetClientSize().Height - 50)
                            {
                                page = document.Pages.Add();
                                graphics = page.Graphics;
                                yPosition = 10;
                            }
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
                        var value = prop.GetValue(data)?.ToString() ?? "";
                        var text = $"{prop.Name}: {value}";
                        graphics.DrawString(text, font, PdfBrushes.Black, 10, yPosition);
                        yPosition += 15;
                    }
                }

                // Save the document
                document.Save(filePath);
            }
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
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Excel2016;

                var workbook = application.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];

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
                            worksheet.Range[rowIndex, i + 1].Value = properties[i].Name;
                        }
                        rowIndex++;

                        // Add data rows
                        foreach (var item in items)
                        {
                            for (int i = 0; i < properties.Length; i++)
                            {
                                var value = properties[i].GetValue(item)?.ToString() ?? "";
                                worksheet.Range[rowIndex, i + 1].Value = value;
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
                        worksheet.Range[rowIndex, 1].Value = prop.Name;
                        worksheet.Range[rowIndex, 2].Value = prop.GetValue(data)?.ToString() ?? "";
                        rowIndex++;
                    }
                }

                // Auto-fit columns
                worksheet.UsedRange.AutofitColumns();

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
    /// Uses Syncfusion.Pdf documented APIs: PdfDocument, PdfPage, PdfGraphics, PdfFont
    /// </summary>
    public async Task ExportComplianceReportToPdfAsync(ComplianceReport report, string filePath)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        await Task.Run(() =>
        {
            using (var document = new PdfDocument())
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;
                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
                var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11);

                float y = 20;
                graphics.DrawString("Compliance Report", titleFont, PdfBrushes.Black, 20, y); y += 24;
                graphics.DrawString($"Enterprise ID: {report.EnterpriseId}", bodyFont, PdfBrushes.Black, 20, y); y += 16;
                graphics.DrawString($"Generated: {report.GeneratedDate:yyyy-MM-dd HH:mm}", bodyFont, PdfBrushes.Black, 20, y); y += 20;
                graphics.DrawString($"Overall Status: {report.OverallStatus}", headerFont, PdfBrushes.Black, 20, y); y += 18;
                graphics.DrawString($"Compliance Score: {report.ComplianceScore}", bodyFont, PdfBrushes.Black, 20, y); y += 20;

                // Violations table
                graphics.DrawString("Violations:", headerFont, PdfBrushes.Black, 20, y); y += 16;
                if (report.Violations != null && report.Violations.Any())
                {
                    foreach (var v in report.Violations)
                    {
                        var line = $"- [{v.Severity}] {v.Regulation}: {v.Description} | Action: {v.CorrectiveAction}";
                        graphics.DrawString(line, bodyFont, PdfBrushes.Black, 25, y);
                        y += 14;
                        if (y > page.GetClientSize().Height - 40)
                        {
                            page = document.Pages.Add();
                            graphics = page.Graphics;
                            y = 20;
                        }
                    }
                }
                else
                {
                    graphics.DrawString("No violations.", bodyFont, PdfBrushes.Black, 25, y); y += 16;
                }

                // Recommendations
                graphics.DrawString("Recommendations:", headerFont, PdfBrushes.Black, 20, y); y += 16;
                if (report.Recommendations != null && report.Recommendations.Any())
                {
                    foreach (var r in report.Recommendations)
                    {
                        graphics.DrawString("- " + r, bodyFont, PdfBrushes.Black, 25, y);
                        y += 14;
                        if (y > page.GetClientSize().Height - 40)
                        {
                            page = document.Pages.Add();
                            graphics = page.Graphics;
                            y = 20;
                        }
                    }
                }
                else
                {
                    graphics.DrawString("No recommendations provided.", bodyFont, PdfBrushes.Black, 25, y); y += 16;
                }

                // Save
                document.Save(filePath);
            }
        });
    }

    /// <summary>
    /// Exports a ComplianceReport to an Excel workbook with separate sections.
    /// Uses Syncfusion.XlsIO documented APIs: ExcelEngine, IWorkbook, IWorksheet
    /// </summary>
    public async Task ExportComplianceReportToExcelAsync(ComplianceReport report, string filePath)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        await Task.Run(() =>
        {
            using (var engine = new ExcelEngine())
            {
                var app = engine.Excel;
                app.DefaultVersion = ExcelVersion.Excel2016;
                var wb = app.Workbooks.Create(3);

                // Summary sheet
                var wsSummary = wb.Worksheets[0];
                wsSummary.Name = "Summary";
                wsSummary.Range[1, 1].Text = "Compliance Report";
                wsSummary.Range[2, 1].Text = "Enterprise ID";
                wsSummary.Range[2, 2].Number = report.EnterpriseId;
                wsSummary.Range[3, 1].Text = "Generated";
                wsSummary.Range[3, 2].DateTime = report.GeneratedDate;
                wsSummary.Range[4, 1].Text = "Overall Status";
                wsSummary.Range[4, 2].Text = report.OverallStatus.ToString();
                wsSummary.Range[5, 1].Text = "Compliance Score";
                wsSummary.Range[5, 2].Number = report.ComplianceScore;
                wsSummary.UsedRange.AutofitColumns();

                // Violations sheet
                var wsViolations = wb.Worksheets[1];
                wsViolations.Name = "Violations";
                wsViolations.Range[1, 1].Text = "Regulation";
                wsViolations.Range[1, 2].Text = "Description";
                wsViolations.Range[1, 3].Text = "Severity";
                wsViolations.Range[1, 4].Text = "Corrective Action";
                int row = 2;
                if (report.Violations != null)
                {
                    foreach (var v in report.Violations)
                    {
                        wsViolations.Range[row, 1].Text = v.Regulation ?? string.Empty;
                        wsViolations.Range[row, 2].Text = v.Description ?? string.Empty;
                        wsViolations.Range[row, 3].Text = v.Severity.ToString();
                        wsViolations.Range[row, 4].Text = v.CorrectiveAction ?? string.Empty;
                        row++;
                    }
                }
                wsViolations.UsedRange.AutofitColumns();

                // Recommendations sheet
                var wsReco = wb.Worksheets[2];
                wsReco.Name = "Recommendations";
                wsReco.Range[1, 1].Text = "Recommendation";
                int rRow = 2;
                if (report.Recommendations != null)
                {
                    foreach (var rec in report.Recommendations)
                    {
                        wsReco.Range[rRow, 1].Text = rec;
                        rRow++;
                    }
                }
                wsReco.UsedRange.AutofitColumns();

                wb.SaveAs(filePath);
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
