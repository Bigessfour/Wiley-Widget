using System;
using System.Drawing.Printing;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;

namespace WileyWidget.WinForms.Extensions
{
    internal static class ChartControlPrinting
    {
        public static PrintDocument? TryGetPrintDocument(ChartControl chart)
        {
            if (chart == null) return null;

            try
            {
                // ChartControl exposes a PrintDocument that renders the chart for printing.
                return chart.PrintDocument;
            }
            catch
            {
                return null;
            }
        }

        public static bool TryPrint(ChartControl chart)
        {
            var doc = TryGetPrintDocument(chart);
            if (doc == null) return false;

            try
            {
                doc.Print();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryShowPrintPreview(IWin32Window owner, ChartControl chart, string? title = null)
        {
            var doc = TryGetPrintDocument(chart);
            if (doc == null) return false;

            try
            {
                using var dialog = new PrintPreviewDialog
                {
                    Document = doc,
                };

                if (!string.IsNullOrWhiteSpace(title))
                {
                    dialog.Text = title;
                }

                dialog.ShowDialog(owner);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryShowPrintDialogAndPrint(IWin32Window owner, ChartControl chart)
        {
            var doc = TryGetPrintDocument(chart);
            if (doc == null) return false;

            try
            {
                using var dialog = new PrintDialog
                {
                    AllowSomePages = true,
                    AllowSelection = false,
                    UseEXDialog = true,
                    Document = doc,
                };

                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return false;
                }

                doc.Print();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
