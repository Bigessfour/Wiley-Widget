using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using System.ComponentModel;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for viewing and managing recommended monthly charges per department.
/// Features AI-driven recommendations, state benchmarking, and profitability analysis.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class RecommendedMonthlyChargePanel : UserControl
{
    private readonly RecommendedMonthlyChargeViewModel _viewModel;
    private readonly ILogger<RecommendedMonthlyChargePanel> _logger;

    // UI Controls
    private SfDataGrid? _departmentsGrid;
    private SfDataGrid? _benchmarksGrid;
    private ChartControl? _chartControl;
    private ChartControlRegionEventWiring? _chartRegionEventWiring;
    private SfButton? _refreshButton;
    private SfButton? _saveButton;
    private SfButton? _queryGrokButton;
    private Label? _totalRevenueLabel;
    private Label? _totalExpensesLabel;
    private Label? _suggestedRevenueLabel;
    private Label? _overallStatusLabel;
    private TextBoxExt? _explanationTextBox;
    private GradientPanelExt? _summaryPanel;
    private GradientPanelExt? _chartPanel;
    private GradientPanelExt? _buttonPanel;
    private SplitContainer? _mainSplitContainer;
    private SplitContainer? _leftSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    public RecommendedMonthlyChargePanel(
        RecommendedMonthlyChargeViewModel viewModel,
        ILogger<RecommendedMonthlyChargePanel> logger)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeControls();
        BindViewModel();

        _logger.LogDebug("RecommendedMonthlyChargePanel initialized");
    }

    /// <summary>
    /// Parameterless constructor for design-time support
    /// </summary>
    public RecommendedMonthlyChargePanel()
    {
        InitializeComponent();
        _viewModel = new RecommendedMonthlyChargeViewModel();
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RecommendedMonthlyChargePanel>.Instance;

        InitializeControls();
    }

    private void InitializeControls()
    {
        Name = "RecommendedMonthlyChargePanel";
        Size = new Size(1400, 900);
        Dock = DockStyle.Fill;

        // ============================================================================
        // Panel Header
        // ============================================================================
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Recommended Monthly Charges",
            Height = 50
        };
        _panelHeader.RefreshClicked += async (s, e) => await RefreshDataAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // ============================================================================
        // Button Panel - Top Action Buttons
        // ============================================================================
        _buttonPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, "Office2019Colorful");

        _refreshButton = new SfButton
        {
            Text = "&Refresh Data",
            Location = new Point(10, 15),
            Size = new Size(130, 35),
            TabIndex = 1,
            AccessibleName = "Refresh Data",
            AccessibleDescription = "Refresh department expense data from QuickBooks"
        };
        _refreshButton.Click += async (s, e) => await RefreshDataAsync();
        var refreshTooltip = new ToolTip();
        refreshTooltip.SetToolTip(_refreshButton, "Load latest expense data from QuickBooks (Alt+R)");

        _queryGrokButton = new SfButton
        {
            Text = "Query &AI",
            Location = new Point(150, 15),
            Size = new Size(130, 35),
            TabIndex = 2,
            AccessibleName = "Query AI",
            AccessibleDescription = "Get AI-driven rate recommendations from Grok"
        };
        _queryGrokButton.Click += async (s, e) => await QueryGrokAsync();
        var grokTooltip = new ToolTip();
        grokTooltip.SetToolTip(_queryGrokButton, "Query Grok AI for recommended adjustment factors (Alt+A)");

        _saveButton = new SfButton
        {
            Text = "&Save Changes",
            Location = new Point(290, 15),
            Size = new Size(130, 35),
            TabIndex = 3,
            AccessibleName = "Save Changes",
            AccessibleDescription = "Save current charge modifications to database"
        };
        _saveButton.Click += async (s, e) => await SaveChangesAsync();
        var saveTooltip = new ToolTip();
        saveTooltip.SetToolTip(_saveButton, "Save modified charges to database (Alt+S)");

        _buttonPanel.Controls.AddRange(new Control[] { _refreshButton, _queryGrokButton, _saveButton });
        Controls.Add(_buttonPanel);

        // ============================================================================
        // Summary Panel - Revenue, Expenses, Status Display
        // ============================================================================
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 180,
            Padding = new Padding(15),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, "Office2019Colorful");

        var summaryTitleLabel = new Label
        {
            Text = "Financial Summary",
            Location = new Point(15, 10),
            Size = new Size(200, 25),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            // ForeColor removed - let SkinManager handle theming
        };
        _summaryPanel.Controls.Add(summaryTitleLabel);

        _totalRevenueLabel = new Label
        {
            Text = "Current Revenue: $0.00",
            Location = new Point(15, 40),
            Size = new Size(350, 25),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            // ForeColor removed - let SkinManager handle theming
            TabIndex = 4,
            AccessibleName = "Total Current Revenue",
            AccessibleDescription = "Total monthly revenue from all departments at current rates"
        };

        _suggestedRevenueLabel = new Label
        {
            Text = "Suggested Revenue: $0.00",
            Location = new Point(15, 65),
            Size = new Size(350, 25),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            // ForeColor removed - let SkinManager handle theming
            TabIndex = 5,
            AccessibleName = "Total Suggested Revenue",
            AccessibleDescription = "Total monthly revenue at AI-suggested rates"
        };

        _totalExpensesLabel = new Label
        {
            Text = "Total Expenses: $0.00",
            Location = new Point(15, 90),
            Size = new Size(350, 25),
            Font = new Font("Segoe UI", 10F),
            // ForeColor removed - let SkinManager handle theming
            TabIndex = 6,
            AccessibleName = "Total Monthly Expenses",
            AccessibleDescription = "Total monthly expenses across all departments"
        };

        _overallStatusLabel = new Label
        {
            Text = "Status: Unknown",
            Location = new Point(400, 40),
            Size = new Size(300, 30),
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            TabIndex = 7,
            AccessibleName = "Overall Profitability Status",
            AccessibleDescription = "Overall profitability status: Losing Money, Breaking Even, or Profitable"
        };

        _explanationTextBox = new TextBoxExt
        {
            Multiline = true,
            ReadOnly = true,
            Location = new Point(15, 120),
            Size = new Size(1200, 45),
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            TabIndex = 8,
            Font = new Font("Segoe UI", 9F),
            AccessibleName = "AI Recommendation Explanation",
            AccessibleDescription = "AI-generated explanation of the recommended rate adjustments"
        };
        var explanationTooltip = new ToolTip();
        explanationTooltip.SetToolTip(_explanationTextBox, "AI-generated explanation for the recommended adjustments");

        _summaryPanel.Controls.AddRange(new Control[]
        {
            _totalRevenueLabel, _suggestedRevenueLabel, _totalExpensesLabel,
            _overallStatusLabel, _explanationTextBox
        });
        Controls.Add(_summaryPanel);

        // ============================================================================
        // Main Split Container - Left (Grids) | Right (Chart)
        // ============================================================================
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 700,
            BorderStyle = BorderStyle.FixedSingle
        };

        // ============================================================================
        // Left Split Container - Top (Departments) | Bottom (Benchmarks)
        // ============================================================================
        _leftSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350,
            BorderStyle = BorderStyle.None
        };

        // ============================================================================
        // Departments Grid (Top Left)
        // ============================================================================
        var deptGridPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(deptGridPanel, "Office2019Colorful");

        var deptGridLabel = new Label
        {
            Text = "Department Rate Analysis",
            Dock = DockStyle.Top,
            Height = 25,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Padding = new Padding(5, 3, 0, 0)
        };
        deptGridPanel.Controls.Add(deptGridLabel);

        _departmentsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = true,
            AllowResizingColumns = true,
            AllowSorting = true,
            AllowFiltering = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            EditMode = EditMode.SingleClick,
            TabIndex = 9,
            AccessibleName = "Department Rates Grid",
            AccessibleDescription = "Editable grid showing monthly charges, expenses, and recommendations per department"
        };

        // Configure department grid columns
        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Department",
            HeaderText = "Department",
            Width = 120,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CustomerCount",
            HeaderText = "Customers",
            Width = 90,
            AllowEditing = false,
            Format = "N0"
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyExpenses",
            HeaderText = "Monthly Expenses",
            Format = "C2",
            Width = 140,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CurrentCharge",
            HeaderText = "Current Charge",
            Format = "C2",
            Width = 120,
            AllowEditing = true
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "SuggestedCharge",
            HeaderText = "Suggested",
            Format = "C2",
            Width = 110,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyGainLoss",
            HeaderText = "Gain/Loss",
            Format = "C2",
            Width = 120,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PositionStatus",
            HeaderText = "Status",
            Width = 110,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "StateAverage",
            HeaderText = "State Avg",
            Format = "C2",
            Width = 100,
            AllowEditing = false
        });

        var gridTooltip = new ToolTip();
        gridTooltip.SetToolTip(_departmentsGrid, "Edit Current Charge column to set new rates. Other columns are calculated automatically.");

        deptGridPanel.Controls.Add(_departmentsGrid);
        _leftSplitContainer.Panel1.Controls.Add(deptGridPanel);

        // ============================================================================
        // Benchmarks Grid (Bottom Left)
        // ============================================================================
        var benchmarkPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(benchmarkPanel, "Office2019Colorful");

        var benchmarkLabel = new Label
        {
            Text = "State & National Benchmarks",
            Dock = DockStyle.Top,
            Height = 25,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Padding = new Padding(5, 3, 0, 0)
        };
        benchmarkPanel.Controls.Add(benchmarkLabel);

        _benchmarksGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AllowFiltering = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            TabIndex = 10,
            AccessibleName = "Benchmarks Grid",
            AccessibleDescription = "State and national benchmark data for comparison"
        };

        // Configure benchmark grid columns
        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Department",
            HeaderText = "Department",
            Width = 120,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "StateAverage",
            HeaderText = "State Avg",
            Format = "C2",
            Width = 110,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TownSizeAverage",
            HeaderText = "Town Size Avg",
            Format = "C2",
            Width = 120,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "NationalAverage",
            HeaderText = "National Avg",
            Format = "C2",
            Width = 110,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PopulationRange",
            HeaderText = "Pop Range",
            Width = 100,
            AllowEditing = false
        });

        _benchmarksGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Source",
            HeaderText = "Source",
            Width = 200,
            AllowEditing = false
        });

        var benchmarkTooltip = new ToolTip();
        benchmarkTooltip.SetToolTip(_benchmarksGrid, "Reference data from state and national utility surveys");

        benchmarkPanel.Controls.Add(_benchmarksGrid);
        _leftSplitContainer.Panel2.Controls.Add(benchmarkPanel);

        _mainSplitContainer.Panel1.Controls.Add(_leftSplitContainer);

        // ============================================================================
        // Chart Panel (Right Side)
        // ============================================================================
        _chartPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_chartPanel, "Office2019Colorful");

        _chartControl = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 11,
            AccessibleName = "Department Expense Chart"
        };
        _chartRegionEventWiring = new ChartControlRegionEventWiring(_chartControl);

        ChartControlDefaults.Apply(_chartControl);

        // Configure chart appearance
        _chartControl.Title.Text = "Expenses vs Current vs Suggested Charges";
        _chartControl.Title.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _chartControl.Legend.Visible = true;
        _chartControl.Legend.Position = ChartDock.Top;
        _chartControl.PrimaryXAxis.Title = "Departments";
        _chartControl.PrimaryYAxis.Title = "Amount ($)";
        _chartControl.PrimaryYAxis.RangeType = ChartAxisRangeType.Auto;

        _chartPanel.Controls.Add(_chartControl);
        _mainSplitContainer.Panel2.Controls.Add(_chartPanel);

        Controls.Add(_mainSplitContainer);

        // ============================================================================
        // Status Strip - Bottom Status Bar
        // ============================================================================
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom
        };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // ============================================================================
        // Overlays - Loading and No Data
        // ============================================================================
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading charge data...",
            Visible = false
        };
        Controls.Add(_loadingOverlay);

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No charge data available",
            Visible = false
        };
        Controls.Add(_noDataOverlay);

        _logger.LogDebug("RecommendedMonthlyChargePanel controls initialized with enhanced layout");
    }

    private void BindViewModel()
    {
        if (_viewModel == null)
            return;

        // Bind departments collection to grids
        if (_departmentsGrid != null)
            _departmentsGrid.DataSource = _viewModel.Departments;

        if (_benchmarksGrid != null)
            _benchmarksGrid.DataSource = _viewModel.Benchmarks;

        // Subscribe to property changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Subscribe to collection changes for chart updates
        _viewModel.Departments.CollectionChanged += (s, e) => UpdateChart();
        _viewModel.Benchmarks.CollectionChanged += (s, e) =>
        {
            if (_benchmarksGrid != null && InvokeRequired)
                Invoke(new System.Action(() => _benchmarksGrid.Refresh()));
            else
                _benchmarksGrid?.Refresh();
        };

        _logger.LogDebug("ViewModel bound to RecommendedMonthlyChargePanel");
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private TextBoxExt Get_explanationTextBox1()
    {
        return _explanationTextBox!;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e, TextBoxExt _explanationTextBox1)
    {
        if (InvokeRequired)
        {
            Invoke(new System.Action(() => ViewModel_PropertyChanged(sender, e, Get_explanationTextBox1())));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(_viewModel.TotalCurrentRevenue):
                if (_totalRevenueLabel != null)
                    _totalRevenueLabel.Text = $"Current Revenue: {_viewModel.TotalCurrentRevenue:C2}/month";
                break;

            case nameof(_viewModel.TotalSuggestedRevenue):
                if (_suggestedRevenueLabel != null)
                {
                    var increase = _viewModel.TotalSuggestedRevenue - _viewModel.TotalCurrentRevenue;
                    var increasePercent = _viewModel.TotalCurrentRevenue > 0
                        ? (increase / _viewModel.TotalCurrentRevenue) * 100m
                        : 0m;
                    _suggestedRevenueLabel.Text = $"Suggested Revenue: {_viewModel.TotalSuggestedRevenue:C2}/month ({increasePercent:+0.0;-0.0;0}%)";
                }
                break;

            case nameof(_viewModel.TotalMonthlyExpenses):
                if (_totalExpensesLabel != null)
                    _totalExpensesLabel.Text = $"Total Expenses: {_viewModel.TotalMonthlyExpenses:C2}/month";
                break;

            case nameof(_viewModel.OverallStatus):
                if (_overallStatusLabel != null)
                {
                    _overallStatusLabel.Text = $"Status: {_viewModel.OverallStatus}";
                    // Use semantic status colors (approved exception to SfSkinManager)
                    _overallStatusLabel.ForeColor = _viewModel.OverallStatusColor switch
                    {
                        "Red" => Color.Red,
                        "Orange" => Color.Orange,
                        "Green" => Color.Green,
                        _ => SystemColors.ControlText
                    };
                }
                break;

            case nameof(_viewModel.IsLoading):
                EnableControls(!_viewModel.IsLoading);
                if (_loadingOverlay != null)
                {
                    _loadingOverlay.Visible = _viewModel.IsLoading;
                    if (_viewModel.IsLoading)
                        _loadingOverlay.BringToFront();
                }
                if (_noDataOverlay != null && !_viewModel.IsLoading)
                {
                    _noDataOverlay.Visible = !_viewModel.Departments.Any();
                    if (!_viewModel.Departments.Any())
                        _noDataOverlay.BringToFront();
                }
                break;

            case nameof(_viewModel.Departments):
                UpdateChart();
                if (_departmentsGrid != null)
                    _departmentsGrid.Refresh();
                if (_noDataOverlay != null)
                {
                    _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.Departments.Any();
                    if (!_viewModel.Departments.Any())
                        _noDataOverlay.BringToFront();
                }
                break;

            case nameof(_viewModel.Benchmarks):
                if (_benchmarksGrid != null)
                    _benchmarksGrid.Refresh();
                break;

            case nameof(_viewModel.RecommendationExplanation):
                if (_explanationTextBox1 != null)
                    _explanationTextBox1.Text = _viewModel.RecommendationExplanation ?? "Click 'Query AI' to get AI-powered rate recommendations.";
                break;

            case nameof(_viewModel.StatusText):
                UpdateStatus(_viewModel.StatusText);
                break;

            case nameof(_viewModel.ErrorMessage):
                if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                {
                    UpdateStatus($"Error: {_viewModel.ErrorMessage}");
                }
                break;
        }
    }

    private void UpdateChart()
    {
        if (_chartControl == null || _viewModel == null || !_viewModel.Departments.Any())
        {
            if (_chartControl != null)
            {
                _chartControl.Series.Clear();
                _chartControl.Refresh();
            }
            return;
        }

        try
        {
            _logger.LogDebug("Updating department expense chart with {Count} departments", _viewModel.Departments.Count);

            // Clear existing series
            _chartControl.Series.Clear();

            // Create series for monthly expenses, current charges revenue, and suggested charges revenue
            var expenseSeries = new ChartSeries("Monthly Expenses", ChartSeriesType.Column);
            var currentRevenueSeries = new ChartSeries("Current Charge", ChartSeriesType.Column);
            var suggestedRevenueSeries = new ChartSeries("Suggested Charge", ChartSeriesType.Column);

            // Add data points for each department
            foreach (var department in _viewModel.Departments.OrderBy(d => d.Department))
            {
                var departmentName = department.Department ?? $"Dept {department.CustomerCount}";
                var monthlyExpenses = (double)department.MonthlyExpenses;
                var currentCharge = (double)department.CurrentCharge;
                var suggestedCharge = (double)department.SuggestedCharge;

                expenseSeries.Points.Add(departmentName, monthlyExpenses);
                currentRevenueSeries.Points.Add(departmentName, currentCharge);
                suggestedRevenueSeries.Points.Add(departmentName, suggestedCharge);
            }

            // Add series to chart
            _chartControl.Series.Add(expenseSeries);
            _chartControl.Series.Add(currentRevenueSeries);
            _chartControl.Series.Add(suggestedRevenueSeries);

            // Configure chart appearance
            _chartControl.Title.Text = "Department Expenses vs Current vs Suggested Charges";
            _chartControl.Legend.Visible = true;
            _chartControl.Legend.Position = ChartDock.Top;

            // Configure axes
            _chartControl.PrimaryXAxis.Title = "Departments";
            _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
            _chartControl.PrimaryYAxis.Title = "Amount ($)";
            _chartControl.PrimaryYAxis.RangeType = ChartAxisRangeType.Auto;

            // Enable tooltips
            _chartControl.ShowToolTips = true;
            // Tooltip uses theme defaults

            // Refresh chart to apply changes
            _chartControl.Refresh();

            _logger.LogDebug("Chart updated successfully with {DepartmentCount} departments", _viewModel.Departments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating chart visualization");
            // Chart will remain empty on error, but don't crash the UI
            if (_chartControl != null)
            {
                _chartControl.Series.Clear();
                _chartControl.Title.Text = "Error Loading Chart Data";
                _chartControl.Refresh();
            }
        }
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing recommended monthly charge data");
            UpdateStatus("Loading department expense data...");

            if (_viewModel.RefreshDataCommand.CanExecute(null))
            {
                await _viewModel.RefreshDataCommand.ExecuteAsync(null);
                UpdateChart();
                UpdateStatus("Data refreshed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing data");
            UpdateStatus($"Error: {ex.Message}");
            MessageBox.Show($"Error refreshing data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task QueryGrokAsync()
    {
        try
        {
            _logger.LogInformation("Querying Grok AI for recommendations");
            UpdateStatus("Querying Grok AI for rate recommendations...");

            if (_viewModel.QueryGrokCommand.CanExecute(null))
            {
                await _viewModel.QueryGrokCommand.ExecuteAsync(null);
                UpdateChart();
                UpdateStatus("AI recommendations applied successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Grok AI");
            UpdateStatus($"AI query failed: {ex.Message}");
            MessageBox.Show($"Error querying AI: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveChangesAsync()
    {
        try
        {
            _logger.LogInformation("Saving current charge changes");
            UpdateStatus("Saving charge modifications to database...");

            if (_viewModel.SaveCurrentChargesCommand.CanExecute(null))
            {
                await _viewModel.SaveCurrentChargesCommand.ExecuteAsync(null);
                UpdateStatus("Changes saved successfully");
                MessageBox.Show("Changes saved successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes");
            UpdateStatus($"Save failed: {ex.Message}");
            MessageBox.Show($"Error saving changes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
        }
    }

    private void EnableControls(bool enabled)
    {
        if (_refreshButton != null) _refreshButton.Enabled = enabled;
        if (_queryGrokButton != null) _queryGrokButton.Enabled = enabled;
        if (_saveButton != null) _saveButton.Enabled = enabled;
        if (_departmentsGrid != null) _departmentsGrid.Enabled = enabled;
        if (_benchmarksGrid != null) _benchmarksGrid.Enabled = enabled;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            // Auto-load data on panel load
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading panel data");
        }
    }

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
            _logger.LogWarning(ex, "RecommendedMonthlyChargePanel: ClosePanel failed");
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
            // Unsubscribe from events
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.Dispose();
            }

            // Dispose Syncfusion controls
            _departmentsGrid?.Dispose();
            _benchmarksGrid?.Dispose();
            try { _chartRegionEventWiring?.Dispose(); } catch { }
            _chartRegionEventWiring = null;
            _chartControl?.Dispose();

            // Dispose containers
            _leftSplitContainer?.Dispose();
            _mainSplitContainer?.Dispose();

            // Dispose other controls
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
        this.Name = "RecommendedMonthlyChargePanel";
        this.Size = new Size(1400, 900);
    }

    #endregion
}
