using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Factories;

#pragma warning disable CS8604

namespace WileyWidget.WinForms.Forms
{
    internal static class MainFormResources
    {
        public const string FormTitle = "Wiley Widget - Municipal Budget Management System";
        public const string ApplicationVersion = "1.0.0";
        public const string LoadingText = "Loading...";
    }

    public partial class MainForm : RibbonForm, IAsyncInitializable
    {
        private static readonly TimeSpan UiProbeStartupWarmup = TimeSpan.FromSeconds(30);
        private const double UiProbeElevatedLatencyDebugThresholdMs = 500;
        private const double UiProbeHighLatencyWarningThresholdMs = 1250;
        private const int WS_EX_COMPOSITED = 0x02000000;
        private const int WM_SETREDRAW = 0x000B;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_FRAME = 0x0400;

        // Core services — _panelNavigator (IPanelNavigationService) is declared in Navigation partial;
        private IServiceProvider? _serviceProvider;
        private IThemeService? _themeService;
        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        private Serilog.ILogger? _asyncLogger;
        private IWindowStateService _windowStateService;
        private IFileImportService _fileImportService;
        private IStatusProgressService? _statusProgressService;
        private EventHandler<StatusProgressUpdate>? _statusProgressChangedHandler;
        private SyncfusionControlFactory? _controlFactory;
        private ContainerControl? _contentHostPanel = null;   // Logical docking host; maps to the form client area when native WinForms MDI owns layout
        private bool _mdiLayoutSyncHooksAttached;
        private bool _dashboardAutoShown;
        private bool _reportViewerLaunched;
        private IServiceScope? _mainViewModelScope;
        private System.Windows.Forms.Timer? _startupFadeTimer;
        private bool _startupFadePrepared;
        private Task? _deferredInitializationTask;

        // MDI constraint coalescing + diagnostics
        private int _mdiConstrainPending;
        private int _mdiConstrainRunning;
        private long _lastMdiConstrainCompletedTimestamp;
        private int _mdiConstrainRequestCount;
        private int _mdiConstrainExecutionCount;
        private int _mdiConstrainSkipCount;
        private Task? _rightDockJarvisInitializationTask;

        // UI responsiveness probe diagnostics
        private System.Threading.Timer? _uiResponsivenessProbeTimer;
        private long _uiProbeSequence;
        private long _uiProbeAckSequence;
        private int _uiProbeTimeoutCount;
        private DateTime _uiProbeStartUtc;

        // UI State
        private UIConfiguration _uiConfig = null!;
        private bool _initialized;

        // Form state
        private readonly ReportViewerLaunchOptions _reportViewerLaunchOptions;

        // Active-grid cache (used by MainForm.Helpers.cs)
        private SfDataGrid? _lastActiveGrid;
        private DateTime _lastActiveGridTime = DateTime.MinValue;
        private readonly TimeSpan _activeGridCacheTtl = TimeSpan.FromMilliseconds(500);

        // Keyboard helpers (used by MainForm.Keyboard.cs)
        private Button? _defaultCancelButton;
        private Panel? _uiAutomationNavigationSpacer;
        private TextBox? _uiAutomationNavigationStatusBox;

        // Document management (used by MainForm.DocumentManagement.cs)
        private TabbedMDIManager? _tabbedMdi;
        private Panel? _rightDockPanel;
        private TabControlAdv? _rightDockTabs;
        private JARVISChatUserControl? _rightDockJarvisPanel;
        private Splitter? _rightDockSplitter;

        // JARVIS auto-hide toggle strip (always visible so user can re-expand)
        private Panel? _jarvisAutoHideStrip;
        private Button? _jarvisAutoHideButton;
        private int _jarvisExpandedWidth = 500;

        // Concrete navigation service — kept alongside the interface field (_panelNavigator in Navigation.cs)
        // so OnShown can call SetTabbedManager() before _panelNavigator is assigned.
        private PanelNavigationService? _panelNavigationService;

        // Component container
        internal System.ComponentModel.IContainer? components;

        // MRU
        private readonly List<string> _mruList = new List<string>();

        public MainViewModel? MainViewModel { get; private set; }

        /// <summary>Logical docking host for layout helpers. When native WinForms MDI is active this maps to the form client area.</summary>
        public ContainerControl? ContentHostPanel => _contentHostPanel;

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool GlobalIsBusy { get; set; }

        protected virtual void OnGlobalIsBusyChanged() { }

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
                catch { }
                return cp;
            }
        }

        public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        private bool TrySuspendRedraw(string phase)
        {
            if (!IsHandleCreated || IsDisposed || Disposing)
            {
                return false;
            }

            try
            {
                SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                _logger?.LogDebug("[{Phase}] WM_SETREDRAW off", phase);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[{Phase}] Failed to suspend redraw", phase);
                return false;
            }
        }

        private void ResumeRedraw(bool redrawSuspended, string phase)
        {
            if (!redrawSuspended || !IsHandleCreated || IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                SendMessage(Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_FRAME);
                _logger?.LogDebug("[{Phase}] WM_SETREDRAW on", phase);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[{Phase}] Failed to resume redraw", phase);
            }
        }

        private void ContentHostPanel_Resize(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            RequestMdiConstrain("ContentHostPanel.Resize");
        }

        private void RightDockPanel_LayoutChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            RequestMdiConstrain("RightDockPanel.LayoutChanged");
        }

        private void EnsureRightDockChromeZOrder()
        {
            var host = (Control)this;
            if (!RightDockChromeNeedsFronting(host))
            {
                return;
            }

            if (_rightDockPanel != null && !_rightDockPanel.IsDisposed && _rightDockPanel.Visible && ReferenceEquals(_rightDockPanel.Parent, host))
            {
                _rightDockPanel.BringToFront();
            }

            if (_rightDockSplitter != null && !_rightDockSplitter.IsDisposed && _rightDockSplitter.Visible && ReferenceEquals(_rightDockSplitter.Parent, host))
            {
                _rightDockSplitter.BringToFront();
            }

            if (_jarvisAutoHideStrip != null && !_jarvisAutoHideStrip.IsDisposed && _jarvisAutoHideStrip.Visible && ReferenceEquals(_jarvisAutoHideStrip.Parent, host))
            {
                _jarvisAutoHideStrip.BringToFront();
            }
        }

        private bool RightDockChromeNeedsFronting(Control host)
        {
            var panelIndex = GetVisibleChildIndex(host, _rightDockPanel);
            var splitterIndex = GetVisibleChildIndex(host, _rightDockSplitter);
            var autoHideIndex = GetVisibleChildIndex(host, _jarvisAutoHideStrip);

            if (splitterIndex != int.MaxValue && panelIndex != int.MaxValue && splitterIndex >= panelIndex)
            {
                return true;
            }

            if (autoHideIndex != int.MaxValue && panelIndex != int.MaxValue && autoHideIndex >= panelIndex)
            {
                return true;
            }

            if (autoHideIndex != int.MaxValue && splitterIndex != int.MaxValue && autoHideIndex >= splitterIndex)
            {
                return true;
            }

            return false;
        }

        private static int GetVisibleChildIndex(Control host, Control? control)
        {
            if (control == null || control.IsDisposed || !control.Visible || !ReferenceEquals(control.Parent, host))
            {
                return int.MaxValue;
            }

            return host.Controls.GetChildIndex(control);
        }

        private void ConfigureStartupRenderingStyles()
        {
            try
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
                DoubleBuffered = true;
                UpdateStyles();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to configure startup rendering styles");
            }
        }

        public MainForm(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<MainForm> logger,
            ReportViewerLaunchOptions reportViewerLaunchOptions,
            IThemeService themeService,
            IWindowStateService windowStateService,
            IFileImportService fileImportService,
            SyncfusionControlFactory controlFactory)
        {
            _serviceProvider = serviceProvider ?? Program.ServicesOrNull ?? Program.CreateFallbackServiceProvider();
            _configuration = configuration;
            _logger = logger;
            _reportViewerLaunchOptions = reportViewerLaunchOptions;
            _themeService = themeService;
            _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
            _fileImportService = fileImportService ?? throw new ArgumentNullException(nameof(fileImportService));
            _controlFactory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
            _statusProgressService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<IStatusProgressService>(_serviceProvider);

            _uiConfig = UIConfiguration.FromConfiguration(configuration);
            ConfigureStartupRenderingStyles();

            var themeName = _themeService?.CurrentTheme ?? Themes.ThemeColors.DefaultTheme;
            _contentHostPanel = this;

            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            Text = MainFormResources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1280, 800);
            StartPosition = FormStartPosition.Manual;

            try
            {
                SfSkinManager.SetVisualStyle(this, themeName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply theme in constructor");
            }

            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

            if (_uiConfig.VerboseFirstChanceExceptions)
            {
                AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;
            }

            Services.FontService.Instance.FontChanged += OnApplicationFontChanged;

            SuspendLayout();
            ResumeLayout(false);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (DesignMode || _initialized) return;

            _initialized = true;

            var onLoadStopwatch = Stopwatch.StartNew();
            var redrawSuspended = TrySuspendRedraw("ONLOAD");
            SuspendLayout();

            try
            {
                _logger?.LogInformation("[ONLOAD] Starting minimal startup path");

                _windowStateService.RestoreWindowState(this);

                _logger?.LogInformation("[ONLOAD] Completed minimal startup path in {ElapsedMs}ms", onLoadStopwatch.ElapsedMilliseconds);
            }
            finally
            {
                ResumeLayout(performLayout: true);
                ResumeRedraw(redrawSuspended, "ONLOAD");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopUiResponsivenessProbe();
                CancelOnShownStartupWorkflow();

                // Unsubscribe from ThemeService to prevent memory leaks (Req 1 — SfSkinManager authority)
                if (_themeService != null)
                    _themeService.ThemeChanged -= OnThemeServiceChanged;

                if (_uiConfig.VerboseFirstChanceExceptions)
                {
                    AppDomain.CurrentDomain.FirstChanceException -= MainForm_FirstChanceException;
                }

                if (_statusProgressService != null && _statusProgressChangedHandler != null)
                {
                    _statusProgressService.ProgressChanged -= _statusProgressChangedHandler;
                    _statusProgressChangedHandler = null;
                }

                try
                {
                    components?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Dispose: components cleanup raised exception");
                }
            }

            try
            {
                base.Dispose(disposing);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("CreateHandle"))
            {
                _logger?.LogDebug(ex, "Dispose: Syncfusion RibbonForm disposal caught expected CreateHandle race");
            }
        }

        /// <summary>
        /// Pauses StatusBar timer when form is deactivated to reduce CPU usage when minimized or backgrounded.
        /// </summary>
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _statusTimer?.Stop();
        }

        /// <summary>
        /// Resumes StatusBar timer when form is activated to restore real-time status updates.
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _statusTimer?.Start();
        }

        /// <summary>
        /// Canonical SafeSuspendAndLayout helper matching ScopedPanelBase's pattern (standards Req 6).
        /// Suspends layout, executes <paramref name="build"/>, then resumes and performs layout.
        /// </summary>
        protected void SafeSuspendAndLayout(System.Action build)
        {
            SuspendLayout();
            try
            {
                build();
            }
            finally
            {
                ResumeLayout(false);
                PerformLayout();
            }
        }

        private static bool IsUiTestEnvironment()
        {
            static bool IsTruthy(string variableName)
            {
                var value = Environment.GetEnvironmentVariable(variableName);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
            }

            return IsTruthy("WILEYWIDGET_UI_TESTS")
                || IsTruthy("WILEYWIDGET_TESTS")
                || IsTruthy("DOTNET_RUNNING_IN_TEST")
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VSTEST_SESSION_ID"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XUNIT_TESTRUNNING"));
        }

        /// <summary>
        /// Returns <see langword="true" /> when the child process was launched explicitly for
        /// FlaUI UI-automation (i.e. <c>WILEYWIDGET_UI_AUTOMATION=true</c>).
        /// UI-automation runs need a full, live UI so this
        /// flag overrides the test-runtime skip-guard.
        /// </summary>
        private static bool IsUiAutomationMode() =>
            string.Equals(
                Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns <see langword="true" /> when any test-runtime signal is active: the explicit harness
        /// config flag, a detected test-runner environment variable (via <see cref="IsUiTestEnvironment"/>),
        /// or the <c>WILEYWIDGET_TESTS</c> env var. This is the single canonical check used to gate
        /// UI test-runtime behavior (nav strip creation, ribbon visual attach, etc.) so all guards
        /// remain in sync.
        /// </summary>
        private bool IsEffectivelyUiTestRuntime() =>
            _uiConfig.IsUiTestHarness
            || IsUiTestEnvironment()
            || string.Equals(
                Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        private static bool IsSupportedDroppedFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            try
            {
                var extension = Path.GetExtension(filePath);
                return string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasSupportedDroppedFile(string[] files)
        {
            foreach (var file in files)
            {
                if (IsSupportedDroppedFile(file))
                {
                    return true;
                }
            }

            return false;
        }

        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
                {
                    e.Effect = DragDropEffects.None;
                    return;
                }

                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0)
                {
                    e.Effect = DragDropEffects.None;
                    return;
                }

                e.Effect = HasSupportedDroppedFile(files)
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "DragEnter validation failed");
                e.Effect = DragDropEffects.None;
            }
        }

        private async void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
                {
                    return;
                }

                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0)
                {
                    return;
                }

                var supportedFiles = new List<string>(files.Length);
                foreach (var file in files)
                {
                    if (IsSupportedDroppedFile(file))
                    {
                        supportedFiles.Add(file);
                    }
                }

                if (supportedFiles.Count == 0)
                {
                    ApplyStatus("Drop ignored: no supported file types found.");
                    ShowErrorDialog("Unsupported File Type", "Dropped files must be CSV, XLSX, XLS, JSON, or XML.");
                    return;
                }

                ApplyStatus($"Importing {supportedFiles.Count} dropped file(s)...");
                await ProcessDroppedFiles(supportedFiles.ToArray(), CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DragDrop processing failed");
                ShowErrorDialog("Drag & Drop Error", "An error occurred while processing dropped files.", ex);
            }
        }
        private void MainForm_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (!_uiConfig.VerboseFirstChanceExceptions)
            {
                return;
            }

            var exception = e.Exception;
            if (exception == null)
            {
                return;
            }

            if (IsThemeInfrastructureException(exception))
            {
                _logger?.LogDebug(exception, "Ignored theme infrastructure first-chance exception");
                return;
            }

            if (IsExpectedBackgroundTransportException(exception))
            {
                _logger?.LogDebug(exception, "Ignored expected HTTP resilience first-chance exception");
                return;
            }

            _logger?.LogError(exception, "Unhandled first-chance exception observed on UI path");
        }

        private static bool IsThemeInfrastructureException(Exception exception)
        {
            static bool MatchesThemeMarker(string? value)
            {
                return !string.IsNullOrWhiteSpace(value)
                    && (value.Contains("theme", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("SfSkinManager", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase));
            }

            for (var current = exception; current != null; current = current.InnerException)
            {
                if (MatchesThemeMarker(current.Message)
                    || MatchesThemeMarker(current.Source)
                    || MatchesThemeMarker(current.StackTrace)
                    || MatchesThemeMarker(current.GetType().FullName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExpectedBackgroundTransportException(Exception exception)
        {
            static bool MatchesTransportMarker(string? value)
            {
                return !string.IsNullOrWhiteSpace(value)
                    && (value.Contains("System.Net.Http", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("SslStream", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("HttpConnection", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("Polly", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("Resilience", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("Grok", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("Socket", StringComparison.OrdinalIgnoreCase));
            }

            for (var current = exception; current != null; current = current.InnerException)
            {
                var expectedType = current is System.Threading.Tasks.TaskCanceledException
                    || current is OperationCanceledException
                    || current is HttpRequestException
                    || current is IOException
                    || current is System.Net.Sockets.SocketException
                    || string.Equals(current.GetType().FullName, "Polly.Timeout.TimeoutRejectedException", StringComparison.Ordinal);

                if (expectedType
                    && (MatchesTransportMarker(current.Message)
                        || MatchesTransportMarker(current.Source)
                        || MatchesTransportMarker(current.StackTrace)
                        || MatchesTransportMarker(current.GetType().FullName)))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnApplicationFontChanged(object? sender, Services.FontChangedEventArgs e) { this.Font = e.NewFont; }

        // ---------------------------------------------------------------------------
        // Core command implementations referenced by ribbon, layout, search, and keyboard
        // ---------------------------------------------------------------------------

        /// <summary>Opens the budget management workspace for creating new entries.</summary>
        private void CreateNewBudget()
        {
            try
            {
                ShowPanel<BudgetPanel>("Budget Management & Analysis", DockingStyle.Right, allowFloating: true);
                ApplyStatus("Budget workspace opened.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateNewBudget failed");
                ShowErrorDialog("New Budget", "Unable to open Budget Management workspace.", ex);
            }
        }

        /// <summary>Opens and imports one or more supported budget source files.</summary>
        private async void OpenBudget()
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Open Budget Data",
                    Filter = "Supported Files (*.csv;*.xlsx;*.xls;*.json;*.xml)|*.csv;*.xlsx;*.xls;*.json;*.xml|CSV Files (*.csv)|*.csv|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|JSON Files (*.json)|*.json|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = true,
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
                {
                    return;
                }

                ApplyStatus($"Importing {dialog.FileNames.Length} file(s)...");
                await ProcessDroppedFiles(dialog.FileNames, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OpenBudget failed");
                ShowErrorDialog("Open Budget", "Unable to open selected budget file(s).", ex);
            }
        }

        /// <summary>Persists the current workspace and window layout.</summary>
        protected void SaveCurrentLayout()
        {
            try
            {
                SaveWorkspaceLayout();
                _windowStateService.SaveWindowState(this);
                ApplyStatus("Layout saved.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveCurrentLayout failed");
                ShowErrorDialog("Save Layout", "Unable to save current layout.", ex);
            }
        }

        /// <summary>Exports the active grid to Excel.</summary>
        private void ExportData()
        {
            try
            {
                _ = ExportActiveGridToExcel(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ExportData failed");
                ShowErrorDialog("Export Data", "Unable to export active data.", ex);
            }
        }

        /// <summary>Resets the workspace layout to defaults.</summary>
        protected void ResetLayout()
        {
            try
            {
                ResetLayoutToDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ResetLayout failed");
                ShowErrorDialog("Reset Layout", "Unable to reset layout to defaults.", ex);
            }
        }

        /// <summary>Gets an availability-based count for Quick Access Toolbar items.</summary>
        protected int GetQATItemCount()
        {
            try
            {
                if (_ribbon == null || _ribbon.IsDisposed)
                {
                    return 0;
                }

                return _ribbon.QuickPanelVisible ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Toggles panel layout customization for tabbed document workspace.</summary>
        private void TogglePanelLocking()
        {
            try
            {
                InitializeMDIManager();
                if (_tabbedMdi == null)
                {
                    ApplyStatus("Panel lock unavailable.");
                    return;
                }

                var currentlyLocked = !_tabbedMdi.AllowTabGroupCustomizing;
                _tabbedMdi.AllowTabGroupCustomizing = currentlyLocked;
                var nowLocked = !_tabbedMdi.AllowTabGroupCustomizing;

                ApplyStatus(nowLocked ? "Panel layout locked." : "Panel layout unlocked.");
                _logger?.LogInformation("Panel layout lock toggled. Locked={Locked}", nowLocked);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "TogglePanelLocking failed");
                ShowErrorDialog("Panel Lock", "Unable to toggle panel locking.", ex);
            }
        }

        /// <summary>
        /// Recursively walks <paramref name="root"/> calling
        /// <see cref="WileyWidget.WinForms.Controls.Base.ScopedPanelBase.TriggerForceFullLayout"/>
        /// on every ready <see cref="WileyWidget.WinForms.Controls.Base.ScopedPanelBase"/> found.
        /// Returns <see langword="true"/> when at least one panel was triggered.
        /// </summary>
        private static bool TryForceLayoutOnScopedPanelChildren(Control root)
        {
            bool found = false;
            foreach (Control child in root.Controls)
            {
                if (child is WileyWidget.WinForms.Controls.Base.ScopedPanelBase spb
                    && !spb.IsDisposed && spb.IsHandleCreated)
                {
                    spb.TriggerForceFullLayout();
                    found = true;
                }
                else if (child.Controls.Count > 0)
                {
                    found |= TryForceLayoutOnScopedPanelChildren(child);
                }
            }
            return found;
        }

        /// <summary>Recursively calls PerformLayout on <paramref name="ctl"/> and all descendants.</summary>
        private static void PerformLayoutRecursive(Control ctl)
        {
            ctl.PerformLayout();
            foreach (Control child in ctl.Controls)
                PerformLayoutRecursive(child);
        }

        private MdiClient? GetMdiClientControl()
        {
            foreach (Control child in Controls)
            {
                if (child is MdiClient mdiClient)
                {
                    return mdiClient;
                }
            }

            return null;
        }

        // Last-logged MdiClient bounds — used to suppress duplicate log lines on every resize.
        private Rectangle _lastLoggedMdiClientBounds = Rectangle.Empty;

        private void RequestMdiConstrain(string reason, bool force = false)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            Interlocked.Increment(ref _mdiConstrainRequestCount);

            if (!force)
            {
                var lastCompletion = Interlocked.Read(ref _lastMdiConstrainCompletedTimestamp);
                if (lastCompletion > 0)
                {
                    var elapsed = Stopwatch.GetElapsedTime(lastCompletion);
                    if (elapsed < TimeSpan.FromMilliseconds(12))
                    {
                        var skipped = Interlocked.Increment(ref _mdiConstrainSkipCount);
                        if (skipped % 100 == 0)
                        {
                            _logger?.LogDebug(
                                "[MDI-CONSTRAIN] Skipped {Skipped} rapid requests (latest reason={Reason})",
                                skipped,
                                reason);
                        }
                        return;
                    }
                }

                if (Interlocked.Exchange(ref _mdiConstrainPending, 1) == 1)
                {
                    return;
                }
            }

            if (!force && !InvokeRequired)
            {
                ConstrainMdiClientToContentHost(reason);
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    Interlocked.Exchange(ref _mdiConstrainPending, 0);
                    ConstrainMdiClientToContentHost(reason);
                }));
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _mdiConstrainPending, 0);
                _logger?.LogDebug(ex, "[MDI-CONSTRAIN] Failed to queue constrain request ({Reason})", reason);
            }
        }

        private void StartUiResponsivenessProbe()
        {
            if (_uiResponsivenessProbeTimer != null || IsDisposed || Disposing || IsUiTestEnvironment())
            {
                return;
            }

            _uiProbeStartUtc = DateTime.UtcNow;
            _uiProbeSequence = 0;
            _uiProbeAckSequence = 0;
            _uiProbeTimeoutCount = 0;

            _uiResponsivenessProbeTimer = new System.Threading.Timer(
                static state =>
                {
                    if (state is MainForm form)
                    {
                        form.QueueUiResponsivenessProbe();
                    }
                },
                this,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2));

            _logger?.LogInformation("[UI-PROBE] Started UI responsiveness probe (interval=2s timeout=3s)");
        }

        private void StopUiResponsivenessProbe()
        {
            if (_uiResponsivenessProbeTimer == null)
            {
                return;
            }

            try
            {
                _uiResponsivenessProbeTimer.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[UI-PROBE] Failed to dispose responsiveness probe timer");
            }
            finally
            {
                _uiResponsivenessProbeTimer = null;
                _logger?.LogInformation(
                    "[UI-PROBE] Stopped (timeouts={Timeouts}, probes={Probes}, uptimeMs={UptimeMs})",
                    _uiProbeTimeoutCount,
                    _uiProbeSequence,
                    (DateTime.UtcNow - _uiProbeStartUtc).TotalMilliseconds);
            }
        }

        private void QueueUiResponsivenessProbe()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            var sequence = Interlocked.Increment(ref _uiProbeSequence);
            var queuedAt = Stopwatch.GetTimestamp();

            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    var latencyMs = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds;
                    Interlocked.Exchange(ref _uiProbeAckSequence, sequence);
                    var inStartupWarmup = DateTime.UtcNow - _uiProbeStartUtc < UiProbeStartupWarmup;

                    if (latencyMs >= UiProbeHighLatencyWarningThresholdMs)
                    {
                        if (inStartupWarmup)
                        {
                            _logger?.LogDebug(
                                "[UI-PROBE] Startup warm-up callback latency {LatencyMs:F0}ms (seq={Sequence}, ack={Ack})",
                                latencyMs,
                                sequence,
                                _uiProbeAckSequence);
                        }
                        else
                        {
                            _logger?.LogWarning(
                                "[UI-PROBE] High UI callback latency {LatencyMs:F0}ms (seq={Sequence}, ack={Ack})",
                                latencyMs,
                                sequence,
                                _uiProbeAckSequence);
                        }
                    }
                    else if (latencyMs >= UiProbeElevatedLatencyDebugThresholdMs)
                    {
                        _logger?.LogDebug("[UI-PROBE] Elevated callback latency {LatencyMs:F0}ms (seq={Sequence})", latencyMs, sequence);
                    }
                }));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[UI-PROBE] Failed to queue UI probe callback (seq={Sequence})", sequence);
                return;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(3000).ConfigureAwait(false);
                if (IsDisposed || Disposing)
                {
                    return;
                }

                var acked = Interlocked.Read(ref _uiProbeAckSequence);
                if (acked >= sequence)
                {
                    return;
                }

                var timeouts = Interlocked.Increment(ref _uiProbeTimeoutCount);
                _logger?.LogWarning(
                    "[UI-PROBE] UI thread did not service probe within 3000ms (seq={Sequence}, ack={Acked}, timeouts={Timeouts})",
                    sequence,
                    acked,
                    timeouts);
            });
        }

        private void ConstrainMdiClientToContentHost(string reason = "direct")
        {
            if (Interlocked.Exchange(ref _mdiConstrainRunning, 1) == 1)
            {
                var skipped = Interlocked.Increment(ref _mdiConstrainSkipCount);
                if (skipped % 100 == 0)
                {
                    _logger?.LogDebug("[MDI-CONSTRAIN] Skipped overlapping invocation count={Skipped}", skipped);
                }
                return;
            }

            var constrainStopwatch = Stopwatch.StartNew();
            try
            {
                if (!IsMdiContainer)
                {
                    return;
                }

                var mdiClient = GetMdiClientControl();
                if (mdiClient == null || mdiClient.IsDisposed)
                {
                    return;
                }

                if (!IsHandleCreated)
                {
                    return;
                }

                var dockWasReset = mdiClient.Dock != DockStyle.Fill;
                if (dockWasReset)
                {
                    mdiClient.Dock = DockStyle.Fill;
                }

                try
                {
                    EnsureChromeZOrder();
                    EnsureRightDockChromeZOrder();
                }
                catch
                {
                    // Non-critical during layout/dispose races.
                }

                if (dockWasReset || mdiClient.Bounds != _lastLoggedMdiClientBounds)
                {
                    _lastLoggedMdiClientBounds = mdiClient.Bounds;
                    _logger?.LogDebug(
                        "[MDI-CONSTRAIN] dockReset={DockReset} MdiClient.Dock={Dock} MdiClient.Bounds={Bounds} DPI={Dpi}",
                        dockWasReset,
                        mdiClient.Dock,
                        mdiClient.Bounds,
                        DeviceDpi);
                }

            }
            finally
            {
                constrainStopwatch.Stop();
                Interlocked.Exchange(ref _lastMdiConstrainCompletedTimestamp, Stopwatch.GetTimestamp());
                var executed = Interlocked.Increment(ref _mdiConstrainExecutionCount);
                if (constrainStopwatch.ElapsedMilliseconds >= 35 || executed % 250 == 0)
                {
                    _logger?.LogInformation(
                        "[MDI-CONSTRAIN] Completed in {ElapsedMs}ms (reason={Reason}, requested={Requested}, executed={Executed}, skipped={Skipped})",
                        constrainStopwatch.ElapsedMilliseconds,
                        reason,
                        _mdiConstrainRequestCount,
                        executed,
                        _mdiConstrainSkipCount);
                }

                Interlocked.Exchange(ref _mdiConstrainRunning, 0);
            }
        }

        /// <summary>Performs a global search across panels and indexed data.</summary>
        public async Task PerformGlobalSearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            try
            {
                _logger?.LogDebug("PerformGlobalSearchAsync: {Query}", query);

                if (MainViewModel?.GlobalSearchCommand != null)
                {
                    await MainViewModel.GlobalSearchCommand.ExecuteAsync(query).ConfigureAwait(true);
                }

                if (_searchDialog != null && !_searchDialog.IsDisposed)
                {
                    await PerformGlobalSearchDialogAsync(query).ConfigureAwait(true);
                }

                ApplyStatus($"Search completed: {query}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PerformGlobalSearchAsync failed for query {Query}", query);
                ShowErrorDialog("Global Search", "Search could not be completed.", ex);
            }
        }

        /// <summary>
        /// Assigns the typed status-bar panels into fields after factory construction.
        /// Chrome partial calls this once the factory has created all panels.
        /// </summary>
        private void SetStatusBarPanels(
            StatusBarAdv statusBar,
            StatusBarAdvPanel statusLabel,
            StatusBarAdvPanel statusTextPanel,
            StatusBarAdvPanel statePanel,
            StatusBarAdvPanel progressPanel,
            Syncfusion.Windows.Forms.Tools.ProgressBarAdv progressBar,
            StatusBarAdvPanel clockPanel)
        {
            // Fields are already assigned by Chrome.cs directly; this method serves
            // as a single validation / hook point if additional logic is needed.
            ConfigureStatusProgressBinding();
            _logger?.LogDebug("SetStatusBarPanels called — all panels assigned");
        }

    }
}
