using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Export;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for printing and previewing reports using PDF generation.
    /// Reuses existing PDF export services and provides print preview capabilities.
    /// </summary>
    public class PrintingService : IPrintingService
    {
        private readonly ILogger<PrintingService> _logger;
        private readonly SyncfusionPdfExportService _pdfExportService;

        public PrintingService(ILogger<PrintingService> logger, SyncfusionPdfExportService pdfExportService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pdfExportService = pdfExportService ?? throw new ArgumentNullException(nameof(pdfExportService));
        }

        /// <inheritdoc />
        public async Task<string> GeneratePdfAsync(object model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            _logger.LogInformation("Starting PDF generation for print preview");

            try
            {
                // Create temp file path
                var tempPath = Path.Combine(Path.GetTempPath(), $"WileyWidget_Print_{Guid.NewGuid()}.pdf");

                // Handle different model types
                if (model.GetType().Name == "ChartViewModel")
                {
                    await GenerateChartPdfAsync((dynamic)model, tempPath);
                }
                else
                {
                    // Use existing PDF export service for other types
                    await _pdfExportService.ExportToPdfAsync(model, tempPath);
                }

                _logger.LogInformation("PDF generated successfully at {Path}", tempPath);
                return tempPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF for printing");
                throw;
            }
        }

        private async Task GenerateChartPdfAsync(dynamic vm, string filePath)
        {
            await Task.Run(() =>
            {
                using var document = new Syncfusion.Pdf.PdfDocument();
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Draw header
                var headerFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 20, Syncfusion.Pdf.Graphics.PdfFontStyle.Bold);
                graphics.DrawString($"Budget Analytics Report - {vm.SelectedYear}", headerFont,
                    Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 10));

                var bodyFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 10);
                var yPosition = 50f;

                // Draw metadata
                graphics.DrawString($"Year: {vm.SelectedYear}, Category: {vm.SelectedCategory}",
                    bodyFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                yPosition += 30;

                // Monthly Trends section
                var sectionFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 12, Syncfusion.Pdf.Graphics.PdfFontStyle.Bold);
                graphics.DrawString("Monthly Trends:", sectionFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                yPosition += 20;

                // Create PdfGrid for monthly data
                var monthlyGrid = new Syncfusion.Pdf.Grid.PdfGrid();
                var monthlyTable = new System.Data.DataTable();
                monthlyTable.Columns.Add("Month");
                monthlyTable.Columns.Add("Amount");

                foreach (var data in vm.LineChartData)
                {
                    monthlyTable.Rows.Add(data.Category, data.Value.ToString("C", System.Globalization.CultureInfo.CurrentCulture));
                }

                monthlyGrid.DataSource = monthlyTable;
                monthlyGrid.Draw(page, new Syncfusion.Drawing.PointF(10, yPosition));
                yPosition += monthlyGrid.Headers.Count * 20 + vm.LineChartData.Count * 15 + 30;

                // Category Breakdown section
                if (yPosition > page.GetClientSize().Height - 150)
                {
                    page = document.Pages.Add();
                    graphics = page.Graphics;
                    yPosition = 10;
                }

                graphics.DrawString("Category Breakdown:", sectionFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                yPosition += 20;

                // Create PdfGrid for category data
                var categoryGrid = new Syncfusion.Pdf.Grid.PdfGrid();
                var categoryTable = new System.Data.DataTable();
                categoryTable.Columns.Add("Category");
                categoryTable.Columns.Add("Amount");

                foreach (var data in vm.PieChartData)
                {
                    categoryTable.Rows.Add(data.Category, data.Value.ToString("C", System.Globalization.CultureInfo.CurrentCulture));
                }

                categoryGrid.DataSource = categoryTable;
                categoryGrid.Draw(page, new Syncfusion.Drawing.PointF(10, yPosition));

                // Save the document
                document.Save(filePath);
            });
        }

        /// <inheritdoc />
        public Task PreviewAsync(string pdfPath)
        {
            _logger.LogInformation("Opening print preview for PDF: {Path}", pdfPath);

            try
            {
                // Launch default PDF viewer
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        UseShellExecute = true,
                        Verb = "open"
                    }
                };

                process.Start();
                _logger.LogInformation("Print preview opened successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open print preview");
                throw;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task PrintAsync(string pdfPath)
        {
            _logger.LogInformation("Starting print operation for PDF: {Path}", pdfPath);

            try
            {
                // Use system print dialog for PDF
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        UseShellExecute = true,
                        Verb = "print"
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                _logger.LogInformation("Print operation completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to print PDF");
                throw;
            }
        }
    }
}
