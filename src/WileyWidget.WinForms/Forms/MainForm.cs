using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
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
    public partial class MainForm : Form, IViewManager
    {
        private static int _inFirstChanceHandler = 0;
        private IServiceProvider? _serviceProvider;
        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        private MenuStrip? _menuStrip;
        private RibbonControlAdv? _ribbon;
        private ToolStripTabItem? _homeTab;
        private ToolStripEx? _navigationStrip;
        private StatusBarAdv? _statusBar;
        private StatusBarAdvPanel? _statusLabel;
        private StatusBarAdvPanel? _statusTextPanel;
        private StatusBarAdvPanel? _statePanel;
        private StatusBarAdvPanel? _clockPanel;
        private System.Windows.Forms.Timer? _statusTimer;
        private bool _dashboardAutoShown;
        private bool _isUiTestHarness;
        private bool _syncfusionDockingInitialized;
        private bool _initialized;
        private Control? _aiChatControl;
        private Panel? _aiChatPanel;
        private System.ComponentModel.IContainer? components;
        // Dashboard description labels are declared in docking partial

        public MainForm()
            : this(
                Program.Services ?? new ServiceCollection().BuildServiceProvider(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? new ConfigurationBuilder().Build(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(Program.Services ?? new ServiceCollection().BuildServiceProvider()) ?? NullLogger<MainForm>.Instance)
        {
        }

        public MainForm(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<MainForm> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            // Load UI configuration
            var uiMode = _configuration.GetValue<string>("UI:UIMode");
            _useSyncfusionDocking = _configuration.GetValue<bool>("UI:UseDockingManager", true);
            _useMdiMode = _configuration.GetValue<bool>("UI:UseMdiMode", true);
            _useTabbedMdi = _configuration.GetValue<bool>("UI:UseTabbedMdi", true);
            _isUiTestHarness = _configuration.GetValue<bool>("UI:IsUiTestHarness", false);
                if (_isUiTestHarness)
            {
                _useMdiMode = false;
                _useTabbedMdi = false;
                _useSyncfusionDocking = true; // Keep docking enabled even for UI tests
                IsMdiContainer = false;
                _logger.LogInformation("UI test harness detected; disabling MDI but keeping Syncfusion docking enabled");
            }

            _logger.LogInformation("UI Config loaded: UIMode={UIMode}, UseDockingManager={Docking}, UseMdiMode={Mdi}, UseTabbedMdi={Tabbed}",
                uiMode ?? "IndividualSettings", _useSyncfusionDocking, _useMdiMode, _useTabbedMdi);

            if (_useMdiMode)
            {
                IsMdiContainer = true;
            }

            ValidateAndSanitizeUiConfiguration();
            InitializeMdiSupport();
            UpdateStateText();

            // Add FirstChanceException handlers for comprehensive error logging
            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;
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

                // If logger is not available, fallback to console to avoid invoking logging pipeline
                bool usedConsoleFallback = false;

                try
                {
                    // Log theme-related exceptions
                    if (ex.Source?.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) == true ||
                        ex.Message.Contains("theme", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Office2019", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("SkinManager", StringComparison.OrdinalIgnoreCase))
                    {
                        if (logger != null)
                            logger.LogDebug(ex, "First-chance theme exception detected: {Message}", ex.Message);
                        else
                        {
                            Console.WriteLine($"First-chance theme exception detected: {ex.Message}");
                            usedConsoleFallback = true;
                        }
                    }

                    // Log docking-related exceptions
                    if (ex.Source?.Contains("DockingManager", StringComparison.OrdinalIgnoreCase) == true ||
                        ex.Message.Contains("dock", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("DockingManager", StringComparison.OrdinalIgnoreCase))
                    {
                        if (logger != null)
                            logger.LogDebug(ex, "First-chance docking exception detected: {Message}", ex.Message);
                        else
                        {
                            Console.WriteLine($"First-chance docking exception detected: {ex.Message}");
                            usedConsoleFallback = true;
                        }
                    }

                    // Log MDI-related exceptions
                    if (ex.Message.Contains("MDI", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("Mdi", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("IsMdiContainer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (logger != null)
                            logger.LogDebug(ex, "First-chance MDI exception detected: {Message}", ex.Message);
                        else
                        {
                            Console.WriteLine($"First-chance MDI exception detected: {ex.Message}");
                            usedConsoleFallback = true;
                        }
                    }
                }
                catch (Exception logEx)
                {
                    // Avoid logging inside the exception handler as this can create a recursive loop
                    try
                    {
                        if (!usedConsoleFallback)
                        {
                            Console.WriteLine($"Exception in FirstChanceException handler: {logEx}");
                        }
                        else
                        {
                            // If we already used console fallback, just write minimal info
                            Console.WriteLine("Exception in FirstChanceException handler while handling FCE");
                        }
                    }
                    catch { }
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
                _useMdiMode = false;
                _useTabbedMdi = false;
                _useSyncfusionDocking = false;
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

            // Deferred docking initialization
            if (!_syncfusionDockingInitialized && _useSyncfusionDocking)
            {
                try
                {
                    InitializeSyncfusionDocking();
                    _syncfusionDockingInitialized = true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Deferred DockingManager initialization in OnLoad failed");
                    _useSyncfusionDocking = false;
                }
            }

            UpdateStateText();

            try
            {
                // Z-order: MDI client first
                if (_useMdiMode && IsMdiContainer)
                {
                    var mdiClient = Controls.OfType<MdiClient>().FirstOrDefault();
                    if (mdiClient != null)
                    {
                        mdiClient.Dock = DockStyle.Fill;
                        mdiClient.SendToBack();
                        _logger?.LogDebug("MDI client configured and sent to back");
                    }
                }

                // Ribbon above chrome
                if (_ribbon != null)
                {
                    _ribbon.BringToFront();
                    _logger?.LogDebug("Ribbon brought to front");
                }

                // Status bar above panels
                if (_statusBar != null)
                {
                    _statusBar.BringToFront();
                    _logger?.LogDebug("Status bar brought to front");
                }

                if (_useSyncfusionDocking)
                {
                    try { EnsureDockingZOrder(); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to ensure docking z-order in OnLoad"); }
                }
                else
                {
                    try { EnsureNonDockingVisibility(); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to ensure non-docking visibility in OnLoad"); }
                }

                this.Refresh();
                this.Invalidate();
                _logger?.LogDebug("Z-order management completed successfully");

                // Background simulation: save docking layout
                Task.Run(() =>
                {
                    try
                    {
                        Thread.Sleep(500);
                        ApplyStatus("Background load complete");

                        if (_useSyncfusionDocking)
                        {
                            try
                            {
                                if (this.InvokeRequired)
                                {
                                    try
                                    {
                                        this.BeginInvoke(new System.Action(() =>
                                        {
                                            try { SaveDockingLayout(); }
                                            catch (Exception ex) { _logger?.LogWarning(ex, "Background simulated SaveDockingLayout failed"); }
                                        }));
                                    }
                                    catch (Exception ex) { _logger?.LogWarning(ex, "Failed to BeginInvoke background docking save"); }
                                }
                                else
                                {
                                    SaveDockingLayout();
                                }
                            }
                            catch (Exception ex) { _logger?.LogWarning(ex, "Background docking save simulation failed"); }
                        }
                    }
                    catch (Exception ex) { _logger?.LogDebug(ex, "Background simulation failed"); }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Z-order management failed in OnLoad - controls may overlap");
            }

            if (!_dashboardAutoShown)
            {
                try { ShowChildForm<DashboardForm, DashboardViewModel>(allowMultiple: false); _dashboardAutoShown = true; }
                catch (Exception ex) { _logger?.LogWarning(ex, "Failed to auto-open Dashboard on startup"); }
            }

            try { EnsureCentralPanelVisibility(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Failed to ensure central panel visibility after dashboard open"); }
        }

        private void ValidateAndSanitizeUiConfiguration()
        {
            // Validate UIMode
            var uiMode = _configuration.GetValue<string>("UI:UIMode");
            if (!string.IsNullOrEmpty(uiMode))
            {
                var lowerUiMode = uiMode.ToLowerInvariant();
                if (lowerUiMode != "dockingonly" && lowerUiMode != "mdionly" && lowerUiMode != "tabbedmdionly")
                {
                    _logger.LogWarning("Invalid UIMode value '{UIMode}' from configuration. Clearing UIMode to fall back to individual settings.", uiMode);
                    // Reset to individual settings
                    _useSyncfusionDocking = _configuration.GetValue<bool>("UI:UseDockingManager", true);
                    _useMdiMode = _configuration.GetValue<bool>("UI:UseMdiMode", true);
                    _useTabbedMdi = _configuration.GetValue<bool>("UI:UseTabbedMdi", true);
                }
            }

            // Rule 1: TabbedMdi requires MdiMode to be enabled
            if (_useTabbedMdi && !_useMdiMode)
            {
                _logger.LogWarning("Invalid UI configuration: UseTabbedMdi=true requires UseMdiMode=true. Disabling TabbedMdi.");
                _useTabbedMdi = false;
            }

            // Rule 2: If both MDI modes are disabled, ensure docking is enabled as fallback
            if (!_useMdiMode && !_useTabbedMdi && !_useSyncfusionDocking)
            {
                _logger.LogWarning("Invalid UI configuration: All UI modes disabled. Enabling DockingManager as fallback.");
                _useSyncfusionDocking = true;
            }

            // Rule 3: Ensure IsMdiContainer consistency
            if (_useMdiMode && !IsMdiContainer)
            {
                _logger.LogWarning("UI configuration inconsistency: UseMdiMode=true but IsMdiContainer=false. Setting IsMdiContainer=true.");
                IsMdiContainer = true;
            }
            else if (!_useMdiMode && IsMdiContainer)
            {
                _logger.LogWarning("UI configuration inconsistency: UseMdiMode=false but IsMdiContainer=true. Setting IsMdiContainer=false.");
                IsMdiContainer = false;
            }

            // Log final validated configuration
            _logger.LogInformation("UI configuration validated: Docking={Docking}, MDI={Mdi}, TabbedMDI={Tabbed}",
                _useSyncfusionDocking, _useMdiMode, _useTabbedMdi);
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
                if (!_useSyncfusionDocking || _dockingManager == null)
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
    }
}
