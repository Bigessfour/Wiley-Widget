using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms.Chart;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Analytics;

/// <summary>
/// Control for the Overview tab in Analytics Hub.
/// Displays executive summary with KPIs, summary tiles, chart, and detailed metrics.
/// Migrated from BudgetOverviewPanel logic.
/// </summary>
public partial class OverviewTabControl : UserControl
{
    private readonly OverviewTabViewModel? _viewModel;
    private readonly AnalyticsHubViewModel? _parentViewModel;

    // UI Controls
    private GradientPanelExt? _toolbarPanel;
    private SfComboBox? _fiscalYearComboBox;
    private SfButton? _refreshButton;
    private SfButton? _exportButton;

    private GradientPanelExt? _summaryPanel;
    private GradientPanelExt? _lblTotalBudget;
    private GradientPanelExt? _lblTotalActual;
    private GradientPanelExt? _lblVariance;
    private GradientPanelExt? _lblOverBudgetCount;
    private GradientPanelExt? _lblUnderBudgetCount;

    private ChartControl? _varianceChart;
    private SfDataGrid? _metricsGrid;
    private LoadingOverlay? _loadingOverlay;

    public bool IsLoaded { get; private set; }

    public OverviewTabControl(OverviewTabViewModel? viewModel, AnalyticsHubViewModel? parentViewModel)
    {
        _viewModel = viewModel;
        _parentViewModel = parentViewModel;
        InitializeControls();
        if (_viewModel != null)
        {
            BindViewModel();
        }
    }

    private void InitializeControls()
    {
        this.Dock = DockStyle.Fill;

        // Main layout - vertical split
        var mainSplit = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };

        // Top half - toolbar and summary tiles
        InitializeToolbarAndSummary(mainSplit.Panel1);

        // Bottom half - chart and grid
        InitializeChartAndGrid(mainSplit.Panel2);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading overview data...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        Controls.Add(mainSplit);
    }

    private void InitializeToolbarAndSummary(Control parent)
    {
        var verticalSplit = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 60
        };

        // Top: Toolbar
        InitializeToolbar(verticalSplit.Panel1);

        // Bottom: Summary tiles
        InitializeSummaryTiles(verticalSplit.Panel2);

        parent.Controls.Add(verticalSplit);
    }

    private void InitializeToolbar(Control parent)
    {
        _toolbarPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 10, 5)
        };
        SfSkinManager.SetVisualStyle(_toolbarPanel, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Fiscal Year label
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Fiscal Year combo
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));  // Spacer
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Buttons

        // Fiscal Year
        var fyLabel = new Label { Text = "Fiscal Year:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _fiscalYearComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            DisplayMember = "Value",
            ValueMember = "Value"
        };

        // Buttons
        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _refreshButton = new SfButton { Text = "Refresh", Width = 80 };
        _exportButton = new SfButton { Text = "Export CSV", Width = 100 };

        buttonsPanel.Controls.AddRange(new Control[] { _refreshButton, _exportButton });

        table.Controls.Add(fyLabel, 0, 0);
        table.Controls.Add(_fiscalYearComboBox, 1, 0);
        table.Controls.Add(new Label(), 2, 0); // Spacer
        table.Controls.Add(buttonsPanel, 3, 0);

        _toolbarPanel.Controls.Add(table);
        parent.Controls.Add(_toolbarPanel);
    }

    private void InitializeSummaryTiles(Control parent)
    {
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Create summary tiles
        _lblTotalBudget = CreateSummaryTile("Total Budget", "$0", Color.DodgerBlue);
        _lblTotalActual = CreateSummaryTile("Total Actual", "$0", Color.Green);
        _lblVariance = CreateSummaryTile("Variance", "$0", Color.Orange);
        _lblOverBudgetCount = CreateSummaryTile("Over Budget", "0", Color.Red);
        _lblUnderBudgetCount = CreateSummaryTile("Under Budget", "0", Color.Green);

        flow.Controls.AddRange(new Control[] {
            _lblTotalBudget, _lblTotalActual, _lblVariance,
            _lblOverBudgetCount, _lblUnderBudgetCount
        });

        _summaryPanel.Controls.Add(flow);
        parent.Controls.Add(_summaryPanel);
    }

    private void InitializeChartAndGrid(Control parent)
    {
        var horizontalSplit = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };

        // Top: Chart
        _varianceChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Budget variance chart",
            AccessibleDescription = "Displays budget vs actual variance by department"
        };
        horizontalSplit.Panel1.Controls.Add(_varianceChart);

        // Bottom: Grid
        InitializeMetricsGrid(horizontalSplit.Panel2);

        parent.Controls.Add(horizontalSplit);
    }

    private void InitializeMetricsGrid(Control parent)
    {
        _metricsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowGrouping = true,
            AutoGenerateColumns = false
        };

        var columns = new GridColumn[]
        {
            new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department" },
            new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budget", Format = "C2" },
            new GridNumericColumn { MappingName = "Amount", HeaderText = "Actual", Format = "C2" },
            new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", Format = "C2" },
            new GridNumericColumn { MappingName = "VariancePercent", HeaderText = "Variance %", Format = "P2" }
        };

        foreach (var column in columns)
        {
            _metricsGrid.Columns.Add(column);
        }
        parent.Controls.Add(_metricsGrid);
    }

    private GradientPanelExt CreateSummaryTile(string title, string value, Color accentColor)
    {
        var tile = new GradientPanelExt
        {
            Width = 150,
            Height = 80,
            Margin = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            AccessibleName = $"{title} summary card",
            AccessibleDescription = $"Displays {title.ToLowerInvariant()} metric"
        };
        SfSkinManager.SetVisualStyle(tile, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = accentColor
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9F)
        };

        layout.Controls.Add(valueLabel, 0, 0);
        layout.Controls.Add(titleLabel, 0, 1);

        tile.Controls.Add(layout);
        return tile;
    }

    private void BindViewModel()
    {
        if (_viewModel == null || _parentViewModel == null) return;

        // Bind fiscal year combo to parent view model
        _fiscalYearComboBox!.DataSource = _parentViewModel.FiscalYears;
        _fiscalYearComboBox!.DataBindings.Add("SelectedValue", _parentViewModel, nameof(_parentViewModel.SelectedFiscalYear), true, DataSourceUpdateMode.OnPropertyChanged);

        // Bind summary tiles
        _lblTotalBudget!.DataBindings.Add("Text", _viewModel, nameof(_viewModel.TotalBudget), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
        _lblTotalActual!.DataBindings.Add("Text", _viewModel, nameof(_viewModel.TotalActual), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
        _lblVariance!.DataBindings.Add("Text", _viewModel, nameof(_viewModel.TotalVariance), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
        _lblOverBudgetCount!.DataBindings.Add("Text", _viewModel, nameof(_viewModel.OverBudgetCount), true, DataSourceUpdateMode.OnPropertyChanged, "0", "N0");
        _lblUnderBudgetCount!.DataBindings.Add("Text", _viewModel, nameof(_viewModel.UnderBudgetCount), true, DataSourceUpdateMode.OnPropertyChanged, "0", "N0");

        // Bind grid
        _metricsGrid!.DataSource = _viewModel.Metrics;

        // Bind loading state
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading))
            {
                UpdateLoadingState();
            }
        };

        UpdateLoadingState();
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
        }
    }

    public async Task LoadAsync()
    {
        if (IsLoaded) return;

        try
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadAsync();
            }

            IsLoaded = true;
        }
        catch (Exception ex)
        {
            // Handle error - could show error overlay
            System.Diagnostics.Debug.WriteLine($"Failed to load overview tab: {ex.Message}");
        }
    }
}
