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
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Abstractions;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.Helpers;

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
        /// Global busy state for long-running operations (e.g., global search).
        /// Use this to track application-level async operations that affect the entire UI.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        private bool _globalIsBusy;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool GlobalIsBusy
        {
            get => _globalIsBusy;
            set
            {
                if (_globalIsBusy != value)
                {
                    _globalIsBusy = value;
                    OnGlobalIsBusyChanged();
                }
            }
        }

        /// <summary>
        /// Invoked when global busy state changes. Override to update UI (e.g., disable search button).
        /// </summary>
        protected virtual void OnGlobalIsBusyChanged()
        {
            // Can be overridden by subclasses to disable buttons, show progress, etc.
            _logger?.LogDebug("GlobalIsBusy changed to {Value}", _globalIsBusy);
        }

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
                    UIHelper.ShowErrorOnUI(this, $"Panel '{displayName}' ({typeof(TPanel).Name}) is not registered in the service container. Please verify DependencyInjection.cs.",
                        "DI Registration Error", _logger);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show panel {PanelName} with parameters in MainForm", displayName);
                // Safe fall-through - PanelNavigationService also has error handling/logging
                if (!IsDisposed && IsHandleCreated)
                {
                    try { UIHelper.ShowMessageOnUI(this, "Failed to open panel: " + ex.Message, "Panel Error", MessageBoxButtons.OK, MessageBoxIcon.Warning, _logger); } catch { }
                }
            }
        }

        /// <summary>
        /// Performs a global search across all modules (accounts, budgets, reports).
        /// This method delegates to MainViewModel.GlobalSearchCommand for MVVM purity.
        /// Called from ribbon search box for backward compatibility.
        /// </summary>
        /// <param name="query">Search query string</param>
        /// <remarks>
        /// DEPRECATED: Use MainViewModel.GlobalSearchCommand.ExecuteAsync(query) directly.
        /// Kept for backward compatibility with ribbon event handlers.
        /// </remarks>
        public async Task PerformGlobalSearchAsync(string query)
        {
            var viewModel = MainViewModel;
            if (viewModel?.GlobalSearchCommand == null)
            {
                UIHelper.ShowErrorOnUI(this, "ViewModel not initialized.", "Search Error", _logger);
                return;
            }

            // Delegate to ViewModel's GlobalSearchCommand
            // Command handles validation, error display, and resilience
            try
            {
                var method = viewModel.GlobalSearchCommand.GetType().GetMethod("ExecuteAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var task = (Task?)method.Invoke(viewModel.GlobalSearchCommand, new object?[] { query });
                    if (task != null)
                        await task;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute GlobalSearchCommand");
                UIHelper.ShowErrorOnUI(this, $"Search failed: {ex.Message}", "Search Error", _logger);
            }
        }

        /// <summary>
        /// Synchronous wrapper for backward compatibility with async void callers.
        /// New code should use PerformGlobalSearchAsync() or MainViewModel.GlobalSearchCommand directly.
        /// </summary>
        [Obsolete("Use PerformGlobalSearchAsync() or MainViewModel.GlobalSearchCommand.ExecuteAsync() instead")]
        public void PerformGlobalSearch(string query)
        {
            // Fire-and-forget for backward compatibility
            _ = PerformGlobalSearchAsync(query);
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
                    try { UIHelper.ShowMessageOnUI(this, "Failed to open panel: " + ex.Message, "Panel Error", MessageBoxButtons.OK, MessageBoxIcon.Warning, _logger); } catch { }
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
        private Task? _deferredInitializationTask;
        private readonly List<string> _mruList = new List<string>();
        // Dashboard description labels are declared in docking partial

        private readonly IWindowStateService _windowStateService;
        private readonly IFileImportService _fileImportService;

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger, ReportViewerLaunchOptions reportViewerLaunchOptions, IThemeService themeService, IWindowStateService windowStateService, IFileImportService fileImportService)
        {
            Log.Debug("[DIAGNOSTIC] MainForm constructor: ENTERED");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] MainForm constructor: ENTERED");

            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;
            _themeService = themeService;
            _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
            _fileImportService = fileImportService ?? throw new ArgumentNullException(nameof(fileImportService));

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

            // Add UI thread exception handler - catches and logs unhandled exceptions on the main UI thread
            System.Windows.Forms.Application.ThreadException += (s, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI thread exception in MainForm");
                _logger?.LogError(e.Exception, "Unhandled UI thread exception in MainForm");
            };

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

            // Load MRU and Restore Window State
            _mruList.Clear();
            _mruList.AddRange(_windowStateService.LoadMru());
            _windowStateService.RestoreWindowState(this);

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
                try { UIHelper.ShowErrorOnUI(this, $"Error processing files: {ex.Message}", "Drag-Drop Error", _logger); } catch { }
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
                    _windowStateService.AddToMru(file);
                    _mruList.Clear();
                    _mruList.AddRange(_windowStateService.LoadMru());

                    if (ext == ".csv" || ext == ".xlsx" || ext == ".xls" || ext == ".json" || ext == ".xml")
                    {
                        var result = await _fileImportService.ImportDataAsync<Dictionary<string, object>>(file, cancellationToken);
                        HandleImportResult(file, result);
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

        private void HandleImportResult<T>(string file, Result<T> result) where T : class
        {
            if (result.IsSuccess && result.Data != null)
            {
                var count = (result.Data as System.Collections.IDictionary)?.Count ?? 0;
                _logger.LogInformation("Successfully imported {File}: {Count} properties", Path.GetFileName(file), count);
                try { UIHelper.ShowMessageOnUI(this, $"File imported: {Path.GetFileName(file)}\nParsed {count} data properties",
                    "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information, _logger); } catch { }
            }
            else
            {
                _logger.LogWarning("Import failed for {File}: {Error}", Path.GetFileName(file), result.ErrorMessage);
                ShowErrorDialog("Import Failed", result.ErrorMessage ?? "Unknown error");
            }
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

            // Load MRU list and restore window state from persistent storage
            _mruList.AddRange(_windowStateService.LoadMru());
            _windowStateService.RestoreWindowState(this);

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
        protected override void OnShown(EventArgs e)
        {
            if (DesignMode)
            {
                base.OnShown(e);
                return;
            }

            if (Interlocked.Exchange(ref _onShownExecuted, 1) != 0)
            {
                _logger?.LogWarning("OnShown called multiple times - ignoring duplicate call");
                return;
            }

            _initializationCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var cancellationToken = _initializationCts.Token;

            if (!_syncfusionDockingInitialized && _uiConfig?.UseSyncfusionDocking == true)
            {
                try
                {
                    _logger?.LogInformation("OnShown: Validating initialization state");
                    ValidateInitializationState();
                }
                catch (InvalidOperationException valEx)
                {
                    _logger?.LogError(valEx, "OnShown: Initialization state validation failed");
                    _asyncLogger?.Error($"Validation Error: {valEx.Message}");
                    UIHelper.ShowErrorOnUI(this, $"Application initialization failed: {valEx.Message}\n\nThe application cannot continue.", "Initialization Error", _logger);
                    Application.Exit();
                    return;
                }
            }

            if (!_syncfusionDockingInitialized && _uiConfig?.UseSyncfusionDocking == true)
            {
                try
                {
                    _logger?.LogInformation("OnShown: Initializing Syncfusion docking");
                    InitializeSyncfusionDocking();
                    _syncfusionDockingInitialized = true;
                    this.Refresh();
                }
                catch (Exception syncEx) when (syncEx.Message.Contains("theme", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogError(syncEx, "OnShown: Theme assembly failed to load");
                    _asyncLogger?.Error($"Theme Assembly Error: {syncEx.Message}");
                }
                catch (Exception syncEx) when (syncEx.GetType().Name.Contains("Syncfusion"))
                {
                    _logger?.LogError(syncEx, "OnShown: Syncfusion initialization failed - {Message}", syncEx.Message);
                    _asyncLogger?.Error($"Syncfusion Error: {syncEx.Message}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "OnShown: Failed to initialize Syncfusion docking - {Type}: {Message}", ex.GetType().Name, ex.Message);
                    _asyncLogger?.Error($"Docking Init Error: {ex.GetType().Name}: {ex.Message}");
                }
            }

            base.OnShown(e);

            _logger?.LogInformation("[UI] MainForm OnShown: Starting deferred initialization - UI thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);

            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            timelineService?.RecordFormLifecycleEvent("MainForm", "Shown");

            try
            {
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

            Activated += MainForm_Activated;

            _asyncLogger?.Information("MainForm OnShown: Starting deferred initialization - UI thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);

            _deferredInitializationTask = RunDeferredInitializationAsync(cancellationToken);
        }

        private async Task RunDeferredInitializationAsync(CancellationToken cancellationToken)
        {
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
                    _logger?.LogWarning("Initialization timeout: Deferred startup health check canceled after 30 seconds");
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
                    _logger?.LogWarning("Initialization timeout: Deferred test data seeding canceled after 30 seconds");
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

                if (_uiConfig.UseSyncfusionDocking)
                {
                    try { EnsureDockingZOrder(); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to ensure docking z-order"); }
                }

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
                        await MainViewModel.InitializeAsync(cancellationToken).ConfigureAwait(false);
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
                        if (this.IsHandleCreated)
                        {
                            try
                            {
                                UIHelper.ShowMessageOnUI(this,
                                    $"Failed to load dashboard data: {ex.Message}\n\nThe application will continue but dashboard may not display correctly.",
                                    "Initialization Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning,
                                    _logger);
                            }
                            catch { }
                        }
                        return;
                    }
                }
                else
                {
                    _logger?.LogWarning("MainViewModel not available in service provider");
                }

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

                if (this.IsHandleCreated)
                {
                    try
                    {
                        UIHelper.ShowErrorOnUI(this, 
                            $"An unexpected error occurred during initialization: {ex.Message}\n\nPlease check the logs for details.",
                            "Critical Error", 
                            _logger);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Validates all critical initialization dependencies before proceeding.
        /// Throws InvalidOperationException if any critical dependency is missing.
        /// </summary>
        private void ValidateInitializationState()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider not initialized - dependency injection setup failed");
            }

            if (!IsHandleCreated)
            {
                throw new InvalidOperationException("Form handle not created - cannot initialize DockingManager");
            }

            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<IThemeService>(_serviceProvider);
            if (themeService == null)
            {
                throw new InvalidOperationException("IThemeService not resolved from ServiceProvider");
            }

            var themeName = themeService.CurrentTheme;
            if (string.IsNullOrEmpty(themeName))
            {
                throw new InvalidOperationException("Theme name not configured in IThemeService");
            }

            _logger?.LogInformation("ValidateInitializationState: All dependencies validated - theme={Theme}", themeName);
        }

        private void EnsurePanelNavigatorInitialized()
        {
            try
            {
                // Defensive checks: Validate all dependencies before proceeding
                if (_serviceProvider == null)
                {
                    _logger?.LogWarning("EnsurePanelNavigatorInitialized: ServiceProvider is null - skipping panel navigator initialization");
                    return;
                }

                if (_dockingManager == null)
                {
                    _logger?.LogWarning("EnsurePanelNavigatorInitialized: DockingManager is null - skipping panel navigator initialization");
                    return;
                }

                // If it's null, create it (fallback if constructor failed for some reason)
                if (_panelNavigator == null)
                {
                    var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetService<ILogger<PanelNavigationService>>(_serviceProvider) ?? NullLogger<PanelNavigationService>.Instance;

                    try
                    {
                        _panelNavigator = new PanelNavigationService(_dockingManager, this, _serviceProvider, navLogger);
                        _logger?.LogDebug("PanelNavigationService created after docking initialization");
                    }
                    catch (Exception creationEx)
                    {
                        _logger?.LogError(creationEx, "Failed to create PanelNavigationService - docking panel navigation will be unavailable");
                        return;
                    }
                }
                else
                {
                    // Update existing navigator with the real docking manager
                    try
                    {
                        _panelNavigator.UpdateDockingManager(_dockingManager);
                        _logger?.LogDebug("PanelNavigationService updated with real DockingManager");
                    }
                    catch (Exception updateEx)
                    {
                        _logger?.LogWarning(updateEx, "Failed to update PanelNavigationService with DockingManager");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Unexpected error in EnsurePanelNavigatorInitialized - panel navigation may be unavailable");
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

                // Save window state (size, position, maximized/minimized) and MRU list for next session
                try
                {
                    _windowStateService.SaveWindowState(this);
                    _windowStateService.SaveMru(_mruList);
                    _logger?.LogDebug("Form closing: window state and MRU list saved");
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
            UIHelper.ShowErrorOnUI(this, message, title, _logger);
        }

        private void ShowErrorDialog(string title, string message, Exception ex)
        {
            _logger?.LogError(ex, "Error: {Message}", message);
            UIHelper.ShowErrorOnUI(this, message, title, _logger);
        }

        private void UpdateMruMenu(ToolStripMenuItem menu)
        {
            menu.DropDownItems.Clear();
            foreach (var file in _mruList)
            {
                var item = new ToolStripMenuItem(file);
                item.Click += async (s, e) => 
                {
                    var result = await _fileImportService.ImportDataAsync<Dictionary<string, object>>(file);
                    HandleImportResult(file, result);
                };
                menu.DropDownItems.Add(item);
            }
        }

        private void ClearMruList()
        {
            _mruList.Clear();
            _windowStateService.ClearMru();
            UpdateMruMenu(_recentFilesMenu!);
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

