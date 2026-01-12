using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Themes;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Utils;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Resource strings for BudgetOverviewPanel UI elements.
    /// </summary>
    internal static class BudgetOverviewPanelResources
    {
        public const string PanelTitle = "Budget Overview";
        public const string RefreshText = "Refresh";
        public const string ExportCsvText = "Export CSV";
        public const string FiscalYearLabel = "Fiscal Year:";
        public const string TotalBudgetLabel = "Total Budget";
        public const string TotalActualLabel = "Total Actual";
        public const string VarianceLabel = "Variance";
        public const string OverBudgetLabel = "Over Budget";
        public const string UnderBudgetLabel = "Under Budget";
    }

    /// <summary>
    /// Budget overview panel (UserControl) displaying budget vs actual analysis with
    /// fiscal year filtering, variance tracking, and CSV export capabilities.
    /// Designed for embedding in DockingManager.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class BudgetOverviewPanel : UserControl
    {
        private readonly BudgetOverviewViewModel _vm;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;
        private readonly Services.IThemeIconService? _iconService;

        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        // Controls
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private GradientPanelExt? _topPanel;
        private GradientPanelExt? _summaryPanel;
        private SfComboBox? _comboFiscalYear;
        private SfButton? _btnRefresh;
        private SfButton? _btnExportCsv;
        private SfDataGrid? _metricsGrid;
        private ChartControl? _varianceChart;
        private ChartControlRegionEventWiring? _varianceChartRegionEventWiring;
        private ErrorProvider? _errorProvider;

        // Summary tiles
        private Label? _lblTotalBudget;
        private Label? _lblTotalActual;
        private Label? _lblVariance;
        private Label? _lblOverBudgetCount;
        private Label? _lblUnderBudgetCount;
        private Label? _lblLastUpdated;

        // Event handlers for cleanup
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

        // private EventHandler<AppTheme>? _btnRefreshThemeChangedHandler; // Removed unused
        // private EventHandler<AppTheme>? _btnExportCsvThemeChangedHandler; // Removed unused
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;

        /// <summary>
        /// Parameterless constructor for DI/designer support.
        /// </summary>
        public BudgetOverviewPanel() : this(new BudgetOverviewViewModel(), null)
        {
        }

        // Runtime resolution helpers removed in favor of constructor injection and explicit test/designer fallbacks.

        public BudgetOverviewPanel(BudgetOverviewViewModel vm, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null, Services.IThemeIconService? iconService = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _iconService = iconService;
            DataContext = vm;

            InitializeComponent();

            // Apply theme via SfSkinManager (single source of truth)
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, "Office2019Colorful"); } catch { }
            SetupUI();
            BindViewModel();
            ApplyCurrentTheme();

            // Theme changes are handled by SfSkinManager cascade
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            Name = "BudgetOverviewPanel";
            // Removed manual Size assignment - panel now uses Dock.Fill or AutoSize
            Dock = DockStyle.Fill;
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
            this.ResumeLayout(false);

        }

        private void SetupUI()
        {
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            // Panel header
            _panelHeader = new PanelHeader { Dock = DockStyle.Top };
            _panelHeader.Title = BudgetOverviewPanelResources.PanelTitle;
            _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
            _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
            _panelHeaderCloseHandler = (s, e) => ClosePanel();
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            Controls.Add(_panelHeader);

            // Top toolbar using TableLayoutPanel for proper responsive layout
            _topPanel = new GradientPanelExt
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8, 4, 8, 4),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(_topPanel, "Office2019Colorful");

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                AutoSize = false
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Label
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // ComboBox
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));  // Spacer
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Refresh button
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));  // Spacer
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Export button
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Fiscal year selector label
            var lblFiscalYear = new Label
            {
                Text = BudgetOverviewPanelResources.FiscalYearLabel,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 4, 0)
            };
            toolbar.Controls.Add(lblFiscalYear, 0, 0);

            _comboFiscalYear = new SfComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AccessibleName = "Fiscal year selector",
                AccessibleDescription = "Select the fiscal year to view budget data",
                Margin = new Padding(0, 2, 0, 2)
            };
            _comboFiscalYear.SelectedIndexChanged += ComboFiscalYear_SelectedIndexChanged;
            toolbar.Controls.Add(_comboFiscalYear, 1, 0);

            // Spacer
            toolbar.Controls.Add(new Label(), 2, 0);

            // Refresh button
            _btnRefresh = new SfButton
            {
                Text = BudgetOverviewPanelResources.RefreshText,
                Dock = DockStyle.Fill,
                AccessibleName = "Refresh budget data",
                AccessibleDescription = "Reload budget overview data",
                Margin = new Padding(0, 2, 0, 2)
            };
            SetupRefreshButtonIcon();
            _btnRefresh.Click += async (s, e) => await RefreshDataAsync();
            toolbar.Controls.Add(_btnRefresh, 3, 0);

            // Spacer
            toolbar.Controls.Add(new Label(), 4, 0);

            // Export CSV button
            _btnExportCsv = new SfButton
            {
                Text = "&" + BudgetOverviewPanelResources.ExportCsvText,
                Dock = DockStyle.Fill,
                AccessibleName = "Export to CSV",
                AccessibleDescription = "Export budget data to CSV file (Alt+E)",
                Margin = new Padding(0, 2, 0, 2)
            };
            SetupExportButtonIcon();
            _btnExportCsv.Click += async (s, e) => await ExportToCsvAsync();
            toolbar.Controls.Add(_btnExportCsv, 5, 0);

            _topPanel.Controls.Add(toolbar);
            Controls.Add(_topPanel);

            // Summary panel with KPI tiles
            _summaryPanel = new GradientPanelExt
            {
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(8),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(_summaryPanel, "Office2019Colorful");
            var summaryFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            // Create summary tiles
            _lblTotalBudget = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.TotalBudgetLabel, "$0", Color.DodgerBlue);
            _lblTotalActual = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.TotalActualLabel, "$0", Color.Green);
            _lblVariance = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.VarianceLabel, "$0", Color.Orange);
            _lblOverBudgetCount = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.OverBudgetLabel, "0", Color.Red);
            _lblUnderBudgetCount = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.UnderBudgetLabel, "0", Color.Green);

            _summaryPanel.Controls.Add(summaryFlow);
            Controls.Add(_summaryPanel);

            // Main content area - split between chart and grid
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal
            };
            // Defer setting min sizes and splitter distance until control is sized
            SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(mainSplit, 200, 150, 300);

            // Variance chart
            _varianceChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Budget variance chart",
                AccessibleDescription = "Displays budget vs actual variance by department"
            };
            _varianceChartRegionEventWiring = new ChartControlRegionEventWiring(_varianceChart);
            ConfigureChart();
            mainSplit.Panel1.Controls.Add(_varianceChart);

            // Metrics grid
            _metricsGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowFiltering = true,
                AllowSorting = true,
                AllowGrouping = true,
                ShowRowHeader = false,
                SelectionMode = Syncfusion.WinForms.DataGrid.Enums.GridSelectionMode.Single,
                AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill,
                RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f),
                HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f),
                AllowResizingColumns = true,
                AllowTriStateSorting = true,
                AccessibleName = "Budget metrics grid",
                AccessibleDescription = "Displays budget metrics by department"
            };
            // Theme applied automatically by SkinManager cascade from parent form
            ConfigureGridColumns();

            // Grid theming handled by SkinManager cascade

            mainSplit.Panel2.Controls.Add(_metricsGrid);

            Controls.Add(mainSplit);

            // Last updated label
            _lblLastUpdated = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "Last updated:",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                Padding = new Padding(0, 0, 8, 0)
            };
            Controls.Add(_lblLastUpdated);

            // Loading and no-data overlays
            _loadingOverlay = new LoadingOverlay { Message = "Loading budget data..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No budget data available\r\nAdd budget entries and accounts to see analysis" };
            Controls.Add(_noDataOverlay);
        }

        private Label CreateSummaryTile(FlowLayoutPanel parent, string title, string value, Color accentColor)
        {
            var tile = new GradientPanelExt
            {
                Width = 150,
                Height = 80,
                Margin = new Padding(4),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                AccessibleName = $"{title} summary card",
                AccessibleDescription = $"Displays {title.ToLower(CultureInfo.CurrentCulture)} metric"
            };
            SfSkinManager.SetVisualStyle(tile, "Office2019Colorful");

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter
                // Font and ForeColor inherited from theme cascade
            };
            tile.Controls.Add(lblTitle);

            var lblValue = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AccessibleName = $"{title} value",
                AccessibleDescription = $"Current {title.ToLower(CultureInfo.CurrentCulture)}: {value}"
                // Font and ForeColor inherited from theme cascade
            };
            tile.Controls.Add(lblValue);

            parent.Controls.Add(tile);
            return lblValue;
        }

        private void ConfigureChart()
        {
            if (_varianceChart == null) return;

            // Theme applied automatically by SfSkinManager cascade from parent form
            ChartControlDefaults.Apply(_varianceChart);
            _varianceChart.ShowLegend = true;
            _varianceChart.LegendsPlacement = ChartPlacement.Outside;

            // Configure axes
            _varianceChart.PrimaryXAxis.Title = "Department";
            _varianceChart.PrimaryXAxis.Font = new Font("Segoe UI", 9F);
            _varianceChart.PrimaryXAxis.DrawGrid = false;

            _varianceChart.PrimaryYAxis.Title = "Amount";
            _varianceChart.PrimaryYAxis.Font = new Font("Segoe UI", 9F);
        }

        private void ConfigureGridColumns()
        {
            if (_metricsGrid == null) return;

            _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", MinimumWidth = 150 });
            _metricsGrid.Columns.Add(new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budgeted", MinimumWidth = 120 });
            _metricsGrid.Columns.Add(new GridNumericColumn { MappingName = "Amount", HeaderText = "Actual", MinimumWidth = 120 });
            _metricsGrid.Columns.Add(new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", MinimumWidth = 120 });
            _metricsGrid.Columns.Add(new GridTextColumn { MappingName = "VariancePercent", HeaderText = "Variance %", MinimumWidth = 100 });
        }

        private void SetupRefreshButtonIcon()
        {
            try
            {
                var iconService = _iconService;
                var theme = AppTheme.Office2019Colorful; // ThemeManager removed, use default
                if (_btnRefresh != null)
                {
                    _btnRefresh.Image = iconService?.GetIcon("refresh", theme, 14);
                    _btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                    _btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;
                }

                // Theme subscription removed - handled by SfSkinManager
            }
            catch { }
        }

        private void SetupExportButtonIcon()
        {
            try
            {
                var iconService = _iconService;
                // Use default theme for icon selection
                if (_btnExportCsv != null)
                {
                    _btnExportCsv.Image = iconService?.GetIcon("export", WileyWidget.WinForms.Theming.AppTheme.Light, 14);
                    _btnExportCsv.ImageAlign = ContentAlignment.MiddleLeft;
                    _btnExportCsv.TextImageRelation = TextImageRelation.ImageBeforeText;
                }

                // Theme subscription removed - handled by SfSkinManager
            }
            catch { }
        }

        private void UpdateButtonIcon(SfButton? button, Image? icon)
        {
            if (button == null) return;
            if (_dispatcherHelper != null)
            {
                _ = _dispatcherHelper.InvokeAsync(() => button.Image = icon);
            }
            else if (button.InvokeRequired)
            {
                button.Invoke(() => button.Image = icon);
            }
            else
            {
                button.Image = icon;
            }
        }

        private void BindViewModel()
        {
            // Bind fiscal years combo
            try
            {
                if (_comboFiscalYear != null && _vm.AvailableFiscalYears.Any())
                {
                    _comboFiscalYear.DataSource = _vm.AvailableFiscalYears.ToList();
                    _comboFiscalYear.SelectedItem = _vm.SelectedFiscalYear;
                }
            }
            catch { }

            // Subscribe to property changes
            _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
            _vm.PropertyChanged += _viewModelPropertyChangedHandler;

            // Subscribe to metrics collection changes
            _vm.Metrics.CollectionChanged -= (s, e) => UpdateUI();  // Remove old
            _vm.Metrics.CollectionChanged += BudgetMetrics_CollectionChanged;  // Add safe handler

            // Initial UI update
            UpdateUI();
        }

        private void BudgetMetrics_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke((MethodInvoker)(() => BudgetMetrics_CollectionChanged(sender, e)));
                }
                return;
            }
            UpdateUI();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    _ = _dispatcherHelper.InvokeAsync(() => ViewModel_PropertyChanged(sender, e));
                    return;
                }
                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
                    }
                    return;
                }

                switch (e.PropertyName)
                {
                    case nameof(_vm.IsLoading):
                        if (_loadingOverlay != null) _loadingOverlay.Visible = _vm.IsLoading;
                        break;

                    case nameof(_vm.Metrics):
                    case nameof(_vm.TotalBudgeted):
                    case nameof(_vm.TotalActual):
                    case nameof(_vm.TotalVariance):
                    case nameof(_vm.OverBudgetCount):
                    case nameof(_vm.UnderBudgetCount):
                    case nameof(_vm.LastUpdated):
                        UpdateUI();
                        break;

                    case nameof(_vm.ErrorMessage):
                        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
                        {
                            _errorProvider?.SetError(this, _vm.ErrorMessage);
                        }
                        else
                        {
                            _errorProvider?.SetError(this, string.Empty);
                        }
                        break;
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: PropertyChanged handler failed");
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (IsDisposed) return;

                // Update summary tiles
                if (_lblTotalBudget != null)
                    _lblTotalBudget.Text = _vm.TotalBudgeted.ToString("C0", CultureInfo.CurrentCulture);
                if (_lblTotalActual != null)
                    _lblTotalActual.Text = _vm.TotalActual.ToString("C0", CultureInfo.CurrentCulture);
                if (_lblVariance != null)
                {
                    _lblVariance.Text = _vm.TotalVariance.ToString("C0", CultureInfo.CurrentCulture);
                    _lblVariance.ForeColor = _vm.TotalVariance >= 0 ? Color.Red : Color.Green;  // Semantic: red for over, green for under
                }
                if (_lblOverBudgetCount != null)
                    _lblOverBudgetCount.Text = _vm.OverBudgetCount.ToString(CultureInfo.CurrentCulture);
                if (_lblUnderBudgetCount != null)
                    _lblUnderBudgetCount.Text = _vm.UnderBudgetCount.ToString(CultureInfo.CurrentCulture);
                if (_lblLastUpdated != null)
                    _lblLastUpdated.Text = $"Last updated: {_vm.LastUpdated:yyyy-MM-dd HH:mm:ss}";

                // Update chart
                UpdateChart();

                // Update grid
                if (_metricsGrid != null)
                {
                    try
                    {
                        _metricsGrid.SuspendLayout();
                        var snapshot = _vm.Metrics.ToList();
                        _metricsGrid.DataSource = snapshot;
                        _metricsGrid.ResumeLayout();
                    }
                    catch { }
                }

                // Show no-data overlay if needed
                if (_noDataOverlay != null)
                    _noDataOverlay.Visible = !_vm.IsLoading && !_vm.Metrics.Any();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: UpdateUI failed");
            }
        }

        private void UpdateChart()
        {
            if (_varianceChart == null) return;

            try
            {
                _varianceChart.Series.Clear();

                var budgetSeries = new ChartSeries("Budgeted", ChartSeriesType.Column);
                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Column);

                foreach (var metric in _vm.Metrics.Take(10)) // Limit to top 10 for chart readability
                {
                    budgetSeries.Points.Add(metric.DepartmentName, (double)metric.BudgetedAmount);
                    actualSeries.Points.Add(metric.DepartmentName, (double)metric.Amount);
                }


                _varianceChart.Series.Add(budgetSeries);
                _varianceChart.Series.Add(actualSeries);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: UpdateChart failed");
            }
        }

        private void ComboFiscalYear_SelectedIndexChanged(object? sender, EventArgs e)
        {
            try
            {
                if (_comboFiscalYear?.SelectedItem is int year && year != _vm.SelectedFiscalYear)
                {
                    _vm.SelectedFiscalYear = year;
                }
            }
            catch { }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                await _vm.RefreshCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: Refresh failed");
                MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ExportToCsvAsync()
        {
            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV Files|*.csv",
                    DefaultExt = "csv",
                    FileName = $"budget-overview-{_vm.SelectedFiscalYear}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Department,Budgeted,Actual,Variance,Variance %,Over Budget");

                foreach (var metric in _vm.Metrics)
                {
                    sb.AppendLine(string.Concat(
                        '"', metric.DepartmentName, '"', ",",
                metric.BudgetedAmount.ToString(CultureInfo.InvariantCulture), ",",
                metric.Amount.ToString(CultureInfo.InvariantCulture), ",",
                metric.Variance.ToString(CultureInfo.InvariantCulture), ",",
                metric.VariancePercent.ToString("F2", CultureInfo.InvariantCulture), ",",
                metric.IsOverBudget.ToString(CultureInfo.InvariantCulture)));
                }

                // Summary row
                sb.AppendLine();
                sb.AppendLine(string.Concat(
                    '"', "TOTAL", '"', ",",
                    _vm.TotalBudgeted.ToString(CultureInfo.InvariantCulture), ",",
                    _vm.TotalActual.ToString(CultureInfo.InvariantCulture), ",",
                    _vm.TotalVariance.ToString(CultureInfo.InvariantCulture), ",",
                    _vm.OverallVariancePercent.ToString("F2", CultureInfo.InvariantCulture), ","));

                await File.WriteAllTextAsync(sfd.FileName, sb.ToString());
                MessageBox.Show($"Exported to {sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetOverviewPanel: Export to CSV failed");
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: ClosePanel failed");
            }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            try
            {
                if (IsDisposed) return;
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    _ = _dispatcherHelper.InvokeAsync(() => OnThemeChanged(sender, theme));
                    return;
                }
                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme)));
                    }
                    return;
                }
                ApplyCurrentTheme();
            }
            catch { }
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                // Theme is applied automatically by SfSkinManager cascade from parent form
                // No manual theme application needed
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                if (!DesignMode && !_vm.IsLoading)
                {
                    // Auto-load data on panel load
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _vm.LoadDataCommand.ExecuteAsync(null);
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, "BudgetOverviewPanel: OnLoad data fetch failed");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetOverviewPanel: OnLoad failed");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from events
                // Theme subscriptions removed - handled by SfSkinManager
                try { if (_viewModelPropertyChangedHandler != null) _vm.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }
                try { _vm.Metrics.CollectionChanged -= BudgetMetrics_CollectionChanged; } catch { }

                try
                {
                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    }
                }
                catch { }

                // Dispose controls
                try { _metricsGrid?.SafeClearDataSource(); } catch { }
                try { _metricsGrid?.SafeDispose(); } catch { }
                try { _varianceChartRegionEventWiring?.Dispose(); } catch { }
                _varianceChartRegionEventWiring = null;
                try { _varianceChart?.Dispose(); } catch { }
                try { _comboFiscalYear?.Dispose(); } catch { }
                try { _btnRefresh?.Dispose(); } catch { }
                try { _btnExportCsv?.Dispose(); } catch { }
                try { _panelHeader?.Dispose(); } catch { }
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }
                try { _errorProvider?.Dispose(); } catch { }
                try { _topPanel?.Dispose(); } catch { }
                try { _summaryPanel?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
