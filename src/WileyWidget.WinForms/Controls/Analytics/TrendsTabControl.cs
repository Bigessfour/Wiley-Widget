using System;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using ChartControl = Syncfusion.Windows.Forms.Chart.ChartControl;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;

using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

namespace WileyWidget.WinForms.Controls.Analytics;

/// <summary>
/// Control for the Trends & Forecasts tab in Analytics Hub.
/// Displays time-series charts with historical data and projections.
/// </summary>
public partial class TrendsTabControl : UserControl
{
    private readonly TrendsTabViewModel? _viewModel;

    // UI Controls
    private ChartControl? _trendsChart;
    private ChartControl? _forecastChart;
    private ChartControl? _departmentChart;
    private LegacyGradientPanel? _controlsPanel;
    private LoadingOverlay? _loadingOverlay;

    // Control elements
    private NumericUpDown? _projectionYearsSpinner;
    private Button? _refreshButton;

    public bool IsLoaded { get; private set; }

    public TrendsTabControl(TrendsTabViewModel? viewModel)
    {
        _viewModel = viewModel;

        // Apply Syncfusion theme
        try
        {
            var theme = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
            SfSkinManager.SetVisualStyle(this, theme);
        }
        catch { /* Theme application is best-effort */ }

        InitializeControls();
        if (_viewModel != null)
        {
            BindViewModel();
        }
    }

    private void InitializeControls()
    {
        this.Dock = DockStyle.Fill;

        // Main layout - controls on top, charts below in a 2x2 grid
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 60
        };

        // Top: Controls panel
        InitializeControlsPanel();
        mainSplit.Panel1.Controls.Add(_controlsPanel);

        // Bottom: Charts in a table layout
        InitializeChartsPanel();
        mainSplit.Panel2.Controls.Add(CreateChartsTable());

        // Loading overlay
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
        _controlsPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        SfSkinManager.SetVisualStyle(_controlsPanel, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Spinner
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Button

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
            {
                _viewModel.ProjectionYears = (int)_projectionYearsSpinner.Value;
            }
        };

        _refreshButton = new Button
        {
            Text = "Refresh Chart",
            Dock = DockStyle.Fill
        };
        _refreshButton.Click += async (s, e) =>
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadAsync();
                UpdateTrendsChart();
                UpdateForecastChart();
                UpdateDepartmentChart();
            }
        };

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_projectionYearsSpinner, 1, 0);
        layout.Controls.Add(new Panel(), 2, 0); // Spacer
        layout.Controls.Add(_refreshButton, 3, 0);

        _controlsPanel.Controls.Add(layout);
    }

    private void InitializeChartsPanel()
    {
        // Initialize all three charts
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

        // Set column and row styles for equal distribution
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        // Add charts to the table
        if (_trendsChart != null) table.Controls.Add(_trendsChart, 0, 0);
        if (_forecastChart != null) table.Controls.Add(_forecastChart, 1, 0);
        if (_departmentChart != null) table.Controls.Add(_departmentChart, 0, 1);
        if (_departmentChart != null) table.SetColumnSpan(_departmentChart, 2); // Department chart spans both columns in bottom row

        return table;
    }

    private void InitializeTrendsChart()
    {
        _trendsChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            Title = { Text = "Budget Trends (Budgeted vs Actual)" }
        };

        // Configure chart appearance
        _trendsChart.PrimaryXAxis.Title = "Period";
        _trendsChart.PrimaryYAxis.Title = "Amount ($)";

        // Enable zooming and scrolling
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

        // Configure chart appearance
        _forecastChart.PrimaryXAxis.Title = "Date";
        _forecastChart.PrimaryYAxis.Title = "Predicted Reserves ($)";

        // Enable zooming and scrolling
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

        // Configure chart appearance
        _departmentChart.PrimaryXAxis.Title = "Department";
        _departmentChart.PrimaryYAxis.Title = "Average Variance %";

        // Enable zooming and scrolling
        _departmentChart.EnableXZooming = true;
        _departmentChart.EnableYZooming = true;

        ChartControlDefaults.Apply(_departmentChart, logger: null);
    }

    private void BindViewModel()
    {
        if (_viewModel == null) return;

        // Bind loading state
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading))
            {
                UpdateLoadingState();
            }
            else if (e.PropertyName == nameof(_viewModel.TrendData))
            {
                UpdateTrendsChart();
            }
            else if (e.PropertyName == nameof(_viewModel.ForecastData))
            {
                UpdateForecastChart();
            }
            else if (e.PropertyName == nameof(_viewModel.DepartmentVariances))
            {
                UpdateDepartmentChart();
            }
            else if (e.PropertyName == nameof(_viewModel.ProjectionYears))
            {
                if (_projectionYearsSpinner != null)
                {
                    _projectionYearsSpinner.Value = _viewModel.ProjectionYears;
                }
            }
        };

        // Set initial values
        if (_projectionYearsSpinner != null)
        {
            _projectionYearsSpinner.Value = _viewModel.ProjectionYears;
        }
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
        }
    }

    private void UpdateTrendsChart()
    {
        if (_trendsChart == null || _viewModel?.TrendData == null) return;

        _trendsChart.Series.Clear();

        foreach (var series in _viewModel.TrendData)
        {
            var chartSeries = new ChartSeries(series.Name);
            chartSeries.Type = ChartSeriesType.Line;

            int index = 0;
            foreach (var point in series.Points)
            {
                chartSeries.Points.Add(index++, (double)point.Value);
            }

            _trendsChart.Series.Add(chartSeries);
        }

        _trendsChart.Refresh();
    }

    private void UpdateForecastChart()
    {
        if (_forecastChart == null || _viewModel?.ForecastData == null) return;

        _forecastChart.Series.Clear();

        var forecastSeries = new ChartSeries("Predicted Reserves");
        forecastSeries.Type = ChartSeriesType.Line;

        int index = 0;
        foreach (var point in _viewModel.ForecastData)
        {
            forecastSeries.Points.Add(index++, (double)point.PredictedReserves);
        }

        _forecastChart.Series.Add(forecastSeries);
        _forecastChart.Refresh();
    }

    private void UpdateDepartmentChart()
    {
        if (_departmentChart == null || _viewModel?.DepartmentVariances == null) return;

        _departmentChart.Series.Clear();

        var varianceSeries = new ChartSeries("Average Variance %");
        varianceSeries.Type = ChartSeriesType.Column;

        int index = 0;
        foreach (var dept in _viewModel.DepartmentVariances)
        {
            varianceSeries.Points.Add(index++, (double)dept.AverageVariancePercent);
        }

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
            // Handle error
            System.Diagnostics.Debug.WriteLine($"Failed to load trends tab: {ex.Message}");
        }
    }
}
