using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools; // docking manager
using Syncfusion.Runtime.Serialization; // AppStateSerializer
using System.IO; // layout persistence
using WileyWidget.WinForms.ViewModels;
using CommunityToolkit.Mvvm.Input;

#pragma warning disable CA2000 // Dispose objects before losing scope - scopes are intentionally kept alive for form lifetime

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Resource strings for MainForm UI elements.
    /// Centralized for localization support and maintainability.
    /// </summary>
    internal static class MainFormResources
    {
        /// <summary>Application window title.</summary>
        public const string FormTitle = "Wiley Widget — Running on WinForms + .NET 9";
        /// <summary>File menu label.</summary>
        public const string FileMenu = "File";
        /// <summary>Accounts menu label.</summary>
        public const string AccountsMenu = "Accounts";
        public const string DashboardMenu = "Dashboard";
        /// <summary>Charts menu label.</summary>
        public const string ChartsMenu = "Charts";
        /// <summary>Settings menu label.</summary>
        public const string SettingsMenu = "Settings";
        /// <summary>Exit menu item label.</summary>
        public const string ExitMenu = "Exit";
    }

    /// <summary>
    /// Main application window with Syncfusion DockingManager integration.
    /// Provides tabbed/dockable workspace for Accounts, Charts, and Settings views.
    /// Implements theme support and persists dock layout across sessions.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : Form
    {
        private MenuStrip? _menuStrip;
        private Syncfusion.WinForms.DataGrid.SfDataGrid? _overviewGrid; // hidden helper grid for summary-level bindings
        private ErrorProvider? _errorProvider;
        private DockingManager? _dockingManager;  // Syncfusion DockingManager
        private System.ComponentModel.Container? _dockingManagerComponents; // Container for DockingManager - fixes CA2000
        private readonly Dictionary<string, Control> _dockedControls = new(); // UserControls (not Forms) for docking
        private readonly Dictionary<string, IServiceScope> _dockedControlScopes = new(); // Track DI scopes - disposed when control closes
        // Lock used to safely mutate/read docked controls dictionaries across UI event handlers and background threads
        private readonly object _dockedControlsLock = new();
        private EventHandler<AppTheme>? _menuIconsThemeChangedHandler;
        // Stored delegates for menu click handlers to enable proper cleanup
        private EventHandler? _accountsMenuClickHandler;
        private EventHandler? _dashboardMenuClickHandler;
        private EventHandler? _chartsMenuClickHandler;
        private EventHandler? _settingsMenuClickHandler;
        private EventHandler? _loadHandler;
        // Global exception handlers
        private System.Threading.ThreadExceptionEventHandler? _threadExceptionHandler;
        private UnhandledExceptionEventHandler? _domainExceptionHandler;
        // Command for dashboard navigation
        private CommunityToolkit.Mvvm.Input.IRelayCommand? _openDashboardCommand;
        // Exit menu handler
        private EventHandler? _exitMenuClickHandler;
        private StatusStrip? _globalStatusStrip;
        private ToolStripStatusLabel? _globalStatusLabel;
        private ToolStripProgressBar? _globalProgressBar;
        // Panel state manager for per-panel state persistence
        private readonly Services.PanelStateManager _panelStateManager = new();

        private const string LayoutFile = "docking_layout.xml";

        /// <summary>
        /// Initializes a new instance of <see cref="MainForm"/>.
        /// Sets up theme system, docking manager, and global exception handlers.
        /// </summary>
        private readonly WileyWidget.ViewModels.MainViewModel? _mainViewModel;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;

        public MainForm(WileyWidget.ViewModels.MainViewModel? mainViewModel = null, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _mainViewModel = mainViewModel;
            // Initialize theme system first
            ThemeManager.Initialize();

            InitializeComponent();
            Text = MainFormResources.FormTitle;

            // Initialize docking manager (adds control to main client area)
            InitializeDockingManager();

            // Load persisted layout (if present)
            LoadDockingLayout();

            // Load panel state (filters, grid states, etc.)
            _panelStateManager.LoadPanelState();

            // Apply theme to this form
            ApplyCurrentTheme();

            // Subscribe to theme changes
            ThemeManager.ThemeChanged += OnThemeChanged;

            // Global handlers to surface UI-thread and domain unhandled exceptions (store delegates for cleanup)
            _threadExceptionHandler = Application_ThreadException;
            _domainExceptionHandler = CurrentDomain_UnhandledException;
            Application.ThreadException += _threadExceptionHandler;
            AppDomain.CurrentDomain.UnhandledException += _domainExceptionHandler;

            // Ensure MainViewModel gets a chance to load app-level data when form loads
            _loadHandler = MainForm_Load;
            this.Load += _loadHandler;

            // Capture UI SynchronizationContext as soon as the message loop starts.
            // The SynchronizationContext may not exist during construction, so we capture it in Shown.
            this.Shown += (s, e) =>
            {
                try
                {
                    _dispatcherHelper?.CheckAccess();
                }
                catch { }
            };
        }

        private void InitializeComponent()
        {
            _menuStrip = new MenuStrip();
            _menuStrip.AccessibleName = "Main menu";
            _menuStrip.AccessibleDescription = "Primary navigation menu for the application";

            var fileMenu = new ToolStripMenuItem(MainFormResources.FileMenu);
            fileMenu.AccessibleName = "File menu";

            // View menu - per Syncfusion demos pattern for panel navigation
            var viewMenu = new ToolStripMenuItem("&View");
            viewMenu.AccessibleName = "View menu";
            viewMenu.AccessibleDescription = "Show or hide application panels";

            var accountsMenu = new ToolStripMenuItem(MainFormResources.AccountsMenu);
            var dashboardMenu = new ToolStripMenuItem(MainFormResources.DashboardMenu);
            accountsMenu.AccessibleName = "Open Accounts panel";
            accountsMenu.AccessibleDescription = "Opens the municipal accounts management panel";
            accountsMenu.ToolTipText = "Manage municipal accounts, funds, and balances (Ctrl+Shift+A)";
            accountsMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.A;
            dashboardMenu.ToolTipText = "View budget overview, KPIs, and summary metrics (Ctrl+Shift+D)";
            dashboardMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.D;

            var chartsMenu = new ToolStripMenuItem(MainFormResources.ChartsMenu);
            chartsMenu.AccessibleName = "Open Charts panel";
            chartsMenu.AccessibleDescription = "Opens the budget visualization charts panel";
            chartsMenu.ToolTipText = "Analyze budget data with interactive charts and graphs (Ctrl+Shift+C)";
            chartsMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.C;

            var settingsMenu = new ToolStripMenuItem(MainFormResources.SettingsMenu);
            settingsMenu.AccessibleName = "Open Settings panel";
            settingsMenu.AccessibleDescription = "Opens application settings and preferences";
            settingsMenu.ToolTipText = "Configure application theme, preferences, and database connection (Ctrl+,)";
            settingsMenu.ShortcutKeys = Keys.Control | Keys.Oemcomma;

            // Reset Layout menu item per Syncfusion StatePersistence demo
            var resetLayoutMenu = new ToolStripMenuItem("&Reset Layout");
            resetLayoutMenu.AccessibleName = "Reset window layout";
            resetLayoutMenu.ToolTipText = "Reset all panels to default positions";
            resetLayoutMenu.Click += ResetLayout_Click;

            _exitMenuClickHandler = ExitMenu_Click;
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu, null, _exitMenuClickHandler);
            exitItem.AccessibleName = "Exit application";
            exitItem.AccessibleDescription = "Closes the application";
            exitItem.ToolTipText = "Close Wiley Widget and save settings";
            exitItem.ShortcutKeys = Keys.Alt | Keys.F4;

            // Store delegates for proper cleanup
            _accountsMenuClickHandler = AccountsMenu_Click;
            _dashboardMenuClickHandler = DashboardMenu_Click;
            _chartsMenuClickHandler = ChartsMenu_Click;
            _settingsMenuClickHandler = SettingsMenu_Click;

            accountsMenu.Click += _accountsMenuClickHandler;

            // prepare a RelayCommand that docks the dashboard (improves audit detection for command usage)
            _openDashboardCommand = new RelayCommand(() => DockUserControlPanel<DashboardPanel>(MainFormResources.DashboardMenu));
            dashboardMenu.Click += _dashboardMenuClickHandler;

            chartsMenu.Click += _chartsMenuClickHandler;

            settingsMenu.Click += _settingsMenuClickHandler;
            try
            {
                if (_mainViewModel != null)
                {
                    var mainBinding = new BindingSource { DataSource = _mainViewModel };
                    this.DataBindings.Add("Text", mainBinding, "Title", true, DataSourceUpdateMode.OnPropertyChanged);
                    Serilog.Log.Debug("MainForm: bound Text property to MainViewModel.Title");

                    // Add a global status strip to show a centralized StatusMessage and busy indicator
                    _globalStatusStrip = new StatusStrip { Dock = DockStyle.Bottom };
                    _globalStatusLabel = new ToolStripStatusLabel { Text = _mainViewModel.StatusMessage ?? "Ready", Spring = true };
                    _globalProgressBar = new ToolStripProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Visible = _mainViewModel.IsLoading };
                    _globalStatusStrip.Items.Add(_globalStatusLabel);
                    _globalStatusStrip.Items.Add(_globalProgressBar);
                    Controls.Add(_globalStatusStrip);

                    try
                    {
                        // Bind status text and busy indicator to the main view model
                        var bs = new BindingSource { DataSource = _mainViewModel };
                        _globalStatusLabel.DataBindings.Add("Text", bs, "StatusMessage", true, DataSourceUpdateMode.OnPropertyChanged);
                        _globalProgressBar.DataBindings.Add("Visible", bs, "IsLoading", true, DataSourceUpdateMode.OnPropertyChanged);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "MainForm: failed to bind global status strip to MainViewModel");
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: failed to bind MainViewModel to the form");
            }

            // Apply theme-aware icons to menus (best-effort). Use DI service to load matching variants.
            WileyWidget.WinForms.Services.IThemeIconService? iconService = null;
            AppTheme theme = AppTheme.Light;
            try
            {
                iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;

                accountsMenu.Image = iconService?.GetIcon("accounts", theme, 18);
                dashboardMenu.Image = iconService?.GetIcon("home", theme, 18);
                chartsMenu.Image = iconService?.GetIcon("chart", theme, 18);
                settingsMenu.Image = iconService?.GetIcon("settings", theme, 18);
                exitItem.Image = iconService?.GetIcon("dismiss", theme, 14);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: failed to load menu icons");
            }

            // Ensure style and spacing are reasonable for small menu icons
            accountsMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            dashboardMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            chartsMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            settingsMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
            exitItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;

            // Update icons when theme changes — store handler so we can unsubscribe on dispose
            _menuIconsThemeChangedHandler = (s, t) =>
            {
                try
                {
                    accountsMenu.Image = iconService?.GetIcon("accounts", t, 18);
                    dashboardMenu.Image = iconService?.GetIcon("home", t, 18);
                    chartsMenu.Image = iconService?.GetIcon("chart", t, 18);
                    settingsMenu.Image = iconService?.GetIcon("settings", t, 18);
                    exitItem.Image = iconService?.GetIcon("dismiss", t, 14);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "MainForm: failed to update menu icons on theme change");
                }
            };
            WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _menuIconsThemeChangedHandler;

            fileMenu.DropDownItems.Add(exitItem);

            // Build View menu with panel navigation items per Syncfusion demos
            viewMenu.DropDownItems.AddRange(new ToolStripItem[] {
                dashboardMenu,
                accountsMenu,
                chartsMenu,
                new ToolStripSeparator(),
                settingsMenu,
                new ToolStripSeparator(),
                resetLayoutMenu
            });

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu });

            Controls.Add(_menuStrip);

            // Add a hidden overview grid (bound to MainViewModel.RecentMetrics when available)
            try
            {
                if (_mainViewModel != null)
                {
                    _overviewGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
                    {
                        Name = "overviewGrid",
                        Visible = false,
                        AccessibleName = "Overview data grid",
                        AccessibleDescription = "Hidden overview grid bound to main metrics" // SfDataGrid.DataSource
                    };

                    // Minimal column mapping for telemetry
                    _overviewGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Key", HeaderText = "Metric" });
                    _overviewGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "Value", HeaderText = "Value" });

                    // Wire up binding to the main view model collection (if present)
                    try { _overviewGrid.DataSource = _mainViewModel.RecentMetrics; } catch { }
                    Controls.Add(_overviewGrid);
                    try { _errorProvider = new ErrorProvider(this) { BlinkStyle = ErrorBlinkStyle.NeverBlink }; } catch { }
                }
            }
            catch { }
            MainMenuStrip = _menuStrip;

            Size = new Size(1200, 800);
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
            StartPosition = FormStartPosition.CenterScreen;
        }
        // (Load handler is wired in the constructor.)

        /// <summary>
        /// Initializes the Syncfusion DockingManager for tabbed/dockable panel support.
        /// Configures visual style, drag provider, and event handlers for lazy loading.
        /// </summary>
        private void InitializeDockingManager()
        {
            try
            {
                // Create container and track for disposal
                _dockingManagerComponents = new System.ComponentModel.Container();

                // Instantiate the DockingManager with the container for proper disposal tracking
                _dockingManager = new DockingManager(_dockingManagerComponents);

                // DockingManager requires BeginInit/EndInit for proper initialization
                _dockingManager.BeginInit();

                // Set both HostForm and HostControl - required for full DockingManager functionality
                _dockingManager.HostForm = this;
                _dockingManager.HostControl = this;

                // Configure DockingManager per Syncfusion demos (SDI, MDI, VisualStudioDemo)
                _dockingManager.ShowCaption = true;
                _dockingManager.EnableContextMenu = true;
                _dockingManager.PersistState = false; // We'll manually persist via XML
                _dockingManager.DockToFill = false; // Allow multiple dock areas with central workspace
                _dockingManager.ThemesEnabled = true;
                _dockingManager.HostFormClientBorder = true;
                _dockingManager.AnimateAutoHiddenWindow = true;
                _dockingManager.EnableDocumentMode = true; // Per demos: enables VS-style document tabs
                _dockingManager.ShowMetroCaptionDottedLines = false; // Per demos: cleaner caption appearance
                _dockingManager.AutoHideEnabled = true; // Per demos: enable auto-hide functionality
                _dockingManager.CloseTabOnMiddleClick = true; // Per demos: middle-click to close tabs

                // Configure CaptionButtons per Syncfusion demos (SDI, MDI demos pattern)
                // These buttons appear on the dock panel caption bar
                _dockingManager.CaptionButtons.Add(new Syncfusion.Windows.Forms.Tools.CaptionButton(
                    Syncfusion.Windows.Forms.Tools.CaptionButtonType.Close, "CloseButton"));
                _dockingManager.CaptionButtons.Add(new Syncfusion.Windows.Forms.Tools.CaptionButton(
                    Syncfusion.Windows.Forms.Tools.CaptionButtonType.Pin, "PinButton"));
                _dockingManager.CaptionButtons.Add(new Syncfusion.Windows.Forms.Tools.CaptionButton(
                    Syncfusion.Windows.Forms.Tools.CaptionButtonType.Menu, "MenuButton"));
                _dockingManager.CaptionButtons.Add(new Syncfusion.Windows.Forms.Tools.CaptionButton(
                    Syncfusion.Windows.Forms.Tools.CaptionButtonType.Maximize, "MaximizeButton"));
                _dockingManager.CaptionButtons.Add(new Syncfusion.Windows.Forms.Tools.CaptionButton(
                    Syncfusion.Windows.Forms.Tools.CaptionButtonType.Restore, "RestoreButton"));

                // Tab configuration per demos for better UX
                _dockingManager.AllowTabsMoving = true; // Allow users to reorder tabs by dragging
                _dockingManager.ShowDockTabScrollButton = true; // Show scroll buttons when tabs overflow
                _dockingManager.DockTabAlignment = DockTabAlignmentStyle.Bottom; // Tabs at bottom (VS-style)

                // Apply theme-aware visual style based on current app theme
                ApplyDockingManagerTheme();

                // Set modern drag provider style (VS2012 has nice appearance)
                _dockingManager.DragProviderStyle = DragProviderStyle.VS2012;

                // Set accessibility properties for DockingManager host
                try { _dockingManager.AccessibleName = "Main docking workspace"; _dockingManager.AccessibleDescription = "Docking manager that hosts app-wide panels like Accounts, Charts, and Settings"; } catch { }

                // Subscribe to dock state change events for lazy loading
                _dockingManager.DockStateChanged += DockingManager_DockStateChanged;
                _dockingManager.DockControlActivated += DockingManager_DockControlActivated;

                // Subscribe to visibility changed for proper cleanup when forms are closed
                _dockingManager.DockVisibilityChanged += DockingManager_DockVisibilityChanged;

                // Enable custom context menu on dock tabs (right-click tab → Close All, Close Others)
                _dockingManager.NewMenu = true;
                _dockingManager.DockTabContextMenu += DockingManager_DockTabContextMenu;

                // Complete initialization
                _dockingManager.EndInit();

                // Add docking manager under menu (menu is top docked item)
                // Ensure menu sits above docking manager
                if (_menuStrip != null)
                {
                    _menuStrip.Dock = DockStyle.Top;
                }

                Serilog.Log.Information("DockingManager initialized successfully with theme: {Theme}", ThemeManager.CurrentTheme);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to initialize DockingManager — falling back to non-docked layout");
            }
        }

        /// <summary>
        /// Apply the appropriate visual style to DockingManager based on the current theme.
        /// </summary>
        /// <summary>
        /// Apply the appropriate visual style to DockingManager based on the current theme.
        /// Configured per Syncfusion demos (SDI, NestedDocking, StatePersistence demos).
        /// </summary>
        private void ApplyDockingManagerTheme()
        {
            if (_dockingManager == null) return;

            try
            {
                // Map app theme to DockingManager VisualStyle per demos
                var visualStyle = ThemeManager.CurrentTheme switch
                {
                    AppTheme.Dark => Syncfusion.Windows.Forms.VisualStyle.Office2016Black,
                    AppTheme.Light => Syncfusion.Windows.Forms.VisualStyle.Office2019Colorful,
                    _ => Syncfusion.Windows.Forms.VisualStyle.Office2019Colorful
                };

                _dockingManager.VisualStyle = visualStyle;

                // Set matching drag provider style per demos (VS2012 is recommended)
                var dragStyle = ThemeManager.CurrentTheme switch
                {
                    AppTheme.Dark => DragProviderStyle.VS2012,
                    AppTheme.Light => DragProviderStyle.VS2012,
                    _ => DragProviderStyle.VS2012
                };

                _dockingManager.DragProviderStyle = dragStyle;

                // Apply theme-specific colors per demos (VisualStudioDemo)
                var colors = ThemeManager.Colors;
                _dockingManager.ActiveCaptionBackground = new Syncfusion.Drawing.BrushInfo(colors.Accent);
                _dockingManager.InActiveCaptionBackground = new Syncfusion.Drawing.BrushInfo(colors.SurfaceAlt);

                Serilog.Log.Debug("Applied DockingManager theme: VisualStyle={VisualStyle}, DragStyle={DragStyle}",
                    visualStyle, dragStyle);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to apply DockingManager theme");
            }
        }

        /// <summary>
        /// Handles dock state changes to trigger lazy data loading when panels become visible.
        /// </summary>
        private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
        {
            try
            {
                var ctrl = e.Controls?.Length > 0 ? e.Controls[0] : null;
                Serilog.Log.Debug("DockingManager_DockStateChanged fired for control {Control}", ctrl?.Name ?? ctrl?.GetType()?.Name ?? "(null)");
                if (ctrl == null) return;

                // Trigger lazy load when a panel becomes visible/docked
                if (ctrl is AccountsPanel ap && ap.DataContext is AccountsViewModel avm)
                {
                    Serilog.Log.Debug("DockStateChanged: loading accounts for {Panel}", ap.Name ?? "AccountsPanel");
                    avm.LoadAccountsCommand?.Execute(null);
                }
                else if (ctrl is DashboardPanel dp && dp.DataContext is WileyWidget.ViewModels.DashboardViewModel dvm)
                {
                    dvm.LoadDashboardCommand?.Execute(null);
                }
                else if (ctrl is ChartPanel cp && cp.DataContext is ChartViewModel cvm)
                {
                    Serilog.Log.Debug("DockStateChanged: loading chart for {Panel}", cp.Name ?? "ChartPanel");
                    _ = cvm.LoadChartDataAsync();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "DockStateChanged handler encountered an error");
            }
        }

        /// <summary>
        /// Handles dock control activation to trigger lazy data loading when panels become active.
        /// </summary>
        private void DockingManager_DockControlActivated(object? sender, DockActivationChangedEventArgs e)
        {
            try
            {
                var ctrl = e.Control;
                Serilog.Log.Debug("DockingManager_DockControlActivated fired for control {Control}", ctrl?.Name ?? ctrl?.GetType()?.Name ?? "(null)");
                if (ctrl == null) return;

                // Trigger lazy load when a panel becomes active
                if (ctrl is AccountsPanel ap && ap.DataContext is AccountsViewModel avm)
                {
                    Serilog.Log.Debug("DockControlActivated: loading accounts for {Panel}", ap.Name ?? "AccountsPanel");
                    avm.LoadAccountsCommand?.Execute(null);
                }
                else if (ctrl is DashboardPanel dp && dp.DataContext is WileyWidget.ViewModels.DashboardViewModel dvm)
                {
                    dvm.LoadDashboardCommand?.Execute(null);
                }
                else if (ctrl is ChartPanel cp && cp.DataContext is ChartViewModel cvm)
                {
                    Serilog.Log.Debug("DockControlActivated: loading chart for {Panel}", cp.Name ?? "ChartPanel");
                    _ = cvm.LoadChartDataAsync();
                }

                // Rebind the global status strip to the active panel's ViewModel (if available)
                try
                {
                    object? vm = null;
                    if (ctrl is AccountsPanel accountPanel) vm = accountPanel.DataContext;
                    else if (ctrl is DashboardPanel dashPanel) vm = dashPanel.DataContext;
                    else if (ctrl is ChartPanel chartPanel) vm = chartPanel.DataContext;

                    BindGlobalStatusTo(vm ?? _mainViewModel);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Failed to rebind MainForm global status to active panel");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "DockControlActivated handler encountered an error");
            }
        }

        /// <summary>
        /// Binds the global status strip to the provided view model (or the main view model when null).
        /// The binding uses StatusMessage for text and IsBusy / IsLoading for busy indicator.
        /// </summary>
        private void BindGlobalStatusTo(object? viewModel)
        {
            try
            {
                if (_globalStatusLabel == null || _globalProgressBar == null) return;

                // Clear existing bindings
                try { _globalStatusLabel.DataBindings.Clear(); } catch { }
                try { _globalProgressBar.DataBindings.Clear(); } catch { }

                if (viewModel == null)
                {
                    var bs = new BindingSource { DataSource = _mainViewModel };
                    _globalStatusLabel.DataBindings.Add("Text", bs, "StatusMessage", true, DataSourceUpdateMode.OnPropertyChanged);
                    _globalProgressBar.DataBindings.Add("Visible", bs, "IsLoading", true, DataSourceUpdateMode.OnPropertyChanged);
                    return;
                }

                var t = viewModel.GetType();
                var bsVm = new BindingSource { DataSource = viewModel };

                if (t.GetProperty("StatusMessage") != null)
                {
                    _globalStatusLabel.DataBindings.Add("Text", bsVm, "StatusMessage", true, DataSourceUpdateMode.OnPropertyChanged);
                }
                else
                {
                    // Fallback to main view model message
                    var bs = new BindingSource { DataSource = _mainViewModel };
                    _globalStatusLabel.DataBindings.Add("Text", bs, "StatusMessage", true, DataSourceUpdateMode.OnPropertyChanged);
                }

                if (t.GetProperty("IsBusy") != null)
                {
                    _globalProgressBar.DataBindings.Add("Visible", bsVm, "IsBusy", true, DataSourceUpdateMode.OnPropertyChanged);
                }
                else if (t.GetProperty("IsLoading") != null)
                {
                    _globalProgressBar.DataBindings.Add("Visible", bsVm, "IsLoading", true, DataSourceUpdateMode.OnPropertyChanged);
                }
                else
                {
                    var bs = new BindingSource { DataSource = _mainViewModel };
                    _globalProgressBar.DataBindings.Add("Visible", bs, "IsLoading", true, DataSourceUpdateMode.OnPropertyChanged);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BindGlobalStatusTo failed");
            }
        }

        /// <summary>
        /// Handle dock visibility changes - clean up when a control is closed via the X button.
        /// This is the recommended Syncfusion pattern for control lifecycle management.
        /// </summary>
        private void DockingManager_DockVisibilityChanged(object? sender, DockVisibilityChangedEventArgs e)
        {
            try
            {
                var ctrl = e.Control;
                if (ctrl == null || _dockingManager == null || IsDisposed) return;

                // Check if the control was closed (visibility = false)
                bool isVisible;
                try
                {
                    isVisible = _dockingManager.GetDockVisibility(ctrl);
                }
                catch (ObjectDisposedException)
                {
                    // DockingManager was disposed - treat as hidden
                    isVisible = false;
                }

                if (!isVisible)
                {
                    var panelName = ctrl.Name;
                    Serilog.Log.Debug("Dock panel '{Panel}' was closed, cleaning up resources", panelName);

                    // Save panel state before disposal
                    SavePanelStateForControl(ctrl);

                    // Ensure we disable docking first so DockingManager drops internal references
                    try
                    {
                        if (_dockingManager != null)
                        {
                            // Marshal to UI thread if necessary
                            if (ctrl != null && ctrl.InvokeRequired)
                            {
                                ctrl.Invoke((Action)(() =>
                                {
                                    try { _dockingManager.SetEnableDocking(ctrl, false); } catch { }
                                }));
                            }
                            else
                            {
                                try { _dockingManager.SetEnableDocking(ctrl, false); } catch { }
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // DockingManager already disposed - ignore
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Debug(ex, "Failed to disable docking for control {Panel} on close", panelName);
                    }

                    // Use the shared cleanup helper under lock to remove tracked entries and dispose scope
                    if (!string.IsNullOrEmpty(panelName))
                    {
                        lock (_dockedControlsLock)
                        {
                            try
                            {
                                CleanupDockedControl(panelName);
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Debug(ex, "CleanupDockedControl failed for {Panel}", panelName);
                            }
                        }
                    }

                    // If the closed panel was the active source of status information, fall back to the main VM
                    try { BindGlobalStatusTo(_mainViewModel); } catch { }
                }
            }
            catch (ObjectDisposedException)
            {
                // MainForm or DockingManager disposed during handler - ignore
                Serilog.Log.Debug("DockVisibilityChanged skipped - form or manager disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error in DockVisibilityChanged handler");
            }
        }

        /// <summary>
        /// Handles dock tab context menu (right-click on tab) to show Close All/Close Others options.
        /// </summary>
        private void DockingManager_DockTabContextMenu(object? sender, DockContextMenuEventArgs e)
        {
            try
            {
                if (e.ContextMenu == null || _dockingManager == null) return;

                e.ContextMenu.Items.Add("-"); // Separator

                var closeAllItem = new ToolStripMenuItem("Close All Panels", null, (s, args) => CloseAllPanels());
                closeAllItem.AccessibleName = "Close all panels";

                var closeOthersItem = new ToolStripMenuItem("Close Other Panels", null, (s, args) => CloseOtherPanels(e.Owner));
                closeOthersItem.AccessibleName = "Close other panels";

                e.ContextMenu.Items.Add(closeAllItem);
                e.ContextMenu.Items.Add(closeOthersItem);

                Serilog.Log.Debug("Added Close All/Close Others to dock tab context menu");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error adding dock tab context menu items");
            }
        }

        /// <summary>
        /// Closes all docked panels.
        /// </summary>
        public void CloseAllPanels()
        {
            try
            {
                if (_dockingManager == null) return;

                lock (_dockedControlsLock)
                {
                    foreach (var control in _dockedControls.Values.ToList())
                    {
                        if (control != null && !control.IsDisposed)
                        {
                            try
                            {
                                SavePanelStateForControl(control);
                                _dockingManager.SetDockVisibility(control, false);
                            }
                            catch { }
                        }
                    }
                }

                _panelStateManager.SavePanelState();
                Serilog.Log.Information("Closed all docked panels");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error closing all panels");
            }
        }

        /// <summary>
        /// Closes all docked panels except the specified one.
        /// </summary>
        public void CloseOtherPanels(Control? keepOpen)
        {
            try
            {
                if (_dockingManager == null) return;

                lock (_dockedControlsLock)
                {
                    foreach (var control in _dockedControls.Values.ToList())
                    {
                        if (control != null && control != keepOpen && !control.IsDisposed)
                        {
                            try
                            {
                                SavePanelStateForControl(control);
                                _dockingManager.SetDockVisibility(control, false);
                            }
                            catch { }
                        }
                    }
                }

                _panelStateManager.SavePanelState();
                Serilog.Log.Information("Closed all panels except {Panel}", keepOpen?.Name ?? "(none)");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error closing other panels");
            }
        }

        /// <summary>
        /// Closes the Settings panel. Called from SettingsPanel instead of reflection hack.
        /// </summary>
        public void CloseSettingsPanel()
        {
            ClosePanel(MainFormResources.SettingsMenu);
        }

        /// <summary>
        /// Closes a docked panel by name.
        /// </summary>
        public void ClosePanel(string panelName)
        {
            try
            {
                if (_dockingManager == null || string.IsNullOrEmpty(panelName)) return;

                lock (_dockedControlsLock)
                {
                    if (_dockedControls.TryGetValue(panelName, out var control) && control != null && !control.IsDisposed)
                    {
                        SavePanelStateForControl(control);
                        _dockingManager.SetDockVisibility(control, false);
                        Serilog.Log.Debug("Closed panel {Panel}", panelName);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error closing panel {Panel}", panelName);
            }
        }

        /// <summary>
        /// Saves panel state for a specific control before it's closed.
        /// </summary>
        private void SavePanelStateForControl(Control? control)
        {
            try
            {
                if (control is AccountsPanel ap)
                    _panelStateManager.SaveAccountsPanelState(ap);
                else if (control is ChartPanel cp)
                    _panelStateManager.SaveChartPanelState(cp);
                else if (control is DashboardPanel dp)
                    _panelStateManager.SaveDashboardPanelState(dp);
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "Error saving panel state for {Panel}", control?.Name);
            }
        }

        /// <summary>
        /// Loads panel state for a newly created control.
        /// </summary>
        private void LoadPanelStateForControl(Control? control)
        {
            try
            {
                if (control is AccountsPanel ap)
                    _panelStateManager.LoadAccountsPanelState(ap);
                else if (control is ChartPanel cp)
                    _panelStateManager.LoadChartPanelState(cp);
                else if (control is DashboardPanel dp)
                    _panelStateManager.LoadDashboardPanelState(dp);
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "Error loading panel state for {Panel}", control?.Name);
            }
        }

        /// <summary>
        /// Applies the current theme to this form's controls.
        /// </summary>
        private void ApplyCurrentTheme()
        {
            ThemeManager.ApplyTheme(this);
        }

        /// <summary>
        /// Handles theme change events. Reapplies theme to form and DockingManager.
        /// </summary>
        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            // Prefer centralized dispatcher when available
            if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
            {
                try { _ = _dispatcherHelper.InvokeAsync(() => OnThemeChanged(sender, theme)); } catch { }
                return;
            }
            if (InvokeRequired)
            {
                Invoke(() => OnThemeChanged(sender, theme));
                return;
            }

            ApplyCurrentTheme();

            // Update DockingManager visual style to match new theme
            ApplyDockingManagerTheme();
        }

        /// <summary>
        /// Handles the form Load event for initial data loading.
        /// </summary>
        private void MainForm_Load(object? sender, EventArgs e)
        {
            try
            {
                _mainViewModel?.LoadDataCommand.Execute(null);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm_Load: LoadDataCommand failed");
            }
        }

        /// <summary>
        /// Handles Accounts menu click with proper thread safety.
        /// </summary>
        private void AccountsMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => AccountsMenu_Click(sender, e)); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(() => AccountsMenu_Click(sender, e));
                    return;
                }

                Serilog.Log.Information("MainForm: Accounts menu clicked - showing Accounts panel");
                DockUserControlPanel<AccountsPanel>(MainFormResources.AccountsMenu);
                try { _mainViewModel?.LoadDataCommand.Execute(null); } catch (Exception ex) { Serilog.Log.Warning(ex, "MainForm: mainViewModel.LoadDataCommand.Execute failed after Accounts menu click"); }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("MainForm: AccountsMenu_Click - form was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: AccountsMenu_Click failed");
            }
        }

        /// <summary>
        /// Handles Dashboard menu click with proper thread safety.
        /// </summary>
        private void DashboardMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => DashboardMenu_Click(sender, e)); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(() => DashboardMenu_Click(sender, e));
                    return;
                }

                Serilog.Log.Information("MainForm: Dashboard menu clicked - activating dashboard");
                try { _openDashboardCommand?.Execute(null); } catch (Exception ex) { Serilog.Log.Warning(ex, "MainForm: openDashboardCommand failed"); }
                try { _mainViewModel?.LoadDataCommand.Execute(null); } catch (Exception ex) { Serilog.Log.Warning(ex, "MainForm: mainViewModel.LoadDataCommand.Execute failed after Dashboard menu click"); }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("MainForm: DashboardMenu_Click - form was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: DashboardMenu_Click failed");
            }
        }

        /// <summary>
        /// Handles Charts menu click with proper thread safety.
        /// </summary>
        private void ChartsMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => ChartsMenu_Click(sender, e)); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(() => ChartsMenu_Click(sender, e));
                    return;
                }

                Serilog.Log.Information("MainForm: Charts menu clicked - showing Charts panel");
                DockUserControlPanel<ChartPanel>(MainFormResources.ChartsMenu);
                try { _mainViewModel?.LoadDataCommand.Execute(null); } catch (Exception ex) { Serilog.Log.Warning(ex, "MainForm: mainViewModel.LoadDataCommand.Execute failed after Charts menu click"); }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("MainForm: ChartsMenu_Click - form was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: ChartsMenu_Click failed");
            }
        }

        /// <summary>
        /// Handles Settings menu click with proper thread safety.
        /// </summary>
        private void SettingsMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => SettingsMenu_Click(sender, e)); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(() => SettingsMenu_Click(sender, e));
                    return;
                }

                Serilog.Log.Information("MainForm: Settings menu clicked - opening Settings");
                DockUserControlPanel<SettingsPanel>(MainFormResources.SettingsMenu);
                try { _mainViewModel?.LoadDataCommand.Execute(null); } catch (Exception ex) { Serilog.Log.Warning(ex, "MainForm: mainViewModel.LoadDataCommand.Execute failed after Settings menu click"); }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("MainForm: SettingsMenu_Click - form was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: SettingsMenu_Click failed");
            }
        }

        /// <summary>
        /// Handles Reset Layout menu click - resets all dock panels to default positions.
        /// Per Syncfusion StatePersistence demo pattern.
        /// </summary>
        private void ResetLayout_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => ResetLayout_Click(sender, e)); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(() => ResetLayout_Click(sender, e));
                    return;
                }

                if (_dockingManager == null) return;

                // Close all docked panels
                lock (_dockedControlsLock)
                {
                    foreach (var control in _dockedControls.Values.ToList())
                    {
                        try
                        {
                            if (control != null && !control.IsDisposed)
                            {
                                _dockingManager.SetDockVisibility(control, false);
                            }
                        }
                        catch { }
                    }
                }

                // Reload default layout or open Dashboard as default view
                try
                {
                    _dockingManager.LoadDesignerDockState();
                }
                catch
                {
                    // Fallback: just open Dashboard
                    DockUserControlPanel<DashboardPanel>(MainFormResources.DashboardMenu);
                }

                Serilog.Log.Information("MainForm: Layout reset to defaults");
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("MainForm: ResetLayout_Click - form was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: ResetLayout_Click failed");
            }
        }

        /// <summary>
        /// Handles Exit menu click.
        /// </summary>
        private void ExitMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                Application.Exit();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "MainForm: ExitMenu_Click failed");
            }
        }

        /// <summary>
        /// Opens the Accounts panel via DockingManager or modal dialog fallback.
        /// </summary>
        private void menuAccounts_Click(object? sender, EventArgs e)
        {
            try
            {
                // If we have a docking manager available, use docked panel, otherwise fall back to modal
                if (_dockingManager != null)
                {
                    DockUserControlPanel<AccountsPanel>(MainFormResources.AccountsMenu);
                }

                // Trigger main view model load if present (MVVM: use command instead of ad-hoc calls)
                if (_dockingManager != null)
                {
                    try { _mainViewModel?.LoadDataCommand.Execute(null); } catch { }
                }
                else
                {
                    using var scope = Program.Services.CreateScope();
                    var provider = scope.ServiceProvider;
                    var accountsForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsForm>(provider);
                    accountsForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var reporting = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.ErrorReportingService>(Program.Services);
                    reporting?.ReportError(ex, "Failed to open Accounts form", showToUser: true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Opens the Settings panel via DockingManager or modal dialog fallback.
        /// </summary>
        private void menuSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_dockingManager != null)
                {
                    DockUserControlPanel<SettingsPanel>(MainFormResources.SettingsMenu);
                }
                else
                {
                    using var settingsForm = new SettingsForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services));
                    settingsForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open Settings form");
            }
        }

        /// <summary>
        /// Handles unhandled UI thread exceptions. Logs and displays error to user.
        /// </summary>
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

        /// <summary>
        /// Releases managed resources and unsubscribes from events.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe global exception handlers
                try { if (_threadExceptionHandler != null) Application.ThreadException -= _threadExceptionHandler; } catch { }
                try { if (_domainExceptionHandler != null) AppDomain.CurrentDomain.UnhandledException -= _domainExceptionHandler; } catch { }

                // Unsubscribe theme handlers
                try { ThemeManager.ThemeChanged -= OnThemeChanged; } catch { }
                try { WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged -= _menuIconsThemeChangedHandler; } catch { }

                // Unsubscribe menu click handlers
                try
                {
                    if (_menuStrip != null)
                    {
                        foreach (ToolStripItem item in _menuStrip.Items)
                        {
                            if (item is ToolStripMenuItem menuItem)
                            {
                                if (menuItem.Text == MainFormResources.AccountsMenu && _accountsMenuClickHandler != null)
                                    menuItem.Click -= _accountsMenuClickHandler;
                                else if (menuItem.Text == MainFormResources.DashboardMenu && _dashboardMenuClickHandler != null)
                                    menuItem.Click -= _dashboardMenuClickHandler;
                                else if (menuItem.Text == MainFormResources.ChartsMenu && _chartsMenuClickHandler != null)
                                    menuItem.Click -= _chartsMenuClickHandler;
                                else if (menuItem.Text == MainFormResources.SettingsMenu && _settingsMenuClickHandler != null)
                                    menuItem.Click -= _settingsMenuClickHandler;
                            }
                        }
                    }
                }
                catch { }

                // Unsubscribe load handler
                try { if (_loadHandler != null) this.Load -= _loadHandler; } catch { }

                // Dispose all tracked UserControls and their DI scopes safely.
                // Ensure DockingManager releases references before disposing controls to avoid
                // ObjectDisposedException / NullReferenceException from Syncfusion internals.
                try
                {
                    lock (_dockedControlsLock)
                    {
                        // Make a copy of keys so CleanupDockedControl can safely remove entries
                        var keys = _dockedControls.Keys.ToList();
                        foreach (var key in keys)
                        {
                            try
                            {
                                // CleanupDockedControl expects to be called while holding the lock.
                                CleanupDockedControl(key);
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Debug(ex, "Dispose: CleanupDockedControl failed for {Panel}", key);
                            }
                        }

                        _dockedControls.Clear();
                        _dockedControlScopes.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "Dispose: error while disposing docked controls");
                }

                _menuStrip?.Dispose();
                _globalStatusStrip?.Dispose();
                _dockingManager?.Dispose();
                _dockingManagerComponents?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Handles form closing event. Persists dock layout and panel states before closing.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Save all panel states before closing
            SaveAllPanelStates();
            _panelStateManager.SavePanelState();

            // Persist layout now (before we disable docking/dispose controls)
            SaveDockingLayout();

            // Safely disable docking for any live controls held by the DockingManager
            // and then dispose them. This avoids Syncfusion internal errors where the
            // DockingManager keeps references to disposed controls.
            try
            {
                if (_dockingManager != null)
                {
                    try
                    {
                        var controlsCopy = _dockingManager.Controls.OfType<Control>().ToList();
                        foreach (var ctrl in controlsCopy)
                        {
                            if (ctrl == null) continue;
                            try
                            {
                                try { _dockingManager.SetEnableDocking(ctrl, false); } catch { }
                            }
                            catch { }

                            try { if (!ctrl.IsDisposed) ctrl.Dispose(); } catch { }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // DockingManager disposed concurrently - ignore
                    }
                }

                // Also ensure tracked panels and scopes are cleaned up
                try
                {
                    lock (_dockedControlsLock)
                    {
                        var keys = _dockedControls.Keys.ToList();
                        foreach (var key in keys)
                        {
                            try { CleanupDockedControl(key); } catch { }
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "Error during OnFormClosing docked-control cleanup");
            }

            base.OnFormClosing(e);
        }

        /// <summary>
        /// Saves state for all currently docked panels.
        /// </summary>
        private void SaveAllPanelStates()
        {
            try
            {
                lock (_dockedControlsLock)
                {
                    foreach (var control in _dockedControls.Values)
                    {
                        SavePanelStateForControl(control);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error saving all panel states");
            }
        }

        /// <summary>
        /// Persists the current dock layout to XML file for session restoration.
        /// Uses Task.Run to avoid blocking the UI thread during file I/O.
        /// </summary>
        private void SaveDockingLayout()
        {
            if (_dockingManager != null)
            {
                try
                {
                    // Capture state on UI thread, then persist on background thread
                    var serializer = new AppStateSerializer(SerializeMode.XMLFile, LayoutFile);
                    _dockingManager.SaveDockState(serializer);

                    // Use Task.Run to avoid blocking UI during file I/O
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            serializer.PersistNow();
                            Serilog.Log.Debug("Docking layout saved to {LayoutFile}", LayoutFile);
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Warning(ex, "Failed to persist docking layout");
                        }
                    }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Failed to save docking layout");
                }
            }
        }

        /// <summary>
        /// Restores dock layout from XML file if available.
        /// </summary>
        private void LoadDockingLayout()
        {
            if (_dockingManager == null) return;

            // Check if layout file exists
            var layoutPath = Path.Combine(AppContext.BaseDirectory, LayoutFile + ".xml");
            if (!File.Exists(layoutPath))
            {
                Serilog.Log.Debug("No docking layout file found at {LayoutPath}", layoutPath);
                return;
            }

            try
            {
                var serializer = new AppStateSerializer(SerializeMode.XMLFile, LayoutFile);
                _dockingManager.LoadDockState(serializer);
                Serilog.Log.Debug("Docking layout loaded from {LayoutFile}", LayoutFile);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to load docking layout");
            }
        }

        /// <summary>
        /// Creates or shows a docked panel for a transient UserControl type using DI.
        /// Manages scope lifecycle and docking integration.
        /// Uses DockingStyle.Tabbed for document-style tabbing (following Syncfusion demos).
        /// </summary>
        /// <typeparam name="TPanel">The UserControl panel type to dock.</typeparam>
        /// <param name="panelName">The display name for the docked panel.</param>
        private void DockUserControlPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            // Guard against calling DockUserControlPanel on a disposed form
            if (IsDisposed)
            {
                Serilog.Log.Debug("DockUserControlPanel called after MainForm disposed - ignoring");
                return;
            }

            if (_dockingManager == null)
            {
                // Fallback to adding as a simple control since no docking available
                Serilog.Log.Debug("DockUserControlPanel: docking manager not available — adding panel as child control for {Panel}", panelName);
                using var modalScope = Program.Services.CreateScope();
                var modalProvider = modalScope.ServiceProvider;
                var fallbackPanel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TPanel>(modalProvider);
                fallbackPanel.Dock = DockStyle.Fill;
                Controls.Add(fallbackPanel);
                return;
            }

            // If already created and tracked, bring to front. Be defensive: guard against disposed objects.
            lock (_dockedControlsLock)
            {
                if (_dockedControls.TryGetValue(panelName, out var existing) && existing != null)
                {
                    try
                    {
                        if (!existing.IsDisposed)
                        {
                            // Make visible again if it was hidden, and activate it (VS-style)
                            _dockingManager.SetDockVisibility(existing, true);
                            _dockingManager.ActivateControl(existing);
                            Serilog.Log.Debug("Activated existing panel {Panel}", panelName);
                            return;
                        }
                        else
                        {
                            // Clean up stale entry if control was disposed
                            CleanupDockedControl(panelName);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Another thread disposed the control — ensure cleanup
                        CleanupDockedControl(panelName);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Debug(ex, "Failed to reactivate existing panel {Panel}", panelName);
                    }
                }
            }

            // Create new docked panel
            try
            {
                // Create scope but DO NOT use 'using' - scope must stay alive for the panel's lifetime
                // The scope will be disposed in DockVisibilityChanged when the panel is closed
                var scope = Program.Services.CreateScope();
                Serilog.Log.Debug("DockUserControlPanel: created DI scope for panel {Panel}", panelName);
                var provider = scope.ServiceProvider;
                var panel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TPanel>(provider);

                // Track the scope under lock immediately to prevent leaks if docking fails
                lock (_dockedControlsLock)
                {
                    _dockedControlScopes[panelName] = scope;
                }

                // Configure panel for docking - UserControls work naturally
                panel.Name = panelName;
                panel.Dock = DockStyle.Fill;

                // Enable docking for the UserControl
                try
                {
                    // If a Form slipped into our generic UserControl flow, ensure it's prepared for docking
                    if (panel is Form asForm)
                    {
                        try
                        {
                            // Call PrepareForDocking when available on known forms (best-effort)
                            var prep = asForm.GetType().GetMethod("PrepareForDocking");
                            if (prep != null)
                            {
                                try { prep.Invoke(asForm, null); } catch { }
                            }

                            // Ensure common properties are set so Syncfusion can host it
                            try
                            {
                                if (asForm.TopLevel) asForm.TopLevel = false;
                                asForm.FormBorderStyle = FormBorderStyle.None;
                                asForm.Visible = false;
                                asForm.Dock = DockStyle.Fill;
                            }
                            catch { }
                        }
                        catch { }
                    }

                    // If we somehow resolved a disposed instance, try to recreate once
                    if (panel != null && panel.IsDisposed)
                    {
                        Serilog.Log.Warning("DockUserControlPanel: resolved panel instance is already disposed for {Panel}; recreating", panelName);
                        try { scope.Dispose(); } catch { }
                        scope = Program.Services.CreateScope();
                        provider = scope.ServiceProvider;
                        panel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TPanel>(provider);
                        lock (_dockedControlsLock)
                        {
                            _dockedControlScopes[panelName] = scope;
                        }
                    }

                    _dockingManager.SetEnableDocking(panel, true);

                    // Clean up any disposed entries proactively
                    lock (_dockedControlsLock)
                    {
                        var disposedKeys = _dockedControls.Where(kvp => kvp.Value == null || kvp.Value.IsDisposed).Select(kvp => kvp.Key).ToList();
                        foreach (var k in disposedKeys)
                        {
                            _dockedControls.Remove(k);
                            if (_dockedControlScopes.TryGetValue(k, out var s))
                            {
                                _dockedControlScopes.Remove(k);
                                try { s.Dispose(); } catch { }
                            }
                        }
                    }

                    // Find a valid (non-disposed) existing control to tab with
                    Control? existingControl = null;
                    try
                    {
                        lock (_dockedControlsLock)
                        {
                            existingControl = _dockedControls.Values.FirstOrDefault(c =>
                            {
                                if (c == null) return false;
                                try
                                {
                                    if (c.IsDisposed) return false;
                                    return _dockingManager.GetEnableDocking(c);
                                }
                                catch (ObjectDisposedException)
                                {
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    Serilog.Log.Debug(ex, "Error while checking docking eligibility for a control");
                                    return false;
                                }
                            });
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Concurrent disposal — continue without an existing control
                        existingControl = null;
                    }

                    // Determine dock style based on panel type (per Syncfusion demos)
                    // Dashboard and Charts are main content - use DockAsDocument for VS-style document tabs
                    // Accounts and Settings are tool windows - use tabbed/docked style
                    bool isDocumentPanel = panel is DashboardPanel || panel is ChartPanel;

                    if (isDocumentPanel)
                    {
                        // Use DockAsDocument for main content panels (VS-style document tabs)
                        try
                        {
                            _dockingManager.DockAsDocument(panel);
                        }
                        catch (Exception docEx)
                        {
                            Serilog.Log.Debug(docEx, "DockAsDocument failed, falling back to safer DockControl");

                            // Defensive guard: don't dock with Fill into the DockingManager's host control (this)
                            // as Syncfusion can throw when Fill is applied to the host control itself.
                            try
                            {
                                if (panel == null || panel.IsDisposed || this.IsDisposed)
                                {
                                    Serilog.Log.Warning("Panel {Panel} or host disposed before fallback docking", panelName);
                                }
                                else
                                {
                                    // Prefer Tabbed docking as a safer alternative to Fill onto the host.
                                    _dockingManager.DockControl(panel, this, DockingStyle.Tabbed, 600);
                                }
                            }
                            catch (Exception innerEx)
                            {
                                Serilog.Log.Debug(innerEx, "Fallback DockControl(Tabbed) also failed; will fall back to adding as standard control");
                                throw;
                            }
                        }
                    }
                    else if (existingControl != null)
                    {
                        // Tab with an existing docked control (DockingStyle.Tabbed creates VS-style document tabs)
                        try
                        {
                            _dockingManager.DockControl(panel, existingControl, DockingStyle.Tabbed, 600);
                        }
                        catch (ObjectDisposedException)
                        {
                            // Existing control got disposed concurrently — dock to left side
                            _dockingManager.DockControl(panel, this, DockingStyle.Left, 600);
                        }
                    }
                    else
                    {
                        // First tool panel - dock to left (tool window pattern from Syncfusion demos)
                        _dockingManager.DockControl(panel, this, DockingStyle.Left, 600);
                    }

                    // Set the caption label for UX - Syncfusion uses this for tab/window titles
                    _dockingManager.SetDockLabel(panel, panelName);

                    // Set dock icon per Syncfusion demos (MDI, SDI)
                    try
                    {
                        var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                        if (iconService != null)
                        {
                            var iconName = panel switch
                            {
                                AccountsPanel => "accounts",
                                DashboardPanel => "dashboard",
                                ChartPanel => "chart",
                                SettingsPanel => "settings",
                                _ => "document"
                            };
                            var icon = iconService.GetIcon(iconName, ThemeManager.CurrentTheme, 16);
                            if (icon != null)
                            {
                                _dockingManager.SetDockIcon(panel, icon);
                            }
                        }
                    }
                    catch (Exception iconEx)
                    {
                        Serilog.Log.Debug(iconEx, "Failed to set dock icon for {Panel}", panelName);
                    }

                    // Subscribe to Disposed event for cleanup when panel is disposed
                    panel.Disposed += (s, args) =>
                    {
                        var disposedPanelName = (s as Control)?.Name;
                        if (!string.IsNullOrEmpty(disposedPanelName))
                        {
                            lock (_dockedControlsLock)
                            {
                                CleanupDockedControl(disposedPanelName);
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    // If docking fails, capture richer diagnostics then fall back to adding as a regular control
                    try
                    {
                        Serilog.Log.Warning(ex, "DockControl failed for {Panel}, falling back to standard Controls.Add. PanelType={PanelType} IsDisposed={IsDisposed} IsHandleCreated={IsHandleCreated}",
                            panelName,
                            panel?.GetType().FullName ?? "<null>",
                            panel?.IsDisposed,
                            panel?.IsHandleCreated);

                        // Best-effort: write a small diagnostic file that test harness can pick up
                        try
                        {
                            var diagDir = Path.Combine(AppContext.BaseDirectory, "logs");
                            if (!Directory.Exists(diagDir)) Directory.CreateDirectory(diagDir);
                            var diagPath = Path.Combine(diagDir, $"dock_diag_{panelName}_{DateTime.UtcNow:yyyyMMddTHHmmss}.txt");
                            var txt = $"PanelName: {panelName}\nPanelType: {panel?.GetType().FullName ?? "<null>"}\nIsDisposed: {panel?.IsDisposed}\nIsHandleCreated: {panel?.IsHandleCreated}\nException: {ex}\n";
                            File.WriteAllText(diagPath, txt);
                        }
                        catch { }
                    }
                    catch { }

                    try
                    {
                        if (panel == null || panel.IsDisposed)
                        {
                            Serilog.Log.Warning("Panel {Panel} was null or disposed before fallback could add it", panelName);
                            return;
                        }

                        try
                        {
                            panel.Dock = DockStyle.Fill;
                            Controls.Add(panel);
                            panel.BringToFront();
                        }
                        catch (ObjectDisposedException ode)
                        {
                            Serilog.Log.Error(ode, "Fallback add failed: panel was disposed during Controls.Add/BringToFront for {Panel}", panelName);
                            return;
                        }

                        // Subscribe to Disposed for proper cleanup even in fallback mode
                        panel.Disposed += (s, args) =>
                        {
                            var disposedPanelName = (s as Control)?.Name;
                            if (!string.IsNullOrEmpty(disposedPanelName))
                            {
                                lock (_dockedControlsLock)
                                {
                                    CleanupDockedControl(disposedPanelName);
                                }
                            }
                        };
                    }
                    catch (Exception fallbackEx)
                    {
                        Serilog.Log.Warning(fallbackEx, "Fallback add of panel {Panel} failed", panelName);
                    }
                }

                // Track the successfully created panel (whether docked or fallback)
                Serilog.Log.Information("DockUserControlPanel: successfully docked panel {Panel}", panelName);
                lock (_dockedControlsLock)
                {
                    _dockedControls[panelName] = panel;
                }

                // Apply theme to embedded panel
                ThemeManager.ApplyTheme(panel);

                // Restore panel state (filters, grid state, etc.)
                LoadPanelStateForControl(panel);

                // Trigger initial load for viewmodels where helpful
                try
                {
                    if (panel is AccountsPanel ap && ap.DataContext is AccountsViewModel avm)
                    {
                        avm.LoadAccountsCommand?.Execute(null);
                    }
                    else if (panel is ChartPanel cp && cp.DataContext is ChartViewModel cvm)
                    {
                        _ = cvm.LoadChartDataAsync();
                    }
                    else if (panel is DashboardPanel dp && dp.DataContext is WileyWidget.ViewModels.DashboardViewModel dvm)
                    {
                        dvm.LoadDashboardCommand?.Execute(null);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to dock {Panel}", panelName);
                MessageBox.Show($"Error docking {panelName}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Helper method to clean up a docked control entry and its DI scope.
        /// Must be called within a lock on _dockedControlsLock.
        /// IMPORTANT: Disposes the control FIRST (while services are available), then the DI scope.
        /// </summary>
        /// <param name="panelName">The panel name key to clean up.</param>
        private void CleanupDockedControl(string panelName)
        {
            // 1. Dispose the control first while DI services are still available
            if (_dockedControls.TryGetValue(panelName, out var control))
            {
                _dockedControls.Remove(panelName);
                if (control != null)
                {
                    try
                    {
                        // Ensure DockingManager releases references before disposing the control.
                        try
                        {
                            if (_dockingManager != null)
                            {
                                // If the control lives on a different thread, marshal disabling onto UI thread
                                if (control.InvokeRequired)
                                {
                                    control.Invoke((Action)(() =>
                                    {
                                        try { _dockingManager.SetEnableDocking(control, false); } catch { }
                                    }));
                                }
                                else
                                {
                                    try { _dockingManager.SetEnableDocking(control, false); } catch { }
                                }
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // DockingManager was disposed concurrently; ignore
                        }

                        // Dispose safely on the control's owning thread
                        if (!control.IsDisposed)
                        {
                            if (control.InvokeRequired)
                            {
                                control.Invoke((Action)(() =>
                                {
                                    try { if (!control.IsDisposed) control.Dispose(); }
                                    catch (Exception ex)
                                    {
                                        Serilog.Log.Debug(ex, "Error disposing control '{Panel}' on UI thread", panelName);
                                    }
                                }));
                            }
                            else
                            {
                                try { control.Dispose(); }
                                catch (Exception ex)
                                {
                                    Serilog.Log.Debug(ex, "Error disposing control '{Panel}'", panelName);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Debug(ex, "Error during cleanup dispose of control '{Panel}'", panelName);
                    }
                }
            }

            // 2. Then dispose the DI scope
            if (_dockedControlScopes.TryGetValue(panelName, out var scope))
            {
                _dockedControlScopes.Remove(panelName);
                try { scope.Dispose(); }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "Error disposing DI scope for '{Panel}'", panelName);
                }
            }
        }

        /// <summary>
        /// Docks an AccountEditForm as a floating tool window.
        /// Called when "Open edit forms docked" setting is enabled.
        /// </summary>
        /// <param name="editForm">The AccountEditForm to dock.</param>
        public void DockAccountEditForm(AccountEditForm editForm)
        {
            if (_dockingManager == null || editForm == null)
            {
                Serilog.Log.Debug("DockAccountEditForm: docking manager or form is null");
                editForm?.ShowDialog();
                return;
            }

            try
            {
                const string panelName = "Account Edit";

                // Check if already docked
                lock (_dockedControlsLock)
                {
                    if (_dockedControls.TryGetValue(panelName, out var existing) && existing != null && !existing.IsDisposed)
                    {
                        _dockingManager.SetDockVisibility(existing, true);
                        _dockingManager.ActivateControl(existing);
                        Serilog.Log.Debug("Activated existing AccountEditForm");
                        return;
                    }
                }

                // Prepare for docking (ensure form is in child-control state)
                try
                {
                    editForm.PrepareForDocking();
                }
                catch { }

                // Common safety: ensure properties
                try
                {
                    if (editForm.TopLevel) editForm.TopLevel = false;
                    editForm.FormBorderStyle = FormBorderStyle.None;
                    editForm.Visible = false;
                    editForm.Dock = DockStyle.Fill;
                }
                catch { }

                editForm.Name = panelName;

                // Guard: ensure not disposed
                if (editForm.IsDisposed)
                {
                    Serilog.Log.Warning("DockAccountEditForm: supplied editForm is already disposed");
                    return;
                }

                // Enable docking
                _dockingManager.SetEnableDocking(editForm, true);

                // Dock as floating window (tool window style)
                try
                {
                    _dockingManager.DockControl(editForm, this, DockingStyle.Float, 500);
                }
                catch (Exception dEx)
                {
                    Serilog.Log.Warning(dEx, "DockControl failed for AccountEditForm; attempting safe fallback");
                    try
                    {
                        if (editForm == null || editForm.IsDisposed)
                        {
                            Serilog.Log.Warning("AccountEditForm was null or disposed during DockControl fallback");
                            return;
                        }
                        editForm.Dock = DockStyle.Fill;
                        Controls.Add(editForm);
                    }
                    catch (ObjectDisposedException ode)
                    {
                        Serilog.Log.Error(ode, "Fallback add failed: AccountEditForm disposed during Controls.Add");
                        return;
                    }
                }
                _dockingManager.SetDockLabel(editForm, panelName);

                // Set icon
                try
                {
                    var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                    var icon = iconService?.GetIcon("edit", ThemeManager.CurrentTheme, 16);
                    if (icon != null)
                    {
                        _dockingManager.SetDockIcon(editForm, icon);
                    }
                }
                catch { }

                // Track the form
                lock (_dockedControlsLock)
                {
                    _dockedControls[panelName] = editForm;
                }

                // Cleanup on disposal
                editForm.Disposed += (s, args) =>
                {
                    lock (_dockedControlsLock)
                    {
                        _dockedControls.Remove(panelName);
                    }
                };

                ThemeManager.ApplyTheme(editForm);
                Serilog.Log.Information("Docked AccountEditForm as floating tool window");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to dock AccountEditForm, showing as modal");
                editForm.ShowDialog();
            }
        }

        /// <summary>
        /// Handles unhandled domain exceptions. Logs fatal errors and notifies user if terminating.
        /// </summary>
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
