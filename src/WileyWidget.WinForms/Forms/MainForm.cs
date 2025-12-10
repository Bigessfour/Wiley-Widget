using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    internal static class MainFormResources
    {
        public const string FormTitle = "Wiley Widget — Running on WinForms + .NET 9";
        public const string Dashboard = "Dashboard";
        public const string Accounts = "Accounts";
        public const string Charts = "Charts";
        public const string Reports = "Reports";
        public const string Settings = "Settings";
        public const string Docking = "Docking";
        public const string Mdi = "MDI Mode";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : Form
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MainForm> _logger;

        private RibbonControlAdv? _ribbon;
        private ToolStripTabItem? _homeTab;
        private ToolStripEx? _navigationStrip;
        private StatusBarAdv? _statusBar;
        private StatusBarAdvPanel? _statusLabel;
        private StatusBarAdvPanel? _statusTextPanel;
        private StatusBarAdvPanel? _statePanel;
        private StatusBarAdvPanel? _clockPanel;
        private System.Windows.Forms.Timer? _statusTimer;

        private Control? _aiChatControl;
        private Panel? _aiChatPanel;
        private Label? _accountsDescLabel;
        private Label? _chartsDescLabel;
        private Label? _settingsDescLabel;
        private Label? _reportsDescLabel;
        private Label? _infoDescLabel;
        private System.ComponentModel.IContainer? components;

        public MainForm()
            : this(
                Program.Services ?? new ServiceCollection().BuildServiceProvider(),
                Program.Services?.GetService<IConfiguration>() ?? new ConfigurationBuilder().Build(),
                Program.Services?.GetService<ILogger<MainForm>>() ?? NullLogger<MainForm>.Instance)
        {
        }

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? NullLogger<MainForm>.Instance;

            _useMdiMode = _configuration.GetValue<bool>("UI:UseMdiMode", true);
            _useTabbedMdi = _configuration.GetValue<bool>("UI:UseTabbedMdi", true);
            _useSyncfusionDocking = _configuration.GetValue<bool>("UI:UseDockingManager", true);

            components = new System.ComponentModel.Container();
            InitializeComponent();
            ApplyTheme();
            InitializeMdiSupport();
            InitializeSyncfusionDocking();
            UpdateStateText();
        }

        private void ApplyTheme()
        {
            ThemeColors.ApplyTheme(this);

            try
            {
                SfSkinManager.SetVisualStyle(this, VisualTheme.Office2019Colorful);

                if (_ribbon != null)
                {
                    _ribbon.ThemeName = "Office2019Colorful";
                    _ribbon.Office2019ColorScheme = Office2019ColorScheme.Colorful;
                }

                if (_statusBar != null)
                {
                    _statusBar.ThemeName = "Office2019Colorful";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to apply Syncfusion visual theme");
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 820);
            StartPosition = FormStartPosition.CenterScreen;
            Text = MainFormResources.FormTitle;
            KeyPreview = true;

            _aiChatControl = BuildAiPlaceholder();
            _aiChatPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
            _aiChatPanel.Controls.Add(_aiChatControl);

            BuildRibbon();
            BuildStatusBar();

            Controls.Add(_aiChatPanel);
            if (_statusBar != null)
            {
                Controls.Add(_statusBar);
            }

            if (_ribbon != null)
            {
                Controls.Add(_ribbon);
            }

            ResumeLayout(performLayout: false);
            PerformLayout();
        }

        private Control BuildAiPlaceholder()
        {
            var placeholder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Name = "AiChatPlaceholder"
            };

            placeholder.Controls.Add(new Label
            {
                Text = "AI chat panel placeholder",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(64, 64, 64)
            });

            return placeholder;
        }

        private void BuildRibbon()
        {
            _ribbon = new RibbonControlAdv
            {
                Dock = DockStyle.Top,
                MenuButtonVisible = false,
                RibbonStyle = RibbonStyle.Office2016,
                Height = 140,
                ShowRibbonDisplayOptionButton = false
            };

            _homeTab = new ToolStripTabItem { Text = "Home" };
            _navigationStrip = new ToolStripEx
            {
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(20, 20),
                Dock = DockStyle.Left,
                Padding = new Padding(10, 0, 10, 0)
            };

            var dashboardButton = CreateRibbonButton(MainFormResources.Dashboard, OnDashboardClicked, "Open dashboard overview");
            var accountsButton = CreateRibbonButton(MainFormResources.Accounts, OnAccountsClicked, "Open municipal accounts");
            var chartsButton = CreateRibbonButton(MainFormResources.Charts, OnChartsClicked, "Open charts view");
            var reportsButton = CreateRibbonButton(MainFormResources.Reports, OnReportsClicked, "Open reports");
            var settingsButton = CreateRibbonButton(MainFormResources.Settings, OnSettingsClicked, "Application settings");

            var dockingToggle = CreateRibbonToggle(MainFormResources.Docking, _useSyncfusionDocking, ToggleDockingRequested);
            var mdiToggle = CreateRibbonToggle(MainFormResources.Mdi, _useMdiMode, ToggleMdiRequested);

            _navigationStrip.Items.AddRange(new ToolStripItem[]
            {
                dashboardButton,
                accountsButton,
                chartsButton,
                reportsButton,
                settingsButton,
                new ToolStripSeparator(),
                dockingToggle,
                mdiToggle
            });

            _homeTab.Panel.Controls.Add(_navigationStrip);
            _ribbon.Header.AddMainItem(_homeTab);
            _ribbon.SelectedTab = _homeTab;
        }

        private void BuildStatusBar()
        {
            _statusBar = new StatusBarAdv
            {
                Dock = DockStyle.Bottom,
                ShowPanels = true,
                BeforeTouchSize = new Size(0, 28),
                SizeGrip = true
            };

            _statusTextPanel = new StatusBarAdvPanel
            {
                Text = "Ready",
                BorderStyle = BorderStyle.None,
                Width = 600
            };
            _statusLabel = _statusTextPanel;

            _statePanel = new StatusBarAdvPanel
            {
                Text = BuildStateText(),
                BorderStyle = BorderStyle.None,
                Width = 260
            };

            _clockPanel = new StatusBarAdvPanel
            {
                Text = DateTime.Now.ToString("t", CultureInfo.CurrentCulture),
                Alignment = HorizontalAlignment.Right,
                BorderStyle = BorderStyle.None,
                Width = 140
            };

            _statusBar.Panels.AddRange(new[] { _statusTextPanel, _statePanel, _clockPanel });

            _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _statusTimer.Tick += (s, e) =>
            {
                if (_clockPanel != null)
                {
                    _clockPanel.Text = DateTime.Now.ToString("t", CultureInfo.CurrentCulture);
                }
            };
            _statusTimer.Start();
        }

        private ToolStripButton CreateRibbonButton(string text, EventHandler onClick, string toolTip)
        {
            var button = new ToolStripButton
            {
                Text = text,
                AutoSize = false,
                Width = 110,
                Height = 72,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageAboveText,
                ToolTipText = toolTip
            };
            button.Click += onClick;
            return button;
        }

        private ToolStripButton CreateRibbonToggle(string text, bool isChecked, EventHandler onClick)
        {
            var toggle = new ToolStripButton
            {
                Text = text,
                CheckOnClick = true,
                Checked = isChecked,
                AutoSize = false,
                Width = 110,
                Height = 72,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageAboveText
            };
            toggle.Click += onClick;
            return toggle;
        }

        private void ToggleDockingRequested(object? sender, EventArgs e)
        {
            ToggleDockingMode();
            UpdateStateText();
        }

        private void ToggleMdiRequested(object? sender, EventArgs e)
        {
            UseMdiMode = !UseMdiMode;
            UpdateStateText();
        }

        private void OnAccountsClicked(object? sender, EventArgs e) => ShowChildForm<AccountsForm, AccountsViewModel>();
        private void OnDashboardClicked(object? sender, EventArgs e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: true);
        private void OnChartsClicked(object? sender, EventArgs e) => ShowChildForm<ChartForm, ChartViewModel>();
        private void OnReportsClicked(object? sender, EventArgs e) => ShowChildForm<ReportsForm, ReportsViewModel>();
        private void OnSettingsClicked(object? sender, EventArgs e) => ShowChildForm<SettingsForm, SettingsViewModel>();

        private void ApplyStatus(string text)
        {
            if (_statusTextPanel != null)
            {
                _statusTextPanel.Text = text;
            }
        }

        private string BuildStateText() => $"Docking: {(_useSyncfusionDocking ? "On" : "Off")} | MDI: {(_useMdiMode ? "On" : "Off")}";

        private void UpdateStateText()
        {
            if (_statePanel != null)
            {
                _statePanel.Text = BuildStateText();
            }
        }

        private Panel CreateDashboardCard(string title, string description, Color accent, out Label descriptionLabel)
        {
            var panel = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Top,
                Height = 140,
                Padding = new Padding(12),
                Margin = new Padding(4)
            };

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
                ForeColor = accent,
                Height = 28
            };

            descriptionLabel = new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            panel.Controls.Add(descriptionLabel);
            panel.Controls.Add(titleLabel);

            return panel;
        }

        private void SetupCardClickHandler(Control card, System.Action onClick)
        {
            void Wire(Control control)
            {
                control.Cursor = Cursors.Hand;
                control.Click += (_, _) => onClick();
                foreach (Control child in control.Controls)
                {
                    Wire(child);
                }
            }

            Wire(card);
        }

        private void ShowChildForm<TForm, TViewModel>(bool allowMultiple = false)
            where TForm : Form
            where TViewModel : class
        {
            try
            {
                if (_useMdiMode)
                {
                    ShowChildFormMdi<TForm, TViewModel>(allowMultiple);
                    UpdateStateText();
                    return;
                }

                var scope = _serviceProvider.CreateScope();
                var form = ServiceProviderServiceExtensions.GetRequiredService<TForm>(scope.ServiceProvider);
                form.FormClosed += (_, _) => scope.Dispose();
                form.StartPosition = FormStartPosition.CenterParent;
                form.Show(this);
                ApplyStatus($"{form.Text} opened");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open child form {Form}", typeof(TForm).Name);
                ApplyStatus($"Unable to open {typeof(TForm).Name}");
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateStateText();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            HandleMdiKeyboardShortcuts(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusTimer?.Dispose();
                _ribbon?.Dispose();
                _statusBar?.Dispose();
                components?.Dispose();
                DisposeSyncfusionDockingResources();
                DisposeMdiResources();
            }
            base.Dispose(disposing);
        }
    }
}
