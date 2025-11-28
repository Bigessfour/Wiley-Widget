using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
using System.Windows.Forms;
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
            var label = new Label
            {
                Text = ChartFormResources.DisabledMessage,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Regular)
            };

            Controls.Add(label);
            Size = new Size(800, 400);
            StartPosition = FormStartPosition.CenterParent;
        }
    }
}
