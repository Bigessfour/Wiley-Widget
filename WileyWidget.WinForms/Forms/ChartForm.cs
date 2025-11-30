using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;

namespace WileyWidget.WinForms.Forms
{
    internal static class ChartFormResources
    {
        public const string FormTitle = "Budget Analytics";
        public const string DisabledMessage = "Charts are currently disabled in this build. Use the Analytics module or Syncfusion charts for reporting.";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class ChartForm : Form
    {
        /// <summary>
        /// Expose the VM as a simple DataContext for hosting scenarios
        /// </summary>
        public new object? DataContext { get; private set; }
        private readonly ChartViewModel _vm;

        public ChartForm(ChartViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
            InitializeComponent();
            Text = ChartFormResources.FormTitle;
            Load += async (s, e) => await _vm.LoadChartDataAsync();
        }

        /// <summary>
        /// Prepare the chart form to be used as a docked child (non-top-level).
        /// </summary>
        public void PrepareForDocking()
        {
            try
            {
                TopLevel = false;
                FormBorderStyle = FormBorderStyle.None;
                Dock = DockStyle.Fill;
                StartPosition = FormStartPosition.Manual;
            }
            catch { }
        }

        private void InitializeComponent()
        {
            Size = new Size(900, 500);
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
            StartPosition = FormStartPosition.CenterParent;

            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Window
            };

            var area = new ChartArea("MainArea");
            area.AxisX.Interval = 1;
            area.AxisX.IsMarginVisible = true;
            area.AxisX.Title = "Department";
            area.AxisY.Title = "Variance (Actual - Budget)";
            chart.ChartAreas.Add(area);

            var series = new Series("Variance")
            {
                ChartType = SeriesChartType.Bar,
                XValueType = ChartValueType.String,
                YValueType = ChartValueType.Double,
                IsValueShownAsLabel = true
            };

            chart.Series.Add(series);

            Controls.Add(chart);

            // Apply theme for this dialog
            try
            {
                WileyWidget.WinForms.Theming.ThemeManager.ApplyTheme(this);
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) => WileyWidget.WinForms.Theming.ThemeManager.ApplyTheme(this);
            }
            catch { }

            // Load data into chart when form loads
            Load += async (s, e) =>
            {
                await _vm.LoadChartDataAsync();
                series.Points.Clear();

                if (_vm.ChartData != null && _vm.ChartData.Any())
                {
                    foreach (var kv in _vm.ChartData.OrderByDescending(k => k.Value))
                    {
                        var ptIndex = series.Points.AddXY(kv.Key, kv.Value);
                        series.Points[ptIndex].Label = kv.Value.ToString("N0");
                    }
                }
                else
                {
                    // No data or error, show appropriate message
                    var message = !string.IsNullOrEmpty(_vm.ErrorMessage)
                        ? _vm.ErrorMessage
                        : (_vm.ChartData != null && !_vm.ChartData.Any()
                            ? "No department budget data available to display."
                            : ChartFormResources.DisabledMessage);

                    var noDataLabel = new Label
                    {
                        Text = message,
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Regular)
                    };

                    Controls.Clear();
                    Controls.Add(noDataLabel);
                }
            };
        }
    }
}
