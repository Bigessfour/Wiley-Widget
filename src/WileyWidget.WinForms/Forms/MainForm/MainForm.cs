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
        private const int WS_EX_COMPOSITED = 0x02000000;
        private const int WM_SETREDRAW = 0x000B;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_FRAME = 0x0400;

        // Core services (removed _panelNavigator – now in Navigation partial)
        private IServiceProvider? _serviceProvider;
        private IThemeService? _themeService;
        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        private Serilog.ILogger? _asyncLogger;
        private IWindowStateService _windowStateService;
        private IFileImportService _fileImportService;
        private IStatusProgressService? _statusProgressService;
        private SyncfusionControlFactory? _controlFactory;
        private DockingManager? _dockingManager;
        private bool _syncfusionDockingInitialized;
        private bool _dashboardAutoShown;
        private bool _reportViewerLaunched;
        private IServiceScope? _mainViewModelScope;

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

        // Navigation service (used by OnShown)
        private PanelNavigationService? _panelNavigationService;

        // Component container
        internal System.ComponentModel.IContainer? components;

        // MRU
        private readonly List<string> _mruList = new List<string>();

        public MainViewModel? MainViewModel { get; private set; }

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

            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1280, 800);
            StartPosition = FormStartPosition.Manual;

            try
            {
                var themeName = _themeService?.CurrentTheme ?? Themes.ThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, themeName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply theme in constructor");
            }

            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

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
                _logger?.LogInformation("[ONLOAD] Starting chrome and state restoration");

                LoadMruList();
                _windowStateService.RestoreWindowState(this);

                InitializeChrome();

                _logger?.LogInformation("[ONLOAD] Completed in {ElapsedMs}ms", onLoadStopwatch.ElapsedMilliseconds);
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
                // Unsubscribe from ThemeService to prevent memory leaks (Req 1 — SfSkinManager authority)
                if (_themeService != null)
                    _themeService.ThemeChanged -= OnThemeServiceChanged;

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
        /// UI-automation runs need a full, live UI — including the DockingManager — so this
        /// flag overrides the DockingManager skip-guard that normally fires in any test runtime.
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
        /// Initializes Syncfusion docking infrastructure via SyncfusionControlFactory (Req 2 + 9).
        /// Creates a real DockingManager when a handle is available and tests are not running.
        /// TabbedMDIManager initialization is kept separate for MDI child-window layout.
        /// </summary>
        private void InitializeSyncfusionDocking()
        {
            if (_syncfusionDockingInitialized && _dockingManager != null)
            {
                return;
            }

            // Always initialize the TabbedMDI manager for MDI document layout.
            InitializeMDIManager();

            // Create DockingManager (real or stub) via factory (MANDATORY per Syncfusion Control Creation Rule).
            // Use stub in UI-test runtime to avoid Syncfusion non-client paint issues and hanging.
            if (_controlFactory != null && IsHandleCreated)
            {
                try
                {
                    var themeName = _themeService?.CurrentTheme
                        ?? SfSkinManager.ApplicationVisualTheme
                        ?? Themes.ThemeColors.DefaultTheme;

                    if (IsEffectivelyUiTestRuntime())
                    {
                        var stubLogger = (ILogger<TestDockingManagerStub>)_serviceProvider.GetService(typeof(ILogger<TestDockingManagerStub>))!;
                        _dockingManager = new TestDockingManagerStub(stubLogger, true);
                        _logger.LogInformation("TEST MODE: DockingManager replaced with stub");
                    }
                    else
                    {
                        _dockingManager = _controlFactory.CreateDockingManager(this, this, dm =>
                        {
                            // PersistState = false: avoid automatic file-based state persistence
                            //   (we use AppStateSerializer in LayoutPersistence instead).
                            dm.PersistState = false;
                            dm.ShowCaption = false;
                            dm.DockToFill = false;  // Do not auto-fill form; panels are managed by PanelNavigationService
                            dm.CloseEnabled = true;
                            dm.AnimateAutoHiddenWindow = false;
                            // Register with component container for proper lifecycle / disposal.
                            components?.Add(dm);
                        });

                        // Ensure form-level SfSkinManager theme cascades to DockingManager.
                        // DockingManager inherits theming from the host form — no separate ThemeName property.
                        SfSkinManager.SetVisualStyle(this, themeName);

                        _logger?.LogDebug("DockingManager created via SyncfusionControlFactory; theme={Theme}", themeName);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "InitializeSyncfusionDocking: DockingManager creation failed; docking will be limited");
                    _dockingManager = null;
                }
            }
            else
            {
                _logger?.LogDebug(
                    "InitializeSyncfusionDocking: DockingManager skipped (factory={HasFactory}, handle={HasHandle}, test={IsTest})",
                    _controlFactory != null,
                    IsHandleCreated,
                    IsEffectivelyUiTestRuntime());
            }

            _syncfusionDockingInitialized = true;
            _logger?.LogDebug("InitializeSyncfusionDocking completed; dockingManager={HasDm}", _dockingManager != null);
        }

        /// <summary>
        /// Ensures docking and tabbed document layout are initialized before panel navigation starts.
        /// Wires EndDock/EndUndock events to trigger ForceFullLayout on ScopedPanelBase children (Req 3).
        /// </summary>
        private void InitializeDockingOrTabbedLayout()
        {
            InitializeSyncfusionDocking();
            InitializeMDIManager();

            // Wire DockingManager layout-completion events so every ScopedPanelBase child
            // repaints/relays itself after a dock or undock operation (standards Req 3).
            if (_dockingManager != null)
            {
                _dockingManager.DockStateChanged -= DockingManager_DockStateChanged;
                _dockingManager.DockStateChanged += DockingManager_DockStateChanged;
                _logger?.LogDebug("DockingManager DockStateChanged event wired");
            }
        }

        /// <summary>
        /// After a dock or undock pass, walks every affected control and calls
        /// <see cref="WileyWidget.WinForms.Controls.Base.ScopedPanelBase.TriggerForceFullLayout"/>
        /// on any hosted <see cref="WileyWidget.WinForms.Controls.Base.ScopedPanelBase"/> child
        /// (Standards Req 3).  Falls back to a generic PerformLayout+Invalidate sweep for
        /// non-ScopedPanelBase hosts (e.g., plain Forms).
        /// </summary>
        private void DockingManager_DockStateChanged(object? sender, DockStateChangeEventArgs e)
        {
            try
            {
                var affected = e?.Controls;
                if (affected is null || affected.Length == 0) return;

                foreach (var ctrl in affected)
                {
                    if (ctrl is null || ctrl.IsDisposed || !ctrl.IsHandleCreated) continue;

                    bool foundScoped = TryForceLayoutOnScopedPanelChildren(ctrl);

                    // Fallback: generic refresh when no ScopedPanelBase lives in this host.
                    if (!foundScoped)
                    {
                        ctrl.SuspendLayout();
                        try
                        {
                            PerformLayoutRecursive(ctrl);
                            ctrl.Invalidate(true);
                            ctrl.Update();
                        }
                        finally
                        {
                            ctrl.ResumeLayout(performLayout: true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "DockStateChanged layout refresh failed");
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

        /// <summary>Configures ribbon/status bar chrome around the tabbed docking surface.</summary>
        private void ConfigureDockingManagerChromeLayout()
        {
            try
            {
                if (_ribbon != null && !_ribbon.IsDisposed)
                {
                    _ribbon.Dock = DockStyleEx.Top;
                }

                if (_statusBar != null && !_statusBar.IsDisposed)
                {
                    _statusBar.Dock = DockStyle.Bottom;
                    _statusBar.Margin = Padding.Empty;
                }

                EnsureChromeZOrder();
                PerformLayout();
                Invalidate(true);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ConfigureDockingManagerChromeLayout failed");
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
            _logger?.LogDebug("SetStatusBarPanels called — all panels assigned");
        }

    }
}
