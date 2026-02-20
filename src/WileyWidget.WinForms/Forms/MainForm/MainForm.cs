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

        // Core services (removed _panelNavigator – now in Navigation partial)
        private IServiceProvider? _serviceProvider;
        private IThemeService? _themeService;
        private IConfiguration? _configuration;
        private ILogger<MainForm>? _logger;
        private IWindowStateService _windowStateService;
        private IFileImportService _fileImportService;
        private SyncfusionControlFactory? _controlFactory;

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

            _uiConfig = UIConfiguration.FromConfiguration(configuration);

            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            Size = new Size(1280, 800);
            MinimumSize = new Size(800, 600);
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

            _logger?.LogInformation("[ONLOAD] Starting chrome and state restoration");

            LoadMruList();
            _windowStateService.RestoreWindowState(this);

            InitializeChrome();

            _logger?.LogInformation("[ONLOAD] Completed");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private static bool IsUiTestEnvironment()
        {
            return false;
        }

        private void MainForm_DragEnter(object? sender, DragEventArgs e) { }
        private void MainForm_DragDrop(object? sender, DragEventArgs e) { }
        private void MainForm_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e) { }
        private void OnApplicationFontChanged(object? sender, Services.FontChangedEventArgs e) { this.Font = e.NewFont; }

        // ---------------------------------------------------------------------------
        // Stubs referenced by partial files (ribbon, layout, search, document mgmt)
        // ---------------------------------------------------------------------------
        private void LoadMruList() { /* MRU loading reserved for future impl */ }

        /// <summary>Opens the New Budget wizard.</summary>
        private void CreateNewBudget() { _logger?.LogDebug("CreateNewBudget stub"); }

        /// <summary>Opens an existing budget file.</summary>
        private void OpenBudget() { _logger?.LogDebug("OpenBudget stub"); }

        /// <summary>Persists the current docking layout.</summary>
        protected void SaveCurrentLayout() { _logger?.LogDebug("SaveCurrentLayout stub"); }

        /// <summary>Exports the active data set.</summary>
        private void ExportData() { _logger?.LogDebug("ExportData stub"); }

        /// <summary>Resets the docking layout to defaults.</summary>
        protected void ResetLayout() { _logger?.LogDebug("ResetLayout stub"); }

        /// <summary>Gets the count of items in the Quick Access Toolbar.</summary>
        protected int GetQATItemCount() => 1; // TODO: Find correct Syncfusion API for QAT item count

        /// <summary>Toggles panel locking via the docking manager.</summary>
        private void TogglePanelLocking() { _logger?.LogDebug("TogglePanelLocking stub"); }

        /// <summary>Performs a global search across panels and data.</summary>
        public async Task PerformGlobalSearchAsync(string query)
        {
            _logger?.LogDebug("PerformGlobalSearchAsync: {Query}", query);
            await Task.CompletedTask;
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
