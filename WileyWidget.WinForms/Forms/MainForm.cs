using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.ViewModels;

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
            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem(MainFormResources.FileMenu);
            var accountsMenu = new ToolStripMenuItem(MainFormResources.AccountsMenu);
            var chartsMenu = new ToolStripMenuItem(MainFormResources.ChartsMenu);
            var settingsMenu = new ToolStripMenuItem(MainFormResources.SettingsMenu);
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu, null, (s, e) => Application.Exit());

            accountsMenu.Click += (s, e) => new AccountsForm(Program.Services.GetRequiredService<AccountsViewModel>()).ShowDialog();
            chartsMenu.Click += (s, e) => new ChartForm(Program.Services.GetRequiredService<ChartViewModel>()).ShowDialog();
            settingsMenu.Click += (s, e) => new SettingsForm(Program.Services.GetRequiredService<SettingsViewModel>()).ShowDialog();

            fileMenu.DropDownItems.Add(exitItem);
            menu.Items.AddRange(new ToolStripItem[] { fileMenu, accountsMenu, chartsMenu, settingsMenu });

            Controls.Add(menu);
            MainMenuStrip = menu;

            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
        }
    }
}
