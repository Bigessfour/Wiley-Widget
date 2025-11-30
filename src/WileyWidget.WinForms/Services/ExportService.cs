using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.XlsIO;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Provides simple export helpers for grids and charts.
    /// </summary>
    public static class ExportService
    {
        public static async Task ExportGridToExcelAsync(SfDataGrid grid, string filePath)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            await Task.Run(() =>
            {
                // Try to obtain enumerable from DataSource
                IEnumerable? rows = grid.DataSource as IEnumerable;

                // Fallback: if DataSource is a BindingSource
                if (rows == null && grid.DataSource is BindingSource bs)
                {
                    rows = bs.List as IEnumerable;
                }

                var dt = new DataTable(grid.Name ?? "Grid");

                // Build schema from visible columns
                var visibleColumns = grid.Columns.Where(c => c.Visible).ToList();
                foreach (var col in visibleColumns)
                {
                    dt.Columns.Add(col.HeaderText ?? col.MappingName ?? col.Name, typeof(string));
                }

                if (rows != null)
                {
                    foreach (var r in rows)
                    {
                        var values = new object[visibleColumns.Count];
                        for (int i = 0; i < visibleColumns.Count; i++)
                        {
                            var mapping = visibleColumns[i].MappingName;
                            if (string.IsNullOrEmpty(mapping))
                            {
                                values[i] = "";
                                continue;
                            }

                            try
                            {
                                var pi = r.GetType().GetProperty(mapping, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                if (pi != null)
                                {
                                    var v = pi.GetValue(r);
                                    values[i] = v?.ToString() ?? string.Empty;
                                }
                                else
                                {
                                    values[i] = string.Empty;
                                }
                            }
                            catch
                            {
                                values[i] = string.Empty;
                            }
                        }

                        dt.Rows.Add(values);
                    }
                }

                using var engine = new ExcelEngine();
                var app = engine.Excel;
                var wb = app.Workbooks.Create(1);
                var ws = wb.Worksheets[0];

                // Import datatable starting at row 1
                ws.ImportDataTable(dt, true, 1, 1);

                // Auto-fit columns
                ws.UsedRange.AutofitColumns();

                // Save
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                wb.SaveAs(fs);
            }).ConfigureAwait(true);
        }

        public static async Task ExportGridToPdfAsync(SfDataGrid grid, string filePath)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            // Capture a bitmap of the grid (visible area) and embed into PDF
            Bitmap? bmp = null;
            try
            {
                if (grid.IsHandleCreated)
                {
                    bmp = new Bitmap(grid.Width, grid.Height);
                    grid.DrawToBitmap(bmp, new Rectangle(0, 0, grid.Width, grid.Height));
                }
            }
            catch { bmp = null; }

            if (bmp == null)
            {
                throw new InvalidOperationException("Unable to capture grid image for PDF export.");
            }

            await Task.Run(() =>
            {
                using var doc = new PdfDocument();
                var page = doc.Pages.Add();
                using var pdfImage = new PdfBitmap(bmp);
                var client = page.GetClientSize();
                var scale = Math.Min(client.Width / (float)bmp.Width, client.Height / (float)bmp.Height);
                var drawW = bmp.Width * scale;
                var drawH = bmp.Height * scale;
                var rect = new System.Drawing.RectangleF(0, 0, drawW, drawH);
                page.Graphics.DrawImage(pdfImage, rect);

                using var fs = File.OpenWrite(filePath);
                doc.Save(fs);
                doc.Close(true);
            }).ConfigureAwait(true);
        }

        public static async Task ExportChartToPdfAsync(Syncfusion.Windows.Forms.Chart.ChartControl chart, string filePath)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            Bitmap? bmp = null;
            try
            {
                bmp = new Bitmap(chart.Width, chart.Height);
                chart.DrawToBitmap(bmp, new Rectangle(0, 0, chart.Width, chart.Height));
            }
            catch { bmp = null; }

            if (bmp == null) throw new InvalidOperationException("Unable to capture chart image for PDF export.");

            await Task.Run(() =>
            {
                using var doc = new PdfDocument();
                var page = doc.Pages.Add();
                using var pdfImage = new PdfBitmap(bmp);
                var client = page.GetClientSize();
                var scale = Math.Min(client.Width / (float)bmp.Width, client.Height / (float)bmp.Height);
                var drawW = bmp.Width * scale;
                var drawH = bmp.Height * scale;
                var rect = new System.Drawing.RectangleF(0, 0, drawW, drawH);
                page.Graphics.DrawImage(pdfImage, rect);

                using var fs = File.OpenWrite(filePath);
                doc.Save(fs);
                doc.Close(true);
            }).ConfigureAwait(true);
        }
    }
}
