using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Input.Events;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// vNext scenarios tab: slider-driven inputs, charted projections, grid, and AI narrative.
/// </summary>
public class AdvancedScenariosTabControl : UserControl
{
    private readonly AdvancedScenariosTabViewModel? _viewModel;
    private readonly SyncfusionControlFactory _factory;
    private readonly ILogger _logger;

    private SfNumericTextBox? _rateBox;
    private SfNumericTextBox? _expenseBox;
    private SfNumericTextBox? _revenueBox;
    private SfNumericTextBox? _yearsBox;
    private SfButton? _runButton;
    private SfButton? _exportButton;
    private SfButton? _resetButton;
    private SfDataGrid? _grid;
    private ChartControl? _chart;
    private RichTextBox? _narrativeBox;
    private Label? _statusLabel;
    private LoadingOverlay? _loadingOverlay;
    private ToolTip? _toolTip;

    private PropertyChangedEventHandler? _vmChanged;

    public bool IsLoaded { get; private set; }

    public AdvancedScenariosTabControl(AdvancedScenariosTabViewModel? viewModel, SyncfusionControlFactory factory, ILogger? logger = null)
    {
        _viewModel = viewModel;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? NullLogger.Instance;

        var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(this, theme);

        InitializeLayout();
        BindViewModel();
    }

    private void InitializeLayout()
    {
        Dock = DockStyle.Fill;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        MinimumSize = ScopedPanelBase.RecommendedEmbeddedPanelMinimumLogicalSize;
        Padding = new Padding(8);
        _toolTip = new ToolTip();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));

        var sidebar = BuildSidebar();
        var mainContent = BuildMainContent();
        var narrativePanel = BuildNarrativePanel();

        root.Controls.Add(sidebar, 0, 0);
        root.Controls.Add(mainContent, 1, 0);
        root.Controls.Add(narrativePanel, 2, 0);

        _loadingOverlay = _factory.CreateLoadingOverlay(o => o.Message = "Running scenario...");
        Controls.Add(root);
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();
    }

    private Control BuildSidebar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = "Scenario Parameters",
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI", 10.0f, System.Drawing.FontStyle.Bold)
        }, 0, 0);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 6)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        AddNumericRow(form, 0, "Rate Increase %", out _rateBox, RateBox_ValueChanged, 3.5, 2);
        AddNumericRow(form, 1, "Expense Increase %", out _expenseBox, ExpenseBox_ValueChanged, 2.8, 2);
        AddNumericRow(form, 2, "Revenue Target %", out _revenueBox, RevenueBox_ValueChanged, 4.0, 2);
        AddNumericRow(form, 3, "Projection Years", out _yearsBox, YearsBox_ValueChanged, 5, 0, 1, 20);

        panel.Controls.Add(form, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 4)
        };

        _runButton = _factory.CreateSfButton("Run Scenario", b =>
        {
            b.Width = 200;
            b.Height = 42;
        });
        _runButton.AccessibleName = "Run scenario";
        _runButton.AccessibleDescription = "Execute advanced scenario analysis";
        _toolTip?.SetToolTip(_runButton, "Run the scenario with the selected assumptions.");
        _runButton.Click += RunButton_ClickAsync;

        _exportButton = _factory.CreateSfButton("Export", b =>
        {
            b.Width = 200;
            b.Height = 32;
        });
        _exportButton.AccessibleName = "Export advanced scenario";
        _exportButton.AccessibleDescription = "Export advanced scenario output";
        _toolTip?.SetToolTip(_exportButton, "Export scenario projections.");
        _exportButton.Click += ExportButton_ClickAsync;

        _resetButton = _factory.CreateSfButton("Reset to Base", b =>
        {
            b.Width = 200;
            b.Height = 32;
        });
        _resetButton.AccessibleName = "Reset scenario";
        _resetButton.AccessibleDescription = "Reset advanced scenario inputs to baseline";
        _toolTip?.SetToolTip(_resetButton, "Reset inputs to baseline assumptions.");
        _resetButton.Click += ResetButton_ClickAsync;

        buttons.Controls.AddRange(new Control[] { _runButton, _exportButton, _resetButton });
        panel.Controls.Add(buttons, 0, 2);

        _statusLabel = new Label
        {
            Text = _viewModel?.StatusMessage ?? "Ready",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        panel.Controls.Add(_statusLabel, 0, 3);

        panel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 4);
        return panel;
    }

    private Control BuildChartPanel()
    {
        _chart = _factory.CreateSfChart("5-Year Reserve Projections", chart =>
        {
            chart.PrimaryXAxis.Title = "Fiscal Year";
            chart.PrimaryYAxis.Title = "Projected Reserves";
            chart.ShowLegend = true;
            chart.AccessibleName = "Scenario projection chart";
            chart.AccessibleDescription = "Chart of reserve projection scenarios";
        });
        _toolTip?.SetToolTip(_chart, "Visual comparison of baseline and alternate projections.");

        return _chart;
    }

    private Control BuildMainContent()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        var chartPanel = BuildChartPanel();
        var gridPanel = BuildGridPanel();

        panel.Controls.Add(chartPanel, 0, 0);
        panel.Controls.Add(gridPanel, 0, 1);
        return panel;
    }

    private Control BuildGridPanel()
    {
        _grid = _factory.CreateSfDataGrid(grid =>
        {
            grid.AutoGenerateColumns = false;
            grid.AllowEditing = false;
            grid.AllowFiltering = true;
            grid.AllowSorting = true;
            grid.SelectionMode = GridSelectionMode.Single;
            grid.AccessibleName = "Projection details grid";
            grid.AccessibleDescription = "Detailed year-by-year projection values";

            grid.Columns.Add(new GridTextColumn { MappingName = nameof(YearlyProjection.Year), HeaderText = "Year" });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(YearlyProjection.ProjectedRevenue), HeaderText = "Revenue", Format = "C0" });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(YearlyProjection.ProjectedExpenses), HeaderText = "Expenses", Format = "C0" });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(YearlyProjection.ProjectedReserves), HeaderText = "Reserves", Format = "C0" });
            grid.Columns.Add(new GridNumericColumn { MappingName = nameof(YearlyProjection.RiskLevel), HeaderText = "Risk", Format = "P0" });
        });
        _toolTip?.SetToolTip(_grid, "Detailed projection values by year.");

        return _grid;
    }

    private Control BuildNarrativePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Text = "AI Analyst (xAI + Semantic Kernel)",
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI", 10.0f, System.Drawing.FontStyle.Bold)
        };

        _narrativeBox = _factory.CreateRichTextBoxExt(r => r.Text = "AI Analyst ready — run a scenario to begin.");
        _narrativeBox.AccessibleName = "AI narrative";
        _narrativeBox.AccessibleDescription = "AI-generated summary of scenario outcomes";
        _toolTip?.SetToolTip(_narrativeBox, "AI analysis and recommendations for the selected scenario.");
        panel.Controls.Add(heading, 0, 0);
        panel.Controls.Add(_narrativeBox, 0, 1);
        return panel;
    }

    private void AddNumericRow(
        TableLayoutPanel form,
        int row,
        string labelText,
        out SfNumericTextBox? box,
        ValueChangedEventHandler changed,
        double defaultValue,
        int decimalPlaces,
        double min = -50,
        double max = 200)
    {
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            AutoSize = true,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 2, 6, 2)
        };

        box = _factory.CreateSfNumericTextBox(n =>
        {
            n.MinValue = min;
            n.MaxValue = max;
            var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            format.NumberDecimalDigits = decimalPlaces;
            n.NumberFormatInfo = format;
            n.Value = defaultValue;
            n.Dock = DockStyle.Fill;
            n.AccessibleName = labelText;
            n.AccessibleDescription = $"Input for {labelText.ToLowerInvariant()}";
        });
        _toolTip?.SetToolTip(box, $"Set {labelText.ToLowerInvariant()}.");
        box.ValueChanged += changed;

        form.Controls.Add(label, 0, row);
        form.Controls.Add(box, 1, row);
    }

    private void BindViewModel()
    {
        if (_viewModel == null)
        {
            return;
        }

        _vmChanged ??= ViewModel_PropertyChanged;
        _viewModel.PropertyChanged -= _vmChanged;
        _viewModel.PropertyChanged += _vmChanged;

        UpdateNumericFields();
        UpdateGrid();
        UpdateChart();
        UpdateNarrative();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(AdvancedScenariosTabViewModel.IsLoading):
                UpdateLoadingState();
                break;
            case nameof(AdvancedScenariosTabViewModel.AiNarrative):
                UpdateNarrative();
                break;
            case nameof(AdvancedScenariosTabViewModel.Projections):
                UpdateGrid();
                UpdateChart();
                break;
            case nameof(AdvancedScenariosTabViewModel.RateIncreasePercent):
            case nameof(AdvancedScenariosTabViewModel.ExpenseIncreasePercent):
            case nameof(AdvancedScenariosTabViewModel.RevenueTargetPercent):
            case nameof(AdvancedScenariosTabViewModel.ProjectionYears):
                UpdateNumericFields();
                break;
            case nameof(AdvancedScenariosTabViewModel.StatusMessage):
                UpdateStatusText();
                break;
        }
    }

    private void RateBox_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_viewModel == null || _rateBox == null) return;
        var value = _rateBox.Value ?? 0d;
        _viewModel.RateIncreasePercent = (decimal)value;
    }

    private void ExpenseBox_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_viewModel == null || _expenseBox == null) return;
        var value = _expenseBox.Value ?? 0d;
        _viewModel.ExpenseIncreasePercent = (decimal)value;
    }

    private void RevenueBox_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_viewModel == null || _revenueBox == null) return;
        var value = _revenueBox.Value ?? 0d;
        _viewModel.RevenueTargetPercent = (decimal)value;
    }

    private void YearsBox_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_viewModel == null || _yearsBox == null) return;
        var value = _yearsBox.Value ?? 0d;
        _viewModel.ProjectionYears = (int)value;
    }

    private void UpdateNumericFields()
    {
        if (_viewModel == null) return;
        if (_rateBox != null) _rateBox.Value = (double)_viewModel.RateIncreasePercent;
        if (_expenseBox != null) _expenseBox.Value = (double)_viewModel.ExpenseIncreasePercent;
        if (_revenueBox != null) _revenueBox.Value = (double)_viewModel.RevenueTargetPercent;
        if (_yearsBox != null) _yearsBox.Value = _viewModel.ProjectionYears;
    }

    private void UpdateGrid()
    {
        if (_grid == null || _viewModel == null) return;
        _grid.DataSource = _viewModel.Projections;
    }

    private void UpdateChart()
    {
        if (_chart == null || _viewModel == null) return;
        _chart.Series.Clear();

        var baseSeries = new ChartSeries("Base", ChartSeriesType.Line);
        var optimistic = new ChartSeries("Optimistic", ChartSeriesType.Line);
        var pessimistic = new ChartSeries("Pessimistic", ChartSeriesType.Line);

        foreach (var projection in _viewModel.Projections)
        {
            var year = projection.Year;
            var reserves = (double)projection.ProjectedReserves;
            baseSeries.Points.Add(new ChartPoint(year, reserves));
            optimistic.Points.Add(new ChartPoint(year, reserves * 1.05));
            pessimistic.Points.Add(new ChartPoint(year, reserves * 0.95));
        }

        _chart.Series.Add(baseSeries);
        _chart.Series.Add(optimistic);
        _chart.Series.Add(pessimistic);
        _chart.Refresh();
    }

    private void UpdateNarrative()
    {
        if (_narrativeBox != null && _viewModel != null)
        {
            _narrativeBox.Text = _viewModel.AiNarrative;
        }
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null && _viewModel != null)
        {
            _loadingOverlay.Visible = _viewModel.IsLoading;
        }

        var buttonsEnabled = _viewModel?.IsLoading == false;
        if (_runButton != null) _runButton.Enabled = buttonsEnabled;
        if (_exportButton != null) _exportButton.Enabled = buttonsEnabled;
        if (_resetButton != null) _resetButton.Enabled = buttonsEnabled;
    }

    private async void RunButton_ClickAsync(object? sender, EventArgs e)
    {
        await ExecuteCommandAsync(_viewModel?.RunScenarioCommand).ConfigureAwait(true);
    }

    private async void ExportButton_ClickAsync(object? sender, EventArgs e)
    {
        await ExecuteCommandAsync(_viewModel?.ExportProjectionsCommand).ConfigureAwait(true);
    }

    private async void ResetButton_ClickAsync(object? sender, EventArgs e)
    {
        await ExecuteCommandAsync(_viewModel?.ResetCommand).ConfigureAwait(true);
        UpdateNumericFields();
        UpdateGrid();
        UpdateChart();
    }

    private void UpdateStatusText()
    {
        if (_loadingOverlay != null && _viewModel != null)
        {
            _loadingOverlay.Message = _viewModel.StatusMessage;
        }

        if (_statusLabel != null && _viewModel != null)
        {
            _statusLabel.Text = _viewModel.StatusMessage;
        }
    }

    private async Task ExecuteCommandAsync(IAsyncRelayCommand? command)
    {
        if (command == null) return;
        try
        {
            await command.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advanced scenarios command execution failed");
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
                UpdateNumericFields();
                UpdateGrid();
                UpdateChart();
            }
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvancedScenariosTabControl load failed");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_rateBox != null) _rateBox.ValueChanged -= RateBox_ValueChanged;
            if (_expenseBox != null) _expenseBox.ValueChanged -= ExpenseBox_ValueChanged;
            if (_revenueBox != null) _revenueBox.ValueChanged -= RevenueBox_ValueChanged;
            if (_yearsBox != null) _yearsBox.ValueChanged -= YearsBox_ValueChanged;
            if (_runButton != null) _runButton.Click -= RunButton_ClickAsync;
            if (_exportButton != null) _exportButton.Click -= ExportButton_ClickAsync;
            if (_resetButton != null) _resetButton.Click -= ResetButton_ClickAsync;
            if (_viewModel != null && _vmChanged != null)
            {
                _viewModel.PropertyChanged -= _vmChanged;
            }

            _toolTip?.Dispose();
        }
        base.Dispose(disposing);
    }
}
