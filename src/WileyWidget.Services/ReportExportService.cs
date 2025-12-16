using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.XlsIO;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of report export service.
/// Uses ClosedXML for Excel and Syncfusion.Pdf for PDF generation.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly ILogger<ReportExportService> _logger;

    public ReportExportService(ILogger<ReportExportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Exports data to PDF format using Syncfusion.Pdf
    /// </summary>
    public async Task ExportToPdfAsync(object data, string filePath)
    {
        await Task.Run(() =>
        {
            using (var document = new PdfDocument())
            {
                var page = document.Pages.Add();
                var gfx = page.Graphics;
                var font = new PdfStandardFont(PdfFontFamily.Helvetica, 12);
                var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
                var brush = new PdfSolidBrush(new PdfColor(0, 0, 0));

                float yPosition = 40;
                float leftMargin = 40;
                float lineHeight = 20;

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
                        float xPosition = leftMargin;
                        float columnWidth = 100;
                        foreach (var prop in properties)
                        {
                            gfx.DrawString(prop.Name, headerFont, brush, new PointF(xPosition, yPosition));
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
                                gfx.DrawString(value, font, brush, new PointF(xPosition, yPosition));
                                xPosition += columnWidth;
                            }
                            yPosition += lineHeight;

                            // Start new page if needed
                            if (yPosition > page.GetClientSize().Height - 100)
                            {
                                page = document.Pages.Add();
                                gfx = page.Graphics;
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
                        gfx.DrawString(text, font, brush, new PointF(leftMargin, yPosition));
                        yPosition += lineHeight;

                        // Start new page if needed
                        if (yPosition > page.GetClientSize().Height - 100)
                        {
                            page = document.Pages.Add();
                            gfx = page.Graphics;
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
    /// Exports data to Excel format using Syncfusion.XlsIO
    /// </summary>
    public async Task ExportToExcelAsync(object data, string filePath)
    {
        await Task.Run(() =>
        {
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;
                var workbook = application.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];
                worksheet.Name = "Data";
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
                            var cell = worksheet.Range[rowIndex, i + 1];
                            cell.Text = properties[i].Name;
                            cell.CellStyle.Font.Bold = true;
                            cell.CellStyle.Color = System.Drawing.Color.LightBlue;
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
                                    worksheet.Range[rowIndex, i + 1].Text = value.ToString();
                                }
                            }
                            rowIndex++;
                        }

                        // Auto-fit columns
                        worksheet.UsedRange.AutofitColumns();
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
                        worksheet.Range[rowIndex, 1].Text = prop.Name;
                        worksheet.Range[rowIndex, 1].CellStyle.Font.Bold = true;
                        worksheet.Range[rowIndex, 2].Text = prop.GetValue(data)?.ToString() ?? "";
                        rowIndex++;
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();
                }

                // Save the workbook
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    workbook.SaveAs(stream);
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
    /// Exports a ComplianceReport to a well-formatted PDF document using Syncfusion.Pdf.
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
                var gfx = page.Graphics;

                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
                var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11);
                var brush = new PdfSolidBrush(new PdfColor(0, 0, 0));
                var grayBrush = new PdfSolidBrush(new PdfColor(64, 64, 64));

                float y = 40;
                float leftMargin = 40;
                float lineHeight = 20;

                // Title
                gfx.DrawString("Compliance Report", titleFont, brush, new PointF(leftMargin, y));
                y += 30;

                // Report details
                gfx.DrawString($"Enterprise ID: {report.EnterpriseId}", bodyFont, brush, new PointF(leftMargin, y));
                y += lineHeight;
                gfx.DrawString($"Generated: {report.GeneratedDate:yyyy-MM-dd HH:mm}", bodyFont, brush, new PointF(leftMargin, y));
                y += lineHeight + 5;

                gfx.DrawString($"Overall Status: {report.OverallStatus}", headerFont, brush, new PointF(leftMargin, y));
                y += lineHeight + 5;
                gfx.DrawString($"Compliance Score: {report.ComplianceScore:F2}", bodyFont, brush, new PointF(leftMargin, y));
                y += lineHeight + 10;

                // Violations section
                gfx.DrawString("Violations:", headerFont, brush, new PointF(leftMargin, y));
                y += lineHeight;

                if (report.Violations != null && report.Violations.Any())
                {
                    foreach (var v in report.Violations)
                    {
                        var line = $"- [{v.Severity}] {v.Regulation}: {v.Description}";
                        if (line.Length > 100) line = line.Substring(0, 97) + "...";
                        gfx.DrawString(line, bodyFont, brush, new PointF(leftMargin + 10, y));
                        y += lineHeight;

                        var actionLine = $"  Action: {v.CorrectiveAction}";
                        if (actionLine.Length > 100) actionLine = actionLine.Substring(0, 97) + "...";
                        gfx.DrawString(actionLine, bodyFont, grayBrush, new PointF(leftMargin + 10, y));
                        y += lineHeight;

                        // Check for new page
                        if (y > page.GetClientSize().Height - 100)
                        {
                            page = document.Pages.Add();
                            gfx = page.Graphics;
                            y = 40;
                        }
                    }
                }
                else
                {
                    gfx.DrawString("No violations.", bodyFont, brush, new PointF(leftMargin + 10, y));
                    y += lineHeight;
                }

                y += 10;

                // Recommendations section
                gfx.DrawString("Recommendations:", headerFont, brush, new PointF(leftMargin, y));
                y += lineHeight;

                if (report.Recommendations != null && report.Recommendations.Any())
                {
                    foreach (var r in report.Recommendations)
                    {
                        var recText = $"- {r}";
                        if (recText.Length > 100) recText = recText.Substring(0, 97) + "...";
                        gfx.DrawString(recText, bodyFont, brush, new PointF(leftMargin + 10, y));
                        y += lineHeight;

                        // Check for new page
                        if (y > page.GetClientSize().Height - 100)
                        {
                            page = document.Pages.Add();
                            gfx = page.Graphics;
                            y = 40;
                        }
                    }
                }
                else
                {
                    gfx.DrawString("No recommendations provided.", bodyFont, brush, new PointF(leftMargin + 10, y));
                }

                // Save document
                document.Save(filePath);
            }
        });
    }

    /// <summary>
    /// Exports a ComplianceReport to an Excel workbook using Syncfusion.XlsIO.
    /// </summary>
    public async Task ExportComplianceReportToExcelAsync(ComplianceReport report, string filePath)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

        await Task.Run(() =>
        {
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;
                var workbook = application.Workbooks.Create(3);

                // Summary sheet
                var wsSummary = workbook.Worksheets[0];
                wsSummary.Name = "Summary";
                wsSummary.Range[1, 1].Text = "Compliance Report";
                wsSummary.Range[1, 1].CellStyle.Font.Bold = true;
                wsSummary.Range[1, 1].CellStyle.Font.Size = 16;

                wsSummary.Range[2, 1].Text = "Enterprise ID";
                wsSummary.Range[2, 2].Text = report.EnterpriseId.ToString();

                wsSummary.Range[3, 1].Text = "Generated";
                wsSummary.Range[3, 2].DateTime = report.GeneratedDate;
                wsSummary.Range[3, 2].NumberFormat = "yyyy-MM-dd HH:mm:ss";

                wsSummary.Range[4, 1].Text = "Overall Status";
                wsSummary.Range[4, 2].Text = report.OverallStatus.ToString();

                wsSummary.Range[5, 1].Text = "Compliance Score";
                wsSummary.Range[5, 2].Number = report.ComplianceScore;
                wsSummary.Range[5, 2].NumberFormat = "0.00";

                wsSummary.UsedRange.AutofitColumns();

                // Violations sheet
                var wsViolations = workbook.Worksheets[1];
                wsViolations.Name = "Violations";
                wsViolations.Range[1, 1].Text = "Regulation";
                wsViolations.Range[1, 2].Text = "Description";
                wsViolations.Range[1, 3].Text = "Severity";
                wsViolations.Range[1, 4].Text = "Corrective Action";

                // Style headers
                var headerRange = wsViolations.Range[1, 1, 1, 4];
                headerRange.CellStyle.Font.Bold = true;
                headerRange.CellStyle.Color = System.Drawing.Color.LightBlue;

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
                var wsReco = workbook.Worksheets[2];
                wsReco.Name = "Recommendations";
                wsReco.Range[1, 1].Text = "Recommendation";
                wsReco.Range[1, 1].CellStyle.Font.Bold = true;
                wsReco.Range[1, 1].CellStyle.Color = System.Drawing.Color.LightBlue;

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

                // Save workbook
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    workbook.SaveAs(stream);
                }
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
