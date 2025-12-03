using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainForm> _logger;
        private readonly MainViewModel? _viewModel;

        public MainForm(IServiceProvider serviceProvider, ILogger<MainForm> logger, MainViewModel? viewModel = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel;

            InitializeComponent();
            Text = MainFormResources.FormTitle;

            _logger.LogInformation("MainForm initialized successfully");
        }

        private void InitializeComponent()
        {
            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem(MainFormResources.FileMenu);
            var accountsMenu = new ToolStripMenuItem(MainFormResources.AccountsMenu);
            var chartsMenu = new ToolStripMenuItem(MainFormResources.ChartsMenu);
            var settingsMenu = new ToolStripMenuItem(MainFormResources.SettingsMenu);
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu, null, (s, e) => Application.Exit());

            // Use scoped resolution for child forms to get fresh DbContext instances
            accountsMenu.Click += (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>();
            chartsMenu.Click += (s, e) => ShowChildForm<ChartForm, ChartViewModel>();
            settingsMenu.Click += (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>();

            fileMenu.DropDownItems.Add(exitItem);
            menu.Items.AddRange(new ToolStripItem[] { fileMenu, accountsMenu, chartsMenu, settingsMenu });

            Controls.Add(menu);
            MainMenuStrip = menu;

            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void ShowChildForm<TForm, TViewModel>()
            where TForm : Form
            where TViewModel : class
        {
            try
            {
                // Create a new scope to get fresh DbContext + ViewModels for each dialog
                using var scope = _serviceProvider.CreateScope();
                var form = scope.ServiceProvider.GetRequiredService<TForm>();
                form.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show child form {FormType}", typeof(TForm).Name);
                MessageBox.Show(
                    $"Error opening {typeof(TForm).Name}: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
