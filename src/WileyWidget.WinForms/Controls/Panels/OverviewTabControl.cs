using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms.Chart;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;

using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Control for the Overview tab in Analytics Hub.
/// Thin view: fiscal year managed by AnalyticsHubPanel. No toolbar, no local export.
/// </summary>
public partial class OverviewTabControl : UserControl
{
    private readonly OverviewTabViewModel? _viewModel;
    private readonly ILogger _logger;

    private LegacyGradientPanel? _summaryPanel;
    private Label? _lblTotalBudget;
    private Label? _lblTotalActual;
    private Label? _lblVariance;
    private Label? _lblOverBudgetCount;
    private Label? _lblUnderBudgetCount;

    private ChartControl? _varianceChart;
    private SfDataGrid? _metricsGrid;
    private LoadingOverlay? _loadingOverlay;

    public bool IsLoaded { get; private set; }

    public OverviewTabControl(OverviewTabViewModel? viewModel, ILogger? logger = null)
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

        var mainSplit = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 100
        };

        InitializeSummaryTiles(mainSplit.Panel1);
        InitializeChartAndGrid(mainSplit.Panel2);

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

    private void InitializeSummaryTiles(Control parent)
    {
        _summaryPanel = new LegacyGradientPanel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        SfSkinManager.SetVisualStyle(_summaryPanel, SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful");

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        _lblTotalBudget      = CreateSummaryTile(flow, "Total Budget",  "$0", Color.DodgerBlue);
        _lblTotalActual      = CreateSummaryTile(flow, "Total Actual",  "$0", Color.Green);
        _lblVariance         = CreateSummaryTile(flow, "Variance",      "$0", Color.Orange);
        _lblOverBudgetCount  = CreateSummaryTile(flow, "Over Budget",   "0",  Color.Red);
        _lblUnderBudgetCount = CreateSummaryTile(flow, "Under Budget",  "0",  Color.Green);

        _summaryPanel.Controls.Add(flow);
        parent.Controls.Add(_summaryPanel);
    }

    private void InitializeChartAndGrid(Control parent)
    {
        var split = new SplitContainerAdv { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

        _varianceChart = new ChartControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Budget variance chart",
            AccessibleDescription = "Displays budget vs actual variance by department"
        };
        split.Panel1.Controls.Add(_varianceChart);

        InitializeMetricsGrid(split.Panel2);
        parent.Controls.Add(split);
    }

    private void InitializeMetricsGrid(Control parent)
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        _metricsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowGrouping = true,
            AutoGenerateColumns = false,
            ThemeName = themeName
        }.PreventStringRelationalFilters(null, "DepartmentName");

        GridColumn[] columns =
        [
            new GridTextColumn    { MappingName = "DepartmentName",  HeaderText = "Department"  },
            new GridNumericColumn { MappingName = "BudgetedAmount",  HeaderText = "Budget",     Format = "C2" },
            new GridNumericColumn { MappingName = "Amount",          HeaderText = "Actual",     Format = "C2" },
            new GridNumericColumn { MappingName = "Variance",        HeaderText = "Variance",   Format = "C2" },
            new GridNumericColumn { MappingName = "VariancePercent", HeaderText = "Variance %", Format = "P2" }
        ];

        foreach (var col in columns) _metricsGrid.Columns.Add(col);
        parent.Controls.Add(_metricsGrid);
    }

    private Label CreateSummaryTile(FlowLayoutPanel parent, string title, string value, Color accentColor)
    {
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        var tile = new LegacyGradientPanel
        {
            Width = 150, Height = 80, Margin = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            AccessibleName = $"{title} summary card",
            AccessibleDescription = $"Displays {title.ToLowerInvariant()} metric"
        };
        SfSkinManager.SetVisualStyle(tile, currentTheme);
        tile.ThemeName = currentTheme;

        tile.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter
        });

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AccessibleName = $"{title} value",
            AccessibleDescription = $"Current {title.ToLowerInvariant()}: {value}"
        };
        tile.Controls.Add(valueLabel);
        parent.Controls.Add(tile);
        return valueLabel;
    }

    private void BindViewModel()
    {
        try
        {
            if (_viewModel == null) return;
            if (!this.IsHandleCreated) this.CreateControl();

            if (_lblTotalBudget != null && !this.IsDisposed)
                _lblTotalBudget.DataBindings.Add("Text", _viewModel, nameof(_viewModel.TotalBudget), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
            if (_lblTotalActual != null && !this.IsDisposed)
                _lblTotalActual.DataBindings.Add("Text", _viewModel, nameof(_viewModel.TotalActual), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
            if (_lblVariance != null && !this.IsDisposed)
                _lblVariance.DataBindings.Add("Text", _viewModel, nameof(_viewModel.TotalVariance), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
            if (_lblOverBudgetCount != null && !this.IsDisposed)
                _lblOverBudgetCount.DataBindings.Add("Text", _viewModel, nameof(_viewModel.OverBudgetCount), true, DataSourceUpdateMode.OnPropertyChanged, "0", "N0");
            if (_lblUnderBudgetCount != null && !this.IsDisposed)
                _lblUnderBudgetCount.DataBindings.Add("Text", _viewModel, nameof(_viewModel.UnderBudgetCount), true, DataSourceUpdateMode.OnPropertyChanged, "0", "N0");

            if (_metricsGrid != null && !this.IsDisposed)
            {
                try
                {
                    _metricsGrid.BeginUpdate();
                    _metricsGrid.DataSource = _viewModel.Metrics;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "[OverviewTabControl] Grid bind error"); }
                finally { _metricsGrid.EndUpdate(); }
            }

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateLoadingState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OverviewTabControl BindViewModel error: {ex.Message}");
            _logger.LogError(ex, "[OverviewTabControl] BindViewModel failed");
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(OverviewTabViewModel.IsLoading) && !this.IsDisposed)
                UpdateLoadingState();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ViewModel_PropertyChanged error: {ex.Message}"); _logger.LogWarning(ex, "[OverviewTabControl] PropertyChanged handler failed"); }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (!this.IsHandleCreated) this.CreateControl();

        try
        {
            if (_metricsGrid != null && _viewModel != null)
            {
                _metricsGrid.BeginUpdate();
                _metricsGrid.DataSource = _viewModel.Metrics;
                _metricsGrid.EndUpdate();
            }
            _ = LoadAsync();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OverviewTabControl.OnLoad error: {ex.Message}"); _logger.LogError(ex, "[OverviewTabControl] OnLoad failed"); }
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay == null || this.IsDisposed) return;

        if (this.InvokeRequired)
        {
            if (this.IsHandleCreated)
                this.BeginInvoke((MethodInvoker)(() =>
                {
                    if (_loadingOverlay != null && !this.IsDisposed)
                        _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
                }));
        }
        else
        {
            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
        }
    }

    public async Task LoadAsync()
    {
        if (IsLoaded) return;
        try
        {
            if (_viewModel != null) await _viewModel.LoadAsync();
            IsLoaded = true;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Overview LoadAsync error: {ex.Message}"); _logger.LogError(ex, "[OverviewTabControl] LoadAsync failed"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _viewModel!.PropertyChanged -= ViewModel_PropertyChanged; }
            catch { /* suppress */ }
        }
        base.Dispose(disposing);
    }
}
