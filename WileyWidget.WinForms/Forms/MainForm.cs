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

            // Global handlers to surface UI-thread and domain unhandled exceptions
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void InitializeComponent()
        {
            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem(MainFormResources.FileMenu);
            var accountsMenu = new ToolStripMenuItem(MainFormResources.AccountsMenu);
            var chartsMenu = new ToolStripMenuItem(MainFormResources.ChartsMenu);
            var settingsMenu = new ToolStripMenuItem(MainFormResources.SettingsMenu);
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu, null, (s, e) => Application.Exit());

            accountsMenu.Click += menuAccounts_Click; // use named handler so we can resolve form/viewmodel via DI

            // named handler defined below

            chartsMenu.Click += (s, e) => new ChartForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ChartViewModel>(Program.Services)).ShowDialog();
            settingsMenu.Click += (s, e) => new SettingsForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services)).ShowDialog();

            fileMenu.DropDownItems.Add(exitItem);
            menu.Items.AddRange(new ToolStripItem[] { fileMenu, accountsMenu, chartsMenu, settingsMenu });

            Controls.Add(menu);
            MainMenuStrip = menu;

            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void menuAccounts_Click(object? sender, EventArgs e)
        {
            try
            {
                using var scope = Program.Services.CreateScope();
                var provider = scope.ServiceProvider;
                var accountsForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsForm>(provider);
                accountsForm.ShowDialog();
            }
            catch (Exception ex)
            {
                try
                {
                    var reporting = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.ErrorReportingService>(Program.Services);
                    reporting?.ReportError(ex, "Failed to open Accounts form", showToUser: true);
                }
                catch { }
            }
        }

        private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            // Log the exception and notify the user
            Serilog.Log.Error(e.Exception, "Unhandled UI thread exception");
            MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}\n\nCheck logs for details.",
                "Application Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Serilog.Log.Fatal(ex, "Unhandled domain exception (IsTerminating: {IsTerminating})", e.IsTerminating);

            if (e.IsTerminating)
            {
                MessageBox.Show(
                    $"Fatal error:\n\n{ex?.Message ?? "Unknown error"}\n\nApplication will close.",
                    "Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Stop);
            }
        }
    }
}
