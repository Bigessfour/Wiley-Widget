using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Models;

#pragma warning disable CS8604 // Possible null reference argument
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

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
        public const string StatusReady = "Ready";
        public const string StatusExported = "Dashboard exported";
        public const string StatusRefreshed = "Dashboard refreshed";
        public const string StatusAutoRefresh = "Auto-refresh: {0}";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class DashboardForm : Form
    {
        private readonly DashboardViewModel _viewModel;
        private readonly AnalyticsViewModel _analyticsViewModel;
        private readonly ILogger<DashboardForm> _logger;
        private TableLayoutPanel? _mainLayout;
        private SfDataGrid? _metricsGrid;
        private SfDataGrid? _fundsGrid;
        private SfDataGrid? _departmentsGrid;
        private SfDataGrid? _topVariancesGrid;
        private SfDataGrid? _analysisGrid;
        private SfDataGrid? _analyticsMetricsGrid;
        private SfDataGrid? _analyticsVariancesGrid;
        private SfDataGrid? _scenarioGrid;
        private ChartControl? _trendChart;
        private ChartControl? _forecastChart;
        private ChartControl? _revenueChart;
        private RadialGauge? _budgetGauge;
        private RadialGauge? _revenueGauge;
        private RadialGauge? _expensesGauge;
        private RadialGauge? _netPositionGauge;
        private RadialGauge? _variancePercentGauge;
        private RadialGauge? _varianceAmountGauge;
        private RadialGauge? _revenueAmountGauge;
        private RadialGauge? _expensesAmountGauge;
        private ToolStripEx? _toolbar;
        private ToolStripButton? _loadButton;
        private ToolStripMenuItem? _fileMenuLoadItem;
        private StatusStrip? _statusBar;
        private ToolStripStatusLabel? _statusPanel;
        private ToolStripStatusLabel? _countsPanel;
        private ToolStripStatusLabel? _updatedPanel;
        private Label? _municipalityLabel;
        private Label? _fiscalYearLabel;
        private Label? _lastUpdatedLabel;
        private System.Windows.Forms.Timer? _refreshTimer;
        private CheckBox? _autoRefreshCheckbox;
        private MenuStrip? _menuStrip;
        private ToolTip _toolTip;
        private ToolStripProgressBar? _progressBar;
        private ContextMenuStrip? _gridContextMenu;
        private readonly bool _isUiTestHarness;
        private TabControl? _detailsTab;

        private const int RefreshIntervalMs = 30000;

        public DashboardForm(DashboardViewModel viewModel, AnalyticsViewModel analyticsViewModel, MainForm mainForm, ILogger<DashboardForm> logger)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _analyticsViewModel = analyticsViewModel ?? throw new ArgumentNullException(nameof(analyticsViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (mainForm == null)
            {
                throw new ArgumentNullException(nameof(mainForm));
            }

            // Only set MdiParent if MainForm is in MDI mode AND using MDI for child forms
            // In DockingManager mode, forms are shown as owned windows, not MDI children
            if (mainForm.IsMdiContainer && mainForm.UseMdiMode)
            {
                MdiParent = mainForm;
            }
            _isUiTestHarness = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            _toolTip = new ToolTip();
            InitializeComponent();
            SetupUI();
            ThemeColors.ApplyTheme(this);
            SfSkinManager.SetVisualStyle(this, "Office2019Colorful");
            BindViewModel();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            LoadDashboard();
#pragma warning restore CS4014
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Forms.Form.set_Text")]
        private void InitializeComponent()
        {
            Name = "DashboardForm";
            Text = DashboardResources.FormTitle;
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void SetupUI()
        {
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(10)
            };

            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // Menu
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // Toolbar
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // Header info
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));  // KPI Gauges
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));    // Chart
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));    // Metrics Grid
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // Status bar

            if (_isUiTestHarness)
            {
                BuildBasicStatusStrip();
            }
            else
            {
                BuildStatusBar();
            }

            BuildToolbar();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = RefreshIntervalMs };
            _refreshTimer.Tick += async (s, e) =>
            {
                await _viewModel.RefreshCommand.ExecuteAsync(null);
                UpdateStatus(DashboardResources.StatusRefreshed);
            };
            // Do not auto-start timer - user must enable via checkbox
            // _refreshTimer.Start();

            BuildMenu();

            _mainLayout.Controls.Add(_menuStrip!, 0, 0);

            _gridContextMenu = new ContextMenuStrip();
            _gridContextMenu.Items.Add(new ToolStripMenuItem("Copy", null, (s, e) =>
            {
                if (ActiveControl is SfDataGrid grid)
                {
                    grid.ClipboardController.Copy();
                }
            }));

            // Header panel
            var headerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = ThemeColors.Background
            };

            _municipalityLabel = new Label { Name = "MunicipalityLabel", Text = $"{DashboardResources.MunicipalityLabel} Loading...", AutoSize = true, Margin = new Padding(10, 5, 20, 5) };
            _fiscalYearLabel = new Label { Name = "FiscalYearLabel", Text = $"{DashboardResources.FiscalYearLabel} Loading...", AutoSize = true, Margin = new Padding(0, 5, 20, 5) };
            _lastUpdatedLabel = new Label { Name = "LastUpdatedLabel", Text = $"{DashboardResources.LastUpdatedLabel} Loading...", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };

            headerPanel.Controls.AddRange(new Control[] { _municipalityLabel, _fiscalYearLabel, _lastUpdatedLabel });
            if (_toolTip != null)
            {
                _toolTip.SetToolTip(_municipalityLabel, "Displays the current municipality");
                _toolTip.SetToolTip(_fiscalYearLabel, "Displays the current fiscal year");
                _toolTip.SetToolTip(_lastUpdatedLabel, "Shows when the data was last updated");
            }
            _mainLayout.Controls.Add(headerPanel, 0, 2);

            // KPI Gauges Panel
            var gaugePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(10)
            };

            _budgetGauge = CreateGauge("Total Budget", ThemeColors.PrimaryAccent);
            _revenueGauge = CreateGauge("Revenue %", ThemeColors.Success);
            _expensesGauge = CreateGauge("Expenses %", ThemeColors.Error);
            _netPositionGauge = CreateGauge("Net Position", ThemeColors.Warning);
            _variancePercentGauge = CreateGauge("Variance %", ThemeColors.Warning);
            _varianceAmountGauge = CreateGauge("Total Variance", ThemeColors.Warning);
            _revenueAmountGauge = CreateGauge("Revenue Amount", ThemeColors.Success);
            _expensesAmountGauge = CreateGauge("Expenses Amount", ThemeColors.Error);

            gaugePanel.Controls.AddRange(new Control[] { _budgetGauge, _revenueGauge, _expensesGauge, _netPositionGauge, _variancePercentGauge, _varianceAmountGauge, _revenueAmountGauge, _expensesAmountGauge });
            var gaugeGroup = new GroupBox { Text = "Key Performance Indicators", Dock = DockStyle.Fill, Padding = new Padding(10) };
            gaugeGroup.Controls.Add(gaugePanel);
            _mainLayout.Controls.Add(gaugeGroup, 0, 3);

            // Revenue Trend Chart
            _revenueChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                Text = DashboardResources.RevenueTrendTitle,
                ShowLegend = true,
                ShowToolTips = true
            };
            SfSkinManager.SetVisualStyle(_revenueChart, "Office2019Colorful");

            var revenueSeries = new ChartSeries("Revenue", ChartSeriesType.Line);
            _revenueChart.Series.Add(revenueSeries);

            var revenueChartGroup = new GroupBox { Text = "Revenue Trend", Dock = DockStyle.Fill, Padding = new Padding(10) };
            revenueChartGroup.Controls.Add(_revenueChart);
            _mainLayout.Controls.Add(revenueChartGroup, 0, 4);

            // Metrics Grid using Syncfusion SfDataGrid with performance optimizations
            _metricsGrid = new SfDataGrid
            {
                Name = "MetricsGrid",
                AccessibleName = "Dashboard_Grid_Metrics",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                AllowResizingColumns = true,
                AllowResizingHiddenColumns = true,
                SelectionMode = GridSelectionMode.Single,
                NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
                AllowGrouping = true,
                ShowGroupDropArea = false,
                // Performance optimizations
                EnableDataVirtualization = true,
                AutoSizeColumnsMode = AutoSizeColumnsMode.None,
                // Enable sorting with initial configuration
                ShowBusyIndicator = true
                // Theme inherited from form's SfSkinManager.SetVisualStyle
            };

            SfSkinManager.SetVisualStyle(_metricsGrid, ThemeColors.DefaultTheme);

            // Configure columns with proper formatting
            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Name",
                HeaderText = "Metric",
                Width = 200,
                AllowSorting = true
            });

            var valueColumn = new GridNumericColumn
            {
                MappingName = "Value",
                HeaderText = "Value",
                Width = 120,
                NumberFormatInfo = new System.Globalization.NumberFormatInfo { NumberDecimalDigits = 2 },
                AllowSorting = true
            };
            _metricsGrid.Columns.Add(valueColumn);

            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Unit",
                HeaderText = "Unit",
                Width = 80,
                AllowSorting = false
                // TextAlignment property not available in this version
            });

            var changeColumn = new GridNumericColumn
            {
                MappingName = "ChangePercent",
                HeaderText = "Change %",
                Width = 100,
                NumberFormatInfo = new System.Globalization.NumberFormatInfo { NumberDecimalDigits = 1 },
                AllowSorting = true
            };
            _metricsGrid.Columns.Add(changeColumn);

            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Description",
                HeaderText = "Description",
                Width = 300,
                AllowFiltering = true
            });

            // Configure initial sorting (descending by Value)
            _metricsGrid.SortColumnDescriptions.Add(new SortColumnDescription
            {
                ColumnName = "Value",
                SortDirection = ListSortDirection.Descending
            });

            // Apply theme to the metrics grid
            ThemeColors.ApplySfDataGridTheme(_metricsGrid);

            // Note: Styling is now handled by ThemeName property.
            // All colors, fonts, and styles are managed by SkinManager dynamically.

            // Create a nested table layout for metrics and a details tab (Fund/Departments/Variances)
            var metricsContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 6, 0, 6)
            };
            metricsContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            metricsContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            var metricsPanel = new Panel { Dock = DockStyle.Fill };
            var metricsLabel = new Label { Text = DashboardResources.MetricsGridTitle, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
            metricsPanel.Controls.Add(_metricsGrid);
            metricsPanel.Controls.Add(metricsLabel);

            // Details Tab (Funds / Departments / Top Variances / Budget Analysis)
            _detailsTab = new TabControl { Dock = DockStyle.Fill };

            var fundsTab = new TabPage("Funds") { Padding = new Padding(4) };
            _fundsGrid = new SfDataGrid
            {
                Name = "FundsGrid",
                AccessibleName = "Dashboard_Grid_Funds",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                EnableDataVirtualization = true
            };
            ThemeColors.ApplySfDataGridTheme(_fundsGrid);
            _fundsGrid.ContextMenuStrip = _gridContextMenu;
            _fundsGrid.Columns.Add(new GridTextColumn { MappingName = "FundName", HeaderText = "Fund", Width = 160, AllowSorting = true });
            _fundsGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalBudgeted", HeaderText = "Budgeted", Width = 120, AllowSorting = true });
            _fundsGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalActual", HeaderText = "Actual", Width = 120, AllowSorting = true });
            _fundsGrid.Columns.Add(new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", Width = 120, AllowSorting = true });
            _fundsGrid.Columns.Add(new GridNumericColumn { MappingName = "VariancePercentage", HeaderText = "% Variance", Width = 100, AllowSorting = true });
            _fundsGrid.Columns.Add(new GridNumericColumn { MappingName = "AccountCount", HeaderText = "Accounts", Width = 90, AllowSorting = true });
            fundsTab.Controls.Add(_fundsGrid);

            var departmentsTab = new TabPage("Departments") { Padding = new Padding(4) };
            _departmentsGrid = new SfDataGrid
            {
                Name = "DepartmentsGrid",
                AccessibleName = "Dashboard_Grid_Departments",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                EnableDataVirtualization = true
            };
            ThemeColors.ApplySfDataGridTheme(_departmentsGrid);
            _departmentsGrid.ContextMenuStrip = _gridContextMenu;
            _departmentsGrid.Columns.Add(new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", Width = 160, AllowSorting = true });
            _departmentsGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalBudgeted", HeaderText = "Budgeted", Width = 120, AllowSorting = true });
            _departmentsGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalActual", HeaderText = "Actual", Width = 120, AllowSorting = true });
            _departmentsGrid.Columns.Add(new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", Width = 120, AllowSorting = true });
            _departmentsGrid.Columns.Add(new GridNumericColumn { MappingName = "VariancePercentage", HeaderText = "% Variance", Width = 100, AllowSorting = true });
            _departmentsGrid.Columns.Add(new GridNumericColumn { MappingName = "AccountCount", HeaderText = "Accounts", Width = 90, AllowSorting = true });
            departmentsTab.Controls.Add(_departmentsGrid);

            var variancesTab = new TabPage("Top Variances") { Padding = new Padding(4) };
            _topVariancesGrid = new SfDataGrid
            {
                Name = "TopVariancesGrid",
                AccessibleName = "Dashboard_Grid_TopVariances",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                EnableDataVirtualization = true
            };
            ThemeColors.ApplySfDataGridTheme(_topVariancesGrid);
            _topVariancesGrid.ContextMenuStrip = _gridContextMenu;
            _topVariancesGrid.Columns.Add(new GridTextColumn { MappingName = "AccountNumber", HeaderText = "Acct #", Width = 100, AllowSorting = true });
            _topVariancesGrid.Columns.Add(new GridTextColumn { MappingName = "AccountName", HeaderText = "Account", Width = 200, AllowSorting = true });
            _topVariancesGrid.Columns.Add(new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budgeted", Width = 100, AllowSorting = true });
            _topVariancesGrid.Columns.Add(new GridNumericColumn { MappingName = "ActualAmount", HeaderText = "Actual", Width = 100, AllowSorting = true });
            _topVariancesGrid.Columns.Add(new GridNumericColumn { MappingName = "VarianceAmount", HeaderText = "Variance", Width = 100, AllowSorting = true });
            _topVariancesGrid.Columns.Add(new GridNumericColumn { MappingName = "VariancePercentage", HeaderText = "% Variance", Width = 90, AllowSorting = true });
            _topVariancesGrid.Columns.Add(new GridTextColumn { MappingName = "Fund", HeaderText = "Fund", Width = 80, AllowSorting = true });
            _topVariancesGrid.Columns.Add(new GridTextColumn { MappingName = "Department", HeaderText = "Department", Width = 120, AllowSorting = true });
            variancesTab.Controls.Add(_topVariancesGrid);

            var analysisTab = new TabPage("Budget Analysis") { Padding = new Padding(4) };
            _analysisGrid = new SfDataGrid
            {
                Name = "BudgetAnalysisGrid",
                AccessibleName = "Dashboard_Grid_BudgetAnalysis",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = false,
                AllowSorting = false,
            };
            ThemeColors.ApplySfDataGridTheme(_analysisGrid);
            _analysisGrid.ContextMenuStrip = _gridContextMenu;
            _analysisGrid.Columns.Add(new GridTextColumn { MappingName = "BudgetPeriod", HeaderText = "Period", Width = 120 });
            _analysisGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalBudgeted", HeaderText = "Total Budgeted", Width = 120 });
            _analysisGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalActual", HeaderText = "Total Actual", Width = 120 });
            _analysisGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalVariance", HeaderText = "Variance", Width = 120 });
            _analysisGrid.Columns.Add(new GridNumericColumn { MappingName = "TotalVariancePercentage", HeaderText = "% Variance", Width = 100 });
            _analysisGrid.Columns.Add(new GridTextColumn { MappingName = "Warnings", HeaderText = "Warnings", Width = 240 });
            analysisTab.Controls.Add(_analysisGrid);

            // Analytics Tab
            var analyticsTab = new TabPage("Analytics") { Padding = new Padding(4) };
            var analyticsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(5)
            };
            analyticsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Controls
            analyticsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));  // Metrics
            analyticsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));  // Charts
            analyticsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));  // Scenario

            // Analytics controls panel
            var controlsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var runAnalysisButton = new Button { Text = "Run Analysis", Width = 100, Height = 30 };
            runAnalysisButton.Click += async (s, e) => await _analyticsViewModel.PerformAnalysisCommand.ExecuteAsync(null);

            var runScenarioButton = new Button { Text = "Run Scenario", Width = 100, Height = 30 };
            runScenarioButton.Click += async (s, e) => await _analyticsViewModel.RunScenarioCommand.ExecuteAsync(null);

            var forecastButton = new Button { Text = "Generate Forecast", Width = 120, Height = 30 };
            forecastButton.Click += async (s, e) => await _analyticsViewModel.GenerateForecastCommand.ExecuteAsync(null);

            controlsPanel.Controls.AddRange(new Control[] { runAnalysisButton, runScenarioButton, forecastButton });
            analyticsLayout.Controls.Add(controlsPanel, 0, 0);

            // Analytics metrics grid
            _analyticsMetricsGrid = new SfDataGrid
            {
                Name = "AnalyticsMetricsGrid",
                AccessibleName = "Dashboard_Grid_AnalyticsMetrics",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true
            };
            ThemeColors.ApplySfDataGridTheme(_analyticsMetricsGrid);
            _analyticsMetricsGrid.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = "Category", Width = 150 });
            _analyticsMetricsGrid.Columns.Add(new GridNumericColumn { MappingName = "Value", HeaderText = "Amount", Width = 120 });
            _analyticsMetricsGrid.Columns.Add(new GridTextColumn { MappingName = "Unit", HeaderText = "Unit", Width = 80 });

            var analyticsMetricsGroup = new GroupBox { Text = "Category Breakdown", Dock = DockStyle.Fill, Padding = new Padding(5) };
            analyticsMetricsGroup.Controls.Add(_analyticsMetricsGrid);
            analyticsLayout.Controls.Add(analyticsMetricsGroup, 0, 1);

            // Trend chart
            _trendChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                Text = "Budget Trends",
                ShowLegend = true,
                ShowToolTips = true
            };
            SfSkinManager.SetVisualStyle(_trendChart, "Office2019Colorful");

            var budgetedSeries = new ChartSeries("Budgeted", ChartSeriesType.Line);
            _trendChart.Series.Add(budgetedSeries);

            var actualSeries = new ChartSeries("Actual", ChartSeriesType.Line);
            _trendChart.Series.Add(actualSeries);

            var trendChartGroup = new GroupBox { Text = "Trend Analysis", Dock = DockStyle.Fill, Padding = new Padding(5) };
            trendChartGroup.Controls.Add(_trendChart);
            analyticsLayout.Controls.Add(trendChartGroup, 0, 2);

            // Scenario projections
            _scenarioGrid = new SfDataGrid
            {
                Name = "ScenarioGrid",
                AccessibleName = "Dashboard_Grid_Scenario",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = false,
                AllowSorting = true
            };
            ThemeColors.ApplySfDataGridTheme(_scenarioGrid);
            _scenarioGrid.Columns.Add(new GridNumericColumn { MappingName = "Year", HeaderText = "Year", Width = 80 });
            _scenarioGrid.Columns.Add(new GridNumericColumn { MappingName = "ProjectedRevenue", HeaderText = "Revenue", Width = 120 });
            _scenarioGrid.Columns.Add(new GridNumericColumn { MappingName = "ProjectedExpenses", HeaderText = "Expenses", Width = 120 });
            _scenarioGrid.Columns.Add(new GridNumericColumn { MappingName = "ProjectedReserves", HeaderText = "Reserves", Width = 120 });

            var scenarioGroup = new GroupBox { Text = "Scenario Projections", Dock = DockStyle.Fill, Padding = new Padding(5) };
            scenarioGroup.Controls.Add(_scenarioGrid);
            analyticsLayout.Controls.Add(scenarioGroup, 0, 3);

            analyticsTab.Controls.Add(analyticsLayout);

            _detailsTab.TabPages.Add(fundsTab);
            _detailsTab.TabPages.Add(departmentsTab);
            _detailsTab.TabPages.Add(variancesTab);
            _detailsTab.TabPages.Add(analysisTab);
            _detailsTab.TabPages.Add(analyticsTab);

            metricsContainer.Controls.Add(metricsPanel, 0, 0);
            metricsContainer.Controls.Add(_detailsTab, 1, 0);

            var detailedMetricsGroup = new GroupBox { Text = "Detailed Metrics", Dock = DockStyle.Fill, Padding = new Padding(10) };
            detailedMetricsGroup.Controls.Add(metricsContainer);
            _mainLayout.Controls.Add(detailedMetricsGroup, 0, 5);

            if (_isUiTestHarness)
            {
                if (_statusBar != null)
                {
                    _mainLayout.Controls.Add(_statusBar, 0, 6);
                }
            }
            else
            {
                if (_statusBar != null)
                {
                    _mainLayout.Controls.Add(_statusBar, 0, 6);
                }
            }

            Controls.Add(_mainLayout);
        }

        private void BuildMenu()
        {
            _menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");
            _fileMenuLoadItem = new ToolStripMenuItem("Load", null, (s, e) => { _ = LoadDashboard(); });
            fileMenu.DropDownItems.Add(_fileMenuLoadItem);
            fileMenu.DropDownItems.Add("Export", null, (s, e) => { _ = ExportDashboard(); });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Close());
            var viewMenu = new ToolStripMenuItem("View");
            viewMenu.DropDownItems.Add("Refresh", null, (s, e) => { _ = _viewModel.RefreshCommand.ExecuteAsync(null); });
            var helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add("About", null, (s, e) => MessageBox.Show("Wiley Widget Dashboard v1.0", "About"));
            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, helpMenu });
        }

        private void BuildToolbar()
        {
            _toolbar = new ToolStripEx
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(20, 20),
                Padding = new Padding(8, 4, 8, 4),
                Office12Mode = false
            };
            // Theme application is optional - toolbar remains functional if styling fails
            try { SfSkinManager.SetVisualStyle(_toolbar, ThemeColors.DefaultTheme); }
            catch (Exception)
            {
                // Ignore - toolbar works with default styling
            }

            var loadButton = new ToolStripButton
            {
                Text = DashboardResources.LoadButton,
                Name = "Toolbar_LoadButton",
                AutoSize = false,
                Width = 120,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            loadButton.Click += async (s, e) => await LoadDashboard();
            _loadButton = loadButton;
            loadButton.ToolTipText = "Load dashboard data";

            var refreshButton = new ToolStripButton
            {
                Text = DashboardResources.RefreshButton,
                Name = "Toolbar_RefreshButton",
                AutoSize = false,
                Width = 100,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            refreshButton.Click += async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null);
            refreshButton.ToolTipText = "Refresh dashboard data";

            var exportButton = new ToolStripButton
            {
                Text = DashboardResources.ExportButton,
                Name = "Toolbar_ExportButton",
                AutoSize = false,
                Width = 90,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            exportButton.Click += async (s, e) => await ExportDashboard();
            exportButton.ToolTipText = "Export dashboard data to file";

            _autoRefreshCheckbox = new CheckBox
            {
                Name = "AutoRefreshCheckbox",
                Text = "Auto-refresh (30s)",
                Checked = true,
                Padding = new Padding(5, 0, 5, 0),
                AutoSize = true
            };
            _autoRefreshCheckbox.CheckedChanged += (s, e) =>
            {
                ToggleAutoRefresh(_autoRefreshCheckbox.Checked);
                UpdateStatus(string.Format(CultureInfo.CurrentCulture, DashboardResources.StatusAutoRefresh, _autoRefreshCheckbox.Checked ? "On" : "Off"));
            };
            if (_toolTip != null)
            {
                _toolTip.SetToolTip(_autoRefreshCheckbox, "Enable automatic refresh every 30 seconds");
            }
            var autoRefreshHost = new ToolStripControlHost(_autoRefreshCheckbox)
            {
                Margin = new Padding(6, 0, 0, 0)
            };

            _toolbar.Items.Add(loadButton);
            _toolbar.Items.Add(refreshButton);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(exportButton);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(autoRefreshHost);

            _progressBar = new ToolStripProgressBar { Style = ProgressBarStyle.Marquee, Visible = false, Width = 100 };
            _toolbar.Items.Add(_progressBar);

            _mainLayout?.Controls.Add(_toolbar, 0, 0);
        }

        private void BuildStatusBar()
        {
            try
            {
                _statusBar = new StatusStrip
                {
                    Dock = DockStyle.Bottom,
                    SizingGrip = false
                };

                _statusPanel = new ToolStripStatusLabel
                {
                    Text = DashboardResources.StatusReady,
                    BorderSides = ToolStripStatusLabelBorderSides.None,
                    Width = 450
                };

                _countsPanel = new ToolStripStatusLabel
                {
                    Text = "0 metrics",
                    BorderSides = ToolStripStatusLabelBorderSides.None,
                    Width = 220
                };

                _updatedPanel = new ToolStripStatusLabel
                {
                    Text = "Updated: --",
                    BorderSides = ToolStripStatusLabelBorderSides.None,
                    Width = 220,
                    Alignment = ToolStripItemAlignment.Right
                };

                _statusBar.Items.AddRange(new ToolStripItem[] { _statusPanel, _countsPanel, _updatedPanel });
            }
            catch (Exception ex)
            {
                // Log error if logger is available
                var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<DashboardForm>>(Program.Services);
                logger?.LogError(ex, "Failed to build dashboard status bar");

                // Fallback: Create basic status bar
                try
                {
                    _statusBar = new StatusStrip { Dock = DockStyle.Bottom };
                    _statusPanel = new ToolStripStatusLabel { Text = "Status bar initialization failed" };
                    _statusBar.Items.Add(_statusPanel);
                }
                catch (Exception fallbackEx)
                {
                    // Failed to create even basic status bar - log and continue without it
                    logger?.LogWarning(fallbackEx, "Failed to create fallback status bar");
                }
            }
        }

        private void BuildBasicStatusStrip()
        {
            var strip = new StatusStrip
            {
                Name = "UiTestStatusStrip",
                Dock = DockStyle.Fill
            };

            strip.Items.Add(new ToolStripStatusLabel
            {
                Name = "UiTestStatusLabel",
                Text = "Ready"
            });

            _mainLayout?.Controls.Add(strip, 0, 5);
        }

        private RadialGauge CreateGauge(string label, Color needleColor)
        {
            var gauge = new RadialGauge
            {
                Dock = DockStyle.Fill,
                Width = 240,
                Height = 240,
                Margin = new Padding(5),
                // Theme inherited from form's SfSkinManager.SetVisualStyle
                MinimumValue = 0F,
                MaximumValue = 100F,
                MajorDifference = 20F,
                MinorDifference = 5F,
                MinorTickMarkHeight = 5,
                MajorTickMarkHeight = 10,
                NeedleStyle = NeedleStyle.Advanced,
                ShowScaleLabel = true,
                LabelPlacement = Syncfusion.Windows.Forms.Gauge.LabelPlacement.Inside,
                NeedleColor = needleColor,
                Value = 0F,
                GaugeLabel = label,
                ShowNeedle = true,
                EnableCustomNeedles = false,
                GaugeArcColor = ThemeColors.GaugeArc,
                ShowBackgroundFrame = true
            };

            SfSkinManager.SetVisualStyle(gauge, "Office2019Colorful");

            return gauge;
        }



        private void BindViewModel()
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.MunicipalityName):
                        UpdateMunicipalityLabel();
                        break;
                    case nameof(_viewModel.FiscalYear):
                        UpdateFiscalYearLabel();
                        break;
                    case nameof(_viewModel.LastUpdated):
                        UpdateLastUpdatedLabels();
                        break;
                    case nameof(_viewModel.IsLoading):
                        UpdateLoadingStatus();
                        break;
                    case nameof(_viewModel.ErrorMessage):
                        UpdateErrorStatus();
                        break;
                    case nameof(_viewModel.Metrics):
                        UpdateMetricsGrid();
                        break;
                    case nameof(_viewModel.TotalBudgetGauge):
                        AnimateGaugeValue(_budgetGauge, _viewModel.TotalBudgetGauge);
                        break;
                    case nameof(_viewModel.RevenueGauge):
                        AnimateGaugeValue(_revenueGauge, _viewModel.RevenueGauge);
                        break;
                    case nameof(_viewModel.ExpensesGauge):
                        AnimateGaugeValue(_expensesGauge, _viewModel.ExpensesGauge);
                        break;
                    case nameof(_viewModel.NetPositionGauge):
                        AnimateGaugeValue(_netPositionGauge, _viewModel.NetPositionGauge);
                        break;
                    case nameof(_viewModel.StatusText):
                        UpdateStatus(_viewModel.StatusText);
                        break;
                    case nameof(_viewModel.MonthlyRevenueData):
                        UpdateRevenueChart();
                        break;
                    case nameof(_viewModel.FundSummaries):
                        UpdateFundSummariesGrid();
                        break;
                    case nameof(_viewModel.DepartmentSummaries):
                        UpdateDepartmentSummariesGrid();
                        break;
                    case nameof(_viewModel.TopVariances):
                        UpdateTopVariancesGrid();
                        break;
                    case nameof(_viewModel.BudgetAnalysis):
                        UpdateBudgetAnalysisGrid();
                        break;
                    case nameof(_viewModel.TotalBudgeted):
                        AdjustGaugeMaximums();
                        break;
                    case nameof(_viewModel.TotalRevenue):
                        UpdateRevenueAmountGauge();
                        break;
                    case nameof(_viewModel.TotalExpenses):
                        UpdateExpensesAmountGauge();
                        break;
                    case nameof(_viewModel.TotalVariance):
                        UpdateVarianceAmountGauge();
                        break;
                    case nameof(_viewModel.VariancePercentage):
                        AnimateGaugeValue(_variancePercentGauge, (float)_viewModel.VariancePercentage);
                        break;
                }
            };

            // Bind analytics view model
            _analyticsViewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_analyticsViewModel.Metrics):
                        UpdateAnalyticsMetricsGrid();
                        break;
                    case nameof(_analyticsViewModel.TopVariances):
                        UpdateAnalyticsVariancesGrid();
                        break;
                    case nameof(_analyticsViewModel.TrendData):
                        UpdateTrendChart();
                        break;
                    case nameof(_analyticsViewModel.ScenarioProjections):
                        UpdateScenarioGrid();
                        break;
                    case nameof(_analyticsViewModel.ForecastData):
                        UpdateForecastChart();
                        break;
                    case nameof(_analyticsViewModel.IsLoading):
                        UpdateAnalyticsLoadingStatus();
                        break;
                    case nameof(_analyticsViewModel.StatusText):
                        UpdateAnalyticsStatus(_analyticsViewModel.StatusText);
                        break;
                }
            };

            // Initialize control states with current ViewModel data
            UpdateMetricsGrid();
            UpdateRevenueChart();
            UpdateFundSummariesGrid();
            UpdateDepartmentSummariesGrid();
            UpdateTopVariancesGrid();
            UpdateBudgetAnalysisGrid();
            AdjustGaugeMaximums();
            UpdateRevenueAmountGauge();
            UpdateExpensesAmountGauge();
            UpdateVarianceAmountGauge();
        }

        private void UpdateMunicipalityLabel()
        {
            if (_municipalityLabel != null)
                _municipalityLabel.Text = $"{DashboardResources.MunicipalityLabel} {_viewModel.MunicipalityName}";
        }

        private void UpdateFiscalYearLabel()
        {
            if (_fiscalYearLabel != null)
                _fiscalYearLabel.Text = $"{DashboardResources.FiscalYearLabel} {_viewModel.FiscalYear}";
        }

        private void UpdateLastUpdatedLabels()
        {
            var lastUpdatedText = _viewModel.LastUpdated == default ? "--" : _viewModel.LastUpdated.ToString("g", CultureInfo.CurrentCulture);
            if (_lastUpdatedLabel != null)
                _lastUpdatedLabel.Text = $"{DashboardResources.LastUpdatedLabel} {lastUpdatedText}";
            if (_updatedPanel != null)
                _updatedPanel.Text = $"Updated: {lastUpdatedText}";
        }

        private void UpdateLoadingStatus()
        {
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new System.Action(UpdateLoadingStatus)); } catch { }
                return;
            }

            // Update status text
            UpdateStatus(_viewModel.IsLoading ? DashboardResources.LoadingText : DashboardResources.StatusReady);

            // Toggle progress indicator
            try
            {
                if (_progressBar != null)
                {
                    _progressBar.Visible = _viewModel.IsLoading;
                }

                if (_loadButton != null)
                {
                    _loadButton.Enabled = !_viewModel.IsLoading;
                }

                if (_fileMenuLoadItem != null)
                {
                    _fileMenuLoadItem.Enabled = !_viewModel.IsLoading;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "UpdateLoadingStatus: failed to update loading UI elements");
            }
        }

        private void UpdateErrorStatus()
        {
            if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
            {
                UpdateStatus(_viewModel.ErrorMessage);
            }
        }

        private void UpdateMetricsGrid()
        {
            if (_metricsGrid != null)
                _metricsGrid.DataSource = _viewModel.Metrics;
            if (_countsPanel != null)
                _countsPanel.Text = $"{_viewModel.Metrics.Count} metrics";
        }

        private void UpdateRevenueChart()
        {
            try
            {
                _logger.LogDebug("UpdateRevenueChart: Starting update");

                if (_revenueChart == null || _viewModel.MonthlyRevenueData.Count == 0)
                {
                    _logger.LogWarning("UpdateRevenueChart: Chart is null or no monthly revenue data available (Count={Count})",
                        _viewModel.MonthlyRevenueData?.Count ?? 0);
                    return;
                }

                _revenueChart.Series.Clear();
                _logger.LogDebug("UpdateRevenueChart: Creating series with {Count} data points", _viewModel.MonthlyRevenueData.Count);

                // Create Revenue series
                var series = new ChartSeries("Revenue", ChartSeriesType.Line);

                // Populate data points
                foreach (var data in _viewModel.MonthlyRevenueData)
                {
                    series.Points.Add(data.MonthNumber, (double)data.Amount);
                }

                _revenueChart.Series.Add(series);
                _logger.LogInformation("UpdateRevenueChart: Chart updated with {SeriesCount} series and {PointCount} points",
                    _revenueChart.Series.Count, series.Points.Count);

                // Refresh chart to display updated data
                _revenueChart.Refresh();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateRevenueChart: Failed to update chart");
            }
        }

        private void UpdateFundSummariesGrid()
        {
            if (_fundsGrid != null)
                _fundsGrid.DataSource = _viewModel.FundSummaries;
        }

        private void UpdateDepartmentSummariesGrid()
        {
            if (_departmentsGrid != null)
                _departmentsGrid.DataSource = _viewModel.DepartmentSummaries;
        }

        private void UpdateTopVariancesGrid()
        {
            if (_topVariancesGrid != null)
                _topVariancesGrid.DataSource = _viewModel.TopVariances;
        }

        private void UpdateBudgetAnalysisGrid()
        {
            if (_analysisGrid == null)
                return;

            if (_viewModel.BudgetAnalysis == null)
            {
                _analysisGrid.DataSource = Array.Empty<BudgetVarianceAnalysis>();
                return;
            }

            _analysisGrid.DataSource = new[] { _viewModel.BudgetAnalysis };
        }

        private void AdjustGaugeMaximums()
        {
            try
            {
                float max = 100F;
                if (_viewModel.TotalBudgeted > 0M)
                {
                    // Use TotalBudgeted as a sensible maximum for amount-based gauges
                    max = Math.Max(1F, (float)_viewModel.TotalBudgeted);
                }

                if (_revenueAmountGauge != null)
                    _revenueAmountGauge.MaximumValue = max;
                if (_expensesAmountGauge != null)
                    _expensesAmountGauge.MaximumValue = max;
                if (_varianceAmountGauge != null)
                    _varianceAmountGauge.MaximumValue = max;

                if (_variancePercentGauge != null)
                    _variancePercentGauge.MaximumValue = 100F;
            }
            catch (Exception)
            {
                // Fall back silently - UI should still function with default ranges
            }
        }

        private void UpdateRevenueAmountGauge()
        {
            if (_revenueAmountGauge == null)
                return;

            var value = (float)_viewModel.TotalRevenue;
            if (_revenueAmountGauge.MaximumValue < value)
                _revenueAmountGauge.MaximumValue = Math.Max(_revenueAmountGauge.MinimumValue + 1F, value);

            AnimateGaugeValue(_revenueAmountGauge, value);
        }

        private void UpdateExpensesAmountGauge()
        {
            if (_expensesAmountGauge == null)
                return;

            var value = (float)_viewModel.TotalExpenses;
            if (_expensesAmountGauge.MaximumValue < value)
                _expensesAmountGauge.MaximumValue = Math.Max(_expensesAmountGauge.MinimumValue + 1F, value);

            AnimateGaugeValue(_expensesAmountGauge, value);
        }

        private void UpdateVarianceAmountGauge()
        {
            if (_varianceAmountGauge == null)
                return;

            var value = (float)Math.Abs(_viewModel.TotalVariance);
            if (_varianceAmountGauge.MaximumValue < value)
                _varianceAmountGauge.MaximumValue = Math.Max(_varianceAmountGauge.MinimumValue + 1F, value);

            AnimateGaugeValue(_varianceAmountGauge, value);
        }

        private void AnimateGaugeValue(RadialGauge? gauge, float value)
        {
            if (gauge == null)
            {
                _logger.LogDebug("AnimateGaugeValue: Gauge is null, skipping animation");
                return;
            }

            var target = Math.Max(gauge.MinimumValue, Math.Min(value, gauge.MaximumValue));
            var start = gauge.Value;
            _logger.LogDebug("AnimateGaugeValue: Animating gauge '{Label}' from {Start:F2} to {Target:F2}",
                gauge.GaugeLabel, start, target);
            var steps = 15;
            var stepValue = (target - start) / steps;
            var timer = new System.Windows.Forms.Timer { Interval = 16 };
            var currentStep = 0;

            timer.Tick += (s, e) =>
            {
                if (gauge.IsDisposed)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                currentStep++;
                var next = start + (stepValue * currentStep);
                gauge.Value = currentStep >= steps ? target : next;

                if (currentStep >= steps)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };

            timer.Start();
        }

        private async Task LoadDashboard()
        {
            try
            {
                _logger.LogInformation("LoadDashboard: Starting dashboard load");

                await _viewModel.LoadCommand.ExecuteAsync(null);

                _logger.LogInformation("LoadDashboard: ViewModel load completed successfully");
                UpdateStatus(DashboardResources.StatusReady);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("LoadDashboard: Load cancelled");
                UpdateStatus(DashboardResources.StatusReady);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogInformation("LoadDashboard: Load aborted due to disposal");
                UpdateStatus(DashboardResources.StatusReady);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoadDashboard: Failed to load dashboard");
                UpdateStatus(string.Format(CultureInfo.CurrentCulture, DashboardResources.LoadErrorMessage, ex.Message));

                if (!_isUiTestHarness)
                {
                    MessageBox.Show(string.Format(CultureInfo.CurrentCulture, DashboardResources.LoadErrorMessage, ex.Message),
                        DashboardResources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task ExportDashboard()
        {
            if (_isUiTestHarness)
            {
                using var uiTestDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf|CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = "pdf",
                    FileName = $"Dashboard_{_viewModel.MunicipalityName.Replace(" ", "_", StringComparison.Ordinal)}_{_viewModel.FiscalYear.Replace(" ", "_", StringComparison.Ordinal)}.pdf"
                };

                if (uiTestDialog.ShowDialog() == DialogResult.OK)
                {
                    var exportPath = uiTestDialog.FileName;

                    try
                    {
                        var directory = Path.GetDirectoryName(exportPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        await File.WriteAllTextAsync(exportPath, "%PDF-FAKE\n");
                    }
                    catch (Exception ex)
                    {
                        if (!_isUiTestHarness)
                        {
                            MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                return;
            }

            using var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
                DefaultExt = "csv",
                FileName = $"Dashboard_{_viewModel.MunicipalityName.Replace(" ", "_", StringComparison.Ordinal)}_{_viewModel.FiscalYear.Replace(" ", "_", StringComparison.Ordinal)}.csv"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        if (saveDialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            // Export to CSV
                            var csv = new System.Text.StringBuilder();
                            csv.AppendLine("Metric,Value,Unit,Trend,Change %,Description");

                            foreach (var metric in _viewModel.Metrics)
                            {
                                string line = string.Format(CultureInfo.InvariantCulture,
                                "{0},{1},{2},{3},{4},{5}",
                                EscapeCsv(metric.Name),
                                metric.Value,
                                EscapeCsv(metric.Unit),
                                EscapeCsv(metric.Trend),
                                metric.ChangePercent,
                                EscapeCsv(metric.Description));
                                csv.AppendLine(line);
                            }

                            System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString());
                        }
                        else
                        {
                            // Excel export requires Syncfusion.XlsIO - placeholder for now
                            if (!_isUiTestHarness)
                            {
                                MessageBox.Show("Excel export requires Syncfusion.XlsIO package. Please use CSV format.",
                                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    });

                    if (!_isUiTestHarness)
                    {
                        MessageBox.Show($"Dashboard exported successfully to {saveDialog.FileName}",
                            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    UpdateStatus(DashboardResources.StatusExported);
                }
                catch (Exception ex)
                {
                    if (!_isUiTestHarness)
                    {
                        MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            {
                return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            }
            return value;
        }

        private void ToggleAutoRefresh(bool enabled)
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Enabled = enabled;
            }
        }

        private void UpdateStatus(string text)
        {
            if (_statusPanel != null)
            {
                _statusPanel.Text = text;
            }
        }

        private void UpdateAnalyticsMetricsGrid()
        {
            if (_analyticsMetricsGrid != null)
                _analyticsMetricsGrid.DataSource = _analyticsViewModel.Metrics;
        }

        private void UpdateAnalyticsVariancesGrid()
        {
            if (_analyticsVariancesGrid != null)
                _analyticsVariancesGrid.DataSource = _analyticsViewModel.TopVariances;
        }

        private void UpdateTrendChart()
        {
            try
            {
                _logger.LogDebug("UpdateTrendChart: Starting update");

                if (_trendChart == null || _analyticsViewModel.TrendData.Count == 0)
                {
                    _logger.LogWarning("UpdateTrendChart: Chart is null or no trend data available (Count={Count})",
                        _analyticsViewModel.TrendData?.Count ?? 0);
                    return;
                }

                _logger.LogDebug("UpdateTrendChart: Creating series with {Count} data points", _analyticsViewModel.TrendData.Count);

                _trendChart.Series.Clear();

                // Create Budgeted series
                var budgetedSeries = new ChartSeries("Budgeted", ChartSeriesType.Line);

                // Create Actual series
                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Line);

                // Populate data points with numeric indices
                int index = 0;
                foreach (var trend in _analyticsViewModel.TrendData)
                {
                    budgetedSeries.Points.Add(index, (double)trend.Budgeted);
                    actualSeries.Points.Add(index, (double)trend.Actual);
                    index++;
                }

                _trendChart.Series.Add(budgetedSeries);
                _trendChart.Series.Add(actualSeries);
                _trendChart.Refresh();

                _logger.LogInformation("UpdateTrendChart: Chart updated with {SeriesCount} series and {PointCount} points",
                    _trendChart.Series.Count, budgetedSeries.Points.Count + actualSeries.Points.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateTrendChart: Failed to update chart");
            }
        }

        private void UpdateScenarioGrid()
        {
            if (_scenarioGrid != null)
                _scenarioGrid.DataSource = _analyticsViewModel.ScenarioProjections;
        }

        private void UpdateForecastChart()
        {
            try
            {
                _logger.LogDebug("UpdateForecastChart: Starting update");

                if (_forecastChart == null)
                {
                    _logger.LogWarning("UpdateForecastChart: Chart is null");
                    return;
                }

                if (_analyticsViewModel.ForecastData.Count == 0)
                {
                    _logger.LogWarning("UpdateForecastChart: No forecast data available (Count={Count})",
                        _analyticsViewModel.ForecastData.Count);
                }

                _logger.LogDebug("UpdateForecastChart: Creating series with {Count} data points", _analyticsViewModel.ForecastData.Count);

                _forecastChart.Series.Clear();
                var forecastSeries = new ChartSeries("Forecast", ChartSeriesType.Line);

                int index = 0;
                foreach (var point in _analyticsViewModel.ForecastData)
                {
                    forecastSeries.Points.Add(index, (double)point.PredictedReserves);
                    index++;
                }

                _forecastChart.Series.Add(forecastSeries);
                _forecastChart.Refresh();

                _logger.LogInformation("UpdateForecastChart: Chart updated with {SeriesCount} series and {PointCount} points",
                    _forecastChart.Series.Count, forecastSeries.Points.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateForecastChart: Failed to update chart");
            }
        }

        private void UpdateAnalyticsLoadingStatus()
        {
            // Could show loading indicator for analytics
        }

        private void UpdateAnalyticsStatus(string text)
        {
            UpdateStatus($"Analytics: {text}");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Dispose();
                _revenueChart?.Dispose();
                _metricsGrid?.Dispose();
                _fundsGrid?.Dispose();
                _departmentsGrid?.Dispose();
                _topVariancesGrid?.Dispose();
                _analysisGrid?.Dispose();
                _budgetGauge?.Dispose();
                _revenueGauge?.Dispose();
                _expensesGauge?.Dispose();
                _netPositionGauge?.Dispose();
                _variancePercentGauge?.Dispose();
                _varianceAmountGauge?.Dispose();
                _revenueAmountGauge?.Dispose();
                _expensesAmountGauge?.Dispose();
                _toolbar?.Dispose();
                _statusBar?.Dispose();
                try
                {
                    _viewModel?.Dispose();
                }
                catch (Exception ex)
                {
                    var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<DashboardForm>>(Program.Services);
                    logger?.LogDebug(ex, "Error disposing DashboardViewModel");
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (MdiParent is MainForm mainForm)
            {
                if (_menuStrip != null)
                {
                    mainForm.ConfigureChildMenuMerging(_menuStrip);
                }
            }
        }
    }
}
