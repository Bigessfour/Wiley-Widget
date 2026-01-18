using System;
using System.Drawing.Printing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Chart;

namespace WileyWidget.WinForms.Extensions
{
    internal static class ChartControlPrinting
    {
        public static PrintDocument? TryGetPrintDocument(ChartControl chart, ILogger? logger = null)
        {
            if (chart == null)
            {
                logger?.LogWarning("TryGetPrintDocument: Chart is null");
                return null;
            }

            if (chart.IsDisposed)
            {
                logger?.LogWarning("TryGetPrintDocument: Chart is disposed");
                return null;
            }

            try
            {
                // ChartControl exposes a PrintDocument that renders the chart for printing.
                var doc = chart.PrintDocument;
                if (doc == null)
                {
                    logger?.LogDebug("TryGetPrintDocument: PrintDocument is null");
                }
                return doc;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "TryGetPrintDocument: Failed to retrieve PrintDocument");
                return null;
            }
        }

        public static bool TryPrint(ChartControl chart, ILogger? logger = null)
        {
            var doc = TryGetPrintDocument(chart, logger);
            if (doc == null)
            {
                logger?.LogDebug("TryPrint: PrintDocument not available");
                return false;
            }

            try
            {
                logger?.LogInformation("Starting chart print");
                doc.Print();
                logger?.LogInformation("Chart print completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "TryPrint: Failed to print chart");
                return false;
            }
        }

        public static bool TryShowPrintPreview(IWin32Window owner, ChartControl chart, string? title = null, ILogger? logger = null)
        {
            var doc = TryGetPrintDocument(chart, logger);
            if (doc == null)
            {
                logger?.LogDebug("TryShowPrintPreview: PrintDocument not available");
                return false;
            }

            try
            {
                logger?.LogDebug("Opening print preview dialog");
                using var dialog = new PrintPreviewDialog
                {
                    Document = doc,
                };

                if (!string.IsNullOrWhiteSpace(title))
                {
                    dialog.Text = title;
                }

                dialog.ShowDialog(owner);
                logger?.LogDebug("Print preview dialog closed");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "TryShowPrintPreview: Failed to show print preview");
                return false;
            }
        }

        public static bool TryShowPrintDialogAndPrint(IWin32Window owner, ChartControl chart, ILogger? logger = null)
        {
            var doc = TryGetPrintDocument(chart, logger);
            if (doc == null)
            {
                logger?.LogDebug("TryShowPrintDialogAndPrint: PrintDocument not available");
                return false;
            }

            try
            {
                logger?.LogDebug("Opening print dialog");
                using var dialog = new PrintDialog
                {
                    AllowSomePages = true,
                    AllowSelection = false,
                    UseEXDialog = true,
                    Document = doc,
                };

                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    logger?.LogDebug("Print dialog cancelled by user");
                    return false;
                }

                logger?.LogInformation("Starting chart print from dialog");
                doc.Print();
                logger?.LogInformation("Chart print completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "TryShowPrintDialogAndPrint: Failed to print");
                return false;
            }
        }
    }
}
