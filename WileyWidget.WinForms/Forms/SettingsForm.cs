using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    internal static class SettingsFormResources
    {
        public const string FormTitle = "Settings";
        public const string LabelText = "Settings Form";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class SettingsForm : Form
    {
        private readonly SettingsViewModel _vm;

        public SettingsForm(SettingsViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            Text = SettingsFormResources.FormTitle;
        }

        private void InitializeComponent()
        {
            // Add your settings controls here
            var label = new Label { Text = SettingsFormResources.LabelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            Controls.Add(label);
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
        }
    }
}
