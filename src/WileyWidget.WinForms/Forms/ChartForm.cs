using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Drawing;
using System.Drawing;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

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
            ThemeColors.ApplyTheme(this);
            Load += async (s, e) => await _vm.LoadChartDataAsync();
        }

        private void InitializeComponent()
        {
            // Create a Cartesian Chart using Syncfusion ChartControl
            var cartesian = new ChartControl
            {
                Dock = DockStyle.Fill,
                Text = "Budget Trend"
            };

            cartesian.PrimaryXAxis.Title = "Month";
            cartesian.PrimaryYAxis.Title = "Amount ($)";

            // Create a pie chart using ChartControl with Pie series type
            var pie = new ChartControl
            {
                Dock = DockStyle.Bottom,
                Height = 300,
                Text = "Budget Distribution"
            };

            // Hide axis for pie chart by setting stroke to transparent
            pie.PrimaryXAxis.DrawGrid = false;
            pie.PrimaryYAxis.DrawGrid = false;

            // Populate initial chart series (loaded later when data is available)
            var split = new SplitContainer { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(cartesian);
            split.Panel2.Controls.Add(pie);

            Controls.Add(split);
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;

            // After loading data populate chart series
            Load += async (s, e) =>
            {
                await _vm.LoadChartDataAsync();
                // Build line series
                cartesian.Series.Clear();
                var series = new ChartSeries("Revenue", ChartSeriesType.Line);
                series.Style.Interior = new BrushInfo(GradientStyle.None, ThemeColors.PrimaryAccent);
                foreach (var data in _vm.MonthlyRevenueData)
                {
                    series.Points.Add(data.MonthNumber, (double)data.Amount);
                }
                cartesian.Series.Add(series);

                // Build pie series
                pie.Series.Clear();
                var pieSeries = new ChartSeries("Distribution", ChartSeriesType.Pie);
                foreach (var p in _vm.PieChartData)
                {
                    pieSeries.Points.Add(p.Category, (double)p.Value);
                }
                pie.Series.Add(pieSeries);
            };
        }
    }
}
