using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for analytics and scenario modeling with budget analysis, forecasting, and recommendations.
/// Features exploratory analysis, rate scenarios, trend visualization, and predictive modeling.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class AnalyticsPanel : UserControl
{
    private readonly AnalyticsViewModel _viewModel;
    private readonly ILogger<AnalyticsPanel> _logger;

    // UI Controls
    private SfDataGrid? _metricsGrid;
    private SfDataGrid? _variancesGrid;
    private ChartControl? _trendsChart;
    private ChartControl? _forecastChart;
    private Button? _performAnalysisButton;
    private Button? _runScenarioButton;
    private Button? _generateForecastButton;
    private Button? _refreshButton;
    private TextBox? _rateIncreaseTextBox;
    private TextBox? _expenseIncreaseTextBox;
    private TextBox? _revenueTargetTextBox;
    private TextBox? _projectionYearsTextBox;
    private TextBox? _metricsSearchTextBox;
    private TextBox? _variancesSearchTextBox;
    private ListBox? _insightsListBox;
    private ListBox? _recommendationsListBox;
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

    /// <summary>
    /// Initializes a new instance of the AnalyticsPanel class.
    /// </summary>
    /// <param name="viewModel">The analytics view model.</param>
    /// <param name="logger">The logger instance.</param>
    public AnalyticsPanel(
        AnalyticsViewModel viewModel,
        ILogger<AnalyticsPanel> logger)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300
        };

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
        _noDataOverlay = new NoDataOverlay { Message = "No analytics data available" };
        Controls.Add(_noDataOverlay);

        Controls.Add(_mainSplitContainer);
        Controls.Add(_statusStrip);

        // Wire up ViewModel events
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Set tab order
        SetTabOrder();
    }

    /// <summary>
    /// Initializes the top panel with buttons and scenario inputs.
    /// </summary>
    private void InitializeTopPanel()
    {
        var topPanel = new Panel { Dock = DockStyle.Fill };

        // Button panel
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10)
        };

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };

        for (int i = 0; i < 4; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _performAnalysisButton = new Button
        {
            Text = "&Perform Analysis",
            TabIndex = 1,
            AccessibleName = "Perform Analysis",
            AccessibleDescription = "Run exploratory data analysis on budget data"
        };
        _performAnalysisButton.Click += async (s, e) => await _viewModel.PerformAnalysisCommand.ExecuteAsync(null);

        _runScenarioButton = new Button
        {
            Text = "&Run Scenario",
            TabIndex = 2,
            AccessibleName = "Run Scenario",
            AccessibleDescription = "Run rate adjustment scenario analysis"
        };
        _runScenarioButton.Click += async (s, e) => await _viewModel.RunScenarioCommand.ExecuteAsync(null);

        _generateForecastButton = new Button
        {
            Text = "&Generate Forecast",
            TabIndex = 3,
            AccessibleName = "Generate Forecast",
            AccessibleDescription = "Generate predictive reserve forecast"
        };
        _generateForecastButton.Click += async (s, e) => await _viewModel.GenerateForecastCommand.ExecuteAsync(null);

        _refreshButton = new Button
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

        _buttonPanel.Controls.Add(buttonTable);
        topPanel.Controls.Add(_buttonPanel);

        // Scenario input panel
        _scenarioPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var scenarioGroup = new GroupBox
        {
            Text = "Scenario Parameters",
            Dock = DockStyle.Fill
        };

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

        _rateIncreaseTextBox = new TextBox
        {
            Text = "5.0",
            Dock = DockStyle.Fill,
            TabIndex = 5,
            AccessibleName = "Rate Increase Percentage",
            AccessibleDescription = "Percentage increase for rate scenario"
        };
        _rateIncreaseTextBox.TextChanged += RateIncreaseTextBox_TextChanged;

        // Row 2: Expense Increase
        var expenseIncreaseLabel = new Label
        {
            Text = "Expense Increase (%):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _expenseIncreaseTextBox = new TextBox
        {
            Text = "3.0",
            Dock = DockStyle.Fill,
            TabIndex = 6,
            AccessibleName = "Expense Increase Percentage",
            AccessibleDescription = "Percentage increase for expenses in scenario"
        };
        _expenseIncreaseTextBox.TextChanged += ExpenseIncreaseTextBox_TextChanged;

        // Row 3: Revenue Target
        var revenueTargetLabel = new Label
        {
            Text = "Revenue Target (%):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _revenueTargetTextBox = new TextBox
        {
            Text = "10.0",
            Dock = DockStyle.Fill,
            TabIndex = 7,
            AccessibleName = "Revenue Target Percentage",
            AccessibleDescription = "Target revenue increase percentage"
        };
        _revenueTargetTextBox.TextChanged += RevenueTargetTextBox_TextChanged;

        // Row 4: Projection Years
        var projectionYearsLabel = new Label
        {
            Text = "Projection Years:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _projectionYearsTextBox = new TextBox
        {
            Text = "3",
            Dock = DockStyle.Fill,
            TabIndex = 8,
            AccessibleName = "Projection Years",
            AccessibleDescription = "Number of years for projections"
        };
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
        var bottomPanel = new Panel { Dock = DockStyle.Fill };

        // Results panel with grids and insights
        _resultsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var resultsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 200
        };

        // Top results - Grids
        var gridsPanel = new Panel { Dock = DockStyle.Fill };

        var gridsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 150
        };

        // Metrics grid
        var metricsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        // Metrics search
        var metricsSearchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30
        };
        var metricsSearchLabel = new Label { Text = "Search Metrics:", Dock = DockStyle.Left, Width = 100 };
        _metricsSearchTextBox = new TextBox { Dock = DockStyle.Fill, TabIndex = 9 };
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
        var variancesPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        // Variances search
        var variancesSearchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30
        };
        var variancesSearchLabel = new Label { Text = "Search Variances:", Dock = DockStyle.Left, Width = 120 };
        _variancesSearchTextBox = new TextBox { Dock = DockStyle.Fill, TabIndex = 11 };
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
        var insightsPanel = new Panel { Dock = DockStyle.Fill };

        var insightsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 150
        };

        // Insights list
        var insightsGroup = new GroupBox
        {
            Text = "Key Insights",
            Dock = DockStyle.Fill
        };

        _insightsListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 13,
            AccessibleName = "Key Insights List",
            AccessibleDescription = "List of key insights from analysis"
        };

        insightsGroup.Controls.Add(_insightsListBox);
        insightsSplit.Panel1.Controls.Add(insightsGroup);

        // Recommendations list
        var recommendationsGroup = new GroupBox
        {
            Text = "Recommendations",
            Dock = DockStyle.Fill
        };

        _recommendationsListBox = new ListBox
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
        _chartsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 300,
            Padding = new Padding(10)
        };

        var chartsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 400
        };

        // Trends chart
        var trendsPanel = new Panel { Dock = DockStyle.Fill };

        _trendsChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 15,
            AccessibleName = "Trends Chart"
        };

        _trendsChart.Title.Text = "Monthly Budget Trends";
        _trendsChart.Title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _trendsChart.Legend.Visible = true;

        trendsPanel.Controls.Add(_trendsChart);
        chartsSplit.Panel1.Controls.Add(trendsPanel);

        // Forecast chart
        var forecastPanel = new Panel { Dock = DockStyle.Fill };

        _forecastChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 16,
            AccessibleName = "Forecast Chart"
        };

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

    /// <summary>
    /// Handles view model property changed event.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.FilteredMetrics):
                if (_metricsGrid != null) _metricsGrid.DataSource = _viewModel.FilteredMetrics;
                break;

            case nameof(_viewModel.FilteredTopVariances):
                if (_variancesGrid != null) _variancesGrid.DataSource = _viewModel.FilteredTopVariances;
                break;

            case nameof(_viewModel.TrendData):
                UpdateTrendsChart();
                break;

            case nameof(_viewModel.Insights):
                if (_insightsListBox != null)
                {
                    _insightsListBox.Items.Clear();
                    foreach (var insight in _viewModel.Insights)
                        _insightsListBox.Items.Add(insight);
                }
                break;

            case nameof(_viewModel.Recommendations):
                if (_recommendationsListBox != null)
                {
                    _recommendationsListBox.Items.Clear();
                    foreach (var rec in _viewModel.Recommendations)
                        _recommendationsListBox.Items.Add(rec);
                }
                break;

            case nameof(_viewModel.ForecastData):
                UpdateForecastChart();
                break;

            case nameof(_viewModel.IsLoading):
                if (_loadingOverlay != null) _loadingOverlay.Visible = _viewModel.IsLoading;
                if (_noDataOverlay != null) _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.Metrics.Any();
                break;

            case nameof(_viewModel.StatusText):
                if (_statusLabel != null) _statusLabel.Text = _viewModel.StatusText;
                break;

            case nameof(_viewModel.TotalBudgetedAmount):
                if (_totalBudgetedLabel != null) _totalBudgetedLabel.Text = $"Total Budgeted: {_viewModel.TotalBudgetedAmount:C}";
                break;

            case nameof(_viewModel.TotalActualAmount):
                if (_totalActualLabel != null) _totalActualLabel.Text = $"Total Actual: {_viewModel.TotalActualAmount:C}";
                break;

            case nameof(_viewModel.TotalVarianceAmount):
                if (_totalVarianceLabel != null) _totalVarianceLabel.Text = $"Total Variance: {_viewModel.TotalVarianceAmount:C}";
                break;

            case nameof(_viewModel.AverageVariancePercentage):
                if (_averageVarianceLabel != null) _averageVarianceLabel.Text = $"Avg Variance: {_viewModel.AverageVariancePercentage:P}";
                break;

            case nameof(_viewModel.RecommendationExplanation):
                if (_recommendationExplanationLabel != null) _recommendationExplanationLabel.Text = _viewModel.RecommendationExplanation;
                break;
        }
    }

    /// <summary>
    /// Updates the trends chart with current data.
    /// </summary>
    private void UpdateTrendsChart()
    {
        if (_trendsChart == null || !_viewModel.TrendData.Any())
            return;

        try
        {
            _trendsChart.Series.Clear();

            var budgetedSeries = new ChartSeries("Budgeted", ChartSeriesType.Line);
            var actualSeries = new ChartSeries("Actual", ChartSeriesType.Line);

            budgetedSeries.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(54, 162, 235)); // Blue
            actualSeries.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(255, 99, 132)); // Red

            foreach (var trend in _viewModel.TrendData)
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
            _logger.LogError(ex, "Error updating trends chart");
        }
    }

    /// <summary>
    /// Updates the forecast chart with current data.
    /// </summary>
    private void UpdateForecastChart()
    {
        if (_forecastChart == null || !_viewModel.ForecastData.Any())
            return;

        try
        {
            _forecastChart.Series.Clear();

            var forecastSeries = new ChartSeries("Predicted Reserves", ChartSeriesType.Line);
            forecastSeries.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(75, 192, 192)); // Green

            foreach (var point in _viewModel.ForecastData)
            {
                forecastSeries.Points.Add(point.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture), (double)point.PredictedReserves);
            }

            _forecastChart.Series.Add(forecastSeries);
            _forecastChart.Refresh();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating forecast chart");
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
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            // Initialize scenario parameters
            _rateIncreaseTextBox!.Text = _viewModel.RateIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
            _expenseIncreaseTextBox!.Text = _viewModel.ExpenseIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
            _revenueTargetTextBox!.Text = _viewModel.RevenueTargetPercentage.ToString("F1", CultureInfo.InvariantCulture);
            _projectionYearsTextBox!.Text = _viewModel.ProjectionYears.ToString(CultureInfo.InvariantCulture);
            _metricsSearchTextBox!.Text = _viewModel.MetricsSearchText;
            _variancesSearchTextBox!.Text = _viewModel.VariancesSearchText;

            // Auto-load data
            _ = Task.Run(async () => await _viewModel.RefreshCommand.ExecuteAsync(null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing panel");
        }
    }

    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer? components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel?.Dispose();

            _metricsGrid?.Dispose();
            _variancesGrid?.Dispose();
            _trendsChart?.Dispose();
            _forecastChart?.Dispose();
            _mainSplitContainer?.Dispose();
            _statusStrip?.Dispose();
            _panelHeader?.Dispose();
            _loadingOverlay?.Dispose();
            _noDataOverlay?.Dispose();

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.Name = "AnalyticsPanel";
        this.AccessibleName = "Budget Analytics"; // For UI automation
        this.Size = new Size(1400, 900);
    }

    #endregion
}
