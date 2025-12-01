using System;
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
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Extensions;

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

        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        // Controls
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private Panel? _topPanel;
        private Panel? _summaryPanel;
        private SfComboBox? _comboFiscalYear;
        private Syncfusion.WinForms.Controls.SfButton? _btnRefresh;
        private Syncfusion.WinForms.Controls.SfButton? _btnExportCsv;
        private SfDataGrid? _metricsGrid;
        private ChartControl? _varianceChart;
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
        private EventHandler<AppTheme>? _themeChangedHandler;
        private EventHandler<AppTheme>? _btnRefreshThemeChangedHandler;
        private EventHandler<AppTheme>? _btnExportCsvThemeChangedHandler;
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _metricsCollectionChangedHandler;

        /// <summary>
        /// Parameterless constructor for DI/designer support.
        /// </summary>
        public BudgetOverviewPanel() : this(ResolveBudgetOverviewViewModel(), ResolveDispatcherHelper())
        {
        }

        private static BudgetOverviewViewModel ResolveBudgetOverviewViewModel()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Warning("BudgetOverviewPanel: Program.Services is null - using fallback ViewModel");
                return new BudgetOverviewViewModel();
            }
            try
            {
                var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<BudgetOverviewViewModel>(Program.Services);
                if (vm != null)
                {
                    Serilog.Log.Debug("BudgetOverviewPanel: BudgetOverviewViewModel resolved from DI container");
                    return vm;
                }
                Serilog.Log.Warning("BudgetOverviewPanel: BudgetOverviewViewModel not registered - using fallback instance");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetOverviewPanel: Failed to resolve BudgetOverviewViewModel from DI");
            }
            return new BudgetOverviewViewModel();
        }

        private static WileyWidget.Services.Threading.IDispatcherHelper? ResolveDispatcherHelper()
        {
            if (Program.Services == null) return null;
            try
            {
                return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services);
            }
            catch { return null; }
        }

        public BudgetOverviewPanel(BudgetOverviewViewModel vm, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = vm;

            InitializeComponent();
            SetupUI();
            BindViewModel();
            ApplyCurrentTheme();

            // Subscribe to theme changes
            _themeChangedHandler = OnThemeChanged;
            ThemeManager.ThemeChanged += _themeChangedHandler;
        }

        private void InitializeComponent()
        {
            Name = "BudgetOverviewPanel";
            Size = new Size(1000, 700);
            Dock = DockStyle.Fill;
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
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

            // Top toolbar
            _topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };

            // Fiscal year selector
            var lblFiscalYear = new Label { Text = BudgetOverviewPanelResources.FiscalYearLabel, AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 4, 0) };
            flow.Controls.Add(lblFiscalYear);

            _comboFiscalYear = new SfComboBox
            {
                Width = 100,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Fiscal year selector",
                AccessibleDescription = "Select the fiscal year to view budget data"
            };
            _comboFiscalYear.SelectedIndexChanged += ComboFiscalYear_SelectedIndexChanged;
            flow.Controls.Add(_comboFiscalYear);

            // Refresh button
            _btnRefresh = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = BudgetOverviewPanelResources.RefreshText,
                Width = 100,
                Height = 28,
                AccessibleName = "Refresh budget data",
                AccessibleDescription = "Reload budget overview data"
            };
            SetupRefreshButtonIcon();
            _btnRefresh.Click += async (s, e) => await RefreshDataAsync();
            flow.Controls.Add(_btnRefresh);

            // Export CSV button
            _btnExportCsv = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = BudgetOverviewPanelResources.ExportCsvText,
                Width = 110,
                Height = 28,
                AccessibleName = "Export to CSV",
                AccessibleDescription = "Export budget data to CSV file"
            };
            SetupExportButtonIcon();
            _btnExportCsv.Click += async (s, e) => await ExportToCsvAsync();
            flow.Controls.Add(_btnExportCsv);

            _topPanel.Controls.Add(flow);
            Controls.Add(_topPanel);

            // Summary panel with KPI tiles
            _summaryPanel = new Panel { Dock = DockStyle.Top, Height = 100, Padding = new Padding(8), BackColor = Color.Transparent };
            var summaryFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            // Create summary tiles
            _lblTotalBudget = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.TotalBudgetLabel, "$0", Color.FromArgb(66, 133, 244));
            _lblTotalActual = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.TotalActualLabel, "$0", Color.FromArgb(52, 168, 83));
            _lblVariance = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.VarianceLabel, "$0", Color.FromArgb(251, 188, 5));
            _lblOverBudgetCount = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.OverBudgetLabel, "0", Color.FromArgb(234, 67, 53));
            _lblUnderBudgetCount = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.UnderBudgetLabel, "0", Color.FromArgb(52, 168, 83));

            _summaryPanel.Controls.Add(summaryFlow);
            Controls.Add(_summaryPanel);

            // Main content area - split between chart and grid
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300,
                Panel1MinSize = 200,
                Panel2MinSize = 150
            };

            // Variance chart
            _varianceChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Budget variance chart",
                AccessibleDescription = "Displays budget vs actual variance by department"
            };
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
            ConfigureGridColumns();
            mainSplit.Panel2.Controls.Add(_metricsGrid);

            Controls.Add(mainSplit);

            // Last updated label
            _lblLastUpdated = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "Last updated: —",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                Padding = new Padding(0, 0, 8, 0)
            };
            Controls.Add(_lblLastUpdated);

            // Loading and no-data overlays
            _loadingOverlay = new LoadingOverlay { Message = "Loading budget data..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No budget data available" };
            Controls.Add(_noDataOverlay);
        }

        private Label CreateSummaryTile(FlowLayoutPanel parent, string title, string value, Color accentColor)
        {
            var tile = new Panel { Width = 150, Height = 80, Margin = new Padding(4), BackColor = Color.FromArgb(40, accentColor) };

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ThemeManager.Colors.TextPrimary
            };
            tile.Controls.Add(lblTitle);

            var lblValue = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = accentColor
            };
            tile.Controls.Add(lblValue);

            parent.Controls.Add(tile);
            return lblValue;
        }

        private void ConfigureChart()
        {
            if (_varianceChart == null) return;

            _varianceChart.Skins = Skins.Metro;
            _varianceChart.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            _varianceChart.BorderAppearance.SkinStyle = ChartBorderSkinStyle.None;
            _varianceChart.ShowToolTips = true;
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
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = ThemeManager.CurrentTheme;
                if (_btnRefresh != null)
                {
                    _btnRefresh.Image = iconService?.GetIcon("refresh", theme, 14);
                    _btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                    _btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;
                }

                _btnRefreshThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        UpdateButtonIcon(_btnRefresh, svc?.GetIcon("refresh", t, 14));
                    }
                    catch { }
                };
                ThemeManager.ThemeChanged += _btnRefreshThemeChangedHandler;
            }
            catch { }
        }

        private void SetupExportButtonIcon()
        {
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = ThemeManager.CurrentTheme;
                if (_btnExportCsv != null)
                {
                    _btnExportCsv.Image = iconService?.GetIcon("export", theme, 14);
                    _btnExportCsv.ImageAlign = ContentAlignment.MiddleLeft;
                    _btnExportCsv.TextImageRelation = TextImageRelation.ImageBeforeText;
                }

                _btnExportCsvThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        UpdateButtonIcon(_btnExportCsv, svc?.GetIcon("export", t, 14));
                    }
                    catch { }
                };
                ThemeManager.ThemeChanged += _btnExportCsvThemeChangedHandler;
            }
            catch { }
        }

        private void UpdateButtonIcon(Syncfusion.WinForms.Controls.SfButton? button, Image? icon)
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
            _metricsCollectionChangedHandler = (s, e) => UpdateUI();
            _vm.Metrics.CollectionChanged += _metricsCollectionChangedHandler;

            // Initial UI update
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
                    BeginInvoke(new Action(() => ViewModel_PropertyChanged(sender, e)));
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
                    _lblVariance.ForeColor = _vm.TotalVariance >= 0 ? Color.FromArgb(234, 67, 53) : Color.FromArgb(52, 168, 83);
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

                budgetSeries.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(66, 133, 244));
                actualSeries.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(52, 168, 83));

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
                if (parentForm is WileyWidget.WinForms.Forms.MainForm mainForm)
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
                    BeginInvoke(new Action(() => OnThemeChanged(sender, theme)));
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
                ThemeManager.ApplyThemeToControl(this);
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from events
                try { ThemeManager.ThemeChanged -= _themeChangedHandler; } catch { }
                try { ThemeManager.ThemeChanged -= _btnRefreshThemeChangedHandler; } catch { }
                try { ThemeManager.ThemeChanged -= _btnExportCsvThemeChangedHandler; } catch { }
                try { if (_viewModelPropertyChangedHandler != null) _vm.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }
                try { if (_metricsCollectionChangedHandler != null) _vm.Metrics.CollectionChanged -= _metricsCollectionChangedHandler; } catch { }

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
