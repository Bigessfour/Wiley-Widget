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
        public const string DocumentationUrl = "https://github.com/WileyWidget/WileyWidget/wiki";
        public const string LoadingText = "Loading...";
    }

    public partial class MainForm : RibbonForm, IAsyncInitializable
    {
        private const int WS_EX_COMPOSITED = 0x02000000;
        private const int WM_SETREDRAW = 0x000B;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_FRAME = 0x0400;
        private static readonly TimeSpan UiProbeWarmupWindow = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan UiProbeWarningCooldown = TimeSpan.FromSeconds(8);

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
        private ContainerControl? _contentHostPanel;   // Sub-container below ribbon, above status bar — houses right-dock panel and MDI client area
        private LocalIdentityHostPanel? _hostedAuthenticationPanel;
        private bool _mdiLayoutSyncHooksAttached;
        private bool _dashboardAutoShown;
        private bool _reportViewerLaunched;
        private IServiceScope? _mainViewModelScope;
        private System.Windows.Forms.Timer? _startupFadeTimer;
        private bool _startupFadePrepared;
        private bool _jarvisStartupAutoOpenCompleted;

        // MDI constraint coalescing + diagnostics
        private int _mdiConstrainPending;
        private int _mdiConstrainRunning;
        private long _lastMdiConstrainCompletedTimestamp;
        private int _mdiConstrainRequestCount;
        private int _mdiConstrainExecutionCount;
        private int _mdiConstrainSkipCount;
        private int _ribbonLayoutSyncQueued;
        private int _ribbonLayoutSyncRequestCount;
        private int _ribbonLayoutSyncExecutionCount;
        private int _ribbonLayoutSyncCoalesceCount;
        private string _pendingRibbonLayoutReason = "Ribbon.Layout";

        // UI responsiveness probe diagnostics
        private System.Threading.Timer? _uiResponsivenessProbeTimer;
        private long _uiProbeSequence;
        private long _uiProbeAckSequence;
        private int _uiProbeTimeoutCount;
        private int _uiProbeConsecutiveHighLatencyCount;
        private int _uiProbeConsecutiveTimeoutCount;
        private long _uiProbeLastWarningTicksUtc;
        private DateTime _uiProbeStartUtc;
        private readonly object _uiProbeOperationGate = new();
        private string _uiProbeCurrentOperation = "idle";
        private long _uiProbeOperationStartTimestamp;

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

        // Document management (used by MainForm.DocumentManagement.cs)
        private TabbedMDIManager? _tabbedMdi;
        private Panel? _rightDockPanel;
        private TabControlAdv? _rightDockTabs;
        private JARVISChatUserControl? _rightDockJarvisPanel;
        private int _rightDockJarvisPanelCreationQueued;
        private object? _pendingRightDockJarvisParameters;
        private Splitter? _rightDockSplitter;

        // JARVIS auto-hide toggle strip (always visible so user can re-expand)
        private Panel? _jarvisAutoHideStrip;
        private Button? _jarvisAutoHideButton;
        private int _jarvisExpandedWidth = 500;
        private int _activityLogExpandedWidth = RightDockPanelFactory.ActivityLogPreferredWidth;
        private bool _isApplyingRightDockWidth;

        // Concrete navigation service — kept alongside the interface field (_panelNavigator in Navigation.cs)
        // so OnShown can call SetTabbedManager() before _panelNavigator is assigned.
        private PanelNavigationService? _panelNavigationService;

        // Component container
        internal System.ComponentModel.IContainer? components;

        // MRU
        private readonly List<string> _mruList = new List<string>();

        public MainViewModel? MainViewModel { get; private set; }

        /// <summary>Sub-container positioned below ribbon and above status bar. Houses the right-dock panel and MDI client area.</summary>
        public ContainerControl? ContentHostPanel => _contentHostPanel;

        // Compatibility alias for legacy MainForm partials/services that still reference _panelHost.
        // The content host is now the canonical panel host container.
        private ContainerControl? _panelHost => _contentHostPanel;

        /// <summary>
        /// Refreshes panel-host/ribbon layout and re-constrains the MDI client region.
        /// This wrapper preserves existing call sites that trigger layout recovery.
        /// </summary>
        internal void RefreshPanelHostLayout(string reason, bool force = false)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            var effectiveReason = string.IsNullOrWhiteSpace(reason)
                ? "RefreshPanelHostLayout"
                : reason;

            if (force)
            {
                SyncContentHostTopInsetToRibbon(effectiveReason);
                RequestMdiConstrain(effectiveReason, force: true);
                return;
            }

            QueueRibbonLayoutSync(effectiveReason);
        }

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

            SyncContentHostTopInsetToRibbon("ContentHostPanel.Resize");
            RequestMdiConstrain("ContentHostPanel.Resize");
        }

        private void SyncContentHostTopInsetToRibbon(string reason)
        {
            if (_contentHostPanel == null || _contentHostPanel.IsDisposed)
            {
                return;
            }

            var baseInset = AppLayoutConstants.ContentHostPadding;
            var topInset = baseInset;

            var contentHostTopInForm = _contentHostPanel.Top;
            if (IsHandleCreated)
            {
                try
                {
                    contentHostTopInForm = PointToClient(_contentHostPanel.PointToScreen(Point.Empty)).Y;
                }
                catch
                {
                    contentHostTopInForm = _contentHostPanel.Top;
                }
            }

            if (_uiConfig.ShowRibbon && _ribbon != null && !_ribbon.IsDisposed && _ribbon.Visible)
            {
                try
                {
                    var ribbonBottomScreen = _ribbon.PointToScreen(new Point(0, _ribbon.Height));
                    var ribbonBottomInForm = PointToClient(ribbonBottomScreen).Y;
                    if (ribbonBottomInForm > 0)
                    {
                        var desiredClientTopInForm = ribbonBottomInForm + baseInset;
                        topInset = Math.Max(baseInset, desiredClientTopInForm - contentHostTopInForm);
                    }
                }
                catch
                {
                    var desiredClientTopInForm = _ribbon.Bottom + baseInset;
                    topInset = Math.Max(baseInset, desiredClientTopInForm - contentHostTopInForm);
                }
            }

            var desiredPadding = new Padding(baseInset, topInset, baseInset, baseInset);
            if (_contentHostPanel.Padding == desiredPadding)
            {
                return;
            }

            _contentHostPanel.SuspendLayout();
            try
            {
                _contentHostPanel.Padding = desiredPadding;
                PerformLayoutRecursive(_contentHostPanel);
                TryForceLayoutOnScopedPanelChildren(_contentHostPanel);
                _contentHostPanel.Invalidate(true);
                _contentHostPanel.Update();
            }
            finally
            {
                _contentHostPanel.ResumeLayout(performLayout: true);
            }

            _logger?.LogDebug("[LAYOUT] ContentHostPanel padding synced ({Reason}) => {Padding}", reason, desiredPadding);
        }

        private void QueueRibbonLayoutSync(string reason)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            var effectiveReason = string.IsNullOrWhiteSpace(reason)
                ? "Ribbon.Layout"
                : reason;

            Interlocked.Increment(ref _ribbonLayoutSyncRequestCount);

            if (!IsHandleCreated)
            {
                SyncContentHostTopInsetToRibbon(effectiveReason);
                RequestMdiConstrain(effectiveReason);
                return;
            }

            _pendingRibbonLayoutReason = effectiveReason;

            if (Interlocked.Exchange(ref _ribbonLayoutSyncQueued, 1) == 1)
            {
                var coalesced = Interlocked.Increment(ref _ribbonLayoutSyncCoalesceCount);
                if (coalesced % 50 == 0)
                {
                    _logger?.LogDebug(
                        "[LAYOUT] Coalesced {Coalesced} queued panel-host layout requests (latest reason={Reason})",
                        coalesced,
                        effectiveReason);
                }

                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    Interlocked.Exchange(ref _ribbonLayoutSyncQueued, 0);

                    if (IsDisposed || Disposing)
                    {
                        return;
                    }

                    var queuedReason = _pendingRibbonLayoutReason;
                    var layoutStopwatch = Stopwatch.StartNew();

                    SyncContentHostTopInsetToRibbon(queuedReason);
                    RequestMdiConstrain(queuedReason);

                    layoutStopwatch.Stop();
                    var executed = Interlocked.Increment(ref _ribbonLayoutSyncExecutionCount);
                    if (layoutStopwatch.ElapsedMilliseconds >= 20 || executed % 25 == 0)
                    {
                        _logger?.LogInformation(
                            "[LAYOUT] Panel-host sync completed in {ElapsedMs}ms (reason={Reason}, requested={Requested}, executed={Executed}, coalesced={Coalesced})",
                            layoutStopwatch.ElapsedMilliseconds,
                            queuedReason,
                            _ribbonLayoutSyncRequestCount,
                            executed,
                            _ribbonLayoutSyncCoalesceCount);
                    }
                }));
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _ribbonLayoutSyncQueued, 0);
                _logger?.LogDebug(ex, "[LAYOUT] Failed to queue ribbon layout sync ({Reason})", effectiveReason);
            }
        }

        private void RightDockPanel_LayoutChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            RequestMdiConstrain("RightDockPanel.LayoutChanged");
        }

        private void ConfigureStartupRenderingStyles()
        {
            try
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
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

            // Create ContentHostPanel early for isolated docking host (prevents empty Controls during init)
            var themeName = _themeService?.CurrentTheme ?? Themes.ThemeColors.DefaultTheme;
            _contentHostPanel = new UserControl
            {
                Name = "ContentHostPanel",
                Dock = DockStyle.Fill,
                Padding = new Padding(AppLayoutConstants.ContentHostPadding),
                TabStop = false,
                Visible = true
            };
            SfSkinManager.SetVisualStyle(_contentHostPanel, themeName);
            Controls.Add(_contentHostPanel);
            _contentHostPanel.Resize -= ContentHostPanel_Resize;
            _contentHostPanel.Resize += ContentHostPanel_Resize;
            SyncContentHostTopInsetToRibbon("Constructor");
            TraceLayoutSnapshot("Constructor.ContentHostCreated");
            _logger?.LogDebug("ContentHostPanel created early in constructor, theme={Theme}", themeName);

            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            Size = new Size(
                (int)DpiAware.LogicalToDeviceUnits(1400f),
                (int)DpiAware.LogicalToDeviceUnits(900f));
            MinimumSize = new Size((int)DpiAware.LogicalToDeviceUnits(1280f), (int)DpiAware.LogicalToDeviceUnits(800f));
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

            // Safety net for UI thread exceptions (e.g., Syncfusion paint crashes)
            Application.ThreadException += (sender, e) =>
            {
                _logger?.LogError(e.Exception, "Unhandled UI thread exception caught: {Message}", e.Exception.Message);
                // Optional: Show non-fatal dialog or continue
                // MessageBox.Show(this, $"UI Error: {e.Exception.Message}\nApp continues.", "Wiley Widget", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            _logger?.LogDebug("Global ThreadException handler wired for UI crashes");

            AppDomain.CurrentDomain.FirstChanceException += MainForm_FirstChanceException;

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
                TraceLayoutSnapshot("OnLoad.BeforeRestoreWindowState");

                _windowStateService.RestoreWindowState(this);
                TraceLayoutSnapshot("OnLoad.AfterRestoreWindowState");

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

                if (_statusProgressService != null && _statusProgressChangedHandler != null)
                {
                    _statusProgressService.ProgressChanged -= _statusProgressChangedHandler;
                    _statusProgressChangedHandler = null;
                }

                DisposeStatusBarTimers();
                DisposeGlobalSearchResources();

                // === FIX: Prevent Dispose race with CreateHandle (Syncfusion v32.2.3 stability) ===
                // If handle creation is in progress, don't dispose yet; let base.Dispose() handle it safely
                if (this.IsHandleCreated && !this.IsDisposed && !this.Disposing)
                {
                    try
                    {
                        // Safe to dispose components now that handle is stable
                        components?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Dispose: components cleanup raised exception (expected in race scenarios)");
                    }
                }
                else if (!this.IsHandleCreated)
                {
                    // Handle not created yet — defer component cleanup to base.Dispose
                    try { components?.Dispose(); } catch { }
                }
            }

            // Let Syncfusion RibbonForm do its dispose LAST (critical for avoiding WndProc race)
            try
            {
                base.Dispose(disposing);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("CreateHandle"))
            {
                // Expected in race condition scenarios; log but don't crash
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
        private void MainForm_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e) { }
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
                    if (elapsed < TimeSpan.FromMilliseconds(40))
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

        internal void ForceMdiConstrain(string reason)
        {
            RequestMdiConstrain(reason, force: true);
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
            _uiProbeConsecutiveHighLatencyCount = 0;
            _uiProbeConsecutiveTimeoutCount = 0;
            _uiProbeLastWarningTicksUtc = 0;
            lock (_uiProbeOperationGate)
            {
                _uiProbeCurrentOperation = "idle";
                _uiProbeOperationStartTimestamp = Stopwatch.GetTimestamp();
            }

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

        private IDisposable BeginUiProbeOperationScope(string operationName)
        {
            var normalizedOperation = string.IsNullOrWhiteSpace(operationName)
                ? "unknown"
                : operationName;

            var startedAt = Stopwatch.GetTimestamp();
            string previousOperation;
            long previousStartTimestamp;

            lock (_uiProbeOperationGate)
            {
                previousOperation = _uiProbeCurrentOperation;
                previousStartTimestamp = _uiProbeOperationStartTimestamp;
                _uiProbeCurrentOperation = normalizedOperation;
                _uiProbeOperationStartTimestamp = startedAt;
            }

            return new UiProbeOperationScope(this, previousOperation, previousStartTimestamp, normalizedOperation, startedAt);
        }

        private (string ActiveOperation, double OperationElapsedMs, string ActivePanel) CaptureUiProbeContext()
        {
            string activeOperation;
            long operationStartTimestamp;

            lock (_uiProbeOperationGate)
            {
                activeOperation = _uiProbeCurrentOperation;
                operationStartTimestamp = _uiProbeOperationStartTimestamp;
            }

            var operationElapsedMs = operationStartTimestamp > 0
                ? Stopwatch.GetElapsedTime(operationStartTimestamp).TotalMilliseconds
                : 0d;

            var activePanel = "<none>";
            try
            {
                activePanel = _panelNavigator?.GetActivePanelName() ?? "<none>";
            }
            catch
            {
                activePanel = "<unavailable>";
            }

            return (activeOperation, operationElapsedMs, activePanel);
        }

        private void EndUiProbeOperationScope(string previousOperation, long previousStartTimestamp, string completedOperation, long completedStartTimestamp)
        {
            lock (_uiProbeOperationGate)
            {
                _uiProbeCurrentOperation = previousOperation;
                _uiProbeOperationStartTimestamp = previousStartTimestamp;
            }

            var elapsedMs = Stopwatch.GetElapsedTime(completedStartTimestamp).TotalMilliseconds;
            if (elapsedMs >= 800)
            {
                _logger?.LogWarning("[UI-PROBE] Slow UI operation {Operation} took {ElapsedMs:F0}ms", completedOperation, elapsedMs);
            }
        }

        private sealed class UiProbeOperationScope : IDisposable
        {
            private readonly MainForm _owner;
            private readonly string _previousOperation;
            private readonly long _previousStartTimestamp;
            private readonly string _completedOperation;
            private readonly long _completedStartTimestamp;
            private int _disposed;

            public UiProbeOperationScope(MainForm owner, string previousOperation, long previousStartTimestamp, string completedOperation, long completedStartTimestamp)
            {
                _owner = owner;
                _previousOperation = previousOperation;
                _previousStartTimestamp = previousStartTimestamp;
                _completedOperation = completedOperation;
                _completedStartTimestamp = completedStartTimestamp;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                _owner.EndUiProbeOperationScope(
                    _previousOperation,
                    _previousStartTimestamp,
                    _completedOperation,
                    _completedStartTimestamp);
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
                    Interlocked.Exchange(ref _uiProbeConsecutiveTimeoutCount, 0);
                    var isWarmupWindow = IsUiProbeWarmupWindowActive();

                    if (latencyMs >= 750)
                    {
                        if (isWarmupWindow)
                        {
                            _logger?.LogDebug(
                                "[UI-PROBE] Warm-up high UI callback latency {LatencyMs:F0}ms (seq={Sequence}, ack={Ack}, warmupSeconds={WarmupSeconds})",
                                latencyMs,
                                sequence,
                                _uiProbeAckSequence,
                                UiProbeWarmupWindow.TotalSeconds);
                        }
                        else
                        {
                            var consecutiveHighLatency = Interlocked.Increment(ref _uiProbeConsecutiveHighLatencyCount);
                            if (consecutiveHighLatency >= 2 && ShouldEmitUiProbeWarning())
                            {
                                var context = CaptureUiProbeContext();
                                _logger?.LogWarning(
                                    "[UI-PROBE] High UI callback latency {LatencyMs:F0}ms (seq={Sequence}, ack={Ack}, consecutive={Consecutive}, activeOp={ActiveOperation}, activePanel={ActivePanel}, opElapsedMs={OperationElapsedMs:F0})",
                                    latencyMs,
                                    sequence,
                                    _uiProbeAckSequence,
                                    consecutiveHighLatency,
                                    context.ActiveOperation,
                                    context.ActivePanel,
                                    context.OperationElapsedMs);
                            }
                            else
                            {
                                _logger?.LogDebug(
                                    "[UI-PROBE] Suppressed transient high latency {LatencyMs:F0}ms (seq={Sequence}, consecutive={Consecutive})",
                                    latencyMs,
                                    sequence,
                                    consecutiveHighLatency);
                            }
                        }
                    }
                    else if (latencyMs >= 250)
                    {
                        Interlocked.Exchange(ref _uiProbeConsecutiveHighLatencyCount, 0);
                        _logger?.LogDebug("[UI-PROBE] Elevated callback latency {LatencyMs:F0}ms (seq={Sequence})", latencyMs, sequence);
                    }
                    else
                    {
                        Interlocked.Exchange(ref _uiProbeConsecutiveHighLatencyCount, 0);
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
                if (IsUiProbeWarmupWindowActive())
                {
                    _logger?.LogDebug(
                        "[UI-PROBE] Warm-up probe timeout after 3000ms (seq={Sequence}, ack={Acked}, timeouts={Timeouts}, warmupSeconds={WarmupSeconds})",
                        sequence,
                        acked,
                        timeouts,
                        UiProbeWarmupWindow.TotalSeconds);
                }
                else
                {
                    var consecutiveTimeouts = Interlocked.Increment(ref _uiProbeConsecutiveTimeoutCount);
                    if (consecutiveTimeouts >= 2 && ShouldEmitUiProbeWarning())
                    {
                        var context = CaptureUiProbeContext();
                        _logger?.LogWarning(
                            "[UI-PROBE] UI thread did not service probe within 3000ms (seq={Sequence}, ack={Acked}, timeouts={Timeouts}, consecutive={Consecutive}, activeOp={ActiveOperation}, activePanel={ActivePanel}, opElapsedMs={OperationElapsedMs:F0})",
                            sequence,
                            acked,
                            timeouts,
                            consecutiveTimeouts,
                            context.ActiveOperation,
                            context.ActivePanel,
                            context.OperationElapsedMs);
                    }
                    else
                    {
                        _logger?.LogDebug(
                            "[UI-PROBE] Suppressed transient timeout (seq={Sequence}, ack={Acked}, consecutive={Consecutive})",
                            sequence,
                            acked,
                            consecutiveTimeouts);
                    }
                }
            });
        }

        private bool ShouldEmitUiProbeWarning()
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var lastTicks = Interlocked.Read(ref _uiProbeLastWarningTicksUtc);
            if (lastTicks > 0 && new TimeSpan(nowTicks - lastTicks) < UiProbeWarningCooldown)
            {
                return false;
            }

            Interlocked.Exchange(ref _uiProbeLastWarningTicksUtc, nowTicks);
            return true;
        }

        private bool IsUiProbeWarmupWindowActive()
        {
            if (_uiProbeStartUtc == default)
            {
                return false;
            }

            return (DateTime.UtcNow - _uiProbeStartUtc) < UiProbeWarmupWindow;
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
                if (!IsMdiContainer || _contentHostPanel == null || _contentHostPanel.IsDisposed)
                {
                    return;
                }

                var mdiClient = GetMdiClientControl();
                if (mdiClient == null || mdiClient.IsDisposed)
                {
                    return;
                }

                // Track whether WinForms MDI reset MdiClient.Dock to Fill (diagnostic).
                bool dockWasReset = mdiClient.Dock != DockStyle.None;

                // ── TOP EDGE ────────────────────────────────────────────────────────────────────
                // Use PointToScreen/PointToClient for all measurements to stay immune to
                // coordinate-space differences introduced by DockStyleEx.Top (Syncfusion) vs the
                // standard DockStyle.Top that feeds WinForms dock layout.
                //
                // Strategy: walk every direct form child that is DockStyle.Top and take the
                // maximum bottom edge in form-client coordinates.  This automatically captures the
                // ribbon AND any TabbedMDI tab strip control that Syncfusion inserts at DockStyle.Top
                // above the MDI area — no separate tab-strip measurement is required.
                int topY = 0;
                int ribbonBottom = 0;

                if (!IsHandleCreated)
                {
                    return; // Cannot use PointToScreen/PointToClient without a live HWND.
                }

                // Snapshot Controls to avoid collection-modified exceptions during iteration.
                var formControls = this.Controls.Cast<Control>().ToArray();

                foreach (Control child in formControls)
                {
                    if (child == mdiClient || child.IsDisposed || !child.Visible) continue;
                    if (child.Dock != DockStyle.Top) continue;

                    try
                    {
                        var screenPt = child.PointToScreen(new Point(0, child.Height));
                        var childBottomInForm = this.PointToClient(screenPt).Y;
                        topY = Math.Max(topY, childBottomInForm);

                        if (child == _ribbon)
                            ribbonBottom = childBottomInForm;
                    }
                    catch
                    {
                        // Non-critical — control may not have a live HWND yet; skip.
                    }
                }

                // Fallback: if we found no DockStyle.Top controls (e.g. DockStyleEx.Top used for
                // ribbon and not caught above), use direct PointToScreen on _ribbon.
                if (topY == 0 && _ribbon != null && !_ribbon.IsDisposed)
                {
                    try
                    {
                        var screenPt = _ribbon.PointToScreen(new Point(0, _ribbon.Height));
                        ribbonBottom = this.PointToClient(screenPt).Y;
                        topY = ribbonBottom;
                    }
                    catch
                    {
                        // Swallow — fall through with topY = 0 which is safe (MdiClient kept at top).
                    }
                }

                // ── BOTTOM / LEFT / RIGHT EDGES ─────────────────────────────────────────────────
                // Bottom is driven by ContentHostPanel (status bar docks below it on the form).
                var hostBottom = _contentHostPanel.Bottom;
                var hostLeft = _contentHostPanel.Left;
                var hostRight = _contentHostPanel.Right;

                if (_rightDockPanel != null
                    && !_rightDockPanel.IsDisposed
                    && _rightDockPanel.Visible
                    && (_rightDockPanel.Parent == _contentHostPanel || _rightDockPanel.Parent == this))
                {
                    var rightDockBounds = this.RectangleToClient(_rightDockPanel.RectangleToScreen(_rightDockPanel.ClientRectangle));
                    if (rightDockBounds.Width > 0)
                    {
                        hostRight = Math.Min(hostRight, rightDockBounds.Left);
                    }
                }
                else if (_jarvisAutoHideStrip != null
                         && !_jarvisAutoHideStrip.IsDisposed
                         && _jarvisAutoHideStrip.Visible
                         && (_jarvisAutoHideStrip.Parent == _contentHostPanel || _jarvisAutoHideStrip.Parent == this))
                {
                    // Sidebar is collapsed — reserve the 22 px toggle strip on the right.
                    var stripBounds = this.RectangleToClient(_jarvisAutoHideStrip.RectangleToScreen(_jarvisAutoHideStrip.ClientRectangle));
                    if (stripBounds.Width > 0)
                    {
                        hostRight = Math.Min(hostRight, stripBounds.Left);
                    }
                }

                // Guard against transient handle/layout races where topY can remain 0.
                // The content host is docked below the ribbon/status chrome, so using its
                // top edge as a floor guarantees MDI children never render underneath ribbon.
                var safeTopFloor = _contentHostPanel.Top;
                try
                {
                    if (IsHandleCreated)
                    {
                        // Use the actual content-host client top (includes container padding) to
                        // keep MDI children below ribbon chrome and any top insets.
                        var contentClientTopScreen = _contentHostPanel.PointToScreen(
                            new Point(_contentHostPanel.DisplayRectangle.Left, _contentHostPanel.DisplayRectangle.Top));
                        safeTopFloor = this.PointToClient(contentClientTopScreen).Y;
                    }
                }
                catch
                {
                    safeTopFloor = _contentHostPanel.Top;
                }
                if (_ribbon != null && !_ribbon.IsDisposed)
                {
                    safeTopFloor = Math.Max(safeTopFloor, _ribbon.Bottom + AppLayoutConstants.ContentHostPadding);
                }
                topY = Math.Max(topY, safeTopFloor);

                var targetBounds = new Rectangle(
                    hostLeft,
                    topY,
                    Math.Max(0, hostRight - hostLeft),
                    Math.Max(0, hostBottom - topY));

                if (targetBounds.Width <= 0 || targetBounds.Height <= 0)
                {
                    return;
                }

                if (mdiClient.Dock != DockStyle.None)
                {
                    mdiClient.Dock = DockStyle.None;
                }

                var boundsChanged = mdiClient.Bounds != targetBounds;
                if (boundsChanged)
                {
                    mdiClient.Bounds = targetBounds;
                }

                // During hosted authentication, the content host must own the foreground so the
                // login surface remains visible above the MDI client.
                if (IsHostedAuthenticationPanelActive())
                {
                    EnsureHostedAuthenticationForeground();
                }
                // Ensure MdiClient is in front of ContentHostPanel (which has Dock=Fill and paints
                // over the MDI area when behind MdiClient).  Then immediately re-assert the ribbon
                // on top so its z-order is not displaced on every constrain call.
                else if (boundsChanged || dockWasReset)
                {
                    try
                    {
                        mdiClient.BringToFront();
                        if (_ribbon != null && !_ribbon.IsDisposed)
                            _ribbon.BringToFront();
                    }
                    catch
                    {
                        // Non-critical — ignore z-order failures during form close / layout races.
                    }
                }

                // Diagnostic — log on bounds change or whenever a Dock reset was detected.
                if (targetBounds != _lastLoggedMdiClientBounds || dockWasReset)
                {
                    _lastLoggedMdiClientBounds = targetBounds;
                    _logger?.LogDebug(
                        "[MDI-CONSTRAIN] ribbon.Bottom={RibbonBottom} topY={TopY} " +
                        "contentHost.Top={ContentTop} dockReset={DockReset} " +
                        "MdiClient.Bounds={Bounds} DPI={Dpi}",
                        ribbonBottom, topY,
                        _contentHostPanel?.Top ?? -1,
                        dockWasReset,
                        targetBounds,
                        DeviceDpi);

                    if (dockWasReset || boundsChanged)
                    {
                        TraceLayoutSnapshot($"MdiConstrain.{reason}");
                    }
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

                await EnsureSearchDialogVisibleAsync(query).ConfigureAwait(true);

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
