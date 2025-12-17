using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms;
using System.IO;
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
    public partial class MainForm : SfForm, IViewManager
    {
        private static int _inFirstChanceHandler = 0;
        private IServiceProvider? _serviceProvider;
        private IServiceScope? _mainViewModelScope;  // Scope for MainViewModel - kept alive for form lifetime

        /// <summary>
        /// Public accessor for ServiceProvider used by child forms.
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");
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
        // Dashboard description labels are declared in docking partial

        public MainForm()
            : this(
                Program.Services ?? new ServiceCollection().BuildServiceProvider(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? new ConfigurationBuilder().Build(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? NullLogger<MainForm>.Instance,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ReportViewerLaunchOptions>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? ReportViewerLaunchOptions.Disabled)
        {
        }

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger, ReportViewerLaunchOptions reportViewerLaunchOptions)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;

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

            // Apply theme and initialize chrome
            ApplyTheme();

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

            if (_uiConfig.AutoShowDashboard && !_dashboardAutoShown)
            {
                ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false);
                _dashboardAutoShown = true;
            }

            _logger?.LogInformation("MainForm startup completed successfully");
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
                _logger?.LogInformation("Initializing MainViewModel");
                ApplyStatus("Loading dashboard data...");
                cancellationToken.ThrowIfCancellationRequested();

                MainViewModel? mainVm = null;
                try
                {
                    // Create a scope for scoped services - CRITICAL: Keep scope alive for MainViewModel's lifetime
                    // Disposing the scope immediately causes ObjectDisposedException when MainViewModel uses DbContext
                    _mainViewModelScope = _serviceProvider.CreateScope();
                    var scopedServices = _mainViewModelScope.ServiceProvider;
                    mainVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainViewModel>(scopedServices);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to resolve MainViewModel from DI container");
                }

                if (mainVm != null)
                {
                    try
                    {
                        await mainVm.InitializeAsync(cancellationToken).ConfigureAwait(true);
                        _logger?.LogInformation("MainViewModel initialized successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation("Dashboard initialization cancelled");
                        ApplyStatus("Initialization cancelled");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to initialize MainViewModel in OnShown");
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
                ShowReportViewerForm(reportPath);
                _reportViewerLaunched = true;
                _logger?.LogInformation("Report viewer opened for CLI path: {ReportPath}", reportPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open report viewer for {ReportPath}", reportPath);
            }
        }

        private void ShowReportViewerForm(string reportPath)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(() => ShowReportViewerForm(reportPath)));
                return;
            }

            if (!File.Exists(reportPath))
            {
                _logger?.LogWarning("Report viewer path became unavailable before launch: {ReportPath}", reportPath);
                return;
            }

            var reportViewer = new ReportViewerForm(this, reportPath);

            if (_uiConfig.UseMdiMode)
            {
                if (!IsMdiContainer)
                {
                    IsMdiContainer = true;
                }

                RegisterMdiChildWithDocking(reportViewer);

                try
                {
                    if (!reportViewer.IsMdiChild)
                    {
                        reportViewer.MdiParent = this;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to assign MdiParent to ReportViewerForm");
                }

                reportViewer.Show();
                return;
            }

            reportViewer.Show(this);
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
            this.ThemeName = _uiConfig.DefaultTheme;
            // Theme inherited from ApplicationVisualTheme set in Program.InitializeTheme()
            // SfSkinManager.SetVisualStyle(this, _uiConfig.DefaultTheme); // REMOVED: Redundant with global theme
            _logger.LogDebug("Theme inherited from ApplicationVisualTheme: {Theme}", _uiConfig.DefaultTheme);
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

        // IViewManager implementation
        public void ShowView<TForm, TViewModel>(bool allowMultiple = false)
            where TForm : Form
            where TViewModel : class
        {
            ShowChildForm<TForm, TViewModel>(allowMultiple);
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
        private void OnApplicationFontChanged(object? sender, Services.FontChangedEventArgs e)
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

                // Dispose other resources
                components?.Dispose();
                _initializationCts?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
