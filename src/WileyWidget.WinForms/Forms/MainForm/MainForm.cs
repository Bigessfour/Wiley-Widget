using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Syncfusion.Runtime.Serialization;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Abstractions;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Initialization;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;

#pragma warning disable CS8604 // Possible null reference argument

namespace WileyWidget.WinForms.Forms
{
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
    public partial class MainForm : RibbonForm, IAsyncInitializable
    {
        private const int WS_EX_COMPOSITED = 0x02000000;
        private static int _inFirstChanceHandler = 0;

        // [PERF] Dependency Injection and Services
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _mainViewModelScope;
        private IPanelNavigationService? _panelNavigator;
        private IUiDispatcherInitializer? _uiDispatcherInitializer;
        private IThemeService? _themeService;
        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        private IStatusProgressService? _statusProgressService;

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

        // [PERF] Layout Persistence
        private AppStateSerializer? _layoutSerializer;
        private FileStream? _layoutSerializerStream;
        private bool _panelsLocked = false;
        private TableLayoutPanel? _mainLayoutPanel;

        // [PERF] Theme tracking for dynamically added controls
        private readonly HashSet<Control> _themeTrackedControls = new HashSet<Control>();

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
                    if (_uiConfig != null && !_uiConfig.IsUiTestHarness && !IsUiTestEnvironment())
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

        private static bool IsUiTestEnvironment()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase);
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
            _uiDispatcherInitializer = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<IUiDispatcherInitializer>(_serviceProvider);

            try
            {
                _uiDispatcherInitializer?.Initialize(this);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "UI dispatcher initialization in constructor will be retried during OnLoad");
            }

            // [PERF] Initialize UI configuration early
            _uiConfig = UIConfiguration.FromConfiguration(configuration);

            // [PERF] Set base form properties before any rendering
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            Load += (s, e) => { _logger?.LogInformation("MainForm Load event fired"); };

            // [PERF] Ensure reasonable default size for complex layouts
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(800, 600);
            // [FIX] Use Manual positioning to prevent flickering - RestoreWindowState will set actual position in OnLoad
            this.StartPosition = FormStartPosition.Manual;
            // Set default centered location (will be overridden by RestoreWindowState if saved state exists)
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            this.Location = new Point(
                (screen.Width - 1280) / 2 + screen.Left,
                (screen.Height - 800) / 2 + screen.Top);
            this.WindowState = System.Windows.Forms.FormWindowState.Normal;

            // [PERF] Set form size constraints
            if (_uiConfig.IsUiTestHarness || IsUiTestEnvironment())
            {
                MaximumSize = new Size(1920, 1080);
            }
            else
            {
                var screenWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 1920;
                var screenHeight = Screen.PrimaryScreen?.WorkingArea.Height ?? 1080;
                MaximumSize = new Size(screenWidth, screenHeight);
            }

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
            if (!_uiConfig.IsUiTestHarness && !IsUiTestEnvironment())
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

            // [PERF] Add exception handlers
            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;

            // [PERF] Subscribe to font changes
            Services.FontService.Instance.FontChanged += OnApplicationFontChanged;

            SuspendLayout();

            Log.Debug("[DIAGNOSTIC] MainForm constructor: COMPLETED");

            // [FIX] Resume layout to match SuspendLayout call
            ResumeLayout(false); // false to avoid immediate layout pass - OnLoad will handle it
        }

        /// <summary>
        /// OnLoad: Initialize UI chrome, load MRU, restore window state,
        /// and initialize docking before first visible paint.
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

            try
            {
                _uiDispatcherInitializer ??= Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<IUiDispatcherInitializer>(_serviceProvider);
                _uiDispatcherInitializer?.Initialize(this);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize UI dispatcher during OnLoad");
            }

            ApplyThemeForFutureControls();

            // [PERF] Load MRU and restore window state (once, in OnLoad)
            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Loading MRU list");
            LoadMruList();

            // [FIX] Restore window state (validation is handled by WindowStateService)
            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Restoring window state");
            try
            {
                _windowStateService.RestoreWindowState(this);
                // RestoreWindowState validates position is on-screen before applying
                // If no saved state or position is invalid, constructor defaults (centered) are used
            }
            catch (Exception wsEx)
            {
                _logger?.LogWarning(wsEx, "Failed to restore window state - using constructor defaults (centered)");
                // Form will use the default centered position set in constructor
            }

            // [PERF] Initialize UI chrome in OnLoad (before form is shown)
            _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Starting UI chrome initialization");
            try
            {
                using var chromePhase = StartupMetrics.TimerScope("Chrome Initialization");
                InitializeChrome();

                // [PERF] Z-order management (already handled by InitializeChrome, but ensuring consistency)
                if (_ribbon != null) _ribbon.BringToFront();
                if (_statusBar != null) _statusBar.BringToFront();

                _logger?.LogInformation("[DIAGNOSTIC] OnLoad: UI chrome initialization completed");
                InitializeStatusProgressService();
            }
            catch (Exception chromeEx)
            {
                _logger?.LogError(chromeEx, "[DIAGNOSTIC] OnLoad: InitializeChrome FAILED - {Type}: {Message}", chromeEx.GetType().Name, chromeEx.Message);
                throw;
            }

            if (!_syncfusionDockingInitialized && _uiConfig?.UseSyncfusionDocking == true)
            {
                SuspendLayout();
                try
                {
                    _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Starting Syncfusion docking initialization");
                    ValidateInitializationState();

                    InitializeSyncfusionDocking();
                    ConfigureDockingManagerChromeLayout();
                    _syncfusionDockingInitialized = true;

                    _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Initializing panel navigator");
                    EnsurePanelNavigatorInitialized();

                    if (_panelNavigator != null)
                    {
                        _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Panel navigator successfully initialized");
                    }
                    else
                    {
                        _logger?.LogWarning("[DIAGNOSTIC] OnLoad: Panel navigator initialization returned null");
                    }

                    _ribbon?.BringToFront();
                    _statusBar?.BringToFront();
                    _logger?.LogInformation("[DIAGNOSTIC] OnLoad: Syncfusion docking initialized");
                }
                catch (Exception dockingEx)
                {
                    _logger?.LogError(dockingEx,
                        "[DIAGNOSTIC] OnLoad: Syncfusion docking initialization FAILED - {Type}: {Message}\nStack: {Stack}",
                        dockingEx.GetType().Name,
                        dockingEx.Message,
                        dockingEx.StackTrace);
                }
                finally
                {
                    ResumeLayout(true);
                }
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

            // [VISIBILITY] Ensure the ribbon has its tab selected and displayed after the form is fully rendered
            if (_ribbon != null && _homeTab != null)
            {
                try
                {
                    _ribbon.SelectedTab = _homeTab;
                    _ribbon.DisplayOption = RibbonDisplayOption.ShowTabsAndCommands;
                    _ribbon.PerformLayout();
                    EnsureChromeZOrder();
                    if (_dockingManager != null && IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke((MethodInvoker)FinalizeDockingChromeLayout);
                    }
                    _logger?.LogInformation("[RIBBON] Re-asserted SelectedTab and DisplayOption in OnShown");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to re-assert ribbon state in OnShown");
                }
            }

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

            if (!_syncfusionDockingInitialized && _uiConfig?.UseSyncfusionDocking == true)
            {
                _logger?.LogError("[DIAGNOSTIC] OnShown: Docking is not initialized. OnLoad is the canonical initialization path and fallback re-initialization is disabled.");
            }

            _logger?.LogInformation("[DIAGNOSTIC] OnShown: Calling base.OnShown");
            base.OnShown(e);
            _logger?.LogInformation("[DIAGNOSTIC] OnShown: base.OnShown completed");
            _logger?.LogInformation("MainForm Shown: Visible={Visible}, Bounds={Bounds}", Visible, Bounds);

            EnsureVisibleOnScreen();

            // Headless / UI test harness: skip all deferred/background initialization.
            // This prevents WebView2 prewarm, async warmups, and other background tasks from running
            // in environments like MCP stdio servers where native components can destabilize the process.
            if ((_uiConfig != null && _uiConfig.IsUiTestHarness) || IsUiTestEnvironment())
            {
                _logger?.LogInformation("[DIAGNOSTIC] OnShown: UI test harness mode - skipping deferred initialization");
                return;
            }

            _logger?.LogInformation("[DIAGNOSTIC] OnShown: Starting deferred initialization");

            // [PERF] Track lifecycle event
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            timelineService?.RecordFormLifecycleEvent("MainForm", "Shown");

            // Async diagnostics logger already initialized earlier in OnShown

            // [PERF] Subscribe to activation event (unsubscribes after first activation)
            Activated += MainForm_Activated;

            if (string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS"), "true", StringComparison.OrdinalIgnoreCase))
            {
                ScheduleJarvisAutomationOpen();
            }

            if (string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_ACCOUNTS"), "true", StringComparison.OrdinalIgnoreCase))
            {
                ScheduleAccountsAutomationOpen();
            }



            // [PERF] Launch CLI report viewer if requested (optional feature)
            TryLaunchReportViewerOnLoad();

            // [PERF] Ensure default action button for keyboard shortcuts
            EnsureDefaultActionButtons();

            // [PERF] Allow UI structures time to fully develop and complete before starting background tasks.
            // Use BeginInvoke after the handle is created to avoid InvalidOperationException.
            void ScheduleDeferredInitialization()
            {
                if (IsDisposed)
                {
                    return;
                }

                if (!IsHandleCreated)
                {
                    EventHandler? handler = null;
                    handler = (_, __) =>
                    {
                        HandleCreated -= handler;
                        ScheduleDeferredInitialization();
                    };
                    HandleCreated += handler;
                    return;
                }

                // [FIX] Use Func<Task> instead of Action to properly handle async exceptions
                BeginInvoke(new Func<Task>(async () =>
                {
                    try
                    {
                        // Small additional delay to allow UI to settle
                        await Task.Delay(100, cancellationToken).ConfigureAwait(true);

                        // [PERF] Run deferred initialization tasks (health check, ViewModel, dashboard)
                        // Capture the task for test observability before awaiting
                        var deferredTask = RunDeferredInitializationAsync(cancellationToken);
                        _deferredInitializationTask = deferredTask;
                        await deferredTask.ConfigureAwait(true);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogDebug("Deferred initialization canceled");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Deferred initialization failed");
                        // Show error to user if form is still valid
                        if (!IsDisposed && IsHandleCreated)
                        {
                            try
                            {
                                UIHelper.ShowErrorOnUI(this,
                                    $"Initialization error: {ex.Message}\n\nPlease check the logs.",
                                    "Startup Error",
                                    _logger);
                            }
                            catch { /* Suppress UI errors during error reporting */ }
                        }
                    }
                }));
            }

            ScheduleDeferredInitialization();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MainForm.OnShown EXIT - Form should now be visible and responsive");
            _logger?.LogInformation("[DIAGNOSTIC] MainForm.OnShown COMPLETED - form visible and ready");
        }

        private void EnsureVisibleOnScreen()
        {
            if (!Visible)
            {
                Visible = true;
            }

            // [FIX] Position validation already handled in OnLoad - no need to reposition after form is visible
            // This prevents flickering when the form is already displayed
            try
            {
                var currentScreen = Screen.FromControl(this);
                if (!currentScreen.WorkingArea.IntersectsWith(this.Bounds))
                {
                    _logger?.LogWarning("Window is off-screen after initial display - bounds={Bounds}, screen={Screen}",
                        this.Bounds, currentScreen.WorkingArea);
                    // Position was already set in OnLoad; log the issue but don't change position after form is visible
                }
            }
            catch (Exception posEx)
            {
                _logger?.LogWarning(posEx, "Failed to validate window position after display");
            }
        }

        /// <summary>
        /// Schedules JARVIS panel opening for UI automation, ensuring handle is created first.
        /// </summary>
        private void ScheduleJarvisAutomationOpen()
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                EventHandler? handler = null;
                handler = (_, __) =>
                {
                    HandleCreated -= handler;
                    ScheduleJarvisAutomationOpen();
                };
                HandleCreated += handler;
                return;
            }

            // [FIX] Use Func<Task> instead of Action to properly handle async exceptions
            BeginInvoke(new Func<Task>(async () =>
            {
                const int maxAttempts = 20;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        ShowPanel<WileyWidget.WinForms.Controls.Supporting.JARVISChatUserControl>(
                            "JARVIS Chat",
                            Syncfusion.Windows.Forms.Tools.DockingStyle.Bottom,
                            allowFloating: true);
                        _logger?.LogInformation("[AUTOMATION] Auto-opened JARVIS panel for UI automation (attempt {Attempt})", attempt);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[AUTOMATION] Failed to auto-open JARVIS panel (attempt {Attempt}/{MaxAttempts})", attempt, maxAttempts);
                        await Task.Delay(500).ConfigureAwait(true);
                    }
                }
            }));
        }

        /// <summary>
        /// Schedules Accounts panel opening for UI automation, ensuring handle is created first.
        /// </summary>
        private void ScheduleAccountsAutomationOpen()
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                EventHandler? handler = null;
                handler = (_, __) =>
                {
                    HandleCreated -= handler;
                    ScheduleAccountsAutomationOpen();
                };
                HandleCreated += handler;
                return;
            }

            // [FIX] Use Func<Task> instead of Action to properly handle async exceptions
            BeginInvoke(new Func<Task>(async () =>
            {
                const int maxAttempts = 20;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left, allowFloating: true);
                        _logger?.LogInformation("[AUTOMATION] Auto-opened Accounts panel for UI automation (attempt {Attempt})", attempt);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[AUTOMATION] Failed to auto-open Accounts panel (attempt {Attempt}/{MaxAttempts})", attempt, maxAttempts);
                        await Task.Delay(500).ConfigureAwait(true);
                    }
                }
            }));
        }

        /// <summary>
        /// OnFormClosing: Cancel async operations, save state, dispose docking.
        /// Note: Must be void to override Form.OnFormClosing(FormClosingEventArgs).
        /// Uses fire-and-forget async cleanup without awaiting.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // [DIAGNOSTIC] Log why form is closing
            _logger?.LogWarning("[DIAGNOSTIC] MainForm closing - Reason: {CloseReason}, Cancel: {Cancel}",
                e.CloseReason, e.Cancel);

            try
            {
                if (e.Cancel)
                {
                    _logger?.LogInformation("[DIAGNOSTIC] Form close cancelled");
                    return;
                }

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
                    if (_ribbon != null && !_ribbon.IsDisposed)
                    {
                        _ribbon.Visible = false;
                        _ribbon.SuspendLayout();
                    }

                    _statusTimer?.Stop();
                    _logger?.LogDebug("Form closing: timers stopped");
                }
                catch { }

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

                // [PERF] Docking resources are disposed in OnHandleDestroyed (prevents Paint NRE)

                // [PERF] Unsubscribe from theme service
                if (_themeService != null)
                {
                    _themeService.ThemeChanged -= OnThemeChanged;
                }

                if (_statusProgressService != null)
                {
                    _statusProgressService.ProgressChanged -= OnStatusProgressChanged;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in OnFormClosing");
            }
            finally
            {
                base.OnFormClosing(e);
            }
        }

        /// <summary>
        /// Dispose: Clean up managed resources.

        /// <summary>
        /// Handles DPI change events to re-apply theme recursively.
        /// </summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            if (_themeService != null)
            {
                var themeName = WileyWidget.WinForms.Themes.ThemeColors.ValidateTheme(_themeService.CurrentTheme, _logger);
                SfSkinManager.ApplicationVisualTheme = themeName;
                SfSkinManager.SetVisualStyle(this, themeName);
            }

            EnsureChromeZOrder();
            if (_dockingManager != null && IsHandleCreated && !IsDisposed)
            {
                BeginInvoke((MethodInvoker)FinalizeDockingChromeLayout);
            }

            PerformLayout();
        }

        /// <summary>
        /// Handles window handle destruction.
        /// Disposes DockingManager AFTER handle is destroyed to prevent Paint NRE.
        /// This is the recommended pattern from Syncfusion to avoid paint events on disposed controls.
        /// </summary>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            try
            {
                if (_ribbon != null && !_ribbon.IsDisposed)
                {
                    _ribbon.Visible = false;
                }
            }
            catch
            {
                // Best-effort shutdown only
            }

            // Dispose docking resources after handle is destroyed (prevents Paint NRE)
            DisposeSyncfusionDockingResources();

            base.OnHandleDestroyed(e);
        }

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

                if (_ribbon != null)
                {
                    try
                    {
                        _ribbon.Visible = false;
                        _ribbon.Dispose();
                    }
                    catch
                    {
                        // Best-effort only during dispose
                    }

                    _ribbon = null;
                    _homeTab = null;
                }

                // [PERF] Dispose scoped services (CRITICAL for DbContext)
                _mainViewModelScope?.Dispose();

                // [PERF] Dispose manually created controls
                _defaultCancelButton?.Dispose();

                // [PERF] Persist and dispose layout serializer
                if (_layoutSerializer != null)
                {
                    try
                    {
                        // Unsubscribe from events
                        _layoutSerializer.BeforePersist -= OnLayoutBeforePersist;

                        // Persist final state before disposal
                        if (_layoutSerializer.Enabled && _dockingManager != null)
                        {
                            ResetLayoutSerializerStream(truncate: true);
                            _dockingManager.SaveDockState(_layoutSerializer);
                            _layoutSerializer.PersistNow();
                            _logger?.LogDebug("[MAINFORM] Layout auto-saved on dispose");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[MAINFORM] Failed to auto-save layout on dispose");
                    }

                    _layoutSerializer = null;
                }

                if (_layoutSerializerStream != null)
                {
                    try
                    {
                        _layoutSerializerStream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "[MAINFORM] Failed to dispose layout serializer stream cleanly");
                    }

                    _layoutSerializerStream = null;
                }
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
        /// Configures JARVIS panel docking to prevent reparenting during WebView2 initialization.
        /// Disables both persistence and dynamic reparenting to keep the panel HWND stable during Blazor bootstrap.
        /// This fixes the E_ABORT (HWND invalidation) issue that occurs when DockingManager reparents a control
        /// mid-CreateCoreWebView2ControllerAsync.


        /// <summary>
        /// Helper: First-chance exception handler with re-entry guard.
        /// Logs all exceptions with full diagnostic information including stack trace.
        /// Special handling for theme and docking exceptions at Debug level.
        /// OperationCanceledException logged at Debug level (expected behavior, not errors).
        /// </summary>
        private void MainForm_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;
            if (ex == null) return;

            // ✅ FIX: OperationCanceledException is expected during cancellation, log at Debug level
            if (ex is System.OperationCanceledException)
            {
                _logger?.LogDebug(ex, "Operation cancelled (expected): {Message}", ex.Message);
                return; // Early return - don't log as TARGET EXCEPTION
            }

            // Special logging for target exceptions (ArgumentException, DockingManagerException)
            if (ex is Syncfusion.Windows.Forms.Tools.DockingManagerException ||
                ex is System.ArgumentException)
            {
                _logger?.LogError(ex, "TARGET EXCEPTION: {Type} | Full details: {FullException}", ex.GetType().FullName, ex.ToString());
            }

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

            // Suppress Syncfusion string comparison errors (relational operators on strings in filter expressions)
            // These are prevented by FilterChanging handlers but may still appear as first-chance exceptions
            if (ex is InvalidOperationException invalidOperationException &&
                (invalidOperationException.Message.Contains("The binary operator GreaterThan is not defined for the types 'System.String' and 'System.String'", StringComparison.Ordinal) ||
                 invalidOperationException.Message.Contains("The binary operator LessThan is not defined for the types 'System.String' and 'System.String'", StringComparison.Ordinal) ||
                 invalidOperationException.Message.Contains("The binary operator GreaterThanOrEqual is not defined for the types 'System.String' and 'System.String'", StringComparison.Ordinal) ||
                 invalidOperationException.Message.Contains("The binary operator LessThanOrEqual is not defined for the types 'System.String' and 'System.String'", StringComparison.Ordinal)))
            {
                // Check if it's from Syncfusion grid filtering (stack trace may vary by version)
                var stackTrace = invalidOperationException.StackTrace ?? string.Empty;
                if (stackTrace.Contains("Syncfusion.WinForms.DataGrid", StringComparison.OrdinalIgnoreCase) ||
                    stackTrace.Contains("Syncfusion.Data", StringComparison.OrdinalIgnoreCase) ||
                    stackTrace.Contains("Expression.GetUserDefinedBinaryOperatorOrThrow", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogDebug("Suppressed Syncfusion first-chance filter expression error on string column (relational operator not supported)");
                    return;
                }
            }

            // Filter benign IOException related to HTTP transport connection cancellations
            if (ex is System.IO.IOException ioEx)
            {
                if (ioEx.Message.Contains("transport connection", StringComparison.OrdinalIgnoreCase) &&
                    (ioEx.Message.Contains("aborted", StringComparison.OrdinalIgnoreCase) ||
                     ioEx.Message.Contains("thread exit", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger?.LogDebug("Ignored benign IOException during transport operation (likely cancellation): {Message}", ioEx.Message);
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
#if DEBUG
                    if (_uiConfig?.VerboseFirstChanceExceptions == true)
                    {
                        logger?.LogDebug(ex, "First-chance exception (verbose)");
                        return;
                    }
#endif
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
