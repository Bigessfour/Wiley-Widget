using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Forms
{
    internal static class MainFormResources
    {
        public const string FormTitle = "Wiley Widget — Running on WinForms + .NET 9";
        public const string FileMenu = "File";
        public const string AccountsMenu = "Accounts";
        public const string ChartsMenu = "Charts";
        public const string SettingsMenu = "Settings";
        public const string ExitMenu = "Exit";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            Text = MainFormResources.FormTitle;
        }

        private void InitializeComponent()
        {
            // Use a lightweight ToolStrip to avoid third-party Ribbon incompatibilities
            var toolStrip = new ToolStrip { Dock = DockStyle.Top };

            // File dropdown (simple) with Exit
            var fileDrop = new ToolStripDropDownButton(MainFormResources.FileMenu);
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu);
            exitItem.Click += (s, e) => Application.Exit();
            fileDrop.DropDownItems.Add(exitItem);

            // Actions: Accounts, Charts, Settings, Dashboard
            var accountsBtn = new ToolStripButton(MainFormResources.AccountsMenu);
            accountsBtn.Click += (s, e) => new AccountsForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(Program.Services)).ShowDialog();

            var chartsBtn = new ToolStripButton(MainFormResources.ChartsMenu);
            chartsBtn.Click += (s, e) => new ChartForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ChartViewModel>(Program.Services)).ShowDialog();

            var settingsBtn = new ToolStripButton(MainFormResources.SettingsMenu);
            settingsBtn.Click += (s, e) => new SettingsForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services)).ShowDialog();

            var dashboardBtn = new ToolStripButton("Dashboard");
            dashboardBtn.Click += (s, e) => new DashboardForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DashboardViewModel>(Program.Services)).ShowDialog();

            toolStrip.Items.Add(fileDrop);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(accountsBtn);
            toolStrip.Items.Add(chartsBtn);
            toolStrip.Items.Add(settingsBtn);
            toolStrip.Items.Add(dashboardBtn);

            Controls.Add(toolStrip);

            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
        }
    }
}
