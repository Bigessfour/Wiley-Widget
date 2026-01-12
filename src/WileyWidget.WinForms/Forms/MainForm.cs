using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.Dialogs;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Forms;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Abstractions;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

#pragma warning disable CS8604 // Possible null reference argument

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Resource strings used by <see cref="MainForm"/> for labels, titles and navigation text.
    /// </summary>
    internal static class MainFormResources
    {
        /// <summary>Professional window title with product branding.</summary>
        public const string FormTitle = "Wiley Widget - Municipal Budget Management System";
        /// <summary>Application version for About dialog and diagnostic purposes.</summary>
        public const string ApplicationVersion = "1.0.0";

        /// <summary>Application description for About dialog.</summary>
        public const string ApplicationDescription = "A comprehensive municipal budget management system built on .NET 9 and Windows Forms.";

        public const string Dashboard = "Dashboard";
        public const string Accounts = "Accounts";
        public const string Charts = "Charts";
        public const string Reports = "Reports";
        public const string Settings = "Settings";
        public const string Docking = "Docking";
        public const string LoadingText = "Loading...";
    }

    /// <summary>
    /// Main application window. Hosts navigation chrome, Syncfusion docking manager, and provides
    /// access to application-level services and panel navigation helpers used by child controls.
    /// </summary>
    /// <remarks>
    /// Initialization sequence: <see cref="InitializeChrome"/>,
    /// <see cref="InitializeSyncfusionDocking"/>, then deferred ViewModel initialization in <see cref="OnShown(EventArgs)"/>.
    /// Dispose will clean up scoped services and UI resources.
    /// </remarks>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : SfForm, IAsyncInitializable
    {
        private static int _inFirstChanceHandler = 0;
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _mainViewModelScope;  // Scope for MainViewModel - kept alive for form lifetime
        private IPanelNavigationService? _panelNavigator;
        private DockingLayoutManager? _dockingLayoutManager; // Layout persistence manager

        private const int WS_EX_COMPOSITED = 0x02000000;

        /// <summary>
        /// Enables flicker reduction via WS_EX_COMPOSITED for heavy UI chrome (Ribbon + Docking).
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;

                try
                {
                    if (_uiConfig != null && !_uiConfig.IsUiTestHarness)
                    {
                        cp.ExStyle |= WS_EX_COMPOSITED;
                    }
                }
                catch
                {
                    // Best-effort only; never block handle creation.
                }

                return cp;
            }
        }

        /// <summary>
        /// The root <see cref="IServiceProvider"/> for the application. Child forms and controls
        /// should use this provider to resolve services. Throws <see cref="InvalidOperationException"/>
        /// if the provider has not been initialized during startup.
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

        /// <summary>
        /// Returns the active <see cref="DockingManager"/> instance used by the form. Throws
        /// <see cref="InvalidOperationException"/> if docking has not been initialized.
        /// </summary>
        public DockingManager GetDockingManager() => _dockingManager ?? throw new InvalidOperationException("DockingManager not initialized");

        // All content hosted in DockingManager-controlled left/right panels

        /// <summary>
        /// Shows or activates a docked panel. Creates it if not already present.
        /// Delegates to PanelNavigationService for centralized panel management.
        /// </summary>
        /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
        /// <param name="panelName">Optional panel name. If null, uses type name.</param>
        /// <param name="preferredStyle">Preferred docking position (default: Right).</param>
        /// <param name="allowFloating">If true, panel can be floated by user (default: true).</param>
        public void ShowPanel<TPanel>(
            string? panelName = null,
            DockingStyle preferredStyle = DockingStyle.Right,
            bool allowFloating = true)
            where TPanel : UserControl
        {
            if (_panelNavigator == null)
            {
                _logger?.LogWarning("Cannot show panel - PanelNavigationService not initialized");
                return;
            }

            var displayName = panelName ?? typeof(TPanel).Name;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                _logger?.LogWarning("ShowPanel called with invalid panel name");
                return;
            }

            _panelNavigator.ShowPanel<TPanel>(displayName, preferredStyle, allowFloating);

            // Force z-order after showing panel to prevent overlap
            try { EnsureDockingZOrder(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to ensure docking z-order after ShowPanel"); }
        }

        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        private readonly Services.IThemeIconService? _iconService;
        private readonly ReportViewerLaunchOptions _reportViewerLaunchOptions;
        private MenuStrip? _menuStrip;
        private RibbonControlAdv? _ribbon;
        private bool _reportViewerLaunched;
        private ToolStripTabItem? _homeTab;
        private ToolStripEx? _navigationStrip;
        private StatusBarAdv? _statusBar;
        private StatusBarAdvPanel? _statusLabel;
        private StatusBarAdvPanel? _statusTextPanel;
        private StatusBarAdvPanel? _statePanel;
        private StatusBarAdvPanel? _progressPanel;
        private Syncfusion.Windows.Forms.Tools.ProgressBarAdv? _progressBar;
        private StatusBarAdvPanel? _clockPanel;
        private System.Windows.Forms.Timer? _statusTimer;
        private bool _dashboardAutoShown;
        private System.Windows.Forms.Timer? _activityRefreshTimer;

        /// <summary>
        /// Internal helper for StatusBarFactory to wire panel references.
        /// Called from StatusBarFactory.CreateStatusBar to establish back-references.
        /// </summary>
        internal void SetStatusBarPanels(
            StatusBarAdv statusBar,
            StatusBarAdvPanel statusLabel,
            StatusBarAdvPanel statusTextPanel,
            StatusBarAdvPanel statePanel,
            StatusBarAdvPanel progressPanel,
            Syncfusion.Windows.Forms.Tools.ProgressBarAdv progressBar,
            StatusBarAdvPanel clockPanel)
        {
            _statusBar = statusBar;
            _statusLabel = statusLabel;
            _statusTextPanel = statusTextPanel;
            _statePanel = statePanel;
            _progressPanel = progressPanel;
            _progressBar = progressBar;
            _clockPanel = clockPanel;
        }
        private bool _syncfusionDockingInitialized;
        private bool _initialized;
        internal System.ComponentModel.IContainer? components;
        private UIConfiguration _uiConfig = null!;
        private int _onShownExecuted = 0;
        private CancellationTokenSource? _initializationCts;
        private Serilog.ILogger? _asyncLogger;
        // Dashboard description labels are declared in docking partial

        public MainForm()
            : this(
                GetDefaultServiceProvider(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(GetDefaultServiceProvider()) ?? new ConfigurationBuilder().Build(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(GetDefaultServiceProvider()) ?? NullLogger<MainForm>.Instance,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ReportViewerLaunchOptions>(GetDefaultServiceProvider()) ?? ReportViewerLaunchOptions.Disabled,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(GetDefaultServiceProvider()))
        {
        }

        private static IServiceProvider GetDefaultServiceProvider()
        {
            return Program.Services ?? WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection(includeDefaults: true).BuildServiceProvider();
        }

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger, ReportViewerLaunchOptions reportViewerLaunchOptions, Services.IThemeIconService? iconService = null)
        {
            Log.Debug("[DIAGNOSTIC] MainForm constructor: ENTERED");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] MainForm constructor: ENTERED");

            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;
            _panelNavigator = null;
            _iconService = iconService;

            // CRITICAL FIX: Initialize components container in constructor
            // This ensures components is available when DockingManager is created
            // Per Syncfusion best practices: DockingManager requires a valid IContainer
            components = new System.ComponentModel.Container();
            _logger.LogDebug("MainForm constructor: Components container initialized");

            // Initialize centralized UI configuration
            _uiConfig = UIConfiguration.FromConfiguration(configuration);

            // Apply global Syncfusion theme before any child controls are created
            AppThemeColors.ApplyTheme(this);

            if (!_uiConfig.IsUiTestHarness)
            {
                // Reduce startup flicker for heavy Syncfusion UI (best-effort).
                try
                {
                    SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
                    DoubleBuffered = true;
                    UpdateStyles();
                    _logger?.LogDebug("Enabled double-buffering / optimized painting on MainForm");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to enable double-buffering / optimized painting on MainForm");
                }
            }

            _logger.LogInformation("UI Architecture: {Architecture}", _uiConfig.GetArchitectureDescription());

            IsMdiContainer = false;

            // TEMP DISABLE: Drag-drop fails on non-STA (logs FTL). Re-enable after MainForm shows.
            // AllowDrop = true;
            // DragEnter += MainForm_DragEnter;
            // DragDrop += MainForm_DragDrop;

            // Set reasonable form size constraints to prevent unusable states
            // Use settings from UIConfiguration and avoid touching the real Desktop in test harness mode
            this.MinimumSize = _uiConfig.MinimumFormSize;
            if (_uiConfig.IsUiTestHarness)
            {
                // In test harness, avoid referencing Screen.PrimaryScreen which may require a real desktop
                this.MaximumSize = new Size(1920, 1080);
            }
            else
            {
                var screenWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 1920;
                var screenHeight = Screen.PrimaryScreen?.WorkingArea.Height ?? 1080;
                this.MaximumSize = new Size(screenWidth, screenHeight);
            }

            // Theme already applied globally in Program.InitializeTheme() via SkinManager.ApplicationVisualTheme
            // No need to set ThemeName here - it cascades automatically to all controls

            // DEFERRED: Chrome initialization happens in OnShown so the form can be
            // constructed quickly before Application.Run(mainForm) starts the UI loop.

            // Add FirstChanceException handlers for comprehensive error logging
            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;

            // Subscribe to font changes for real-time updates
            Services.FontService.Instance.FontChanged += OnApplicationFontChanged;

            Log.Debug("[DIAGNOSTIC] MainForm constructor: COMPLETED");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] MainForm constructor: COMPLETED");
        }

        /// <summary>
        /// Implements <see cref="IAsyncInitializable.InitializeAsync(CancellationToken)"/>.
        /// Performs async initialization work after the MainForm is shown.
        /// Currently a no-op as synchronous initialization is sufficient.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            // Heavy async initialization can be placed here if needed in future
            await Task.CompletedTask;
        }

        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                {
                    e.Effect = DragDropEffects.Copy;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in DragEnter handler");
            }
        }

        private void MainForm_Activated(object? sender, EventArgs e)
        {
            _asyncLogger?.Debug("MainForm activated - thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);

            // Track activation event (only first time for startup timeline)
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            if (timelineService != null && timelineService.CurrentPhase != null)
            {
                timelineService.RecordFormLifecycleEvent("MainForm", "Activated");
                // Unsubscribe after first activation
                Activated -= MainForm_Activated;
                _asyncLogger?.Information("MainForm activation recorded and event unsubscribed");
            }
        }

        private async void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    _logger?.LogInformation("Dropped {Count} file(s): {Files}", files.Length, string.Join(", ", files));
                    await ProcessDroppedFiles(files);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing dropped files");
                MessageBox.Show($"Error processing files: {ex.Message}", "Drag-Drop Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ProcessDroppedFiles(string[] files)
        {
            if (files == null || files.Length == 0)
            {
                _logger?.LogWarning("No files provided to ProcessDroppedFiles");
                return;
            }

            _asyncLogger?.Information("Processing {Count} dropped files", files.Length);

            foreach (var file in files)
            {
                try
                {
                    // Validate file
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        _logger?.LogWarning("Empty file path in dropped files");
                        continue;
                    }

                    if (!File.Exists(file))
                    {
                        ShowErrorDialog("File Not Found", $"The file '{Path.GetFileName(file)}' does not exist.");
                        continue;
                    }

                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length > 100 * 1024 * 1024) // 100MB limit
                    {
                        ShowErrorDialog("File Too Large", $"The file '{Path.GetFileName(file)}' is too large ({fileInfo.Length / 1024 / 1024}MB). Maximum size is 100MB.");
                        continue;
                    }

                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    _asyncLogger?.Debug("Processing dropped file: {File} (ext: {Ext})", file, ext);
                    _logger?.LogInformation("Processing dropped file: {File} (ext: {Ext})", file, ext);

                    // Add to MRU
                    AddToMruList(file);

                    // Handle based on file type
                    if (ext == ".csv" || ext == ".xlsx" || ext == ".xls")
                    {
                        await ImportDataFileAsync(file);
                    }
                    else if (ext == ".json" || ext == ".xml")
                    {
                        await ImportConfigurationDataAsync(file);
                    }
                    else
                    {
                        ShowErrorDialog("Unsupported File Type", $"Unsupported file type: {ext}\n\nSupported: CSV, XLSX, XLS, JSON, XML");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to process dropped file: {File}", file);
                    ShowErrorDialog("File Processing Error", $"Failed to process '{Path.GetFileName(file)}': {ex.Message}", ex);
                }
            }

            await Task.CompletedTask;
        }

        private void MainForm_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;
            if (ex == null) return;

            // Prevent recursive re-entry into this handler (which can happen if logging throws)
            if (System.Threading.Interlocked.Exchange(ref _inFirstChanceHandler, 1) == 1)
                return;

            try
            {
                // Use a local logger reference to avoid potential property race conditions
                var logger = _logger;

                try
                {
                    // Log theme-related exceptions
                    if (ex.Source?.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) == true ||
                        ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Office2019", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("SkinManager", StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogDebug(ex, "First-chance theme exception detected: {Message}", ex.Message);
                    }

                    // Log docking-related exceptions
                    if (ex.Source?.Contains("DockingManager", StringComparison.OrdinalIgnoreCase) == true ||
                        ex.Message.Contains("dock", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("DockingManager", StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogDebug(ex, "First-chance docking exception detected: {Message}", ex.Message);
                    }

                    // Avoid doing anything expensive in a first-chance handler.
                }
                catch (Exception logEx)
                {
                    // Swallow logging exceptions to prevent recursive loops in exception handler
                    System.Diagnostics.Debug.WriteLine($"Exception in FirstChanceException handler: {logEx.Message}");
                }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _inFirstChanceHandler, 0);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Track form lifecycle event for startup timeline analysis
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            timelineService?.RecordFormLifecycleEvent("MainForm", "Load");

            // CRITICAL: Theme is already cascaded from Program.InitializeTheme() via ApplicationVisualTheme
            // No need to set theme again - it cascades automatically to all controls
            try
            {
                var themeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                Log.Information("[THEME] MainForm.OnLoad: Theme inherited from global ApplicationVisualTheme - {ThemeName}", themeName);
                timelineService?.RecordFormLifecycleEvent("MainForm", "OnLoad: Theme Verified");
            }
            catch (Exception themeEx)
            {
                Log.Warning(themeEx, "[THEME] MainForm.OnLoad: Deferred theme verification failed - cascade from global ApplicationVisualTheme will be used");
            }

            // Designer short-circuit
            if (DesignMode)
            {
                IsMdiContainer = false;
                return;
            }

            if (_initialized)
                return;

            // Lazy-init services when needed
            if (_serviceProvider == null)
                _serviceProvider = Program.Services ?? new ServiceCollection().BuildServiceProvider();

            if (_configuration == null)
                _configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(_serviceProvider) ?? new ConfigurationBuilder().Build();

            if (_logger == null)
                _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(_serviceProvider) ?? NullLogger<MainForm>.Instance;

            _initialized = true;

            // Load MRU list from registry
            LoadMruFromRegistry();

            // Restore window state (size, position, maximized/minimized) from previous session
            RestoreWindowState();

            // CRITICAL: Initialize UI Chrome and Docking synchronously on UI thread BEFORE OnShown
            // Heavy initialization (Chrome, Docking) must complete before form is shown to prevent rendering issues
            _logger?.LogDebug("OnLoad: Starting UI initialization on UI thread {ThreadId}", Thread.CurrentThread.ManagedThreadId);

            try
            {
                // Phase 1: Initialize Chrome (Ribbon, StatusBar, MenuBar) - must run on UI thread
                _logger?.LogDebug("OnLoad: Initializing UI chrome");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] OnLoad: About to call InitializeChrome()");
                InitializeChrome();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] OnLoad: InitializeChrome() completed - Controls.Count={Controls.Count}");
                _logger?.LogInformation("OnLoad: Chrome initialization completed - added {ControlCount} controls", Controls.Count);

                // Verify controls were actually added
                if (_ribbon != null) Console.WriteLine($"[DIAGNOSTIC] Ribbon exists: Size={_ribbon.Size}, Visible={_ribbon.Visible}, Parent={_ribbon.Parent?.Name}");
                if (_statusBar != null) Console.WriteLine($"[DIAGNOSTIC] StatusBar exists: Size={_statusBar.Size}, Visible={_statusBar.Visible}, Parent={_statusBar.Parent?.Name}");
                if (_menuStrip != null) Console.WriteLine($"[DIAGNOSTIC] MenuStrip exists: Size={_menuStrip.Size}, Visible={_menuStrip.Visible}, Parent={_menuStrip.Parent?.Name}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OnLoad: InitializeChrome failed");
                Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeChrome failed: {ex.Message}");
                Console.WriteLine($"[DIAGNOSTIC ERROR] Stack: {ex.StackTrace}");
                // Continue - chrome initialization failure is non-critical
            }

            try
            {
                // Phase 2: Initialize Syncfusion Docking - must run on UI thread
                if (!_syncfusionDockingInitialized)
                {
                    _logger?.LogDebug("OnLoad: Initializing Syncfusion docking");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] OnLoad: About to call InitializeSyncfusionDocking()");
                    InitializeSyncfusionDocking();
                    _syncfusionDockingInitialized = true;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] OnLoad: InitializeSyncfusionDocking() completed - _dockingManager={_dockingManager != null}");
                    _logger?.LogInformation("OnLoad: Docking initialization completed");

                    // Initialize panel navigator after docking is ready
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] OnLoad: About to call EnsurePanelNavigatorInitialized() - _dockingManager={_dockingManager != null}");
                    EnsurePanelNavigatorInitialized();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] OnLoad: EnsurePanelNavigatorInitialized() completed - _panelNavigator={_panelNavigator != null}");

                    // Auto-show dashboard if configured
                    if (_uiConfig.AutoShowDashboard && !_dashboardAutoShown && _panelNavigator != null)
                    {
                        _panelNavigator.ShowPanel<Controls.DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                        _dashboardAutoShown = true;
                        _logger?.LogDebug("OnLoad: Auto-showed Dashboard panel");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OnLoad: Failed to initialize docking manager");
                Console.WriteLine($"[DIAGNOSTIC ERROR] InitializeSyncfusionDocking failed: {ex.Message}");
                Console.WriteLine($"[DIAGNOSTIC ERROR] Stack: {ex.StackTrace}");
                // Continue - docking initialization failure is non-critical
            }

            try
            {
                // Phase 3: Ensure docking z-order
                if (_uiConfig.UseSyncfusionDocking)
                {
                    EnsureDockingZOrder();
                    _logger?.LogDebug("OnLoad: Docking z-order ensured");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OnLoad: Failed to ensure docking z-order");
            }

            // CRITICAL: Verify docking state after potential LoadDockState
            // If layout load failed or panels are not visible, force default docking
            if (_uiConfig.UseSyncfusionDocking && _dockingManager != null)
            {
                try
                {
                    var dockedControlCount = 0;
                    foreach (Control ctrl in this.Controls)
                    {
                        if (ctrl is Panel panel && panel.Visible)
                        {
                            try
                            {
                                if (_dockingManager.GetEnableDocking(panel))
                                {
                                    dockedControlCount++;
                                }
                            }
                            catch { /* Skip non-docked panels */ }
                        }
                    }

                    _logger?.LogDebug("OnLoad: Post-LoadDockState verification - {DockedCount} docked panels found", dockedControlCount);
                    Console.WriteLine($"[DIAGNOSTIC] OnLoad: Post-LoadDockState verification - {dockedControlCount} docked panels");

                    // If no docked panels found, default docking will occur naturally
                    // REMOVED: Blocking LoadLayoutAsync().GetAwaiter().GetResult() call that caused 21-second UI freeze
                    // The fallback was loading a non-existent temp file and blocking the UI thread unnecessarily
                    if (dockedControlCount == 0)
                    {
                        _logger?.LogDebug("OnLoad: No docked panels found - default docking will occur naturally");
                        Console.WriteLine("[DIAGNOSTIC] OnLoad: No docked panels found - default docking will occur naturally");
                    }
                }
                catch (Exception verifyEx)
                {
                    _logger?.LogWarning(verifyEx, "OnLoad: Failed to verify docking state post-load");
                }
            }

            UpdateDockingStateText();

            // Z-order management: Keep chrome on top, but AFTER docking manager setup
            // CRITICAL: Ribbon and StatusBar should be on top of docking panels
            // But DON'T call BringToFront before docking is initialized - it can hide panels
            try
            {
                // Let DockingManager settle first, then bring chrome to front
                if (_dockingManager != null && _dockingManager.HostControl != null)
                {
                    // Send DockingManager's host control to back so chrome stays on top
                    if (_dockingManager.HostControl is Control hostControl)
                    {
                        hostControl.SendToBack();
                        _logger?.LogDebug("Docking host control sent to back");
                    }
                }

                if (_ribbon != null)
                {
                    _ribbon.BringToFront();
                    _logger?.LogDebug("Ribbon brought to front");
                }

                if (_statusBar != null)
                {
                    _statusBar.BringToFront();
                    _logger?.LogDebug("Status bar brought to front");
                }

                // Force repaint to show docking panels
                Refresh();
                Invalidate();
                _logger?.LogInformation("OnLoad: UI initialization and z-order management completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OnLoad failed during z-order configuration");
                throw;
            }

            // Finalize form sizing and layout after chrome/docking init
            try
            {
                if (!_uiConfig.IsUiTestHarness)
                {
                    // Ensure we meet configured defaults, then maximize for best use of space
                    this.Size = new Size(
                        Math.Max(this.Width, _uiConfig.DefaultFormSize.Width),
                        Math.Max(this.Height, _uiConfig.DefaultFormSize.Height));

                    this.StartPosition = FormStartPosition.CenterScreen;
                    this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                }

                Refresh();
                _ribbon?.PerformLayout();
            }
            catch (Exception sizingEx)
            {
                _logger?.LogDebug(sizingEx, "OnLoad: post-init sizing/layout failed");
            }

            // Panel navigation is created after docking is initialized (OnShown).

            _logger?.LogInformation("MainForm startup completed successfully");
        }

        /// <summary>
        /// REMOVED: WndProc BackStage exception handling.
        /// The root cause (BackStage initializing before ribbon.ResumeLayout) has been fixed.
        /// BackStage now initializes AFTER ribbon layout completes, so renderer properties exist when paint occurs.
        /// See RibbonFactory.CreateRibbon for proper initialization order.
        /// </summary>
        // Previous WndProc workaround removed - no longer needed after architectural fix

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Ctrl+F: Focus global search box
            if (keyData == (Keys.Control | Keys.F))
            {
                try
                {
                    if (_ribbon != null)
                    {
                        var searchBox = FindToolStripItem(_ribbon, "GlobalSearch") as ToolStripTextBox;
                        if (searchBox != null)
                        {
                            searchBox.Focus();
                            searchBox.SelectAll();
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error focusing search box");
                }
            }

            // Ctrl+Shift+T: Toggle theme
            if (keyData == (Keys.Control | Keys.Shift | Keys.T))
            {
                try
                {
                    if (_ribbon != null)
                    {
                        var themeToggle = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                        themeToggle?.PerformClick();
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error toggling theme");
                }
            }

            // TIER 3: Keyboard Navigation Support
            // Alt+Left/Right/Up/Down: Navigate between docked panels
            if ((keyData & Keys.Alt) == Keys.Alt &&
                (keyData & (Keys.Left | Keys.Right | Keys.Up | Keys.Down)) != 0)
            {
                // Keyboard navigation will be handled by DockingKeyboardNavigator when integrated
                _logger?.LogDebug("Keyboard navigation shortcut triggered: {KeyData}", keyData);
                return true;
            }

            // TIER 2: Panel Navigation Shortcuts (already implemented earlier)
            // Alt+A: Show Accounts panel
            if (keyData == (Keys.Alt | Keys.A))
            {
                try
                {
                    _panelNavigator?.ShowPanel<Controls.AccountsPanel>("Accounts", DockingStyle.Right, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error showing Accounts panel");
                }
            }

            // Alt+B: Show Budget panel
            if (keyData == (Keys.Alt | Keys.B))
            {
                try
                {
                    _panelNavigator?.ShowPanel<Controls.BudgetPanel>("Budget", DockingStyle.Right, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error showing Budget panel");
                }
            }

            // Alt+C: Show Charts panel
            if (keyData == (Keys.Alt | Keys.C))
            {
                try
                {
                    _panelNavigator?.ShowPanel<Controls.ChartPanel>("Charts", DockingStyle.Right, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error showing Charts panel");
                }
            }

            // Alt+D: Show Dashboard panel
            if (keyData == (Keys.Alt | Keys.D))
            {
                try
                {
                    _panelNavigator?.ShowPanel<Controls.DashboardPanel>("Dashboard", DockingStyle.Top, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error showing Dashboard panel");
                }
            }

            // Alt+R: Show Reports panel
            if (keyData == (Keys.Alt | Keys.R))
            {
                try
                {
                    _panelNavigator?.ShowPanel<Controls.ReportsPanel>("Reports", DockingStyle.Right, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error showing Reports panel");
                }
            }

            // Alt+S: Show Settings panel
            if (keyData == (Keys.Alt | Keys.S))
            {
                try
                {
                    _panelNavigator?.ShowPanel<Controls.SettingsPanel>("Settings", DockingStyle.Right, true);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error showing Settings panel");
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private ToolStripItem? FindToolStripItem(RibbonControlAdv ribbon, string name)
        {
            foreach (ToolStripTabItem tab in ribbon.Header.MainItems)
            {
                if (tab.Panel != null)
                {
                    foreach (var panel in tab.Panel.Controls.OfType<ToolStripEx>())
                    {
                        var item = panel.Items.Find(name, searchAllChildren: true).FirstOrDefault();
                        if (item != null) return item;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// <summary>
        /// OnShown override. Deferred initialization for non-critical background operations.
        /// Heavy UI initialization (Chrome, Docking) is now performed in OnLoad.
        /// This method handles background tasks (health checks, test data seeding) and async ViewModel initialization.
        /// </summary>
        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (DesignMode)
                return;

            // Thread-safe guard: Prevent duplicate execution
            if (Interlocked.Exchange(ref _onShownExecuted, 1) != 0)
            {
                _logger?.LogWarning("OnShown called multiple times - ignoring duplicate call");
                return;
            }

            // Create cancellation token source for initialization operations
            _initializationCts = new CancellationTokenSource();
            var cancellationToken = _initializationCts.Token;

            // Apply final theme safe point after initial paint (already on UI thread in OnShown)
            try
            {
                SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                var config = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(_serviceProvider ?? Program.Services!);
                var themeName = config["UI:Theme"] ?? "Office2019Colorful";
                SkinManager.ApplicationVisualTheme = themeName;
                _logger?.LogDebug("Global Syncfusion theme applied after first paint: {ThemeName}", themeName);
            }
            catch (Exception themeEx)
            {
                _logger?.LogWarning(themeEx, "Deferred theme initialization failed - continuing with current styling");
            }

            // Database health check + test data seeding (background)
            var healthCheckTask = Task.Run(async () =>
            {
                try
                {
                    if (_serviceProvider == null) return;
                    using var scope = _serviceProvider.CreateScope();
                    await Program.RunStartupHealthCheckAsync(scope.ServiceProvider).ConfigureAwait(false);
                    _logger?.LogInformation("Deferred startup health check completed");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Deferred startup health check failed (non-fatal)");
                }
            });
            Program.RegisterBackgroundTask(healthCheckTask);

            // Initialize Grok service asynchronously (deferred initialization pattern)
            var grokTask = Task.Run(async () =>
            {
                try
                {
                    if (_serviceProvider == null) return;
                    var grokService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetService<WileyWidget.WinForms.Services.AI.GrokAgentService>(_serviceProvider);
                    if (grokService is IAsyncInitializable asyncInitializable)
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        cts.CancelAfter(TimeSpan.FromSeconds(30));
                        await asyncInitializable.InitializeAsync(cts.Token).ConfigureAwait(false);
                        _logger?.LogInformation("Grok service initialized asynchronously on background thread");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to initialize Grok service asynchronously (non-critical - chat will function via HTTP fallback)");
                }
            });
            Program.RegisterBackgroundTask(grokTask);

            var seedTask = Task.Run(async () =>
            {
                try
                {
                    if (_serviceProvider == null) return;
                    await UiTestDataSeeder.SeedIfEnabledAsync(_serviceProvider).ConfigureAwait(false);
                    _logger?.LogDebug("Deferred test data seeding completed successfully");
                }
                catch (Exception seedEx)
                {
                    _logger?.LogWarning(seedEx, "Deferred test data seeding failed (non-critical)");
                }
            });
            Program.RegisterBackgroundTask(seedTask);

            // Track form shown event for startup timeline analysis
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            timelineService?.RecordFormLifecycleEvent("MainForm", "Shown");

            // Initialize async logging for MainForm diagnostics to avoid blocking UI thread
            try
            {
                // CRITICAL: Use SAME centralized logs directory as main logger
                // This ensures ALL application logs are in one location
                var projectRoot = Directory.GetCurrentDirectory();
                var logsDirectory = Path.Combine(projectRoot, "logs");
                Directory.CreateDirectory(logsDirectory);
                var asyncLogPath = Path.Combine(logsDirectory, "mainform-diagnostics-.log");
                _asyncLogger = new LoggerConfiguration()
                    .WriteTo.Async(a => a.File(asyncLogPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        formatProvider: CultureInfo.InvariantCulture))
                    .Enrich.FromLogContext()
                    .MinimumLevel.Debug()
                    .CreateLogger();

                _asyncLogger.Information("✓ Async diagnostics logger initialized - path: {LogPath}", asyncLogPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize async logging for MainForm - falling back to main logger");
            }

            // Wire Activated event for tracking (only once)
            Activated += MainForm_Activated;

            _asyncLogger?.Information("MainForm OnShown: Starting deferred initialization - UI thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);

            try
            {
                _logger?.LogInformation("OnShown: Starting deferred background initialization");

                // Call MainForm's own InitializeAsync to defer heavy data loading
                _ = InitializeAsync(cancellationToken).ConfigureAwait(false);

                ApplyStatus("Ready");
                _logger?.LogInformation("OnShown: Deferred initialization completed");

                // CRITICAL: Late validation pass to catch any images that became invalid after initial load
                // This prevents GDI+ "Parameter is not valid" crashes in ImageAnimator during paint
                try
                {
                    _logger?.LogDebug("OnShown: Running late image validation pass");
                    LateValidateMenuBarImages();
                    LateValidateRibbonImages();
                    _logger?.LogDebug("OnShown: Late image validation completed");
                }
                catch (Exception validationEx)
                {
                    _logger?.LogError(validationEx, "OnShown: Late image validation failed");
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("OnShown initialization cancelled");
                ApplyStatus("Initialization cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during OnShown deferred initialization");
                ApplyStatus("Initialization error");

                // Critical error - show user-friendly message
                if (this.IsHandleCreated)
                {
                    try
                    {
                        MessageBox.Show(this,
                            $"An unexpected error occurred during initialization: {ex.Message}\n\nPlease check the logs for details.",
                            "Critical Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    catch { /* Swallow MessageBox errors */ }
                }
            }
        }

        private void EnsurePanelNavigatorInitialized()
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: _panelNavigator={_panelNavigator != null}, _serviceProvider={_serviceProvider != null}, _dockingManager={_dockingManager != null}");

                if (_panelNavigator != null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: Already initialized, returning");
                    return;
                }

                if (_serviceProvider == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: _serviceProvider is null, returning");
                    return;
                }

                if (_dockingManager == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: _dockingManager is null, returning");
                    _logger?.LogWarning("Cannot initialize PanelNavigationService - DockingManager not yet created");
                    return;
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: Creating PanelNavigationService...");

                // Note: Central panel is now managed by DockingLayoutManager
                // Pass MainForm as parent control for panel hosting
                var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ILogger<PanelNavigationService>>(_serviceProvider) ?? NullLogger<PanelNavigationService>.Instance;

                _panelNavigator = new PanelNavigationService(_dockingManager, this, _serviceProvider, navLogger);
                _logger?.LogDebug("PanelNavigationService created after docking initialization");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: PanelNavigationService created successfully");

                // Enable navigation buttons after PanelNavigator is ready
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: About to call EnableNavigationButtons()");
                EnableNavigationButtons();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnsurePanelNavigatorInitialized: EnableNavigationButtons() completed");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize PanelNavigationService");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC ERROR] EnsurePanelNavigatorInitialized failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Enables navigation buttons after PanelNavigationService is initialized.
        /// Called from EnsurePanelNavigatorInitialized to ensure proper timing.
        /// </summary>
        private void EnableNavigationButtons()
        {
            try
            {
                int enabledCount = 0;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: _ribbon={_ribbon != null}, tabs={_ribbon?.Header.MainItems.Count ?? 0}");

                // Enable ribbon navigation buttons
                if (_ribbon != null)
                {
                    // Navigate through ribbon structure: Header.MainItems → ToolStripTabItem → Panel → ToolStripEx (via AddToolStrip)
                    foreach (ToolStripTabItem tab in _ribbon.Header.MainItems)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: Checking tab '{tab.Text}', Panel={tab.Panel != null}");

                        if (tab.Panel != null)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: Panel.Controls.Count = {tab.Panel.Controls.Count}");

                            foreach (Control control in tab.Panel.Controls)
                            {
                                if (control is ToolStripEx toolStrip)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: ToolStripEx '{toolStrip.Name}' has {toolStrip.Items.Count} items");

                                    foreach (ToolStripItem item in toolStrip.Items)
                                    {
                                        // Check direct buttons
                                        if (item is ToolStripButton btn && btn.Name.StartsWith("Nav_", StringComparison.Ordinal))
                                        {
                                            btn.Enabled = true;
                                            enabledCount++;
                                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: Enabled '{btn.Name}' ('{btn.Text}')");
                                            _logger?.LogDebug("Enabled navigation button: {ButtonName}", btn.Name);
                                        }
                                        // Check buttons inside ToolStripPanelItem containers
                                        else if (item is ToolStripPanelItem panel)
                                        {
                                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: Found ToolStripPanelItem '{panel.Text}' with {panel.Items.Count} items");
                                            foreach (ToolStripItem panelItem in panel.Items)
                                            {
                                                if (panelItem is ToolStripButton panelBtn && panelBtn.Name.StartsWith("Nav_", StringComparison.Ordinal))
                                                {
                                                    panelBtn.Enabled = true;
                                                    enabledCount++;
                                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: Enabled panel button '{panelBtn.Name}' ('{panelBtn.Text}')");
                                                    _logger?.LogDebug("Enabled navigation button in panel: {ButtonName}", panelBtn.Name);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger?.LogWarning("EnableNavigationButtons: _ribbon is null");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] EnableNavigationButtons: _ribbon is null!");
                }

                // Enable navigation strip buttons
                if (_navigationStrip != null)
                {
                    foreach (ToolStripItem item in _navigationStrip.Items)
                    {
                        if (item is ToolStripButton btn && btn.Name.StartsWith("Nav_", StringComparison.Ordinal))
                        {
                            btn.Enabled = true;
                            enabledCount++;
                            _logger?.LogDebug("Enabled navigation strip button: {ButtonName}", btn.Name);
                        }
                    }
                }

                _logger?.LogInformation("Navigation buttons enabled: {Count} total", enabledCount);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to enable navigation buttons");
            }
        }



        private void TryLaunchReportViewerOnLoad()
        {
            if (_reportViewerLaunchOptions == null || !_reportViewerLaunchOptions.ShowReportViewer)
            {
                return;
            }

            if (_reportViewerLaunched)
            {
                _logger?.LogDebug("Report viewer launch already handled for {ReportPath}", _reportViewerLaunchOptions.ReportPath);
                return;
            }

            var reportPath = _reportViewerLaunchOptions.ReportPath;
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                _logger?.LogWarning("Report viewer launch requested but no report path was supplied");
                return;
            }

            if (!File.Exists(reportPath))
            {
                _logger?.LogWarning("Report viewer launch requested but report file was missing: {ReportPath}", reportPath);
                return;
            }

            try
            {
                ShowReportsPanel(reportPath);
                _reportViewerLaunched = true;
                _logger?.LogInformation("Report viewer opened for CLI path: {ReportPath}", reportPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open report viewer for {ReportPath}", reportPath);
            }
        }

        private void ShowReportsPanel(string reportPath)
        {
            try
            {
                _panelNavigator.ShowPanel<Controls.ReportsPanel>("Reports", reportPath, DockingStyle.Right, allowFloating: true);
                _logger?.LogInformation("Reports panel shown with auto-load path: {ReportPath}", reportPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show reports panel");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                // Request cancellation for any ongoing initialization operations
                // Note: We request cancellation but don't force abort - operations can complete gracefully
                try
                {
                    _initializationCts?.Cancel();
                    _logger?.LogDebug("Form closing: cancellation requested for initialization operations");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Exception cancelling initialization during form closing (expected if already completed)");
                }

                // CRITICAL: Stop timers BEFORE disposal to prevent disposed service provider access
                try
                {
                    _statusTimer?.Stop();
                    _activityRefreshTimer?.Stop();
                    _logger?.LogDebug("Form closing: timers stopped");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Exception stopping timers during form closing");
                }

                // Give brief time for async operations to complete cancellation (max 500ms)
                // This prevents ObjectDisposedException from DbContext disposal mid-operation
                if (_initializationCts?.Token.IsCancellationRequested == true)
                {
                    try
                    {
                        System.Threading.Thread.Sleep(500);
                        _logger?.LogDebug("Form closing: waited 500ms for async operations to handle cancellation");
                    }
                    catch { /* Timing not critical */ }
                }

                // Save docking layout and dispose docking resources
                if (_dockingManager != null && _dockingLayoutManager != null)
                {
                    try
                    {
                        _dockingLayoutManager.SaveLayout(_dockingManager, GetDockingLayoutPath());
                        _logger?.LogDebug("Form closing: docking layout saved");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to save docking layout during form closing");
                    }
                }

                // Save window state (size, position, maximized/minimized) for next session
                try
                {
                    SaveWindowState();
                    _logger?.LogDebug("Form closing: window state saved");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to save window state during form closing");
                }

                // Phase 1 Simplification: Dispose docking resources
                DisposeSyncfusionDockingResources();
            }
            finally
            {
                base.OnFormClosing(e);
            }
        }

        /// <summary>
        /// Apply the configured theme from UIConfiguration.
        /// Phase 1: Hard-coded to Office2019Colorful.
        /// NOTE: Theme is inherited from Program.InitializeTheme() which sets ApplicationVisualTheme globally.
        /// No need to call SetVisualStyle here - it cascades automatically from the global setting.
        /// </summary>
        private void ApplyTheme()
        {
            // REMOVED: Setting ThemeName conflicts with global SkinManager theming
            // this.ThemeName = _uiConfig.DefaultTheme;
            // Theme inherited from ApplicationVisualTheme set in Program.InitializeTheme()
            // SkinManager.SetVisualStyle(this, _uiConfig.DefaultTheme); // REMOVED: Redundant with global theme
            _logger?.LogDebug("Theme inherited from ApplicationVisualTheme: {Theme}", _uiConfig.DefaultTheme);
        }

        /// <summary>
        /// Thread-safe helper to update the status text panel from any thread.
        /// Automatically marshals to UI thread if needed.
        /// </summary>
        /// <param name="text">Status text to display.</param>
        private void ApplyStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                if (this.IsHandleCreated && this.InvokeRequired)
                {
                    try { this.BeginInvoke(new System.Action(() => ApplyStatus(text))); } catch { }
                    return;
                }
            }
            catch { }

            try
            {
                if (_statusTextPanel != null && !_statusTextPanel.IsDisposed)
                {
                    _statusTextPanel.Text = text;
                    return;
                }

                if (_statusLabel != null && !_statusLabel.IsDisposed)
                {
                    _statusLabel.Text = text;
                    return;
                }
            }
            catch { }
        }
    }
}

/// <summary>
/// Shows a user-friendly error dialog with retry option.
/// Thread-safe: can be called from any thread.
/// </summary>
/// <param name="title">Dialog title.</param>
/// <param name="message">Error message.</param>
/// <param name="exception">Optional exception for logging.</param>
/// <param name="showRetry">If true, shows retry button.</param>
/// <returns>DialogResult indicating user choice.</returns>
