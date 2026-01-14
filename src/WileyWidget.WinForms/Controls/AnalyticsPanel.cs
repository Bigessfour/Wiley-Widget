using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChartControl = Syncfusion.Windows.Forms.Chart.ChartControl;
using ChartSeries = Syncfusion.Windows.Forms.Chart.ChartSeries;
using ChartSeriesType = Syncfusion.Windows.Forms.Chart.ChartSeriesType;
using GradientPanelExt = Syncfusion.Windows.Forms.Tools.GradientPanelExt;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfListView = Syncfusion.WinForms.ListView.SfListView;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using TextBoxExt = Syncfusion.Windows.Forms.Tools.TextBoxExt;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using GridNumericColumn = Syncfusion.WinForms.DataGrid.GridNumericColumn;
using Syncfusion.Drawing;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for analytics and scenario modeling with budget analysis, forecasting, and recommendations.
/// Features exploratory analysis, rate scenarios, trend visualization, and predictive modeling.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class AnalyticsPanel : ScopedPanelBase<AnalyticsViewModel>
{

    // UI Controls
    private SfDataGrid? _metricsGrid;
    private SfDataGrid? _variancesGrid;
    private ChartControl? _trendsChart;
    private ChartControl? _forecastChart;
    private SfButton? _performAnalysisButton;
    private SfButton? _runScenarioButton;
    private SfButton? _generateForecastButton;
    private SfButton? _refreshButton;
    private TextBoxExt? _rateIncreaseTextBox;
    private TextBoxExt? _expenseIncreaseTextBox;
    private TextBoxExt? _revenueTargetTextBox;
    private TextBoxExt? _projectionYearsTextBox;
    private TextBoxExt? _metricsSearchTextBox;
    private TextBoxExt? _variancesSearchTextBox;
    private SfListView? _insightsListBox;
    private SfListView? _recommendationsListBox;
    private Label? _totalBudgetedLabel;
    private Label? _totalActualLabel;
    private Label? _totalVarianceLabel;
    private Label? _averageVarianceLabel;
    private Label? _recommendationExplanationLabel;
    private Panel? _scenarioPanel;
    private Panel? _resultsPanel;
    private Panel? _chartsPanel;
    private Panel? _buttonPanel;
    private SplitContainer? _mainSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private ErrorProvider? _errorProvider;

    /// <summary>
    /// Initializes a new instance of the AnalyticsPanel class.
    /// </summary>
    /// <param name="viewModel">The analytics view model.</param>
    /// <param name="logger">The logger instance.</param>
    public AnalyticsPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<AnalyticsViewModel>> logger)
        : base(scopeFactory, logger)
    {
        // Apply theme via SfSkinManager (single source of truth)
        try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, "Office2019Colorful"); } catch { }

        // Call the actual initialization method
        InitializeControls();
    }

    /// <summary>
    /// Initializes all UI controls and sets up the layout.
    /// </summary>
    private void InitializeControls()
    {
        // Apply Syncfusion theme using SkinManager
        SfSkinManager.SetTheme(this, "Office2019Colorful");

        // Set up form properties
        Text = "Budget Analytics";
        Size = new Size(1400, 900);
        MinimumSize = new Size(1000, 600);

        // Panel header with actions
        _panelHeader = new PanelHeader { Dock = DockStyle.Top, Title = "Budget Analytics & Forecasting" };
        _panelHeader.RefreshClicked += async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null);
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main split container
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplitContainer, 300);

        // Top panel - Controls and scenario input
        InitializeTopPanel();

        // Bottom panel - Results and charts
        InitializeBottomPanel();

        // Status strip for operation feedback
        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay { Message = "Running analytics..." };
        Controls.Add(_loadingOverlay);

        // No data overlay
        _noDataOverlay = new NoDataOverlay { Message = "No analytics data available\r\nPerform budget operations to generate insights" };
        Controls.Add(_noDataOverlay);

        Controls.Add(_mainSplitContainer);
        Controls.Add(_statusStrip);

        // Error provider for validation
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
            BlinkRate = 0
        };

        // Set tab order
        SetTabOrder();
    }

    /// <summary>
    /// Initializes the top panel with buttons and scenario inputs.
    /// </summary>
    private void InitializeTopPanel()
    {
        var topPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(topPanel, ThemeColors.DefaultTheme);

        // Button panel
        _buttonPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, "Office2019Colorful");

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1
        };

        for (int i = 0; i < 7; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.28f));

        _performAnalysisButton = new SfButton
        {
            Text = "&Perform Analysis",
            TabIndex = 1,
            AccessibleName = "Perform Analysis",
            AccessibleDescription = "Run exploratory data analysis on budget data"
        };
        _performAnalysisButton.Click += async (s, e) => await ViewModel!.PerformAnalysisCommand.ExecuteAsync(null);

        _runScenarioButton = new SfButton
        {
            Text = "&Run Scenario",
            TabIndex = 2,
            AccessibleName = "Run Scenario",
            AccessibleDescription = "Run rate adjustment scenario analysis"
        };
        _runScenarioButton.Click += async (s, e) => await ViewModel!.RunScenarioCommand.ExecuteAsync(null);

        _generateForecastButton = new SfButton
        {
            Text = "&Generate Forecast",
            TabIndex = 3,
            AccessibleName = "Generate Forecast",
            AccessibleDescription = "Generate predictive reserve forecast"
        };
        _generateForecastButton.Click += async (s, e) => await ViewModel!.GenerateForecastCommand.ExecuteAsync(null);

        _refreshButton = new SfButton
        {
            Text = "&Refresh",
            TabIndex = 4,
            AccessibleName = "Refresh",
            AccessibleDescription = "Refresh analytics data"
        };
        _refreshButton.Click += async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null);

        buttonTable.Controls.Add(_performAnalysisButton, 0, 0);
        buttonTable.Controls.Add(_runScenarioButton, 1, 0);
        buttonTable.Controls.Add(_generateForecastButton, 2, 0);
        buttonTable.Controls.Add(_refreshButton, 3, 0);

        var entityLabel = new Label
        {
            Text = "Entity:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _entityComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 5,
            AccessibleName = "Entity Selector",
            AccessibleDescription = "Filter analytics by entity or fund",
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
        };
        _entityComboBox.DataSource = new List<string> { "All Entities" };
        _entityComboBox.SelectedIndexChanged += EntityComboBox_SelectedIndexChanged;

        buttonTable.Controls.Add(entityLabel, 4, 0);
        buttonTable.Controls.Add(_entityComboBox, 5, 0);

        // Navigation buttons removed from UI design per updated UX flow.
        // Pragma suppression is safe here: null checks are defensive for potential future restoration of navigation buttons.
        // if (_navigateToBudgetButton != null) buttonTable.Controls.Add(_navigateToBudgetButton, 4, 0);
        // if (_navigateToAccountsButton != null) buttonTable.Controls.Add(_navigateToAccountsButton, 5, 0);
        // if (_navigateToDashboardButton != null) buttonTable.Controls.Add(_navigateToDashboardButton, 6, 0);

        _buttonPanel.Controls.Add(buttonTable);
        topPanel.Controls.Add(_buttonPanel);

        // Scenario input panel
        _scenarioPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_scenarioPanel, "Office2019Colorful");

        var scenarioGroup = new GradientPanelExt
        {
            Text = "Scenario Parameters",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(scenarioGroup, "Office2019Colorful");

        var scenarioTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4
        };

        scenarioTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        scenarioTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        scenarioTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        scenarioTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        // Row 1: Rate Increase
        var rateIncreaseLabel = new Label
        {
            Text = "Rate Increase (%):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
        _rateIncreaseTextBox = new TextBoxExt
        {
            Text = "5.0",
            Dock = DockStyle.Fill,
            TabIndex = 5,
            AccessibleName = "Rate Increase Percentage",
            AccessibleDescription = "Percentage increase for rate scenario"
        };
#pragma warning restore RS0030
        _rateIncreaseTextBox.TextChanged += RateIncreaseTextBox_TextChanged;

        // Row 2: Expense Increase
        var expenseIncreaseLabel = new Label
        {
            Text = "Expense Increase (%):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
        _expenseIncreaseTextBox = new TextBoxExt
        {
            Text = "3.0",
            Dock = DockStyle.Fill,
            TabIndex = 6,
            AccessibleName = "Expense Increase Percentage",
            AccessibleDescription = "Percentage increase for expenses in scenario"
        };
#pragma warning restore RS0030
        _expenseIncreaseTextBox.TextChanged += ExpenseIncreaseTextBox_TextChanged;

        // Row 3: Revenue Target
        var revenueTargetLabel = new Label
        {
            Text = "Revenue Target (%):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
        _revenueTargetTextBox = new TextBoxExt
        {
            Text = "10.0",
            Dock = DockStyle.Fill,
            TabIndex = 7,
            AccessibleName = "Revenue Target Percentage",
            AccessibleDescription = "Target revenue increase percentage"
        };
#pragma warning restore RS0030
        _revenueTargetTextBox.TextChanged += RevenueTargetTextBox_TextChanged;

        // Row 4: Projection Years
        var projectionYearsLabel = new Label
        {
            Text = "Projection Years:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

#pragma warning disable RS0030 // TextBoxExt is the approved replacement for TextBox
        _projectionYearsTextBox = new TextBoxExt
        {
            Text = "3",
            Dock = DockStyle.Fill,
            TabIndex = 8,
            AccessibleName = "Projection Years",
            AccessibleDescription = "Number of years for projections"
        };
#pragma warning restore RS0030
        _projectionYearsTextBox.TextChanged += ProjectionYearsTextBox_TextChanged;

        scenarioTable.Controls.Add(rateIncreaseLabel, 0, 0);
        scenarioTable.Controls.Add(_rateIncreaseTextBox, 1, 0);
        scenarioTable.Controls.Add(expenseIncreaseLabel, 0, 1);
        scenarioTable.Controls.Add(_expenseIncreaseTextBox, 1, 1);
        scenarioTable.Controls.Add(revenueTargetLabel, 0, 2);
        scenarioTable.Controls.Add(_revenueTargetTextBox, 1, 2);
        scenarioTable.Controls.Add(projectionYearsLabel, 0, 3);
        scenarioTable.Controls.Add(_projectionYearsTextBox, 1, 3);

        scenarioGroup.Controls.Add(scenarioTable);
        _scenarioPanel.Controls.Add(scenarioGroup);
        topPanel.Controls.Add(_scenarioPanel);

        _mainSplitContainer.Panel1.Controls.Add(topPanel);
    }

    /// <summary>
    /// Initializes the bottom panel with grids, charts, and insights.
    /// </summary>
    private void InitializeBottomPanel()
    {
        var bottomPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(bottomPanel, ThemeColors.DefaultTheme);

        // Results panel with grids and insights
        _resultsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_resultsPanel, ThemeColors.DefaultTheme);

        var resultsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(resultsSplit, 200);

        // Top results - Grids
        var gridsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(gridsPanel, ThemeColors.DefaultTheme);

        var gridsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(gridsSplit, 850);

        // Metrics grid
        var metricsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(metricsPanel, ThemeColors.DefaultTheme);

        // Metrics search (deduplicated, always use TextBoxExt and GradientPanelExt)
        var metricsSearchPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 30,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(metricsSearchPanel, ThemeColors.DefaultTheme);
        var metricsSearchLabel = new Label { Text = "Search Metrics:", Dock = DockStyle.Left, Width = 100 };
        _metricsSearchTextBox = new TextBoxExt { Dock = DockStyle.Fill, TabIndex = 9 };
        _metricsSearchTextBox.TextChanged += MetricsSearchTextBox_TextChanged;
        metricsSearchPanel.Controls.Add(_metricsSearchTextBox);
        metricsSearchPanel.Controls.Add(metricsSearchLabel);

        _metricsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            TabIndex = 10,
            AccessibleName = "Analytics Metrics Grid"
        };

        _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = "Category" });
        _metricsGrid.Columns.Add(new GridNumericColumn { MappingName = "Value", HeaderText = "Amount", Format = "C2" });
        _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "Unit", HeaderText = "Unit" });

        metricsPanel.Controls.Add(_metricsGrid);
        metricsPanel.Controls.Add(metricsSearchPanel);
        gridsSplit.Panel1.Controls.Add(metricsPanel);

        // Variances grid
        var variancesPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(variancesPanel, "Office2019Colorful");

        // Variances search (deduplicated, always use TextBoxExt and GradientPanelExt)
        var variancesSearchPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 30,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(variancesSearchPanel, "Office2019Colorful");
        var variancesSearchLabel = new Label { Text = "Search Variances:", Dock = DockStyle.Left, Width = 120 };
        _variancesSearchTextBox = new TextBoxExt { Dock = DockStyle.Fill, TabIndex = 11 };
        _variancesSearchTextBox.TextChanged += VariancesSearchTextBox_TextChanged;
        variancesSearchPanel.Controls.Add(_variancesSearchTextBox);
        variancesSearchPanel.Controls.Add(variancesSearchLabel);

        _variancesGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            TabIndex = 12,
            AccessibleName = "Variance Analysis Grid"
        };

        _variancesGrid.Columns.Add(new GridTextColumn { MappingName = "AccountNumber", HeaderText = "Account" });
        _variancesGrid.Columns.Add(new GridTextColumn { MappingName = "AccountName", HeaderText = "Name" });
        _variancesGrid.Columns.Add(new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budgeted", Format = "C2" });
        _variancesGrid.Columns.Add(new GridNumericColumn { MappingName = "ActualAmount", HeaderText = "Actual", Format = "C2" });
        _variancesGrid.Columns.Add(new GridNumericColumn { MappingName = "VarianceAmount", HeaderText = "Variance", Format = "C2" });
        _variancesGrid.Columns.Add(new GridNumericColumn { MappingName = "VariancePercentage", HeaderText = "Variance %", Format = "P2" });

        variancesPanel.Controls.Add(_variancesGrid);
        variancesPanel.Controls.Add(variancesSearchPanel);
        gridsSplit.Panel2.Controls.Add(variancesPanel);

        resultsSplit.Panel1.Controls.Add(gridsSplit);

        // Bottom results - Insights and recommendations
        var insightsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(insightsPanel, ThemeColors.DefaultTheme);

        var insightsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(insightsSplit, 150);

        // Insights list
        var insightsGroup = new GradientPanelExt
        {
            Text = "Key Insights",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(insightsGroup, ThemeColors.DefaultTheme);

        _insightsListBox = new SfListView
        {
            Dock = DockStyle.Fill,
            TabIndex = 13,
            AccessibleName = "Key Insights List",
            AccessibleDescription = "List of key insights from analysis"
        };

        insightsGroup.Controls.Add(_insightsListBox);
        insightsSplit.Panel1.Controls.Add(insightsGroup);

        // Recommendations list
        var recommendationsGroup = new GradientPanelExt
        {
            Text = "Recommendations",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(recommendationsGroup, "Office2019Colorful");

        _recommendationsListBox = new SfListView
        {
            Dock = DockStyle.Fill,
            TabIndex = 14,
            AccessibleName = "Recommendations List",
            AccessibleDescription = "List of recommendations from scenario analysis"
        };

        recommendationsGroup.Controls.Add(_recommendationsListBox);
        insightsSplit.Panel2.Controls.Add(recommendationsGroup);

        resultsSplit.Panel2.Controls.Add(insightsSplit);
        _resultsPanel.Controls.Add(resultsSplit);
        bottomPanel.Controls.Add(_resultsPanel);

        // Summary panel
        var summaryPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(10)
        };

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1
        };

        for (int i = 0; i < 5; i++)
            summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        _totalBudgetedLabel = new Label { Text = "Total Budgeted: $0.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        _totalActualLabel = new Label { Text = "Total Actual: $0.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        _totalVarianceLabel = new Label { Text = "Total Variance: $0.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        _averageVarianceLabel = new Label { Text = "Avg Variance: 0.00%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        _recommendationExplanationLabel = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };

        summaryTable.Controls.Add(_totalBudgetedLabel, 0, 0);
        summaryTable.Controls.Add(_totalActualLabel, 1, 0);
        summaryTable.Controls.Add(_totalVarianceLabel, 2, 0);
        summaryTable.Controls.Add(_averageVarianceLabel, 3, 0);
        summaryTable.Controls.Add(_recommendationExplanationLabel, 4, 0);

        summaryPanel.Controls.Add(summaryTable);
        bottomPanel.Controls.Add(summaryPanel);

        // Charts panel
        _chartsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Bottom,
            Height = 300,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_chartsPanel, "Office2019Colorful");

        var chartsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(chartsSplit, 400);

        // Trends chart
        var trendsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(trendsPanel, "Office2019Colorful");

        _trendsChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 14,
            AccessibleName = "Trends Chart"
        };

        ChartControlDefaults.Apply(_trendsChart!);

        _trendsChart.Title.Text = "Monthly Budget Trends";
        _trendsChart.Title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _trendsChart.Legend.Visible = true;

        trendsPanel.Controls.Add(_trendsChart);
        chartsSplit.Panel1.Controls.Add(trendsPanel);

        // Forecast chart
        var forecastPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(forecastPanel, "Office2019Colorful");

        _forecastChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 16,
            AccessibleName = "Forecast Chart"
        };

        ChartControlDefaults.Apply(_forecastChart!);

        _forecastChart.Title.Text = "Reserve Forecast";
        _forecastChart.Title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _forecastChart.Legend.Visible = true;

        forecastPanel.Controls.Add(_forecastChart);
        chartsSplit.Panel2.Controls.Add(forecastPanel);

        _chartsPanel.Controls.Add(chartsSplit);
        bottomPanel.Controls.Add(_chartsPanel);

        _mainSplitContainer.Panel2.Controls.Add(bottomPanel);
    }

    /// <summary>
    /// Sets the tab order for controls.
    /// </summary>
    private void SetTabOrder()
    {
        // Tab order set in control initialization
    }

    private SfComboBox? _entityComboBox;

    /// <summary>
    /// Called when the ViewModel is resolved from the scoped provider so panels can bind to it safely.
    /// </summary>
    protected override void OnViewModelResolved(AnalyticsViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);

        // Subscribe to property changed for UI updates
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Initial binding for grids and lists
        if (_metricsGrid != null) _metricsGrid.DataSource = viewModel.FilteredMetrics;
        if (_variancesGrid != null) _variancesGrid.DataSource = viewModel.FilteredTopVariances;
        if (_insightsListBox != null) _insightsListBox.DataSource = viewModel.Insights ?? new System.Collections.ObjectModel.ObservableCollection<string>();
        if (_recommendationsListBox != null) _recommendationsListBox.DataSource = viewModel.Recommendations ?? new System.Collections.ObjectModel.ObservableCollection<string>();

        // Bind entity combo if present
        if (_entityComboBox != null)
        {
            _entityComboBox.DataSource = viewModel.AvailableEntities ?? new System.Collections.ObjectModel.ObservableCollection<string>(new[] { "All Entities" });
            _entityComboBox.SelectedItem = string.IsNullOrWhiteSpace(viewModel.SelectedEntity) ? "All Entities" : viewModel.SelectedEntity;
            _entityComboBox.SelectedIndexChanged -= EntityComboBox_SelectedIndexChanged;
            _entityComboBox.SelectedIndexChanged += EntityComboBox_SelectedIndexChanged;
        }
    }

    /// <summary>
    /// Handles rate increase text box text changed event.
    /// </summary>
    private void RateIncreaseTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (decimal.TryParse(_rateIncreaseTextBox?.Text, out var value) && value >= 0 && value <= 100)
            _viewModel.RateIncreasePercentage = value;
        else
            _rateIncreaseTextBox!.Text = _viewModel.RateIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Handles expense increase text box text changed event.
    /// </summary>
    private void ExpenseIncreaseTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (decimal.TryParse(_expenseIncreaseTextBox?.Text, out var value) && value >= 0 && value <= 100)
            _viewModel.ExpenseIncreasePercentage = value;
        else
            _expenseIncreaseTextBox!.Text = _viewModel.ExpenseIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Handles revenue target text box text changed event.
    /// </summary>
    private void RevenueTargetTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (decimal.TryParse(_revenueTargetTextBox?.Text, out var value) && value >= 0 && value <= 100)
            _viewModel.RevenueTargetPercentage = value;
        else
            _revenueTargetTextBox!.Text = _viewModel.RevenueTargetPercentage.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Handles projection years text box text changed event.
    /// </summary>
    private void ProjectionYearsTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (int.TryParse(_projectionYearsTextBox?.Text, out var value) && value > 0 && value <= 10)
            _viewModel.ProjectionYears = value;
        else
            _projectionYearsTextBox!.Text = _viewModel.ProjectionYears.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Handles metrics search text box text changed event.
    /// </summary>
    private void MetricsSearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        _viewModel.MetricsSearchText = _metricsSearchTextBox?.Text ?? string.Empty;
    }

    /// <summary>
    /// Handles variances search text box text changed event.
    /// </summary>
    private void VariancesSearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        _viewModel.VariancesSearchText = _variancesSearchTextBox?.Text ?? string.Empty;
    }

    private void EntityComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;
        if (_entityComboBox?.SelectedItem is string entity && !string.Equals(entity, "All Entities", StringComparison.OrdinalIgnoreCase))
        {
            ViewModel.SelectedEntity = entity;
        }
        else
        {
            ViewModel.SelectedEntity = null;
        }
    }

    /// <summary>
    /// Handles view model property changed event.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        // Per Microsoft documentation: Check if we're on a different thread and marshal if needed
        // This prevents InvalidOperationException: "Cross-thread operation not valid"
        if (InvokeRequired)
        {
            // Recursively call this handler on the UI thread (per Microsoft pattern)
            // Use fully-qualified System.Action to avoid ambiguity with Syncfusion.Windows.Forms.Tools.Action
            BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(_viewModel.FilteredMetrics):
                if (_metricsGrid != null) _metricsGrid.DataSource = _viewModel.FilteredMetrics;
                break;

            case nameof(_viewModel.FilteredTopVariances):
                if (_variancesGrid != null) _variancesGrid.DataSource = _viewModel.FilteredTopVariances;
                break;

            case nameof(ViewModel.AvailableEntities):
                if (_entityComboBox != null && ViewModel.AvailableEntities != null)
                {
                    _entityComboBox.DataSource = ViewModel.AvailableEntities;
                    _entityComboBox.SelectedItem = string.IsNullOrWhiteSpace(ViewModel.SelectedEntity) ? "All Entities" : ViewModel.SelectedEntity;
                }
                break;

            case nameof(ViewModel.SelectedEntity):
                if (_entityComboBox != null)
                {
                    _entityComboBox.SelectedItem = string.IsNullOrWhiteSpace(ViewModel.SelectedEntity) ? "All Entities" : ViewModel.SelectedEntity;
                }
                break;

            case nameof(ViewModel.TrendData):
                UpdateTrendsChart();
                break;

            case nameof(ViewModel.Insights):
                if (_insightsListBox != null)
                {
                    _insightsListBox.DataSource = ViewModel.Insights ?? new System.Collections.ObjectModel.ObservableCollection<string>();
                }
                break;

            case nameof(ViewModel.Recommendations):
                if (_recommendationsListBox != null)
                {
                    _recommendationsListBox.DataSource = ViewModel.Recommendations ?? new System.Collections.ObjectModel.ObservableCollection<string>();
                }
                break;

            case nameof(ViewModel.ForecastData):
                UpdateForecastChart();
                break;

            case nameof(ViewModel.IsLoading):
                _loadingOverlay?.Visible = ViewModel.IsLoading;
                _noDataOverlay?.Visible = !ViewModel.IsLoading && (ViewModel.Metrics == null || ViewModel.Metrics.Count == 0);
                break;

            case nameof(ViewModel.StatusText):
                _statusLabel?.Text = ViewModel.StatusText;
                break;

            case nameof(ViewModel.TotalBudgetedAmount):
                if (_totalBudgetedLabel != null) _totalBudgetedLabel.Text = $"Total Budgeted: {ViewModel.TotalBudgetedAmount:C}";
                break;

            case nameof(ViewModel.TotalActualAmount):
                if (_totalActualLabel != null) _totalActualLabel.Text = $"Total Actual: {ViewModel.TotalActualAmount:C}";
                break;

            case nameof(ViewModel.TotalVarianceAmount):
                if (_totalVarianceLabel != null) _totalVarianceLabel.Text = $"Total Variance: {ViewModel.TotalVarianceAmount:C}";
                break;

            case nameof(ViewModel.AverageVariancePercentage):
                if (_averageVarianceLabel != null) _averageVarianceLabel.Text = $"Avg Variance: {ViewModel.AverageVariancePercentage:P}";
                break;

            case nameof(ViewModel.RecommendationExplanation):
                if (_recommendationExplanationLabel != null) _recommendationExplanationLabel.Text = ViewModel.RecommendationExplanation;
                break;
        }
    }

    /// <summary>
    /// Updates the trends chart with current data.
    /// </summary>
    private void UpdateTrendsChart()
    {
        if (_trendsChart == null || ViewModel == null || !ViewModel.TrendData.Any())
            return;

        try
        {
            _trendsChart.Series.Clear();

            var budgetedSeries = new ChartSeries("Budgeted", ChartSeriesType.Line);
            var actualSeries = new ChartSeries("Actual", ChartSeriesType.Line);

            foreach (var trend in ViewModel.TrendData)
            {
                budgetedSeries.Points.Add(trend.Month, (double)trend.Budgeted);
                actualSeries.Points.Add(trend.Month, (double)trend.Actual);
            }

            _trendsChart.Series.Add(budgetedSeries);
            _trendsChart.Series.Add(actualSeries);
            _trendsChart.Refresh();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating trends chart");
        }
    }

    /// <summary>
    /// Updates the forecast chart with current data.
    /// </summary>
    private void UpdateForecastChart()
    {
        if (_forecastChart == null || ViewModel == null || !ViewModel.ForecastData.Any())
            return;

        try
        {
            _forecastChart.Series.Clear();

            var forecastSeries = new ChartSeries("Predicted Reserves", ChartSeriesType.Line);

            foreach (var point in ViewModel.ForecastData)
            {
                forecastSeries.Points.Add(point.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture), (double)point.PredictedReserves);
            }

            _forecastChart.Series.Add(forecastSeries);
            _forecastChart.Refresh();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating forecast chart");
        }
    }

    /// <summary>
    /// Closes the panel.
    /// </summary>
    private void ClosePanel()
    {
        try
        {
            var parentForm = this.FindForm();
            if (parentForm is Forms.MainForm mainForm)
            {
                mainForm.ClosePanel(Name);
                return;
            }

            var method = parentForm?.GetType().GetMethod("ClosePanel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            method?.Invoke(parentForm, new object[] { Name });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AnalyticsPanel: ClosePanel failed");
        }
    }

    /// <summary>
    /// Handles the load event.
    /// </summary>
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (ViewModel == null)
        {
            Logger.LogWarning("ViewModel not initialized in OnLoad");
            return;
        }

        try
        {
            // Initialize scenario parameters
            if (_rateIncreaseTextBox != null)
                _rateIncreaseTextBox.Text = ViewModel.RateIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
            if (_expenseIncreaseTextBox != null)
                _expenseIncreaseTextBox.Text = ViewModel.ExpenseIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
            if (_revenueTargetTextBox != null)
                _revenueTargetTextBox.Text = ViewModel.RevenueTargetPercentage.ToString("F1", CultureInfo.InvariantCulture);
            if (_projectionYearsTextBox != null)
                _projectionYearsTextBox.Text = ViewModel.ProjectionYears.ToString(CultureInfo.InvariantCulture);
            if (_metricsSearchTextBox != null)
                _metricsSearchTextBox.Text = ViewModel.MetricsSearchText;
            if (_variancesSearchTextBox != null)
                _variancesSearchTextBox.Text = ViewModel.VariancesSearchText;

            // Auto-load data using async/await (per Microsoft async pattern for WinForms)
            // This allows PropertyChanged events to be marshaled on UI thread automatically
            // Reference: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls-to-windows-forms-controls
            await ViewModel.RefreshCommand.ExecuteAsync(null);

            // Defer sizing validation to prevent "controls cut off" - Analytics has complex grid/chart layouts
            this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing panel");
        }
    }



    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events before disposal
            if (ViewModel != null)
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            // Use SafeDispose for Syncfusion controls
            _metricsGrid.SafeDispose();
            _variancesGrid.SafeDispose();

            // Region event wiring removed; ChartControl handles cleanup via Dispose

            _trendsChart.SafeDispose();
            _forecastChart.SafeDispose();
            _mainSplitContainer.SafeDispose();
            _statusStrip.SafeDispose();
            _panelHeader.SafeDispose();
            _loadingOverlay.SafeDispose();
            _noDataOverlay.SafeDispose();
            _errorProvider.SafeDispose();

            // Note: No components container used in programmatic UI
            // components?.Dispose();
        }

        base.Dispose(disposing);
    }



}
