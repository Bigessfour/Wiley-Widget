using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.ViewModels;
using ServiceProviderExtensions = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using Syncfusion.Windows.Forms.Chart;
using System.IO;

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

        // Dashboard card labels for dynamic data binding
        private Label? _accountsDescLabel;
        private Label? _chartsDescLabel;
        private Label? _settingsDescLabel;
        private Label? _infoDescLabel;
        private ToolStripStatusLabel? _statusLabel;

        public MainForm(IServiceProvider serviceProvider, ILogger<MainForm> logger, MainViewModel? viewModel = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel;

            // Guard the UI initialization so form-level exceptions are logged with full stacks
            try
            {
                InitializeComponent();
                Text = MainFormResources.FormTitle;

                // Subscribe to ViewModel property changes for dynamic updates
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                    // Trigger initial data load
                    _ = InitializeDataAsync();
                }

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
                if (_viewModel != null)
                {
                    await _viewModel.InitializeAsync();
                    UpdateDashboardCards();
                }
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
                }
                else if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                {
                    _statusLabel.Text = $"Error: {_viewModel.ErrorMessage}";
                }
                else
                {
                    _statusLabel.Text = $"Ready — Last updated: {_viewModel.LastUpdateTime ?? "N/A"}";
                }
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // === Menu Strip ===
            var menu = new MenuStrip { Dock = DockStyle.Top };
            var fileMenu = new ToolStripMenuItem(MainFormResources.FileMenu);
            var viewMenu = new ToolStripMenuItem(MainFormResources.ViewMenu);
            var toolsMenu = new ToolStripMenuItem(MainFormResources.ToolsMenu);
            var helpMenu = new ToolStripMenuItem(MainFormResources.HelpMenu);

            // File menu items
            var accountsMenuItem = new ToolStripMenuItem(MainFormResources.AccountsMenu, null, (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>());
            var chartsMenuItem = new ToolStripMenuItem(MainFormResources.ChartsMenu, null, (s, e) => ShowChildForm<ChartForm, ChartViewModel>());
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu, null, (s, e) => Application.Exit());
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { accountsMenuItem, chartsMenuItem, new ToolStripSeparator(), exitItem });

            // View menu items
            var refreshItem = new ToolStripMenuItem(MainFormResources.RefreshMenu, null, (s, e) => RefreshDashboard());
            refreshItem.ShortcutKeys = Keys.F5;
            viewMenu.DropDownItems.Add(refreshItem);

            // Tools menu items
            var settingsMenuItem = new ToolStripMenuItem(MainFormResources.SettingsMenu, null, (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>());
            toolsMenu.DropDownItems.Add(settingsMenuItem);

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
                SplitterDistance = 850,
                BackColor = Color.FromArgb(245, 245, 250)
            };

            // === Left Panel: Dashboard Cards ===
            var dashboardPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 245, 250)
            };
            dashboardPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            dashboardPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            // === Quick Action Cards (with dynamic data binding) ===
            var accountsCard = CreateDashboardCard("📊 Accounts", "View and manage municipal accounts\n\nLoading...", Color.FromArgb(66, 133, 244), out _accountsDescLabel);
            SetupCardClickHandler(accountsCard, () => ShowChildForm<AccountsForm, AccountsViewModel>());

            var chartsCard = CreateDashboardCard("📈 Charts", "Budget analytics and visualizations\n\nLoading...", Color.FromArgb(52, 168, 83), out _chartsDescLabel);
            SetupCardClickHandler(chartsCard, () => ShowChildForm<ChartForm, ChartViewModel>());

            var settingsCard = CreateDashboardCard("⚙️ Settings", "Configure application preferences\n\nLoading...", Color.FromArgb(251, 188, 4), out _settingsDescLabel);
            SetupCardClickHandler(settingsCard, () => ShowChildForm<SettingsForm, SettingsViewModel>());

            var infoCard = CreateDashboardCard("ℹ️ Budget Status", $"Loading budget data...\n\nRuntime: {Environment.Version}\nMemory: {GC.GetTotalMemory(false) / 1024 / 1024} MB", Color.FromArgb(234, 67, 53), out _infoDescLabel);

            dashboardPanel.Controls.Add(accountsCard, 0, 0);
            dashboardPanel.Controls.Add(chartsCard, 1, 0);
            dashboardPanel.Controls.Add(settingsCard, 0, 1);
            dashboardPanel.Controls.Add(infoCard, 1, 1);

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
                RowHeight = 36
            };
            activityGrid.Columns.Add(new GridTextColumn { MappingName = "Time", HeaderText = "Time", Width = 80 });
            activityGrid.Columns.Add(new GridTextColumn { MappingName = "Action", HeaderText = "Action", Width = 150 });
            activityGrid.Columns.Add(new GridTextColumn { MappingName = "Details", HeaderText = "Details", Width = 200 });

            // Add sample activity items
            var activities = new[]
            {
                new { Time = DateTime.Now.AddMinutes(-5).ToString("HH:mm", CultureInfo.CurrentCulture), Action = "Account Updated", Details = "GL-1001" },
                new { Time = DateTime.Now.AddMinutes(-15).ToString("HH:mm", CultureInfo.CurrentCulture), Action = "Report Generated", Details = "Budget Q4" },
                new { Time = DateTime.Now.AddMinutes(-30).ToString("HH:mm", CultureInfo.CurrentCulture), Action = "QuickBooks Sync", Details = "42 records" },
                new { Time = DateTime.Now.AddHours(-1).ToString("HH:mm", CultureInfo.CurrentCulture), Action = "User Login", Details = "Admin" },
                new { Time = DateTime.Now.AddHours(-2).ToString("HH:mm", CultureInfo.CurrentCulture), Action = "Backup Complete", Details = "12.5 MB" }
            };
            activityGrid.DataSource = activities;
            activityPanel.Controls.Add(activityGrid);
            activityPanel.Controls.Add(activityHeader);
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

            var quickAccountsBtn = new ToolStripButton("📊 Accounts", null, (s, e) => ShowChildForm<AccountsForm, AccountsViewModel>());
            var quickChartsBtn = new ToolStripButton("📈 Charts", null, (s, e) => ShowChildForm<ChartForm, ChartViewModel>());
            var quickSettingsBtn = new ToolStripButton("⚙️ Settings", null, (s, e) => ShowChildForm<SettingsForm, SettingsViewModel>());
            var quickRefreshBtn = new ToolStripButton("🔄 Refresh", null, (s, e) => RefreshDashboard());
            var quickExportBtn = new ToolStripButton("📄 Export", null, (s, e) => ExportDashboard());

            quickToolbar.Items.AddRange(new ToolStripItem[]
            {
                quickAccountsBtn,
                new ToolStripSeparator(),
                quickChartsBtn,
                new ToolStripSeparator(),
                quickSettingsBtn,
                new ToolStripSeparator(),
                quickRefreshBtn,
                new ToolStripSeparator(),
                quickExportBtn
            });

            // === Add Controls in Order ===
            Controls.Add(mainSplit);
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
                        // Create PDF report using Syncfusion
                        using var document = new Syncfusion.Pdf.PdfDocument();
                        var page = document.Pages.Add();
                        var graphics = page.Graphics;

                        // Draw header
                        var headerFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 20, Syncfusion.Pdf.Graphics.PdfFontStyle.Bold);
                        graphics.DrawString($"Dashboard Report - {DateTime.Now:yyyy-MM-dd}", headerFont,
                            Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 10));

                        var bodyFont = new Syncfusion.Pdf.Graphics.PdfStandardFont(Syncfusion.Pdf.Graphics.PdfFontFamily.Helvetica, 10);
                        var yPosition = 50f;

                        // Draw dashboard metrics
                        graphics.DrawString($"Total Budget: {_viewModel.TotalBudget:C}", bodyFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                        yPosition += 20;
                        graphics.DrawString($"Total Actual: {_viewModel.TotalActual:C}", bodyFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                        yPosition += 20;
                        graphics.DrawString($"Variance: {_viewModel.Variance:C}", bodyFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                        yPosition += 20;
                        graphics.DrawString($"Active Accounts: {_viewModel.ActiveAccountCount}", bodyFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                        yPosition += 20;
                        graphics.DrawString($"Total Departments: {_viewModel.TotalDepartments}", bodyFont, Syncfusion.Pdf.Graphics.PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, yPosition));
                        yPosition += 30;

                        // Footer
                        graphics.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", bodyFont, Syncfusion.Pdf.Graphics.PdfBrushes.Gray, new Syncfusion.Drawing.PointF(10, yPosition));

                        // Save the document
                        document.Save(saveDialog.FileName);
                        _logger.LogInformation("Dashboard exported to: {FileName}", saveDialog.FileName);
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
                _logger.LogWarning(oce, "Showing child form {FormType} was canceled", typeof(TForm).Name);
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
    }
}
