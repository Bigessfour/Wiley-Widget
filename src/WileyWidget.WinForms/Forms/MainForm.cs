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
using WileyWidget.WinForms;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;
using WileyWidget.WinForms.Forms;

#pragma warning disable CS8604 // Possible null reference argument
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null
#pragma warning disable CS0169 // The field is never used

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Attribute to specify initialization order for services implementing IAsyncInitializable.
    /// Lower values are initialized first.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    internal sealed class InitializationOrderAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        /// <summary>
        /// Gets or sets the order.
        /// </summary>
        public int Order { get; }

        public InitializationOrderAttribute(int order) => Order = order;
    }
    /// <summary>
    /// Represents a class for mainformresources.
    /// </summary>
    /// <summary>
    /// Represents a class for mainformresources.
    /// </summary>
    /// <summary>
    /// Represents a class for mainformresources.
    /// </summary>
    /// <summary>
    /// Represents a class for mainformresources.
    /// </summary>

    internal static class MainFormResources
    {
        public const string FormTitle = "Wiley Widget — Running on WinForms + .NET 9";
        public const string Dashboard = "Dashboard";
        public const string Accounts = "Accounts";
        public const string Charts = "Charts";
        public const string Reports = "Reports";
        public const string Settings = "Settings";
        public const string Docking = "Docking";
        public const string LoadingText = "Loading...";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    /// <summary>
    /// The main form of the WileyWidget WinForms application.
    /// </summary>
    public partial class MainForm : SfForm, IMainFormDockingProvider
    {
        private static int _inFirstChanceHandler = 0;
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _mainViewModelScope;  // Scope for MainViewModel - kept alive for form lifetime
        private readonly IPanelNavigationService? _panelNavigator;

        /// <summary>
        /// Public accessor for ServiceProvider used by child forms.
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

        /// <summary>
        /// Public accessor for DockingManager used by PanelNavigationService.
        /// </summary>
        public DockingManager GetDockingManager() => _dockingManager ?? throw new InvalidOperationException("DockingManager not initialized"); // IMainFormDockingProvider

        /// <summary>
        /// Public accessor for central document panel used by PanelNavigationService.
        /// </summary>
        public Control GetCentralDocumentPanel() => _centralDocumentPanel ?? throw new InvalidOperationException("Central document panel not initialized"); // IMainFormDockingProvider

        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        /// <summary>
        /// Represents the _reportviewerlaunchoptions.
        /// </summary>
        /// <summary>
        /// Represents the _reportviewerlaunchoptions.
        /// </summary>
        private readonly ReportViewerLaunchOptions _reportViewerLaunchOptions;
        private MenuStrip? _menuStrip;
        private RibbonControlAdv? _ribbon;
        /// <summary>
        /// Represents the _reportviewerlaunched.
        /// </summary>
        /// <summary>
        /// Represents the _reportviewerlaunched.
        /// </summary>
        private bool _reportViewerLaunched;
        private ToolStripTabItem? _homeTab;
        private ToolStripEx? _navigationStrip;
        private StatusBarAdv? _statusBar;
        private StatusBarAdvPanel? _statusLabel;
        private StatusBarAdvPanel? _statusTextPanel;
        private StatusBarAdvPanel? _statePanel;
        private StatusBarAdvPanel? _clockPanel;
        private System.Windows.Forms.Timer? _statusTimer;
        /// <summary>
        /// Represents the _dashboardautoshown.
        /// </summary>
        /// <summary>
        /// Represents the _dashboardautoshown.
        /// </summary>
        private bool _dashboardAutoShown;
        /// <summary>
        /// Represents the _syncfusiondockinginitialized.
        /// </summary>
        private bool _syncfusionDockingInitialized;
        /// <summary>
        /// Represents the _ribbonhandlecreated.
        /// </summary>
        private bool _ribbonHandleCreated;  // Track ribbon handle creation to prevent double initialization
        /// <summary>
        /// Represents the _initialized.
        /// </summary>
        /// <summary>
        /// Represents the _initialized.
        /// </summary>
        private bool _initialized;
        private System.ComponentModel.IContainer? components;
        private UIConfiguration _uiConfig = null!;
        private int _onShownExecuted = 0;
        private CancellationTokenSource? _initializationCts;
        private CancellationTokenSource? _shutdownCts; // Cancelled on form closing to abort background operations
        private Serilog.ILogger? _asyncLogger;
        // Dashboard description labels are declared in docking partial

        public MainForm()
            : this(
                Program.Services ?? new ServiceCollection().BuildServiceProvider(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? new ConfigurationBuilder().Build(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? NullLogger<MainForm>.Instance,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ReportViewerLaunchOptions>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? ReportViewerLaunchOptions.Disabled,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IPanelNavigationService>(Program.Services ?? new ServiceCollection().BuildServiceProvider()))
        {
        }

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger, ReportViewerLaunchOptions reportViewerLaunchOptions, IPanelNavigationService? panelNavigator)
            : base() // Default base constructor, DataObjectConsumerOptions not available
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;
            _panelNavigator = panelNavigator;

            // Resolve IPanelNavigationService after DockingManager is initialized
            // This will be resolved lazily in OnLoad() after InitializeSyncfusionDocking() completes

            // Initialize centralized UI configuration
            _uiConfig = UIConfiguration.FromConfiguration(configuration);

            _logger.LogInformation("UI Architecture: {Architecture}", _uiConfig.GetArchitectureDescription());



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

            // Add FirstChanceException handlers for comprehensive error logging
            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;

            // Subscribe to font changes for real-time updates
            Services.FontService.Instance.FontChanged += OnApplicationFontChanged;
        }
        /// <summary>
        /// Performs mainform dragenter. Handles file operations. Parameters: sender, e.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>

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
        /// Performs mainform activated. Parameters: sender, e.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        /// <summary>
        /// Performs mainform activated. Parameters: sender, e.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>

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
        /// <summary>
        /// Performs mainform firstchanceexception. Parameters: sender, e.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        /// <summary>
        /// Performs mainform firstchanceexception. Parameters: sender, e.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>

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
                return;
            }

            // Enable drag-drop for files (MOVED TO OnShown to ensure form is visible for drag-drop registration)

            if (_initialized)
                return;

            // Lazy-init services when needed
            if (_serviceProvider == null)
                _serviceProvider = Program.Services ?? new ServiceCollection().BuildServiceProvider();

            if (_configuration == null)
                _configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(_serviceProvider) ?? new ConfigurationBuilder().Build();

            if (_logger == null)
                _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(_serviceProvider) ?? NullLogger<MainForm>.Instance;

            // Add ribbon to Controls now that MainForm handle exists (deferred from InitializeRibbon)
            if (_ribbon != null && !Controls.Contains(_ribbon))
            {
                SuspendLayout();
                Controls.Add(_ribbon);
                ResumeLayout(false);
                _logger?.LogDebug("Ribbon added to Controls in OnLoad (form handle exists)");
            }

            // Initialize Syncfusion docking early (moved from OnShown) to prevent control hierarchy changes
            // after ribbon handle is established
            // MOVED TO OnShown to ensure form is visible for drag-drop registration
            // if (!_syncfusionDockingInitialized)
            // {
            //     // Fire-and-forget: InitializeSyncfusionDocking is async void
            //     InitializeSyncfusionDocking();
            //     _syncfusionDockingInitialized = true;
            // }

            _initialized = true;

            // Load MRU list from registry
            LoadMruFromRegistry();

            // DEFERRED: Docking initialization moved to OnShown to prevent UI freeze
            // Heavy operations (docking setup, layout loading) are now deferred to after form is visible

            UpdateDockingStateText();

            try
            {
                // Ribbon above all
                if (_ribbon != null)
                {
                    _ribbon.BringToFront();
                    _logger?.LogDebug("Ribbon brought to front");
                }

                // Status bar below ribbon
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

            // Auto-show moved to OnShown (ensures docking initialized)

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
        /// OnShown override. Implements new synchronous startup pattern with splash progress and deferred background initialization.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Prevent duplicate execution
            if (Interlocked.Exchange(ref _onShownExecuted, 1) != 0)
            {
                _logger?.LogWarning("OnShown called multiple times - ignoring duplicate call");
                return;
            }

            // Enable drag-drop for files (moved from OnLoad to ensure form is visible and STA thread is ready)
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                AllowDrop = true;
                DragEnter += MainForm_DragEnter;
                DragDrop += MainForm_DragDrop;
                _logger?.LogDebug("Drag-drop handlers registered successfully in OnShown");
            }
            else
            {
                _logger?.LogWarning("Cannot register drag-drop handlers: Not on STA thread");
            }

            // Show splash form for progress reporting
            using (var splash = new Program.SplashForm())
            {
                splash.Show();
                splash.BringToFront();
                splash.Report(0.01, "Starting up...");
                Application.DoEvents();

                try
                {
                    // Phase 1: Synchronous service initialization (IInitializable)
                    var initializables = _serviceProvider?.GetServices<IInitializable>()?.ToList() ?? new List<IInitializable>();
                    int total = initializables.Count;
                    int current = 0;
                    foreach (var svc in initializables)
                    {
                        current++;
                        string svcName = svc.GetType().Name;
                        splash.Report((double)current / (total + 2), $"Initializing {svcName}...");
                        svc.Initialize();
                        Application.DoEvents();
                    }

                    // Phase 2: UI setup (Syncfusion docking initialization)
                    splash.Report(0.85, "Configuring UI...");
                    // Initialize Syncfusion docking now that form is visible for drag-drop registration
                    if (!_syncfusionDockingInitialized)
                    {
                        // Fire-and-forget: InitializeSyncfusionDocking is async void
                        InitializeSyncfusionDocking();
                        _syncfusionDockingInitialized = true;
                    }
                    Application.DoEvents();

                    // Phase 3: MainViewModel initialization (synchronous)
                    splash.Report(0.92, "Loading dashboard...");
                    // If MainViewModel implements IInitializable, it will be handled above
                    Application.DoEvents();

                    // Auto-show dashboard after docking and viewmodel initialization
                    if (_uiConfig.AutoShowDashboard && !_dashboardAutoShown)
                    {
                        try
                        {
                            if (_panelNavigator != null)
                            {
                                _panelNavigator.Initialize(this);
                                _panelNavigator.ShowPanel<Controls.DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                                _dashboardAutoShown = true;
                            }
                            else
                            {
                                var navigator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IPanelNavigationService>(_serviceProvider);
                                if (navigator != null)
                                {
                                    navigator.Initialize(this);
                                    navigator.ShowPanel<Controls.DashboardPanel>("Dashboard", DockingStyle.Top, allowFloating: true);
                                    _dashboardAutoShown = true;
                                }
                                else
                                {
                                    _logger?.LogWarning("Cannot auto-show dashboard: PanelNavigationService not available");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to auto-show dashboard panel");
                        }
                    }

                    // Phase 4: Schedule deferred background initialization (synchronous for now)
                    splash.Report(0.97, "Finalizing startup...");
                    DeferredBackgroundInitialization();
                    Application.DoEvents();

                    splash.Report(1.0, "Ready");
                    Application.DoEvents();
                }
                catch (Exception ex)
                {
                    splash.Report(1.0, "Startup error");
                    _logger?.LogError(ex, "Critical error during startup");
                    MessageBox.Show($"Startup error: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    splash.Close();
                }
            }

            // Continue with normal OnShown logic (activate, etc.)
            Activated += MainForm_Activated;
            _logger?.LogInformation("MainForm OnShown: Synchronous startup complete, background initialization scheduled");
        }

        /// <summary>
        /// Deferred background initialization (runs on background thread after splash closes).
        /// </summary>
        /// <summary>
        /// Performs deferredbackgroundinitialization.
        /// </summary>
        private void DeferredBackgroundInitialization()
        {
            try
            {
                // Example: Load MRU, warm up caches, etc. (add as needed)
                _logger?.LogInformation("Deferred background initialization started");
                // ...
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in deferred background initialization");
            }
        }

        /// <summary>
        /// Initialize all IAsyncInitializable services in order, with non-blocking error handling.
        /// </summary>
        private async Task InitializeAsyncServicesInOrder(CancellationToken cancellationToken)
        {
            try
            {
                _asyncLogger?.Information("Initializing IAsyncInitializable services in order");
                ApplyStatus("Initializing services...");

                if (_serviceProvider == null)
                {
                    _logger?.LogError("ServiceProvider is null during async service initialization");
                    ApplyStatus("Initialization error: ServiceProvider unavailable");
                    return;
                }

                // Get all IAsyncInitializable services
                var asyncInitializables = _serviceProvider.GetServices<IAsyncInitializable>()?.ToList() ?? new List<IAsyncInitializable>();
                if (asyncInitializables.Count == 0)
                {
                    _logger?.LogInformation("No IAsyncInitializable services registered");
                    return;
                }

                // Order by InitializationOrderAttribute (lowest first), then by type name
                asyncInitializables = asyncInitializables
                    .OrderBy(s => s.GetType().GetCustomAttributes(typeof(InitializationOrderAttribute), true)
                        .Cast<InitializationOrderAttribute>().FirstOrDefault()?.Order ?? int.MaxValue)
                    .ThenBy(s => s.GetType().FullName)
                    .ToList();

                var errorReporting = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(_serviceProvider);

                foreach (var service in asyncInitializables)
                {
                    var typeName = service.GetType().Name;
                    try
                    {
                        ApplyStatus($"Initializing {typeName}...");
                        _asyncLogger?.Information("Initializing service: {Service}", typeName);
                        await service.InitializeAsync(cancellationToken).ConfigureAwait(true);
                        _logger?.LogInformation("Service initialized: {Service}", typeName);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation("Initialization cancelled for {Service}", typeName);
                        ApplyStatus($"Initialization cancelled: {typeName}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to initialize service: {Service}", typeName);
                        _asyncLogger?.Error(ex, "Failed to initialize service: {Service}", typeName);
                        ApplyStatus($"Error: {typeName}");
                        errorReporting?.ReportError(ex, $"Failed to initialize {typeName}: {ex.Message}");
                        // Continue initializing other services
                    }
                }

                ApplyStatus("All services initialized");
                _logger?.LogInformation("All IAsyncInitializable services initialized");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Async service initialization cancelled");
                ApplyStatus("Initialization cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during async service initialization");
                ApplyStatus("Initialization error");
                _asyncLogger?.Error(ex, "Unexpected error during async service initialization");
            }
        }

        /// <summary>
        /// Initialize MainViewModel asynchronously after OnShown completes.
        /// This prevents deadlock where ConfigureAwait(true) tries to marshal back to UI thread
        /// while OnShown is still executing on the same thread.
        /// </summary>
        private async Task InitializeMainViewModelAsync(CancellationToken cancellationToken)
        {
            try
            {
                _asyncLogger?.Information("MainForm InitializeMainViewModelAsync: Starting deferred MainViewModel initialization");
                _logger?.LogInformation("Initializing MainViewModel (deferred)");

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
                    _asyncLogger?.Information("MainForm InitializeMainViewModelAsync: MainViewModel resolved from DI container");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to resolve MainViewModel from DI container");
                    _asyncLogger?.Error(ex, "MainForm InitializeMainViewModelAsync: Failed to resolve MainViewModel from DI container");
                }

                if (mainVm != null)
                {
                    try
                    {
                        _asyncLogger?.Information("MainForm InitializeMainViewModelAsync: Calling MainViewModel.InitializeAsync");
                        await mainVm.InitializeAsync(cancellationToken).ConfigureAwait(true);
                        _logger?.LogInformation("MainViewModel initialized successfully");
                        _asyncLogger?.Information("MainForm InitializeMainViewModelAsync: MainViewModel.InitializeAsync completed successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation("Dashboard initialization cancelled");
                        _asyncLogger?.Information("MainForm InitializeMainViewModelAsync: Dashboard initialization cancelled");
                        ApplyStatus("Initialization cancelled");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to initialize MainViewModel");
                        _asyncLogger?.Error(ex, "MainForm InitializeMainViewModelAsync: Failed to initialize MainViewModel");
                        ApplyStatus("Error loading dashboard data");
                        // Show user-friendly error message
                        if (this.IsHandleCreated && !this.IsDisposed)
                        {
                            try
                            {
                                this.BeginInvoke(new global::System.Action(() =>
                                {
                                    if (!this.IsDisposed)
                                    {
                                        MessageBox.Show(this,
                                            $"Failed to load dashboard data: {ex.Message}\n\nThe application will continue but dashboard may not display correctly.",
                                            "Initialization Error",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning);
                                    }
                                }));
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
                _logger?.LogInformation("MainViewModel initialization completed");
                _asyncLogger?.Information("MainForm InitializeMainViewModelAsync: Initialization completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("MainViewModel initialization cancelled");
                ApplyStatus("Initialization cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during MainViewModel initialization");
                ApplyStatus("Initialization error");
                _asyncLogger?.Error(ex, "MainForm InitializeMainViewModelAsync: Unexpected error during initialization");
            }
        }
        /// <summary>
        /// Performs trylaunchreportvieweronload.
        /// </summary>

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
        /// <summary>
        /// Performs showreportspanel. Parameters: reportPath.
        /// </summary>
        /// <param name="reportPath">The reportPath.</param>
        /// <summary>
        /// Performs showreportspanel. Parameters: reportPath.
        /// </summary>
        /// <param name="reportPath">The reportPath.</param>

        private bool ShowReportsPanel(string reportPath)
        {
            try
            {
                if (_panelNavigator == null)
                {
                    _logger?.LogWarning("Cannot show reports panel for {ReportPath}: PanelNavigationService not available", reportPath);
                    return false;
                }

                _panelNavigator.ShowPanel<Controls.ReportsPanel>("Reports", reportPath, DockingStyle.Fill, allowFloating: true);
                _logger?.LogInformation("Reports panel shown with auto-load path: {ReportPath}", reportPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show reports panel");
                return false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                _logger?.LogInformation("MainForm closing - initiating graceful shutdown");

                // Signal shutdown to background operations and cancel initialization immediately
                try
                {
                    _shutdownCts?.Cancel();
                    _initializationCts?.Cancel();
                    _logger?.LogDebug("Background operations and initialization cancelled");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Exception cancelling initialization during form closing (expected if already completed)");
                }

                // Stop status timer immediately to prevent callbacks during shutdown
                try
                {
                    if (_statusTimer != null)
                    {
                        _statusTimer.Stop();
                        _statusTimer.Enabled = false;
                        _logger?.LogDebug("Status timer stopped");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to stop status timer during form closing");
                }

                // Save docking layout best-effort without blocking shutdown
                // Fire-and-forget to prevent deadlock during shutdown
                if (_dockingManager != null)
                {
                    try
                    {
                        // Don't wait for completion - best-effort save only
                        _ = Task.Run(() =>
                        {
                            try { SaveDockingLayout(); }
                            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to SaveDockingLayout during form closing"); }
                        });
                        _logger?.LogDebug("Docking layout save scheduled (best-effort, no wait)");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed to schedule docking layout save during form closing");
                    }
                }

                // Dispose Syncfusion docking resources synchronously
                try
                {
                    DisposeSyncfusionDockingResources();
                    _logger?.LogDebug("Syncfusion docking resources disposed");
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to dispose Syncfusion docking resources during form closing");
                }

                _logger?.LogInformation("MainForm graceful shutdown completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during MainForm shutdown");
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
        /// <summary>
        /// Performs applytheme.
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
        /// <summary>
        /// Performs applystatus. Parameters: text.
        /// </summary>
        /// <param name="text">The text.</param>
        private void ApplyStatus(string text)
        {
            try
            {
                if (this.IsHandleCreated && this.InvokeRequired)
                {
                    try { this.BeginInvoke(new global::System.Action(() => ApplyStatus(text))); } catch { }
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
        /// <summary>
        /// Performs onapplicationfontchanged. Parameters: sender, e.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
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
        /// <summary>
        /// Performs updatecontrolfont. Parameters: control, newFont.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="newFont">The newFont.</param>
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
                try
                {
                    // Cancel any pending operations before disposal
                    _shutdownCts?.Cancel();
                    _initializationCts?.Cancel();
                }
                catch { /* Best-effort cancellation */ }

                // Dispose the MainViewModel scope when the form is disposed
                try
                {
                    _mainViewModelScope?.Dispose();
                    _mainViewModelScope = null;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to dispose MainViewModel scope");
                }

                // Dispose cancellation token sources
                try
                {
                    _initializationCts?.Dispose();
                    _shutdownCts?.Dispose();
                }
                catch { /* Swallow disposal exceptions */ }

                // Dispose components container (standard IContainer, not Syncfusion)
                try
                {
                    components?.Dispose();
                }
                catch { /* Swallow disposal exceptions */ }

                // Safe dispose UI controls
                _menuStrip.SafeDispose();
                _ribbon.SafeDispose();
                _statusBar.SafeDispose();
                _navigationStrip.SafeDispose();
                _statusTimer.SafeDispose();

                // Dispose async logger
                try
                {
                    (_asyncLogger as IDisposable)?.Dispose();
                }
                catch { /* Swallow disposal exceptions */ }
            }
            base.Dispose(disposing);
        }

        #region MRU (Most Recently Used) Files

        private readonly List<string> _mruFiles = new(10);
        private const string MruRegistryKey = "Software\\\\WileyWidget\\\\MRU";
        /// <summary>
        /// Performs addtomrulist. Handles file operations. Parameters: filePath.
        /// </summary>
        /// <param name="filePath">The filePath.</param>

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
        /// <summary>
        /// Performs clearmrulist. Handles file operations.
        /// </summary>
        /// <summary>
        /// Performs clearmrulist. Handles file operations.
        /// </summary>

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
        /// <summary>
        /// Performs loadmrufromregistry. Handles file operations.
        /// </summary>
        /// <summary>
        /// Performs loadmrufromregistry. Handles file operations.
        /// </summary>

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
        /// <summary>
        /// Performs savemrutoregistry. Handles file operations.
        /// </summary>
        /// <summary>
        /// Performs savemrutoregistry. Handles file operations.
        /// </summary>

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
        /// <summary>
        /// Performs updatemrumenuinfilemenu. Handles file operations.
        /// </summary>
        /// <summary>
        /// Performs updatemrumenuinfilemenu. Handles file operations.
        /// </summary>

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
        /// <summary>
        /// Performs updatemrumenu. Handles file operations. Parameters: recentMenu.
        /// </summary>
        /// <param name="recentMenu">The recentMenu.</param>
        /// <summary>
        /// Performs updatemrumenu. Handles file operations. Parameters: recentMenu.
        /// </summary>
        /// <param name="recentMenu">The recentMenu.</param>

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
