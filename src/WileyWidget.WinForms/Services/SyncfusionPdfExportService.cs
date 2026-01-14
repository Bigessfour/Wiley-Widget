using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// PDF exporter using Syncfusion PdfGrid with styled headers, column widths,
    /// numeric alignment and conditional coloring for percentage overage.
    /// </summary>
    public static class SyncfusionPdfExportService
    {
        public static void ExportBudgetComparisonPdf(DataSet dataSet, string outputPath, string title, int fiscalYear, string? entity = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            using var document = new PdfDocument();

            // create an initial page to calculate client size and templates
            var firstPage = document.Pages.Add();
            var pageSize = firstPage.GetClientSize();
            const float margin = 40f;

            // Fonts
            var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14f, PdfFontStyle.Bold);
            var subHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10f, PdfFontStyle.Bold);
            var subHeaderSmall = new PdfStandardFont(PdfFontFamily.Helvetica, 9f, PdfFontStyle.Regular);
            var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10f, PdfFontStyle.Bold);
            var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9f, PdfFontStyle.Regular);

            // Prepare small dynamic pieces for header
            var actualPeriod = toDate.HasValue ? toDate.Value.ToString("MM/yyyy") : $"11/{fiscalYear}";

            // Top template/header that repeats on every page
            float headerHeight = 72f;
            var headerTemplate = new PdfPageTemplateElement(pageSize.Width, headerHeight);
            var headerGraphics = headerTemplate.Graphics;
            headerGraphics.DrawString(title, titleFont, PdfBrushes.Black, new RectangleF(0, 6, pageSize.Width, 20), new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle });
            var subtitle = string.IsNullOrWhiteSpace(entity) ? $"Fiscal Year {fiscalYear}" : $"Fiscal Year {fiscalYear} — {entity}";
            headerGraphics.DrawString(subtitle, bodyFont, PdfBrushes.Black, new RectangleF(0, 28, pageSize.Width, 16), new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle });

            // Sub-header lines (closely match the target image layout)
            headerGraphics.DrawString($"PROPOSED BUDGET          {actualPeriod}          BUDGET          % OF", subHeaderFont, PdfBrushes.Black, new RectangleF(0, 46, pageSize.Width, 16), new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle });
            headerGraphics.DrawString($"ACTUAL          REMAINING          BUDGET", subHeaderSmall, PdfBrushes.Black, new RectangleF(0, 60, pageSize.Width, 12), new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle });

            document.Template.Top = headerTemplate;

            // Footer template (generated date). Page numbers are still rendered per-page below.
            var footerTemplate = new PdfPageTemplateElement(pageSize.Width, 28f);
            var footerGraphics = footerTemplate.Graphics;
            footerGraphics.DrawString($"Generated {DateTime.Now:yyyy-MM-dd}", bodyFont, PdfBrushes.Gray, new RectangleF(margin, 6, pageSize.Width - margin * 2, 12), new PdfStringFormat { Alignment = PdfTextAlignment.Left, LineAlignment = PdfVerticalAlignment.Top });
            document.Template.Bottom = footerTemplate;

            // Start drawing after the header template
            float y = headerHeight + 8f;

            // Draw sections
            if (dataSet.Tables.Contains("Revenues"))
            {
                var revenuesTable = dataSet.Tables["Revenues"];
                if (revenuesTable != null)
                {
                    y = DrawSection(document, revenuesTable, "REVENUES OTHER THAN PROPERTY TAXES", margin, y, sectionFont, bodyFont);
                }
            }
            if (dataSet.Tables.Contains("Expenses"))
            {
                var expensesTable = dataSet.Tables["Expenses"];
                if (expensesTable != null)
                {
                    y = DrawSection(document, expensesTable, "EXPENSES", margin, y + 8f, sectionFont, bodyFont);
                }
            }

            // Summary/net totals (basic)
            y += 12f;
            decimal totalRevProp = SumColumn(dataSet, "Revenues", "ProposedBudget");
            decimal totalRevAct = SumColumn(dataSet, "Revenues", "Actual_11_2025");
            decimal totalExpProp = SumColumn(dataSet, "Expenses", "ProposedBudget");
            decimal totalExpAct = SumColumn(dataSet, "Expenses", "Actual_11_2025");

            decimal netProp = totalRevProp - totalExpProp;
            decimal netAct = totalRevAct - totalExpAct;

            var lastPage = document.Pages[document.Pages.Count - 1];
            var lastGraphics = lastPage.Graphics;
            var lastSize = lastPage.GetClientSize();

            lastGraphics.DrawString("NET (PROPOSED - EXPENSES):", sectionFont, PdfBrushes.Black, new PointF(margin, y));
            lastGraphics.DrawString(netProp.ToString("C2"), sectionFont, PdfBrushes.Black, new PointF(lastSize.Width - margin - 120, y));
            y += 14f;
            lastGraphics.DrawString("NET (ACTUAL - EXPENSES):", sectionFont, PdfBrushes.Black, new PointF(margin, y));
            lastGraphics.DrawString(netAct.ToString("C2"), sectionFont, PdfBrushes.Black, new PointF(lastSize.Width - margin - 120, y));

            // Add page numbers (Page X of Y)
            for (int i = 0; i < document.Pages.Count; i++)
            {
                var p = document.Pages[i];
                var g = p.Graphics;
                var rect = new RectangleF(0, p.GetClientSize().Height - 18, p.GetClientSize().Width, 16);
                g.DrawString($"Page {i + 1} of {document.Pages.Count}", bodyFont, PdfBrushes.Gray, rect, new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle });
            }

            // Ensure directory exists and save
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            document.Save(outputPath);
        }

        private static float DrawSection(PdfDocument document, DataTable table, string sectionTitle, float margin, float startY, PdfFont sectionFont, PdfFont bodyFont)
        {
            // Use the last page as the drawing surface; PdfGrid.Draw may add pages automatically
            var page = document.Pages[document.Pages.Count - 1];
            var graphics = page.Graphics;
            var pageSize = page.GetClientSize();
            float y = startY;

            // If there's insufficient space for a section header + a few rows, add a new page
            const float minSectionHeight = 120f;
            if (y > pageSize.Height - margin - minSectionHeight)
            {
                page = document.Pages.Add();
                graphics = page.Graphics;
                pageSize = page.GetClientSize();
                y = margin; // start at top margin on new page; header template (if any) is applied automatically
            }

            graphics.DrawString(sectionTitle, sectionFont, PdfBrushes.Black, new PointF(margin, y));
            y += 12f;

            // Handle empty or null table
            if (table == null || table.Rows.Count == 0)
            {
                graphics.DrawString("No data available", bodyFont, PdfBrushes.Gray, new PointF(margin, y));
                y += 14f;
                return y;
            }

            // Build a render table (string-formatted columns so PdfGrid shows C2/P2 values exactly)
            var render = new DataTable();
            render.Columns.Add("Account", typeof(string));
            render.Columns.Add("Description", typeof(string));
            render.Columns.Add("ProposedBudget", typeof(string));
            render.Columns.Add("Actual", typeof(string));
            render.Columns.Add("Remaining", typeof(string));
            render.Columns.Add("Percent", typeof(string));

            var percentList = new List<decimal>();

            foreach (DataRow r in table.Rows)
            {
                var acct = r.Table.Columns.Contains("Account") ? (r["Account"]?.ToString() ?? string.Empty) : string.Empty;
                var desc = r.Table.Columns.Contains("Description") ? (r["Description"]?.ToString() ?? string.Empty) : string.Empty;
                decimal proposed = ToDecimalSafe(r, "ProposedBudget");
                decimal actual = ToDecimalSafe(r, "Actual_11_2025");
                var remaining = proposed - actual;
                var pct = proposed == 0m ? 0m : actual / proposed;
                percentList.Add(pct);

                render.Rows.Add(acct, desc, proposed.ToString("C2"), actual.ToString("C2"), remaining.ToString("C2"), pct.ToString("P2"));
            }

            var grid = new PdfGrid();
            grid.Style.CellPadding = new PdfPaddings(5, 5, 5, 5);
            grid.DataSource = render;

            // Grid visual polish: repeat header for multi-page tables
            grid.RepeatHeader = true;

            // Add header row and style it
            grid.Headers.Add(1);
            var header = grid.Headers[0];
            header.Cells[0].Value = "Account";
            header.Cells[1].Value = "Description";
            header.Cells[2].Value = "PROPOSED BUDGET";
            header.Cells[3].Value = "ACTUAL 11/2025";
            header.Cells[4].Value = "REMAINING";
            header.Cells[5].Value = "% OF BUDGET";

            for (int i = 0; i < header.Cells.Count; i++)
            {
                header.Cells[i].Style.Font = sectionFont;
                header.Cells[i].Style.StringFormat = new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle, WordWrap = PdfWordWrapType.Word };
                header.Cells[i].Style.BackgroundBrush = new PdfSolidBrush(new PdfColor(240, 240, 240));
            }

            // Column widths (relative)
            float tableWidth = pageSize.Width - margin * 2;
            if (grid.Columns.Count >= 6)
            {
                grid.Columns[0].Width = tableWidth * 0.16f; // Account
                grid.Columns[1].Width = tableWidth * 0.44f; // Description
                grid.Columns[2].Width = tableWidth * 0.12f; // Proposed
                grid.Columns[3].Width = tableWidth * 0.12f; // Actual
                grid.Columns[4].Width = tableWidth * 0.10f; // Remaining
                grid.Columns[5].Width = tableWidth * 0.06f; // %
            }

            // Apply row-level styling (alignment, conditional coloring, alternating row shading)
            for (int rowIndex = 0; rowIndex < grid.Rows.Count; rowIndex++)
            {
                var row = grid.Rows[rowIndex];

                // Wrap description text
                row.Cells[1].Style.StringFormat = new PdfStringFormat { WordWrap = PdfWordWrapType.Word, Alignment = PdfTextAlignment.Left, LineAlignment = PdfVerticalAlignment.Middle };

                // Right-align numeric columns
                row.Cells[2].Style.StringFormat = new PdfStringFormat { Alignment = PdfTextAlignment.Right, LineAlignment = PdfVerticalAlignment.Middle };
                row.Cells[3].Style.StringFormat = new PdfStringFormat { Alignment = PdfTextAlignment.Right, LineAlignment = PdfVerticalAlignment.Middle };
                row.Cells[4].Style.StringFormat = new PdfStringFormat { Alignment = PdfTextAlignment.Right, LineAlignment = PdfVerticalAlignment.Middle };
                row.Cells[5].Style.StringFormat = new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle };

                // Alternating row background for readability
                if (rowIndex % 2 == 1)
                {
                    row.Style.BackgroundBrush = new PdfSolidBrush(new PdfColor(250, 250, 250));
                }

                // Conditional coloring: percent > 100% -> red
                if (rowIndex < percentList.Count && percentList[rowIndex] > 1m)
                {
                    var redBrush = new PdfSolidBrush(new PdfColor(192, 0, 0));
                    row.Cells[3].Style.TextBrush = redBrush; // Actual
                    row.Cells[5].Style.TextBrush = redBrush; // Percent
                }
            }

            // Draw grid; PdfGrid will paginate automatically. If there is not enough vertical space, start on a new page.
            float availableHeight = pageSize.Height - y - margin;
            if (availableHeight < 80f)
            {
                page = document.Pages.Add();
                pageSize = page.GetClientSize();
                y = margin;
            }

            var bounds = new System.Drawing.RectangleF(margin, y, pageSize.Width - margin * 2, pageSize.Height - y - margin);
            PdfGridLayoutResult result = grid.Draw(page, bounds);

            // Update to last page returned by draw
            page = result.Page;
            graphics = page.Graphics;
            y = result.Bounds.Bottom + 6f;

            // Draw a light outer border around the rendered grid for visual separation
            try
            {
                graphics.DrawRectangle(new PdfPen(PdfBrushes.Black, 0.5f), result.Bounds);
            }
            catch
            {
                // If low-level drawing isn't supported for any reason, ignore and continue
            }

            // Section totals
            decimal sumProp = SumColumn(table, "ProposedBudget");
            decimal sumAct = SumColumn(table, "Actual_11_2025");
            decimal sumRem = sumProp - sumAct;

            graphics.DrawString("Section Total:", sectionFont, PdfBrushes.Black, new PointF(margin, y));
            graphics.DrawString(sumProp.ToString("C2"), sectionFont, PdfBrushes.Black, new PointF(pageSize.Width - margin - 220, y));
            graphics.DrawString(sumAct.ToString("C2"), sectionFont, PdfBrushes.Black, new PointF(pageSize.Width - margin - 120, y));
            y += 14f;

            return y;
        }

        private static decimal SumColumn(DataSet ds, string tableName, string columnName)
        {
            if (ds == null || !ds.Tables.Contains(tableName))
            {
                return 0m;
            }
            DataTable? table = ds.Tables[tableName];
            if (table == null) return 0m;
            return SumColumn(table, columnName);
        }

        private static decimal SumColumn(DataTable table, string columnName)
        {
            if (table == null || !table.Columns.Contains(columnName)) return 0m;
            decimal total = 0m;
            foreach (DataRow r in table.Rows)
            {
                total += ToDecimalSafe(r, columnName);
            }
            return total;
        }

        private static decimal ToDecimalSafe(DataRow row, string columnName)
        {
            if (row == null) return 0m;
            if (!row.Table.Columns.Contains(columnName)) return 0m;
            var o = row[columnName];
            if (o == null || o == DBNull.Value) return 0m;
            if (o is decimal d) return d;
            if (decimal.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return 0m;
        }
    }
}
