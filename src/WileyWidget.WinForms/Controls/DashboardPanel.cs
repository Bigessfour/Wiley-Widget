using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using System.ComponentModel;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Theming;
using WileyWidget.ViewModels;
using CommunityToolkit.Mvvm.Input;

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
        public object? DataContext { get; private set; }
        private readonly DashboardViewModel _vm;

        // controls
        private Panel _topPanel = null!;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private ToolStrip _toolStrip = null!;
        private ToolStripButton _btnRefresh = null!;
        private Label _lblLastRefreshed = null!;

        private Syncfusion.WinForms.ListView.SfListView _kpiList = null!;
        private ChartControl _mainChart = null!;
        private Syncfusion.WinForms.DataGrid.SfDataGrid _detailsGrid = null!;
        private StatusStrip _statusStrip = null!;
        private ErrorProvider? _errorProvider;
        private ToolStripStatusLabel _statusLabel = null!;

        private EventHandler<AppTheme>? _themeChangedHandler;
        private EventHandler<AppTheme>? _btnRefreshThemeChangedHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        // per-tile sparkline references
        private ChartControl? _sparkBudget;
        private ChartControl? _sparkExpenditure;
        private ChartControl? _sparkRemaining;
        private Label? _tileBudgetValueLabel;
        private Label? _tileExpenditureValueLabel;
        private Label? _tileRemainingValueLabel;

        // DI-friendly default constructor for container/hosting convenience
        public DashboardPanel() : this(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DashboardViewModel>(Program.Services),
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services))
        {
        }

        private IAsyncRelayCommand? _refreshCommand;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;

        public DashboardPanel(DashboardViewModel vm, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = vm;
            InitializeComponent();
            SetupUI();
            ApplyCurrentTheme();

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
                if (_vm.LoadDashboardCommand != null && !_vm.IsLoading)
                {
                    await _vm.LoadDashboardCommand.ExecuteAsync(null).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _errorProvider ??= new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
                    if (_mainChart != null) _errorProvider.SetError(_mainChart, "Failed to load dashboard data — check logs");
                    MessageBox.Show($"Could not load dashboard data: {ex.Message}", "Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        private void InitializeComponent()
        {
            Name = "DashboardPanel";
            Size = new Size(1200, 800);
            Dock = DockStyle.Fill;
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
        }

        private void SetupUI()
        {
            // Top panel and toolbar
            // Shared header (consistent 44px height + 8px padding)
            _panelHeader = new PanelHeader { Dock = DockStyle.Top };
            _topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
            _toolStrip = new ToolStrip { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden };

            _btnRefresh = new ToolStripButton(DashboardPanelResources.RefreshText) { AccessibleName = "Refresh Dashboard", AccessibleDescription = "Reload metrics and charts", ToolTipText = "Reload dashboard metrics and charts (F5)" };
            _refreshCommand = _vm.RefreshCommand;
            _btnRefresh.Click += (s, e) =>
            {
                try { _refreshCommand?.ExecuteAsync(null); } catch { }
            };

            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                _btnRefresh.Image = iconService?.GetIcon("refresh", theme, 16);
                _btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                _btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;

                _btnRefreshThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => _btnRefresh.Image = iconService?.GetIcon("refresh", t, 16));
                        }
                        else if (_btnRefresh.GetCurrentParent()?.InvokeRequired ?? false)
                        {
                            _btnRefresh.GetCurrentParent().BeginInvoke(new Action(() => _btnRefresh.Image = iconService?.GetIcon("refresh", t, 16)));
                        }
                        else
                        {
                            _btnRefresh.Image = iconService?.GetIcon("refresh", t, 16);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnRefreshThemeChangedHandler;
            }
            catch { }

            _lblLastRefreshed = new Label { AutoSize = true, Text = "Last: —", Anchor = AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight };

            // Dashboard label with home icon
            var dashboardLabel = new ToolStripLabel("Dashboard");
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                dashboardLabel.Image = iconService?.GetIcon("home", theme, 16);
                dashboardLabel.ImageAlign = ContentAlignment.MiddleLeft;
                dashboardLabel.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            _toolStrip.Items.Add(dashboardLabel);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_btnRefresh);
            _toolStrip.Items.Add(new ToolStripSeparator());

            // Export buttons (Excel / PDF)
            var btnExportExcel = new ToolStripButton("Export Excel") { ToolTipText = "Export details to Excel" };
            try { btnExportExcel.Image = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)?.GetIcon("excel", ThemeManager.CurrentTheme, 16); } catch { }
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

            var btnExportPdf = new ToolStripButton("Export PDF") { ToolTipText = "Export details/chart to PDF" };
            try { btnExportPdf.Image = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)?.GetIcon("pdf", ThemeManager.CurrentTheme, 16); } catch { }
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
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnAccounts.Image = iconService?.GetIcon("wallet", theme, 16);
                btnAccounts.ImageAlign = ContentAlignment.MiddleLeft;
                btnAccounts.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnAccounts.Click += (s, e) => NavigateToPanel<AccountsPanel>("Accounts");
            _toolStrip.Items.Add(btnAccounts);

            var btnCharts = new ToolStripButton("Charts")
            {
                AccessibleName = "View Charts",
                AccessibleDescription = "Navigate to Charts analytics panel",
                ToolTipText = "View Budget Analytics (Ctrl+Shift+C)"
            };
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnCharts.Image = iconService?.GetIcon("chart", theme, 16);
                btnCharts.ImageAlign = ContentAlignment.MiddleLeft;
                btnCharts.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnCharts.Click += (s, e) => NavigateToPanel<ChartPanel>("Charts");
            _toolStrip.Items.Add(btnCharts);

            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(new ToolStripLabel(" "));

            // Wire header actions
            try
            {
                _panelHeader.Title = DashboardPanelResources.PanelTitle;
                _panelHeader.RefreshClicked += async (s, e) => { try { await (_vm.RefreshCommand?.ExecuteAsync(null) ?? Task.CompletedTask); } catch { } };
                _panelHeader.CloseClicked += (s, e) =>
                {
                    try
                    {
                        var parent = this.FindForm();
                        var method = parent?.GetType().GetMethod("ClosePanel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        method?.Invoke(parent, new object[] { this.Name });
                    }
                    catch { }
                };
            }
            catch { }

            _topPanel.Controls.Add(_toolStrip);
            Controls.Add(_panelHeader);
            Controls.Add(_topPanel);

            // Create a split layout - top KPI tiles area + main content area
            var mainSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(8)
            };
            mainSplit.RowStyles.Add(new RowStyle(SizeType.Absolute, 140)); // KPI row
            mainSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // content
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            // KPI list - horizontal tiles per Syncfusion SfListView demos
            _kpiList = new Syncfusion.WinForms.ListView.SfListView
            {
                Dock = DockStyle.Fill,
                View = Syncfusion.WinForms.ListView.Enums.ListViewMode.Tiles,
                AllowMultiSelection = false,
                HotTracking = true,
                ShowGroups = false, // Per demos: disable grouping for tiles
                ShowHeader = false, // Per demos: hide header for tiles
                ShowToolTip = true, // Per demos: enable tooltips
                AutoSizeMode = Syncfusion.WinForms.ListView.Enums.AutoSizeMode.None, // Per demos: fixed size tiles
                AccessibleName = "Summary metrics",
                AccessibleDescription = "Quick metrics such as Total Budget, Expenditures and Remaining funds"
            };
            // Configure tile size for KPIs
            _kpiList.ItemSize = new Size(180, 80);
            mainSplit.Controls.Add(_kpiList, 0, 0);
            mainSplit.SetColumnSpan(_kpiList, 2);

            // Left column: chart area - configured per Syncfusion demo patterns
            var chartPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            _mainChart = new ChartControl { Dock = DockStyle.Fill, AccessibleName = "Budget chart", AccessibleDescription = "Displays budget vs expenditure" };

            // Apply Metro skin per demos
            _mainChart.Skins = Syncfusion.Windows.Forms.Chart.Skins.Metro;

            // Chart appearance per demos (ChartAppearance.cs patterns)
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
            _mainChart.ShowLegend = false; // Hide for this compact chart
            _mainChart.LegendsPlacement = Syncfusion.Windows.Forms.Chart.ChartPlacement.Outside; // Per demos

            chartPanel.Controls.Add(_mainChart);
            mainSplit.Controls.Add(chartPanel, 0, 1);

            // Right column: details grid
            var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

            // SfDataGrid configured per Syncfusion demos (Getting Started, Themes demos)
            _detailsGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
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
                // Column configuration per demos - use NumberFormatInfo for proper formatting
                var nfi = new System.Globalization.NumberFormatInfo { NumberDecimalDigits = 0, CurrencyDecimalDigits = 0 };
                _detailsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", MinimumWidth = 140 });
                _detailsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budget", FormatMode = Syncfusion.WinForms.DataGrid.Enums.FormatMode.Currency, MinimumWidth = 120 });
                _detailsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "Amount", HeaderText = "Actual", FormatMode = Syncfusion.WinForms.DataGrid.Enums.FormatMode.Currency, MinimumWidth = 120 });
            }
            catch { }

            rightPanel.Controls.Add(_detailsGrid, 0, 0);

            // small summary area
            var summaryPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            // Replace the small summary labels with three tile panels that include a sparkline next to each
            var summaryTiles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = false };
            summaryTiles.WrapContents = false;

            // Helper to create a tile
            Func<string, string, decimal, ChartControl> createTile = (title, tooltip, value) =>
            {
                var tile = new Panel { Width = 260, Height = 72, Padding = new Padding(8), Margin = new Padding(6), BackColor = Color.Transparent };

                var lbl = new Label { Text = title, AutoSize = false, Height = 20, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
                var valLbl = new Label { Text = value.ToString("C0"), AutoSize = false, Height = 20, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 11, FontStyle.Bold) };

                var spark = new ChartControl { Width = 220, Height = 28, Dock = DockStyle.Bottom, AccessibleName = title + " sparkline" };
                spark.ShowLegend = false; spark.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality; spark.ElementsSpacing = 2; spark.PrimaryYAxis.HidePartialLabels = true; spark.PrimaryXAxis.HidePartialLabels = true; spark.ShowToolTips = false;

                tile.Controls.Add(spark);
                tile.Controls.Add(valLbl);
                tile.Controls.Add(lbl);

                // Capture references to the value labels for later updates
                if (title.StartsWith("Total Budget")) _tileBudgetValueLabel = valLbl;
                else if (title.StartsWith("Expenditure")) _tileExpenditureValueLabel = valLbl;
                else if (title.StartsWith("Remaining")) _tileRemainingValueLabel = valLbl;

                summaryTiles.Controls.Add(tile);

                return spark;
            };

            // Create three tiles (values will be updated in bindings)
            _sparkBudget = createTile("Total Budget", "Total budget", _vm.TotalBudget);
            _sparkExpenditure = createTile("Expenditure", "Total expenditure", _vm.TotalExpenditure);
            _sparkRemaining = createTile("Remaining", "Remaining budget", _vm.RemainingBudget);

            summaryPanel.Controls.Add(summaryTiles);

            rightPanel.Controls.Add(summaryPanel, 0, 1);

            mainSplit.Controls.Add(rightPanel, 1, 1);

            Controls.Add(mainSplit);

            // status bar
            _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            _statusLabel = new ToolStripStatusLabel { Text = "Ready" };
            _statusStrip.Items.Add(_statusLabel);
            Controls.Add(_statusStrip);

            // Add overlays for loading/no-data states
            _loadingOverlay = new LoadingOverlay { Message = "Loading dashboard..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No dashboard data available" };
            Controls.Add(_noDataOverlay);

            // Bindings
            TryApplyViewModelBindings();

            // Theme update handler
            _themeChangedHandler = (s, t) => ApplyCurrentTheme();
            ThemeManager.ThemeChanged += _themeChangedHandler;
        }

        private void TryApplyViewModelBindings()
        {
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
                    var ser = new ChartSeries("Metrics", ChartSeriesType.Column);
                    foreach (var m in metricsList)
                    {
                        ser.Points.Add(m.Name, m.Value);
                    }
                    _mainChart.Series.Add(ser);

                    // Fill the sparkline mini-charts attached to each summary tile and update their value labels
                    try
                    {
                        void FillSpark(ChartControl? c, double baseValue)
                        {
                            if (c == null) return;
                            c.Series.Clear();
                            var s = new ChartSeries("s", ChartSeriesType.Line);
                            var values = new double[] { baseValue * 0.8, baseValue * 0.95, baseValue * 1.02, baseValue * 0.9, baseValue * 1.1, baseValue };
                            for (int i = 0; i < values.Length; i++) s.Points.Add(i.ToString(), values[i]);
                            s.Style.DisplayText = false;
                            s.Style.Interior = new Syncfusion.Drawing.BrushInfo(ThemeManager.Colors.Accent);
                            c.Series.Add(s);
                        }

                        FillSpark(_sparkBudget, (double)_vm.TotalBudget);
                        FillSpark(_sparkExpenditure, (double)_vm.TotalExpenditure);
                        FillSpark(_sparkRemaining, (double)_vm.RemainingBudget);

                        if (_tileBudgetValueLabel != null) _tileBudgetValueLabel.Text = _vm.TotalBudget.ToString("C0");
                        if (_tileExpenditureValueLabel != null) _tileExpenditureValueLabel.Text = _vm.TotalExpenditure.ToString("C0");
                        if (_tileRemainingValueLabel != null) _tileRemainingValueLabel.Text = _vm.RemainingBudget.ToString("C0");
                    }
                    catch { }
                }

                // Wire status and labels
                if (Controls.Find("lblBudget", true).FirstOrDefault() is Label lblBudget)
                {
                    lblBudget.Text = $"Total Budget: {_vm.TotalBudget:C0}";
                }
                if (Controls.Find("lblExpenditure", true).FirstOrDefault() is Label lblExp)
                {
                    lblExp.Text = $"Expenditure: {_vm.TotalExpenditure:C0}";
                }
                if (Controls.Find("lblRemaining", true).FirstOrDefault() is Label lblRem)
                {
                    lblRem.Text = $"Remaining: {_vm.RemainingBudget:C0}";
                }

                // details grid mapping
                var prop = _vm.GetType().GetProperty("DepartmentSummaries") ?? _vm.GetType().GetProperty("Metrics");
                if (prop != null)
                {
                    var val = prop.GetValue(_vm);
                    if (val != null)
                    {
                        _detailsGrid.DataSource = val;
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

                if (e.PropertyName == nameof(_vm.IsLoading))
                {
                    try { if (_loadingOverlay != null) _loadingOverlay.Visible = _vm.IsLoading; } catch { }
                }

                if (e.PropertyName == nameof(_vm.Metrics) || e.PropertyName == nameof(_vm.TotalBudget) || e.PropertyName == nameof(_vm.TotalExpenditure) || e.PropertyName == nameof(_vm.RemainingBudget) || e.PropertyName == nameof(_vm.LastRefreshed))
                {
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(TryApplyViewModelBindings); } catch { }
                    }
                    else if (InvokeRequired)
                    {
                        try { BeginInvoke(new Action(TryApplyViewModelBindings)); } catch { }
                    }
                    else
                    {
                        TryApplyViewModelBindings();
                    }
                    try { if (_noDataOverlay != null) _noDataOverlay.Visible = (_vm.Metrics == null || !_vm.Metrics.Any()); } catch { }
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

        private void ApplyCurrentTheme()
        {
            try
            {
                ThemeManager.ApplyTheme(this);
                // Apply Syncfusion skin using global theme (FluentDark default, FluentLight fallback)
                try { Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(this, ThemeManager.GetSyncfusionThemeName()); } catch { }
            }
            catch { }
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

                // Use reflection to invoke the parent form's navigation method
                var method = parentForm.GetType().GetMethod("DockUserControlPanel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { ThemeManager.ThemeChanged -= _themeChangedHandler; } catch { }
                try { WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged -= _btnRefreshThemeChangedHandler; } catch { }
                try { if (_viewModelPropertyChangedHandler != null && _vm is INotifyPropertyChanged npc) npc.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }

                try { _kpiList.SafeClearDataSource(); } catch { }
                try { _kpiList.SafeDispose(); } catch { }
                try { _detailsGrid.SafeClearDataSource(); } catch { }
                try { _detailsGrid.SafeDispose(); } catch { }

                try { _kpiList?.Dispose(); } catch { }
                try { _mainChart?.Dispose(); } catch { }
                try { _detailsGrid?.Dispose(); } catch { }
                try { _statusStrip?.Dispose(); } catch { }
                try { _toolStrip?.Dispose(); } catch { }
                try { _topPanel?.Dispose(); } catch { }
                // Dispose any additional UI elements and clear handlers
                try
                {
                    // Attempt to clear PanelHeader events (these were attached with anonymous handlers)
                    if (_panelHeader != null)
                    {
                        try
                        {
                            var t = _panelHeader.GetType();
                            // common backing-field patterns
                            var candidateNames = new[] { "RefreshClicked", "CloseClicked", "PinToggled", "<RefreshClicked>k__BackingField", "<CloseClicked>k__BackingField", "<PinToggled>k__BackingField" };
                            foreach (var name in candidateNames)
                            {
                                try
                                {
                                    var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                    if (f != null)
                                    {
                                        // Clear the delegate backing field
                                        f.SetValue(_panelHeader, null);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }

                        try { _panelHeader.Dispose(); } catch { }
                    }

                    // Loading / no-data overlays
                    try
                    {
                        if (_loadingOverlay != null)
                        {
                            // try to clear any event fields that may hold anonymous delegates
                            var tlo = _loadingOverlay.GetType();
                            var candidateNames = new[] { "Resize", "<Resize>k__BackingField" };
                            foreach (var name in candidateNames)
                            {
                                try
                                {
                                    var f = tlo.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                    if (f != null)
                                    {
                                        f.SetValue(_loadingOverlay, null);
                                    }
                                }
                                catch { }
                            }
                            try { _loadingOverlay.Dispose(); } catch { }
                        }
                    }
                    catch { }

                    try
                    {
                        if (_noDataOverlay != null)
                        {
                            try { _noDataOverlay.Dispose(); } catch { }
                        }
                    }
                    catch { }

                    // Dispose per-tile sparks
                    try { _sparkBudget?.Dispose(); } catch { }
                    try { _sparkExpenditure?.Dispose(); } catch { }
                    try { _sparkRemaining?.Dispose(); } catch { }
                    try { _errorProvider?.Dispose(); } catch { }
                }
                catch { }
            }
            base.Dispose(disposing);
        }
    }
}
