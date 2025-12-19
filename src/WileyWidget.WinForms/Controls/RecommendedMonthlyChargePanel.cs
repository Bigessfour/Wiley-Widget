using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

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
    private ChartControl? _chartControl;
    private Button? _refreshButton;
    private Button? _saveButton;
    private Button? _queryGrokButton;
    private Label? _totalRevenueLabel;
    private Label? _totalExpensesLabel;
    private Label? _overallStatusLabel;
    private Panel? _summaryPanel;
    private Panel? _gridPanel;
    private Panel? _chartPanel;
    private Panel? _buttonPanel;
    private SplitContainer? _mainSplitContainer;
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

        // Panel header
        _panelHeader = new PanelHeader { Dock = DockStyle.Top };
        _panelHeader.Title = "Recommended Monthly Charges";
        _panelHeader.RefreshClicked += async (s, e) => await RefreshDataAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main layout container
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 600
        };

        // Top panel: Summary cards
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(10)
        };

        _totalRevenueLabel = new Label
        {
            Text = "Current Revenue: $0.00",
            Location = new Point(20, 20),
            Size = new Size(300, 30),
            Font = new Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
            TabIndex = 0,
            AccessibleName = "Total Current Revenue",
            AccessibleDescription = "Total monthly revenue from all departments"
        };

        _totalExpensesLabel = new Label
        {
            Text = "Total Expenses: $0.00",
            Location = new Point(20, 55),
            Size = new Size(300, 30),
            Font = new Font("Segoe UI", 12F),
            TabIndex = 1,
            AccessibleName = "Total Monthly Expenses",
            AccessibleDescription = "Total monthly expenses across all departments"
        };

        _overallStatusLabel = new Label
        {
            Text = "Status: Unknown",
            Location = new Point(350, 20),
            Size = new Size(300, 30),
            Font = new Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
            TabIndex = 2,
            AccessibleName = "Overall Profitability Status",
            AccessibleDescription = "Overall profitability status: Losing Money, Breaking Even, or Profitable"
        };

        _summaryPanel.Controls.AddRange(new Control[] { _totalRevenueLabel, _totalExpensesLabel, _overallStatusLabel });

        // Button panel
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(10)
        };

        _refreshButton = new Button
        {
            Text = "&Refresh Data",
            Location = new Point(10, 15),
            Size = new Size(120, 35),
            TabIndex = 3,
            AccessibleName = "Refresh Data",
            AccessibleDescription = "Refresh department expense data from QuickBooks"
        };
        var refreshTooltip = new ToolTip();
        refreshTooltip.SetToolTip(_refreshButton, "Load latest expense data from QuickBooks (Alt+R)");
        _refreshButton.Click += async (s, e) => await RefreshDataAsync();

        _queryGrokButton = new Button
        {
            Text = "&Query AI",
            Location = new Point(140, 15),
            Size = new Size(120, 35),
            TabIndex = 4,
            AccessibleName = "Query AI",
            AccessibleDescription = "Get AI-driven rate recommendations from Grok"
        };
        var grokTooltip = new ToolTip();
        grokTooltip.SetToolTip(_queryGrokButton, "Query Grok AI for recommended adjustment factors (Alt+Q)");
        _queryGrokButton.Click += async (s, e) => await QueryGrokAsync();

        _saveButton = new Button
        {
            Text = "&Save Changes",
            Location = new Point(270, 15),
            Size = new Size(120, 35),
            TabIndex = 5,
            AccessibleName = "Save Changes",
            AccessibleDescription = "Save current charge modifications to database"
        };
        var saveTooltip = new ToolTip();
        saveTooltip.SetToolTip(_saveButton, "Save modified charges to database (Alt+S)");
        _saveButton.Click += async (s, e) => await SaveChangesAsync();

        _buttonPanel.Controls.AddRange(new Control[] { _refreshButton, _queryGrokButton, _saveButton });

        // Grid panel for departments data
        _gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        // Department rates grid
        _departmentsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = true,
            AllowResizingColumns = true,
            AllowSorting = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            EditMode = EditMode.SingleClick,
            TabIndex = 6,
            AccessibleName = "Department Rates Grid",
            AccessibleDescription = "Editable grid showing monthly charges, expenses, and recommendations per department"
        };
        var gridTooltip = new ToolTip();
        gridTooltip.SetToolTip(_departmentsGrid, "Edit Current Charge column to set new rates. Other columns are calculated automatically.");

        // Configure columns
        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Department",
            HeaderText = "Department",
            Width = 150,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyExpenses",
            HeaderText = "Monthly Expenses",
            Format = "C2",
            Width = 150,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CurrentCharge",
            HeaderText = "Current Charge",
            Format = "C2",
            Width = 130,
            AllowEditing = true
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "SuggestedCharge",
            HeaderText = "Suggested Charge",
            Format = "C2",
            Width = 150,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "MonthlyGainLoss",
            HeaderText = "Gain/Loss",
            Format = "C2",
            Width = 130,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PositionStatus",
            HeaderText = "Status",
            Width = 140,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "CustomerCount",
            HeaderText = "Customers",
            Width = 100,
            AllowEditing = false
        });

        _departmentsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "StateAverage",
            HeaderText = "State Avg",
            Format = "C2",
            Width = 120,
            AllowEditing = false
        });

        _gridPanel.Controls.Add(_departmentsGrid);

        // Chart panel with Syncfusion chart
        _chartPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _chartControl = new ChartControl
        {
            Dock = DockStyle.Fill,
            TabIndex = 7,
            AccessibleName = "Department Expense Chart"
        };

        // Configure chart appearance
        _chartControl.Title.Text = "Department Expenses vs Recommended Charges";
        _chartControl.Title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _chartControl.Legend.Visible = true;
        _chartControl.Legend.Position = ChartDock.Top;

        _chartPanel.Controls.Add(_chartControl);

        // Status strip for operation feedback
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

        // Assemble layout
        _mainSplitContainer.Panel1.Controls.Add(_gridPanel);
        _mainSplitContainer.Panel1.Controls.Add(_buttonPanel);
        _mainSplitContainer.Panel1.Controls.Add(_summaryPanel);
        _mainSplitContainer.Panel2.Controls.Add(_chartPanel);

        Controls.Add(_mainSplitContainer);
        Controls.Add(_statusStrip);

        // Loading and no-data overlays
        _loadingOverlay = new LoadingOverlay { Message = "Loading charge data..." };
        Controls.Add(_loadingOverlay);

        _noDataOverlay = new NoDataOverlay { Message = "No charge data available" };
        Controls.Add(_noDataOverlay);

        _logger.LogDebug("RecommendedMonthlyChargePanel controls initialized");
    }

    private void BindViewModel()
    {
        if (_viewModel == null || _departmentsGrid == null)
            return;

        // Bind departments collection to grid
        _departmentsGrid.DataSource = _viewModel.Departments;

        // Subscribe to property changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        _logger.LogDebug("ViewModel bound to RecommendedMonthlyChargePanel");
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(_viewModel.TotalCurrentRevenue):
                if (_totalRevenueLabel != null)
                    _totalRevenueLabel.Text = $"Current Revenue: {_viewModel.TotalCurrentRevenue:C2}";
                break;

            case nameof(_viewModel.TotalMonthlyExpenses):
                if (_totalExpensesLabel != null)
                    _totalExpensesLabel.Text = $"Total Expenses: {_viewModel.TotalMonthlyExpenses:C2}";
                break;

            case nameof(_viewModel.OverallStatus):
                if (_overallStatusLabel != null)
                {
                    _overallStatusLabel.Text = $"Status: {_viewModel.OverallStatus}";
                    // Use semantic status colors (approved exception to SkinManager)
                    _overallStatusLabel.ForeColor = _viewModel.OverallStatusColor switch
                    {
                        "Red" => System.Drawing.Color.Red,
                        "Orange" => System.Drawing.Color.Orange,
                        "Green" => System.Drawing.Color.Green,
                        _ => System.Drawing.Color.Gray
                    };
                }
                break;

            case nameof(_viewModel.IsLoading):
                EnableControls(!_viewModel.IsLoading);
                if (_loadingOverlay != null) _loadingOverlay.Visible = _viewModel.IsLoading;
                if (_noDataOverlay != null) _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.Departments.Any();
                break;

            case nameof(_viewModel.Departments):
                UpdateChart();
                if (_noDataOverlay != null) _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.Departments.Any();
                break;
        }
    }

    private void UpdateChart()
    {
        if (_chartControl == null || _viewModel == null || !_viewModel.Departments.Any())
            return;

        try
        {
            _logger.LogDebug("Updating department expense chart");

            // Clear existing series
            _chartControl.Series.Clear();

            // Create series for actual expenses and recommended charges
            var expenseSeries = new ChartSeries("Actual Expenses", ChartSeriesType.Column);
            var recommendedSeries = new ChartSeries("Recommended Charges", ChartSeriesType.Column);

            // Configure series appearance
            expenseSeries.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(255, 99, 132)); // Red
            recommendedSeries.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(54, 162, 235)); // Blue

            // Add data points
            foreach (var department in _viewModel.Departments)
            {
                var departmentName = department.Department ?? $"Dept {department.CustomerCount}";
                var actualExpense = department.MonthlyExpenses;
                var recommendedCharge = department.SuggestedCharge;

                expenseSeries.Points.Add(departmentName, (double)actualExpense);
                recommendedSeries.Points.Add(departmentName, (double)recommendedCharge);
            }

            // Add series to chart
            _chartControl.Series.Add(expenseSeries);
            _chartControl.Series.Add(recommendedSeries);

            // Configure axes
            _chartControl.PrimaryXAxis.Title = "Departments";
            _chartControl.PrimaryYAxis.Title = "Amount ($)";
            // _chartControl.PrimaryYAxis.LabelFormat = "C0"; // Currency format - TODO: Check correct property

            // Refresh chart
            _chartControl.Refresh();

            _logger.LogDebug("Chart updated successfully with {DepartmentCount} departments", _viewModel.Departments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating chart");
            // Chart will remain empty on error, but don't crash the UI
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
            _viewModel?.Dispose();

            _departmentsGrid?.Dispose();
            _chartControl?.Dispose();
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
        this.Name = "RecommendedMonthlyChargePanel";
        this.Size = new Size(1400, 900);
    }

    #endregion
}
