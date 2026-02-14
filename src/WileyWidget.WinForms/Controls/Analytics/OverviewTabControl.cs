using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Windows.Forms.Chart;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;

using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;

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
    private LegacyGradientPanel? _toolbarPanel;
    private SfComboBox? _fiscalYearComboBox;
    private SfButton? _refreshButton;
    private SfButton? _exportButton;

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

    public OverviewTabControl(OverviewTabViewModel? viewModel, AnalyticsHubViewModel? parentViewModel)
    {
        _viewModel = viewModel;
        _parentViewModel = parentViewModel;

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
        _toolbarPanel = new LegacyGradientPanel
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
        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        _fiscalYearComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            ThemeName = themeName,
            DisplayMember = null,  // Use default ToString for List<int> integers
            ValueMember = null,    // Use value itself without explicit member mapping
            AllowNull = false,     // Require selection
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
        };

        // Buttons
        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _refreshButton = new SfButton { Text = "Refresh", Width = 80, ThemeName = themeName };
        _exportButton = new SfButton { Text = "Export CSV", Width = 100, ThemeName = themeName };

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
        _summaryPanel = new LegacyGradientPanel
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

        // Create summary tiles - CreateSummaryTile now adds tile to parent and returns the value label
        _lblTotalBudget = CreateSummaryTile(flow, "Total Budget", "$0", Color.DodgerBlue);
        _lblTotalActual = CreateSummaryTile(flow, "Total Actual", "$0", Color.Green);
        _lblVariance = CreateSummaryTile(flow, "Variance", "$0", Color.Orange);
        _lblOverBudgetCount = CreateSummaryTile(flow, "Over Budget", "0", Color.Red);
        _lblUnderBudgetCount = CreateSummaryTile(flow, "Under Budget", "0", Color.Green);

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
        var themeName = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        _metricsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowGrouping = true,
            AutoGenerateColumns = false,
            ThemeName = themeName  // Apply theme to grid
        }.PreventStringRelationalFilters(null, "DepartmentName");

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

    private Label CreateSummaryTile(FlowLayoutPanel parent, string title, string value, Color accentColor)
    {
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
        var tile = new LegacyGradientPanel
        {
            Width = 150,
            Height = 80,
            Margin = new Padding(5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            AccessibleName = $"{title} summary card",
            AccessibleDescription = $"Displays {title.ToLowerInvariant()} metric"
        };
        SfSkinManager.SetVisualStyle(tile, currentTheme);
        tile.ThemeName = currentTheme;

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter
            // Font and ForeColor inherited from theme cascade
        };
        tile.Controls.Add(titleLabel);

        var valueLabel = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AccessibleName = $"{title} value",
            AccessibleDescription = $"Current {title.ToLowerInvariant()}: {value}"
            // Font and ForeColor inherited from theme cascade
        };
        tile.Controls.Add(valueLabel);

        parent.Controls.Add(tile);
        return valueLabel;
    }

    private void BindViewModel()
    {
        try
        {
            if (_viewModel == null || _parentViewModel == null) return;

            // Ensure handle is created before binding
            if (!this.IsHandleCreated)
            {
                this.CreateControl();
            }

            // Bind fiscal year combo to parent view model - use direct binding, not DataBindings
            if (_fiscalYearComboBox != null && !this.IsDisposed)
            {
                try
                {
                    var yearsList = new List<int>(_parentViewModel.FiscalYears);
                    yearsList.Sort();
                    _fiscalYearComboBox.DataSource = yearsList;

                    // Set initial selection
                    var selectedIndex = yearsList.IndexOf(_parentViewModel.SelectedFiscalYear);
                    if (selectedIndex >= 0)
                    {
                        _fiscalYearComboBox.SelectedIndex = selectedIndex;
                    }
                    else if (yearsList.Count > 0)
                    {
                        _fiscalYearComboBox.SelectedIndex = 0;
                    }

                    // Wire up selection change handler with proper thread safety
                    _fiscalYearComboBox.SelectedIndexChanged += FiscalYear_SelectedIndexChanged;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error binding fiscal year combo: {ex.Message}");
                }
            }

            // Subscribe to fiscal year changes from parent with thread safety
            if (_parentViewModel != null)
            {
                _parentViewModel.PropertyChanged += ParentViewModel_PropertyChanged;
            }

            // Bind summary tiles - use friendly Labels, not gradient panels
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

            // Bind grid with performance optimization
            if (_metricsGrid != null && !this.IsDisposed)
            {
                try
                {
                    _metricsGrid.BeginUpdate();
                    _metricsGrid.DataSource = _viewModel.Metrics;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error binding metrics grid: {ex.Message}");
                }
                finally
                {
                    _metricsGrid.EndUpdate();
                }
            }

            // Bind loading state with proper thread safety
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            UpdateLoadingState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error binding OverviewTabControl ViewModel: {ex.Message}");
        }
    }

    private void FiscalYear_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (_fiscalYearComboBox?.SelectedItem is int year && _parentViewModel != null)
            {
                _parentViewModel.SelectedFiscalYear = year;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in FiscalYear_SelectedIndexChanged: {ex.Message}");
        }
    }

    private void ParentViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(_parentViewModel.FiscalYears) && _fiscalYearComboBox != null && !this.IsDisposed)
            {
                if (this.InvokeRequired && this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        if (_parentViewModel != null && _fiscalYearComboBox != null && !this.IsDisposed)
                        {
                            try
                            {
                                var years = new List<int>(_parentViewModel.FiscalYears);
                                years.Sort();
                                _fiscalYearComboBox.DataSource = years;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error rebinding fiscal years: {ex.Message}");
                            }
                        }
                    }));
                }
                else if (!this.InvokeRequired)
                {
                    try
                    {
                        var years = new List<int>(_parentViewModel.FiscalYears);
                        years.Sort();
                        _fiscalYearComboBox.DataSource = years;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error rebinding fiscal years: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ParentViewModel_PropertyChanged: {ex.Message}");
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading) && !this.IsDisposed)
            {
                UpdateLoadingState();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ViewModel_PropertyChanged: {ex.Message}");
        }
    }

    private void OnRefresh()
    {
        try
        {
            // Refresh button click handler
            _ = LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnRefresh: {ex.Message}");
        }
    }

    private void OnExport()
    {
        try
        {
            // Export button click handler - to be implemented
            System.Diagnostics.Debug.WriteLine("Export functionality to be implemented");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnExport: {ex.Message}");
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Ensure handle is created before accessing controls
        if (!this.IsHandleCreated)
        {
            this.CreateControl();
        }

        try
        {
            // Set up initial data binding timing
            if (_metricsGrid != null && _viewModel != null)
            {
                // Wire up grid data source refresh on load
                _metricsGrid.BeginUpdate();
                _metricsGrid.DataSource = _viewModel.Metrics;
                _metricsGrid.EndUpdate();
            }

            // Load initial data
            _ = LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OverviewTabControl.OnLoad: {ex.Message}");
        }
    }

    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null && !this.IsDisposed)
        {
            if (this.InvokeRequired)
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        if (_loadingOverlay != null && !this.IsDisposed)
                        {
                            _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
                        }
                    }));
                }
            }
            else
            {
                _loadingOverlay.Visible = _viewModel?.IsLoading ?? false;
            }
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
            System.Diagnostics.Debug.WriteLine($"Failed to load overview tab: {ex.Message}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Unsubscribe from events
                if (_refreshButton != null)
                {
                    _refreshButton.Click -= (s, e) => OnRefresh();
                }
                if (_exportButton != null)
                {
                    _exportButton.Click -= (s, e) => OnExport();
                }
                if (_fiscalYearComboBox != null)
                {
                    _fiscalYearComboBox.SelectedIndexChanged -= (s, e) => { /* handled in BindViewModel */ };
                }
            }
            catch { /* Suppress errors during disposal */ }
        }

        base.Dispose(disposing);
    }
}
