using System;
using System.IO;
using System.Threading.Tasks;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.DataGridConverter;
using Syncfusion.XlsIO;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Export service for Syncfusion grids using official APIs.
    /// Implements Excel and PDF export functionality per Syncfusion documentation.
    /// </summary>
    public static class ExportService
    {
        /// <summary>
        /// Exports SfDataGrid to Excel file using Syncfusion.XlsIO.
        /// </summary>
        /// <param name="grid">The SfDataGrid to export.</param>
        /// <param name="filePath">Output file path for the Excel file.</param>
        /// <returns>Task representing the async export operation.</returns>
        public static Task ExportGridToExcelAsync(SfDataGrid grid, string filePath)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            return Task.Run(() =>
            {
                using var excelEngine = new ExcelEngine();
                var workbook = excelEngine.Excel.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];

                var options = new ExcelExportingOptions
                {
                    ExcelVersion = ExcelVersion.Xlsx
                };

                // Export using SfDataGrid's built-in export functionality
                grid.ExportToExcel(grid.View, options, worksheet);

                workbook.Version = ExcelVersion.Xlsx;
                workbook.SaveAs(filePath);
            });
        }

        /// <summary>
        /// Exports SfDataGrid to PDF file using Syncfusion.Pdf.Grid.
        /// </summary>
        /// <param name="grid">The SfDataGrid to export.</param>
        /// <param name="filePath">Output file path for the PDF file.</param>
        /// <returns>Task representing the async export operation.</returns>
        public static Task ExportGridToPdfAsync(SfDataGrid grid, string filePath)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            return Task.Run(() =>
            {
                using var document = new PdfDocument();
                document.PageSettings.Orientation = PdfPageOrientation.Landscape;
                document.PageSettings.Margins.All = 20;

                var options = new PdfExportingOptions
                {
                    AutoColumnWidth = true,
                    AutoRowHeight = true
                };

                // Export using SfDataGrid's built-in PDF export functionality
                var pdfGrid = grid.ExportToPdfGrid(grid.View, options);
                var page = document.Pages.Add();

                pdfGrid.Draw(page, new System.Drawing.PointF(0, 0));

                document.Save(filePath);
            });
        }

        /// <summary>
        /// Exports a chart control to PDF file.
        /// Note: Requires specific chart type casting based on actual chart control used.
        /// </summary>
        /// <param name="chart">The chart object to export (must be Syncfusion chart control).</param>
        /// <param name="filePath">Output file path for the PDF file.</param>
        /// <returns>Task representing the async export operation.</returns>
        public static Task ExportChartToPdfAsync(object chart, string filePath)
        {
            if (chart == null)
                throw new ArgumentNullException(nameof(chart));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            return Task.Run(() =>
            {
                using var document = new PdfDocument();
                document.PageSettings.Margins.All = 20;
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                try
                {
                    if (chart is System.Windows.Forms.Control chartControl)
                    {
                        using var chartImage = new System.Drawing.Bitmap(chartControl.Width, chartControl.Height);
                        chartControl.DrawToBitmap(chartImage, new System.Drawing.Rectangle(0, 0, chartControl.Width, chartControl.Height));

                        var pdfImage = PdfImage.FromImage(chartImage);

                        var availableWidth = page.GetClientSize().Width;
                        var availableHeight = page.GetClientSize().Height;
                        var aspectRatio = (float)chartImage.Width / chartImage.Height;

                        float drawWidth = availableWidth;
                        float drawHeight = drawWidth / aspectRatio;

                        if (drawHeight > availableHeight)
                        {
                            drawHeight = availableHeight;
                            drawWidth = drawHeight * aspectRatio;
                        }

                        graphics.DrawImage(pdfImage, 0, 0, drawWidth, drawHeight);
                    }
                    else
                    {
                        throw new ArgumentException("Chart must be a Windows Forms control", nameof(chart));
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to export chart to PDF: {ex.Message}", ex);
                }

                document.Save(filePath);
            });
        }
    }
}
