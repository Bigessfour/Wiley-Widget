using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.Drawing;
using WileyWidget.Models;

namespace WileyWidget.Services.Export
{
    /// <summary>
    /// Service for exporting data to PDF using Syncfusion.Pdf.Net.Core.
    /// Professional PDF generation with headers, footers, page numbers, and styled grids.
    /// </summary>
    public class SyncfusionPdfExportService
    {
        private readonly ILogger<SyncfusionPdfExportService> _logger;

        public SyncfusionPdfExportService(ILogger<SyncfusionPdfExportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Adds professional header and footer with page numbers to the document
        /// </summary>
        private void AddHeaderFooter(PdfDocument document, string reportTitle)
        {
            var pageSize = document.Pages[0].GetClientSize();
            var bounds = new RectangleF(0, 0, pageSize.Width, 50);

            // Create header template
            var header = new PdfPageTemplateElement(bounds);
            var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
            header.Graphics.DrawString(reportTitle, headerFont,
                PdfBrushes.DarkBlue, new PointF(10, 10));

            // Add horizontal line
            var pen = new PdfPen(new PdfColor(173, 216, 230), 1);
            header.Graphics.DrawLine(pen, new PointF(0, 35), new PointF(pageSize.Width, 35));

            document.Template.Top = header;

            // Create footer template with page numbers
            var footer = new PdfPageTemplateElement(new RectangleF(0, 0, pageSize.Width, 50));
            var footerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9);
            var brush = new PdfSolidBrush(Color.Gray);

            // Add page number field
            var pageNumberField = new PdfPageNumberField(footerFont, brush);
            var pageCountField = new PdfPageCountField(footerFont, brush);
            var compositeField = new PdfCompositeField(footerFont, brush,
                "Page {0} of {1}", pageNumberField, pageCountField);
            compositeField.Draw(footer.Graphics, new PointF(pageSize.Width - 100, 10));

            // Add timestamp
            var timestamp = DateTime.Now.ToString("'Generated:' yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            footer.Graphics.DrawString(timestamp, footerFont, brush, new PointF(10, 10));

            document.Template.Bottom = footer;
        }

        /// <summary>
        /// Applies professional styling to PdfGrid with alternating row colors
        /// </summary>
        private void StyleGrid(PdfGrid grid)
        {
            // Set grid font
            grid.Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 9);
            grid.Style.CellPadding = new PdfPaddings(3, 3, 3, 3);

            // Style headers
            if (grid.Headers.Count > 0)
            {
                grid.Headers[0].Style.BackgroundBrush = new PdfSolidBrush(new PdfColor(41, 128, 185)); // Professional blue
                grid.Headers[0].Style.TextBrush = PdfBrushes.White;
                grid.Headers[0].Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
            }

            // Apply alternating row colors
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                if (i % 2 == 0)
                {
                    grid.Rows[i].Style.BackgroundBrush = new PdfSolidBrush(new PdfColor(236, 240, 241)); // Light gray
                }
                else
                {
                    grid.Rows[i].Style.BackgroundBrush = PdfBrushes.White;
                }
            }

            // Add cell borders - using CellSpacing and BorderOverlapStyle for grid borders
            // Note: BorderPen is not available in current Syncfusion version
            // Grid borders are handled through cell padding and background colors
        }

        /// <summary>
        /// Exports data to PDF format
        /// </summary>
        public virtual async Task ExportToPdfAsync(object data, string filePath)
        {
            await Task.Run(() =>
            {
                using var document = new PdfDocument();
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Draw header
                var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

                graphics.DrawString("Wiley Widget Report", headerFont,
                    PdfBrushes.Blue, new PointF(10, 10));

                var yPosition = 50f;

                // Handle different data types
                if (data is System.Collections.IEnumerable enumerableData && !(data is string))
                {
                    var items = enumerableData.Cast<object>().ToList();
                    if (items.Any())
                    {
                        var firstItem = items.First();
                        var properties = firstItem.GetType().GetProperties();

                        // Create DataTable for PdfGrid
                        var dataTable = new DataTable();
                        foreach (var prop in properties)
                        {
                            dataTable.Columns.Add(prop.Name);
                        }

                        foreach (var item in items)
                        {
                            var row = dataTable.NewRow();
                            foreach (var prop in properties)
                            {
                                var value = prop.GetValue(item);
                                row[prop.Name] = value?.ToString() ?? "";
                            }
                            dataTable.Rows.Add(row);
                        }

                        // Create and draw PdfGrid
                        var pdfGrid = new PdfGrid();
                        pdfGrid.DataSource = dataTable;
                        pdfGrid.Draw(page, new PointF(10, yPosition));
                    }
                }
                else
                {
                    // Single object
                    var properties = data.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(data);
                        graphics.DrawString($"{prop.Name}: {value?.ToString() ?? ""}",
                            bodyFont, PdfBrushes.Black, new PointF(10, yPosition));
                        yPosition += 20;
                    }
                }

                document.Save(filePath);
                _logger.LogInformation("PDF exported successfully to {FilePath}", filePath);
            });
        }

        /// <summary>
        /// Exports budget entries to PDF with professional styling
        /// </summary>
        public async Task<string> ExportBudgetEntriesToPdfAsync(IEnumerable<BudgetEntry> entries, string filePath)
        {
            await Task.Run(() =>
            {
                var entryList = entries.ToList();

                using var document = new PdfDocument();
                var page = document.Pages.Add();

                // Add professional header and footer with page numbers
                AddHeaderFooter(document, "Budget Entries Report");

                // Create DataTable for PdfGrid
                var dataTable = new DataTable();
                dataTable.Columns.Add("ID");
                dataTable.Columns.Add("Account Code");
                dataTable.Columns.Add("Description");
                dataTable.Columns.Add("Amount");
                dataTable.Columns.Add("Date");
                dataTable.Columns.Add("Category");

                decimal totalAmount = 0;
                foreach (var entry in entryList)
                {
                    dataTable.Rows.Add(
                        entry.Id.ToString(CultureInfo.InvariantCulture),
                        entry.AccountNumber ?? "",
                        entry.Description ?? "",
                        entry.BudgetedAmount.ToString("C2", CultureInfo.CurrentCulture),
                        entry.StartPeriod.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                        entry.FundType.ToString()
                    );
                    totalAmount += entry.BudgetedAmount;
                }

                // Create and configure PdfGrid
                var pdfGrid = new PdfGrid();
                pdfGrid.DataSource = dataTable;

                // Apply professional styling
                StyleGrid(pdfGrid);

                // Set column widths for better layout
                pdfGrid.Columns[0].Width = 40;  // ID
                pdfGrid.Columns[1].Width = 80;  // Account Code
                pdfGrid.Columns[2].Width = 150; // Description
                pdfGrid.Columns[3].Width = 70;  // Amount
                pdfGrid.Columns[4].Width = 80;  // Date
                pdfGrid.Columns[5].Width = 80;  // Category

                // Enable pagination
                pdfGrid.Style.AllowHorizontalOverflow = true;

                // Draw the grid with automatic pagination
                var result = pdfGrid.Draw(page, new PointF(10, 50));

                // Add summary at the bottom
                var summaryFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                var summaryY = result.Bounds.Bottom + 20;

                // Check if we need a new page for summary
                if (summaryY > page.GetClientSize().Height - 100)
                {
                    page = document.Pages.Add();
                    summaryY = 50;
                }

                page.Graphics.DrawString($"Total Entries: {entryList.Count}", summaryFont,
                    PdfBrushes.Black, new PointF(10, summaryY));
                page.Graphics.DrawString($"Total Amount: {totalAmount:C2}", summaryFont,
                    PdfBrushes.Black, new PointF(10, summaryY + 20));

                document.Save(filePath);
                _logger.LogInformation("Exported {Count} budget entries to PDF: {FilePath}", entryList.Count, filePath);
            });

            return filePath;
        }

        /// <summary>
        /// Exports municipal accounts to PDF with professional styling
        /// </summary>
        public async Task<string> ExportMunicipalAccountsToPdfAsync(IEnumerable<MunicipalAccount> accounts, string filePath)
        {
            await Task.Run(() =>
            {
                var accountList = accounts.ToList();

                using var document = new PdfDocument();
                var page = document.Pages.Add();

                // Add professional header and footer with page numbers
                AddHeaderFooter(document, "Municipal Accounts Report");

                // Create DataTable for PdfGrid
                var dataTable = new DataTable();
                dataTable.Columns.Add("Account #");
                dataTable.Columns.Add("Description");
                dataTable.Columns.Add("Type");
                dataTable.Columns.Add("Balance");
                dataTable.Columns.Add("Budget");
                dataTable.Columns.Add("Variance");

                decimal totalBalance = 0;
                decimal totalBudget = 0;

                foreach (var account in accountList)
                {
                    var variance = account.BudgetAmount - account.Balance;

                    dataTable.Rows.Add(
                        account.AccountNumber?.ToString() ?? account.AccountNumber_Value ?? "",
                        account.Name ?? "",
                        account.TypeDescription ?? account.Type.ToString(),
                        account.Balance.ToString("C2", CultureInfo.CurrentCulture),
                        account.BudgetAmount.ToString("C2", CultureInfo.CurrentCulture),
                        variance.ToString("C2", CultureInfo.CurrentCulture)
                    );

                    totalBalance += account.Balance;
                    totalBudget += account.BudgetAmount;
                }

                // Create and configure PdfGrid
                var pdfGrid = new PdfGrid();
                pdfGrid.DataSource = dataTable;

                // Apply professional styling
                StyleGrid(pdfGrid);

                // Set column widths for better layout
                pdfGrid.Columns[0].Width = 70;  // Account #
                pdfGrid.Columns[1].Width = 140; // Description
                pdfGrid.Columns[2].Width = 70;  // Type
                pdfGrid.Columns[3].Width = 70;  // Balance
                pdfGrid.Columns[4].Width = 70;  // Budget
                pdfGrid.Columns[5].Width = 70;  // Variance

                // Enable pagination
                pdfGrid.Style.AllowHorizontalOverflow = true;

                // Draw the grid with automatic pagination
                var result = pdfGrid.Draw(page, new PointF(10, 50));

                // Add summary at the bottom
                var summaryFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                var summaryY = result.Bounds.Bottom + 20;

                // Check if we need a new page for summary
                if (summaryY > page.GetClientSize().Height - 100)
                {
                    page = document.Pages.Add();
                    summaryY = 50;
                }

                var totalVariance = totalBudget - totalBalance;
                page.Graphics.DrawString($"Total Accounts: {accountList.Count}", summaryFont,
                    PdfBrushes.Black, new PointF(10, summaryY));
                page.Graphics.DrawString($"Total Balance: {totalBalance:C2}", summaryFont,
                    PdfBrushes.Black, new PointF(10, summaryY + 20));
                page.Graphics.DrawString($"Total Budget: {totalBudget:C2}", summaryFont,
                    PdfBrushes.Black, new PointF(10, summaryY + 40));
                page.Graphics.DrawString($"Total Variance: {totalVariance:C2}", summaryFont,
                    totalVariance >= 0 ? PdfBrushes.Green : PdfBrushes.Red,
                    new PointF(10, summaryY + 60));

                document.Save(filePath);
                _logger.LogInformation("Exported {Count} municipal accounts to PDF: {FilePath}", accountList.Count, filePath);
            });

            return filePath;
        }
    }
}
