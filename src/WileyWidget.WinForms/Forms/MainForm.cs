using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
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
        private System.Threading.SynchronizationContext? _uiContext;

        private MenuStrip? _menuStrip;
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
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? new ConfigurationBuilder().Build(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? NullLogger<MainForm>.Instance)
        {
        }

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? NullLogger<MainForm>.Instance;

            // Capture UI synchronization context for marshaling from background threads
            _uiContext = Program.UISynchronizationContext ?? System.Threading.SynchronizationContext.Current;

            _useMdiMode = _configuration.GetValue<bool>("UI:UseMdiMode", true);
            _useTabbedMdi = _configuration.GetValue<bool>("UI:UseTabbedMdi", true);
            _useSyncfusionDocking = _configuration.GetValue<bool>("UI:UseDockingManager", true);

            _logger.LogInformation("UI Config loaded: UseDockingManager={Docking}, UseMdiMode={Mdi}, UseTabbedMdi={Tabbed}",
                _useSyncfusionDocking, _useMdiMode, _useTabbedMdi);

            components = new System.ComponentModel.Container();
            InitializeComponent();
            ApplyTheme();
            InitializeMdiSupport();
            InitializeSyncfusionDocking();
            UpdateStateText();

            // Add FirstChanceException handlers for comprehensive error logging
            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;
        }

        private void ApplyTheme()
        {
            try
            {
                ThemeColors.ApplyTheme(this);

                if (_ribbon != null)
                {
                    // Replace deprecated Office2019ColorScheme with ThemeName property
                    _ribbon.ThemeName = "Office2019Colorful";
                    SfSkinManager.SetVisualStyle(_ribbon, ThemeColors.DefaultTheme);
                }

                if (_statusBar != null)
                {
                    // Replace deprecated Office2019ColorScheme with ThemeName property
                    _statusBar.ThemeName = "Office2019Colorful";
                    SfSkinManager.SetVisualStyle(_statusBar, ThemeColors.DefaultTheme);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply Office2019Colorful theme to RibbonControlAdv/StatusBarAdv");

                // Fallback: Try to apply default theme
                try
                {
                    if (_ribbon != null)
                        SfSkinManager.SetVisualStyle(_ribbon, "default");
                    if (_statusBar != null)
                        SfSkinManager.SetVisualStyle(_statusBar, "default");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Theme fallback also failed");
                }

                // User-friendly fallback: Show message and continue with default theme
                try
                {
                    MessageBox.Show(
                        "Theme initialization failed. The application will continue with default styling.",
                        "Theme Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                catch (Exception msgEx)
                {
                    _logger.LogError(msgEx, "Failed to show theme warning message");
                }
            }
        }

        private void MainForm_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;

            // Log theme-related exceptions
            if (ex.Source?.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) == true || ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Office2019", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("SkinManager", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(ex, "First-chance theme exception detected: {Message}", ex.Message);
            }

            // Log docking-related exceptions
            if (ex.Source?.Contains("DockingManager", StringComparison.OrdinalIgnoreCase) == true ||
                ex.Message.Contains("dock", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("DockingManager", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(ex, "First-chance docking exception detected: {Message}", ex.Message);
            }

            // Log MDI-related exceptions
            if (ex.Message.Contains("MDI", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("Mdi", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("IsMdiContainer", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(ex, "First-chance MDI exception detected: {Message}", ex.Message);
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

            // Create a hidden MenuStrip for MDI window list integration (MenuStrip is required for MDI merging features)
            _menuStrip = new MenuStrip { Name = "MainMenuStrip", Dock = DockStyle.Top, Visible = false, AllowMerge = true };
            MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);

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
                BackColor = ThemeColors.Background,
                Name = "AiChatPlaceholder"
            };

            var lbl = new Label
            {
                Text = "AI chat panel placeholder",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ThemeColors.HeaderText,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };

            placeholder.Controls.Add(lbl);

            // Apply SkinManager to placeholder and label to respect theme
            try { SfSkinManager.SetVisualStyle(placeholder, ThemeColors.DefaultTheme); } catch { }
            try { SfSkinManager.SetVisualStyle(lbl, ThemeColors.DefaultTheme); } catch { }

            return placeholder;
        }

        private void BuildRibbon()
        {
        _ribbon = new RibbonControlAdv
        {
            Dock = Syncfusion.Windows.Forms.Tools.DockStyleEx.Top,
            MenuButtonVisible = false,
            RibbonStyle = RibbonStyle.Office2016,
            Height = 140,
            ShowRibbonDisplayOptionButton = false
        };
        SfSkinManager.SetVisualStyle(_ribbon, ThemeColors.DefaultTheme);
            _homeTab = new ToolStripTabItem { Text = "Home" };
            SfSkinManager.SetVisualStyle(_homeTab, ThemeColors.DefaultTheme);
            _navigationStrip = new ToolStripEx
            {
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(20, 20),
                Dock = DockStyle.Left,
                Padding = new Padding(10, 0, 10, 0)
            };
            SfSkinManager.SetVisualStyle(_navigationStrip, ThemeColors.DefaultTheme);

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
            try
            {
                _statusBar = new StatusBarAdv
                {
                    Dock = DockStyle.Bottom,
                    BeforeTouchSize = new Size(0, 28),
                    SizingGrip = true
                };
                SfSkinManager.SetVisualStyle(_statusBar, ThemeColors.DefaultTheme);

                _statusTextPanel = new StatusBarAdvPanel
                {
                    Text = "Ready",
                    BorderStyle = BorderStyle.None,
                    Width = 600
                };
                SfSkinManager.SetVisualStyle(_statusTextPanel, ThemeColors.DefaultTheme);
                _statusLabel = _statusTextPanel;

                _statePanel = new StatusBarAdvPanel
                {
                    Text = BuildStateText(),
                    BorderStyle = BorderStyle.None,
                    Width = 260
                };
                SfSkinManager.SetVisualStyle(_statePanel, ThemeColors.DefaultTheme);

                _clockPanel = new StatusBarAdvPanel
                {
                    Text = DateTime.Now.ToString("t", CultureInfo.CurrentCulture),
                    Alignment = HorizontalAlignment.Right,
                    BorderStyle = BorderStyle.None,
                    Width = 140
                };
                SfSkinManager.SetVisualStyle(_clockPanel, ThemeColors.DefaultTheme);

                _statusBar.Panels = new StatusBarAdvPanel[] { _statusTextPanel, _statePanel, _clockPanel };

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build status bar");

                // Fallback: Create a simple status bar without advanced features
                try
                {
                    _statusBar = new StatusBarAdv { Dock = DockStyle.Bottom };
                    _statusTextPanel = new StatusBarAdvPanel { Text = "Status bar initialization failed - using basic mode" };
                    _statusBar.Panels = new StatusBarAdvPanel[] { _statusTextPanel };
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Status bar fallback also failed");
                }
            }
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
        private void OnDashboardClicked(object? sender, EventArgs e) => ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false);
        private void OnChartsClicked(object? sender, EventArgs e) => ShowChildForm<ChartForm, ChartViewModel>();
        private void OnReportsClicked(object? sender, EventArgs e) => ShowChildForm<ReportsForm, ReportsViewModel>();
        private void OnSettingsClicked(object? sender, EventArgs e) => ShowChildForm<SettingsForm, SettingsViewModel>();

        /// <summary>
        /// Closes the settings panel if it's displayed in the current form.
        /// Called by SettingsPanel to hide itself.
        /// </summary>
        public void CloseSettingsPanel()
        {
            // Find and close any SettingsForm child windows
            foreach (Form childForm in this.MdiChildren)
            {
                if (childForm is SettingsForm settingsForm)
                {
                    settingsForm.Close();
                    return;
                }
            }
        }

        /// <summary>
        /// Closes a panel by name. Used by panels to close themselves.
        /// </summary>
        public void ClosePanel(string panelName)
        {
            // Find and close child form or panel by name
            foreach (Form childForm in this.MdiChildren)
            {
                if (childForm.Text.Contains(panelName, StringComparison.OrdinalIgnoreCase))
                {
                    childForm.Close();
                    return;
                }
            }
        }

        private void ApplyStatus(string text)
        {
            if (this.IsDisposed || this.Disposing) return;

                if (this.InvokeRequired)
                {
                    try { this.BeginInvoke(new System.Action(() => ApplyStatus(text))); } catch { }
                    return;
                }

            if (_statusTextPanel != null)
            {
                _statusTextPanel.Text = text;
            }
        }

        private string BuildStateText() => $"Docking: {(_useSyncfusionDocking ? "On" : "Off")} | MDI: {(_useMdiMode ? "On" : "Off")}";

        private void UpdateStateText()
        {
            if (this.IsDisposed || this.Disposing) return;

            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new System.Action(UpdateStateText)); } catch { }
                return;
            }

            if (_statePanel != null)
            {
                _statePanel.Text = BuildStateText();
            }
        }

        private Panel CreateDashboardCard(string title, string description, Color accent, out Label descriptionLabel)
        {
            var panel = new Panel
            {
                BackColor = ThemeColors.Background,
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
                ForeColor = ThemeManager.Colors.TextPrimary
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

                // For modal/non-MDI windows, avoid opening duplicate windows when allowMultiple=false
                if (!allowMultiple)
                {
                    var existingNonMdi = this.OwnedForms.OfType<TForm>().FirstOrDefault();
                    if (existingNonMdi != null && !existingNonMdi.IsDisposed)
                    {
                        existingNonMdi.Activate();
                        return;
                    }
                }

                IServiceScope? scope = null;
                TForm? form = null;
                try
                {
                    scope = _serviceProvider.CreateScope();
                    form = ServiceProviderServiceExtensions.GetRequiredService<TForm>(scope.ServiceProvider);
                    // Ensure scope is released once the form closes
                    form.FormClosed += (_, _) => { try { scope?.Dispose(); } catch { } };
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.Show(this);
                    ApplyStatus($"{form.Text} opened");
                }
                catch
                {
                    try { form?.Close(); } catch { }
                    try { scope?.Dispose(); } catch { }
                    throw;
                }
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

            try
            {
                // =====================================================================================
                // RELIABLE Z-ORDER MANAGEMENT FOR SYNCFUSION WINFORMS
                // =====================================================================================
                // This OnLoad handler ensures proper layering of:
                // 1. Ribbon (topmost) - Syncfusion RibbonControlAdv
                // 2. Status Bar - Syncfusion StatusBarAdv
                // 3. Central Content - AI chat or MDI client area
                // 4. Docked Panels - Syncfusion DockingManager panels (left/right sides)
                // 5. MDI Client - Must be sent to back when MDI mode is active
                //
                // Key principles:
                // - MDI client must be handled first when active (SendToBack)
                // - Ribbon and status bar must be BringToFront to stay above docked panels
                // - Central content should be visible but below chrome
                // - Docked panels are managed by DockingManager but chrome takes precedence
                // =====================================================================================

                // Step 1: Handle MDI client (must be done first when MDI is active)
                if (_useMdiMode && IsMdiContainer)
                {
                    var mdiClient = Controls.OfType<MdiClient>().FirstOrDefault();
                    if (mdiClient != null)
                    {
                        mdiClient.Dock = DockStyle.Fill;
                        mdiClient.SendToBack();
                        _logger.LogDebug("MDI client configured and sent to back");
                    }
                }

                // Step 2: Ensure ribbon is topmost (Syncfusion ribbon should be above all other chrome)
                if (_ribbon != null)
                {
                    _ribbon.BringToFront();
                    _logger.LogDebug("Ribbon brought to front");
                }

                // Step 3: Ensure status bar is above docked panels but below ribbon
                if (_statusBar != null)
                {
                    _statusBar.BringToFront();
                    _logger.LogDebug("Status bar brought to front");
                }

                // Step 4: Handle docking-specific z-order (Syncfusion DockingManager)
                if (_useSyncfusionDocking)
                {
                    try
                    {
                        EnsureDockingZOrder();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to ensure docking z-order in OnLoad");
                    }
                }
                else
                {
                    // Non-docking mode: ensure central content is visible
                    try
                    {
                        EnsureNonDockingVisibility();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to ensure non-docking visibility in OnLoad");
                    }
                }

                // Step 5: Force layout refresh to apply all z-order changes
                this.Refresh();
                this.Invalidate();

                _logger.LogDebug("Z-order management completed successfully");

                // Background simulation to test cross-thread UI updates and docking save
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        System.Threading.Thread.Sleep(500);
                        ApplyStatus("Background load complete");

                        if (_useSyncfusionDocking)
                        {
                            try
                            {
                                if (this.InvokeRequired)
                                {
                                    try
                                    {
                                        this.BeginInvoke(new System.Action(() =>
                                        {
                                            try { SaveDockingLayout(); } catch (Exception ex) { _logger.LogWarning(ex, "Background simulated SaveDockingLayout failed"); }
                                        }));
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to BeginInvoke background docking save");
                                    }
                                }
                                else
                                {
                                    SaveDockingLayout();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Background docking save simulation failed");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Background simulation failed");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Z-order management failed in OnLoad - controls may overlap");
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Ensure central panels remain visible after resize operations
            // This handles cases where layout changes during resize can affect z-order
            if (_useSyncfusionDocking)
            {
                try
                {
                    EnsureCentralPanelVisibility();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to ensure visibility during resize");
                }
            }
        }
    }
}
