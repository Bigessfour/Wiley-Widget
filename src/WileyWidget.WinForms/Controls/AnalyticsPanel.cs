using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChartControl = Syncfusion.Windows.Forms.Chart.ChartControl;
using ChartSeries = Syncfusion.Windows.Forms.Chart.ChartSeries;
using ChartSeriesType = Syncfusion.Windows.Forms.Chart.ChartSeriesType;
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
using WileyWidget.WinForms.Helpers;

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
    private GradientPanelExt? _scenarioPanel;
    private GradientPanelExt? _resultsPanel;
    private GradientPanelExt? _chartsPanel;
    private GradientPanelExt? _buttonPanel;
    private SplitContainer? _mainSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private ErrorProvider? _errorProvider;
    private SfComboBox? _entityComboBox;

    // === EVENT HANDLER STORAGE FOR CLEANUP ===
    private EventHandler? _performAnalysisButtonClickHandler;
    private EventHandler? _runScenarioButtonClickHandler;
    private EventHandler? _generateForecastButtonClickHandler;
    private EventHandler? _refreshButtonClickHandler;
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;
    private EventHandler? _entityComboBoxSelectedIndexChangedHandler;
    private EventHandler? _rateIncreaseTextBoxTextChangedHandler;
    private EventHandler? _expenseIncreaseTextBoxTextChangedHandler;
    private EventHandler? _revenueTargetTextBoxTextChangedHandler;
    private EventHandler? _projectionYearsTextBoxTextChangedHandler;
    private EventHandler? _metricsSearchTextBoxTextChangedHandler;
    private EventHandler? _variancesSearchTextBoxTextChangedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

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
        // Call the actual initialization method (theme applied inside InitializeControls)
        InitializeControls();
    }

    #region ICompletablePanel Implementation

    /// <summary>
    /// Loads the panel asynchronously and initializes analytics data.
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (IsLoaded) return;
        try
        {
            IsBusy = true;
            if (ViewModel != null && !DesignMode && ViewModel.PerformAnalysisCommand.CanExecute(null))
            {
                await ViewModel.PerformAnalysisCommand.ExecuteAsync(null);
            }
            _logger?.LogDebug("AnalyticsPanel loaded successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AnalyticsPanel load cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load AnalyticsPanel");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Saves the panel asynchronously. Analytics panel is read-only, so this is a no-op.
    /// </summary>
    public override async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            await Task.CompletedTask;
            _logger?.LogDebug("AnalyticsPanel save completed");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AnalyticsPanel save cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save AnalyticsPanel");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Validates the panel asynchronously. Ensures scenario parameters are valid and data is available.
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;

            // Clear existing error indicators before validation
            if (_errorProvider != null)
            {
                if (InvokeRequired)
                    Invoke(() => ClearErrorIndicators());
                else
                    ClearErrorIndicators();
            }

            var errors = new List<ValidationItem>();
            if (ViewModel == null)
            {
                errors.Add(new ValidationItem("ViewModel", "ViewModel not initialized", ValidationSeverity.Error));
            }
            else
            {
                // Validate rate increase (0-100)
                if (_rateIncreaseTextBox != null)
                {
                    if (!decimal.TryParse(_rateIncreaseTextBox.Text, out var rateValue) || rateValue < 0 || rateValue > 100)
                    {
                        var error = new ValidationItem("RateIncrease", "Rate increase must be 0-100", ValidationSeverity.Error, _rateIncreaseTextBox);
                        errors.Add(error);
                        _errorProvider?.SetError(_rateIncreaseTextBox, "Rate increase must be 0-100");
                    }
                }

                // Validate expense increase (0-100)
                if (_expenseIncreaseTextBox != null)
                {
                    if (!decimal.TryParse(_expenseIncreaseTextBox.Text, out var expenseValue) || expenseValue < 0 || expenseValue > 100)
                    {
                        var error = new ValidationItem("ExpenseIncrease", "Expense increase must be 0-100", ValidationSeverity.Error, _expenseIncreaseTextBox);
                        errors.Add(error);
                        _errorProvider?.SetError(_expenseIncreaseTextBox, "Expense increase must be 0-100");
                    }
                }

                // Validate revenue target (0-100)
                if (_revenueTargetTextBox != null)
                {
                    if (!decimal.TryParse(_revenueTargetTextBox.Text, out var revenueValue) || revenueValue < 0 || revenueValue > 100)
                    {
                        var error = new ValidationItem("RevenueTarget", "Revenue target must be 0-100", ValidationSeverity.Error, _revenueTargetTextBox);
                        errors.Add(error);
                        _errorProvider?.SetError(_revenueTargetTextBox, "Revenue target must be 0-100");
                    }
                }

                // Validate projection years (1-10)
                if (_projectionYearsTextBox != null)
                {
                    if (!int.TryParse(_projectionYearsTextBox.Text, out var yearsValue) || yearsValue < 1 || yearsValue > 10)
                    {
                        var error = new ValidationItem("ProjectionYears", "Projection years must be 1-10", ValidationSeverity.Error, _projectionYearsTextBox);
                        errors.Add(error);
                        _errorProvider?.SetError(_projectionYearsTextBox, "Projection years must be 1-10");
                    }
                }

                // Check data availability
                if (!ViewModel.FilteredMetrics.Any())
                    errors.Add(new ValidationItem("Data", "No analytics data available", ValidationSeverity.Warning));
            }
            await Task.CompletedTask;
            return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failed(errors.ToArray());
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AnalyticsPanel validation cancelled");
            return ValidationResult.Failed(new ValidationItem("Cancelled", "Validation was cancelled", ValidationSeverity.Info));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Validation error in AnalyticsPanel");
            return ValidationResult.Failed(new ValidationItem("Validation", ex.Message, ValidationSeverity.Error));
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Clears all error indicators from the error provider.
    /// </summary>
    private void ClearErrorIndicators()
    {
        if (_errorProvider == null) return;

        if (_rateIncreaseTextBox != null)
            _errorProvider.SetError(_rateIncreaseTextBox, "");
        if (_expenseIncreaseTextBox != null)
            _errorProvider.SetError(_expenseIncreaseTextBox, "");
        if (_revenueTargetTextBox != null)
            _errorProvider.SetError(_revenueTargetTextBox, "");
        if (_projectionYearsTextBox != null)
            _errorProvider.SetError(_projectionYearsTextBox, "");
    }

    /// <summary>
    /// Focuses the first validation error control.
    /// </summary>
    public override void FocusFirstError()
    {
        if (_scenarioPanel?.Visible == true)
            _scenarioPanel.Focus();
        else
            _rateIncreaseTextBox?.Focus();
    }

    #endregion

    /// <summary>
    /// Initializes all UI controls and sets up the layout.
    /// </summary>
    private void InitializeControls()
    {
        // Suspend layout during initialization to prevent flickering and layout thrashing
        this.SuspendLayout();

        // Apply Syncfusion theme via SfSkinManager (single source of truth)
        SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        // Set up form properties
        Text = "Budget Analytics";
        Size = new Size(1400, 900);
        // Enforce minimum panel size to prevent cramped layouts (DPI-aware)
        MinimumSize = new Size(
            (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1200.0f),
            (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(700.0f)
        );

        // Panel header with actions
        _panelHeader = new PanelHeader { Dock = DockStyle.Top, Title = "Budget Analytics & Forecasting" };
        _panelHeaderRefreshHandler = async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null);
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Main split container - proportional sizing per Syncfusion demos (Option B)
        // Ensures top controls panel ≥300px, bottom results panel ≥400px
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(300.0f)
        };
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(_mainSplitContainer, 300, 400, 300);

        // Top panel - Controls and scenario input
        InitializeTopPanel();

        // Bottom panel - Results and charts
        InitializeBottomPanel();

        // Status strip for operation feedback
        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true
        };
        _statusStrip.Items.Add(_statusLabel);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Running analytics...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        // No data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Message = "No analytics data available\r\nPerform budget operations to generate insights",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

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

        // Explicit overlay Z-order management: LoadingOverlay on bottom, NoDataOverlay on top
        // Per Syncfusion demos: explicit SendToBack/BringToFront more robust than reliance on add order
        _loadingOverlay?.SendToBack();
        _noDataOverlay?.BringToFront();

        // Resume layout after all controls added
        this.ResumeLayout(false);
        this.PerformLayout();
        this.Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    /// <summary>
    /// Initializes the top panel with buttons and scenario inputs.
    /// </summary>
    private void InitializeTopPanel()
    {
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        var topPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(topPanel, currentTheme);

        // Button panel - now with AutoScroll to handle overflow on small screens (Option C)
        _buttonPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            AutoScroll = true  // Enable horizontal scroll if buttons overflow at <900px width
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, currentTheme);

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            AutoSize = false,
            MinimumSize = new Size(800, 30)  // Prevent button table from collapsing
        };

        for (int i = 0; i < 7; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));  // Fixed 120px per button

        _performAnalysisButton = new SfButton
        {
            Text = "&Perform Analysis",
            TabIndex = 1,
            AccessibleName = "Perform Analysis",
            AccessibleDescription = "Run exploratory data analysis on budget data"
        };
        _performAnalysisButtonClickHandler = async (s, e) => { SetHasUnsavedChanges(true); await ViewModel!.PerformAnalysisCommand.ExecuteAsync(null); };
        _performAnalysisButton.Click += _performAnalysisButtonClickHandler;

        _runScenarioButton = new SfButton
        {
            Text = "&Run Scenario",
            TabIndex = 2,
            AccessibleName = "Run Scenario",
            AccessibleDescription = "Run rate adjustment scenario analysis"
        };
        _runScenarioButtonClickHandler = async (s, e) => { SetHasUnsavedChanges(true); await ViewModel!.RunScenarioCommand.ExecuteAsync(null); };
        _runScenarioButton.Click += _runScenarioButtonClickHandler;

        _generateForecastButton = new SfButton
        {
            Text = "&Generate Forecast",
            TabIndex = 3,
            AccessibleName = "Generate Forecast",
            AccessibleDescription = "Generate predictive reserve forecast"
        };
        _generateForecastButtonClickHandler = async (s, e) => { SetHasUnsavedChanges(true); await ViewModel!.GenerateForecastCommand.ExecuteAsync(null); };
        _generateForecastButton.Click += _generateForecastButtonClickHandler;

        _refreshButton = new SfButton
        {
            Text = "&Refresh",
            TabIndex = 4,
            AccessibleName = "Refresh",
            AccessibleDescription = "Refresh analytics data"
        };
        _refreshButtonClickHandler = async (s, e) => await _viewModel.RefreshCommand.ExecuteAsync(null);
        _refreshButton.Click += _refreshButtonClickHandler;

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
        _entityComboBoxSelectedIndexChangedHandler = EntityComboBox_SelectedIndexChanged;
        _entityComboBox.SelectedIndexChanged += _entityComboBoxSelectedIndexChangedHandler;

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
        SfSkinManager.SetVisualStyle(_scenarioPanel, currentTheme);

        var scenarioGroup = new GradientPanelExt
        {
            Text = "Scenario Parameters",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(scenarioGroup, currentTheme);

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
            AccessibleDescription = "Percentage increase for rate scenario (0-100)",
            MaxLength = 5
        };
#pragma warning restore RS0030
        _rateIncreaseTextBoxTextChangedHandler = RateIncreaseTextBox_TextChanged;
        _rateIncreaseTextBox.TextChanged += _rateIncreaseTextBoxTextChangedHandler;

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
            AccessibleDescription = "Percentage increase for expenses in scenario (0-100)",
            MaxLength = 5
        };
#pragma warning restore RS0030
        _expenseIncreaseTextBoxTextChangedHandler = ExpenseIncreaseTextBox_TextChanged;
        _expenseIncreaseTextBox.TextChanged += _expenseIncreaseTextBoxTextChangedHandler;

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
            AccessibleDescription = "Target revenue increase percentage (0-100)",
            MaxLength = 5
        };
#pragma warning restore RS0030
        _revenueTargetTextBoxTextChangedHandler = RevenueTargetTextBox_TextChanged;
        _revenueTargetTextBox.TextChanged += _revenueTargetTextBoxTextChangedHandler;

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
            AccessibleDescription = "Number of years for projections (1-10)",
            MaxLength = 2
        };
#pragma warning restore RS0030
        _projectionYearsTextBoxTextChangedHandler = ProjectionYearsTextBox_TextChanged;
        _projectionYearsTextBox.TextChanged += _projectionYearsTextBoxTextChangedHandler;

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
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        var bottomPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(bottomPanel, currentTheme);

        // Results panel with grids and insights
        _resultsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            MinimumSize = new Size(600, 300)
        };
        SfSkinManager.SetVisualStyle(_resultsPanel, currentTheme);

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
        SfSkinManager.SetVisualStyle(gridsPanel, currentTheme);

        var gridsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        // Enforce safe minimum sizes on grids split (Option B): metrics ≥350px, variances ≥200px
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(gridsSplit, 350, 200, 850);

        // Metrics grid
        var metricsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            MinimumSize = new Size(300, 150)
        };
        SfSkinManager.SetVisualStyle(metricsPanel, currentTheme);

        // Metrics search (deduplicated, always use TextBoxExt and GradientPanelExt)
        var metricsSearchPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 30,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(metricsSearchPanel, currentTheme);
        var metricsSearchLabel = new Label { Text = "Search Metrics:", Dock = DockStyle.Left, Width = 100 };
        _metricsSearchTextBox = new TextBoxExt { Dock = DockStyle.Fill, TabIndex = 9, MaxLength = 100 };
        _metricsSearchTextBoxTextChangedHandler = MetricsSearchTextBox_TextChanged;
        _metricsSearchTextBox.TextChanged += _metricsSearchTextBoxTextChangedHandler;
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
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            MinimumSize = new Size(300, 150)
        };
        SfSkinManager.SetVisualStyle(variancesPanel, currentTheme);

        // Variances search (deduplicated, always use TextBoxExt and GradientPanelExt)
        var variancesSearchPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 30,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(variancesSearchPanel, currentTheme);
        var variancesSearchLabel = new Label { Text = "Search Variances:", Dock = DockStyle.Left, Width = 120 };
        _variancesSearchTextBox = new TextBoxExt { Dock = DockStyle.Fill, TabIndex = 11, MaxLength = 100 };
        _variancesSearchTextBoxTextChangedHandler = VariancesSearchTextBox_TextChanged;
        _variancesSearchTextBox.TextChanged += _variancesSearchTextBoxTextChangedHandler;
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
        SfSkinManager.SetVisualStyle(insightsPanel, currentTheme);

        var insightsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        // Enforce safe minimum sizes on insights split (Option B): insights ≥150px, recommendations ≥150px
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(insightsSplit, 150, 150, 300);

        // Insights list
        var insightsGroup = new GradientPanelExt
        {
            Text = "Key Insights",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(insightsGroup, currentTheme);

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
        SfSkinManager.SetVisualStyle(recommendationsGroup, currentTheme);

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

        // Summary panel - increased padding with label truncation (Option A: AutoEllipsis)
        var summaryPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(15),  // Increased from 10px
            MinimumSize = new Size(500, 60)
        };

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1
        };

        for (int i = 0; i < 5; i++)
            summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        // Create shared tooltip for truncated labels
        var summaryTooltip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 500,
            ReshowDelay = 100
        };

        // Labels with AutoEllipsis for clean truncation + tooltip for full text (Option A)
        _totalBudgetedLabel = new Label
        {
            Text = "Total Budgeted: $0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        summaryTooltip.SetToolTip(_totalBudgetedLabel, "Total Budgeted Amount");

        _totalActualLabel = new Label
        {
            Text = "Total Actual: $0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        summaryTooltip.SetToolTip(_totalActualLabel, "Total Actual Amount");

        _totalVarianceLabel = new Label
        {
            Text = "Total Variance: $0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        summaryTooltip.SetToolTip(_totalVarianceLabel, "Total Variance");

        _averageVarianceLabel = new Label
        {
            Text = "Avg Variance: 0.00%",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        summaryTooltip.SetToolTip(_averageVarianceLabel, "Average Variance Percentage");

        _recommendationExplanationLabel = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        summaryTooltip.SetToolTip(_recommendationExplanationLabel, "Recommendation Details");

        summaryTable.Controls.Add(_totalBudgetedLabel, 0, 0);
        summaryTable.Controls.Add(_totalActualLabel, 1, 0);
        summaryTable.Controls.Add(_totalVarianceLabel, 2, 0);
        summaryTable.Controls.Add(_averageVarianceLabel, 3, 0);
        summaryTable.Controls.Add(_recommendationExplanationLabel, 4, 0);

        summaryPanel.Controls.Add(summaryTable);
        bottomPanel.Controls.Add(summaryPanel);

        // Charts panel - increased padding and explicit MinimumSize
        _chartsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Bottom,
            Height = 300,
            Padding = new Padding(12),  // Increased from 10px
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            MinimumSize = new Size(600, 250)
        };
        SfSkinManager.SetVisualStyle(_chartsPanel, currentTheme);

        var chartsSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        // Enforce safe minimum sizes on chart split (Option B): trends ≥300px, forecast ≥300px
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(chartsSplit, 300, 300, 400);

        // Trends chart
        var trendsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(trendsPanel, currentTheme);

        _trendsChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 14,
            AccessibleName = "Trends Chart"
        };

        ChartControlDefaults.Apply(_trendsChart!, logger: _logger);

        _trendsChart.Title.Text = "Monthly Budget Trends";
        _trendsChart.Title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _trendsChart.Legend.Visible = true;
        _trendsChart.Legend.Position = Syncfusion.Windows.Forms.Chart.ChartDock.Bottom;  // Explicit legend positioning per Syncfusion standards
        _trendsChart.ShowToolTips = true;  // Enable tooltips for data point inspection

        trendsPanel.Controls.Add(_trendsChart);
        chartsSplit.Panel1.Controls.Add(trendsPanel);

        // Forecast chart
        var forecastPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(forecastPanel, currentTheme);

        _forecastChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 16,
            AccessibleName = "Forecast Chart"
        };

        ChartControlDefaults.Apply(_forecastChart!, logger: _logger);

        _forecastChart.Title.Text = "Reserve Forecast";
        _forecastChart.Title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _forecastChart.Legend.Visible = true;
        _forecastChart.Legend.Position = Syncfusion.Windows.Forms.Chart.ChartDock.Bottom;  // Explicit legend positioning per Syncfusion standards
        _forecastChart.ShowToolTips = true;  // Enable tooltips for data point inspection

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
    /// Called when the ViewModel is resolved from the scoped provider so panels can bind to it safely.
    /// </summary>
    protected override void OnViewModelResolved(AnalyticsViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);

        // Subscribe to property changed for UI updates and live preview (store handler for cleanup)
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Initial binding for grids and lists
        if (_metricsGrid != null) _metricsGrid.DataSource = viewModel.FilteredMetrics;
        if (_variancesGrid != null) _variancesGrid.DataSource = viewModel.FilteredTopVariances;
        if (_insightsListBox != null) _insightsListBox.DataSource = viewModel.Insights ?? new System.Collections.ObjectModel.ObservableCollection<string>();
        if (_recommendationsListBox != null) _recommendationsListBox.DataSource = viewModel.Recommendations ?? new System.Collections.ObjectModel.ObservableCollection<string>();

        // Two-way binding for scenario parameters (live preview)
        // TextBox -> ViewModel: handled by TextChanged handlers
        // ViewModel -> TextBox: handled by PropertyChanged handler below
        if (_rateIncreaseTextBox != null)
            _rateIncreaseTextBox.Text = viewModel.RateIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
        if (_expenseIncreaseTextBox != null)
            _expenseIncreaseTextBox.Text = viewModel.ExpenseIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
        if (_revenueTargetTextBox != null)
            _revenueTargetTextBox.Text = viewModel.RevenueTargetPercentage.ToString("F1", CultureInfo.InvariantCulture);
        if (_projectionYearsTextBox != null)
            _projectionYearsTextBox.Text = viewModel.ProjectionYears.ToString(CultureInfo.InvariantCulture);

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
    /// Validates and updates scenario parameter with error feedback.
    /// </summary>
    private void ValidateScenarioParameter(TextBoxExt textBox, Action<decimal> setter, decimal min, decimal max, string fieldName)
    {
        if (decimal.TryParse(textBox.Text, out var value) && value >= min && value <= max)
        {
            setter(value);
            _errorProvider?.SetError(textBox, "");
        }
        else
        {
            _errorProvider?.SetError(textBox, $"{fieldName} must be {min}-{max}");
        }
    }

    /// <summary>
    /// Validates and updates scenario parameter (integer) with error feedback.
    /// </summary>
    private void ValidateScenarioParameterInt(TextBoxExt textBox, Action<int> setter, int min, int max, string fieldName)
    {
        if (int.TryParse(textBox.Text, out var value) && value >= min && value <= max)
        {
            setter(value);
            _errorProvider?.SetError(textBox, "");
        }
        else
        {
            _errorProvider?.SetError(textBox, $"{fieldName} must be {min}-{max}");
        }
    }

    /// <summary>
    /// Handles rate increase text box text changed event with error feedback.
    /// </summary>
    private void RateIncreaseTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_rateIncreaseTextBox == null) return;
        SetHasUnsavedChanges(true);
        ValidateScenarioParameter(_rateIncreaseTextBox, v => _viewModel.RateIncreasePercentage = v, 0, 100, "Rate increase");
    }

    /// <summary>
    /// Handles expense increase text box text changed event with error feedback.
    /// </summary>
    private void ExpenseIncreaseTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_expenseIncreaseTextBox == null) return;
        SetHasUnsavedChanges(true);
        ValidateScenarioParameter(_expenseIncreaseTextBox, v => _viewModel.ExpenseIncreasePercentage = v, 0, 100, "Expense increase");
    }

    /// <summary>
    /// Handles revenue target text box text changed event with error feedback.
    /// </summary>
    private void RevenueTargetTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_revenueTargetTextBox == null) return;
        SetHasUnsavedChanges(true);
        ValidateScenarioParameter(_revenueTargetTextBox, v => _viewModel.RevenueTargetPercentage = v, 0, 100, "Revenue target");
    }

    /// <summary>
    /// Handles projection years text box text changed event with error feedback.
    /// </summary>
    private void ProjectionYearsTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_projectionYearsTextBox == null) return;
        SetHasUnsavedChanges(true);
        ValidateScenarioParameterInt(_projectionYearsTextBox, v => _viewModel.ProjectionYears = v, 1, 10, "Projection years");
    }

    /// <summary>
    /// Handles metrics search text box text changed event.
    /// </summary>
    private void MetricsSearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        SetHasUnsavedChanges(true);
        _viewModel.MetricsSearchText = _metricsSearchTextBox?.Text ?? string.Empty;
    }

    /// <summary>
    /// Handles variances search text box text changed event.
    /// </summary>
    private void VariancesSearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        SetHasUnsavedChanges(true);
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
            case nameof(_viewModel.RateIncreasePercentage):
                // Live preview: update TextBox when ViewModel property changes
                if (_rateIncreaseTextBox != null && _rateIncreaseTextBox.Focused == false)
                    _rateIncreaseTextBox.Text = _viewModel.RateIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
                break;

            case nameof(_viewModel.ExpenseIncreasePercentage):
                // Live preview: update TextBox when ViewModel property changes
                if (_expenseIncreaseTextBox != null && _expenseIncreaseTextBox.Focused == false)
                    _expenseIncreaseTextBox.Text = _viewModel.ExpenseIncreasePercentage.ToString("F1", CultureInfo.InvariantCulture);
                break;

            case nameof(_viewModel.RevenueTargetPercentage):
                // Live preview: update TextBox when ViewModel property changes
                if (_revenueTargetTextBox != null && _revenueTargetTextBox.Focused == false)
                    _revenueTargetTextBox.Text = _viewModel.RevenueTargetPercentage.ToString("F1", CultureInfo.InvariantCulture);
                break;

            case nameof(_viewModel.ProjectionYears):
                // Live preview: update TextBox when ViewModel property changes
                if (_projectionYearsTextBox != null && _projectionYearsTextBox.Focused == false)
                    _projectionYearsTextBox.Text = _viewModel.ProjectionYears.ToString(CultureInfo.InvariantCulture);
                break;

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
                if (_loadingOverlay != null && !_loadingOverlay.IsDisposed)
                    _loadingOverlay.SafeInvoke(() => _loadingOverlay.Visible = ViewModel.IsLoading);

                if (_noDataOverlay != null && !_noDataOverlay.IsDisposed)
                    _noDataOverlay.SafeInvoke(() => _noDataOverlay.Visible = !ViewModel.IsLoading && (ViewModel.Metrics == null || ViewModel.Metrics.Count == 0));
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

        if (_trendsChart.InvokeRequired)
        {
            _trendsChart.SafeInvoke(() => UpdateTrendsChart());
            return;
        }

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

        if (_forecastChart.InvokeRequired)
        {
            _forecastChart.SafeInvoke(() => UpdateForecastChart());
            return;
        }

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
    protected override void OnLoad(EventArgs e)
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

            // Note: Data loading is now handled by ILazyLoadViewModel via DockingManager events

            // Defer sizing validation to prevent "controls cut off" - Analytics has complex grid/chart layouts
            DeferSizeValidation();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing panel");
        }
    }

    private void DeferSizeValidation()
    {
        if (IsDisposed) return;

        if (IsHandleCreated)
        {
            try { BeginInvoke(new global::System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _))); }
            catch { }
            return;
        }

        EventHandler? handleCreatedHandler = null;
        handleCreatedHandler = (s, e) =>
        {
            HandleCreated -= handleCreatedHandler;
            if (IsDisposed) return;

            try { BeginInvoke(new global::System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _))); }
            catch { }
        };

        HandleCreated += handleCreatedHandler;
    }



    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from all events before disposal to prevent memory leaks
            if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;

            // Button event handlers
            if (_performAnalysisButton != null && _performAnalysisButtonClickHandler != null)
                _performAnalysisButton.Click -= _performAnalysisButtonClickHandler;
            if (_runScenarioButton != null && _runScenarioButtonClickHandler != null)
                _runScenarioButton.Click -= _runScenarioButtonClickHandler;
            if (_generateForecastButton != null && _generateForecastButtonClickHandler != null)
                _generateForecastButton.Click -= _generateForecastButtonClickHandler;
            if (_refreshButton != null && _refreshButtonClickHandler != null)
                _refreshButton.Click -= _refreshButtonClickHandler;

            // Panel header event handlers
            if (_panelHeader != null)
            {
                if (_panelHeaderRefreshHandler != null)
                    _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                if (_panelHeaderCloseHandler != null)
                    _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
            }

            // Entity combo box event handler
            if (_entityComboBox != null && _entityComboBoxSelectedIndexChangedHandler != null)
                _entityComboBox.SelectedIndexChanged -= _entityComboBoxSelectedIndexChangedHandler;

            // TextBox event handlers
            if (_rateIncreaseTextBox != null && _rateIncreaseTextBoxTextChangedHandler != null)
                _rateIncreaseTextBox.TextChanged -= _rateIncreaseTextBoxTextChangedHandler;
            if (_expenseIncreaseTextBox != null && _expenseIncreaseTextBoxTextChangedHandler != null)
                _expenseIncreaseTextBox.TextChanged -= _expenseIncreaseTextBoxTextChangedHandler;
            if (_revenueTargetTextBox != null && _revenueTargetTextBoxTextChangedHandler != null)
                _revenueTargetTextBox.TextChanged -= _revenueTargetTextBoxTextChangedHandler;
            if (_projectionYearsTextBox != null && _projectionYearsTextBoxTextChangedHandler != null)
                _projectionYearsTextBox.TextChanged -= _projectionYearsTextBoxTextChangedHandler;
            if (_metricsSearchTextBox != null && _metricsSearchTextBoxTextChangedHandler != null)
                _metricsSearchTextBox.TextChanged -= _metricsSearchTextBoxTextChangedHandler;
            if (_variancesSearchTextBox != null && _variancesSearchTextBoxTextChangedHandler != null)
                _variancesSearchTextBox.TextChanged -= _variancesSearchTextBoxTextChangedHandler;

            // Use SafeDispose for Syncfusion controls
            _metricsGrid.SafeDispose();
            _variancesGrid.SafeDispose();

            // Region event wiring removed; ChartControl handles cleanup via Dispose
            _trendsChart.SafeDispose();
            _forecastChart.SafeDispose();
            _insightsListBox.SafeClearDataSource();
            _insightsListBox.SafeDispose();
            _recommendationsListBox.SafeClearDataSource();
            _recommendationsListBox.SafeDispose();
            _mainSplitContainer.SafeDispose();
            _statusStrip.SafeDispose();
            _panelHeader.SafeDispose();
            _entityComboBox.SafeDispose();
            _loadingOverlay.SafeDispose();
            _noDataOverlay.SafeDispose();
            _errorProvider.SafeDispose();

            // Note: No components container used in programmatic UI
            // components?.Dispose();
        }

        base.Dispose(disposing);
    }



}
