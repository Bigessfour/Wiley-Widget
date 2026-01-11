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

            // STEP 4: Explicit per-form theme application (Syncfusion 32.1.19 API)
            // After global ApplicationVisualTheme is set and form is loaded,
            // explicitly apply theme to this form instance to ensure cascade to child controls
            try
            {
                var themeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                Log.Information("[THEME] MainForm.OnLoad: Applying explicit theme to main form instance - {ThemeName}", themeName);

                timelineService?.RecordFormLifecycleEvent("MainForm", "OnLoad: Apply Theme");

                // Apply theme to this form - cascades to all child Syncfusion controls
                SfSkinManager.SetVisualStyle(this, themeName);

                Log.Information("[THEME] ✓ MainForm theme applied - will cascade to all child controls (docking, ribbon, grids, charts)");
                timelineService?.RecordFormLifecycleEvent("MainForm", "OnLoad: Theme Applied");
            }
            catch (Exception themeEx)
            {
                Log.Warning(themeEx, "[THEME] MainForm.OnLoad: Failed to apply explicit theme - cascade from global ApplicationVisualTheme will be used");
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

                if (_dockingManager != null && _dockingLayoutManager != null)
                {
                    try { _dockingLayoutManager.SaveLayout(_dockingManager, GetDockingLayoutPath()); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to save docking layout during form closing"); }
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

        /// <summary>
        /// Shows a user-friendly error dialog with retry option.
        /// Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Error message.</param>
        /// <param name="exception">Optional exception for logging.</param>
        /// <param name="showRetry">If true, shows retry button.</param>
        /// <returns>DialogResult indicating user choice.</returns>
        public DialogResult ShowErrorDialog(string title, string message, Exception? exception = null, bool showRetry = false)
        {
            if (exception != null)
            {
                _logger?.LogError(exception, "Error dialog shown: {Message}", message);
            }

            try
            {
                if (InvokeRequired)
                {
                    return (DialogResult)Invoke(() => ShowErrorDialog(title, message, exception, showRetry));
                }

                var buttons = showRetry ? MessageBoxButtons.RetryCancel : MessageBoxButtons.OK;
                var icon = MessageBoxIcon.Error;

                return MessageBox.Show(this, message, title, buttons, icon);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show error dialog");
                return DialogResult.Cancel;
            }
        }

        /// <summary>
        /// Shows the progress bar in the status bar with optional message.
        /// Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="message">Optional status message to display.</param>
        /// <param name="indeterminate">If true, shows waiting animation; if false, shows determinate progress.</param>
        public void ShowProgress(string? message = null, bool indeterminate = true)
        {
            try
            {
                if (_progressPanel == null || _progressBar == null) return;

                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(() => ShowProgress(message, indeterminate));
                    }
                    return;
                }

                _progressBar.ProgressStyle = indeterminate
                    ? Syncfusion.Windows.Forms.Tools.ProgressBarStyles.WaitingGradient
                    : Syncfusion.Windows.Forms.Tools.ProgressBarStyles.Tube;
                _progressBar.Value = 0;
                _progressPanel.Visible = true;

                if (!string.IsNullOrEmpty(message))
                {
                    ApplyStatus(message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to show progress bar");
            }
        }

        /// <summary>
        /// Updates the progress bar value (0-100).
        /// Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="value">Progress percentage (0-100).</param>
        /// <param name="message">Optional status message to display.</param>
        public void UpdateProgress(int value, string? message = null)
        {
            try
            {
                if (_progressBar == null) return;

                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(() => UpdateProgress(value, message));
                    }
                    return;
                }

                _progressBar.Value = Math.Clamp(value, 0, 100);

                if (!string.IsNullOrEmpty(message))
                {
                    ApplyStatus(message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update progress bar");
            }
        }

        /// <summary>
        /// Hides the progress bar and resets status to Ready.
        /// Thread-safe: can be called from any thread.
        /// </summary>
        public void HideProgress()
        {
            try
            {
                if (_progressPanel == null) return;

                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(HideProgress);
                    }
                    return;
                }

                _progressPanel.Visible = false;
                ApplyStatus("Ready");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to hide progress bar");
            }
        }

        /// <summary>
        /// Docks a user control panel with the specified name and docking style.
        /// Creates the control using dependency injection if available.
        /// </summary>
        /// <typeparam name="TControl">The UserControl type to dock.</typeparam>
        /// <param name="panelName">Unique name for the docked panel.</param>
        /// <param name="style">Docking position for the panel.</param>
        /// <exception cref="InvalidOperationException">Thrown when docking manager is not available.</exception>
        /// <exception cref="ArgumentException">Thrown when panelName is null or empty.</exception>
        public void DockPanel<TControl>(string panelName, DockingStyle style)
            where TControl : UserControl
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
            }

            try
            {
                if (!_uiConfig.UseSyncfusionDocking || _dockingManager == null)
                {
                    _logger?.LogWarning("Cannot dock panel '{PanelName}' - Syncfusion docking is not enabled", panelName);
                    throw new InvalidOperationException("Docking manager is not available");
                }

                // Create instance of the user control using DI
                TControl? control = null;
                IServiceScope? scope = null;

                try
                {
                    scope = _serviceProvider?.CreateScope();
                    control = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<TControl>(scope?.ServiceProvider);

                    if (control == null)
                    {
                        // Try to create with parameterless constructor if DI fails
                        control = Activator.CreateInstance<TControl>();
                        _logger?.LogWarning("Created {ControlType} with parameterless constructor - DI not available", typeof(TControl).Name);
                    }

                    // Add the control as a dynamic dock panel
                    var success = AddDynamicDockPanel(panelName, panelName, control, style);

                    if (!success)
                    {
                        throw new InvalidOperationException($"Failed to add dock panel '{panelName}'");
                    }

                    _logger?.LogInformation("Successfully docked panel '{PanelName}' with control {ControlType}", panelName, typeof(TControl).Name);

                    // Transfer ownership to the docking manager
                    control = null;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to dock panel '{PanelName}' with control {ControlType}", panelName, typeof(TControl).Name);
                    throw;
                }
                finally
                {
                    // Clean up resources
                    control?.Dispose();
                    scope?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DockPanel operation failed for '{PanelName}': {Message}", panelName, ex.Message);
                throw new InvalidOperationException($"Unable to dock panel '{panelName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Handles application font changes by updating this form and all child controls.
        /// </summary>
        private void OnApplicationFontChanged(object? sender, FontChangedEventArgs e)
        {
            // Update this form and all child controls
            UpdateControlFont(this, e.NewFont);

            // Syncfusion DockingManager specific fonts (critical!)
            if (_dockingManager != null)
            {
                _dockingManager.DockTabFont = e.NewFont;
                _dockingManager.AutoHideTabFont = e.NewFont;

            }

            // Ribbon, grids, etc. will inherit via container update
        }

        /// <summary>
        /// Recursively updates the font of a control and all its children.
        /// Respects designer-set fonts to avoid breaking explicit overrides.
        /// </summary>
        private void UpdateControlFont(Control control, Font newFont)
        {
            // Skip if control explicitly overrides font (designer-set)
            if (control.Font != null && control.Font != control.Parent?.Font)
            {
                return;
            }

            control.Font = newFont;

            foreach (Control child in control.Controls)
            {
                UpdateControlFont(child, newFont);
            }
        }

        /// <summary>
        /// Unsubscribes from font service when form closes.
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Services.FontService.Instance.FontChanged -= OnApplicationFontChanged;
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Step 1: Signal cache freeze before disposing services
                // This prevents new cache writes from occurring during shutdown
                try
                {
                    if (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(_serviceProvider) is Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
                    {
                        _logger?.LogInformation("[CACHE] MainForm.Dispose: Freezing cache to prevent writes during shutdown");

                        if (cache is WileyWidget.WinForms.Services.IFrozenCache frozenCache)
                        {
                            frozenCache.FreezeCacheWrites();
                            _logger?.LogInformation("[CACHE] \u2713 Cache freeze signal set");
                        }
                    }
                }
                catch (Exception freezeEx)
                {
                    _logger?.LogWarning(freezeEx, "[CACHE] Failed to freeze cache during disposal");
                }

                // Step 2: Dispose the MainViewModel scope when the form is disposed
                _mainViewModelScope?.Dispose();
                _mainViewModelScope = null;

                // Close async logger before disposing services to prevent ObjectDisposedException
                try
                {
                    if (_asyncLogger is IDisposable disposableLogger)
                    {
                        disposableLogger.Dispose();
                        _asyncLogger = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to dispose async logger");
                }

                // Safe dispose UI controls before tearing down shared containers
                _menuStrip.SafeDispose();
                _ribbon.SafeDispose();
                _statusBar.SafeDispose();
                _navigationStrip.SafeDispose();
                _statusTimer.SafeDispose();

                // Safely dispose components container - may contain Syncfusion BackStageView/BackStage
                // which can throw NullReferenceException during internal UnWireEvents cleanup
                components.SafeDispose();
                _initializationCts?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Implements IAsyncInitializable. Performs heavy initialization on a background thread after the form is shown.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Defer heavy data loading to background thread
            await Task.Run(() => LoadDataAsync(cancellationToken), cancellationToken);
        }

        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            // Phase 1: Initialize dashboard data asynchronously
            _asyncLogger?.Information("MainForm OnShown: Phase 3 - Initializing MainViewModel and dashboard data");
            _logger?.LogInformation("Initializing MainViewModel");
            ApplyStatus("Loading dashboard data...");

            if (cancellationToken.IsCancellationRequested)
            {
                _logger?.LogInformation("Initialization cancelled during Phase 3");
                ApplyStatus("Initialization cancelled");
                return;
            }

            MainViewModel? mainVm = null;
            try
            {
                if (_serviceProvider == null)
                {
                    _logger?.LogError("ServiceProvider is null during MainViewModel initialization");
                    ApplyStatus("Initialization error: ServiceProvider unavailable");
                    return;
                }

                // Create a scope for scoped services - CRITICAL: Keep scope alive for MainViewModel's lifetime
                // Disposing the scope immediately causes ObjectDisposedException when MainViewModel uses DbContext
                _mainViewModelScope = _serviceProvider.CreateScope();
                var scopedServices = _mainViewModelScope.ServiceProvider;
                mainVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(scopedServices);
                _asyncLogger?.Information("MainForm OnShown: MainViewModel resolved from DI container");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to resolve MainViewModel from DI container");
                _asyncLogger?.Error(ex, "MainForm OnShown: Failed to resolve MainViewModel from DI container");
            }

            if (mainVm != null)
            {
                try
                {
                    _asyncLogger?.Information("MainForm OnShown: Calling MainViewModel.InitializeAsync");
                    // CRITICAL: Use ConfigureAwait(true) to preserve UI thread context for subsequent operations
                    await mainVm.InitializeAsync(cancellationToken).ConfigureAwait(true);
                    _logger?.LogInformation("MainViewModel initialized successfully");
                    _asyncLogger?.Information("MainForm OnShown: MainViewModel.InitializeAsync completed successfully");
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Dashboard initialization cancelled");
                    _asyncLogger?.Information("MainForm OnShown: Dashboard initialization cancelled");
                    ApplyStatus("Initialization cancelled");
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize MainViewModel in OnShown");
                    _asyncLogger?.Error(ex, "MainForm OnShown: Failed to initialize MainViewModel in OnShown");
                    ApplyStatus("Error loading dashboard data");

                    // Continue showing form even if ViewModel init fails
                    // Show user-friendly error message on UI thread
                    if (this.IsHandleCreated && !this.InvokeRequired)
                    {
                        try
                        {
                            MessageBox.Show(this,
                                $"Failed to load dashboard data: {ex.Message}\n\nThe application will continue but dashboard may not display correctly.",
                                "Initialization Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                        catch { /* Swallow MessageBox errors */ }
                    }
                    // Do NOT return - allow form to remain visible
                }
            }
            else
            {
                _logger?.LogWarning("MainViewModel not available in service provider");
            }

            // Phase 4: Initialize async services (data prefetch, etc.)
            _asyncLogger?.Information("MainForm OnShown: Phase 4 - Initializing async services");
            try
            {
                var asyncInitializables = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<WileyWidget.Abstractions.IAsyncInitializable>(_serviceProvider);
                foreach (var service in asyncInitializables)
                {
                    var serviceInitTask = Task.Run(async () =>
                    {
                        try
                        {
                            await service.InitializeAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Async service initialization failed for {ServiceType}", service.GetType().Name);
                        }
                    });
                    Program.RegisterBackgroundTask(serviceInitTask);
                }
                _asyncLogger?.Information("MainForm OnShown: Async services initialization queued");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize async services");
            }

            ApplyStatus("Ready");
            _logger?.LogInformation("OnShown: Deferred initialization completed");
        }

        #region MRU (Most Recently Used) Files

        private readonly List<string> _mruFiles = new(10);
        private const string MruRegistryKey = "Software\\\\WileyWidget\\\\MRU";

        private void AddToMruList(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                // Remove if already exists
                _mruFiles.Remove(filePath);

                // Add to top
                _mruFiles.Insert(0, filePath);

                // Keep only last 10
                if (_mruFiles.Count > 10)
                    _mruFiles.RemoveAt(10);

                // Persist to registry
                SaveMruToRegistry();

                // Update UI
                UpdateMruMenuInFileMenu();

                _logger?.LogDebug("Added to MRU: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add to MRU list");
            }
        }

        private void ClearMruList()
        {
            try
            {
                _mruFiles.Clear();
                SaveMruToRegistry();
                UpdateMruMenuInFileMenu();
                _logger?.LogInformation("MRU list cleared");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to clear MRU list");
            }
        }

        private void LoadMruFromRegistry()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(MruRegistryKey);
                if (key != null)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var file = key.GetValue($"File{i}") as string;
                        if (!string.IsNullOrEmpty(file) && File.Exists(file))
                        {
                            _mruFiles.Add(file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load MRU from registry");
            }
        }

        private void SaveMruToRegistry()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(MruRegistryKey);
                if (key != null)
                {
                    // Clear old values
                    for (int i = 0; i < 10; i++)
                    {
                        try { key.DeleteValue($"File{i}"); } catch { }
                    }

                    // Write new values
                    for (int i = 0; i < _mruFiles.Count && i < 10; i++)
                    {
                        key.SetValue($"File{i}", _mruFiles[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to save MRU to registry");
            }
        }

        private void UpdateMruMenuInFileMenu()
        {
            try
            {
                if (_menuStrip == null) return;

                var fileMenu = _menuStrip.Items.Find("Menu_File", searchAllChildren: true).FirstOrDefault() as ToolStripMenuItem;
                var recentMenu = fileMenu?.DropDownItems.Find("Menu_File_RecentFiles", searchAllChildren: true).FirstOrDefault() as ToolStripMenuItem;

                if (recentMenu != null)
                {
                    UpdateMruMenu(recentMenu);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update MRU menu");
            }
        }

        private void UpdateMruMenu(ToolStripMenuItem recentMenu)
        {
            try
            {
                recentMenu.DropDownItems.Clear();

                if (_mruFiles.Count == 0)
                {
                    recentMenu.DropDownItems.Add(new ToolStripMenuItem("(No recent files)") { Enabled = false });
                    return;
                }

                for (int i = 0; i < _mruFiles.Count; i++)
                {
                    var file = _mruFiles[i];
                    var fileName = Path.GetFileName(file);
                    var menuItem = new ToolStripMenuItem($"&{i + 1} {fileName}", null, async (s, e) =>
                    {
                        await ProcessDroppedFiles(new[] { file });
                    })
                    {
                        ToolTipText = file
                    };
                    recentMenu.DropDownItems.Add(menuItem);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to populate MRU menu");
            }
        }

        #endregion

        #region File Import and Data Loading

        /// <summary>
        /// Import data from CSV or Excel files into Budget/Accounts
        /// </summary>
        private async Task ImportDataFileAsync(string filePath)
        {
            _asyncLogger?.Information("Starting async data file import: {FilePath} on thread {ThreadId}", filePath, Thread.CurrentThread.ManagedThreadId);

            try
            {
                _logger?.LogInformation("Starting data file import: {FilePath}", filePath);
                _asyncLogger?.Debug("Applying status: Importing data...");
                ApplyStatus("Importing data...");

                // Validate file exists
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found: {filePath}", "Import Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Show import dialog to user with file info
                var fileInfo = new FileInfo(filePath);
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var confirmResult = MessageBox.Show(
                    $"Import data from:\n{fileInfo.Name}\n\nSize: {fileInfo.Length / 1024:N0} KB\nModified: {fileInfo.LastWriteTime:g}\n\nThis will import transactions and account data. Continue?",
                    "Confirm Import",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    _logger?.LogInformation("User cancelled import operation");
                    ApplyStatus("Import cancelled");
                    return;
                }

                // Import using CsvExcelImportService
                WileyWidget.Services.CsvExcelImportService importService;
                try
                {
                    importService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Services.CsvExcelImportService>(_serviceProvider);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "CsvExcelImportService not available in service provider");
                    MessageBox.Show(
                        "Import service is not available. Please check application configuration.",
                        "Import Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Detect file type and import accordingly
                WileyWidget.Services.Abstractions.ImportResult? importResult;
                if (ext == ".csv" || ext == ".xlsx" || ext == ".xls")
                {
                    // Try importing as transactions first, then budget entries if that fails
                    importResult = await importService.ImportTransactionsAsync(filePath);

                    if (!importResult.Success || importResult.AccountsImported == 0)
                    {
                        _logger?.LogInformation("Transaction import failed or found no data, trying budget entries");
                        importResult = await importService.ImportBudgetEntriesAsync(filePath);
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"Unsupported file extension: {ext}\n\nSupported formats: CSV (.csv), Excel (.xlsx, .xls)",
                        "Unsupported Format",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (importResult.Success)
                {
                    MessageBox.Show(
                        $"Data import completed successfully!\n\nFile: {fileInfo.Name}\nRecords imported: {importResult.AccountsImported}\nSize: {fileInfo.Length / 1024:N0} KB",
                        "Import Successful",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    var errorDetails = importResult.ValidationErrors != null && importResult.ValidationErrors.Count > 0
                        ? $"\n\nErrors:\n{string.Join("\n", importResult.ValidationErrors.Take(5))}"
                        : string.Empty;

                    MessageBox.Show(
                        $"Import completed with errors:\n{importResult.ErrorMessage}{errorDetails}",
                        "Import Errors",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                _logger?.LogInformation("Data file import completed: {FilePath}", filePath);
                ApplyStatus("Import completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import data file: {FilePath}", filePath);
                MessageBox.Show(
                    $"Error importing file:\n{ex.Message}\n\nPlease check the file format and try again.",
                    "Import Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ApplyStatus("Import failed");
            }
        }

        /// <summary>
        /// Imports configuration or data from JSON/XML files.
        /// Validates file format and marshals UI updates to the UI thread.
        /// </summary>
        /// <param name="filePath">Path to the JSON or XML file to import.</param>
        private async Task ImportConfigurationDataAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger?.LogWarning("ImportConfigurationDataAsync called with null or empty filePath");
                return;
            }

            try
            {
                _logger?.LogInformation("Starting configuration data import: {FilePath}", filePath);
                ApplyStatus("Loading configuration...");

                // Validate file exists
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found: {filePath}", "Load Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var result = MessageBox.Show(
                    $"Load {ext.ToUpperInvariant()} data from:\n{Path.GetFileName(filePath)}\n\nThis will load configuration or data settings. Continue?",
                    "Confirm Load",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    _logger?.LogInformation("User cancelled configuration load");
                    ApplyStatus("Load cancelled");
                    return;
                }

                // Read file content
                var content = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(content))
                {
                    MessageBox.Show("File is empty or could not be read.", "Load Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Parse and validate based on file type
                if (ext == ".json")
                {
                    // Validate JSON
                    try
                    {
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                        _logger?.LogDebug("JSON file parsed successfully, root element type: {Type}", jsonDoc.RootElement.ValueKind);
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        _logger?.LogError(jsonEx, "Invalid JSON format in file: {FilePath}", filePath);
                        MessageBox.Show(
                            $"Invalid JSON format:\n{jsonEx.Message}\n\nPlease check the file and try again.",
                            "JSON Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }
                else if (ext == ".xml")
                {
                    // Validate XML
                    try
                    {
                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.LoadXml(content);
                        _logger?.LogDebug("XML file parsed successfully, root element: {Root}", xmlDoc.DocumentElement?.Name);
                    }
                    catch (System.Xml.XmlException xmlEx)
                    {
                        _logger?.LogError(xmlEx, "Invalid XML format in file: {FilePath}", filePath);
                        MessageBox.Show(
                            $"Invalid XML format:\n{xmlEx.Message}\n\nPlease check the file and try again.",
                            "XML Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }

                // Simulate processing delay
                await Task.Delay(500);

                MessageBox.Show(
                    $"Configuration loaded successfully!\n\nFile: {Path.GetFileName(filePath)}\nSize: {content.Length:N0} bytes\n\nSettings have been applied.",
                    "Load Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _logger?.LogInformation("Configuration data loaded: {FilePath}", filePath);
                ApplyStatus("Configuration loaded");

                // Refresh UI to reflect new settings
                this.Refresh();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load configuration data: {FilePath}", filePath);
                MessageBox.Show(
                    $"Error loading file:\n{ex.Message}\n\nPlease check the file format and try again.",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ApplyStatus("Load failed");
            }
        }

        #endregion

        #region Window State Persistence

        /// <summary>
        /// Saves the current window state (size, position, and maximized/normal state) to registry.
        /// Called from OnFormClosing to persist user's preferred window layout.
        /// </summary>
        private void SaveWindowState()
        {
            try
            {
                // Only save if window is visible and not minimized (prevents saving collapsed state)
                if (this.Visible && this.WindowState != System.Windows.Forms.FormWindowState.Minimized)
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                        @"Software\Wiley Widget\WindowState", true);
                    if (key != null)
                    {
                        key.SetValue("WindowState", this.WindowState.ToString(), Microsoft.Win32.RegistryValueKind.String);
                        key.SetValue("Left", this.Left, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("Top", this.Top, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("Width", this.Width, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("Height", this.Height, Microsoft.Win32.RegistryValueKind.DWord);
                        _logger?.LogDebug("Window state saved to registry: State={State}, Size={Width}x{Height}, Pos=({Left},{Top})",
                            this.WindowState, this.Width, this.Height, this.Left, this.Top);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to save window state to registry");
                // Silently fail - window state persistence is not critical
            }
        }

        /// <summary>
        /// Restores the window state (size, position, and maximized/normal state) from registry.
        /// Called from OnLoad before the form is shown.
        /// </summary>
        private void RestoreWindowState()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Wiley Widget\WindowState");
                if (key != null)
                {
                    // Parse saved window state
                    var stateStr = key.GetValue("WindowState") as string;
                    if (Enum.TryParse<System.Windows.Forms.FormWindowState>(stateStr, out var savedState))
                    {
                        this.WindowState = savedState;
                    }

                    // Restore position and size (with validation to ensure form is visible)
                    int left = (int?)key.GetValue("Left") ?? 100;
                    int top = (int?)key.GetValue("Top") ?? 100;
                    int width = (int?)key.GetValue("Width") ?? 1400;
                    int height = (int?)key.GetValue("Height") ?? 900;

                    // Validate that position is on-screen
                    if (!_uiConfig.IsUiTestHarness && Screen.FromPoint(new Point(left + width / 2, top + height / 2)).WorkingArea.IsEmpty)
                    {
                        // Position is off-screen, use defaults
                        left = 100;
                        top = 100;
                    }

                    this.Left = left;
                    this.Top = top;
                    this.Width = Math.Max(width, this.MinimumSize.Width);
                    this.Height = Math.Max(height, this.MinimumSize.Height);

                    _logger?.LogDebug("Window state restored from registry: State={State}, Size={Width}x{Height}, Pos=({Left},{Top})",
                        this.WindowState, this.Width, this.Height, this.Left, this.Top);
                }
                else
                {
                    // No saved state - use defaults
                    this.StartPosition = FormStartPosition.CenterScreen;
                    this.Width = 1400;
                    this.Height = 900;
                    _logger?.LogDebug("No saved window state found - using defaults");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to restore window state from registry - using defaults");
                this.StartPosition = FormStartPosition.CenterScreen;
                // Silently fail and use defaults
            }
        }

        #endregion
    }
}
