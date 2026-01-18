using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.Dialogs;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Controls;
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
        private const int WS_EX_COMPOSITED = 0x02000000;
        private static int _inFirstChanceHandler = 0;
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _mainViewModelScope;  // Scope for MainViewModel - kept alive for form lifetime
        private IPanelNavigationService? _panelNavigator;
        private IThemeService? _themeService;

        /// <summary>
        /// Global PanelNavigator for the application.
        /// Created during MainForm initialization and exposed for DI resolution.
        /// </summary>
        public IPanelNavigationService? PanelNavigator => _panelNavigator;

        // Grid discovery caching
        private Syncfusion.WinForms.DataGrid.SfDataGrid? _lastActiveGrid;
        private DateTime _lastActiveGridTime = DateTime.MinValue;
        private readonly TimeSpan _activeGridCacheTtl = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Global MainViewModel for the application.
        /// Resolved during MainForm.OnShown and kept alive for the form's lifetime.
        /// </summary>
        public MainViewModel? MainViewModel { get; private set; }

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

            // Force z-order after showing panel to prevent layout issues
            try { EnsureDockingZOrder(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to ensure docking z-order after ShowPanel"); }
        }

        /// <summary>
        /// Shows or activates a docked panel with initialization parameters. Creates it if not already present.
        /// Delegates to PanelNavigationService for centralized panel management.
        /// </summary>
        /// <typeparam name="TPanel">The UserControl panel type.</typeparam>
        /// <param name="panelName">Optional panel name. If null, uses type name.</param>
        /// <param name="parameters">Parameters to pass to panel constructor or initialization.</param>
        /// <param name="preferredStyle">Preferred docking position (default: Right).</param>
        /// <param name="allowFloating">If true, panel can be floated by user (default: true).</param>
        public void ShowPanel<TPanel>(
            string? panelName,
            object? parameters,
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

            try
            {
                _panelNavigator.ShowPanel<TPanel>(displayName, parameters, preferredStyle, allowFloating);

                // Force z-order after showing panel to prevent layout issues
                try { EnsureDockingZOrder(); }
                catch (Exception ex) { _logger?.LogDebug(ex, "Failed to ensure docking z-order after ShowPanel"); }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No service for type") || ex.Message.Contains("not registered"))
            {
                _logger?.LogError(ex, "Panel {PanelType} not registered in DI container", typeof(TPanel).Name);
                if (!IsDisposed && IsHandleCreated)
                {
                    MessageBox.Show($"Panel '{displayName}' ({typeof(TPanel).Name}) is not registered in the service container. Please verify DependencyInjection.cs.",
                        "DI Registration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show panel {PanelName} with parameters in MainForm", displayName);
                // Safe fall-through - PanelNavigationService also has error handling/logging
                if (!IsDisposed && IsHandleCreated)
                {
                    try { MessageBox.Show("Failed to open panel: " + ex.Message, "Panel Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
                }
            }
        }

        /// <summary>
        /// Performs a global search across all modules (accounts, budgets, reports).
        /// Delegates to GlobalSearchService and displays results.
        /// </summary>
        /// <param name="query">Search query string</param>
        public async void PerformGlobalSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show("Please enter a search query.", "Empty Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _logger?.LogInformation("Global search initiated from ribbon: '{Query}'", query);

                // Try to resolve global search service from DI
                var searchService = _serviceProvider?.GetService(typeof(IGlobalSearchService)) as IGlobalSearchService;
                if (searchService != null)
                {
                    var results = await searchService.SearchAsync(query);
                    _logger?.LogInformation("Global search returned {ResultCount} results for '{Query}'", results.TotalResults, query);

                    // Show results in a message box for now
                    // Future: Display results in a dedicated search results panel
                    MessageBox.Show(
                        $"Search found {results.TotalResults} results for '{query}'.\n\nFeature expansion coming soon.",
                        "Search Results",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    _logger?.LogWarning("GlobalSearchService not registered in DI container");
                    MessageBox.Show("Global search service not available.", "Service Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Global search failed for query '{Query}'", query);
                MessageBox.Show($"Search failed: {ex.Message}", "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Adds an existing panel instance to the docking manager asynchronously.
        /// Useful for panels pre-initialized with specific ViewModels or state.
        /// </summary>
        /// <param name="panel">The UserControl panel instance to add.</param>
        /// <param name="panelName">The display name used for caption and identification.</param>
        /// <param name="preferredStyle">The preferred docking style (default: Right).</param>
        /// <returns>A task that completes when the panel is added.</returns>
        public async Task AddPanelAsync(
            UserControl panel,
            string panelName,
            DockingStyle preferredStyle = DockingStyle.Right)
        {
            if (_panelNavigator == null)
            {
                _logger?.LogWarning("Cannot add panel - PanelNavigationService not initialized");
                return;
            }

            try
            {
                await _panelNavigator.AddPanelAsync(panel, panelName, preferredStyle);

                // Force z-order after showing panel to prevent layout issues
                try { EnsureDockingZOrder(); }
                catch (Exception ex) { _logger?.LogDebug(ex, "Failed to ensure docking z-order after AddPanelAsync"); }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add panel {PanelName} async in MainForm", panelName);
                if (!IsDisposed && IsHandleCreated)
                {
                    try { MessageBox.Show("Failed to open panel: " + ex.Message, "Panel Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
                }
            }
        }

        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        private readonly ReportViewerLaunchOptions _reportViewerLaunchOptions;
        private MenuStrip? _menuStrip;
        private ToolStripMenuItem? _recentFilesMenu;
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
        private Button? _defaultCancelButton;

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
        private readonly List<string> _mruList = new List<string>();
        // Dashboard description labels are declared in docking partial

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger, ReportViewerLaunchOptions reportViewerLaunchOptions, IThemeService themeService)
        {
            Log.Debug("[DIAGNOSTIC] MainForm constructor: ENTERED");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] MainForm constructor: ENTERED");

            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;
            _themeService = themeService;

            // Initialize centralized UI configuration
            _uiConfig = UIConfiguration.FromConfiguration(configuration);

            // Initialize navigator EARLY to avoid circular dependency issues when child panels are resolved
            // DockingManager is null initially but will be updated in InitializeSyncfusionDocking()
            _panelNavigator = new PanelNavigationService(null, this, _serviceProvider, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<PanelNavigationService>>(_serviceProvider));

            // Apply global Syncfusion theme before any child controls are created
            AppThemeColors.ApplyTheme(this);

            // Subscribe to theme switching service
            _themeService.ThemeChanged += OnThemeChanged;

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

            // Enable drag-drop for files
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

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
        /// Optimized for non-blocking docking layout restoration and component setup.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            using var phase = timelineService?.BeginPhaseScope("MainForm Async Initialization");

            _asyncLogger?.Information("MainForm.InitializeAsync started - thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);

            // Diagnostic: Check initial state
            _logger?.LogInformation("[DIAGNOSTIC] InitializeAsync: _serviceProvider={HasValue}, _themeService={HasValue}, _panelNavigator={HasValue}, _dockingManager={HasValue}",
                _serviceProvider != null, _themeService != null, _panelNavigator != null, _dockingManager != null);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [INIT] InitializeAsync started. _serviceProvider={_serviceProvider != null}, _panelNavigator={_panelNavigator != null}, _dockingManager={_dockingManager != null}");

            // Align UI with persisted theme from service
            if (_themeService != null)
            {
                _themeService.ApplyTheme(_themeService.CurrentTheme);
            }
            else
            {
                _logger?.LogWarning("[DIAGNOSTIC] _themeService is null in InitializeAsync");
            }

            // CRITICAL: DockingManager is initialized in OnShown Phase 1.
            // Verify it exists before proceeding with panel operations.
            if (_dockingManager == null)
            {
                _logger?.LogWarning("[CRITICAL] DockingManager is null in InitializeAsync - docking was not initialized successfully in OnShown");
                _asyncLogger?.Warning("[CRITICAL] DockingManager is null in InitializeAsync");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] DockingManager not initialized - check OnShown for errors!");
                return;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [INIT] DockingManager is ready (initialized in OnShown)");

            // Phase 1: Show priority panels for faster startup
            // Instead of loading full layout, show only essential panels to reduce initialization time
            if (_uiConfig.UseSyncfusionDocking && _panelNavigator != null)
            {
                _logger?.LogInformation("Showing priority panels for faster startup");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [INIT] Starting panel initialization");
                try
                {
                    // Priority panels: Dashboard only to reduce clutter
                    _logger?.LogInformation("[PANEL] Showing Dashboard");
                    _panelNavigator.ShowPanel<DashboardPanel>("Dashboard", DockingStyle.Right, allowFloating: true);

                    //_logger?.LogInformation("[PANEL] Showing AI Chat");
                    //_panelNavigator.ShowPanel<InsightFeedPanel>("AI Chat", DockingStyle.Right, allowFloating: true);

                    //_logger?.LogInformation("[PANEL] Showing QuickBooks");
                    //_panelNavigator.ShowPanel<AccountsPanel>("QuickBooks", DockingStyle.Right, allowFloating: true);

                    //_logger?.LogInformation("[PANEL] Showing Customers");
                    //_panelNavigator.ShowPanel<CustomersPanel>("Customers", DockingStyle.Right, allowFloating: true);

                    //_logger?.LogInformation("[PANEL] Showing Revenue Trends");
                    //_panelNavigator.ShowPanel<AnalyticsPanel>("Revenue Trends", DockingStyle.Right, allowFloating: true);

                    _logger?.LogInformation("Priority panels shown successfully");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [INIT] Dashboard panel shown");
                }
                catch (NullReferenceException nrex)
                {
                    _logger?.LogError(nrex, "[CRITICAL NRE] NullReferenceException while showing panels. Stack: {Stack}", nrex.StackTrace);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] NRE in panel init: {nrex.Message}\n{nrex.StackTrace}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to show priority panels: {Type}: {Message}", ex.GetType().Name, ex.Message);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] {ex.GetType().Name} in panel init: {ex.Message}");
                }
            }
            else
            {
                _logger?.LogWarning("[DIAGNOSTIC] Skipping panel init: UseSync={UseSyncfusionDocking}, PanelNav={HasPanelNav}", _uiConfig.UseSyncfusionDocking, _panelNavigator != null);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [WARN] Skipping panels: UseSyncfusionDocking={_uiConfig.UseSyncfusionDocking}, _panelNavigator={_panelNavigator != null}");
            }

            // Phase 2: Notify ViewModels of initial visibility for lazy loading
            // This ensures panels visible on startup (e.g. Dashboard) trigger their data loads.
            if (_dockingManager != null)
            {
                _logger?.LogInformation("Triggering initial visibility notifications for all docked panels");
                foreach (Control control in this.Controls)
                {
                    // Syncfusion check: Is control managed by DockingManager?
                    if (_dockingManager.GetEnableDocking(control))
                    {
                        await NotifyPanelVisibilityChangedAsync(control);
                    }
                }
            }

            _asyncLogger?.Information("MainForm.InitializeAsync completed successfully");
            ApplyTheme();
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

        private async Task ProcessDroppedFiles(string[] files, CancellationToken cancellationToken = default)
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

            if (ex is NullReferenceException && ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("Ignored theme NullReferenceException during initialization");
                return;
            }

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

            // UI Chrome (Ribbon/StatusBar) initialized synchronously in OnLoad.
            // Docking initialization is deferred to OnShown for safer timing.
            _logger?.LogDebug("OnLoad: Starting UI initialization on UI thread {ThreadId}", Thread.CurrentThread.ManagedThreadId);
            InitializeChrome();

            // Z-order management: ribbon/status above docking panels.
            try
            {
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

                Refresh();
                Invalidate();
                _logger?.LogDebug("Z-order management completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OnLoad failed during z-order configuration");
                throw;
            }

            // DEFERRED: Docking initialization moved to OnShown for safer timing (per recommendation)
            // if (!_syncfusionDockingInitialized && _uiConfig?.UseSyncfusionDocking == true) { ... }
            if (_uiConfig?.UseSyncfusionDocking == true)
            {
                _logger?.LogInformation("OnLoad: Deferring docking initialization to OnShown");
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
            if (keyData == Keys.Enter)
            {
                if (HandleEnterShortcut())
                {
                    return true;
                }
            }

            if (keyData == Keys.Escape)
            {
                if (HandleEscapeShortcut())
                {
                    return true;
                }
            }

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
                    _panelNavigator?.ShowPanel<Controls.BudgetAnalyticsPanel>("Charts", DockingStyle.Right, true);
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

        private bool HandleEnterShortcut()
        {
            if (ActiveControl is TextBoxBase || ActiveControl is MaskedTextBox)
            {
                return false;
            }

            return FocusGlobalSearchTextBox(selectAll: true);
        }

        private bool HandleEscapeShortcut()
        {
            if (TryClearSearchText())
            {
                return true;
            }

            if (_statusTextPanel != null && !_statusTextPanel.IsDisposed && !string.IsNullOrWhiteSpace(_statusTextPanel.Text))
            {
                _statusTextPanel.Text = string.Empty;
                return true;
            }

            return false;
        }

        private bool FocusGlobalSearchTextBox(bool selectAll)
        {
            if (_ribbon == null)
            {
                return false;
            }

            if (FindToolStripItem(_ribbon, "GlobalSearch") is ToolStripTextBox searchBox)
            {
                searchBox.Focus();
                if (selectAll)
                {
                    searchBox.SelectAll();
                }
                return true;
            }

            return false;
        }

        private bool TryClearSearchText()
        {
            if (_ribbon == null)
            {
                return false;
            }

            if (FindToolStripItem(_ribbon, "GlobalSearch") is ToolStripTextBox searchBox)
            {
                if (!string.IsNullOrEmpty(searchBox.Text))
                {
                    searchBox.Clear();
                    return true;
                }
            }

            return false;
        }

        private void EnsureDefaultActionButtons()
        {
            if (_defaultCancelButton == null)
            {
                _defaultCancelButton = new Button
                {
                    Name = "DefaultCancelButton",
                    AccessibleName = "Cancel current action",
                    AccessibleDescription = "Press Escape to clear search or dismiss status text",
                    TabStop = false,
                    Visible = false,
                    Size = new Size(1, 1),
                    Location = new Point(-1000, -1000)
                };
                _defaultCancelButton.Click += (s, e) => HandleEscapeShortcut();
                Controls.Add(_defaultCancelButton);
            }

            CancelButton = _defaultCancelButton;
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

public void ToggleTheme()
        {
            try
            {
                var currentTheme = _themeService?.CurrentTheme ?? SkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                var nextTheme = string.Equals(currentTheme, "Office2019Dark", StringComparison.OrdinalIgnoreCase)
                    ? "Office2019Colorful"
                    : "Office2019Dark";

                if (_themeService != null)
                {
                    _themeService.ApplyTheme(nextTheme);
                }
                else
                {
                    SkinManager.ApplicationVisualTheme = nextTheme;
                    SfSkinManager.SetVisualStyle(this, nextTheme);
                }

                _logger?.LogInformation("Theme toggled from {CurrentTheme} to {NextTheme}", currentTheme, nextTheme);

                // Update theme toggle button text
                if (_ribbon != null)
                {
                    var themeToggle = FindToolStripItem(_ribbon, "ThemeToggle") as ToolStripButton;
                    if (themeToggle != null)
                    {
                        themeToggle.Text = SfSkinManager.ApplicationVisualTheme == "Office2019Dark" ? "☀️ Light" : "🌙 Dark";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Theme toggle failed");
            }
        }

        /// <summary>
        /// OnShown override. Deferred initialization for non-critical background operations.
        /// </summary>
        protected override async void OnShown(EventArgs e)
        {
            if (DesignMode)
            {
                base.OnShown(e);
                return;
            }

            // Thread-safe guard: Prevent duplicate execution
            if (Interlocked.Exchange(ref _onShownExecuted, 1) != 0)
            {
                _logger?.LogWarning("OnShown called multiple times - ignoring duplicate call");
                return;
            }

            // Create cancellation token source for initialization operations
            _initializationCts = new CancellationTokenSource();
            var cancellationToken = _initializationCts.Token;

            // Phase 1: Initialize Syncfusion Docking (deferred from OnLoad)
            // Initializing here ensures the form handle and bounds are fully ready.
            if (!_syncfusionDockingInitialized && _uiConfig?.UseSyncfusionDocking == true)
            {
                try
                {
                    _logger?.LogInformation("OnShown: Initializing Syncfusion docking");
                    InitializeSyncfusionDocking();
                    _syncfusionDockingInitialized = true;
                    this.Refresh();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "OnShown: Failed to initialize Syncfusion docking");
                }
            }

            // Raise Shown event AFTER docking is ready
            // This ensures event handlers (like StartupOrchestrator) see a fully prepared UI
            base.OnShown(e);

            _logger?.LogInformation("[UI] MainForm OnShown: Starting deferred initialization - UI thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);

            // Database health check + test data seeding (background)
            _ = Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_serviceProvider == null) return;

                    using var scope = _serviceProvider.CreateScope();
                    cancellationToken.ThrowIfCancellationRequested();
                    await Program.RunStartupHealthCheckAsync(scope.ServiceProvider).ConfigureAwait(false);
                    _logger?.LogInformation("Deferred startup health check completed");
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogDebug("Deferred startup health check canceled");
                }
                catch (ObjectDisposedException)
                {
                    _logger?.LogDebug("Deferred startup health check skipped due to disposal");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Deferred startup health check failed (non-fatal)");
                }
            }, cancellationToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_serviceProvider == null) return;
                    await UiTestDataSeeder.SeedIfEnabledAsync(_serviceProvider).ConfigureAwait(false);
                    _logger?.LogDebug("Deferred test data seeding completed successfully");
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogDebug("Deferred test data seeding canceled");
                }
                catch (ObjectDisposedException)
                {
                    _logger?.LogDebug("Deferred test data seeding skipped due to disposal");
                }
                catch (Exception seedEx)
                {
                    _logger?.LogWarning(seedEx, "Deferred test data seeding failed (non-critical)");
                }
            }, cancellationToken);

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
                    .MinimumLevel.Verbose()
                    .WriteTo.Async(a => a.File(asyncLogPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        buffered: false,
                        shared: true,
                        formatProvider: CultureInfo.InvariantCulture,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}"),
                        bufferSize: 10000,    // Configure queue size to prevent blocking
                        blockWhenFull: false) // Do not block producer if queue is full; drop if necessary
                    .Enrich.FromLogContext()
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
                try
                {
                    _logger?.LogInformation("OnShown: Starting deferred background initialization");
                    _asyncLogger?.Information("→ About to call ApplyStatus...");
                    ApplyStatus("Initializing...");
                    _asyncLogger?.Information("→ ApplyStatus completed");
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception prephaseEx)
                {
                    _asyncLogger?.Error(prephaseEx, "★ CRITICAL: Exception before Phase 1 code!");
                    _logger?.LogError(prephaseEx, "★ CRITICAL: Exception before Phase 1 code!");
                    throw;
                }

                _logger?.LogInformation("→ Phase 1: Docking already initialized at start of OnShown");
                _asyncLogger?.Information("→ Phase 1: Docking verification complete");

                // Ensure docking z-order
                if (_uiConfig.UseSyncfusionDocking)
                {
                    try { EnsureDockingZOrder(); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to ensure docking z-order"); }
                }

                // Phase 2: Initialize dashboard data asynchronously
                _asyncLogger?.Information("MainForm OnShown: Phase 3 - Initializing MainViewModel and dashboard data");
                _logger?.LogInformation("Initializing MainViewModel");
                ApplyStatus("Loading dashboard data...");
                cancellationToken.ThrowIfCancellationRequested();

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
                    MainViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(scopedServices);
                    _asyncLogger?.Information("MainForm OnShown: MainViewModel resolved from DI container");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to resolve MainViewModel from DI container");
                    _asyncLogger?.Error(ex, "MainForm OnShown: Failed to resolve MainViewModel from DI container");
                }

                if (MainViewModel != null)
                {
                    try
                    {
                        _asyncLogger?.Information("MainForm OnShown: Calling MainViewModel.InitializeAsync");
                        // CRITICAL FIX: Use ConfigureAwait(false) to avoid blocking UI thread during data load
                        // This allows other UI operations to proceed while ViewModel loads data
                        await MainViewModel.InitializeAsync(cancellationToken).ConfigureAwait(false);
                        // Now switch back to UI thread for any UI updates via proper marshaling
                        if (this.InvokeRequired)
                        {
                            await this.InvokeAsync(() =>
                            {
                                _logger?.LogInformation("MainViewModel initialized successfully");
                                _asyncLogger?.Information("MainForm OnShown: MainViewModel.InitializeAsync completed successfully");
                            });
                        }
                        else
                        {
                            _logger?.LogInformation("MainViewModel initialized successfully");
                            _asyncLogger?.Information("MainForm OnShown: MainViewModel.InitializeAsync completed successfully");
                        }
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
                        // Show user-friendly error message
                        if (this.IsHandleCreated)
                        {
                            try
                            {
                                // Use proper thread marshaling for MessageBox
                                if (this.InvokeRequired)
                                {
                                    this.Invoke(() =>
                                    {
                                        MessageBox.Show(this,
                                            $"Failed to load dashboard data: {ex.Message}\n\nThe application will continue but dashboard may not display correctly.",
                                            "Initialization Error",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning);
                                    });
                                }
                                else
                                {
                                    MessageBox.Show(this,
                                        $"Failed to load dashboard data: {ex.Message}\n\nThe application will continue but dashboard may not display correctly.",
                                        "Initialization Error",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
                                }
                            }
                            catch { /* Swallow MessageBox errors */ }
                        }
                        return;
                    }
                }
                else
                {
                    _logger?.LogWarning("MainViewModel not available in service provider");
                }

                // Force Dashboard display on startup after ViewModel is ready
                if (!_dashboardAutoShown && _panelNavigator != null)
                {
                    try
                    {
                        _logger?.LogInformation("Showing initial dashboard panel...");
                        ShowPanel<Controls.DashboardPanel>("Dashboard", null, DockingStyle.Top);
                        _dashboardAutoShown = true;
                        _logger?.LogInformation("Initial dashboard panel shown successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to show initial dashboard panel");
                        ShowErrorDialog("Startup Error", "Could not load dashboard. Check logs.");
                    }
                }

                ApplyStatus("Ready");
                _logger?.LogInformation("OnShown: Deferred initialization completed");

                _logger?.LogInformation("[UI] MainForm OnShown: Deferred initialization completed successfully");

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
                if (_serviceProvider == null) return;
                if (_dockingManager == null) return;

                // If it's null, create it (fallback if constructor failed for some reason)
                if (_panelNavigator == null)
                {
                    var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetService<ILogger<PanelNavigationService>>(_serviceProvider) ?? NullLogger<PanelNavigationService>.Instance;

                    _panelNavigator = new PanelNavigationService(_dockingManager, this, _serviceProvider, navLogger);
                    _logger?.LogDebug("PanelNavigationService created after docking initialization");
                }
                else
                {
                    // Update existing navigator with the real docking manager
                    _panelNavigator.UpdateDockingManager(_dockingManager);
                    _logger?.LogDebug("PanelNavigationService updated with real DockingManager");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize PanelNavigationService");
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

        /// <summary>
        /// Handles form closing event with graceful shutdown of initialization operations.
        /// Layout persistence: Docking positions are saved to AppData via DockingLayoutManager when available.
        /// If DockingLayoutManager.SaveLayout() is not available, layout falls back to defaults on next start.
        /// Users can reset layout by holding Shift while launching (see ShouldLoadDockingLayout()).
        /// </summary>
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
                        // Extended grace period to allow Serilog async sink background worker to drain queue
                        // and gracefully exit before token source disposal. Default 500ms insufficient for large queues.
                        System.Threading.Thread.Sleep(3000);
                        _logger?.LogDebug("Form closing: waited 3000ms for async operations to handle cancellation");
                    }
                    catch { /* Timing not critical */ }
                }

                // Save docking layout and dispose docking resources
                if (_dockingManager != null)
                {
                    try
                    {
                        // Use the centralized layout manager
                        if (_dockingLayoutManager != null)
                        {
                            _dockingLayoutManager.SaveDockingLayout(_dockingManager);
                            _logger?.LogDebug("Form closing: docking layout saved via DockingLayoutManager");
                        }
                        else
                        {
                            // Fallback manual save if manager missing (unlikely)
                            _logger?.LogWarning("Form closing: _dockingLayoutManager is null, skipping layout save");
                        }
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

                // Unsubscribe from theme service
                if (_themeService != null)
                {
                    _themeService.ThemeChanged -= OnThemeChanged;
                }
            }
            finally
            {
                base.OnFormClosing(e);
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                // Add: Dispose logger if it's a scoped Serilog instance
                (_logger as IDisposable)?.Dispose();

                // Cancel any pending start-up tasks
                try { _initializationCts?.Cancel(); } catch { }
                // Dispose manually created logger BEFORE canceling initialization token
                // to avoid race condition where background worker encounters disposed cancellation source
                if (_asyncLogger is IDisposable asyncDisposable)
                {
                    try
                    {
                        asyncDisposable.Dispose();
                    }
                    catch (OperationCanceledException)
                    {
                        // Async sink background worker may throw OperationCanceledException during disposal
                        // when draining queue with a signaled cancellation token. This is expected during shutdown.
                    }
                    catch
                    {
                        // Ignore other exceptions during logger disposal
                    }
                }

                // Now safe to dispose initialization token source after logger and any pending async operations
                _initializationCts?.Dispose();

                // Stop and dispose timers
                _statusTimer?.Stop();
                _statusTimer?.Dispose();

                // Dispose scoped services (CRITICAL for DbContext-holding scopes)
                _mainViewModelScope?.Dispose();

                // Dispose local controls created manually
                _defaultCancelButton?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Apply the configured theme from UIConfiguration.
        /// Phase 1: Hard-coded to Office2019Colorful.
        /// NOTE: Theme is inherited from Program.InitializeTheme() which sets ApplicationVisualTheme globally.
        /// No need to call SetVisualStyle here - it cascades automatically from the global setting.
        /// </summary>
        private void ApplyTheme()
        {
            // Delay applying theme until the docking panels are set up to prevent NullReferenceExceptions
            if (_activityLogPanel == null || _leftDockPanel == null || _rightDockPanel == null)
            {
                _logger?.LogDebug("Theme apply skipped: Docking controls are not initialized yet");
                return;
            }

            var themeName = SkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;

            try
            {
                SfSkinManager.SetVisualStyle(this, themeName);
                SfSkinManager.SetVisualStyle(_activityLogPanel, themeName);
                SfSkinManager.SetVisualStyle(_leftDockPanel, themeName);
                SfSkinManager.SetVisualStyle(_rightDockPanel, themeName);
                SfSkinManager.SetVisualStyle(_centralDocumentPanel, themeName);
                _logger?.LogInformation("Applied SfSkinManager theme after initialization: {Theme}", themeName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Theme application failed after initialization");
            }
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

        private void OnApplicationFontChanged(object? sender, FontChangedEventArgs e)
        {
            this.Font = e.NewFont;
        }

        private void ShowErrorDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowErrorDialog(string title, string message, Exception ex)
        {
            _logger?.LogError(ex, "Error: {Message}", message);
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void AddToMruList(string file)
        {
            if (!_mruList.Contains(file))
            {
                _mruList.Insert(0, file);
                if (_mruList.Count > 10) _mruList.RemoveAt(_mruList.Count - 1);
                SaveMruToRegistry();
            }
        }

        private void SaveMruToRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\WileyWidget\MRU");
                if (key == null) return;

                // Clear existing values
                foreach (var valueName in key.GetValueNames())
                {
                    key.DeleteValue(valueName, false);
                }

                // Save current MRU list
                for (int i = 0; i < _mruList.Count; i++)
                {
                    key.SetValue($"File{i}", _mruList[i]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save MRU to registry");
            }
        }

        private void LoadMruFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\WileyWidget\MRU");
                if (key == null) return;

                _mruList.Clear();
                for (int i = 0; i < 10; i++)
                {
                    var value = key.GetValue($"File{i}") as string;
                    if (!string.IsNullOrEmpty(value) && File.Exists(value))
                    {
                        _mruList.Add(value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load MRU from registry");
            }
        }

        private void RestoreWindowState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\WileyWidget\WindowState");
                if (key == null) return;

                var left = key.GetValue("Left") as int?;
                var top = key.GetValue("Top") as int?;
                var width = key.GetValue("Width") as int?;
                var height = key.GetValue("Height") as int?;
                var state = key.GetValue("WindowState") as int?;

                if (left.HasValue && top.HasValue && width.HasValue && height.HasValue)
                {
                    var screen = Screen.FromPoint(new Point(left.Value, top.Value));
                    var workingArea = screen.WorkingArea;

                    // Ensure window is visible on screen
                    if (left.Value < workingArea.Right && top.Value < workingArea.Bottom &&
                        left.Value + width.Value > workingArea.Left && top.Value + height.Value > workingArea.Top)
                    {
                        Location = new Point(left.Value, top.Value);
                        Size = new Size(width.Value, height.Value);
                    }
                }

                if (state.HasValue)
                {
                    WindowState = (System.Windows.Forms.FormWindowState)state.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore window state");
            }
        }

        private void SaveWindowState()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\WileyWidget\WindowState");
                if (key == null) return;

                key.SetValue("Left", Location.X);
                key.SetValue("Top", Location.Y);
                key.SetValue("Width", Size.Width);
                key.SetValue("Height", Size.Height);
                key.SetValue("WindowState", (int)WindowState);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save window state");
            }
        }

        private async Task ImportDataFileAsync(string file, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Importing data from {File}", file);

                var content = await File.ReadAllTextAsync(file);
                _logger.LogInformation("Read {Length} characters from file", content.Length);

                // Try to parse as JSON data
                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    });

                    if (data != null)
                    {
                        _logger.LogInformation("Successfully parsed JSON data with {Count} properties", data.Count);
                        MessageBox.Show($"Data imported from {Path.GetFileName(file)}\nParsed {data.Count} data properties", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Data imported from {Path.GetFileName(file)}\n({content.Length} characters read)", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, just show content info
                    MessageBox.Show($"Data imported from {Path.GetFileName(file)}\n({content.Length} characters read)", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                _logger.LogInformation("Data import completed from {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import data from {File}", file);
                ShowErrorDialog("Import Failed", $"Failed to import data: {ex.Message}");
            }
        }

        private async Task ImportConfigurationDataAsync(string file, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Importing configuration from {File}", file);

                var content = await File.ReadAllTextAsync(file);
                _logger.LogInformation("Read {Length} characters from configuration file", content.Length);

                // Try to parse as JSON configuration
                try
                {
                    var config = JsonSerializer.Deserialize<Dictionary<string, object>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    });

                    if (config != null)
                    {
                        _logger.LogInformation("Successfully parsed configuration with {Count} settings", config.Count);
                        MessageBox.Show($"Configuration imported from {Path.GetFileName(file)}\nParsed {config.Count} settings", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Configuration imported from {Path.GetFileName(file)}\n({content.Length} characters read)", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, just show content info
                    MessageBox.Show($"Configuration imported from {Path.GetFileName(file)}\n({content.Length} characters read)", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                _logger.LogInformation("Configuration import completed from {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import configuration from {File}", file);
                ShowErrorDialog("Import Failed", $"Failed to import configuration: {ex.Message}");
            }
        }

        private void UpdateMruMenu(ToolStripMenuItem menu)
        {
            menu.DropDownItems.Clear();
            foreach (var file in _mruList)
            {
                var item = new ToolStripMenuItem(file);
                item.Click += async (s, e) => await ImportDataFileAsync(file);
                menu.DropDownItems.Add(item);
            }
        }

        private void ClearMruList()
        {
            _mruList.Clear();
            UpdateMruMenu(_recentFilesMenu);
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

