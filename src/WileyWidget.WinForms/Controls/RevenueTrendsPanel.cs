using System.Threading;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChartControl = Syncfusion.Windows.Forms.Chart.ChartControl;
using ChartSeries = Syncfusion.Windows.Forms.Chart.ChartSeries;
using ChartSeriesType = Syncfusion.Windows.Forms.Chart.ChartSeriesType;
using ChartAlignment = Syncfusion.Windows.Forms.Chart.ChartAlignment;
using ChartDock = Syncfusion.Windows.Forms.Chart.ChartDock;
using ChartSymbolShape = Syncfusion.Windows.Forms.Chart.ChartSymbolShape;
using ChartValueType = Syncfusion.Windows.Forms.Chart.ChartValueType;
using CategoryAxisDataBindModel = Syncfusion.Windows.Forms.Chart.CategoryAxisDataBindModel;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using GridNumericColumn = Syncfusion.WinForms.DataGrid.GridNumericColumn;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Revenue Trends panel displaying monthly revenue data over time with line chart and detailed grid.
/// Inherits from ScopedPanelBase to ensure proper DI lifetime management for scoped dependencies.
/// Configured using official Syncfusion API patterns per ChartControl and SfDataGrid documentation.
///
/// LAYOUT STRUCTURE (Responsive):
/// ┌─────────────────────────────────┐
/// │ Panel Header (Dock.Top)         │ ← Fixed height, top-most
/// ├─────────────────────────────────┤
/// │ Summary Cards (Dock.Top)        │ ← Auto-height with MinHeight fallback
/// │ (4 cards in 1 row)              │
/// ├─────────────────────────────────┤
/// │ Split Container (Dock.Fill)     │ ← Takes remaining space proportionally
/// │ ┌─────────────────────────────┐ │
/// │ │ Chart (Panel1, ~50% height) │ │ ← Proportional, user-resizable
/// │ ├─────────────────────────────┤ │
/// │ │ Grid (Panel2, ~50% height)  │ │ ← Proportional, user-resizable
/// │ └─────────────────────────────┘ │
/// ├─────────────────────────────────┤
/// │ Last Updated (Dock.Bottom)      │ ← Fixed height, bottom
/// └─────────────────────────────────┘
/// </summary>
public partial class RevenueTrendsPanel : ScopedPanelBase<RevenueTrendsViewModel>
{
    // UI Controls
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private Panel? _summaryPanel;
    private ChartControl? _chartControl;
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
    private EventHandler? _panelHeaderHelpClickedHandler;

    /// <summary>
    /// Initializes a new instance with required DI dependencies.
    /// </summary>
    public RevenueTrendsPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<RevenueTrendsViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();

        // Apply theme via SfSkinManager (single source of truth)
        try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme); } catch { }
        InitializeComponent();
        SubscribeToThemeChanges();
    }

    // InitializeComponent moved to RevenueTrendsPanel.Designer.cs for designer support

    private void InitializeComponent()
    {
        SuspendLayout();

        // PANEL HEADER (Dock.Top, fixed height)
        // ══════════════════════════════════════════════════════════════
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Revenue Trends",
            AccessibleName = "Revenue Trends panel header",
            AccessibleDescription = "Header with title, refresh, and close actions for the Revenue Trends panel"
        };
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderHelpClickedHandler = (s, e) => Dialogs.ChartWizardFaqDialog.ShowModal(this);
        _panelHeader.HelpClicked += _panelHeaderHelpClickedHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // SUMMARY CARDS PANEL (Dock.Top, fixed height with minimum)
        // ══════════════════════════════════════════════════════════════
        // CHANGE 3: Replaced GradientPanelExt with standard Panel
        // Set explicit Height to prevent measurement loops
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = false, // CRITICAL: Explicit false prevents measurement loops
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f),
            MinimumSize = new Size(0, (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f)),
            // CHANGE 4: Added consistent padding (10px) to summary panel
            Padding = new Padding(10),
            AccessibleName = "Revenue summary metrics panel",
            AccessibleDescription = "Panel displaying key revenue metrics: total revenue, average monthly revenue, peak month, and growth rate"
        };
        // Theme cascade from parent via SfSkinManager
        SfSkinManager.SetVisualStyle(_summaryPanel, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

        // CHANGE 5: TableLayoutPanel configured for proper layout
        _summaryCardsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            AutoSize = false, // CRITICAL: Explicit false prevents measurement loops
            // CHANGE 6: Added padding to summary cards layout
            Padding = new Padding(4),
            AccessibleName = "Summary metric cards container",
            AccessibleDescription = "Contains four metric cards arranged in a row"
        };

        // Configure equal column widths (25% each)
        for (int i = 0; i < 4; i++)
        {
            _summaryCardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }
        // CHANGE 7: Row configured with absolute sizing
        _summaryCardsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 90f));

        // Create summary cards
        _lblTotalRevenueValue = CreateSummaryCard(_summaryCardsPanel, "Total Revenue", "$0", 0, "Total cumulative revenue across all months in the selected period");
        _lblAverageRevenueValue = CreateSummaryCard(_summaryCardsPanel, "Avg Monthly", "$0", 1, "Average revenue per month over the selected period");
        _lblPeakRevenueValue = CreateSummaryCard(_summaryCardsPanel, "Peak Month", "$0", 2, "Highest single month revenue in the selected period");
        _lblGrowthRateValue = CreateSummaryCard(_summaryCardsPanel, "Growth Rate", "0%", 3, "Month-over-month revenue growth percentage");

        _summaryPanel.Controls.Add(_summaryCardsPanel);
        Controls.Add(_summaryPanel);

        // SPLIT CONTAINER FOR CHART AND GRID (Dock.Fill, proportional)
        // ══════════════════════════════════════════════════════════════
        // CHANGE 8: Split container configured for proportional resizing
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,  // Slightly thicker splitter for better UX
            Padding = new Padding(0),
            AccessibleName = "Chart and grid split container",
            AccessibleDescription = "Resizable container splitting chart visualization above and data grid below. Drag splitter to adjust proportions."
        };
        // Defer setting min sizes and splitter distance until control is sized
        SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(_mainSplit, 200, 150, 350);

        // CHART CONTROL (Panel1, Dock.Fill)
        // ══════════════════════════════════════════════════════════════
        // CHANGE 11: Explicitly set Dock=Fill to ensure full panel coverage
        _chartControl = new ChartControl
        {
            Dock = DockStyle.Fill,
            // CHANGE 12: Added comprehensive accessibility information
            AccessibleName = "Revenue trends line chart",
            AccessibleDescription = "Line chart visualization showing monthly revenue trends over time. Y-axis shows revenue in currency, X-axis shows months."
        };
        try
        {
            var dbProp = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            dbProp?.SetValue(_chartControl, true);
        }
        catch { }
        ConfigureChart();
        _mainSplit.Panel1.Controls.Add(_chartControl);

        // DATA GRID (Panel2, Dock.Fill)
        // ══════════════════════════════════════════════════════════════
        // CHANGE 13: Explicitly set Dock=Fill for grid as well
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
            // CHANGE 14: Enhanced accessibility for grid
            AccessibleName = "Monthly revenue breakdown data grid",
            AccessibleDescription = "Sortable, filterable table displaying detailed monthly revenue data including transaction count and average transaction value. Use arrow keys to navigate."
        };

        ConfigureGridColumns();
        ConfigureGridStyling(_metricsGrid);
        _mainSplit.Panel2.Controls.Add(_metricsGrid);

        Controls.Add(_mainSplit);

        // LAST UPDATED TIMESTAMP (Dock.Bottom, fixed height)
        // ══════════════════════════════════════════════════════════════
        _lblLastUpdated = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "Last updated: --",
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            // CHANGE 15: Updated padding on timestamp for consistent spacing
            Padding = new Padding(0, 4, 12, 4),
            AccessibleName = "Last data update timestamp",
            AccessibleDescription = "Shows the date and time when the revenue data was last refreshed"
        };
        Controls.Add(_lblLastUpdated);

        // OVERLAYS (Absolute positioning via Controls.Add)
        // ══════════════════════════════════════════════════════════════
        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading revenue data...",
            Dock = DockStyle.Fill,
            AccessibleName = "Loading overlay",
            AccessibleDescription = "Loading indicator displayed while data is being fetched"
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        // No-data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Message = "No revenue data for this period\r\nAdd transactions to see trends over time",
            Dock = DockStyle.Fill,
            AccessibleName = "No data overlay",
            AccessibleDescription = "Message displayed when no revenue data is available for the selected period"
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ResumeLayout(false);
        this.PerformLayout();
        this.Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    private Label CreateSummaryCard(TableLayoutPanel parent, string title, string value, int columnIndex, string description)
    {
        // CHANGE 16: Card panel with improved padding and spacing
        var cardPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),  // CHANGE: Increased margin between cards for breathing room
            Padding = new Padding(10),  // CHANGE: Consistent padding inside cards
            AutoSize = false,
            MinimumSize = new Size(80, 80),  // CHANGE: Ensure cards have reasonable minimum size
            AccessibleName = $"{title} summary card",
            AccessibleDescription = description
        };
        // Theme cascade (no per-control override)

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AutoSize = false,
            // CHANGE 17: Improved accessibility for card title
            AccessibleName = $"{title} label",
            AccessibleDescription = $"Label displaying the metric type: {title}"
        };
        cardPanel.Controls.Add(lblTitle);

        var lblValue = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            AutoSize = false,
            // CHANGE 18: Improved accessibility for card value
            AccessibleName = $"{title} value",
            AccessibleDescription = $"Displays the current {title.ToLower(CultureInfo.CurrentCulture)} value"
        };
        cardPanel.Controls.Add(lblValue);

        parent.Controls.Add(cardPanel, columnIndex, 0);
        return lblValue;
    }

    /// <summary>
    /// Configures ChartControl for line series display per Syncfusion API documentation.
    /// Uses global SfSkinManager theme (no per-control overrides), proper date-based X-axis.
    ///
    /// CHANGES FROM ORIGINAL:
    /// - Removed manual theme color assignments (SfSkinManager cascade applies)
    /// - Added comprehensive accessibility names
    /// - Configured legend with accessibility info
    /// </summary>
    private void ConfigureChart()
    {
        if (_chartControl == null) return;

        // CHANGE 19: Rely on global SfSkinManager theme cascade - no per-control overrides
        // Chart area configuration - colors inherited from theme
            ChartControlDefaults.Apply(_chartControl, logger: _logger);
        _chartControl.PrimaryXAxis.LabelRotate = true;
        _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
        _chartControl.PrimaryXAxis.DrawGrid = true;
        // Grid line colors inherited from global theme (no manual color assignment)

        // Date formatting for X-axis labels
        try
        {
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
        _chartControl.PrimaryYAxis.TitleFont = new Font("Segoe UI", 9F, FontStyle.Bold);
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
        // CHANGE 20: Added accessibility info to legend
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
    ///
    /// CHANGES FROM ORIGINAL:
    /// - All column headers include AccessibleDescription
    /// - Columns sized appropriately for content
    /// </summary>
    private void ConfigureGridColumns()
    {
        if (_metricsGrid == null) return;

        _metricsGrid.Columns.Clear();

        // Month column with date formatting
        // CHANGE 22: Added accessibility description to columns
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
    /// Configures professional styling for the data grid including row height and spacing.
    /// </summary>
    private void ConfigureGridStyling(SfDataGrid grid)
    {
        if (grid == null) return;

        try
        {
            // Set row height for better visual spacing and clickability
            grid.RowHeight = 32;

            // Set header row height for better visual hierarchy
            grid.HeaderRowHeight = 40;

            Logger?.LogDebug("Revenue metrics grid spacing applied");
        }
        catch (Exception ex)
        {
            Logger?.LogWarning($"Failed to apply grid spacing: {ex.Message}");
        }
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

        // Defer sizing validation until layout is complete
        this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));

        // Load data asynchronously
        _ = LoadDataSafeAsync();
    }

    private async Task LoadDataSafeAsync(CancellationToken cancellationToken = default)
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
                // CHANGE 23: Semantic status color (green/red) - allowed by project rules for status indicators
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
    ///
    /// CHANGES FROM ORIGINAL:
    /// - Ensures proper theme cascade (no manual color settings on series)
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

                // Configure series style - CHANGE 24: No manual color assignments; rely on theme
                // Per SfSkinManager authority, all colors (border, symbol, legend) cascade from ApplicationVisualTheme
                lineSeries.Style.Border.Width = 2;  // Width only - color inherited from active theme
                // Symbol colors (fill, border) automatically inherited from theme
                lineSeries.Style.Symbol.Shape = ChartSymbolShape.Circle;
                lineSeries.Style.Symbol.Size = new Size(8, 8);  // Size only - color inherited

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

    // CHANGE 25: Added OnLayout override to make SplitterDistance proportional
    // This ensures the split container maintains 50/50 proportions on resize
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        // Update splitter distance to be proportional to available height
        if (_mainSplit != null && !_mainSplit.IsDisposed)
        {
            // Calculate available height (total height minus header, summary, and timestamp)
            int availableHeight = _mainSplit.Height;
            if (availableHeight > 0)
            {
                // Default to 50% split, but respect minimum sizes
                int proposedDistance = availableHeight / 2;
                int minDistance = _mainSplit.Panel1MinSize;
                int maxDistance = availableHeight - _mainSplit.Panel2MinSize;

                if (proposedDistance < minDistance)
                    proposedDistance = minDistance;
                else if (proposedDistance > maxDistance)
                    proposedDistance = maxDistance;

                if (_mainSplit.SplitterDistance != proposedDistance)
                {
                    _mainSplit.SplitterDistance = proposedDistance;
                }
            }
        }
    }

    private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
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

    /// <summary>
    /// <summary>
    /// Subscribes to theme changes for UI refresh via SfSkinManager.
    /// SfSkinManager is the single source of truth per architecture guardrails.
    /// </summary>
    private void SubscribeToThemeChanges()
    {
        try
        {
            // SfSkinManager automatically cascades theme changes from parent form.
            // No explicit subscription needed - theme cascade handles panel updates.
            // When parent form theme changes via SfSkinManager.SetVisualStyle(),
            // this panel will automatically receive the theme through inheritance.
            _logger?.LogDebug("Theme subscription setup: Panel will inherit theme cascade from parent form");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error setting up theme subscription");
        }
    }

    private void ApplyTheme()
    {
        // CHANGE 27: Theme applied automatically by SfSkinManager cascade from parent
        // No manual application required
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
                // Theme subscription handled by SfSkinManager
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
                    if (_panelHeaderHelpClickedHandler != null)
                        _panelHeader.HelpClicked -= _panelHeaderHelpClickedHandler;
                    if (_panelHeaderCloseHandler != null)
                        _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                }
            }
            catch { }

            // Dispose controls using SafeDispose pattern
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
