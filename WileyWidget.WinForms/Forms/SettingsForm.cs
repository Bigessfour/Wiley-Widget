using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
// Using standard WinForms controls for design-time build stability
using System.Windows.Forms; // TabControl, TabPage, ComboBox, TextBox


namespace WileyWidget.WinForms.Forms
{
    internal static class SettingsFormResources
    {
        public const string FormTitle = "Settings";
        public const string GeneralTab = "General";
        public const string ApiTab = "API";
        public const string ApiKeyLabel = "API Key:";
        public const string ThemeLabel = "Theme:";
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
            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // General Tab
            var generalTab = new TabPage { Text = SettingsFormResources.GeneralTab };
            var themeLabel = new Label { Text = SettingsFormResources.ThemeLabel, Location = new Point(10, 10) };
            var themeCombo = new ComboBox { Location = new Point(100, 10), Width = 200 };            
            generalTab.Controls.AddRange(new Control[] { themeLabel, themeCombo });

            // API Tab
            var apiTab = new TabPage { Text = SettingsFormResources.ApiTab };
            var apiKeyLabel = new Label { Text = SettingsFormResources.ApiKeyLabel, Location = new Point(10, 10) };
            var apiKeyTextBox = new TextBox { Location = new Point(100, 10), Width = 300 };
            apiTab.Controls.AddRange(new Control[] { apiKeyLabel, apiKeyTextBox });

            tabControl.TabPages.Add(generalTab);
            tabControl.TabPages.Add(apiTab);

            Controls.Add(tabControl);
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
        }
    }
}
