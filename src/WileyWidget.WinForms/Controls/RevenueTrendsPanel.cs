using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Revenue Trends panel displaying monthly revenue data over time with line chart and detailed grid.
/// Inherits from ScopedPanelBase to ensure proper DI lifetime management for scoped dependencies.
/// Configured using official Syncfusion API patterns per ChartControl and SfDataGrid documentation.
/// </summary>
public partial class RevenueTrendsPanel : ScopedPanelBase<RevenueTrendsViewModel>
{
    // UI Controls
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private GradientPanelExt? _summaryPanel;
    private ChartControl? _chartControl;
    private ChartControlRegionEventWiring? _chartRegionEventWiring;
    private SfDataGrid? _metricsGrid;
    private SplitContainer? _mainSplit;
    private TableLayoutPanel? _summaryCardsPanel;

    // Chart binding state (Syncfusion-recommended: bind model + batched updates)
    private ChartSeries? _monthlyRevenueSeries;
    private CategoryAxisDataBindModel? _monthlyRevenueBindModel;

    private sealed class RevenueChartPoint
    {
        public RevenueChartPoint(DateTime month, double revenue)
        {
            Month = month;
            Revenue = revenue;
        }

        public DateTime Month { get; }

        public double Revenue { get; }
    }

    // Summary metric labels
    private Label? _lblTotalRevenueValue;
    private Label? _lblAverageRevenueValue;
    private Label? _lblPeakRevenueValue;
    private Label? _lblGrowthRateValue;
    private Label? _lblLastUpdated;

    // Event handlers for cleanup
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _monthlyDataCollectionChangedHandler;

    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;

    /// <summary>
    /// Initializes a new instance with required DI dependencies.
    /// </summary>
    public RevenueTrendsPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<RevenueTrendsViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
        SetupUI();
        SubscribeToThemeChanges();
    }

    private void InitializeComponent()
    {
        Name = "RevenueTrendsPanel";
        Size = new Size(1000, 700);
        MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
        AutoScroll = true;
        Padding = new Padding(8);
        Dock = DockStyle.Fill;
        AccessibleName = "Revenue Trends Panel";
        AccessibleDescription = "Displays monthly revenue trends with line chart and detailed breakdown";

        try
        {
            AutoScaleMode = AutoScaleMode.Dpi;
        }
        catch
        {
            // Fall back if DPI scaling not supported
        }
    }

    private void SetupUI()
    {
        SuspendLayout();

        // Panel header with title and actions
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Revenue Trends",
            AccessibleName = "Revenue Trends header"
        };
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Summary cards panel (top section)
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 100,
            Padding = new Padding(8),
            AccessibleName = "Summary metrics panel"
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, "Office2019Colorful");

        _summaryCardsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            AutoSize = true,
            AccessibleName = "Summary cards"
        };

        // Configure equal column widths
        for (int i = 0; i < 4; i++)
        {
            _summaryCardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }
        _summaryCardsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Create summary cards
        _lblTotalRevenueValue = CreateSummaryCard(_summaryCardsPanel, "Total Revenue", "$0", 0, "Total revenue across all months");
        _lblAverageRevenueValue = CreateSummaryCard(_summaryCardsPanel, "Avg Monthly", "$0", 1, "Average monthly revenue");
        _lblPeakRevenueValue = CreateSummaryCard(_summaryCardsPanel, "Peak Month", "$0", 2, "Highest monthly revenue");
        _lblGrowthRateValue = CreateSummaryCard(_summaryCardsPanel, "Growth Rate", "0%", 3, "Revenue growth rate over period");

        _summaryPanel.Controls.Add(_summaryCardsPanel);
        Controls.Add(_summaryPanel);

        // Split container for chart and grid
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350,
            Panel1MinSize = 200,
            Panel2MinSize = 150,
            AccessibleName = "Chart and grid container"
        };

        // Chart control - configured per Syncfusion API best practices
        _chartControl = new ChartControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Revenue trends line chart",
            AccessibleDescription = "Line chart showing revenue trends over time"
        };
        _chartRegionEventWiring = new ChartControlRegionEventWiring(_chartControl);
        ConfigureChart();
        _mainSplit.Panel1.Controls.Add(_chartControl);

        // Data grid for monthly breakdown
        _metricsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowFiltering = true,
            AllowSorting = true,
            AllowGrouping = false,
            ShowRowHeader = false,
            SelectionMode = GridSelectionMode.Single,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f),
            HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f),
            AllowResizingColumns = true,
            AllowTriStateSorting = true,
            AccessibleName = "Monthly revenue breakdown grid",
            AccessibleDescription = "Grid displaying detailed monthly revenue data"
        };

        ConfigureGridColumns();
        _mainSplit.Panel2.Controls.Add(_metricsGrid);

        Controls.Add(_mainSplit);

        // Last updated timestamp label
        _lblLastUpdated = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "Last updated: --",
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            Padding = new Padding(0, 0, 8, 0),
            AccessibleName = "Last updated timestamp"
        };
        Controls.Add(_lblLastUpdated);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading revenue data...",
            AccessibleName = "Loading overlay"
        };
        Controls.Add(_loadingOverlay);

        // No-data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Message = "No revenue data for this period\r\nAdd transactions to see trends over time",
            AccessibleName = "No data overlay"
        };
        Controls.Add(_noDataOverlay);

        ResumeLayout(false);
    }

    private Label CreateSummaryCard(TableLayoutPanel parent, string title, string value, int columnIndex, string description)
    {
        var cardPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(8),
            AccessibleName = $"{title} card",
            AccessibleDescription = description
        };
        SfSkinManager.SetVisualStyle(cardPanel, "Office2019Colorful");

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AutoSize = false,
            AccessibleName = $"{title} label"
        };
        cardPanel.Controls.Add(lblTitle);

        var lblValue = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            AutoSize = false,
            AccessibleName = $"{title} value"
        };
        cardPanel.Controls.Add(lblValue);

        parent.Controls.Add(cardPanel, columnIndex, 0);
        return lblValue;
    }

    /// <summary>
    /// Configures ChartControl for line series display per Syncfusion API documentation.
    /// Uses global SfSkinManager theme (no per-control overrides), proper date-based X-axis.
    /// </summary>
    private void ConfigureChart()
    {
        if (_chartControl == null) return;

        // Rely on global SfSkinManager theme per project punchlist rules - NO per-control theme overrides
        // Chart area configuration - no manual colors, rely on theme
        ChartControlDefaults.Apply(_chartControl);

        // Configure X-axis for date values per Syncfusion datetime axis documentation
        _chartControl.PrimaryXAxis.ValueType = ChartValueType.DateTime;
        _chartControl.PrimaryXAxis.Title = "Month";
        _chartControl.PrimaryXAxis.Font = new Font("Segoe UI", 9F);
        _chartControl.PrimaryXAxis.LabelRotate = true;
        _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
        _chartControl.PrimaryXAxis.DrawGrid = true;
        // Grid line colors inherited from global theme

        // Date formatting for X-axis labels
        try
        {
            // Use reflection to set DateTimeFormat if property exists
            var xAxis = _chartControl.PrimaryXAxis;
            var propDateFormat = xAxis.GetType().GetProperty("DateTimeFormat");
            if (propDateFormat != null && propDateFormat.CanWrite)
            {
                propDateFormat.SetValue(xAxis, "MMM yyyy");
            }
        }
        catch { }

        // Configure Y-axis for currency values
        _chartControl.PrimaryYAxis.Title = "Revenue";
        _chartControl.PrimaryYAxis.Font = new Font("Segoe UI", 9F);
        try
        {
            var yAxis = _chartControl.PrimaryYAxis;
            var propNumFormat = yAxis.GetType().GetProperty("NumberFormat");
            if (propNumFormat != null && propNumFormat.CanWrite)
            {
                propNumFormat.SetValue(yAxis, "C0");
            }
        }
        catch { }

        // Enable legend per Syncfusion best practices
        _chartControl.ShowLegend = true;
        _chartControl.LegendsPlacement = Syncfusion.Windows.Forms.Chart.ChartPlacement.Outside;
        _chartControl.LegendPosition = ChartDock.Bottom;
        _chartControl.LegendAlignment = ChartAlignment.Center;
        _chartControl.Legend.Font = new Font("Segoe UI", 9F);
        // Legend colors inherited from global theme

    }

    /// <summary>
    /// Configures SfDataGrid columns with proper formatting per Syncfusion API.
    /// Uses currency and date formatting as documented in SfDataGrid column configuration.
    /// </summary>
    private void ConfigureGridColumns()
    {
        if (_metricsGrid == null) return;

        _metricsGrid.Columns.Clear();

        // Month column with date formatting
        _metricsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(RevenueMonthlyData.MonthLabel),
            HeaderText = "Month",
            MinimumWidth = 100,
            AllowSorting = true,
            AllowFiltering = true
        });

        // Revenue column with currency formatting per Syncfusion GridNumericColumn docs
        _metricsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(RevenueMonthlyData.Revenue),
            HeaderText = "Revenue",
            MinimumWidth = 120,
            Format = "C2", // Currency format with 2 decimals
            AllowSorting = true
        });

        // Transaction count
        _metricsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(RevenueMonthlyData.TransactionCount),
            HeaderText = "Transactions",
            MinimumWidth = 100,
            Format = "N0", // Numeric format with thousands separator
            AllowSorting = true
        });

        // Average transaction value
        _metricsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(RevenueMonthlyData.AverageTransactionValue),
            HeaderText = "Avg Transaction",
            MinimumWidth = 120,
            Format = "C2",
            AllowSorting = true
        });
    }

    /// <summary>
    /// Called after ViewModel is resolved from scoped service provider.
    /// Binds ViewModel data and initiates data load.
    /// </summary>
    protected override void OnViewModelResolved(RevenueTrendsViewModel viewModel)
    {
        if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
        base.OnViewModelResolved(viewModel);

        // Subscribe to ViewModel property changes
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Subscribe to MonthlyData collection changes
        _monthlyDataCollectionChangedHandler = (s, e) => UpdateUI();
        viewModel.MonthlyData.CollectionChanged += _monthlyDataCollectionChangedHandler;

        // Initial UI update
        UpdateUI();

        // Load data asynchronously
        _ = LoadDataSafeAsync();
    }

    private async Task LoadDataSafeAsync()
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new System.Action(() => ShowError(ex)));
            }
            else
            {
                ShowError(ex);
            }
        }
    }

    private void ShowError(Exception ex)
    {
        MessageBox.Show(
            $"Failed to load revenue data: {ex.Message}",
            "Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed || ViewModel == null) return;

        // Thread-safe UI updates
        if (InvokeRequired)
        {
            BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsLoading):
                    if (_loadingOverlay != null)
                        _loadingOverlay.Visible = ViewModel.IsLoading;
                    break;

                case nameof(ViewModel.TotalRevenue):
                case nameof(ViewModel.AverageRevenue):
                case nameof(ViewModel.PeakRevenue):
                case nameof(ViewModel.GrowthRate):
                case nameof(ViewModel.LastUpdated):
                    UpdateSummaryCards();
                    break;

                case nameof(ViewModel.ErrorMessage):
                    if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                    {
                        MessageBox.Show(
                            ViewModel.ErrorMessage,
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    break;
            }
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed during update
        }
    }

    private void UpdateUI()
    {
        if (IsDisposed || ViewModel == null) return;

        if (InvokeRequired)
        {
            BeginInvoke(new System.Action(UpdateUI));
            return;
        }

        try
        {
            UpdateSummaryCards();
            UpdateChartData();
            UpdateGridData();
            UpdateNoDataOverlay();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed
        }
    }

    private void UpdateSummaryCards()
    {
        if (ViewModel == null) return;

        try
        {
            if (_lblTotalRevenueValue != null)
                _lblTotalRevenueValue.Text = ViewModel.TotalRevenue.ToString("C0", CultureInfo.CurrentCulture);

            if (_lblAverageRevenueValue != null)
                _lblAverageRevenueValue.Text = ViewModel.AverageRevenue.ToString("C0", CultureInfo.CurrentCulture);

            if (_lblPeakRevenueValue != null)
                _lblPeakRevenueValue.Text = ViewModel.PeakRevenue.ToString("C0", CultureInfo.CurrentCulture);

            if (_lblGrowthRateValue != null)
            {
                _lblGrowthRateValue.Text = ViewModel.GrowthRate.ToString("F1", CultureInfo.CurrentCulture) + "%";
                // Semantic status color: green for positive growth, red for negative (allowed by project rules)
                _lblGrowthRateValue.ForeColor = ViewModel.GrowthRate >= 0 ? Color.Green : Color.Red;
            }

            if (_lblLastUpdated != null)
                _lblLastUpdated.Text = $"Last updated: {ViewModel.LastUpdated:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RevenueTrendsPanel: UpdateSummaryCards failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates chart using Syncfusion-recommended data binding model.
    /// Avoids per-point chart updates by binding a data source and batching redraw.
    /// </summary>
    private void UpdateChartData()
    {
        if (_chartControl == null || ViewModel == null) return;

        try
        {
            _chartControl.BeginUpdate();
            try
            {
                _chartControl.Series.Clear();
                _monthlyRevenueSeries = null;
                _monthlyRevenueBindModel = null;

                if (!ViewModel.MonthlyData.Any())
                    return;

                var dataSource = new BindingList<RevenueChartPoint>(
                    ViewModel.MonthlyData
                        .Select(d => new RevenueChartPoint(d.Month, (double)d.Revenue))
                        .ToList());

                var bindModel = new CategoryAxisDataBindModel(dataSource)
                {
                    CategoryName = nameof(RevenueChartPoint.Month),
                    YNames = new[] { nameof(RevenueChartPoint.Revenue) }
                };

                var lineSeries = new ChartSeries("Monthly Revenue", ChartSeriesType.Line)
                {
                    CategoryModel = bindModel
                };

                // Configure series style - rely on theme colors, no manual color assignments
                lineSeries.Style.Border.Width = 2;

                // Markers are OK for monthly granularity; colors inherit from theme
                lineSeries.Style.Symbol.Shape = ChartSymbolShape.Circle;
                lineSeries.Style.Symbol.Size = new Size(8, 8);

                // Configure tooltip format
                lineSeries.PointsToolTipFormat = "{1:C0}";

                _chartControl.Series.Add(lineSeries);
                _monthlyRevenueSeries = lineSeries;
                _monthlyRevenueBindModel = bindModel;
            }
            finally
            {
                _chartControl.EndUpdate();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RevenueTrendsPanel: UpdateChartData failed: {ex.Message}");
        }
    }

    private void UpdateGridData()
    {
        if (_metricsGrid == null || ViewModel == null) return;

        try
        {
            _metricsGrid.SuspendLayout();

            // Create snapshot to avoid collection modification issues
            var snapshot = ViewModel.MonthlyData.ToList();
            _metricsGrid.DataSource = snapshot;

            _metricsGrid.ResumeLayout();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RevenueTrendsPanel: UpdateGridData failed: {ex.Message}");
        }
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        try
        {
            _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.MonthlyData.Any();
        }
        catch
        {
            // Ignore
        }
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to refresh data: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ClosePanel()
    {
        try
        {
            var parentForm = FindForm();
            if (parentForm == null) return;

            // Try to find ClosePanel method on parent form
            var closePanelMethod = parentForm.GetType().GetMethod(
                "ClosePanel",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            closePanelMethod?.Invoke(parentForm, new object[] { Name });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RevenueTrendsPanel: ClosePanel failed: {ex.Message}");
        }
    }

    private void SubscribeToThemeChanges()
    {
        // Theme subscription removed - handled by SfSkinManager

    }

    private void ApplyTheme()
    {
        try
        {
            // Theme is applied automatically by SfSkinManager cascade from parent form
            // No manual application needed
        }
        catch
        {
            // Ignore theme application failures
        }
    }

    /// <summary>
    /// Disposes resources using SafeDispose pattern to prevent disposal errors.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events
            try
            {
                // Theme subscription removed - handled by SfSkinManager
            }
            catch { }

            try
            {
                if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                    ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            }
            catch { }

            try
            {
                if (ViewModel != null && _monthlyDataCollectionChangedHandler != null)
                    ViewModel.MonthlyData.CollectionChanged -= _monthlyDataCollectionChangedHandler;
            }
            catch { }

            try
            {
                if (_panelHeader != null)
                {
                    if (_panelHeaderRefreshHandler != null)
                        _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                    if (_panelHeaderCloseHandler != null)
                        _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                }
            }
            catch { }

            // Dispose controls using SafeDispose pattern
            try { _chartRegionEventWiring?.Dispose(); } catch { }
            _chartRegionEventWiring = null;
            try { _chartControl?.Dispose(); } catch { }
            try { _metricsGrid?.SafeClearDataSource(); } catch { }
            try { _metricsGrid?.SafeDispose(); } catch { }
            try { _panelHeader?.Dispose(); } catch { }
            try { _loadingOverlay?.Dispose(); } catch { }
            try { _noDataOverlay?.Dispose(); } catch { }
            try { _summaryCardsPanel?.Dispose(); } catch { }
            try { _summaryPanel?.Dispose(); } catch { }
            try { _mainSplit?.Dispose(); } catch { }
        }

        base.Dispose(disposing);
    }
}
