using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Syncfusion.Drawing;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.Models;

namespace BudgetGridPreview
{
    public class PreviewForm : Form
    {
        private SfDataGrid? _grid;

        public PreviewForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Text = "Budget Grid Preview";
            Width = 1000;
            Height = 540;

            _grid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
                AllowEditing = false
            };

            // Columns similar to BudgetPanel subset
            _grid.Columns.Add(new GridTextColumn { MappingName = "AccountNumber", HeaderText = "Account" });
            _grid.Columns.Add(new GridTextColumn { MappingName = "Description", HeaderText = "Description" });
            _grid.Columns.Add(new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budgeted", Format = "C2" });
            _grid.Columns.Add(new GridNumericColumn { MappingName = "ActualAmount", HeaderText = "Actual", Format = "C2" });
            _grid.Columns.Add(new GridNumericColumn { MappingName = "PercentOfBudgetFraction", HeaderText = "% of Budget", Format = "P2" });

            _grid.QueryCellStyle += Grid_QueryCellStyle;

            Controls.Add(_grid);

            // Sample data: include a >100% case (125%)
            var sample = new List<BudgetEntry>
            {
                new BudgetEntry { AccountNumber = "410.1", Description = "Town Administrator", BudgetedAmount = 150000m, ActualAmount = 145000m },
                new BudgetEntry { AccountNumber = "440.1", Description = "Sewer Operations (Lift Station Utilities)", BudgetedAmount = 400m, ActualAmount = 500m }, // 125%
                new BudgetEntry { AccountNumber = "410.2", Description = "Town Clerk", BudgetedAmount = 80000m, ActualAmount = 82000m }
            };

            _grid.DataSource = sample;
        }

        private void Grid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
        {
            if (e == null) return;

            if (e.Column?.MappingName == "PercentOfBudgetFraction")
            {
                // Prefer underlying row data for accuracy
                if (e.DataRow?.RowData is BudgetEntry be && be.BudgetedAmount > 0)
                {
                    var frac = be.ActualAmount / be.BudgetedAmount;
                    if (frac > 1m)
                    {
                        e.Style.TextColor = Color.Red;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(e.DisplayText))
                {
                    var pctText = e.DisplayText.Replace("%", string.Empty).Trim();
                    if (decimal.TryParse(pctText, NumberStyles.Number, CultureInfo.CurrentCulture, out var rawPercent))
                    {
                        var frac = rawPercent / 100m;
                        if (frac > 1m)
                        {
                            e.Style.TextColor = Color.Red;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Captures the form to a PNG image and saves it at <paramref name="path"/>.
        /// The caller should ensure layout/paint has completed.
        /// </summary>
        public void CaptureAndSave(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                // Ensure the control has been laid out
                this.PerformLayout();

                var bmp = new Bitmap(ClientSize.Width > 0 ? ClientSize.Width : 800, ClientSize.Height > 0 ? ClientSize.Height : 600);
                DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                bmp.Save(path, ImageFormat.Png);
                bmp.Dispose();
            }
            catch
            {
                // Swallow any errors during preview capture
            }
        }
    }
}
