extern alias sync31pdf;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.ViewModels;
using ServiceProviderExtensions = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using Syncfusion.Windows.Forms.Chart;
using System.IO;
using System.Drawing;
using SyncPdf = sync31pdf::Syncfusion.Pdf;
using SyncPdfGraphics = sync31pdf::Syncfusion.Pdf.Graphics;
using WileyWidget.WinForms.Controls;
using Microsoft.Extensions.Configuration;
using Syncfusion.WinForms.Controls;
using System.ComponentModel;
using System.Collections;
using System.Windows.Forms.Design;
using System.Text.Json;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Forms
{
    internal static class MainFormResources
    {
        public const string FormTitle = "Wiley Widget — Running on WinForms + .NET 9";
        public const string FileMenu = "File";
        public const string ViewMenu = "View";
        public const string ToolsMenu = "Tools";
        public const string HelpMenu = "Help";
        public const string AccountsMenu = "Accounts";
        public const string ChartsMenu = "Charts";
        public const string BudgetOverviewMenu = "Budget Overview";
        public const string SettingsMenu = "Settings";
        public const string ExitMenu = "Exit";
        public const string RefreshMenu = "Refresh";
        public const string AboutMenu = "About";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : Form
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainForm> _logger;
        private readonly MainViewModel? _viewModel;
        private readonly IConfiguration _configuration;
        private IContainer? components = null;

        // Activity binding source
        private BindingSource? _activityBindingSource;

        // Simple loading indicator for long-running operations
        private ProgressBar? _loadingBar;
        // ToolTip helper for cards and toolbar items
        private ToolTip? _toolTip;

        // Timer for periodic status/dashboard refresh
        private System.Windows.Forms.Timer? _statusTimer;

        // Optional dashboard service for lightweight refreshes
        private WileyWidget.Services.Abstractions.IDashboardService? _dashboardSvc;

        // Dashboard card labels for dynamic data binding
        private Label? _accountsDescLabel;
        private Label? _chartsDescLabel;
        private Label? _settingsDescLabel;
        private Label? _reportsDescLabel;
        private Label? _infoDescLabel;
        private ToolStripStatusLabel? _statusLabel;

        // AI Chat Panel
        private Panel? _aiChatPanel;
        private AIChatControl? _aiChatControl;

        // Cancellation token source for async operations
        private CancellationTokenSource? _cts;

        public MainForm(IServiceProvider serviceProvider, ILogger<MainForm> logger, IConfiguration configuration, MainViewModel? viewModel = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _viewModel = viewModel;

            // Guard the UI initialization so form-level exceptions are logged with full stacks
            try
            {
                // Apply Syncfusion theme globally before initializing components
                var themeName = _configuration.GetValue<string>("UI:SyncfusionTheme", "Office2019Colorful");
                ApplySyncfusionTheme(themeName);

                InitializeComponent();
                Text = MainFormResources.FormTitle;

                // Enable keyboard shortcuts (e.g., Ctrl+1 for AI panel)
                KeyPreview = true;
                KeyDown += MainForm_KeyDown;

                // Phase 2: Initialize Syncfusion docking if enabled in configuration
                var useSyncfusionDocking = _configuration.GetValue<bool>("UI:UseSyncfusionDocking", false);
                if (useSyncfusionDocking)
                {
                    _useSyncfusionDocking = true;
                    InitializeSyncfusionDocking();
                }

                // Subscribe to ViewModel property changes for dynamic updates
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                    // Trigger initial data load
                    _ = InitializeDataAsync();
                }

                // Initialize cancellation token source
                _cts = new CancellationTokenSource();

                FormClosing += (s, e) =>
                {
                    Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
                };

                _logger.LogInformation("MainForm initialized successfully");
            }
            catch (Exception ex)
            {
                // Log full stack and surface to user, then rethrow so the host can handle fatal startup errors
                _logger.LogCritical(ex, "Fatal error while initializing MainForm");
                throw;
            }
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                if (_viewModel != null && _cts != null)
                {
                    await _viewModel.InitializeAsync(_cts.Token);
                    UpdateDashboardCards();

                    // Attempt to restore persisted UI state (AI panel visibility)
                    try
                    {
                        var appStateSvc = ServiceProviderExtensions.GetService<WileyWidget.Abstractions.IApplicationStateService>(_serviceProvider);
                        if (appStateSvc != null)
                        {
                            var restored = await appStateSvc.RestoreStateAsync().ConfigureAwait(false);
                            if (restored != null)
                            {
                                // Try multiple common shapes for the saved object
                                if (restored is System.Collections.IDictionary dict && dict.Contains("IsAIChatVisible"))
                                {
                                    var val = dict["IsAIChatVisible"];
                                    if (val is bool b)
                                    {
                                        _aiChatPanel!.Visible = b;
                                    }
                                }
                                else if (restored is JsonElement je && je.ValueKind == JsonValueKind.Object && je.TryGetProperty("IsAIChatVisible", out var prop))
                                {
                                    if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                                    {
                                        _aiChatPanel!.Visible = prop.GetBoolean();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to restore application UI state");
                    }

                        // Initialize periodic dashboard refresh timer (30s) using IDashboardService when available
                        try
                        {
                            _dashboardSvc = ServiceProviderExtensions.GetService<WileyWidget.Services.Abstractions.IDashboardService>(_serviceProvider);
                            if (_dashboardSvc != null)
                            {
                                _statusTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
                                _statusTimer.Tick += async (s, e) =>
                                {
                                    try
                                    {
                                        await _dashboardSvc.RefreshDashboardAsync();
                                        if (_viewModel != null)
                                        {
                                            // Trigger ViewModel refresh to update UI
                                            _viewModel.LoadDataCommand.Execute(null);
                                        }
                                    }
                                    catch (OperationCanceledException) { }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Periodic dashboard refresh failed");
                                    }
                                };
                                _statusTimer.Start();
                                _logger.LogInformation("Started dashboard status timer (30s)");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to initialize dashboard timer");
                        }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogDebug(oce, "Dashboard initialization was canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize dashboard data");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update UI on any relevant property change
            if (InvokeRequired)
            {
                Invoke(() => UpdateDashboardCards());
            }
            else
            {
                UpdateDashboardCards();
            }
        }

        private void UpdateDashboardCards()
        {
            if (_viewModel == null) return;

            // Update Accounts card with real data
            if (_accountsDescLabel != null)
            {
                _accountsDescLabel.Text = $"View and manage municipal accounts\n\n{_viewModel.ActiveAccountCount} active accounts\n{_viewModel.TotalDepartments} departments";
            }

            // Update Charts card with real budget data
            if (_chartsDescLabel != null)
            {
                _chartsDescLabel.Text = $"Budget analytics and visualizations\n\nBudget: {_viewModel.TotalBudget:C0}\nActual: {_viewModel.TotalActual:C0}";
            }

            // Update Settings card with last update time
            if (_settingsDescLabel != null)
            {
                var updateInfo = string.IsNullOrEmpty(_viewModel.LastUpdateTime) ? "Not synced" : $"Last sync: {_viewModel.LastUpdateTime}";
                _settingsDescLabel.Text = $"Configure application preferences\n\nQuickBooks: Connected\n{updateInfo}";
            }

            // Update Reports card
            if (_reportsDescLabel != null)
            {
                _reportsDescLabel.Text = "Generate and view detailed reports\n\nBudget reports, audit logs\nand financial summaries";
            }

            // Update System Info card
            if (_infoDescLabel != null)
            {
                var variance = _viewModel.Variance;
                var varianceText = variance >= 0 ? $"+{variance:C0}" : $"{variance:C0}";
                var varianceStatus = variance >= 0 ? "Under budget ✓" : "Over budget ⚠";
                _infoDescLabel.Text = $"Variance: {varianceText}\n{varianceStatus}\nRuntime: {Environment.Version}\nMemory: {GC.GetTotalMemory(false) / 1024 / 1024} MB";
            }

            // Update status bar
            if (_statusLabel != null)
            {
                if (_viewModel.IsLoading)
                {
                    _statusLabel.Text = "Loading data...";
                    if (_loadingBar != null) _loadingBar.Visible = true;
                }
                else if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                {
                    _statusLabel.Text = $"Error: {_viewModel.ErrorMessage}";
                    if (_loadingBar != null) _loadingBar.Visible = false;
                }
                else
                {
                    _statusLabel.Text = $"Ready — Last updated: {_viewModel.LastUpdateTime ?? "N/A"}";
                    if (_loadingBar != null) _loadingBar.Visible = false;
                }
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // === Menu Strip ===
            var menu = new MenuStrip { Dock = DockStyle.Top };
            menu.ShowItemToolTips = true;
            var fileMenu = new ToolStripMenuItem(MainFormResources.FileMenu);
            var viewMenu = new ToolStripMenuItem(MainFormResources.ViewMenu);
            var toolsMenu = new ToolStripMenuItem(MainFormResources.ToolsMenu);
            var helpMenu = new ToolStripMenuItem(MainFormResources.HelpMenu);

            // File menu items
            var accountsMenuItem = new ToolStripMenuItem(MainFormResources.AccountsMenu, null, (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>());
            var chartsMenuItem = new ToolStripMenuItem(MainFormResources.ChartsMenu, null, (s, e) => ShowChildForm<ChartForm, ChartViewModel>());
            var budgetOverviewMenuItem = new ToolStripMenuItem(MainFormResources.BudgetOverviewMenu, null, (s, e) => ShowChildForm<BudgetOverviewForm, BudgetOverviewViewModel>());
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu, null, (s, e) => Application.Exit());
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { accountsMenuItem, chartsMenuItem, budgetOverviewMenuItem, new ToolStripSeparator(), exitItem });

            // View menu items
            var refreshItem = new ToolStripMenuItem(MainFormResources.RefreshMenu, null, (s, e) => RefreshDashboard());
            refreshItem.ShortcutKeys = Keys.F5;
            viewMenu.DropDownItems.Add(refreshItem);

            // Tools menu items
            var settingsMenuItem = new ToolStripMenuItem(MainFormResources.SettingsMenu, null, (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>());
            var reportsMenuItem = new ToolStripMenuItem("Reports", null, (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>());
            toolsMenu.DropDownItems.AddRange(new ToolStripItem[] { settingsMenuItem, reportsMenuItem });

            // Help menu items
            var aboutItem = new ToolStripMenuItem(MainFormResources.AboutMenu, null, (s, e) => ShowAboutDialog());
            helpMenu.DropDownItems.Add(aboutItem);

            menu.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, toolsMenu, helpMenu });

            // === Status Strip ===
            var statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            _statusLabel = new ToolStripStatusLabel("Loading...") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var connectionStatus = new ToolStripStatusLabel("🟢 Database Connected") { ForeColor = Color.Green };
            var versionLabel = new ToolStripStatusLabel(".NET 9 | WinForms") { Alignment = ToolStripItemAlignment.Right };
            statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, connectionStatus, versionLabel });

            // === Main Split Container ===
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700,  // Reduced from 850 to balance with wider AI panel
                BackColor = Color.FromArgb(245, 245, 250)
            };

            // === Left Panel: Dashboard Cards ===
            var dashboardPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 245, 250)
            };
            dashboardPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            dashboardPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));

            // === Quick Action Cards (with dynamic data binding) ===
            var accountsCard = CreateDashboardCard("📊 Accounts", "View and manage municipal accounts\n\nLoading...", Color.FromArgb(66, 133, 244), out _accountsDescLabel);
            SetupCardClickHandler(accountsCard, () => ShowChildForm<AccountsForm, AccountsViewModel>());

            var chartsCard = CreateDashboardCard("📈 Charts", "Budget analytics and visualizations\n\nLoading...", Color.FromArgb(52, 168, 83), out _chartsDescLabel);
            SetupCardClickHandler(chartsCard, () => ShowChildForm<ChartForm, ChartViewModel>());

            var settingsCard = CreateDashboardCard("⚙️ Settings", "Configure application preferences\n\nLoading...", Color.FromArgb(251, 188, 4), out _settingsDescLabel);
            SetupCardClickHandler(settingsCard, () => ShowChildForm<SettingsForm, SettingsViewModel>());

            var reportsCard = CreateDashboardCard("📄 Reports", "Generate and view detailed reports\n\nLoading...", Color.FromArgb(156, 39, 176), out _reportsDescLabel);
            SetupCardClickHandler(reportsCard, () => ShowChildForm<ReportsForm, ReportsViewModel>());

            var infoCard = CreateDashboardCard("ℹ️ Budget Status", $"Loading budget data...\n\nRuntime: {Environment.Version}\nMemory: {GC.GetTotalMemory(false) / 1024 / 1024} MB", Color.FromArgb(234, 67, 53), out _infoDescLabel);

            dashboardPanel.Controls.Add(accountsCard, 0, 0);
            dashboardPanel.Controls.Add(chartsCard, 1, 0);
            dashboardPanel.Controls.Add(settingsCard, 0, 1);
            dashboardPanel.Controls.Add(reportsCard, 1, 1);
            dashboardPanel.Controls.Add(infoCard, 0, 2);

            mainSplit.Panel1.Controls.Add(dashboardPanel);

            // === Right Panel: Recent Activity ===
            var activityPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            var activityHeader = new Label
            {
                Text = "📋 Recent Activity",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Top,
                Height = 35,
                Padding = new Padding(5, 8, 0, 0)
            };

            var activityGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                ShowGroupDropArea = false,
                RowHeight = 36,
                // Performance settings for large datasets
                AllowSorting = true,
                AllowFiltering = true
            };
            // Map columns to ActivityItem properties
            activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridDateTimeColumn { MappingName = "Timestamp", HeaderText = "Time", Format = "HH:mm", Width = 80 });
            activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Activity", HeaderText = "Action", Width = 150 });
            activityGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Details", HeaderText = "Details", Width = 200 });

            // Bind to ViewModel ActivityItems when available, otherwise fall back to sample data
            if (_viewModel != null)
            {
                _activityBindingSource = new BindingSource { DataSource = _viewModel.ActivityItems };
                activityGrid.DataSource = _activityBindingSource;
            }
            else
            {
                // Create typed sample ActivityItem instances for consistent binding
                var activities = new[]
                {
                    new ActivityItem { Timestamp = DateTime.Now.AddMinutes(-5), Activity = "Account Updated", Details = "GL-1001", User = "System" },
                    new ActivityItem { Timestamp = DateTime.Now.AddMinutes(-15), Activity = "Report Generated", Details = "Budget Q4", User = "Scheduler" },
                    new ActivityItem { Timestamp = DateTime.Now.AddMinutes(-30), Activity = "QuickBooks Sync", Details = "42 records", User = "Integrator" },
                    new ActivityItem { Timestamp = DateTime.Now.AddHours(-1), Activity = "User Login", Details = "Admin", User = "Admin" },
                    new ActivityItem { Timestamp = DateTime.Now.AddHours(-2), Activity = "Backup Complete", Details = "12.5 MB", User = "System" }
                };
                activityGrid.DataSource = activities;
            }
            activityPanel.Controls.Add(activityGrid);
            activityPanel.Controls.Add(activityHeader);
            // Progress bar used during ViewModel IsLoading state
            _loadingBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 6,
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            activityPanel.Controls.Add(_loadingBar);
            mainSplit.Panel2.Controls.Add(activityPanel);

            // === Header Panel ===
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(33, 37, 41),
                Padding = new Padding(20, 0, 20, 0)
            };

            var headerLabel = new Label
            {
                Text = "🏛️ Wiley Widget Dashboard",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var dateTimeLabel = new Label
            {
                Text = DateTime.Now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(173, 181, 189),
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(headerPanel.Width - 200, 30)
            };

            headerPanel.Controls.Add(dateTimeLabel);
            headerPanel.Controls.Add(headerLabel);

            // === Quick Action Toolbar ===
            var quickToolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(10, 0, 0, 0)
            };

            quickToolbar.ShowItemToolTips = true;

            // Initialize a shared ToolTip for non-ToolStrip controls
            _toolTip = new ToolTip { ShowAlways = true, AutoPopDelay = 8000, InitialDelay = 300, ReshowDelay = 100 };

            var quickAccountsBtn = new ToolStripButton("📊 Accounts", null, (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>());
            var quickChartsBtn = new ToolStripButton("📈 Charts", null, (s, e) => ShowChildForm<ChartForm, ChartViewModel>());
            var quickBudgetBtn = new ToolStripButton("💰 Budget", null, (s, e) => ShowChildForm<BudgetOverviewForm, BudgetOverviewViewModel>());
            var quickReportsBtn = new ToolStripButton("📄 Reports", null, (s, e) => ShowChildForm<ReportsForm, ReportsViewModel>());
            var quickSettingsBtn = new ToolStripButton("⚙️ Settings", null, (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>());
            var quickRefreshBtn = new ToolStripButton("🔄 Refresh", null, (s, e) => RefreshDashboard());
            var quickExportBtn = new ToolStripButton("📄 Export", null, (s, e) => ExportDashboard());
            var quickAIAssistantBtn = new ToolStripButton("🤖 AI Assistant", null, (s, e) => ToggleAIChatPanel());

            quickToolbar.Items.AddRange(new ToolStripItem[]
            {
                quickAccountsBtn,
                new ToolStripSeparator(),
                quickChartsBtn,
                new ToolStripSeparator(),
                quickBudgetBtn,
                new ToolStripSeparator(),
                quickReportsBtn,
                new ToolStripSeparator(),
                quickSettingsBtn,
                new ToolStripSeparator(),
                quickRefreshBtn,
                new ToolStripSeparator(),
                quickExportBtn,
                new ToolStripSeparator(),
                quickAIAssistantBtn
            });

            // Tooltips for toolbar items
            quickAccountsBtn.ToolTipText = "Open Accounts — View and manage municipal accounts (Alt+A)";
            quickChartsBtn.ToolTipText = "Open Charts — Budget analytics (Alt+C)";
            quickBudgetBtn.ToolTipText = "Open Budget Overview (Alt+B)";
            quickReportsBtn.ToolTipText = "Open Reports (Alt+R)";
            quickSettingsBtn.ToolTipText = "Open Settings (Alt+S)";
            quickRefreshBtn.ToolTipText = "Refresh Dashboard (F5 or Ctrl+R)";
            quickExportBtn.ToolTipText = "Export Dashboard to PDF";
            quickAIAssistantBtn.ToolTipText = "Toggle AI Assistant (Ctrl+1)";

            // Tooltips for dashboard cards
            if (_toolTip != null)
            {
                try
                {
                    _toolTip.SetToolTip(accountsCard, "Open Accounts — View and manage municipal accounts. Click to open.");
                    _toolTip.SetToolTip(chartsCard, "Open Charts — View budget analytics and visualizations.");
                    _toolTip.SetToolTip(settingsCard, "Open Settings — Configure application preferences.");
                    _toolTip.SetToolTip(reportsCard, "Open Reports — Generate and view detailed reports.");
                    _toolTip.SetToolTip(infoCard, "Budget status summary: variance, runtime, memory.");
                }
                catch { }
            }

            // === AI Chat Panel (Right Docked, Visible on Launch - Phase 1 Enhancement) ===
            var aiDefaultWidth = _configuration.GetValue<int>("UI:AIDefaultWidth", 550);
            var defaultAIVisible = _configuration.GetValue<bool>("UI:DefaultAIVisible", true);

            _aiChatPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = aiDefaultWidth,  // Configurable via appsettings.json
                BackColor = Color.FromArgb(248, 249, 250),
                Visible = defaultAIVisible  // Configurable - AI visible on launch by default
            };

            try
            {
                // AIChatControl is not registered in DI; manually resolve dependencies and instantiate
                var aiService = ServiceProviderExtensions.GetRequiredService<WileyWidget.Services.Abstractions.IAIAssistantService>(_serviceProvider);
                var aiLogger = ServiceProviderExtensions.GetRequiredService<ILogger<AIChatControl>>(_serviceProvider);

                // Attempt to resolve optional conversational AI service for fallback responses
                var conversationalAI = ServiceProviderExtensions.GetService<WileyWidget.Services.Abstractions.IAIService>(_serviceProvider);

                _aiChatControl = new AIChatControl(aiService, aiLogger, conversationalAI);

                _aiChatControl.Dock = DockStyle.Fill;
                _aiChatPanel.Controls.Add(_aiChatControl);

                if (conversationalAI != null)
                {
                    _logger.LogInformation("AIChatControl initialized with IAIAssistantService + conversational AI fallback (IAIService)");
                }
                else
                {
                    _logger.LogInformation("AIChatControl initialized with IAIAssistantService (conversational AI fallback not available)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AI Chat Control - check DI configuration");
            }

            // === Add Controls in Order ===
            Controls.Add(mainSplit);
            Controls.Add(_aiChatPanel);
            Controls.Add(quickToolbar);
            Controls.Add(headerPanel);
            Controls.Add(statusStrip);
            Controls.Add(menu);

            MainMenuStrip = menu;
            Size = new Size(1200, 800);
            MinimumSize = new Size(900, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 245, 250);

            ResumeLayout(false);
            PerformLayout();
        }

        private void RefreshDashboard()
        {
            _logger.LogInformation("Dashboard refresh requested");
            // Trigger a refresh of dashboard data
            if (_viewModel != null)
            {
                try
                {
                    _viewModel.LoadDataCommand.Execute(null);
                    _logger.LogInformation("Dashboard data refresh initiated");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh dashboard data");
                }
            }
            else
            {
                Invalidate(true);
            }
        }

        private void ToggleAIChatPanel()
        {
            if (_aiChatPanel != null)
            {
                var previousState = _aiChatPanel.Visible;
                _aiChatPanel.Visible = !_aiChatPanel.Visible;
                _logger.LogInformation(
                    "AI Chat Panel toggled: {PreviousState} → {NewState}. " +
                    "Consider persisting this preference to user settings.",
                    previousState ? "Visible" : "Hidden",
                    _aiChatPanel.Visible ? "Visible" : "Hidden");

                // Auto-focus AI control when panel is shown
                if (_aiChatPanel.Visible && _aiChatControl != null)
                {
                    _aiChatControl.Focus();
                    _logger.LogDebug("AI Chat Control focused after panel show");
                }

                // Persist AI panel visibility
                try
                {
                    var appStateSvc = ServiceProviderExtensions.GetService<WileyWidget.Abstractions.IApplicationStateService>(_serviceProvider);
                    if (appStateSvc != null)
                    {
                        var stateObj = new { IsAIChatVisible = _aiChatPanel.Visible };
                        _ = appStateSvc.SaveStateAsync(stateObj);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to persist AI panel visibility");
                }
            }
        }

        /// <summary>
        /// Handle keyboard shortcuts: Ctrl+1 toggles AI panel, Ctrl+D toggles Syncfusion docking (Phase 2)
        /// </summary>
        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            // Ctrl+1: Toggle AI Chat Panel
            if (e.Control && e.KeyCode == Keys.D1)
            {
                ToggleAIChatPanel();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Ctrl+R: Refresh dashboard
            if (e.Control && e.KeyCode == Keys.R)
            {
                RefreshDashboard();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Ctrl+D: Toggle between standard and Syncfusion docking (Phase 2 - future)
            if (e.Control && e.KeyCode == Keys.D)
            {
                // TODO: Implement Syncfusion docking toggle in Phase 2
                _logger.LogInformation("Syncfusion docking toggle requested (not yet implemented)");
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ExportDashboard()
        {
            _logger.LogInformation("Dashboard export requested");
            try
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files|*.pdf|All Files|*.*",
                    DefaultExt = "pdf",
                    FileName = $"DashboardReport_{DateTime.Now:yyyyMMdd}"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    if (_viewModel != null)
                    {
                        // Use extern alias to reference the Syncfusion PDF assembly explicitly (see csproj Aliases)
                        try
                        {
                            using var document = new SyncPdf.PdfDocument();
                            var page = document.Pages.Add();
                            var graphics = page.Graphics;
                            var font = new SyncPdfGraphics.PdfStandardFont(SyncPdfGraphics.PdfFontFamily.Helvetica, 12);

                            graphics.DrawString("Wiley Widget — Dashboard Report", font, SyncPdfGraphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(20, 20));
                            graphics.DrawString($"Generated: {DateTime.Now}", font, SyncPdfGraphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(20, 40));

                            // Embed some key summary metrics from ViewModel
                            graphics.DrawString($"Total Budget: {_viewModel.TotalBudget:C}", font, SyncPdfGraphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(20, 80));
                            graphics.DrawString($"Total Actual: {_viewModel.TotalActual:C}", font, SyncPdfGraphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(20, 100));
                            graphics.DrawString($"Variance: {_viewModel.Variance:C}", font, SyncPdfGraphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(20, 120));
                            graphics.DrawString($"Active Accounts: {_viewModel.ActiveAccountCount}", font, SyncPdfGraphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(20, 140));

                            // Add a lightweight activity list
                            var y = 180f;
                            foreach (var item in _viewModel.ActivityItems)
                            {
                                var line = $"{item.Timestamp:yyyy-MM-dd HH:mm} - {item.Activity} - {item.Details}";
                                graphics.DrawString(line, font, SyncPdfGraphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(20, y));
                                y += 18f;
                                if (y > page.GetClientSize().Height - 40)
                                {
                                    // Create a new page
                                    var next = document.Pages.Add();
                                    graphics = next.Graphics;
                                    y = 20f;
                                }
                            }

                            document.Save(saveDialog.FileName);
                            document.Close(true);
                            _logger.LogInformation("Exported dashboard to PDF: {File}", saveDialog.FileName);
                            MessageBox.Show($"Dashboard exported to {saveDialog.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to generate PDF export using Syncfusion PDF");
                            MessageBox.Show("Failed to export PDF. See logs for details.", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("ViewModel not available for export");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export dashboard");
            }
        }

        private void ShowAboutDialog()
        {
            _logger.LogInformation("About dialog requested: Wiley Widget v1.0.0, Municipal Budget Management System, Runtime: .NET {Version}, OS: {OS}, © 2025 Wiley Widget Corp", Environment.Version, Environment.OSVersion);
        }

        /// <summary>
        /// Apply Syncfusion visual style to this form and controls.
        /// Note: In Syncfusion v31.2.16 for WinForms, theming is control-specific.
        /// For comprehensive theming, consider Syncfusion.WinForms.Themes package.
        /// </summary>
        private void ApplySyncfusionTheme(string themeName)
        {
            try
            {
                // Apply visual style to form - basic color scheme based on theme name
                // Note: Full theme support requires Syncfusion.WinForms.Themes package
                // which provides ThemeSettings and comprehensive theming

                // For now, apply basic styling to form
                switch (themeName)
                {
                    case "Office2016DarkGray":
                    case "MaterialDark":
                    case "HighContrastBlack":
                        BackColor = Color.FromArgb(45, 45, 48);
                        ForeColor = Color.White;
                        break;
                    case "MaterialLight":
                    case "Office2019Colorful":
                    case "Office2016Colorful":
                    default:
                        BackColor = Color.FromArgb(245, 245, 250);
                        ForeColor = Color.FromArgb(33, 37, 41);
                        break;
                }

                _logger.LogInformation("Applied Syncfusion visual style: {Theme}", themeName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply Syncfusion theme {Theme}, using default", themeName);
            }
        }

        private static Panel CreateDashboardCard(string title, string description, Color accentColor, out Label? descriptionLabel)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10),
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            // Accent bar at top
            var accentBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 5,
                BackColor = accentColor
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(0, 10, 0, 0)
            };

            var descLabel = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 5, 0, 0)
            };

            // Return reference for dynamic updates
            descriptionLabel = descLabel;

            card.Controls.Add(descLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(accentBar);

            // Hover effect
            card.MouseEnter += (s, e) => card.BackColor = Color.FromArgb(248, 249, 250);
            card.MouseLeave += (s, e) => card.BackColor = Color.White;

            return card;
        }

        private static void SetupCardClickHandler(Panel card, Action clickAction)
        {
            card.Click += (s, e) => clickAction();
            card.Cursor = Cursors.Hand;

            // Propagate click and hover to all child controls
            foreach (Control c in card.Controls)
            {
                c.Click += (s, e) => clickAction();
                c.Cursor = Cursors.Hand;
                c.MouseEnter += (s, e) => card.BackColor = Color.FromArgb(248, 249, 250);
                c.MouseLeave += (s, e) => card.BackColor = Color.White;
            }
        }

        private void ShowChildForm<TForm, TViewModel>()
            where TForm : Form
            where TViewModel : class
        {
            try
            {
                // Create a new scope to get fresh DbContext + ViewModels for each dialog
                using var scope = _serviceProvider.CreateScope();
                var form = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TForm>(scope.ServiceProvider);
                form.ShowDialog(this);
            }
            catch (OperationCanceledException oce)
            {
                // Child form startup was canceled (likely shutdown or abort). Log and continue without raising modal errors.
                _logger.LogDebug(oce, "Showing child form {FormType} was canceled", typeof(TForm).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show child form {FormType}", typeof(TForm).Name);
#if DEBUG
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Break();
                }
#endif
                // After logging/alerting, rethrow so callers or the app host can detect and handle the failure
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusLabel?.Dispose();
                // Dispose AI chat controls
                try
                {
                    _aiChatControl?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed disposing AI chat control during form Dispose");
                }

                try
                {
                    _aiChatPanel?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed disposing AI chat panel during form Dispose");
                }

                // Dispose docking-related resources defined in the docking partial
                try
                {
                    DisposeSyncfusionDockingResources();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed disposing docking resources during form Dispose");
                }

                // Dispose IContainer for DockingManager
                try
                {
                    components?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed disposing components container");
                }

                // Cancel and dispose async operations
                Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
                // Dispose status timer
                try
                {
                    if (_statusTimer != null)
                    {
                        _statusTimer.Stop();
                        _statusTimer.Dispose();
                        _statusTimer = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed disposing status timer");
                }

                // Dispose tooltip
                try
                {
                    _toolTip?.Dispose();
                    _toolTip = null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed disposing ToolTip");
                }
            }
            base.Dispose(disposing);
        }
    }
}
