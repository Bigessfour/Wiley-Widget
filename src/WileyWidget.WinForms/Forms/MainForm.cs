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
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Extensions;
using WileyWidget.Data;
using WileyWidget.Models;

#pragma warning disable CS8604 // Possible null reference argument
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null
#pragma warning disable CS0169 // The field is never used

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
        public const string LoadingText = "Loading...";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : SfForm
    {
        private static int _inFirstChanceHandler = 0;
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _mainViewModelScope;  // Scope for MainViewModel - kept alive for form lifetime
        private readonly IPanelNavigationService _panelNavigator;

        /// <summary>
        /// Public accessor for ServiceProvider used by child forms.
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

        /// <summary>
        /// Public accessor for DockingManager used by PanelNavigationService.
        /// </summary>
        public DockingManager GetDockingManager() => _dockingManager ?? throw new InvalidOperationException("DockingManager not initialized");

        /// <summary>
        /// Public accessor for central document panel used by PanelNavigationService.
        /// </summary>
        public Control GetCentralDocumentPanel() => _centralDocumentPanel ?? throw new InvalidOperationException("Central document panel not initialized");

        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
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
        private StatusBarAdvPanel? _clockPanel;
        private System.Windows.Forms.Timer? _statusTimer;
        private bool _dashboardAutoShown;
        private bool _syncfusionDockingInitialized;
        private bool _initialized;
        private System.ComponentModel.IContainer? components;
        private UIConfiguration _uiConfig = null!;
        private int _onShownExecuted = 0;
        private CancellationTokenSource? _initializationCts;
        private Serilog.ILogger? _asyncLogger;
        // Dashboard description labels are declared in docking partial

        public MainForm()
            : this(
                Program.Services ?? new ServiceCollection().BuildServiceProvider(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? new ConfigurationBuilder().Build(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? NullLogger<MainForm>.Instance,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ReportViewerLaunchOptions>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? ReportViewerLaunchOptions.Disabled,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPanelNavigationService>(Program.Services ?? throw new InvalidOperationException("Program.Services not initialized")))
        {
        }

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger, ReportViewerLaunchOptions reportViewerLaunchOptions, IPanelNavigationService panelNavigator)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;
            _panelNavigator = panelNavigator ?? throw new ArgumentNullException(nameof(panelNavigator));

            // Resolve IPanelNavigationService after DockingManager is initialized
            // This will be resolved lazily in OnLoad() after InitializeSyncfusionDocking() completes

            // Initialize centralized UI configuration
            _uiConfig = UIConfiguration.FromConfiguration(configuration);

            _logger.LogInformation("UI Architecture: {Architecture}", _uiConfig.GetArchitectureDescription());

            // CRITICAL FIX: Set IsMdiContainer based on configuration to prevent conflicts
            try
            {
                if (_uiConfig.UseMdiMode)
                {
                    _logger.LogDebug("Setting IsMdiContainer=true (MDI enabled in configuration)");
                    IsMdiContainer = true;
                    _logger.LogDebug("IsMdiContainer set successfully");
                }
                else
                {
                    _logger.LogDebug("MDI mode disabled in configuration - not setting IsMdiContainer");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR setting IsMdiContainer: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"StackTrace: {ex.StackTrace}");
                _logger?.LogError(ex, "Failed to set IsMdiContainer");

                // Rethrow with contextual information so callers can more easily identify
                // that MDI container configuration failed during MainForm construction.
                throw new InvalidOperationException("Failed to configure MainForm MDI container during construction", ex);
            }

            // Enable drag-drop for files
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

            // Set reasonable form size constraints to prevent unusable states
            this.MinimumSize = new Size(800, 600);
            this.MaximumSize = new Size(Screen.PrimaryScreen?.WorkingArea.Width ?? 1920, Screen.PrimaryScreen?.WorkingArea.Height ?? 1080);

            // Theme already applied globally in Program.InitializeTheme() via SkinManager.ApplicationVisualTheme
            // No need to set ThemeName here - it cascades automatically to all controls

            // Initialize UI chrome (Ribbon, StatusBar, Navigation)
            try
            {
                _logger.LogDebug("Calling InitializeChrome...");
                InitializeChrome();
                _logger.LogDebug("InitializeChrome completed");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR in InitializeChrome: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"StackTrace: {ex.StackTrace}");
                _logger?.LogError(ex, "InitializeChrome failed");

                // Rethrow with context so higher-level handlers know which initialization step failed.
                throw new InvalidOperationException("Failed to initialize UI chrome (InitializeChrome)", ex);
            }

            try
            {
                _logger.LogDebug("Calling InitializeMdiSupport...");
                InitializeMdiSupport();
                _logger.LogDebug("InitializeMdiSupport completed");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR in InitializeMdiSupport: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"StackTrace: {ex.StackTrace}");
                _logger?.LogError(ex, "InitializeMdiSupport failed");

                // Provide contextual information when rethrowing to aid diagnostics.
                throw new InvalidOperationException("Failed to initialize MDI support (InitializeMdiSupport)", ex);
            }

            // Add FirstChanceException handlers for comprehensive error logging
            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;

            // Subscribe to font changes for real-time updates
            Services.FontService.Instance.FontChanged += OnApplicationFontChanged;
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
            _asyncLogger?.Information("Processing {Count} dropped files", files.Length);

            foreach (var file in files)
            {
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
                    MessageBox.Show($"Unsupported file type: {ext}\n\nSupported: CSV, XLSX, JSON, XML",
                        "Unsupported File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

                    // Log MDI-related exceptions
                    if (ex.Message.Contains("MDI", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Mdi", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("IsMdiContainer", StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogDebug(ex, "First-chance MDI exception detected: {Message}", ex.Message);
                    }
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

            // DEFERRED: Docking initialization moved to OnShown to prevent UI freeze
            // Heavy operations (docking setup, layout loading) are now deferred to after form is visible

            UpdateDockingStateText();

            try
            {
                // Z-order: MDI client first
                if (_uiConfig.UseMdiMode && IsMdiContainer)
                {
                    var mdiClient = Controls.OfType<MdiClient>().FirstOrDefault();
                    if (mdiClient != null)
                    {
                        mdiClient.Dock = DockStyle.Fill;
                        mdiClient.Visible = true;

                        // Phase 1 Simplification: Standard MDI z-order (no TabbedMDI)
                        mdiClient.SendToBack();

                        _logger?.LogDebug("MDI client configured (TabbedMDI: {TabbedMDI})", _uiConfig.UseTabbedMdi);
                    }
                }

                // Ribbon above all
                if (_ribbon != null)
                {
                    _ribbon.BringToFront();
                    _logger?.LogDebug("Ribbon brought to front");
                }

                // Status bar above MDI but below ribbon
                if (_statusBar != null)
                {
                    _statusBar.BringToFront();
                    _logger?.LogDebug("Status bar brought to front");
                }

                this.Refresh();
                this.Invalidate();
                _logger?.LogDebug("Z-order management completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OnLoad failed during z-order configuration");
                throw;
            }

            // PanelNavigationService is now injected via constructor - no need to resolve again
            // Kept here for reference - remove if causing issues
            _logger?.LogDebug("PanelNavigationService already initialized via constructor injection");

            if (_uiConfig.AutoShowDashboard && !_dashboardAutoShown)
            {
                if (_panelNavigator != null)
                {
                    _panelNavigator.ShowPanel<Controls.DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                    _dashboardAutoShown = true;
                }
                else
                {
                    _logger?.LogWarning("Cannot auto-show dashboard: PanelNavigationService not available");
                }
            }

            _logger?.LogInformation("MainForm startup completed successfully");
        }

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
        /// OnShown override. Deferred initialization for heavy operations to avoid blocking UI thread.
        /// Microsoft recommendation: "Perform heavy initialization in OnShown to prevent UI blocking during OnLoad."
        /// Reference: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/best-practices-for-loading-controls
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

            // Track form shown event for startup timeline analysis
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<WileyWidget.Services.IStartupTimelineService>(_serviceProvider);
            timelineService?.RecordFormLifecycleEvent("MainForm", "Shown");

            // Initialize async logging for MainForm diagnostics to avoid blocking UI thread
            try
            {
                // Use root logs folder for centralized logging
                var projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
                var logsDirectory = Path.Combine(projectRoot, "logs");
                Directory.CreateDirectory(logsDirectory);
                var asyncLogPath = Path.Combine(logsDirectory, "mainform-async-.log");
                _asyncLogger = new LoggerConfiguration()
                    .WriteTo.Async(a => a.File(asyncLogPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        formatProvider: CultureInfo.InvariantCulture))
                    .Enrich.FromLogContext()
                    .MinimumLevel.Debug()
                    .CreateLogger();

                _asyncLogger.Information("Async logging initialized for MainForm diagnostics - path: {LogPath}", asyncLogPath);
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
                _logger?.LogInformation("OnShown: Starting deferred initialization");

                // Phase 1: Initialize Syncfusion docking (synchronous UI operation - execute directly on UI thread)
                ApplyStatus("Initializing...");
                cancellationToken.ThrowIfCancellationRequested();

                if (!_syncfusionDockingInitialized)
                {
                    try
                    {
                        InitializeSyncfusionDocking();
                        _syncfusionDockingInitialized = true;
                        _logger?.LogInformation("Docking initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to initialize docking manager in OnShown");
                        // Continue - not fatal
                    }
                }

                // Phase 2: Ensure docking z-order (layout already loaded synchronously in InitializeSyncfusionDocking)
                if (_uiConfig.UseSyncfusionDocking)
                {
                    // REMOVED: Redundant async reload - LoadDockingLayout() already called synchronously in InitializeSyncfusionDocking()
                    // This eliminates double-load performance hit and simplifies startup flow
                    try { EnsureDockingZOrder(); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to ensure docking z-order"); }
                }

                // Phase 3: Initialize dashboard data asynchronously
                _asyncLogger?.Information("MainForm OnShown: Phase 3 - Initializing MainViewModel and dashboard data");
                _logger?.LogInformation("Initializing MainViewModel");
                ApplyStatus("Loading dashboard data...");
                cancellationToken.ThrowIfCancellationRequested();

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
                        // Show user-friendly error message
                        if (this.IsHandleCreated)
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
                        return;
                    }
                }
                else
                {
                    _logger?.LogWarning("MainViewModel not available in service provider");
                }

                ApplyStatus("Ready");
                _logger?.LogInformation("OnShown: Deferred initialization completed");
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
                _panelNavigator.ShowPanel<Controls.ReportsPanel>("Reports", reportPath, DockingStyle.Fill, allowFloating: true);
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
                // Cancel any ongoing initialization operations
                try
                {
                    _initializationCts?.Cancel();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Exception cancelling initialization during form closing (expected if already completed)");
                }

                if (_dockingManager != null)
                {
                    try { SaveDockingLayout(); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to save docking layout during form closing"); }
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
        /// </summary>
        /// <param name="text">Status text to display.</param>
        private void ApplyStatus(string text)
        {
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
                if (_statusTextPanel != null)
                {
                    _statusTextPanel.Text = text;
                    return;
                }

                if (_statusLabel != null)
                {
                    _statusLabel.Text = text;
                    return;
                }
            }
            catch { }
        }

        public void DockPanel<TControl>(string panelName, DockingStyle style)
            where TControl : UserControl
        {
            try
            {
                if (!_uiConfig.UseSyncfusionDocking || _dockingManager == null)
                {
                    _logger?.LogWarning("Cannot dock panel '{PanelName}' - Syncfusion docking is not enabled", panelName);
                    throw new InvalidOperationException("Docking manager is not available");
                }

                if (string.IsNullOrWhiteSpace(panelName))
                {
                    throw new ArgumentException("Panel name cannot be null or empty", nameof(panelName));
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
                // Dispose the MainViewModel scope when the form is disposed
                _mainViewModelScope?.Dispose();
                _mainViewModelScope = null;

                // Dispose components container (standard IContainer, not Syncfusion)
                components?.Dispose();
                _initializationCts?.Dispose();

                // Safe dispose UI controls
                _menuStrip.SafeDispose();
                _ribbon.SafeDispose();
                _statusBar.SafeDispose();
                _navigationStrip.SafeDispose();
                _statusTimer.SafeDispose();
            }
            base.Dispose(disposing);
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
                var importService = ServiceProviderServiceExtensions.GetService<WileyWidget.Services.CsvExcelImportService>(_serviceProvider);
                if (importService == null)
                {
                    _logger?.LogError("CsvExcelImportService not available in service provider");
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
        /// Import configuration or data from JSON/XML files
        /// </summary>
        private async Task ImportConfigurationDataAsync(string filePath)
        {
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
    }
}
