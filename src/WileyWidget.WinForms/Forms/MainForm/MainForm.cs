using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Abstractions;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;

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
    /// Main application window (lean entry point).
    /// Hosts navigation chrome, Syncfusion docking manager, and provides access to application-level services.
    ///
    /// Lifecycle:
    /// 1. Constructor: Initialize DI, services, event handlers. Apply global theme.
    /// 2. OnLoad: Initialize UI chrome (Ribbon, StatusBar). Load MRU and window state.
    /// 3. OnShown: Initialize Syncfusion docking. Resolve and initialize MainViewModel.
    /// 4. Dispose: Clean up scoped services, UI resources, and async operations.
    ///
    /// Partials:
    /// - MainForm.Chrome.cs: Ribbon, StatusBar, MenuBar creation (617 lines)
    /// - MainForm.Docking.cs: DockingManager, layout management (1448 lines)
    /// - MainForm.Navigation.cs: Panel navigation helpers (280 lines)
    /// - MainForm.Initialization.cs: Async init, deferred ViewModel resolution
    /// - MainForm.Keyboard.cs: Keyboard shortcuts and event handlers
    /// - MainForm.Helpers.cs: Theme, status, error dialog, MRU management
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : SfForm, IAsyncInitializable
    {
        private const int WS_EX_COMPOSITED = 0x02000000;
        private static int _inFirstChanceHandler = 0;

        // [PERF] Dependency Injection and Services
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _mainViewModelScope;
        private IPanelNavigationService? _panelNavigator;
        private IThemeService? _themeService;
        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;

        // [PERF] UI State
        private UIConfiguration _uiConfig = null!;
        private bool _initialized;
        private bool _syncfusionDockingInitialized;
        private int _onShownExecuted = 0;

        // [PERF] Async Initialization State
        private CancellationTokenSource? _initializationCts;
        private Serilog.ILogger? _asyncLogger;
        private Task? _deferredInitializationTask;

        // [PERF] Services and Configuration
        private readonly ReportViewerLaunchOptions _reportViewerLaunchOptions;
        private readonly IWindowStateService _windowStateService;
        private readonly IFileImportService _fileImportService;

        // [PERF] UI State Flags
        private bool _reportViewerLaunched;
        private bool _dashboardAutoShown;
        private Button? _defaultCancelButton;

        // [PERF] Grid Discovery Caching
        private Syncfusion.WinForms.DataGrid.SfDataGrid? _lastActiveGrid;
        private DateTime _lastActiveGridTime = DateTime.MinValue;
        private readonly TimeSpan _activeGridCacheTtl = TimeSpan.FromMilliseconds(500);

        // [PERF] Component Container
        internal System.ComponentModel.IContainer? components;

        // [PERF] MRU List (persisted across sessions)
        private readonly List<string> _mruList = new List<string>();

        /// <summary>
        /// Global MainViewModel for the application.
        /// Resolved during OnShown and kept alive for the form's lifetime.
        /// </summary>
        public MainViewModel? MainViewModel { get; private set; }

        /// <summary>
        /// Global busy state for long-running operations (e.g., global search).
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        private bool _globalIsBusy;

        /// <summary>
        /// Global busy state property. Set to true during long-running operations.
        /// </summary>
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
        /// Called when global busy state changes. Override to update UI.
        /// </summary>
        protected virtual void OnGlobalIsBusyChanged()
        {
            _logger?.LogDebug("GlobalIsBusy changed to {Value}", _globalIsBusy);
        }

        /// <summary>
        /// Enables flicker reduction via WS_EX_COMPOSITED for heavy UI chrome.
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
                    // Best-effort only; never block handle creation
                }
                return cp;
            }
        }

        /// <summary>
        /// The root IServiceProvider for the application.
        /// Throws InvalidOperationException if not initialized.
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

        /// <summary>
        /// Returns the active DockingManager instance.
        /// Throws InvalidOperationException if not initialized.
        /// </summary>
        public DockingManager GetDockingManager() => _dockingManager ?? throw new InvalidOperationException("DockingManager not initialized");

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

        /// <summary>
        /// Constructor: Initialize DI, services, theme, event handlers.
        /// Heavy initialization deferred to OnLoad/OnShown.
        /// </summary>
        public MainForm(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<MainForm> logger,
            ReportViewerLaunchOptions reportViewerLaunchOptions,
            IThemeService themeService,
            IWindowStateService windowStateService,
            IFileImportService fileImportService)
        {
            Log.Debug("[DIAGNOSTIC] MainForm constructor: ENTERED");

            // [PERF] Initialize DI container first, before creating any services
            // This ensures all dependencies are available for resolution. If caller did not provide
            // a service provider, prefer Program.Services when available; otherwise create a
            // minimal fallback provider so first-run UX does not crash.
            _serviceProvider = serviceProvider ?? WileyWidget.WinForms.Program.ServicesOrNull ?? WileyWidget.WinForms.Program.CreateFallbackServiceProvider();
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;
            _themeService = themeService;
            _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
            _fileImportService = fileImportService ?? throw new ArgumentNullException(nameof(fileImportService));

            // [PERF] Initialize UI configuration early
            _uiConfig = UIConfiguration.FromConfiguration(configuration);

            // [PERF] Apply global theme before any child controls are created
            // Theme is inherited from Program.InitializeTheme() via SfSkinManager.ApplicationVisualTheme
            // This call ensures cascade to form and early controls
            try
            {
                WileyWidget.WinForms.Themes.ThemeColors.ApplyTheme(this);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply theme in MainForm constructor");
                // Continue without theme - form will use default styling
            }

            // [PERF] Subscribe to theme switching
            try
            {
                _themeService.ThemeChanged += OnThemeChanged;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to subscribe to theme changes in MainForm constructor");
            }

            // [PERF] Optimize painting for heavy Syncfusion UI (non-test-harness only)
            if (!_uiConfig.IsUiTestHarness)
            {
                try
                {
                    SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
                    DoubleBuffered = true;
                    UpdateStyles();
                    _logger?.LogDebug("Enabled double-buffering / optimized painting on MainForm");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to enable double-buffering / optimized painting");
                }
            }

            _logger.LogInformation("UI Architecture: {Architecture}", _uiConfig.GetArchitectureDescription());

            // [PERF] Enable drag-drop for file import
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

            // [PERF] Set form size constraints
            this.MinimumSize = _uiConfig.MinimumFormSize;
            if (_uiConfig.IsUiTestHarness)
            {
                this.MaximumSize = new Size(1920, 1080);
            }
            else
            {
                var screenWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 1920;
                var screenHeight = Screen.PrimaryScreen?.WorkingArea.Height ?? 1080;
                this.MaximumSize = new Size(screenWidth, screenHeight);
            }

            // [PERF] Add exception handlers
            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;
            System.Windows.Forms.Application.ThreadException += (s, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI thread exception");
                _logger?.LogError(e.Exception, "Unhandled UI thread exception");
            };

            // [PERF] Subscribe to font changes
            Services.FontService.Instance.FontChanged += OnApplicationFontChanged;

            Log.Debug("[DIAGNOSTIC] MainForm constructor: COMPLETED");
        }

        /// <summary>
        /// OnLoad: Initialize UI chrome, load MRU, restore window state.
        /// Defers docking initialization to OnShown for safer timing.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            if (IsDisposed)
            {
                _logger?.LogWarning("OnLoad called on disposed form - ignoring");
                return;
            }

            if (!IsHandleCreated)
            {
                _logger?.LogWarning("OnLoad called before handle creation - deferring");
                base.OnLoad(e);
                return;
            }

            _logger?.LogInformation("[DIAGNOSTIC] MainForm.OnLoad START");
            base.OnLoad(e);
            _logger?.LogInformation("[DIAGNOSTIC] MainForm.OnLoad base.OnLoad completed");

            // [PERF] Track lifecycle event
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            timelineService?.RecordFormLifecycleEvent("MainForm", "Load");

            if (DesignMode) return;
            if (_initialized) return;

            // [PERF] Lazy-init services if needed
            _serviceProvider ??= Program.Services ?? new ServiceCollection().BuildServiceProvider();
            _configuration ??= Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<IConfiguration>(_serviceProvider) ?? new ConfigurationBuilder().Build();
            _logger ??= Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<ILogger<MainForm>>(_serviceProvider) ?? NullLogger<MainForm>.Instance;

            _initialized = true;

            // [PERF] Load MRU and restore window state (once, in OnLoad)
            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Loading MRU list");
            LoadMruList();
            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Restoring window state");
            _windowStateService.RestoreWindowState(this);

            // [PERF] Initialize UI chrome (Ribbon/StatusBar/MenuBar)
            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Starting UI chrome initialization");
            try
            {
                InitializeChrome();
                _logger?.LogInformation("[DIAGNOSTIC] OnLoad: UI chrome initialization completed");
            }
            catch (Exception chromeEx)
            {
                _logger?.LogError(chromeEx, "[DIAGNOSTIC] OnLoad: InitializeChrome FAILED - {Type}: {Message}", chromeEx.GetType().Name, chromeEx.Message);
                throw;
            }

            // [PERF] Z-order management
            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Starting Z-order management");
            try
            {
                if (_ribbon != null) _ribbon.BringToFront();
                if (_statusBar != null) _statusBar.BringToFront();
                Refresh();
                Invalidate();
                _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Z-order management completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[DIAGNOSTIC] OnLoad failed during z-order configuration - {Type}: {Message}", ex.GetType().Name, ex.Message);
                throw;
            }

            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: UI initialization COMPLETED SUCCESSFULLY");
        }

        /// <summary>
        /// OnShown: Initialize Syncfusion docking. Resolve MainViewModel.
        /// Deferred initialization tasks run in background after form is shown.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            if (IsDisposed)
            {
                _logger?.LogWarning("OnShown called on disposed form - ignoring");
                return;
            }

            if (!IsHandleCreated)
            {
                _logger?.LogWarning("OnShown called before handle creation - deferring");
                base.OnShown(e);
                return;
            }

            if (DesignMode)
            {
                base.OnShown(e);
                return;
            }

            _logger?.LogInformation("[DIAGNOSTIC] MainForm.OnShown START");

            // [PERF] Guard against multiple OnShown calls
            if (Interlocked.Exchange(ref _onShownExecuted, 1) != 0)
            {
                _logger?.LogWarning("[DIAGNOSTIC] OnShown called multiple times - ignoring duplicate");
                return;
            }

            // [PERF] Create cancellation token for 30-second initialization timeout
            try
            {
                _initializationCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            }
            catch (ObjectDisposedException)
            {
                _logger?.LogWarning("Form disposed during initialization token creation");
                return;
            }
            var cancellationToken = _initializationCts.Token;

            // Initialize async diagnostics logger early so we capture all OnShown lifecycle events
            // (moved earlier to ensure docking and layout restore logs are recorded)
            try
            {
                InitializeAsyncDiagnosticsLogger();
            }
            catch (Exception ex)
            {
                // Ensure diagnostics logger failures do not block startup
                _logger?.LogWarning(ex, "InitializeAsyncDiagnosticsLogger failed during OnShown startup - continuing");
            }

            // [PERF] Phase 1: Initialize Syncfusion docking
            if (!_syncfusionDockingInitialized && _uiConfig?.UseSyncfusionDocking == true)
            {
                try
                {
                    _logger?.LogInformation("[DIAGNOSTIC] OnShown: Validating initialization state");
                    ValidateInitializationState();
                    _logger?.LogInformation("[DIAGNOSTIC] OnShown: Validation state check PASSED");
                }
                catch (InvalidOperationException valEx)
                {
                    _logger?.LogError(valEx, "[DIAGNOSTIC] OnShown: Initialization state validation FAILED - {Message}", valEx.Message);
                    UIHelper.ShowErrorOnUI(this,
                        $"Application initialization failed: {valEx.Message}\n\nThe application cannot continue.",
                        "Initialization Error", _logger);
                    Application.Exit();
                    return;
                }

                try
                {
                    _logger?.LogInformation("[DIAGNOSTIC] OnShown: Starting Syncfusion docking initialization");
                    InitializeSyncfusionDocking();
                    _syncfusionDockingInitialized = true;
                    _logger?.LogInformation("[DIAGNOSTIC] OnShown: Syncfusion docking initialized, refreshing UI");
                    this.Refresh();

                    // Defer docking layout loading to OnShown for better timing
                    if (_uiConfig?.UseSyncfusionDocking == true)
                    {
                        _logger?.LogInformation("[DIAGNOSTIC] OnShown: Loading docking layout");
                        var layoutPath = GetDockingLayoutPath();
                        _ = LoadAndApplyDockingLayout(layoutPath, cancellationToken);
                        _logger?.LogInformation("[DIAGNOSTIC] OnShown: Applying theme to docking panels");
                        ApplyThemeToDockingPanels();
                        _logger?.LogInformation("[DIAGNOSTIC] OnShown: Docking layout and theme applied");
                        // Z-order adjustment is now handled by debounced handler in DockStateChanged
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[DIAGNOSTIC] OnShown: Syncfusion docking initialization FAILED - {Type}: {Message}\nStack: {Stack}",
                        ex.GetType().Name, ex.Message, ex.StackTrace);
                }
            }

            _logger?.LogInformation("[DIAGNOSTIC] OnShown: Calling base.OnShown");
            base.OnShown(e);
            _logger?.LogInformation("[DIAGNOSTIC] OnShown: base.OnShown completed");

            _logger?.LogInformation("[DIAGNOSTIC] OnShown: Starting deferred initialization");

            // [PERF] Track lifecycle event
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            timelineService?.RecordFormLifecycleEvent("MainForm", "Shown");

            // Async diagnostics logger already initialized earlier in OnShown

            // [PERF] Subscribe to activation event (unsubscribes after first activation)
            Activated += MainForm_Activated;

            // [PERF] Launch CLI report viewer if requested (optional feature)
            TryLaunchReportViewerOnLoad();

            // [PERF] Ensure default action button for keyboard shortcuts
            EnsureDefaultActionButtons();

            // [PERF] Run deferred initialization tasks (health check, ViewModel, dashboard)
            _deferredInitializationTask = RunDeferredInitializationAsync(cancellationToken);
        }

        /// <summary>
        /// OnFormClosing: Cancel async operations, save state, dispose docking.
        /// Note: Must be void to override Form.OnFormClosing(FormClosingEventArgs).
        /// Uses fire-and-forget async cleanup without awaiting.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                // [PERF] Request cancellation for initialization operations
                try
                {
                    _initializationCts?.Cancel();
                    _logger?.LogDebug("Form closing: cancellation requested");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Exception cancelling initialization");
                }

                // [PERF] Stop timers before disposal
                try
                {
                    _statusTimer?.Stop();
                    _logger?.LogDebug("Form closing: timers stopped");
                }
                catch { }

                // [PERF] Give brief moment for pending cancellation callbacks
                // Use Application.DoEvents() to pump messages without blocking
                Application.DoEvents();

                // [PERF] Save docking layout (synchronous, non-blocking)
                if (_dockingManager != null)
                {
                    try
                    {
                        _dockingLayoutManager?.SaveDockingLayout(_dockingManager);
                        _logger?.LogDebug("Form closing: docking layout saved");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to save docking layout");
                    }
                }

                // [PERF] Save window state and MRU (synchronous, non-blocking)
                try
                {
                    _windowStateService.SaveWindowState(this);
                    _windowStateService.SaveMru(_mruList);
                    _logger?.LogDebug("Form closing: window state and MRU saved");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to save window state");
                }

                // [PERF] Dispose docking resources
                DisposeSyncfusionDockingResources();

                // [PERF] Unsubscribe from theme service
                if (_themeService != null)
                {
                    _themeService.ThemeChanged -= OnThemeChanged;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in OnFormClosing");
            }
        }

        /// <summary>
        /// Dispose: Clean up managed resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                (_logger as IDisposable)?.Dispose();

                // [PERF] Cancel pending initialization (safe if already disposed)
                try
                {
                    if (_initializationCts != null && !_initializationCts.IsCancellationRequested)
                    {
                        _initializationCts.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Token source already disposed - this is fine during cleanup
                }
                catch { }

                // [PERF] Dispose async logger before token source
                if (_asyncLogger is IDisposable asyncDisposable)
                {
                    try
                    {
                        asyncDisposable.Dispose();
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                }

                // [PERF] Dispose initialization token source
                _initializationCts?.Dispose();

                // [PERF] Stop and dispose timers
                _statusTimer?.Stop();
                _statusTimer?.Dispose();

                // [PERF] Dispose scoped services (CRITICAL for DbContext)
                _mainViewModelScope?.Dispose();

                // [PERF] Dispose manually created controls
                _defaultCancelButton?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Helper: Drag-enter event for file drop support.
        /// </summary>
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

        /// <summary>
        /// Helper: Drag-drop event for file import.
        /// </summary>
        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            BeginInvoke(new Func<Task>(async () =>
            {
                try
                {
                    if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        _logger?.LogInformation("Dropped {Count} file(s)", files.Length);
                        await ProcessDroppedFiles(files);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing dropped files");
                    try { UIHelper.ShowErrorOnUI(this, $"Error: {ex.Message}", "Drag-Drop Error", _logger); } catch { }
                }
            }));
        }

        /// <summary>
        /// Helper: Activated event tracking for startup timeline (one-time).
        /// </summary>
        private void MainForm_Activated(object? sender, EventArgs e)
        {
            if (IsDisposed)
            {
                return;
            }

            _asyncLogger?.Debug("MainForm activated - thread: {ThreadId}", Thread.CurrentThread.ManagedThreadId);
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            if (timelineService != null && timelineService.CurrentPhase != null)
            {
                timelineService.RecordFormLifecycleEvent("MainForm", "Activated");
                Activated -= MainForm_Activated; // Unsubscribe after first call
            }
        }

        /// <summary>
        /// Helper: First-chance exception handler with re-entry guard.
        /// Logs all exceptions with full diagnostic information including stack trace.
        /// Special handling for theme and docking exceptions at Debug level.
        /// </summary>
        private void MainForm_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;
            if (ex == null) return;

            // Filter common Syncfusion noise patterns
            if (ex is NullReferenceException)
            {
                if (ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("dock", StringComparison.OrdinalIgnoreCase) ||
                    ex.StackTrace?.Contains("Syncfusion.Windows.Forms.Tools", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger?.LogDebug("Ignored Syncfusion NullReferenceException during initialization: {Message}", ex.Message);
                    return;
                }
            }

            // [PERF] Prevent recursive re-entry
            if (System.Threading.Interlocked.Exchange(ref _inFirstChanceHandler, 1) == 1)
                return;

            try
            {
                var logger = _logger;
                try
                {
                    // Enhanced logging with stack trace for better debugging
                    if (ex.Source?.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) == true ||
                        ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogDebug(ex, "First-chance theme exception detected - Stack Trace: {StackTrace}", ex.StackTrace);
                    }

                    if (ex.Source?.Contains("DockingManager", StringComparison.OrdinalIgnoreCase) == true ||
                        ex.Message.Contains("dock", StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogDebug(ex, "First-chance docking exception detected - Stack Trace: {StackTrace}", ex.StackTrace);
                    }

                    // Log all other first-chance exceptions for diagnostics
                    if (ex.InnerException != null)
                    {
                        logger?.LogTrace("First-chance exception has inner exception - Type: {InnerExceptionType}, Message: {InnerMessage}, Stack: {InnerStack}",
                            ex.InnerException.GetType().Name, ex.InnerException.Message, ex.InnerException.StackTrace);
                    }

                    // Log error for non-theme/docking exceptions
                    logger?.LogError(ex, "First-chance exception detected");
                }
                catch { }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _inFirstChanceHandler, 0);
            }
        }

        /// <summary>
        /// Helper: Application font change handler.
        /// </summary>
        private void OnApplicationFontChanged(object? sender, Services.FontChangedEventArgs e)
        {
            this.Font = e.NewFont;
        }
    }
}

#pragma warning restore CS8604
