using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using ChartControl = Syncfusion.Windows.Forms.Chart.ChartControl;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Utilities;


namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Control for the Trends and Forecasts tab in Analytics Hub.
/// Thin view: no local refresh button; refresh is driven by AnalyticsHubPanel.
/// </summary>
public partial class TrendsTabControl : UserControl
{
    private readonly TrendsTabViewModel? _viewModel;
    private readonly ILogger _logger;

    private ChartControl? _trendsChart;
    private ChartControl? _forecastChart;
    private ChartControl? _departmentChart;
    private Panel? _controlsPanel;
    private LoadingOverlay? _loadingOverlay;

    private NumericUpDownExt? _projectionYearsSpinner;

    public bool IsLoaded { get; private set; }

    public TrendsTabControl(TrendsTabViewModel? viewModel, ILogger? logger = null)
    {
        _viewModel = viewModel;
        _logger = logger ?? NullLogger.Instance;

        try
        {
            var theme = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
            SfSkinManager.SetVisualStyle(this, theme);
        }
        catch { /* best-effort */ }

        InitializeControls();
        if (_viewModel != null) BindViewModel();
    }

    private void InitializeControls()
    {
        this.Dock = DockStyle.Fill;

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 60
        };

        InitializeControlsPanel();
        mainSplit.Panel1.Controls.Add(_controlsPanel);

        InitializeChartsPanel();
        mainSplit.Panel2.Controls.Add(CreateChartsTable());

        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading trends data...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();
        Controls.Add(mainSplit);
    }

    private void InitializeControlsPanel()
    {
        _controlsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(LayoutTokens.PanelPadding) };
        SfSkinManager.SetVisualStyle(_controlsPanel, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Spinner
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Spacer

        var label = new Label
        {
            Text = "Projection Years:",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        };

        _projectionYearsSpinner = new NumericUpDownExt
        {
            Minimum = 1,
            Maximum = 10,
            Value = 3,
            Dock = DockStyle.Fill
        };
        _projectionYearsSpinner.ValueChanged += (s, e) =>
        {
            if (_viewModel != null)
                _viewModel.ProjectionYears = (int)_projectionYearsSpinner.Value;
        };

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_projectionYearsSpinner, 1, 0);
        layout.Controls.Add(new Panel(), 2, 0);

        _controlsPanel.Controls.Add(layout);
    }

    private void InitializeChartsPanel()
    {
        InitializeTrendsChart();
        InitializeForecastChart();
        InitializeDepartmentChart();
    }

    private Control CreateChartsTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(5)
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        if (_trendsChart != null) table.Controls.Add(_trendsChart, 0, 0);
        if (_forecastChart != null) table.Controls.Add(_forecastChart, 1, 0);
        if (_departmentChart != null)
        {
            table.Controls.Add(_departmentChart, 0, 1);
            table.SetColumnSpan(_departmentChart, 2);
        }

        return table;
    }

    private void InitializeTrendsChart()
    {
        _trendsChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            Title = { Text = "Budget Trends (Budgeted vs Actual)" }
        };
        _trendsChart.PrimaryXAxis.Title = "Period";
        _trendsChart.PrimaryYAxis.Title = "Amount ($)";
        _trendsChart.EnableXZooming = true;
        _trendsChart.EnableYZooming = true;
        ChartControlDefaults.Apply(_trendsChart, logger: null);
    }

    private void InitializeForecastChart()
    {
        _forecastChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            Title = { Text = "Reserve Forecast" }
        };
        _forecastChart.PrimaryXAxis.Title = "Date";
        _forecastChart.PrimaryYAxis.Title = "Predicted Reserves ($)";
        _forecastChart.EnableXZooming = true;
        _forecastChart.EnableYZooming = true;
        ChartControlDefaults.Apply(_forecastChart, logger: null);
    }

    private void InitializeDepartmentChart()
    {
        _departmentChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            Title = { Text = "Department Variance Analysis" }
        };
        _departmentChart.PrimaryXAxis.Title = "Department";
        _departmentChart.PrimaryYAxis.Title = "Average Variance %";
        _departmentChart.EnableXZooming = true;
        _departmentChart.EnableYZooming = true;
        ChartControlDefaults.Apply(_departmentChart, logger: null);
    }

    private void BindViewModel()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading))
                UpdateLoadingState();
            else if (e.PropertyName == nameof(_viewModel.TrendData))
                UpdateTrendsChart();
            else if (e.PropertyName == nameof(_viewModel.ForecastData))
                UpdateForecastChart();
            else if (e.PropertyName == nameof(_viewModel.DepartmentVariances))
                UpdateDepartmentChart();
            else if (e.PropertyName == nameof(_viewModel.ProjectionYears) && _projectionYearsSpinner != null)
                _projectionYearsSpinner.Value = _viewModel.ProjectionYears;
        };

        if (_projectionYearsSpinner != null)
            _projectionYearsSpinner.Value = _viewModel.ProjectionYears;
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null)
            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
    }

    private void UpdateTrendsChart()
    {
        if (_trendsChart == null || _viewModel?.TrendData == null) return;

        _trendsChart.Series.Clear();
        foreach (var series in _viewModel.TrendData)
        {
            var chartSeries = new ChartSeries(series.Name) { Type = ChartSeriesType.Line };
            int index = 0;
            foreach (var point in series.Points)
                chartSeries.Points.Add(index++, (double)point.Value);
            _trendsChart.Series.Add(chartSeries);
        }
        _trendsChart.Refresh();
    }

    private void UpdateForecastChart()
    {
        if (_forecastChart == null || _viewModel?.ForecastData == null) return;

        _forecastChart.Series.Clear();
        var forecastSeries = new ChartSeries("Predicted Reserves") { Type = ChartSeriesType.Line };
        int index = 0;
        foreach (var point in _viewModel.ForecastData)
            forecastSeries.Points.Add(index++, (double)point.PredictedReserves);
        _forecastChart.Series.Add(forecastSeries);
        _forecastChart.Refresh();
    }

    private void UpdateDepartmentChart()
    {
        if (_departmentChart == null || _viewModel?.DepartmentVariances == null) return;

        _departmentChart.Series.Clear();
        var varianceSeries = new ChartSeries("Average Variance %") { Type = ChartSeriesType.Column };
        int index = 0;
        foreach (var dept in _viewModel.DepartmentVariances)
            varianceSeries.Points.Add(index++, (double)dept.AverageVariancePercent);
        _departmentChart.Series.Add(varianceSeries);
        _departmentChart.Refresh();
    }

    public async Task LoadAsync()
    {
        if (IsLoaded) return;
        try
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadAsync();
                UpdateTrendsChart();
                UpdateForecastChart();
                UpdateDepartmentChart();
            }
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Trends LoadAsync error: {ex.Message}");
            _logger.LogError(ex, "[TrendsTabControl] LoadAsync failed");
        }
    }
}
