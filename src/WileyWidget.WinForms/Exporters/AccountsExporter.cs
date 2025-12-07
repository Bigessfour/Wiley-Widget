extern alias sync31;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Abstractions.Models;
using Microsoft.Extensions.Logging;
using Xls = sync31::Syncfusion.XlsIO;
using Syncfusion.XlsIO;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Grid;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Exporters
{
    public sealed class AccountsExporter
    {
        private readonly ILogger? _logger;

        // Define export headers as a constant for consistency
        private static readonly string[] ExportHeaders = {
            "AccountNumber", "Name", "Description", "Type", "Fund",
            "Balance", "BudgetAmount", "Department", "IsActive", "HasParent"
        };

        public AccountsExporter(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the export values for a single account in the correct order.
        /// </summary>
        private static object[] GetAccountValues(MunicipalAccountDisplay account)
        {
            return new object[]
            {
                account.AccountNumber ?? string.Empty,
                account.Name ?? string.Empty,
                account.Description ?? string.Empty,
                account.Type ?? string.Empty,
                account.Fund ?? string.Empty,
                account.Balance,
                account.BudgetAmount,
                account.Department ?? string.Empty,
                account.IsActive ? "true" : "false",
                account.HasParent ? "true" : "false"
            };
        }

        /// <summary>
        /// Reports progress for export operations, avoiding duplicate reports.
        /// </summary>
        private static void ReportProgress(IProgress<int>? progress, int processed, int total, ref int lastReported)
        {
            if (processed % 5 == 0 || processed == total)
            {
                var percent = total == 0 ? 80 : (int)((processed * 90L) / Math.Max(1, total));
                if (percent != lastReported)
                {
                    progress?.Report(percent);
                    lastReported = percent;
                }
            }
        }

        public void ExportToXlsx(string path, IEnumerable<MunicipalAccountDisplay> accounts)
        {
            // Use Syncfusion XlsIO (aliased to the v31 runtime) for Excel export
            using var excelEngine = new Xls.ExcelEngine();
            var application = excelEngine.Excel;
            application.DefaultVersion = Xls.ExcelVersion.Excel2016;

            var workbook = application.Workbooks.Create(1);
            var worksheet = workbook.Worksheets[0];
            worksheet.Name = "Accounts";

            // Add headers
            for (int c = 0; c < ExportHeaders.Length; c++)
            {
                worksheet.Range[1, c + 1].Text = ExportHeaders[c];
            }

            // Add data rows
            var r = 2;
            foreach (var account in accounts)
            {
                var values = GetAccountValues(account);
                for (int c = 0; c < values.Length; c++)
                {
                    if (c == 5 || c == 6) // Balance and BudgetAmount are numeric
                    {
                        worksheet.Range[r, c + 1].Number = Convert.ToDouble(values[c]);
                    }
                    else
                    {
                        worksheet.Range[r, c + 1].Text = values[c].ToString();
                    }
                }
                r++;
            }

            // Auto-fit columns for readability
            worksheet.UsedRange.AutofitColumns();

            // Save workbook to file path
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
            workbook.SaveAs(fileStream);
            workbook.Close();

            _logger?.LogInformation("Exported {Count} accounts to XLSX: {Path}", r - 2, path);
        }

        /// <summary>
        /// Asynchronously export accounts to an XLSX stream. Reports progress (0-100) and supports cancellation.
        /// </summary>
        public async Task ExportToXlsxAsync(Stream outputStream, IEnumerable<MunicipalAccountDisplay> accounts, CancellationToken cancellationToken, IProgress<int>? progress = null)
        {
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            if (accounts == null) throw new ArgumentNullException(nameof(accounts));

            var list = accounts is IList<MunicipalAccountDisplay> l ? l : accounts.ToList();
            int total = list.Count;

            // Run the Syncfusion XlsIO work on a thread pool thread to avoid blocking the UI thread.
            await Task.Run(() =>
            {
                using var excelEngine = new Xls.ExcelEngine();
                var application = excelEngine.Excel;
                application.DefaultVersion = Xls.ExcelVersion.Excel2016;

                var workbook = application.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];
                worksheet.Name = "Accounts";

                // Add headers
                for (int c = 0; c < ExportHeaders.Length; c++)
                {
                    worksheet.Range[1, c + 1].Text = ExportHeaders[c];
                }

                // Add data rows with progress tracking
                int r = 2;
                int processed = 0;
                int lastReported = 0;

                for (int i = 0; i < list.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var account = list[i];
                    var values = GetAccountValues(account);

                    for (int c = 0; c < values.Length; c++)
                    {
                        if (c == 5 || c == 6) // Balance and BudgetAmount are numeric
                        {
                            worksheet.Range[r, c + 1].Number = Convert.ToDouble(values[c]);
                        }
                        else
                        {
                            worksheet.Range[r, c + 1].Text = values[c].ToString();
                        }
                    }

                    r++;
                    processed++;

                    // Report progress every 5 rows or on last item
                    ReportProgress(progress, processed, total, ref lastReported);
                }

                // Autofit columns and save
                worksheet.UsedRange.AutofitColumns();
                progress?.Report(92);

                cancellationToken.ThrowIfCancellationRequested();
                workbook.SaveAs(outputStream);
                workbook.Close();

                progress?.Report(100);
                _logger?.LogInformation("Exported {Count} accounts to XLSX stream", processed);
            }, cancellationToken).ConfigureAwait(false);
        }

        public void ExportToCsv(string path, IEnumerable<MunicipalAccountDisplay> accounts)
        {
            using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);

            // Write headers
            sw.WriteLine(string.Join(",", ExportHeaders));

            // Write data rows
            var count = 0;
            foreach (var account in accounts)
            {
                var values = GetAccountValues(account);
                var escapedValues = values.Select(v => EscapeCsvValue(v.ToString()));
                sw.WriteLine(string.Join(",", escapedValues));
                count++;
            }

            _logger?.LogInformation("Exported {Count} accounts to CSV: {Path}", count, path);
        }

        /// <summary>
        /// Escapes a value for CSV format by wrapping in quotes and escaping internal quotes.
        /// </summary>
        private static string EscapeCsvValue(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // If value contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        /// <summary>
        /// Asynchronously export accounts to a PDF stream. Reports progress (0-100) and supports cancellation.
        /// </summary>
        public async Task ExportToPdfAsync(Stream outputStream, IEnumerable<MunicipalAccountDisplay> accounts, CancellationToken cancellationToken, IProgress<int>? progress = null)
        {
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
            if (accounts == null) throw new ArgumentNullException(nameof(accounts));

            var list = accounts is IList<MunicipalAccountDisplay> l ? l : accounts.ToList();
            int total = list.Count;

            // Run the Syncfusion PDF work on a thread pool thread to avoid blocking the UI thread.
            await Task.Run(() =>
            {
                using var pdfDocument = new PdfDocument();
                var page = pdfDocument.Pages.Add();
                var pdfGrid = new PdfGrid();

                // Setup grid columns
                pdfGrid.Columns.Add(ExportHeaders.Length);

                // Add header row
                var headerRow = pdfGrid.Rows.Add();
                for (int c = 0; c < ExportHeaders.Length; c++)
                {
                    headerRow.Cells[c].Value = ExportHeaders[c];
                }

                // Add data rows with progress tracking
                int processed = 0;
                int lastReported = 0;

                for (int i = 0; i < list.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var account = list[i];
                    var values = GetAccountValues(account);

                    var row = pdfGrid.Rows.Add();
                    for (int c = 0; c < values.Length; c++)
                    {
                        // Format currency values for PDF
                        if (c == 5 || c == 6) // Balance and BudgetAmount
                        {
                            row.Cells[c].Value = ((decimal)values[c]).ToString("C2");
                        }
                        else
                        {
                            row.Cells[c].Value = values[c].ToString();
                        }
                    }

                    processed++;
                    ReportProgress(progress, processed, total, ref lastReported);
                }

                // Draw the grid on the page
                pdfGrid.Draw(page.Graphics, new System.Drawing.RectangleF(10, 10, page.GetClientSize().Width - 20, page.GetClientSize().Height - 20));

                // Report near-complete before save
                progress?.Report(92);

                // Save PDF to provided stream
                cancellationToken.ThrowIfCancellationRequested();
                pdfDocument.Save(outputStream);

                // Finalize progress
                progress?.Report(100);
                _logger?.LogInformation("Exported {Count} accounts to PDF stream", processed);
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
