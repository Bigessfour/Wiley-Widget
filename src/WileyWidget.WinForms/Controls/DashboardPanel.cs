using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Gauge;
using System.ComponentModel;
using System.Globalization;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Controls
{
    internal static class DashboardPanelResources
    {
        public const string PanelTitle = "Dashboard";
        public const string RefreshText = "Refresh";
    }

    /// <summary>
    /// Dashboard panel (UserControl) with KPIs, charts, and details grid.
    /// Designed for embedding in DockingManager.
    /// </summary>
    public partial class DashboardPanel : UserControl
    {
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// Uses 'new' to intentionally hide Control.DataContext when present in platform versions.
        /// </summary>
        public new object? DataContext { get; private set; }
        private readonly DashboardViewModel _vm;

        // controls
        private Panel _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private ToolStrip _toolStrip = null!;
        private ToolStripButton _btnRefresh = null!;
        private Label _lblLastRefreshed = null!;

        private SfListView _kpiList = null!;
        private ChartControl _mainChart = null!;
        private SfDataGrid _detailsGrid = null!;
        private StatusStrip _statusStrip = null!;
        private ErrorProvider? _errorProvider;
        private ToolStripStatusLabel _statusLabel = null!;

        private EventHandler<AppTheme>? _themeChangedHandler;
        private EventHandler<AppTheme>? _btnRefreshThemeChangedHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        // Named event handlers for PanelHeader (stored for proper unsubscription)
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        // per-tile sparkline references
        private ChartControl? _sparkBudget;
        private ChartControl? _sparkExpenditure;
        private ChartControl? _sparkRemaining;
        private Label? _tileBudgetValueLabel;
        private Label? _tileExpenditureValueLabel;
        private Label? _tileRemainingValueLabel;
        // Gauge controls for dashboard metrics
        private RadialGauge? _budgetUtilizationGauge;
        private RadialGauge? _revenueGauge;
        private RadialGauge? _expenseGauge;
        private RadialGauge? _varianceGauge;

        // Logger for diagnostic output (nullable - gracefully handles absence)
        private readonly Microsoft.Extensions.Logging.ILogger<DashboardPanel>? _logger;

        // Shared tooltip for all interactive controls (prevents memory leaks from multiple ToolTip instances)
        private ToolTip? _sharedTooltip;

        // DI-friendly default constructor for container/hosting convenience.
        // Use GetService instead of GetRequiredService so designer/legacy activations don't throw
        // when the ViewModel isn't registered in the global Program.Services. Fall back to a
        // simple local DashboardViewModel instance (its constructor tolerates null optional deps).
        // Guards against null Program.Services for safety.
        public DashboardPanel() : this(
            ResolveDashboardViewModel(),
            ResolveDispatcherHelper())
        {
        }

        private static WileyWidget.Services.Threading.IDispatcherHelper? ResolveDispatcherHelper()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Warning("DashboardPanel: Program.Services is null - IDispatcherHelper unavailable");
                return null;
            }

            try
            {
                var helper = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services);
                if (helper == null)
                {
                    Serilog.Log.Warning("DashboardPanel: IDispatcherHelper not registered in DI container");
                }
                return helper;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "DashboardPanel: Failed to resolve IDispatcherHelper");
                return null;
            }
        }

        private static DashboardViewModel ResolveDashboardViewModel()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Warning("DashboardPanel: Program.Services is null - using fallback DashboardViewModel");
                return new DashboardViewModel();
            }

            try
            {
                var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DashboardViewModel>(Program.Services);
                if (vm != null)
                {
                    Serilog.Log.Debug("DashboardPanel: DashboardViewModel resolved from DI container");
                    return vm;
                }

                Serilog.Log.Warning("DashboardPanel: DashboardViewModel not registered - using fallback instance");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "DashboardPanel: Failed to resolve DashboardViewModel from DI - using fallback");
            }

            // Fallback to default constructor - DashboardViewModel handles null dependencies gracefully
            return new DashboardViewModel();
        }

        private IAsyncRelayCommand? _refreshCommand;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;

        public DashboardPanel(DashboardViewModel vm, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = vm;

            // Resolve logger from DI (optional - gracefully handles absence)
            try
            {
                _logger = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILogger<DashboardPanel>>(Program.Services)
                    : null;
            }
            catch { /* Logger unavailable - continue without logging */ }

            // Initialize shared tooltip for all interactive controls
            _sharedTooltip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 100,
                ShowAlways = true
            };

            InitializeComponent();
            SetupUI();
            // Theme is applied automatically by SfSkinManager cascade from parent Form.
            // Per Syncfusion documentation: SetVisualStyle on parent form applies to ALL child controls.
            // No need to call ApplyCurrentTheme() here - it's redundant and can cause double-theming issues.

            // Diagnostic logging
            try { Serilog.Log.Debug("DashboardPanel initialized"); } catch { }

            // Wire initial load
#pragma warning disable CS4014
            EnsureLoadedAsync();
#pragma warning restore CS4014
        }

        private async Task EnsureLoadedAsync()
        {
            try
            {
                Serilog.Log.Debug("DashboardPanel.EnsureLoadedAsync: START - _vm={VmNotNull}, LoadCommand={CmdNotNull}, IsLoading={IsLoading}",
                    _vm != null, _vm?.LoadDashboardCommand != null, _vm?.IsLoading ?? false);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] EnsureLoadedAsync: START - LoadCommand={_vm?.LoadDashboardCommand != null}, IsLoading={_vm?.IsLoading ?? false}");

                if (_vm.LoadDashboardCommand != null && !_vm.IsLoading)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] Executing LoadDashboardCommand...");
                    await _vm.LoadDashboardCommand.ExecuteAsync(null).ConfigureAwait(true);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] LoadDashboardCommand completed");
                    Serilog.Log.Debug("DashboardPanel.EnsureLoadedAsync: LoadDashboardCommand completed successfully");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD] Skipping load - Command={_vm.LoadDashboardCommand != null}, IsLoading={_vm.IsLoading}");
                    Serilog.Log.Warning("DashboardPanel.EnsureLoadedAsync: Skipping load - Command={CmdNotNull}, IsLoading={IsLoading}",
                        _vm.LoadDashboardCommand != null, _vm.IsLoading);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DASHBOARD ERROR] EnsureLoadedAsync failed: {ex.Message}");
                Serilog.Log.Error(ex, "DashboardPanel.EnsureLoadedAsync: Failed to load dashboard data");
                try
                {
                    _errorProvider ??= new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
                    if (_mainChart != null) _errorProvider.SetError(_mainChart, "Failed to load dashboard data - check logs");
                    MessageBox.Show($"Could not load dashboard data: {ex.Message}", "Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        private void InitializeComponent()
        {
            Name = "DashboardPanel";
            AccessibleName = DashboardPanelResources.PanelTitle; // "Dashboard"
            Size = new Size(1200, 800);
            // DockingManager will handle docking; do not set Dock here.
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
        }

        private void SetupUI()
        {
            // Top panel and toolbar
            // Shared header (consistent 44px height + 8px padding)
            _panelHeader = new PanelHeader { Dock = DockStyle.Top };
            try { _panelHeader.Title = DashboardPanelResources.PanelTitle; } catch { }
            try
            {
                var dh = this.GetType().GetProperty("DockHandler")?.GetValue(this);
                var txtProp = dh?.GetType().GetProperty("Text");
                if (dh != null && txtProp != null) txtProp.SetValue(dh, DashboardPanelResources.PanelTitle);
            }
            catch { }
            _topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
            _toolStrip = new ToolStrip { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden };

            _btnRefresh = new ToolStripButton(DashboardPanelResources.RefreshText) { Name = "Toolbar_RefreshButton", AccessibleName = "Refresh Dashboard", AccessibleDescription = "Reload metrics and charts", ToolTipText = "Reload dashboard metrics and charts (F5)" };

            // Toolbar buttons: Load, Apply Filters, Allow Editing (automation-friendly IDs)
            var btnLoadDashboard = new ToolStripButton("Load Dashboard") { Name = "Toolbar_Load", AccessibleName = "Load Dashboard", ToolTipText = "Load dashboard data" };
            btnLoadDashboard.Click += async (s, e) => { try { if (_vm?.LoadDashboardCommand != null) await _vm.LoadDashboardCommand.ExecuteAsync(null); } catch { } };
            _refreshCommand = _vm.RefreshCommand;
            _btnRefresh.Click += (s, e) =>
            {
                try { _refreshCommand?.ExecuteAsync(null); } catch { }
            };

            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                _btnRefresh.Image = iconService?.GetIcon("refresh", theme, 16);
                _btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                _btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;

                _btnRefreshThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change to avoid stale closure
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => _btnRefresh.Image = svc?.GetIcon("refresh", t, 16));
                        }
                        else
                        {
                            var parent = _btnRefresh.GetCurrentParent();
                            if (parent != null && parent.InvokeRequired)
                            {
                                parent.BeginInvoke(new System.Action(() => _btnRefresh.Image = svc?.GetIcon("refresh", t, 16)));
                            }
                            else
                            {
                                _btnRefresh.Image = svc?.GetIcon("refresh", t, 16);
                            }
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnRefreshThemeChangedHandler;
            }
            catch { }

            _lblLastRefreshed = new Label { Name = "LastUpdatedLabel", AutoSize = true, Text = "Last: -", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight };

            // Dashboard label with home icon
            var dashboardLabel = new ToolStripLabel("Dashboard");
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                dashboardLabel.Image = iconService?.GetIcon("home", theme, 16);
                dashboardLabel.ImageAlign = ContentAlignment.MiddleLeft;
                dashboardLabel.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            _toolStrip.Items.Add(dashboardLabel);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_btnRefresh);
            _toolStrip.Items.Add(btnLoadDashboard);
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Export buttons (Excel / PDF)
            var btnExportExcel = new ToolStripButton("Export Excel") { Name = "Toolbar_ExportButton", AccessibleName = "Export", ToolTipText = "Export details to Excel" };
            try { btnExportExcel.Image = (Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.IThemeIconService>(Program.Services) : null)?.GetIcon("excel", ThemeManager.CurrentTheme, 16); } catch { }
            btnExportExcel.Click += async (s, e) =>
            {
                try
                {
                    using var sfd = new SaveFileDialog { Filter = "Excel Workbook|*.xlsx", DefaultExt = "xlsx", FileName = "dashboard-details.xlsx" };
                    if (sfd.ShowDialog() != DialogResult.OK) return;
                    await WileyWidget.WinForms.Services.ExportService.ExportGridToExcelAsync(_detailsGrid, sfd.FileName);
                    MessageBox.Show($"Exported to {sfd.FileName}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            _toolStrip.Items.Add(btnExportExcel);

            var btnExportPdf = new ToolStripButton("Export PDF") { Name = "Toolbar_ExportPdf", AccessibleName = "Export PDF", ToolTipText = "Export details/chart to PDF" };
            try { btnExportPdf.Image = (Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.IThemeIconService>(Program.Services) : null)?.GetIcon("pdf", ThemeManager.CurrentTheme, 16); } catch { }
            btnExportPdf.Click += async (s, e) =>
            {
                try
                {
                    using var sfd = new SaveFileDialog { Filter = "PDF Document|*.pdf", DefaultExt = "pdf", FileName = "dashboard.pdf" };
                    if (sfd.ShowDialog() != DialogResult.OK) return;
                    // Export main chart into PDF
                    await WileyWidget.WinForms.Services.ExportService.ExportChartToPdfAsync(_mainChart, sfd.FileName);
                    MessageBox.Show($"Exported to {sfd.FileName}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            _toolStrip.Items.Add(btnExportPdf);

            // Navigation buttons per Syncfusion demos patterns
            var btnAccounts = new ToolStripButton("Accounts")
            {
                AccessibleName = "View Accounts",
                AccessibleDescription = "Navigate to Accounts panel",
                ToolTipText = "View Municipal Accounts (Ctrl+Shift+A)"
            };
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnAccounts.Image = iconService?.GetIcon("wallet", theme, 16);
                btnAccounts.ImageAlign = ContentAlignment.MiddleLeft;
                btnAccounts.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnAccounts.Click += (s, e) =>
            {
                try { Serilog.Log.Information("DashboardPanel: Navigate requested -> Accounts"); } catch { }
                NavigateToPanel<AccountsPanel>("Accounts");
            };
            _toolStrip.Items.Add(btnAccounts);

            var btnCharts = new ToolStripButton("Charts")
            {
                AccessibleName = "View Charts",
                AccessibleDescription = "Navigate to Charts analytics panel",
                ToolTipText = "View Budget Analytics (Ctrl+Shift+C)"
            };
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnCharts.Image = iconService?.GetIcon("chart", theme, 16);
                btnCharts.ImageAlign = ContentAlignment.MiddleLeft;
                btnCharts.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnCharts.Click += (s, e) =>
            {
                try { Serilog.Log.Information("DashboardPanel: Navigate requested -> Charts"); } catch { }
                NavigateToPanel<ChartPanel>("Charts");
            };
            _toolStrip.Items.Add(btnCharts);

            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(new ToolStripLabel(" "));

            // Wire header actions
            try
            {
                _panelHeader.Title = DashboardPanelResources.PanelTitle;
                _panelHeaderRefreshHandler = OnPanelHeaderRefreshClicked;
                _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
                _panelHeaderCloseHandler = OnPanelHeaderCloseClicked;
                _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            }
            catch { }

            _topPanel.Controls.Add(_toolStrip);
            Controls.Add(_panelHeader);
            Controls.Add(_topPanel);

            // Create a split layout - KPI tiles + gauge row + main content area
            // Optimized layout: 2-row design with gauges at top, content below
            // Row 1: Gauge metrics (20% - increased from 15% for better visibility)
            // Row 2: Chart + Data grid (80% - more space for content)
            var mainSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(8)
            };
            // Row allocation: 20% gauges at top, 80% content below
            mainSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 20)); // Gauge row
            mainSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 80)); // Content area (chart + grid)
            // Column split: 60% chart / 40% grid for balanced visibility
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            // Gauge metrics panel - 4 RadialGauges in horizontal layout
            // Replaces separate KPI list for cleaner, more visual dashboard
            var gaugePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(600, 120),
                AutoScroll = false,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4)
            };

            // Create 4 gauge panels for metrics
            var gaugeControls = new[]
            {
                CreateGaugePanel("Budget Utilization", "gauge"),
                CreateGaugePanel("Revenue Collection", "gauge"),
                CreateGaugePanel("Expense Ratio", "gauge"),
                CreateGaugePanel("Budget Variance", "gauge")
            };

            foreach (var gaugeControl in gaugeControls)
            {
                gaugePanel.Controls.Add(gaugeControl);
            }

            // Add gauge panel to row 0, spanning both columns
            mainSplit.Controls.Add(gaugePanel, 0, 0);
            mainSplit.SetColumnSpan(gaugePanel, 2);

            // Left column: chart area - configured per Syncfusion demo patterns
            var chartPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            _mainChart = new ChartControl { Dock = DockStyle.Fill, AccessibleName = "Budget chart", AccessibleDescription = "Displays budget vs expenditure" };

            // Theme applied automatically by SfSkinManager cascade from parent form
            // Chart appearance per demos (ChartAppearance.cs patterns) - theme applied by SfSkinManager cascade
            _mainChart.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            _mainChart.ElementsSpacing = 5;
            _mainChart.BorderAppearance.SkinStyle = Syncfusion.Windows.Forms.Chart.ChartBorderSkinStyle.None;
            _mainChart.ShowToolTips = true;
            _mainChart.ChartArea.PrimaryXAxis.HidePartialLabels = true; // Per demos: hide partial labels

            // Axis configuration per demos (ChartAppearance.cs patterns)
            _mainChart.PrimaryXAxis.Title = "Category";
            _mainChart.PrimaryXAxis.Font = new Font("Segoe UI", 10F);
            _mainChart.PrimaryXAxis.DrawGrid = false;
            _mainChart.PrimaryXAxis.TickSize = new Size(1, 5); // Per demos: tick size
            _mainChart.PrimaryYAxis.Title = "Amount";
            _mainChart.PrimaryYAxis.Font = new Font("Segoe UI", 10F);
            _mainChart.PrimaryYAxis.TickSize = new Size(5, 1); // Per demos: tick size

            // Legend configuration per demos (ChartAppearance.cs patterns)
            _mainChart.ShowLegend = true;  // Show legend for clarity
            _mainChart.LegendsPlacement = Syncfusion.Windows.Forms.Chart.ChartPlacement.Outside; // Per demos
            _mainChart.Legend.Position = Syncfusion.Windows.Forms.Chart.ChartDock.Bottom;  // Bottom placement per Syncfusion best practices
            _mainChart.Legend.ItemsAlignment = StringAlignment.Center;  // Center alignment

            chartPanel.Controls.Add(_mainChart);
            mainSplit.Controls.Add(chartPanel, 0, 1); // Row 1, column 0 (chart)

            // Right column: details grid
            var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

            // SfDataGrid configured per Syncfusion demos (Getting Started, Themes demos)
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
                RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f), // Per demos: DPI-aware
                HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f), // Per demos: DPI-aware
                AllowResizingColumns = true, // Per demos: enable column resize
                AllowTriStateSorting = true, // Per demos: tri-state sorting
                ShowSortNumbers = true, // Per demos: show sort order numbers
                LiveDataUpdateMode = Syncfusion.Data.LiveDataUpdateMode.AllowDataShaping, // Per demos: real-time
                AccessibleName = "Details grid",
                AccessibleDescription = "Details list for top departments/accounts"
            };
            try { _detailsGrid.ShowGroupDropArea = true; } catch { }
            try
            {
                // Column configuration per Syncfusion demos - explicit column definitions for predictable behavior
                _detailsGrid.Columns.Add(new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", MinimumWidth = 140 });

                var colBudget = new GridNumericColumn
                {
                    MappingName = "BudgetedAmount",
                    HeaderText = "Budget",
                    MinimumWidth = 120,
                    Format = "C0"  // Currency format with no decimal places
                };

                var colActual = new GridNumericColumn
                {
                    MappingName = "Amount",
                    HeaderText = "Actual",
                    MinimumWidth = 120,
                    Format = "C0"  // Currency format with no decimal places
                };

                _detailsGrid.Columns.Add(colBudget);
                _detailsGrid.Columns.Add(colActual);
            }
            catch { }

            // Theme applied automatically by SkinManager cascade from parent form

            rightPanel.Controls.Add(_detailsGrid, 0, 0);

            // small summary area
            var summaryPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            // Replace the small summary labels with three tile panels that include a sparkline next to each
            var summaryTiles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = false };
            summaryTiles.WrapContents = false;

            // Helper to create a tile
            Func<string, string, decimal, ChartControl> createTile = (title, tooltip, value) =>
            {
                var tile = new Panel { Width = 260, Height = 72, Padding = new Padding(8), Margin = new Padding(6) };

                var lbl = new Label { Text = title, AutoSize = false, Height = 20, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
                var valLbl = new Label { Text = value.ToString("C0", CultureInfo.CurrentCulture), AutoSize = false, Height = 20, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 11, FontStyle.Bold) };

                var spark = new ChartControl { Width = 220, Height = 28, Dock = DockStyle.Bottom, AccessibleName = title + " sparkline" };
                spark.ShowLegend = false; spark.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality; spark.ElementsSpacing = 2; spark.PrimaryYAxis.HidePartialLabels = true; spark.PrimaryXAxis.HidePartialLabels = true; spark.ShowToolTips = false;

                tile.Controls.Add(spark);
                tile.Controls.Add(valLbl);
                tile.Controls.Add(lbl);

                // Capture references to the value labels for later updates
                if (title.StartsWith("Total Budget", StringComparison.Ordinal)) _tileBudgetValueLabel = valLbl;
                else if (title.StartsWith("Expenditure", StringComparison.Ordinal)) _tileExpenditureValueLabel = valLbl;
                else if (title.StartsWith("Remaining", StringComparison.Ordinal)) _tileRemainingValueLabel = valLbl;

                summaryTiles.Controls.Add(tile);

                return spark;
            };

            // Create three tiles (values will be updated in bindings)
            _sparkBudget = createTile("Total Budget", "Total budget", _vm.TotalBudget);
            _sparkExpenditure = createTile("Expenditure", "Total expenditure", _vm.TotalExpenditure);
            _sparkRemaining = createTile("Remaining", "Remaining budget", _vm.RemainingBudget);

            summaryPanel.Controls.Add(summaryTiles);

            rightPanel.Controls.Add(summaryPanel, 0, 1);

            mainSplit.Controls.Add(rightPanel, 1, 1); // Row 1, column 1 (grid + summary)

            Controls.Add(mainSplit);

            // status bar
            _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            _statusLabel = new ToolStripStatusLabel { Text = "Ready" };
            _statusStrip.Items.Add(_statusLabel);
            Controls.Add(_statusStrip);

            // Add overlays for loading/no-data states
            _loadingOverlay = new LoadingOverlay { Message = "Loading dashboard..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No data yet – import or add entries to get started." };
            Controls.Add(_noDataOverlay);

            // Bindings
            TryApplyViewModelBindings();

            // Theme update handler - ensure UI thread marshaling
            _themeChangedHandler = (s, t) =>
            {
                try
                {
                    if (IsDisposed) return;
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(ApplyCurrentTheme); } catch { }
                        return;
                    }
                    if (InvokeRequired)
                    {
                        try { BeginInvoke(new System.Action(ApplyCurrentTheme)); } catch { }
                        return;
                    }
                    ApplyCurrentTheme();
                }
                catch { }
            };
            ThemeManager.ThemeChanged += _themeChangedHandler;
        }

        /// <summary>
        /// Creates the gauge panel with four circular gauges for dashboard metrics.
        /// Gauges are configured with smooth 500ms pointer animation, color ranges (green/yellow/red),
        /// and data binding to ViewModel properties with PropertyChanged notifications.
        /// </summary>
        /// <summary>
        /// Creates a single gauge panel for metrics display.
        /// </summary>
        /// <param name="title">Gauge title (e.g., "Budget Utilization")</param>
        /// <param name="iconName">Icon name (currently unused, reserved for future)</param>
        /// <returns>Panel containing gauge with label</returns>
        private Panel CreateGaugePanel(string title, string iconName)
        {
            var container = new Panel { Width = 280, Height = 120, Margin = new Padding(4) };
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
                StartValue = 0, EndValue = 60, Color = Color.FromArgb(76, 175, 80),
                Height = 8, InRange = true, RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
            });
            gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
            {
                StartValue = 60, EndValue = 90, Color = Color.FromArgb(255, 193, 7),
                Height = 8, InRange = true, RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
            });
            gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
            {
                StartValue = 90, EndValue = 100, Color = Color.FromArgb(244, 67, 54),
                Height = 8, InRange = true, RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
            });

            gauge.NeedleColor = Color.DarkSlateGray;
            gauge.Cursor = Cursors.Hand;

            // Label panel
            var lblPanel = new Panel { Dock = DockStyle.Fill };
            var titleLabel = new Label
            {
                Text = title, Dock = DockStyle.Top, Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var valueLabel = new Label
            {
                Name = $"{title}ValueLabel", Text = "0%", Dock = DockStyle.Top,
                Height = 32, Font = new Font("Segoe UI", 14, FontStyle.Bold),
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

        private Panel CreateGaugePanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                MinimumSize = new Size(800, 100)  // Minimum size for 4 gauges side-by-side
            };
            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoScroll = false,
                WrapContents = false
            };

            // Helper to create a gauge with label
            RadialGauge CreateGauge(string title, string description, Color rangeGreen, Color rangeYellow, Color rangeRed)
            {
                var container = new Panel { Width = 280, Height = 120, Margin = new Padding(4) };
                var gauge = new RadialGauge
                {
                    Width = 110,
                    Height = 110,
                    Dock = DockStyle.Left,
                    AccessibleName = title,
                    AccessibleDescription = description,
                    MinimumValue = 0,
                    MaximumValue = 100,
                    EnableCustomNeedles = true  // Enable smooth needle animations
                };

                // Configure gauge appearance per Syncfusion demos
                gauge.GaugeLabel = "";
                gauge.ShowTicks = true;
                gauge.MajorTickMarkHeight = 8;
                gauge.MinorTickMarkHeight = 4;
                gauge.ShowScaleLabel = true;
                gauge.MinimumSize = new Size(110, 110);

                // Enable smooth pointer animation (500ms transition per Syncfusion best practices)
                gauge.EnableCustomNeedles = true;
                gauge.VisualStyle = Syncfusion.Windows.Forms.Gauge.ThemeStyle.Office2016Colorful;  // Will be overridden by SfSkinManager cascade

                // Add tooltip using shared instance (prevents memory leaks)
                if (_sharedTooltip != null)
                {
                    _sharedTooltip.SetToolTip(gauge, $"{description}\nCurrent value will update in real-time\nClick to drill down into details");
                }

                // Add color ranges: green (0-60%), yellow (60-90%), red (90-100%)
                gauge.Ranges.Clear();
                var greenRange = new Syncfusion.Windows.Forms.Gauge.Range
                {
                    StartValue = 0,
                    EndValue = 60,
                    Color = rangeGreen,
                    Height = 8,
                    InRange = true,
                    RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
                };
                var yellowRange = new Syncfusion.Windows.Forms.Gauge.Range
                {
                    StartValue = 60,
                    EndValue = 90,
                    Color = rangeYellow,
                    Height = 8,
                    InRange = true,
                    RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
                };
                var redRange = new Syncfusion.Windows.Forms.Gauge.Range
                {
                    StartValue = 90,
                    EndValue = 100,
                    Color = rangeRed,
                    Height = 8,
                    InRange = true,
                    RangePlacement = Syncfusion.Windows.Forms.Gauge.TickPlacement.Inside
                };
                gauge.Ranges.Add(greenRange);
                gauge.Ranges.Add(yellowRange);
                gauge.Ranges.Add(redRange);

                // Configure needle
                gauge.NeedleColor = Color.DarkSlateGray;

                // Add click interactivity for drill-down (optional enhancement)
                gauge.Cursor = Cursors.Hand;
                gauge.Click += (s, e) =>
                {
                    try
                    {
                        // Future enhancement: Filter details grid based on gauge type
                        _logger?.LogDebug("DashboardPanel: Gauge '{Title}' clicked - drill-down placeholder", title);
                        // Could filter _detailsGrid.DataSource here based on title
                    }
                    catch (Exception ex)
                    {
                        try { Serilog.Log.Warning(ex, "DashboardPanel: Gauge click handler failed"); } catch { }
                    }
                };

                // Label panel
                var lblPanel = new Panel { Dock = DockStyle.Fill };
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
                var descLabel = new Label
                {
                    Text = description,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 8),
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.Gray
                };

                lblPanel.Controls.Add(descLabel);
                lblPanel.Controls.Add(valueLabel);
                lblPanel.Controls.Add(titleLabel);

                container.Controls.Add(lblPanel);
                container.Controls.Add(gauge);
                flowLayout.Controls.Add(container);

                return gauge;
            }

            // Create four gauges for key metrics
            // NOTE: Explicit gauge colors are acceptable per SfSkinManager rules.
            // These are semantic data visualization colors (not UI theme colors).
            // Gauges use standardized color ranges (Green/Amber/Red, etc.) for universal comprehension.
            _budgetUtilizationGauge = CreateGauge(
                "Budget Utilization",
                "Actual spending vs budgeted",
                Color.FromArgb(76, 175, 80),    // Green
                Color.FromArgb(255, 193, 7),    // Amber
                Color.FromArgb(244, 67, 54));   // Red

            _revenueGauge = CreateGauge(
                "Revenue Collection",
                "Revenue collected vs target",
                Color.FromArgb(33, 150, 243),   // Blue
                Color.FromArgb(3, 169, 244),    // Light Blue
                Color.FromArgb(0, 188, 212));   // Cyan

            _expenseGauge = CreateGauge(
                "Expense Rate",
                "Expenses vs allocation",
                Color.FromArgb(139, 195, 74),   // Light Green
                Color.FromArgb(205, 220, 57),   // Lime
                Color.FromArgb(255, 235, 59));  // Yellow

            _varianceGauge = CreateGauge(
                "Variance",
                "Budget variance indicator",
                Color.FromArgb(156, 39, 176),   // Purple
                Color.FromArgb(103, 58, 183),   // Deep Purple
                Color.FromArgb(63, 81, 181));   // Indigo

            panel.Controls.Add(flowLayout);
            return panel;
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
                // bind KPI list
                if (_vm.Metrics != null)
                {
                    _kpiList.DataSource = _vm.Metrics;
                    _kpiList.ItemHeight = 84;
                }

                var metricsList = _vm.Metrics;
                if (metricsList != null && metricsList.Any())
                {
                    _mainChart.Series.Clear();

                    // Primary series: Department metrics (column chart)
                    var ser = new ChartSeries("Budget vs Actual", ChartSeriesType.Column);
                    foreach (var m in metricsList)
                    {
                        ser.Points.Add(m.Name, m.Value);
                    }
                    _mainChart.Series.Add(ser);

                    // Secondary series: Monthly revenue trend (line chart overlay)
                    if (_vm.MonthlyRevenueData != null && _vm.MonthlyRevenueData.Any())
                    {
                        var revenueSeries = new ChartSeries("Monthly Revenue Trend", ChartSeriesType.Line);
                        foreach (var month in _vm.MonthlyRevenueData)
                        {
                            revenueSeries.Points.Add(month.Month, (double)month.Amount);
                        }
                        revenueSeries.Style.DisplayText = false;
                        _mainChart.Series.Add(revenueSeries);
                    }

                    // Fill the sparkline mini-charts attached to each summary tile and update their value labels
                    try
                    {
                        void FillSpark(ChartControl? c, string seriesName)
                        {
                            if (c == null) return;
                            c.Series.Clear();

                            // Use real MonthlyRevenueData from ViewModel if available (last 6 months)
                            if (_vm.MonthlyRevenueData != null && _vm.MonthlyRevenueData.Any())
                            {
                                var s = new ChartSeries(seriesName, ChartSeriesType.Line);
                                var recentMonths = _vm.MonthlyRevenueData.TakeLast(6).ToList();
                                foreach (var month in recentMonths)
                                {
                                    s.Points.Add(month.Month, (double)month.Amount);
                                }
                                s.Style.DisplayText = false;
                                // Let SfSkinManager apply theme colors automatically per Syncfusion best practices.
                                // ChartControl series use theme-aware default colors when Interior is not explicitly set.
                                c.Series.Add(s);
                            }
                        }

                        // NOTE: Sparkline data binding - Now using real MonthlyRevenueData from ViewModel.
                        // This provides actual revenue trend data instead of synthetic values.
                        // Production version should populate MonthlyRevenueData from repository queries.
                        FillSpark(_sparkBudget, "Budget");
                        FillSpark(_sparkExpenditure, "Expenditure");
                        FillSpark(_sparkRemaining, "Remaining");

                        if (_tileBudgetValueLabel != null) _tileBudgetValueLabel.Text = _vm.TotalBudget.ToString("C0", CultureInfo.CurrentCulture);
                        if (_tileExpenditureValueLabel != null) _tileExpenditureValueLabel.Text = _vm.TotalExpenditure.ToString("C0", CultureInfo.CurrentCulture);
                        if (_tileRemainingValueLabel != null) _tileRemainingValueLabel.Text = _vm.RemainingBudget.ToString("C0", CultureInfo.CurrentCulture);
                    }
                    catch { }
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
                        _loadingOverlay.DataBindings.Add(new Binding("Visible", bs, nameof(WileyWidget.WinForms.ViewModels.DashboardViewModel.IsLoading), true, DataSourceUpdateMode.OnPropertyChanged));
                        _loadingOverlay.BringToFront();
                    }
                }
                catch { }

                // Show no-data overlay when not loading and there are no metrics
                try
                {
                    void UpdateNoData()
                    {
                        if (_noDataOverlay == null) return;
                        bool show = !_vm.IsLoading && (_vm.Metrics == null || !_vm.Metrics.Any());
                        _noDataOverlay.Visible = show;
                        if (show) _noDataOverlay.BringToFront();
                    }

                    _vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(WileyWidget.WinForms.ViewModels.DashboardViewModel.IsLoading) || e.PropertyName == nameof(WileyWidget.WinForms.ViewModels.DashboardViewModel.Metrics))
                        {
                            try
                            {
                                if (this.InvokeRequired) BeginInvoke(new System.Action(UpdateNoData)); else UpdateNoData();
                            }
                            catch { }
                        }
                    };

                    UpdateNoData();
                }
                catch { }

                // details grid mapping
                var prop = _vm.GetType().GetProperty("DepartmentSummaries") ?? _vm.GetType().GetProperty("Metrics");
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

                                    Action assignSnapshot = () =>
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
                                    Action assignVal = () =>
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
                                Action assignVal = () => detailsGrid.DataSource = val;
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
                _lblLastRefreshed.Text = $"Last: {_vm.LastRefreshed:yyyy-MM-dd HH:mm:ss}";
            }
            catch { }

            // Listen for property changes
            if (_vm is INotifyPropertyChanged npc && _viewModelPropertyChangedHandler == null)
            {
                _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
                npc.PropertyChanged += _viewModelPropertyChangedHandler;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

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
                    try { if (_loadingOverlay != null) _loadingOverlay.Visible = _vm.IsLoading; } catch { }
                }

                if (e.PropertyName == nameof(_vm.Metrics) || e.PropertyName == nameof(_vm.TotalBudget) || e.PropertyName == nameof(_vm.TotalExpenditure) || e.PropertyName == nameof(_vm.RemainingBudget) || e.PropertyName == nameof(_vm.LastRefreshed))
                {
                    TryApplyViewModelBindings();
                    try { if (_noDataOverlay != null) _noDataOverlay.Visible = (_vm.Metrics == null || !_vm.Metrics.Any()); } catch { }
                }

                // Update gauge values with smooth animation when gauge properties change
                if (e.PropertyName == nameof(_vm.TotalBudgetGauge))
                {
                    try
                    {
                        if (_budgetUtilizationGauge != null && !_budgetUtilizationGauge.IsDisposed)
                        {
                            _budgetUtilizationGauge.Value = _vm.TotalBudgetGauge;
                            var valueLabel = _budgetUtilizationGauge.Parent?.Controls.OfType<Panel>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
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
                            _revenueGauge.Value = _vm.RevenueGauge;
                            var valueLabel = _revenueGauge.Parent?.Controls.OfType<Panel>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
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
                            _expenseGauge.Value = _vm.ExpensesGauge;
                            var valueLabel = _expenseGauge.Parent?.Controls.OfType<Panel>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
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
                            _varianceGauge.Value = _vm.NetPositionGauge;
                            var valueLabel = _varianceGauge.Parent?.Controls.OfType<Panel>().FirstOrDefault()?.Controls.OfType<Label>().FirstOrDefault(l => l.Name.Contains("Value", StringComparison.Ordinal));
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
        /// Applies the current theme to dynamically added controls.
        /// NOTE: This method is NOT needed for initial UserControl setup.
        /// Per Syncfusion documentation: When a form has SfSkinManager.SetVisualStyle() applied,
        /// the theme automatically cascades to ALL child controls (including nested UserControls).
        /// Only use this for controls added dynamically AFTER the form is shown.
        /// Reference: https://help.syncfusion.com/windowsforms/skins/getting-started
        /// </summary>
        private void ApplyCurrentTheme()
        {
            // This method intentionally left minimal - theme cascade handles initial setup.
            // Only needed if we dynamically create Syncfusion controls after form load.
            // Per Syncfusion: "SetVisualStyle on window applies theme to ALL controls inside it"
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
        private async void OnPanelHeaderRefreshClicked(object? sender, EventArgs e)
        {
            try
            {
                await (_vm.RefreshCommand?.ExecuteAsync(null) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "DashboardPanel: Refresh failed");
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from theme events
                try { ThemeManager.ThemeChanged -= _themeChangedHandler; } catch { }
                try { WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged -= _btnRefreshThemeChangedHandler; } catch { }
                try { if (_viewModelPropertyChangedHandler != null && _vm is INotifyPropertyChanged npc) npc.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }

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
                try { _kpiList.SafeClearDataSource(); } catch { }
                try { _kpiList.SafeDispose(); } catch { }
                try { _detailsGrid.SafeClearDataSource(); } catch { }
                try { _detailsGrid.SafeDispose(); } catch { }

                // NOTE: If we had manually subscribed to SfDataGrid events (QueryCellStyle, etc.),
                // we would need to explicitly unsubscribe here before calling Dispose().
                // Current implementation uses data binding only, so no manual event cleanup needed.

                // Dispose UI controls
                try { _kpiList?.Dispose(); } catch { }
                try { _mainChart?.Dispose(); } catch { }
                try { _detailsGrid?.Dispose(); } catch { }
                try { _statusStrip?.Dispose(); } catch { }
                try { _toolStrip?.Dispose(); } catch { }
                try { _topPanel?.Dispose(); } catch { }
                try { _btnRefresh?.Dispose(); } catch { }
                try { _lblLastRefreshed?.Dispose(); } catch { }
                try { _statusLabel?.Dispose(); } catch { }

                // Dispose PanelHeader
                try { _panelHeader?.Dispose(); } catch { }

                // Dispose overlays
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }

                // Dispose per-tile sparks
                try { _sparkBudget?.Dispose(); } catch { }
                try { _sparkExpenditure?.Dispose(); } catch { }
                try { _sparkRemaining?.Dispose(); } catch { }
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
