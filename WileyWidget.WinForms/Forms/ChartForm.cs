using LiveChartsCore.SkiaSharpView.WinForms;
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
            var cartesian = new CartesianChart
            {
                Series = _vm.ChartSeries,
                XAxes = _vm.XAxes,
                YAxes = _vm.YAxes,
                Dock = DockStyle.Fill
            };

            var pie = new PieChart
            {
                Series = _vm.PieChartSeries,
                Dock = DockStyle.Bottom,
                Height = 300
            };

            var split = new SplitContainer { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(cartesian);
            split.Panel2.Controls.Add(pie);

            Controls.Add(split);
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;
        }
    }
}
