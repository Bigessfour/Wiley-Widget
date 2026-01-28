using System.Threading;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using System.ComponentModel;
using System.Globalization;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.Services;
using static WileyWidget.WinForms.Services.ExportService;
using WileyWidget.WinForms.Forms; // Added for MainViewModel access
using FormsMainViewModel = WileyWidget.WinForms.Forms.MainViewModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.Controls
{
    internal static class DashboardPanelResources
    {
        public const string PanelTitle = "Dashboard";
        public const string RefreshText = "Refresh";
    }

    /// <summary>
    /// Dashboard panel with KPIs, charts, and details grid.
    /// Migrated to ScopedPanelBase for proper DI scoping and lifecycle management.
    /// </summary>
    public partial class DashboardPanel : ScopedPanelBase
    {
        // Strongly-typed ViewModel (this is what you use in your code)
        public new FormsMainViewModel? ViewModel
        {
            get => (FormsMainViewModel?)base.ViewModel;
            set => base.ViewModel = value;
        }

        private FormsMainViewModel? _vm => ViewModel;

        // controls
        private GradientPanelExt _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private ToolStrip _toolStrip = null!;
        private ToolStripButton _btnRefresh = null!;
        private Label _lblLastRefreshed = null!;

        private SfListView? _kpiList = null;
        private ChartControl _mainChart = null!;
        private ChartControlRegionEventWiring? _mainChartRegionEventWiring;
        private SfDataGrid _detailsGrid = null!;
        private SplitContainerAdv _mainSplitContainer = null!;
        private StatusStrip _statusStrip = null!;
        private ErrorProvider? _errorProvider;
        private ToolStripStatusLabel _statusLabel = null!;

        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        // Named event handlers for PanelHeader (stored for proper unsubscription)
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        // Gauge controls for dashboard metrics
        private RadialGauge? _budgetUtilizationGauge;
        private RadialGauge? _revenueGauge;
        private RadialGauge? _expenseGauge;
        private RadialGauge? _varianceGauge;

        // Shared tooltip for all interactive controls (prevents memory leaks from multiple ToolTip instances)
        private ToolTip? _sharedTooltip;

        // Dispatcher helper for marshaling UI updates to the UI thread
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;

        // DI-friendly constructor for DependencyInjection container.
        // ViewModel is resolved from scoped provider after handle creation.
        public DashboardPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase> logger,
            WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
            : base(scopeFactory, logger)
        {
            _dispatcherHelper = dispatcherHelper;

            // Initialize shared tooltip for all interactive controls
            _sharedTooltip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 100,
                ShowAlways = true
            };

            // Enforce minimum panel size to prevent cramped layouts on small screens
            // Accounts for multi-chart, gauge, and grid layouts (DPI-aware)
            this.MinimumSize = new Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000.0f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600.0f)
            );

            InitializeControls();

            // Apply theme via SfSkinManager (single source of truth)
            try { var theme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme; Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, theme); } catch { }

            // Defer sizing validations until control is properly laid out to prevent "controls cut off" issues
            // Dashboard has complex nested layouts (TableLayoutPanel, SplitContainer, gauges, chart, grid)
            DeferSizingValidation();

            // Diagnostic logging
            try { Serilog.Log.Debug("DashboardPanel initialized"); } catch { }
        }

        /// <summary>
        /// Called after the ViewModel has been resolved from the scoped service provider.
        /// Wires up event handlers but defers heavy binding work to async task.
        /// </summary>
        protected override void OnViewModelResolved(object? viewModel)
        {
            base.OnViewModelResolved(viewModel);
            if (viewModel is not FormsMainViewModel typedViewModel)
            {
                return;
            }

            try
            {
                // Subscribe to ViewModel property changes - lightweight, synchronous only
                if (typedViewModel is INotifyPropertyChanged npc && _viewModelPropertyChangedHandler == null)
                {
                    _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
                    npc.PropertyChanged += _viewModelPropertyChangedHandler;
                }

                // Defer heavy binding work to background thread to prevent UI blocking
                // This follows async-initialization-pattern: synchronous initialize, heavy work deferred
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(100); // Allow UI to settle after control creation
                        if (!IsDisposed)
                        {
                            await EnsureLoadedAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "DashboardPanel: Failed to apply deferred bindings");
                    }
                });

                Logger.LogDebug("DashboardPanel: ViewModel resolved, deferred binding scheduled");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "DashboardPanel: Failed to initialize ViewModel");
            }
        }

        private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Serilog.Log.Debug("DashboardPanel.EnsureLoadedAsync: START - _vm={VmNotNull}, LoadCommand={CmdNotNull}, IsLoading={IsLoading}",
                    _vm != null, _vm?.LoadDataCommand != null, _vm?.IsLoading ?? false);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] EnsureLoadedAsync: START - LoadCommand={_vm?.LoadDataCommand != null}, IsLoading={_vm?.IsLoading ?? false}");

                UpdateStatus("Loading dashboard data...");

                if (_vm?.LoadDataCommand != null && !(_vm?.IsLoading ?? false))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] Executing LoadDataCommand...");
                    await _vm.LoadDataCommand.ExecuteAsync(null).ConfigureAwait(true);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] LoadDataCommand completed");
                    Serilog.Log.Debug("DashboardPanel.EnsureLoadedAsync: LoadDataCommand completed successfully");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] Skipping load - Command={_vm?.LoadDataCommand != null}, IsLoading={_vm?.IsLoading}");
                    Serilog.Log.Warning("DashboardPanel.EnsureLoadedAsync: Skipping load - Command={CmdNotNull}, IsLoading={IsLoading}",
                        _vm?.LoadDataCommand != null, _vm?.IsLoading);
                }

                UpdateStatus("Dashboard data loaded");

                // Now apply bindings on the UI thread after data is loaded
                if (!IsDisposed)
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke(new System.Action(TryApplyViewModelBindings));
                    }
                    else
                    {
                        TryApplyViewModelBindings();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD ERROR] EnsureLoadedAsync failed: {ex.Message}");
                Serilog.Log.Error(ex, "DashboardPanel.EnsureLoadedAsync: Failed to load dashboard data");
                UpdateStatus($"Load failed: {ex.Message}");
                try
                {
                    _errorProvider ??= new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
                    if (_mainChart != null) _errorProvider.SetError(_mainChart, "Failed to load dashboard data - check logs");
                    MessageBox.Show($"Could not load dashboard data: {ex.Message}", "Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        private void InitializeControls()
        {
            if (ViewModel == null) return;

            // Suspend layout during initialization to prevent flickering and layout thrashing
            this.SuspendLayout();

            var theme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            var rootTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: Header/Toolbar
            rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F)); // Row 2: KPI Gauges
            rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Row 3: Content

            // --- Row 1: Header and ToolStrip ---
            var headerTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Height = 48 };
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _panelHeader = new PanelHeader { Dock = DockStyle.Fill, Title = DashboardPanelResources.PanelTitle };
            _panelHeaderRefreshHandler = OnPanelHeaderRefreshClicked;
            _panelHeaderCloseHandler = OnPanelHeaderCloseClicked;
            _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;

            _toolStrip = new ToolStrip { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden };
            SetupToolStrip(_toolStrip);

            headerTable.Controls.Add(_panelHeader, 0, 0);
            headerTable.Controls.Add(_toolStrip, 1, 0);
            rootTable.Controls.Add(headerTable, 0, 0);

            // --- Row 2: KPI and Gauges ---
            var kpiPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++) kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            kpiPanel.Controls.Add(CreateGaugePanel("Budget Utilization", "gauge"), 0, 0);
            kpiPanel.Controls.Add(CreateGaugePanel("Revenue Collection", "gauge"), 1, 0);
            kpiPanel.Controls.Add(CreateGaugePanel("Expense Ratio", "gauge"), 2, 0);
            kpiPanel.Controls.Add(CreateGaugePanel("Budget Variance", "gauge"), 3, 0);

            rootTable.Controls.Add(kpiPanel, 0, 1);

            // --- Row 3: Content Area (SplitContainerAdv with Chart and Grid) ---
            _mainSplitContainer = new SplitContainerAdv
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300
            };

            // Chart setup
            _mainChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Budget chart",
                AccessibleDescription = "Displays budget vs expenditure"
            };
            _mainChartRegionEventWiring = new ChartControlRegionEventWiring(_mainChart);
            ChartControlDefaults.Apply(_mainChart, logger: Logger);
            ConfigureChart(_mainChart);

            // Grid setup
            _detailsGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowFiltering = true,
                AllowSorting = true,
                AllowGrouping = true,
                ShowRowHeader = false,
                SelectionMode = Syncfusion.WinForms.DataGrid.Enums.GridSelectionMode.Single,
                AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill,
                RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f),
                HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f)
            };
            ConfigureDetailsGrid(_detailsGrid);

            _mainSplitContainer.Panel1.Controls.Add(_mainChart);
            _mainSplitContainer.Panel2.Controls.Add(_detailsGrid);
            rootTable.Controls.Add(_mainSplitContainer, 0, 2);

            // Status Bar
            _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            _statusLabel = new ToolStripStatusLabel { Text = "Ready" };
            _statusStrip.Items.Add(_statusLabel);

            // Overlays
            _loadingOverlay = new LoadingOverlay { Message = "Loading dashboard...", Dock = DockStyle.Fill, Visible = false };
            _noDataOverlay = new NoDataOverlay { Message = "No data yet.", Dock = DockStyle.Fill, Visible = false };

            this.Controls.Add(_loadingOverlay);
            this.Controls.Add(_noDataOverlay);
            this.Controls.Add(_statusStrip);
            this.Controls.Add(rootTable);

            _loadingOverlay.BringToFront();
            _noDataOverlay.BringToFront();

            try { Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name); } catch { }

            // Validate toolbar visibility post-layout (OPTION B)
            // Logs if toolbar is collapsed or invisible after layout settles
            ValidateToolbarVisibility();

            ResumeLayout(true);
            this.PerformLayout();
            this.Refresh();
        }

        private void SetupToolStrip(ToolStrip toolStrip)
        {
            var btnRefresh = new ToolStripButton(DashboardPanelResources.RefreshText) { ToolTipText = "Reload metrics (F5)" };
            btnRefresh.Click += (s, e) => _vm?.RefreshCommand?.ExecuteAsync(null);

            var btnLoad = new ToolStripButton("Load") { ToolTipText = "Load dashboard data" };
            btnLoad.Click += async (s, e) => { if (_vm?.LoadDataCommand != null) await _vm.LoadDataCommand.ExecuteAsync(null); };

            toolStrip.Items.Add(new ToolStripLabel("Dashboard"));
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnRefresh);
            toolStrip.Items.Add(btnLoad);
        }

        private void ConfigureChart(ChartControl chart)
        {
            chart.PrimaryXAxis.Title = "Category";
            chart.PrimaryYAxis.Title = "Amount";
            chart.ShowLegend = true;
            chart.Legend.Position = Syncfusion.Windows.Forms.Chart.ChartDock.Bottom;
        }

        private void ConfigureDetailsGrid(SfDataGrid grid)
        {
            grid.Columns.Add(new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", MinimumWidth = 140 });
            grid.Columns.Add(new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budget", Format = "C0", MinimumWidth = 120 });
            grid.Columns.Add(new GridNumericColumn { MappingName = "Amount", HeaderText = "Actual", Format = "C0", MinimumWidth = 120 });
        }

        /// <summary>
        /// Validates toolbar visibility after layout completion (OPTION B).
        /// Logs a warning if the toolbar is collapsed or has zero height,
        /// indicating potential layout issues that could prevent user access to Refresh/Export buttons.
        /// </summary>
        private void ValidateToolbarVisibility()
        {
            try
            {
                if (_topPanel == null || _topPanel.IsDisposed)
                    return;

                // Check if toolbar container has valid dimensions
                if (_topPanel.Height <= 0 || _topPanel.Width <= 0)
                {
                    Logger.LogWarning(
                        "DashboardPanel: Toolbar visibility issue detected - Height={ToolbarHeight}, Width={ToolbarWidth}. " +
                        "Buttons (Refresh, Export, Navigate) may not be accessible.",
                        _topPanel.Height, _topPanel.Width);
                    return;
                }

                // Check if toolbar is visible and not obscured
                if (!_topPanel.Visible)
                {
                    Logger.LogWarning(
                        "DashboardPanel: Toolbar is hidden (Visible=false). User cannot access Refresh, Export, or Navigation buttons.");
                    return;
                }

                Logger.LogDebug(
                    "DashboardPanel: Toolbar validation passed - Height={ToolbarHeight}, Width={ToolbarWidth}, Visible={IsVisible}",
                    _topPanel.Height, _topPanel.Width, _topPanel.Visible);
            }
            catch (ObjectDisposedException)
            {
                // Panel was disposed before validation could run
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "DashboardPanel: Unexpected error during toolbar validation");
            }
        }

        /// <summary>
        /// Defers sizing validation until the control is fully laid out.
        /// Prevents "controls cut off" issues by ensuring all nested panels, grids, and charts
        /// have sufficient space and valid dimensions after the control is docked.
        /// Per SafeControlSizeValidator documentation: validate after layout completion.
        /// </summary>
        private void DeferSizingValidation()
        {
            // Schedule validation after layout completes (100ms delay allows paint to settle)
            var validationTimer = new System.Windows.Forms.Timer();
            validationTimer.Interval = 100;
            validationTimer.Tick += (s, e) =>
            {
                validationTimer.Stop();
                validationTimer.Dispose();

                if (this.IsDisposed) return;

                try
                {
                    // Validate main panel size
                    var panelValidation = SafeControlSizeValidator.ValidateControlSize(this);
                    if (!panelValidation.IsValid)
                    {
                        Logger.LogWarning($"Dashboard sizing issue: {panelValidation.Message}");
                    }

                    // Validate child control sizes - prevents cut-off controls
                    if (_mainChart != null && !_mainChart.IsDisposed)
                    {
                        var chartValidation = SafeControlSizeValidator.ValidateControlSize(_mainChart);
                        if (!chartValidation.IsValid && _mainChart.Width > 0 && _mainChart.Height > 0)
                        {
                            Logger.LogWarning($"Chart sizing issue: {chartValidation.Message}");
                        }
                    }

                    if (_detailsGrid != null && !_detailsGrid.IsDisposed)
                    {
                        var gridValidation = SafeControlSizeValidator.ValidateControlSize(_detailsGrid);
                        if (!gridValidation.IsValid && _detailsGrid.Width > 0 && _detailsGrid.Height > 0)
                        {
                            Logger.LogWarning($"Grid sizing issue: {gridValidation.Message}");
                        }
                    }

                    // Adjust any controls with constraint violations
                    foreach (Control ctrl in this.Controls)
                    {
                        if (ctrl != null && !ctrl.IsDisposed)
                        {
                            SafeControlSizeValidator.TryAdjustConstrainedSize(ctrl, out _, out _);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error during dashboard sizing validation: {ex.Message}");
                }
            };
            validationTimer.Start();
        }

        /// <summary>
        /// Creates a single gauge panel for metrics display.
        /// </summary>
        /// <param name="title">Gauge title (e.g., "Budget Utilization")</param>
        /// <param name="iconName">Icon name (currently unused, reserved for future)</param>
        /// <returns>GradientPanelExt containing gauge with label</returns>
        private GradientPanelExt CreateGaugePanel(string title, string iconName)
        {
            var panelTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            var container = new GradientPanelExt
            {
                Width = 280,
                Height = 120,
                Margin = new Padding(4),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(container, panelTheme);
            var gauge = new RadialGauge
            {
                Width = 110,
                Height = 110,
                Dock = DockStyle.Left,
                AccessibleName = title,
                AccessibleDescription = title + " metric gauge",
                MinimumValue = 0,
                MaximumValue = 100,
                EnableCustomNeedles = true,
                MinimumSize = new Size(110, 110)
            };

            // Configure gauge appearance
            gauge.GaugeLabel = "";
            gauge.ShowTicks = true;
            gauge.MajorTickMarkHeight = 8;
            gauge.MinorTickMarkHeight = 4;
            gauge.ShowScaleLabel = true;
            gauge.VisualStyle = Syncfusion.Windows.Forms.Gauge.ThemeStyle.Office2016Colorful;

            // Add tooltip
            if (_sharedTooltip != null)
            {
                _sharedTooltip.SetToolTip(gauge, $"{title}\nUpdates in real-time");
            }

            // Add color ranges
            gauge.Ranges.Clear();
            gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
            {
                StartValue = 0,
                EndValue = 60,
                Color = Color.Green,
                Height = 8,
                InRange = true,
                RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
            });
            gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
            {
                StartValue = 60,
                EndValue = 90,
                Color = Color.Orange,
                Height = 8,
                InRange = true,
                RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
            });
            gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
            {
                StartValue = 90,
                EndValue = 100,
                Color = Color.Red,
                Height = 8,
                InRange = true,
                RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
            });

            gauge.NeedleColor = Color.DarkSlateGray;
            gauge.Cursor = Cursors.Hand;

            // Label panel
            var lblPanel = new GradientPanelExt
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(lblPanel, panelTheme);
            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var valueLabel = new Label
            {
                Name = $"{title}ValueLabel",
                Text = "0%",
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblPanel.Controls.Add(valueLabel);
            lblPanel.Controls.Add(titleLabel);

            container.Controls.Add(lblPanel);
            container.Controls.Add(gauge);

            // Store gauge references
            if (title.Contains("Budget", StringComparison.OrdinalIgnoreCase)) _budgetUtilizationGauge = gauge;
            else if (title.Contains("Revenue", StringComparison.OrdinalIgnoreCase)) _revenueGauge = gauge;
            else if (title.Contains("Expense", StringComparison.OrdinalIgnoreCase)) _expenseGauge = gauge;
            else if (title.Contains("Variance", StringComparison.OrdinalIgnoreCase)) _varianceGauge = gauge;

            return container;
        }

        private void TryApplyViewModelBindings()
        {
            // Ensure this method runs on the UI thread. Some callers may originate
            // from background threads (ViewModel property changes, async tasks, etc.).
            // If we're not already on the UI thread, re-dispatch the call and return.
            if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
            {
                try { _ = _dispatcherHelper.InvokeAsync(TryApplyViewModelBindings); } catch { }
                return;
            }

            if (_detailsGrid != null && !_detailsGrid.IsDisposed && _detailsGrid.InvokeRequired)
            {
                try { _detailsGrid.BeginInvoke(new System.Action(TryApplyViewModelBindings)); } catch { }
                return;
            }

            if (this.InvokeRequired)
            {
                try { BeginInvoke(new System.Action(TryApplyViewModelBindings)); } catch { }
                return;
            }

            try
            {
                // Controls may not be fully initialized yet (or may be disposed).
                if (IsDisposed || _mainChart == null || _detailsGrid == null || _lblLastRefreshed == null || _vm == null)
                {
                    return;
                }

                // KPI list is optional (legacy UI). Only bind if it exists.
                var kpiList = _kpiList;
                var kpiSnapshot = _vm.Metrics?.ToList();
                if (kpiSnapshot != null && kpiList != null && !kpiList.IsDisposed)
                {
                    kpiList.DataSource = kpiSnapshot;
                    kpiList.ItemHeight = 84;
                }

                // Snapshot collections to avoid cross-thread mutations during paint
                var metricsSnapshot = _vm.Metrics?.ToList();
                if (metricsSnapshot != null && metricsSnapshot.Any())
                {
                    _mainChart.Series.Clear();

                    // Primary series: Department metrics (column chart)
                    var ser = new ChartSeries("Budget vs Actual", ChartSeriesType.Column);
                    foreach (var m in metricsSnapshot)
                    {
                        if (m == null) continue;
                        ser.Points.Add(m.Name, (double)m.Value);
                    }
                    _mainChart.Series.Add(ser);

                    // Secondary series: Monthly revenue trend (line chart overlay)
                    var monthlyRevenueSnapshot = _vm.MonthlyRevenueData?.ToList();
                    if (monthlyRevenueSnapshot != null && monthlyRevenueSnapshot.Any())
                    {
                        var revenueSeries = new ChartSeries("Monthly Revenue Trend", ChartSeriesType.Line);
                        foreach (var month in monthlyRevenueSnapshot)
                        {
                            if (month == null) continue;
                            revenueSeries.Points.Add(month.Month, (double)month.Amount);
                        }
                        revenueSeries.Style.DisplayText = false;
                        _mainChart.Series.Add(revenueSeries);
                    }

                    // Note: Sparkline charts removed - current UI uses gauge panels instead
                }

                // Wire status and labels
                if (Controls.Find("lblBudget", true).FirstOrDefault() is Label lblBudget)
                {
                    lblBudget.Text = $"Total Budget: {_vm.TotalBudget.ToString("C0", CultureInfo.CurrentCulture)}";
                }
                if (Controls.Find("lblExpenditure", true).FirstOrDefault() is Label lblExp)
                {
                    lblExp.Text = $"Expenditure: {_vm.TotalExpenditure.ToString("C0", CultureInfo.CurrentCulture)}";
                }
                if (Controls.Find("lblRemaining", true).FirstOrDefault() is Label lblRem)
                {
                    lblRem.Text = $"Remaining: {_vm.RemainingBudget.ToString("C0", CultureInfo.CurrentCulture)}";
                }

                // Bind loading overlay visibility to view model IsLoading
                try
                {
                    if (_loadingOverlay != null)
                    {
                        try { _loadingOverlay.DataBindings.Clear(); } catch { }
                        var bs = new BindingSource { DataSource = _vm };
                        _loadingOverlay.DataBindings.Add(new Binding("Visible", bs, nameof(FormsMainViewModel.IsLoading), true, DataSourceUpdateMode.OnPropertyChanged));
                        _loadingOverlay.BringToFront();
                    }
                }
                catch { }

                // Show no-data overlay only when truly no data exists across primary collections.
                // Sample data in Metrics or ActivityItems will prevent overlay display.
                // Keep this synchronous and avoid repeated PropertyChanged subscriptions.
                try
                {
                    if (_noDataOverlay != null)
                    {
                        UpdateOverlays();
                    }

                    _vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(FormsMainViewModel.IsLoading) ||
                            e.PropertyName == nameof(FormsMainViewModel.Metrics) ||
                            e.PropertyName == nameof(FormsMainViewModel.ActivityItems))
                        {
                            try
                            {
                                if (this.InvokeRequired) BeginInvoke(new System.Action(UpdateOverlays)); else UpdateOverlays();
                            }
                            catch { }
                        }
                    };

                    UpdateOverlays();
                }
                catch { }

                // details grid mapping
                var prop = _vm.GetType().GetProperty("ActivityItems") ?? _vm.GetType().GetProperty("Metrics");
                if (prop != null)
                {
                    var val = prop.GetValue(_vm);
                    if (val != null)
                    {
                        try
                        {
                            // Inspect element shape and adapt grid columns to avoid Syncfusion painter errors
                            Type? elementType = null;
                            if (val is System.Collections.IEnumerable enumerable)
                            {
                                foreach (var it in enumerable)
                                {
                                    if (it != null)
                                    {
                                        elementType = it.GetType();
                                        break;
                                    }
                                }
                            }

                            // If we didn't find an instance, try to infer generic argument (IEnumerable<T>)
                            if (elementType == null)
                            {
                                var t = val.GetType();
                                if (t.IsGenericType)
                                {
                                    var args = t.GetGenericArguments();
                                    if (args.Length == 1) elementType = args[0];
                                }
                            }

                            // Defensive: if elementType is metrics-like, adjust columns to stable mappings
                            if (elementType != null)
                            {
                                var hasDepartment = elementType.GetProperty("DepartmentName") != null;
                                var hasBudgeted = elementType.GetProperty("BudgetedAmount") != null || elementType.GetProperty("Budget") != null;
                                var hasActual = elementType.GetProperty("Amount") != null || elementType.GetProperty("Actual") != null;

                                if (_detailsGrid == null) return;

                                if (!hasDepartment && !hasBudgeted && !hasActual)
                                {
                                    // Probably binding to DashboardMetric or other shape - switch to safe two-column mapping (Name/Value)
                                    try
                                    {
                                        try { _detailsGrid.SuspendLayout(); } catch { }
                                        _detailsGrid.Columns.Clear();
                                        _detailsGrid.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = "Name", MinimumWidth = 160 });
                                        var valCol = new GridTextColumn { MappingName = "Value", HeaderText = "Value", MinimumWidth = 120 };
                                        _detailsGrid.Columns.Add(valCol);
                                    }
                                    catch { }
                                    finally { try { _detailsGrid.ResumeLayout(); } catch { } }
                                }
                                else
                                {
                                    // Ensure default expected columns are present when binding department summaries
                                    try
                                    {
                                        if (!_detailsGrid.Columns.Any(c => c.MappingName == "DepartmentName"))
                                        {
                                            try { _detailsGrid.SuspendLayout(); } catch { }
                                            _detailsGrid.Columns.Clear();
                                            _detailsGrid.Columns.Add(new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", MinimumWidth = 140 });
                                            _detailsGrid.Columns.Add(new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budget", MinimumWidth = 120 });
                                            _detailsGrid.Columns.Add(new GridNumericColumn { MappingName = "Amount", HeaderText = "Actual", MinimumWidth = 120 });
                                        }
                                    }
                                    catch { }
                                    finally { try { _detailsGrid.ResumeLayout(); } catch { } }
                                }
                            }

                            // Take a snapshot of the enumerable to prevent "Collection was modified"
                            // exceptions if the backing ObservableCollection (or similar) is modified
                            // on another thread while the grid paints or enumerates.
                            var detailsGrid = _detailsGrid;
                            if (detailsGrid == null) return;

                            if (val is System.Collections.IEnumerable en)
                            {
                                try
                                {
                                    var snapshot = en.Cast<object?>().ToList();

                                    System.Action assignSnapshot = () =>
                                    {
                                        try { detailsGrid.SuspendLayout(); } catch { }
                                        detailsGrid.DataSource = snapshot;
                                        try { detailsGrid.ResumeLayout(); } catch { }
                                    };

                                    if (detailsGrid.InvokeRequired)
                                    {
                                        try { detailsGrid.Invoke(assignSnapshot); } catch { }
                                    }
                                    else
                                    {
                                        assignSnapshot();
                                    }
                                }
                                catch
                                {
                                    // Fallback: assign the original value if Cast/ToList fails for some reason
                                    System.Action assignVal = () =>
                                    {
                                        try { detailsGrid.SuspendLayout(); } catch { }
                                        detailsGrid.DataSource = val;
                                        try { detailsGrid.ResumeLayout(); } catch { }
                                    };

                                    if (detailsGrid.InvokeRequired)
                                    {
                                        try { detailsGrid.Invoke(assignVal); } catch { }
                                    }
                                    else
                                    {
                                        assignVal();
                                    }
                                }
                            }
                            else
                            {
                                System.Action assignVal = () => detailsGrid.DataSource = val;
                                if (detailsGrid.InvokeRequired)
                                {
                                    try { detailsGrid.Invoke(assignVal); } catch { }
                                }
                                else
                                {
                                    assignVal();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Protect the UI thread - log and skip binding if Syncfusion throws
                            try { Serilog.Log.Warning(ex, "DashboardPanel: failed to bind details grid - skipping grid bind"); } catch { }
                        }
                    }
                }

                // Last refreshed
                if (_lblLastRefreshed != null && !_lblLastRefreshed.IsDisposed)
                {
                    _lblLastRefreshed.Text = $"Last: {_vm.LastUpdateTime}";
                }
            }
            catch { }

        }

        /// <summary>
        /// Updates overlay visibility based on data state.
        /// Shows loading overlay when IsLoading = true.
        /// Shows "No Data" overlay only when truly no data exists (Metrics collection empty) and not loading.
        /// Sample data in Metrics collection prevents overlay display.
        /// </summary>
        private void UpdateOverlays()
        {
            try
            {
                if (_vm == null) return;

                // Loading overlay: visible when ViewModel is loading
                if (_loadingOverlay != null)
                {
                    _loadingOverlay.Visible = _vm.IsLoading;
                }

                // No-data overlay: visible only when NOT loading AND truly no data exists
                if (_noDataOverlay != null)
                {
                    // Check if data collection has content (Metrics is the primary data source for MainViewModel)
                    bool hasMetrics = _vm.Metrics?.Count > 0;
                    bool hasActivityItems = _vm.ActivityItems?.Count > 0;

                    bool trulyNoData = !hasMetrics && !hasActivityItems;
                    bool shouldShowOverlay = !_vm.IsLoading && trulyNoData;

                    _noDataOverlay.Visible = shouldShowOverlay;
                    if (shouldShowOverlay) _noDataOverlay.BringToFront();
                }
            }
            catch (ObjectDisposedException)
            {
                // Control was disposed during update
            }
            catch { }
        }

        /// <summary>
        /// Legacy method - preserved for compatibility. Use UpdateOverlays() instead.
        /// </summary>
        private void UpdateNoData()
        {
            UpdateOverlays();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (IsDisposed || _vm == null) return;

                // Marshal to UI thread for all property changes that affect UI
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => ViewModel_PropertyChanged(sender, e)); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    try { BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e))); } catch { }
                    return;
                }

                if (e.PropertyName == nameof(_vm.IsLoading))
                {
                    try
                    {
                        UpdateOverlays();
                    }
                    catch { }
                }

                if (e.PropertyName == nameof(_vm.Metrics) ||
                    e.PropertyName == nameof(_vm.ActivityItems) ||
                    e.PropertyName == nameof(_vm.TotalBudget) ||
                    e.PropertyName == nameof(_vm.TotalExpenditure) ||
                    e.PropertyName == nameof(_vm.RemainingBudget) ||
                    e.PropertyName == nameof(_vm.LastUpdateTime))
                {
                    TryApplyViewModelBindings();
                    try
                    {
                        UpdateOverlays();
                    }
                    catch { }
                }

                // Update gauge values with smooth animation when gauge properties change
                if (e.PropertyName == nameof(_vm.TotalBudgetGauge))
                {
                    try
                    {
                        if (_budgetUtilizationGauge != null && !_budgetUtilizationGauge.IsDisposed)
                        {
                            _budgetUtilizationGauge.Value = (float)_vm.TotalBudgetGauge;
                            var valueLabel = _budgetUtilizationGauge.Parent?.Controls.OfType<GradientPanelExt>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
                            if (valueLabel != null) valueLabel.Text = $"{_vm.TotalBudgetGauge:F1}%";
                        }
                    }
                    catch { }
                }
                if (e.PropertyName == nameof(_vm.RevenueGauge))
                {
                    try
                    {
                        if (_revenueGauge != null && !_revenueGauge.IsDisposed)
                        {
                            _revenueGauge.Value = (float)_vm.RevenueGauge;
                            var valueLabel = _revenueGauge.Parent?.Controls.OfType<GradientPanelExt>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
                            if (valueLabel != null) valueLabel.Text = $"{_vm.RevenueGauge:F1}%";
                        }
                    }
                    catch { }
                }
                if (e.PropertyName == nameof(_vm.ExpensesGauge))
                {
                    try
                    {
                        if (_expenseGauge != null && !_expenseGauge.IsDisposed)
                        {
                            _expenseGauge.Value = (float)_vm.ExpensesGauge;
                            var valueLabel = _expenseGauge.Parent?.Controls.OfType<GradientPanelExt>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
                            if (valueLabel != null) valueLabel.Text = $"{_vm.ExpensesGauge:F1}%";
                        }
                    }
                    catch { }
                }
                if (e.PropertyName == nameof(_vm.NetPositionGauge))
                {
                    try
                    {
                        if (_varianceGauge != null && !_varianceGauge.IsDisposed)
                        {
                            _varianceGauge.Value = (float)_vm.NetPositionGauge;
                            var valueLabel = _varianceGauge.Parent?.Controls.OfType<GradientPanelExt>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
                            if (valueLabel != null) valueLabel.Text = $"{_vm.NetPositionGauge:F1}%";
                        }
                    }
                    catch { }
                }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("DashboardPanel: ViewModel_PropertyChanged - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "DashboardPanel: ViewModel_PropertyChanged failed");
            }
        }

        /// <summary>
        /// Navigates to another panel via parent form's DockingManager.
        /// Per Syncfusion demos navigation pattern.
        /// </summary>
        /// <typeparam name="TPanel">The panel type to navigate to.</typeparam>
        /// <param name="panelName">Display name for the panel.</param>
        private void NavigateToPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            try
            {
                var parentForm = this.FindForm();
                if (parentForm == null) return;

                // Prefer direct API on MainForm where possible - avoids reflection brittleness.
                if (parentForm is Forms.MainForm mf)
                {
                    try { mf.ShowPanel<TPanel>(panelName); return; } catch { }
                }

                // Fallback to reflection for older hosts
                var method = parentForm.GetType().GetMethod("DockUserControlPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(typeof(TPanel));
                    genericMethod.Invoke(parentForm, new object[] { panelName });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "DashboardPanel: NavigateToPanel<{Panel}> failed", typeof(TPanel).Name);
            }
        }

        /// <summary>
        /// Named handler for PanelHeader.RefreshClicked event (enables proper unsubscription).
        /// </summary>
        private void OnPanelHeaderRefreshClicked(object? sender, EventArgs e)
        {
            BeginInvoke(new Func<Task>(async () =>
            {
                try
                {
                    await (_vm?.RefreshCommand?.ExecuteAsync(null) ?? Task.CompletedTask);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "DashboardPanel: Refresh failed");
                }
            }));
        }

        /// <summary>
        /// Named handler for PanelHeader.CloseClicked event (enables proper unsubscription).
        /// </summary>
        private void OnPanelHeaderCloseClicked(object? sender, EventArgs e)
        {
            try
            {
                var parent = this.FindForm();
                var method = parent?.GetType().GetMethod("ClosePanel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                method?.Invoke(parent, new object[] { this.Name });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "DashboardPanel: Close panel failed");
            }
        }

        /// <summary>
        /// Loads dashboard data asynchronously.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;
                UpdateStatus("Loading dashboard data...");
                await EnsureLoadedAsync(ct);
                SetHasUnsavedChanges(false);
                UpdateStatus("Dashboard loaded successfully");
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Load cancelled");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Load failed: {ex.Message}");
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Saves dashboard data (dashboard is read-only, so no-op).
        /// </summary>
        public override async Task SaveAsync(CancellationToken ct)
        {
            // Dashboard is read-only, no save operation
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates dashboard state.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            var errors = new List<ValidationItem>();
            // Dashboard has no editable fields, always valid
            return ValidationResult.Success;
        }

        /// <summary>
        /// Focuses the first error (no errors in dashboard).
        /// </summary>
        public override void FocusFirstError()
        {
            // No editable fields to focus
        }

        /// <summary>
        /// Updates the status strip with a message.
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (_statusLabel != null && !IsDisposed)
            {
                if (InvokeRequired)
                    BeginInvoke(() => _statusLabel.Text = message);
                else
                    _statusLabel.Text = message;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from ViewModel events
                try { if (_viewModelPropertyChangedHandler != null && _vm is INotifyPropertyChanged npc) npc.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }

                // Button click handlers are wired with inline lambdas, no need to unsubscribe
                // They will be disposed with their parent controls

                // Unsubscribe from PanelHeader events using stored named handlers (no reflection needed)
                try
                {
                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "DashboardPanel: Failed to unsubscribe PanelHeader events");
                }

                // Clear Syncfusion control data sources before disposal per Syncfusion best practices.
                // Per Syncfusion docs: Always call Dispose() on SfDataGrid/SfListView to clear internal
                // event handlers and prevent memory leaks. SafeClearDataSource() extension handles
                // null-checking and DataSource = null assignment.
                try { if (_kpiList != null) _kpiList.SafeClearDataSource(); } catch { }
                try { if (_kpiList != null) _kpiList.SafeDispose(); } catch { }
                try { _detailsGrid.SafeClearDataSource(); } catch { }
                try { _detailsGrid.SafeDispose(); } catch { }

                // NOTE: If we had manually subscribed to SfDataGrid events (QueryCellStyle, etc.),
                // we would need to explicitly unsubscribe here before calling Dispose().
                // Current implementation uses data binding only, so no manual event cleanup needed.

                // Dispose UI controls
                try { _kpiList?.Dispose(); } catch { }
                try { _mainChartRegionEventWiring?.Dispose(); } catch { }
                _mainChartRegionEventWiring = null;
                try { _mainChart?.Dispose(); } catch { }
                try { _detailsGrid?.Dispose(); } catch { }
                try { _statusStrip?.Dispose(); } catch { }
                try { _toolStrip?.Dispose(); } catch { }
                try { _mainSplitContainer?.Dispose(); } catch { }
                try { _topPanel?.Dispose(); } catch { }
                try { _btnRefresh?.Dispose(); } catch { }
                try { _lblLastRefreshed?.Dispose(); } catch { }
                try { _statusLabel?.Dispose(); } catch { }

                // Dispose PanelHeader
                try { _panelHeader?.Dispose(); } catch { }

                // Dispose overlays
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }

                try { _errorProvider?.Dispose(); } catch { }

                // Dispose gauge controls
                try { _budgetUtilizationGauge?.Dispose(); } catch { }
                try { _revenueGauge?.Dispose(); } catch { }
                try { _expenseGauge?.Dispose(); } catch { }
                try { _varianceGauge?.Dispose(); } catch { }

                // Dispose shared tooltip
                try { _sharedTooltip?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
