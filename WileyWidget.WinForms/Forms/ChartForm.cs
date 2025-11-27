using Syncfusion.Windows.Forms.Chart;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    internal static class ChartFormResources
    {
        public const string FormTitle = "Budget Analytics";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class ChartForm : Form
    {
        private readonly ChartViewModel _vm;

        public ChartForm(ChartViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            Text = ChartFormResources.FormTitle;
            Load += async (s, e) => await _vm.LoadChartDataAsync();
        }

        private void InitializeComponent()
        {
            // Create a Syncfusion ChartControl for WinForms and populate it with series
            var chartControl = new ChartControl
            {
                Dock = DockStyle.Fill
            };

            // Line series for monthly points
            var lineSeries = new ChartSeries
            {
                Name = "Monthly",
                Type = ChartSeriesType.Line
            };

            foreach (var p in _vm.MonthlyPoints)
            {
                var pt = new ChartPoint { Category = p.Month, YValues = new double[] { p.Amount } };
                lineSeries.Points.Add(pt);
            }

            chartControl.Series.Add(lineSeries);

            // Pie series for distribution
            var pieSeries = new ChartSeries
            {
                Name = "Distribution",
                Type = ChartSeriesType.Pie
            };

            foreach (var p in _vm.PiePoints)
            {
                var pt = new ChartPoint { Category = p.Category, YValues = new double[] { p.Amount } };
                pieSeries.Points.Add(pt);
            }

            var pieChart = new ChartControl
            {
                Dock = DockStyle.Bottom,
                Height = 300
            };
            pieChart.Series.Add(pieSeries);

            var split = new SplitContainer { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(chartControl);
            split.Panel2.Controls.Add(pieChart);

            Controls.Add(split);
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;
        }
    }
}
