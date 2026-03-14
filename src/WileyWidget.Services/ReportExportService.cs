using System.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;
using Syncfusion.XlsIO;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of report export service.
/// Uses Syncfusion.Pdf and Syncfusion.XlsIO for branded report export generation.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly ILogger _logger;
    private static readonly PdfColor BrandPrimaryColor = new(24, 63, 93);
    private static readonly PdfColor BrandAccentColor = new(70, 138, 173);
    private static readonly PdfColor BrandMutedColor = new(93, 110, 126);
    private static readonly PdfColor BrandSurfaceColor = new(244, 247, 250);
    private static readonly PdfColor BrandBorderColor = new(210, 220, 228);
    private const float PdfLeftMargin = 40f;
    private const float PdfRightMargin = 40f;
    private const float PdfTopContent = 120f;
    private static readonly System.Drawing.Color ExcelBrandPrimaryColor = System.Drawing.Color.FromArgb(24, 63, 93);
    private static readonly System.Drawing.Color ExcelBrandAccentColor = System.Drawing.Color.FromArgb(222, 235, 247);

    public ReportExportService(ILogger logger)
    {
        _logger = logger?.ForContext<ReportExportService>() ?? throw new ArgumentNullException(nameof(logger));
        _logger.Information("ReportExportService initialized");
    }

    /// <summary>
    /// Exports data to PDF format using Syncfusion.Pdf
    /// </summary>
    public async Task ExportToPdfAsync(object data, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = EnsureOutputPath(filePath);

        _logger.Information("Exporting data to PDF: {FilePath}", normalizedPath);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (data is ReportExportDocument brandedDocument)
            {
                ExportBrandedDocumentToPdf(brandedDocument, normalizedPath, cancellationToken);
                return;
            }

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
                                cancellationToken.ThrowIfCancellationRequested();
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
                            cancellationToken.ThrowIfCancellationRequested();
                            page = document.Pages.Add();
                            gfx = page.Graphics;
                            yPosition = 40;
                        }
                    }
                }

                // Save the document
                cancellationToken.ThrowIfCancellationRequested();
                document.Save(normalizedPath);
                _logger.Information("PDF export completed successfully: {FilePath}", normalizedPath);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Exports data to Excel format using Syncfusion.XlsIO
    /// </summary>
    public async Task ExportToExcelAsync(object data, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = EnsureOutputPath(filePath);

        _logger.Information("Exporting data to Excel: {FilePath}", normalizedPath);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (data is ReportExportDocument brandedDocument)
            {
                ExportBrandedDocumentToExcel(brandedDocument, normalizedPath, cancellationToken);
                return;
            }

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
                            cancellationToken.ThrowIfCancellationRequested();

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
                cancellationToken.ThrowIfCancellationRequested();
                using (var stream = new FileStream(normalizedPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    workbook.SaveAs(stream);
                }

                _logger.Information("Excel export completed successfully: {FilePath}", normalizedPath);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Exports data to CSV format
    /// </summary>
    public async Task ExportToCsvAsync(IEnumerable<object> data, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = EnsureOutputPath(filePath);

        _logger.Information("Exporting data to CSV: {FilePath}", normalizedPath);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = data.ToList();
            if (!items.Any()) return;

            using (var writer = new StreamWriter(normalizedPath))
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
                    cancellationToken.ThrowIfCancellationRequested();

                    var values = properties.Select(p =>
                    {
                        var value = p.GetValue(item)?.ToString() ?? "";
                        return EscapeCsvValue(value);
                    });
                    var line = string.Join(",", values);
                    writer.WriteLine(line);
                }

                _logger.Information("CSV export completed successfully: {FilePath}, Rows: {RowCount}", normalizedPath, items.Count);
            }
        }, cancellationToken);
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
    public async Task ExportComplianceReportToPdfAsync(ComplianceReport report, string filePath, CancellationToken cancellationToken = default)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = EnsureOutputPath(filePath);

        _logger.Information("Exporting compliance report to PDF: {FilePath}, EnterpriseId: {EnterpriseId}", normalizedPath, report.EnterpriseId);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                        cancellationToken.ThrowIfCancellationRequested();

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
                        cancellationToken.ThrowIfCancellationRequested();

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
                cancellationToken.ThrowIfCancellationRequested();
                document.Save(normalizedPath);
                _logger.Information("Compliance report PDF export completed: {FilePath}, Violations: {ViolationCount}",
                    normalizedPath, report.Violations?.Count ?? 0);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Exports a ComplianceReport to an Excel workbook using Syncfusion.XlsIO.
    /// </summary>
    public async Task ExportComplianceReportToExcelAsync(ComplianceReport report, string filePath, CancellationToken cancellationToken = default)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = EnsureOutputPath(filePath);

        _logger.Information("Exporting compliance report to Excel: {FilePath}, EnterpriseId: {EnterpriseId}", normalizedPath, report.EnterpriseId);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                wsSummary.Range[2, 2].Text = report.EnterpriseId.ToString(CultureInfo.InvariantCulture);

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
                        cancellationToken.ThrowIfCancellationRequested();

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
                        cancellationToken.ThrowIfCancellationRequested();

                        wsReco.Range[rRow, 1].Text = rec;
                        rRow++;
                    }
                }
                wsReco.UsedRange.AutofitColumns();

                // Save workbook
                cancellationToken.ThrowIfCancellationRequested();
                using (var stream = new FileStream(normalizedPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    workbook.SaveAs(stream);
                }

                _logger.Information("Compliance report Excel export completed: {FilePath}, Violations: {ViolationCount}, Recommendations: {RecCount}",
                    normalizedPath, report.Violations?.Count ?? 0, report.Recommendations?.Count ?? 0);
            }
        }, cancellationToken);
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

    private void ExportBrandedDocumentToPdf(ReportExportDocument document, string filePath, CancellationToken cancellationToken)
    {
        using var pdfDocument = new PdfDocument();
        var page = pdfDocument.Pages.Add();
        var graphics = page.Graphics;

        var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
        var subtitleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Regular);
        var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
        var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
        var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9, PdfFontStyle.Regular);
        var titleBrush = new PdfSolidBrush(new PdfColor(255, 255, 255));
        var bodyBrush = new PdfSolidBrush(new PdfColor(28, 28, 28));
        var mutedBrush = new PdfSolidBrush(BrandMutedColor);
        var accentBrush = new PdfSolidBrush(BrandAccentColor);
        var brandBrush = new PdfSolidBrush(BrandPrimaryColor);
        var tableStripeBrush = new PdfSolidBrush(BrandSurfaceColor);
        var tableHeaderBrush = new PdfSolidBrush(BrandPrimaryColor);
        var headerTextBrush = new PdfSolidBrush(new PdfColor(255, 255, 255));
        var borderPen = new PdfPen(BrandBorderColor, 0.75f);

        var y = DrawPdfMasthead(page, graphics, document, titleFont, subtitleFont, titleBrush, bodyBrush, mutedBrush, brandBrush);

        if (document.Metadata is { Count: > 0 })
        {
            foreach (var metadata in document.Metadata)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metadataBounds = new RectangleF(PdfLeftMargin, y, page.GetClientSize().Width - PdfLeftMargin - PdfRightMargin, 18);
                graphics.DrawRectangle(tableStripeBrush, metadataBounds);
                graphics.DrawString($"{metadata.Key}: {metadata.Value}", bodyFont, mutedBrush, new PointF(metadataBounds.X + 6, metadataBounds.Y + 4));
                y += 22;
            }

            y += 8;
        }

        foreach (var section in document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var columns = section.Columns.Count > 0
                ? section.Columns
                : section.Rows.FirstOrDefault()?.Keys.ToList() ?? [];

            if (columns.Count == 0)
            {
                continue;
            }

            EnsurePdfSpace(ref page, ref graphics, ref y, 96, pdfDocument, document, titleFont, subtitleFont, titleBrush, bodyBrush, mutedBrush, brandBrush);

            var printableWidth = page.GetClientSize().Width - PdfLeftMargin - PdfRightMargin;
            var columnWidths = GetPdfColumnWidths(columns, printableWidth);
            var headerHeight = 22f;

            DrawPdfSectionTitle(graphics, section.Title, sectionFont, bodyBrush, accentBrush, y, printableWidth);
            y += 22;
            DrawPdfTableHeader(graphics, columns, columnWidths, headerFont, headerTextBrush, tableHeaderBrush, borderPen, y);
            y += headerHeight;

            var rowIndex = 0;

            foreach (var row in section.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowHeight = CalculatePdfRowHeight(row, columns, columnWidths, bodyFont);
                if (EnsurePdfSpace(ref page, ref graphics, ref y, rowHeight + headerHeight + 8, pdfDocument, document, titleFont, subtitleFont, titleBrush, bodyBrush, mutedBrush, brandBrush))
                {
                    DrawPdfSectionTitle(graphics, section.Title + " (continued)", sectionFont, bodyBrush, accentBrush, y, printableWidth);
                    y += 22;
                    DrawPdfTableHeader(graphics, columns, columnWidths, headerFont, headerTextBrush, tableHeaderBrush, borderPen, y);
                    y += headerHeight;
                }

                DrawPdfTableRow(
                    graphics,
                    row,
                    columns,
                    columnWidths,
                    bodyFont,
                    bodyBrush,
                    mutedBrush,
                    tableStripeBrush,
                    borderPen,
                    y,
                    rowHeight,
                    rowIndex % 2 == 0);

                y += rowHeight;
                rowIndex++;
            }

            y += 12;
        }

        DrawPdfFooter(page, graphics, document.Branding.Attribution, subtitleFont, mutedBrush);
        pdfDocument.Save(filePath);
        _logger.Information("Branded PDF export completed successfully: {FilePath}", filePath);
    }

    private float DrawPdfMasthead(
        PdfPage page,
        PdfGraphics graphics,
        ReportExportDocument document,
        PdfFont titleFont,
        PdfFont subtitleFont,
        PdfBrush titleBrush,
        PdfBrush bodyBrush,
        PdfBrush mutedBrush,
        PdfBrush brandBrush)
    {
        var pageWidth = page.GetClientSize().Width;
        graphics.DrawRectangle(brandBrush, new RectangleF(0, 0, pageWidth, 86));

        var titleX = 40f;
        if (TryGetReportLogoPath(document.Branding.LogoPath) is { } logoPath)
        {
            var image = new PdfBitmap(logoPath);
            graphics.DrawImage(image, 24, 18, 48, 48);
            titleX = 84f;
        }

        graphics.DrawString(document.Branding.OrganizationName, subtitleFont, titleBrush, new PointF(titleX, 18));
        graphics.DrawString(document.Title, titleFont, titleBrush, new PointF(titleX, 32));
        graphics.DrawString(document.Subtitle, subtitleFont, titleBrush, new PointF(titleX, 56));
        graphics.DrawString(document.GeneratedBy, subtitleFont, mutedBrush, new PointF(PdfLeftMargin, 98));
        graphics.DrawString($"Generated {document.GeneratedAt:yyyy-MM-dd HH:mm}", subtitleFont, bodyBrush, new PointF(pageWidth - 180, 98));
        return PdfTopContent;
    }

    private bool EnsurePdfSpace(
        ref PdfPage page,
        ref PdfGraphics graphics,
        ref float y,
        float requiredHeight,
        PdfDocument document,
        ReportExportDocument exportDocument,
        PdfFont titleFont,
        PdfFont subtitleFont,
        PdfBrush titleBrush,
        PdfBrush bodyBrush,
        PdfBrush mutedBrush,
        PdfBrush brandBrush)
    {
        var bottomLimit = page.GetClientSize().Height - 40;
        if (y + requiredHeight <= bottomLimit)
        {
            return false;
        }

        DrawPdfFooter(page, graphics, exportDocument.Branding.Attribution, subtitleFont, mutedBrush);
        page = document.Pages.Add();
        graphics = page.Graphics;
        y = DrawPdfMasthead(page, graphics, exportDocument, titleFont, subtitleFont, titleBrush, bodyBrush, mutedBrush, brandBrush);
        return true;
    }

    private static void DrawPdfFooter(PdfPage page, PdfGraphics graphics, string footerText, PdfFont font, PdfBrush brush)
    {
        var footerY = page.GetClientSize().Height - 24;
        graphics.DrawString(footerText, font, brush, new PointF(PdfLeftMargin, footerY));
    }

    private static void DrawPdfSectionTitle(PdfGraphics graphics, string sectionTitle, PdfFont font, PdfBrush textBrush, PdfBrush accentBrush, float y, float printableWidth)
    {
        graphics.DrawString(sectionTitle, font, textBrush, new PointF(PdfLeftMargin, y));
        graphics.DrawRectangle(accentBrush, new RectangleF(PdfLeftMargin, y + 16, printableWidth, 2));
    }

    private static void DrawPdfTableHeader(
        PdfGraphics graphics,
        IReadOnlyList<string> columns,
        IReadOnlyList<float> columnWidths,
        PdfFont font,
        PdfBrush textBrush,
        PdfBrush backgroundBrush,
        PdfPen borderPen,
        float y)
    {
        var x = PdfLeftMargin;
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var bounds = new RectangleF(x, y, columnWidths[columnIndex], 22);
            graphics.DrawRectangle(backgroundBrush, bounds);
            graphics.DrawRectangle(borderPen, bounds);
            graphics.DrawString(columns[columnIndex], font, textBrush, new PointF(bounds.X + 4, bounds.Y + 5));
            x += columnWidths[columnIndex];
        }
    }

    private static void DrawPdfTableRow(
        PdfGraphics graphics,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyList<string> columns,
        IReadOnlyList<float> columnWidths,
        PdfFont font,
        PdfBrush bodyBrush,
        PdfBrush mutedBrush,
        PdfBrush stripeBrush,
        PdfPen borderPen,
        float y,
        float rowHeight,
        bool shaded)
    {
        var x = PdfLeftMargin;
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var bounds = new RectangleF(x, y, columnWidths[columnIndex], rowHeight);
            if (shaded)
            {
                graphics.DrawRectangle(stripeBrush, bounds);
            }

            graphics.DrawRectangle(borderPen, bounds);
            row.TryGetValue(columns[columnIndex], out var value);
            var brush = IsMutedColumn(columns[columnIndex]) ? mutedBrush : bodyBrush;
            DrawPdfCellText(
                graphics,
                value ?? string.Empty,
                font,
                brush,
                bounds,
                IsNumericColumn(columns[columnIndex]));
            x += columnWidths[columnIndex];
        }
    }

    private static void DrawPdfCellText(PdfGraphics graphics, string value, PdfFont font, PdfBrush brush, RectangleF bounds, bool alignRight)
    {
        var wrappedLines = WrapPdfText(value, font, Math.Max(24f, bounds.Width - 8f));
        var y = bounds.Y + 4f;
        foreach (var line in wrappedLines)
        {
            var x = bounds.X + 4f;
            if (alignRight)
            {
                x = Math.Max(bounds.X + 4f, bounds.Right - font.MeasureString(line).Width - 4f);
            }

            graphics.DrawString(line, font, brush, new PointF(x, y));
            y += font.MeasureString(line).Height + 1f;
        }
    }

    private static float CalculatePdfRowHeight(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyList<string> columns,
        IReadOnlyList<float> columnWidths,
        PdfFont font)
    {
        var maxHeight = 18f;
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            row.TryGetValue(columns[columnIndex], out var value);
            var wrappedLines = WrapPdfText(value ?? string.Empty, font, Math.Max(24f, columnWidths[columnIndex] - 8f));
            var lineHeight = wrappedLines.Count * (font.MeasureString("Ag").Height + 1f);
            maxHeight = Math.Max(maxHeight, lineHeight + 8f);
        }

        return maxHeight;
    }

    private static IReadOnlyList<float> GetPdfColumnWidths(IReadOnlyList<string> columns, float printableWidth)
    {
        var weights = columns.Select(GetPdfColumnWeight).ToArray();
        var totalWeight = weights.Sum();
        var widths = new float[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            widths[index] = printableWidth * (weights[index] / totalWeight);
        }

        return widths;
    }

    private static float GetPdfColumnWeight(string columnName)
    {
        var normalized = columnName.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalized switch
        {
            var name when name.Contains("Description", StringComparison.OrdinalIgnoreCase) => 2.7f,
            var name when name.Contains("Recommendation", StringComparison.OrdinalIgnoreCase) => 2.6f,
            var name when name.Contains("Action", StringComparison.OrdinalIgnoreCase) => 2.2f,
            var name when name.Contains("Metric", StringComparison.OrdinalIgnoreCase) => 1.4f,
            var name when name.Contains("Department", StringComparison.OrdinalIgnoreCase) => 1.4f,
            var name when name.Contains("Entity", StringComparison.OrdinalIgnoreCase) => 1.35f,
            var name when name.Contains("Account", StringComparison.OrdinalIgnoreCase) => 1.15f,
            var name when name.Contains("Budget", StringComparison.OrdinalIgnoreCase) => 1.05f,
            var name when name.Contains("Actual", StringComparison.OrdinalIgnoreCase) => 1.05f,
            var name when name.Contains("Variance", StringComparison.OrdinalIgnoreCase) => 1.05f,
            var name when name.Contains("Percent", StringComparison.OrdinalIgnoreCase) => 0.9f,
            var name when name.Contains("Used", StringComparison.OrdinalIgnoreCase) => 0.9f,
            _ => 1f,
        };
    }

    private static bool IsNumericColumn(string columnName)
    {
        return columnName.Contains("Budget", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("Actual", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("Variance", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("Percent", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("Used", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("Score", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("Value", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMutedColumn(string columnName)
    {
        return columnName.Contains("Entity", StringComparison.OrdinalIgnoreCase)
            || columnName.Contains("Department", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> WrapPdfText(string value, PdfFont font, float width)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("\r", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(normalized))
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        foreach (var paragraph in normalized.Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var currentLine = words[0];
            for (var index = 1; index < words.Length; index++)
            {
                var candidate = currentLine + " " + words[index];
                if (font.MeasureString(candidate).Width <= width)
                {
                    currentLine = candidate;
                    continue;
                }

                lines.Add(currentLine);
                currentLine = words[index];
            }

            lines.Add(currentLine);
        }

        return lines;
    }

    private void ExportBrandedDocumentToExcel(ReportExportDocument document, string filePath, CancellationToken cancellationToken)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(Math.Max(document.Sections.Count + 1, 1));
        var overviewSheet = workbook.Worksheets[0];
        overviewSheet.Name = "Overview";

        RenderExcelOverviewSheet(overviewSheet, document);

        for (var sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worksheet = workbook.Worksheets[sectionIndex + 1];
            RenderExcelSectionSheet(worksheet, document, document.Sections[sectionIndex]);
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        workbook.SaveAs(stream);
        _logger.Information("Branded Excel export completed successfully: {FilePath}", filePath);
    }

    private void RenderExcelOverviewSheet(IWorksheet worksheet, ReportExportDocument document)
    {
        worksheet.Range[1, 2, 1, 6].Merge();
        worksheet.Range[1, 2].Text = document.Title;
        worksheet.Range[1, 2].CellStyle.Font.Bold = true;
        worksheet.Range[1, 2].CellStyle.Font.Size = 18;
        worksheet.Range[1, 2].CellStyle.Color = ExcelBrandPrimaryColor;
        worksheet.Range[1, 2].CellStyle.Font.Color = ExcelKnownColors.White;

        worksheet.Range[2, 2, 2, 6].Merge();
        worksheet.Range[2, 2].Text = document.Subtitle;
        worksheet.Range[2, 2].CellStyle.Color = ExcelBrandAccentColor;

        worksheet.Range[3, 2, 3, 6].Merge();
        worksheet.Range[3, 2].Text = document.GeneratedBy;

        worksheet.Range[4, 2].Text = "Generated";
        worksheet.Range[4, 3].DateTime = document.GeneratedAt;
        worksheet.Range[4, 3].NumberFormat = "yyyy-MM-dd HH:mm:ss";

        if (TryGetReportLogoPath(document.Branding.LogoPath) is { } logoPath)
        {
            var picture = worksheet.Pictures.AddPicture(1, 1, logoPath);
            picture.Height = 48;
            picture.Width = 48;
        }

        var row = 6;
        if (document.Metadata is { Count: > 0 })
        {
            foreach (var metadata in document.Metadata)
            {
                worksheet.Range[row, 2].Text = metadata.Key;
                worksheet.Range[row, 2].CellStyle.Font.Bold = true;
                worksheet.Range[row, 3].Text = metadata.Value;
                row++;
            }
        }

        worksheet.Range[row + 1, 2].Text = "Sections";
        worksheet.Range[row + 1, 2].CellStyle.Font.Bold = true;

        for (var index = 0; index < document.Sections.Count; index++)
        {
            worksheet.Range[row + 2 + index, 2].Text = document.Sections[index].Title;
            worksheet.Range[row + 2 + index, 3].Text = $"{document.Sections[index].Rows.Count} rows";
        }

        worksheet.UsedRange.AutofitColumns();
    }

    private void RenderExcelSectionSheet(IWorksheet worksheet, ReportExportDocument document, ReportExportSection section)
    {
        worksheet.Name = SanitizeWorksheetName(section.Title);

        worksheet.Range[1, 2, 1, 6].Merge();
        worksheet.Range[1, 2].Text = document.Title;
        worksheet.Range[1, 2].CellStyle.Font.Bold = true;
        worksheet.Range[1, 2].CellStyle.Font.Size = 16;
        worksheet.Range[1, 2].CellStyle.Color = ExcelBrandPrimaryColor;
        worksheet.Range[1, 2].CellStyle.Font.Color = ExcelKnownColors.White;

        worksheet.Range[2, 2, 2, 6].Merge();
        worksheet.Range[2, 2].Text = section.Title;
        worksheet.Range[2, 2].CellStyle.Color = ExcelBrandAccentColor;

        if (TryGetReportLogoPath(document.Branding.LogoPath) is { } logoPath)
        {
            var picture = worksheet.Pictures.AddPicture(1, 1, logoPath);
            picture.Height = 40;
            picture.Width = 40;
        }

        var columns = section.Columns.Count > 0
            ? section.Columns
            : section.Rows.FirstOrDefault()?.Keys.ToList() ?? [];

        var headerRow = 4;
        for (var index = 0; index < columns.Count; index++)
        {
            var cell = worksheet.Range[headerRow, index + 1];
            cell.Text = columns[index];
            cell.CellStyle.Font.Bold = true;
            cell.CellStyle.Color = ExcelBrandAccentColor;
        }

        var rowIndex = headerRow + 1;
        foreach (var row in section.Rows)
        {
            for (var index = 0; index < columns.Count; index++)
            {
                row.TryGetValue(columns[index], out var value);
                worksheet.Range[rowIndex, index + 1].Text = value ?? string.Empty;
            }
            rowIndex++;
        }

        worksheet.UsedRange.AutofitColumns();
    }

    private static string SanitizeWorksheetName(string name)
    {
        var invalidCharacters = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = new string(name.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Report";
        }

        return sanitized.Length <= 31 ? sanitized : sanitized.Substring(0, 31);
    }

    private static string? TryGetReportLogoPath(string? logoPath)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            return logoPath;
        }

        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("WILEYWIDGET_REPORT_LOGO_PATH"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-report-logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-report-logo.jpg"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-brand-hero.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Branding", "wiley-brand-hero.jpg"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-report-logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-report-logo.jpg"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-brand-hero.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "Resources", "Branding", "wiley-brand-hero.jpg"),
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static string EnsureOutputPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        var normalizedPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(normalizedPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The output directory could not be determined.");
        }

        Directory.CreateDirectory(directory);
        return normalizedPath;
    }
}
