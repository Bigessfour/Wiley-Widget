using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of report export service using open-source libraries.
/// Uses ClosedXML for Excel and PdfSharpCore for PDF generation.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly ILogger<ReportExportService> _logger;

    public ReportExportService(ILogger<ReportExportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Exports data to PDF format using PdfSharpCore
    /// </summary>
    public async Task ExportToPdfAsync(object data, string filePath)
    {
        await Task.Run(() =>
        {
            using (var document = new PdfDocument())
            {
                var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);
                var font = new XFont("Arial", 12, XFontStyle.Regular);
                var headerFont = new XFont("Arial", 14, XFontStyle.Bold);

                double yPosition = 40;
                double leftMargin = 40;
                double lineHeight = 20;

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
                        double xPosition = leftMargin;
                        double columnWidth = 100;
                        foreach (var prop in properties)
                        {
                            gfx.DrawString(prop.Name, headerFont, XBrushes.Black, xPosition, yPosition);
                            xPosition += columnWidth;
                        }
                        yPosition += lineHeight + 5;

                        // Add data rows
                        foreach (var item in items)
                        {
                            xPosition = leftMargin;
                            foreach (var prop in properties)
                            {
                                var value = prop.GetValue(item)?.ToString() ?? "";
                                if (value.Length > 20) value = value.Substring(0, 17) + "...";
                                gfx.DrawString(value, font, XBrushes.Black, xPosition, yPosition);
                                xPosition += columnWidth;
                            }
                            yPosition += lineHeight;

                            // Start new page if needed
                            if (yPosition > page.Height - 100)
                            {
                                page = document.AddPage();
                                gfx = XGraphics.FromPdfPage(page);
                                yPosition = 40;
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
                        gfx.DrawString(text, font, XBrushes.Black, leftMargin, yPosition);
                        yPosition += lineHeight;

                        // Start new page if needed
                        if (yPosition > page.Height - 100)
                        {
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            yPosition = 40;
                        }
                    }
                }

                // Save the document
                document.Save(filePath);
            }
        });
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
    /// Exports a ComplianceReport to a well-formatted PDF document using PdfSharpCore.
    /// </summary>
    public async Task ExportComplianceReportToPdfAsync(ComplianceReport report, string filePath)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        await Task.Run(() =>
        {
            using (var document = new PdfDocument())
            {
                var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
                var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
                var bodyFont = new XFont("Arial", 11, XFontStyle.Regular);

                double y = 40;
                double leftMargin = 40;
                double lineHeight = 20;

                // Title
                gfx.DrawString("Compliance Report", titleFont, XBrushes.Black, leftMargin, y);
                y += 30;

                // Report details
                gfx.DrawString($"Enterprise ID: {report.EnterpriseId}", bodyFont, XBrushes.Black, leftMargin, y);
                y += lineHeight;
                gfx.DrawString($"Generated: {report.GeneratedDate:yyyy-MM-dd HH:mm}", bodyFont, XBrushes.Black, leftMargin, y);
                y += lineHeight + 5;

                gfx.DrawString($"Overall Status: {report.OverallStatus}", headerFont, XBrushes.Black, leftMargin, y);
                y += lineHeight + 5;
                gfx.DrawString($"Compliance Score: {report.ComplianceScore:F2}", bodyFont, XBrushes.Black, leftMargin, y);
                y += lineHeight + 10;

                // Violations section
                gfx.DrawString("Violations:", headerFont, XBrushes.Black, leftMargin, y);
                y += lineHeight;

                if (report.Violations != null && report.Violations.Any())
                {
                    foreach (var v in report.Violations)
                    {
                        var line = $"- [{v.Severity}] {v.Regulation}: {v.Description}";
                        if (line.Length > 100) line = line.Substring(0, 97) + "...";
                        gfx.DrawString(line, bodyFont, XBrushes.Black, leftMargin + 10, y);
                        y += lineHeight;

                        var actionLine = $"  Action: {v.CorrectiveAction}";
                        if (actionLine.Length > 100) actionLine = actionLine.Substring(0, 97) + "...";
                        gfx.DrawString(actionLine, bodyFont, XBrushes.DarkGray, leftMargin + 10, y);
                        y += lineHeight;

                        // Check for new page
                        if (y > page.Height - 100)
                        {
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            y = 40;
                        }
                    }
                }
                else
                {
                    gfx.DrawString("No violations.", bodyFont, XBrushes.Black, leftMargin + 10, y);
                    y += lineHeight;
                }

                y += 10;

                // Recommendations section
                gfx.DrawString("Recommendations:", headerFont, XBrushes.Black, leftMargin, y);
                y += lineHeight;

                if (report.Recommendations != null && report.Recommendations.Any())
                {
                    foreach (var r in report.Recommendations)
                    {
                        var recText = $"- {r}";
                        if (recText.Length > 100) recText = recText.Substring(0, 97) + "...";
                        gfx.DrawString(recText, bodyFont, XBrushes.Black, leftMargin + 10, y);
                        y += lineHeight;

                        // Check for new page
                        if (y > page.Height - 100)
                        {
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            y = 40;
                        }
                    }
                }
                else
                {
                    gfx.DrawString("No recommendations provided.", bodyFont, XBrushes.Black, leftMargin + 10, y);
                }

                // Save document
                document.Save(filePath);
            }
        });
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
