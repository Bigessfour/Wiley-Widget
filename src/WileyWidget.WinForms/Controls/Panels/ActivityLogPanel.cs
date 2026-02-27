using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using Syncfusion.WinForms.DataGrid;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using SplitContainerAdv = Syncfusion.Windows.Forms.Tools.SplitContainerAdv;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Activity Log panel for displaying recent navigation and application events.
    /// Replaces left dock navigation buttons, providing activity audit trail.
    /// Uses SfDataGrid to show recent actions in a user-friendly interface.
    /// </summary>
    public partial class ActivityLogPanel : ScopedPanelBase<ActivityLogViewModel>
    {
        // Strongly-typed ViewModel (this is what you use in your code)
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.DefaultValue(null)]
        public new ActivityLogViewModel? ViewModel
        {
            get => (ActivityLogViewModel?)base.ViewModel;
            set => base.ViewModel = value;
        }
        private readonly SyncfusionControlFactory? _factory;
        private SfDataGrid? _activityGrid;
        private PanelHeader? _panelHeader;
        private System.Windows.Forms.Timer? _autoRefreshTimer;
        private EventHandler? _autoRefreshTickHandler;
        private SfButton? _btnClearLog;
        private SfButton? _btnExport;
        private CheckBoxAdv? _chkAutoRefresh;
        private BindingSource? _bindingSource;
        private CancellationTokenSource? _autoRefreshCancellationTokenSource;
        private readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1, 1);
        private SplitContainerAdv? _gridSplit;
        private TableLayoutPanel? _content;
        private LoadingOverlay? _loader;
        private TableLayoutPanel? _mainTable;
        private ToolTip? _toolTip;
        private bool _gridInitialized = false;

        /// <summary>
        /// Initializes a new instance with required DI dependencies.
        /// </summary>
        public ActivityLogPanel(ActivityLogViewModel vm, SyncfusionControlFactory factory)
            : base(vm, ResolveLogger())
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));

            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(12);

            SafeSuspendAndLayout(CreateControls);
            ApplyTheme();
            InitializeAutoRefresh();
        }

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public ActivityLogPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase<ActivityLogViewModel>> logger)
            : base(scopeFactory, logger)
        {
            _factory = ControlFactory;
            // Set AutoScaleMode for proper DPI scaling
            this.AutoScaleMode = AutoScaleMode.Dpi;

            // Add padding for proper spacing
            this.Padding = new Padding(12);

            // Create controls programmatically instead of using InitializeComponent
            SafeSuspendAndLayout(CreateControls);
            ApplyTheme();
            // SetupUI() moved to OnShown after _activityGrid is created
            InitializeAutoRefresh();
            // LoadInitialData() moved to OnViewModelResolved to ensure ViewModel is available
        }

        private static ILogger ResolveLogger()
        {
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<ActivityLogPanel>>(Program.Services)
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ActivityLogPanel>.Instance;
        }

        private SyncfusionControlFactory Factory => _factory ?? ControlFactory;

        protected override void OnViewModelResolved(object? viewModel)
        {
            base.OnViewModelResolved(viewModel);
            if (viewModel is not ActivityLogViewModel typedViewModel)
            {
                return;
            }
            LoadInitialData();
        }

        protected async void LoadInitialData()
        {
            try
            {
                if (ViewModel != null)
                {
                    await ViewModel.LoadActivityAsync();
                }
                else
                {
                    Logger?.LogWarning("ViewModel is null in LoadInitialData - skipping refresh");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to load initial activity data");
            }
        }

        private void CreateControls()
        {
            this.SuspendLayout();

            WileyWidget.WinForms.Themes.ThemeColors.EnsureThemeAssemblyLoaded(Logger);

            Name = "ActivityLogPanel";
            Dock = DockStyle.Fill;
            MinimumSize = new Size(1024, 720);

            // Panel Header - added directly to Controls with Dock = Top
            _panelHeader = new PanelHeader
            {
                Title = "Recent Activity (Navigation Hub Activity Log)",
                Dock = DockStyle.Top
            };
            _panelHeader.AccessibleName = "Activity Log Header";
            _panelHeader.AccessibleDescription = "Header for the activity log panel";
            _panelHeader.TabIndex = 0;
            this.Controls.Add(_panelHeader);

            // Wire PanelHeader events
            _panelHeader.RefreshClicked += (s, e) => { Logger?.LogDebug("Refresh clicked on ActivityLogPanel"); /* Refresh logic if needed */ };
            _panelHeader.CloseClicked += (s, e) => ClosePanel();
            _panelHeader.HelpClicked += (s, e) => { MessageBox.Show("Activity Log Help: This panel shows recent navigation and system activities.", "Help", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            _panelHeader.PinToggled += (s, e) => { Logger?.LogDebug("Pin toggled on ActivityLogPanel"); /* Pin logic */ };
            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_panelHeader, "Refresh, pin, or close the activity log.");

            // Main TableLayoutPanel root (now starts with content, header is above)
            var mainTable = new TableLayoutPanel
            {
                Name = "MainTable",
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            _content = mainTable;

            // Row 0: Button controls
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Button panel (Row 0)
            var buttonPanel = new Panel
            {
                Name = "ButtonPanel",
                Dock = DockStyle.Fill,
                Height = 36,
                Padding = new Padding(8)
            };

            // Control buttons - add right-docked controls first
            _btnExport = Factory.CreateSfButton("Export", button =>
            {
                button.AutoSize = true;
                button.Dock = DockStyle.Right;
                button.Padding = new Padding(8);
            });
            _btnExport.Click += OnExportClicked;
            _btnExport.AccessibleName = "Export Activity Log";
            _btnExport.AccessibleDescription = "Export activity log entries to CSV file";
            _btnExport.TabIndex = 2;
            _toolTip.SetToolTip(_btnExport, "Export activity entries to CSV.");
            buttonPanel.Controls.Add(_btnExport);

            _btnClearLog = Factory.CreateSfButton("Clear", button =>
            {
                button.AutoSize = true;
                button.Dock = DockStyle.Right;
                button.Padding = new Padding(8);
            });
            _btnClearLog.Click += OnClearLogClicked;
            _btnClearLog.AccessibleName = "Clear Activity Log";
            _btnClearLog.AccessibleDescription = "Clears all activity log entries";
            _btnClearLog.TabIndex = 3;
            _toolTip.SetToolTip(_btnClearLog, "Clear all entries from the activity log.");
            buttonPanel.Controls.Add(_btnClearLog);

            _chkAutoRefresh = Factory.CreateCheckBoxAdv("Auto-refresh", checkBox =>
            {
                checkBox.AutoSize = true;
                checkBox.Checked = true;
                checkBox.Dock = DockStyle.Right;
                checkBox.Padding = new Padding(8);
            });
            _chkAutoRefresh.CheckedChanged += OnAutoRefreshCheckedChanged;
            _chkAutoRefresh.AccessibleName = "Auto Refresh";
            _chkAutoRefresh.AccessibleDescription = "Toggle auto refresh for activity log";
            _chkAutoRefresh.TabIndex = 4;
            _toolTip.SetToolTip(_chkAutoRefresh, "Automatically refresh activity entries.");
            buttonPanel.Controls.Add(_chkAutoRefresh);

            mainTable.Controls.Add(buttonPanel, 0, 0);

            // Row 1: temporary layout host for SplitContainer (created in OnShown)
            // Deferred to OnShown to avoid InvalidOperationException from premature SplitterDistance set
            var placeholderPanel = new Panel
            {
                Name = "GrowPlaceholder",
                Dock = DockStyle.Fill
            };
            mainTable.Controls.Add(placeholderPanel, 0, 1);

            Controls.Add(mainTable);
            _mainTable = mainTable;

            _loader = Factory.CreateLoadingOverlay(overlay =>
            {
                overlay.Dock = DockStyle.Fill;
                overlay.Visible = false;
                overlay.Message = "Loading activity log...";
            });
            Controls.Add(_loader);
            _loader.BringToFront();

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Defers gridSplit creation to OnLoad to avoid InvalidOperationException
        /// from setting SplitterDistance before the control has a valid handle and dimensions.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Skip if already initialized (idempotent)
            if (_gridInitialized || _mainTable == null)
                return;

            _gridInitialized = true;

            try
            {
                Logger?.LogDebug("ActivityLogPanel.OnShown: Initializing gridSplit (Width={W}, Height={H}, Handle={H})",
                    this.Width, this.Height, this.IsHandleCreated);

                // Row 1 index is 1 in the table (row 0 is the button panel)
                // Replace temporary layout host with the real split container
                var placeholder = _mainTable.Controls["GrowPlaceholder"];
                if (placeholder != null)
                {
                    _mainTable.Controls.Remove(placeholder);
                    placeholder.Dispose();
                }

                // Create SplitContainer AFTER the parent is fully sized
                _gridSplit = Factory.CreateSplitContainerAdv(splitter =>
                {
                    splitter.Name = "GridSplit";
                    splitter.Dock = DockStyle.Fill;
                    splitter.Orientation = Orientation.Horizontal;
                    splitter.SplitterDistance = 0;
                });

                // Data grid in the split container
                _activityGrid = Factory.CreateSfDataGrid(grid =>
                {
                    grid.Name = "ActivityGrid";
                    grid.Dock = DockStyle.Fill;
                    grid.AllowResizingColumns = true;
                    grid.AllowSorting = true;
                    grid.AllowFiltering = true;
                    grid.ShowBusyIndicator = false;
                });
                _activityGrid.AccessibleName = "Activity Log Grid";
                _activityGrid.AccessibleDescription = "Grid listing recent activity entries";
                _activityGrid.TabIndex = 1;

                // Configure columns - use DateTime column for timestamps
                _activityGrid.Columns.Add(new GridDateTimeColumn
                {
                    MappingName = "Timestamp",
                    HeaderText = "Time",
                    Format = "g",
                    AllowSorting = true,
                    Width = 150
                });

                _activityGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "Activity",
                    HeaderText = "Activity",
                    AllowSorting = true,
                    Width = 150
                });

                _activityGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "Details",
                    HeaderText = "Details",
                    AllowSorting = false,
                    Width = 300
                });

                _activityGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "Status",
                    HeaderText = "Status",
                    AllowSorting = true,
                    Width = 80
                });

                _gridSplit.Panel1.Controls.Add(_activityGrid);
                _mainTable.Controls.Add(_gridSplit, 0, 1);

                // Now that the grid is added and the parent is sized, setup UI bindings
                SetupUI();

                Logger?.LogDebug("ActivityLogPanel.OnShown: gridSplit initialized successfully");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize gridSplit in OnShown");
            }
        }

        /// <summary>
        /// Called when panel is first shown. Configure splitter distance.
        /// Note: base.OnShown timer handles ForceFullLayout automatically.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);   // starts the 180ms _finalLayoutTimer in ScopedPanelBase

            BeginInvoke(() =>
            {
                if (_gridSplit != null) _gridSplit.SplitterDistance = 280;
                Logger?.LogDebug("[{Panel}] Splitter configured after layout", GetType().Name);
            });
        }

        private void CreateControls_Old()
        {
            // This method is NO LONGER USED - kept for reference only
            //GridSplit creation has been moved to OnShown to prevent InvalidOperationException
        }

        private void SetupUI()
        {
            try
            {
                if (_activityGrid != null)
                {
                    if (ViewModel != null)
                    {
                        // Use BindingSource for reactive data binding and consistent grid updates
                        _bindingSource = new BindingSource
                        {
                            DataSource = ViewModel,
                            DataMember = "ActivityEntries"
                        };
                        _activityGrid.DataSource = _bindingSource;
                    }
                    else
                    {
                        // Defensive initialization: provide empty collection when ViewModel not yet loaded
                        // (e.g., during form instantiation in test harness before InitializeAsync runs)
                        _activityGrid.DataSource = new System.Collections.Generic.List<ActivityLog>();
                        Logger?.LogDebug("ActivityGrid initialized with empty collection - ViewModel will be bound in InitializeAsync()");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to setup activity grid");
            }
        }

        private void InitializeAutoRefresh()
        {
            _autoRefreshCancellationTokenSource = new CancellationTokenSource();
            _autoRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000 // Refresh every 5 seconds
            };
            // Store handler reference to prevent GC issues and ensure proper unsubscription in Dispose
            _autoRefreshTickHandler = OnAutoRefreshTick;
            _autoRefreshTimer.Tick += _autoRefreshTickHandler;
            _autoRefreshTimer.Start();
        }

        private void ClearActivityLog()
        {
            if (MessageBox.Show(
                "Clear all activity log entries?",
                "Clear Activity Log",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    ViewModel?.ClearActivityLog();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to clear activity log");
                    MessageBox.Show($"Failed to clear log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyTheme()
        {
            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme;
                if (!string.IsNullOrEmpty(theme))
                {
                    // SfSkinManager.SetVisualStyle applies theme cascade to form and all child controls
                    // Do NOT set ThemeName manually; rely on theme cascade from ScopedPanelBase
                    SfSkinManager.SetVisualStyle(this, theme);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to apply theme");
            }
        }

        private void OnClearLogClicked(object? sender, EventArgs e) => ClearActivityLog();

        private async void OnExportClicked(object? sender, EventArgs e)
        {
            try
            {
                if (ViewModel?.ActivityEntries == null || ViewModel.ActivityEntries.Count == 0)
                {
                    MessageBox.Show("No activity entries to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                    owner: this,
                    operationKey: $"{nameof(ActivityLogPanel)}.Csv",
                    dialogTitle: "Export Activity Log",
                    filter: "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    defaultExtension: "csv",
                    defaultFileName: $"ActivityLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv",
                    exportAction: (filePath, ct) =>
                    {
                        ExportToCSV(filePath);
                        return Task.CompletedTask;
                    },
                    logger: Logger,
                    cancellationToken: CancellationToken.None);

                if (result.IsSkipped)
                {
                    MessageBox.Show(result.ErrorMessage ?? "An export is already in progress.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (result.IsCancelled)
                {
                    return;
                }

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.ErrorMessage ?? "Export failed.", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show($"Activity log exported to {Path.GetFileName(result.FilePath)}.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to export activity log");
                MessageBox.Show($"Failed to export log: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportToCSV(string filePath)
        {
            if (ViewModel?.ActivityEntries == null || ViewModel.ActivityEntries.Count == 0)
            {
                MessageBox.Show("No activity entries to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // Write header
                writer.WriteLine("Timestamp,Activity,Details,Status");

                // Write data rows
                foreach (var entry in ViewModel.ActivityEntries)
                {
                    var timestamp = entry.Timestamp.ToString("g");
                    var activity = EscapeCSVField(entry.Activity);
                    var details = EscapeCSVField(entry.Details);
                    var status = EscapeCSVField(entry.Status);

                    writer.WriteLine($"{timestamp},{activity},{details},{status}");
                }
            }
        }

        private static string EscapeCSVField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\""; // Escape quotes and wrap in quotes
            }

            return field;
        }

        private void OnAutoRefreshCheckedChanged(object? sender, EventArgs e)
        {
            if (_autoRefreshTimer != null && _chkAutoRefresh != null)
            {
                _autoRefreshTimer.Enabled = _chkAutoRefresh.Checked;
            }
        }

        private void OnAutoRefreshTick(object? sender, EventArgs e)
        {
            // Use AsyncEventHelper to wrap async operation with proper error handling and cancellation support
            if (!IsDisposed)
            {
                // Queue the async refresh work using AsyncEventHelper for structured error handling
                _ = AsyncEventHelper.ExecuteAsync(
                    async ct => await RefreshActivityLogsAsync(),
                    _autoRefreshCancellationTokenSource,
                    this,
                    Logger,
                    "Activity Log Auto-Refresh",
                    showErrorDialog: false, // Don't show dialog for background timer refreshes
                    statusLabel: null);
            }
        }

        private Control? FindDockedHost(Syncfusion.Windows.Forms.Tools.DockingManager dockingManager)
        {
            Control? current = this;
            while (current != null)
            {
                try
                {
                    if (dockingManager.GetEnableDocking(current))
                    {
                        return current;
                    }
                }
                catch { }
                current = current.Parent;
            }
            return null;
        }

        /// <summary>
        /// Performs async activity log refresh on the UI thread.
        /// Accepts CancellationToken to allow cancellation during panel disposal.
        /// </summary>
        private async Task RefreshActivityLogsAsync(CancellationToken cancellationToken = default)
        {
            // Prevent concurrent refreshes
            if (!await _refreshSemaphore.WaitAsync(0, cancellationToken))
            {
                Logger?.LogDebug("Refresh already in progress, skipping");
                return;
            }

            try
            {
                if (IsDisposed || ViewModel == null || _chkAutoRefresh?.Checked != true)
                    return;

                // Refresh data on UI thread context (no ConfigureAwait needed)
                await ViewModel.LoadActivityAsync();

                // Update grid binding on UI thread
                if (!IsDisposed)
                {
                    _bindingSource?.ResetBindings(false);
                }
            }
            catch (OperationCanceledException)
            {
                Logger?.LogDebug("Auto-refresh cancelled");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Auto-refresh failed");
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        /// <summary>
        /// Validate the activity log. Since it's append-only, validation passes if ViewModel is ready.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            // Activity log is append-only and read-only from the UI.
            // Valid if the ViewModel is loaded and ActivityEntries collection exists.
            if (ViewModel == null)
            {
                return ValidationResult.Failed(new ValidationItem("ViewModel", "Activity log ViewModel not loaded", ValidationSeverity.Error));
            }

            if (ViewModel.ActivityEntries == null)
            {
                return ValidationResult.Failed(new ValidationItem("ActivityEntries", "Activity entries collection not initialized", ValidationSeverity.Error));
            }

            return await Task.FromResult(ValidationResult.Success);
        }

        /// <summary>
        /// Save is a no-op for the read-only Activity Log panel.
        /// </summary>
        public override Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Load activity entries from the ViewModel asynchronously.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            if (ViewModel == null)
            {
                return;
            }

            try
            {
                var ct_op = RegisterOperation();
                IsBusy = true;
                if (_loader != null)
                {
                    _loader.Visible = true;
                }
                await ViewModel.LoadActivityAsync();
                SetHasUnsavedChanges(false);
            }
            catch (OperationCanceledException)
            {
                Logger?.LogDebug("ActivityLogPanel load cancelled");
            }
            finally
            {
                if (_loader != null)
                {
                    _loader.Visible = false;
                }
                IsBusy = false;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (MinimumSize.Width < 1024 || MinimumSize.Height < 720)
            {
                MinimumSize = new Size(1024, 720);
            }

            PerformLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cancel any pending auto-refresh operations
                AsyncEventHelper.CancelAndDispose(ref _autoRefreshCancellationTokenSource);

                // Cleanup timer with stored handler reference
                if (_autoRefreshTimer != null)
                {
                    try
                    {
                        if (_autoRefreshTickHandler != null)
                        {
                            _autoRefreshTimer.Tick -= _autoRefreshTickHandler;
                        }
                    }
                    catch { }
                    try { _autoRefreshTimer.Stop(); } catch { }
                    try { _autoRefreshTimer.Dispose(); } catch { }
                    _autoRefreshTimer = null;
                }

                if (_chkAutoRefresh != null)
                {
                    try { _chkAutoRefresh.CheckedChanged -= OnAutoRefreshCheckedChanged; } catch { }
                    try { _chkAutoRefresh.Dispose(); } catch { }
                    _chkAutoRefresh = null;
                }

                if (_btnClearLog != null)
                {
                    try { _btnClearLog.Click -= OnClearLogClicked; } catch { }
                    try { _btnClearLog.Dispose(); } catch { }
                    _btnClearLog = null;
                }

                if (_btnExport != null)
                {
                    try { _btnExport.Click -= OnExportClicked; } catch { }
                    try { _btnExport.Dispose(); } catch { }
                    _btnExport = null;
                }

                // Cleanup BindingSource and grid data binding
                if (_bindingSource != null)
                {
                    try { _bindingSource.Dispose(); } catch { }
                    _bindingSource = null;
                }

                if (_loader != null)
                {
                    try { _loader.Dispose(); } catch { }
                    _loader = null;
                }

                if (_activityGrid != null)
                {
                    try { _activityGrid.DataSource = null; } catch { }
                    try { _activityGrid.Dispose(); } catch { }
                    _activityGrid = null;
                }

                try { _panelHeader?.Dispose(); } catch { }
                _panelHeader = null;
                try { _toolTip?.Dispose(); } catch { }
                _toolTip = null;

                // Dispose semaphore
                try { _refreshSemaphore?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>

}
