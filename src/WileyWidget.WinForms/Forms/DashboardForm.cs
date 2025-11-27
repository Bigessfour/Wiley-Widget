using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;

namespace WileyWidget.WinForms.Forms
{
    internal static class DashboardResources
    {
        public const string FormTitle = "Dashboard - Wiley Widget";
        public const string LoadButton = "Load Dashboard";
        public const string RefreshButton = "Refresh";
        public const string ExportButton = "Export";
        public const string LoadingText = "Loading dashboard...";
        public const string MunicipalityLabel = "Municipality:";
        public const string FiscalYearLabel = "Fiscal Year:";
        public const string LastUpdatedLabel = "Last Updated:";
        public const string ErrorTitle = "Error";
        public const string LoadErrorMessage = "Error loading dashboard: {0}";
        public const string MetricsGridTitle = "Key Performance Metrics";
        public const string RevenueTrendTitle = "Revenue Trend";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class DashboardForm : Form
    {
        private readonly DashboardViewModel _viewModel;
        private TableLayoutPanel? _mainLayout;
        private SfDataGrid? _metricsGrid;
        private ChartControl? _revenueChart;
        private RadialGauge? _budgetGauge;
        private RadialGauge? _revenueGauge;
        private RadialGauge? _expensesGauge;
        private RadialGauge? _netPositionGauge;
        private Label? _municipalityLabel;
        private Label? _fiscalYearLabel;
        private Label? _lastUpdatedLabel;
        private Label? _loadingLabel;
        private Label? _errorLabel;
        private System.Windows.Forms.Timer? _refreshTimer;
        private CheckBox? _autoRefreshCheckbox;

        public DashboardForm(DashboardViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            SetupUI();
            BindViewModel();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            LoadDashboard();
#pragma warning restore CS4014
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Forms.Form.set_Text")]
        private void InitializeComponent()
        {
            Text = DashboardResources.FormTitle;
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void SetupUI()
        {
            // Create main layout
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };

            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Toolbar
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // Header info
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); // KPI Gauges
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // Chart
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // Metrics Grid
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Status

            // Toolbar
            var toolStrip = new ToolStrip();
            var loadButton = new ToolStripButton(DashboardResources.LoadButton, null, async (s, e) => await LoadDashboard()) { Name = "Toolbar_LoadButton", AccessibleName = "Load Dashboard" }; 
            var refreshButton = new ToolStripButton(DashboardResources.RefreshButton, null, async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null)) { Name = "Toolbar_RefreshButton", AccessibleName = "Refresh" }; 
            var exportButton = new ToolStripButton(DashboardResources.ExportButton, null, async (s, e) => await ExportDashboard()) { Name = "Toolbar_ExportButton", AccessibleName = "Export" }; 

            // Auto-refresh checkbox
            _autoRefreshCheckbox = new CheckBox { Name = "AutoRefreshCheckbox", Text = "Auto-refresh (30s)", Checked = true, Padding = new Padding(5, 0, 5, 0) };
            _autoRefreshCheckbox.CheckedChanged += (s, e) => ToggleAutoRefresh(_autoRefreshCheckbox.Checked);
            var autoRefreshHost = new ToolStripControlHost(_autoRefreshCheckbox);

            toolStrip.Items.AddRange(new ToolStripItem[] { loadButton, refreshButton, new ToolStripSeparator(), exportButton, new ToolStripSeparator(), autoRefreshHost });
            _mainLayout.Controls.Add(toolStrip, 0, 0);

            // Initialize auto-refresh timer
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 30000 }; // 30 seconds
            _refreshTimer.Tick += async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null);
            _refreshTimer.Start();

            // Header panel
            var headerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            _municipalityLabel = new Label { Name = "MunicipalityLabel", Text = $"{DashboardResources.MunicipalityLabel} Loading...", AutoSize = true, Margin = new Padding(10, 5, 20, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _fiscalYearLabel = new Label { Name = "FiscalYearLabel", Text = $"{DashboardResources.FiscalYearLabel} Loading...", AutoSize = true, Margin = new Padding(0, 5, 20, 5), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _lastUpdatedLabel = new Label { Name = "LastUpdatedLabel", Text = $"{DashboardResources.LastUpdatedLabel} Loading...", AutoSize = true, Margin = new Padding(0, 5, 0, 5), Font = new Font("Segoe UI", 9) }; 

            headerPanel.Controls.AddRange(new Control[] { _municipalityLabel, _fiscalYearLabel, _lastUpdatedLabel });
            _mainLayout.Controls.Add(headerPanel, 0, 1);

            // KPI Gauges Panel
            var gaugePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(10)
            };

            _budgetGauge = CreateGauge("Total Budget", Color.FromArgb(0, 120, 215));
            _revenueGauge = CreateGauge("Revenue", Color.FromArgb(16, 137, 62));
            _expensesGauge = CreateGauge("Expenses", Color.FromArgb(232, 17, 35));
            _netPositionGauge = CreateGauge("Net Position", Color.FromArgb(255, 185, 0));

            gaugePanel.Controls.AddRange(new Control[] { _budgetGauge, _revenueGauge, _expensesGauge, _netPositionGauge });
            _mainLayout.Controls.Add(gaugePanel, 0, 2);

            // Revenue Trend Chart
            _revenueChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                Text = DashboardResources.RevenueTrendTitle
            };
            _revenueChart.PrimaryXAxis.Title = "Month";
            _revenueChart.PrimaryYAxis.Title = "Amount ($)";
            _revenueChart.Series.Add(new ChartSeries("Revenue", ChartSeriesType.Line));

            _mainLayout.Controls.Add(_revenueChart, 0, 3);

            // Metrics Grid using Syncfusion SfDataGrid
            _metricsGrid = new SfDataGrid
            {
                Name = "MetricsGrid",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                AllowResizingColumns = true,
                AllowResizingHiddenColumns = true,
                SelectionMode = GridSelectionMode.Single,
                NavigationMode = NavigationMode.Row
            };

            // Configure columns
            _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = "Metric", Width = 200 });
            _metricsGrid.Columns.Add(new GridNumericColumn { MappingName = "Value", HeaderText = "Value", Format = "N2", Width = 120 });
            _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "Unit", HeaderText = "Unit", Width = 80 });
            _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "Trend", HeaderText = "Trend", Width = 80 });
            _metricsGrid.Columns.Add(new GridNumericColumn { MappingName = "ChangePercent", HeaderText = "Change %", Format = "N1", Width = 100 });
            _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "Description", HeaderText = "Description", Width = 300 });

            // Apply modern styling
            _metricsGrid.Style.HeaderStyle.BackColor = Color.FromArgb(0, 120, 215);
            _metricsGrid.Style.HeaderStyle.TextColor = Color.White;
            _metricsGrid.Style.HeaderStyle.Font.Facename = "Segoe UI";
            _metricsGrid.Style.HeaderStyle.Font.Size = 10;
            _metricsGrid.Style.HeaderStyle.Font.Bold = true;

            var metricsPanel = new Panel { Dock = DockStyle.Fill };
            var metricsLabel = new Label { Text = DashboardResources.MetricsGridTitle, Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 11, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            metricsPanel.Controls.Add(_metricsGrid);
            metricsPanel.Controls.Add(metricsLabel);

            _mainLayout.Controls.Add(metricsPanel, 0, 4);

            // Status panel
            var statusPanel = new Panel { Dock = DockStyle.Fill };
            _loadingLabel = new Label { Text = "", Visible = false, ForeColor = Color.Blue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _errorLabel = new Label { Text = "", Visible = false, ForeColor = Color.Red, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

            statusPanel.Controls.AddRange(new Control[] { _loadingLabel, _errorLabel });
            _mainLayout.Controls.Add(statusPanel, 0, 5);

            Controls.Add(_mainLayout);
        }

        private RadialGauge CreateGauge(string label, Color needleColor)
        {
            var gauge = new RadialGauge
            {
                Width = 180,
                Height = 180,
                Margin = new Padding(5),
                VisualStyle = ThemeStyle.Office2016Colorful,
                MinimumValue = 0F,
                MaximumValue = 100F,
                MajorDifference = 20F,
                MinorDifference = 5F,
                MinorTickMarkHeight = 5,
                MajorTickMarkHeight = 10,
                NeedleStyle = NeedleStyle.Advanced,
                ShowScaleLabel = true,
                LabelPlacement = LabelPlacement.Inside,
                NeedleColor = needleColor,
                Value = 0F,
                GaugeLabel = label,
                ShowNeedle = true
            };

            return gauge;
        }

        private void BindViewModel()
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.MunicipalityName):
                        if (_municipalityLabel != null)
                            _municipalityLabel.Text = $"{DashboardResources.MunicipalityLabel} {_viewModel.MunicipalityName}";
                        break;
                    case nameof(_viewModel.FiscalYear):
                        if (_fiscalYearLabel != null)
                            _fiscalYearLabel.Text = $"{DashboardResources.FiscalYearLabel} {_viewModel.FiscalYear}";
                        break;
                    case nameof(_viewModel.LastUpdated):
                        if (_lastUpdatedLabel != null)
                            _lastUpdatedLabel.Text = $"{DashboardResources.LastUpdatedLabel} {_viewModel.LastUpdated:g}";
                        break;
                    case nameof(_viewModel.IsLoading):
                        if (_loadingLabel != null)
                        {
                            _loadingLabel.Text = _viewModel.IsLoading ? DashboardResources.LoadingText : "";
                            _loadingLabel.Visible = _viewModel.IsLoading;
                        }
                        break;
                    case nameof(_viewModel.ErrorMessage):
                        if (_error_label != null)
                        {
                            _error_label.Text = _viewModel.ErrorMessage ?? "";
                            _error_label.Visible = !string.IsNullOrEmpty(_viewModel.ErrorMessage);
                        }
                        break;
                    case nameof(_view_model.Metrics):
                        if (_metrics_grid != null)
                            _metrics_grid.DataSource = _view_model.Metrics;
                        break;
                    case nameof(_viewModel.TotalBudgetGauge):
                        UpdateGaugeValueSafely(_budgetGauge, _viewModel.TotalBudgetGauge);
                        break;
                    case nameof(_viewModel.RevenueGauge):
                        UpdateGaugeValueSafely(_revenueGauge, _viewModel.RevenueGauge);
                        break;
                    case nameof(_viewModel.ExpensesGauge):
                        UpdateGaugeValueSafely(_expensesGauge, _viewModel.ExpensesGauge);
                        break;
                    case nameof(_viewModel.NetPositionGauge):
                        UpdateGaugeValueSafely(_netPositionGauge, _viewModel.NetPositionGauge);
                        break;
                }
            };
        }
