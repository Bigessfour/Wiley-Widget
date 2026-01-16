using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Themes;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Utils;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Budget analytics panel displaying budget variance trends, department performance,
    /// and financial forecasting. Uses proper Syncfusion API (Dock-based layout, GradientPanelExt per docs).
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class BudgetAnalyticsPanel : UserControl
    {
        private readonly BudgetAnalyticsViewModel _vm;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;

        // Controls
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private GradientPanelExt? _filterPanel;
        private ErrorProvider? _errorProvider;

        // Filter controls
        private SfComboBox? _comboDepartment;
        private SfComboBox? _comboDateRange;
        private SfButton? _btnApplyFilter;
        private SfButton? _btnReset;
        private SfButton? _btnExportReport;

        // Analytics controls
        private ChartControl? _trendChart;
        private ChartControl? _departmentChart;
        private SfDataGrid? _analyticsGrid;
        private Label? _summaryLabel;

        // Event handlers
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;

        /// <summary>
        /// Parameterless constructor for designer support ONLY. Use DI constructor in production.
        /// </summary>
        [Obsolete("Use DI constructor with BudgetAnalyticsViewModel and IDispatcherHelper parameters", false)]
        public BudgetAnalyticsPanel() : this(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<BudgetAnalyticsViewModel>(Program.Services!) ?? new BudgetAnalyticsViewModel(),
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services!))
        {
        }

        public BudgetAnalyticsPanel(BudgetAnalyticsViewModel vm, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));

            InitializeComponent();

            // Apply theme via SfSkinManager (single source of truth)
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, "Office2019Colorful"); } catch { }
            SetupUI();
            BindViewModel();
            ApplyCurrentTheme();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            Name = "BudgetAnalyticsPanel";
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
            _panelHeader.Title = "Budget Analytics";
            _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
            _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
            _panelHeaderCloseHandler = (s, e) => ClosePanel();
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            Controls.Add(_panelHeader);

            // Filter panel with proper Dock-based layout
            CreateFilterPanel();
            Controls.Add(_filterPanel!);

            // Main analytics split container
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal
            };
            // Defer setting min sizes and splitter distance until control is sized
            SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(mainSplit, 250, 150, 400);

            // Top: Charts side-by-side
            var chartSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.None
            };
            SafeSplitterDistanceHelper.TrySetSplitterDistance(chartSplit, 400);

            _trendChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Budget trend chart",
                AccessibleDescription = "Displays budget vs actual trend over time"
            };
            ConfigureTrendChart();
            chartSplit.Panel1.Controls.Add(_trendChart);

            _departmentChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Department performance chart",
                AccessibleDescription = "Displays department budget performance"
            };
            ConfigureDepartmentChart();
            chartSplit.Panel2.Controls.Add(_departmentChart);

            mainSplit.Panel1.Controls.Add(chartSplit);

            // Bottom: Analytics grid
            _analyticsGrid = new SfDataGrid
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
                AccessibleName = "Analytics grid",
                AccessibleDescription = "Displays detailed budget analytics by department and period"
            };
            ConfigureGridColumns();
            mainSplit.Panel2.Controls.Add(_analyticsGrid);

            Controls.Add(mainSplit);

            // Summary label
            _summaryLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "Ready",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                Padding = new Padding(0, 0, 8, 0)
            };
            Controls.Add(_summaryLabel);

            // Overlays
            _loadingOverlay = new LoadingOverlay { Message = "Loading analytics..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No analytics data available\r\nAdd budget entries and accounts to generate analytics" };
            Controls.Add(_noDataOverlay);
        }

        /// <summary>
        /// Creates the filter panel with proper Dock-based layout (no absolute positioning).
        /// </summary>
        private void CreateFilterPanel()
        {
            _filterPanel = new GradientPanelExt
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(8),
                BorderStyle = BorderStyle.None,
                CornerRadius = 2,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(_filterPanel, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true
            };

            // Department filter
            var lblDept = new Label
            {
                Text = "Department:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 4, 0)
            };
            flow.Controls.Add(lblDept);

            _comboDepartment = new SfComboBox
            {
                Width = 150,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Department filter",
                AccessibleDescription = "Filter analytics by department"
            };
            _comboDepartment.SelectedIndexChanged += ComboFilter_SelectedIndexChanged;
            flow.Controls.Add(_comboDepartment);

            // Date range filter
            var lblDate = new Label
            {
                Text = "Date Range:",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 6, 4, 0)
            };
            flow.Controls.Add(lblDate);

            _comboDateRange = new SfComboBox
            {
                Width = 150,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Date range filter",
                AccessibleDescription = "Filter analytics by date range"
            };
            _comboDateRange.SelectedIndexChanged += ComboFilter_SelectedIndexChanged;
            flow.Controls.Add(_comboDateRange);

            // Buttons using FlowLayoutPanel
            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };

            _btnApplyFilter = new SfButton
            {
                Text = "Apply Filter",
                AutoSize = true,
                Margin = new Padding(3),
                AccessibleName = "Apply analytics filter",
                AccessibleDescription = "Apply selected filters to analytics"
            };
            _btnApplyFilter.Click += async (s, e) => await RefreshDataAsync();
            btnFlow.Controls.Add(_btnApplyFilter);

            _btnReset = new SfButton
            {
                Text = "Reset",
                AutoSize = true,
                Margin = new Padding(3),
                AccessibleName = "Reset analytics filters",
                AccessibleDescription = "Clear all filter selections"
            };
            _btnReset.Click += (s, e) => ResetFilters();
            btnFlow.Controls.Add(_btnReset);

            _btnExportReport = new SfButton
            {
                Text = "📊 Export Report",
                AutoSize = true,
                Margin = new Padding(3),
                AccessibleName = "Export analytics report",
                AccessibleDescription = "Export analytics data to CSV"
            };
            _btnExportReport.Click += async (s, e) => await ExportReportAsync();
            btnFlow.Controls.Add(_btnExportReport);

            flow.Controls.Add(btnFlow);
            _filterPanel.Controls.Add(flow);
        }

        private void ConfigureTrendChart()
        {
            if (_trendChart == null) return;

            ChartControlDefaults.Apply(_trendChart);
            _trendChart.ShowLegend = true;
            _trendChart.LegendsPlacement = ChartPlacement.Outside;
            _trendChart.Title.Text = "Budget Trend";
            _trendChart.PrimaryXAxis.Title = "Period";
            _trendChart.PrimaryXAxis.Font = new Font("Segoe UI", 9F);
            _trendChart.PrimaryXAxis.DrawGrid = false;
            _trendChart.PrimaryYAxis.Title = "Amount";
            _trendChart.PrimaryYAxis.Font = new Font("Segoe UI", 9F);
        }

        private void ConfigureDepartmentChart()
        {
            if (_departmentChart == null) return;

            ChartControlDefaults.Apply(_departmentChart);
            _departmentChart.ShowLegend = true;
            _departmentChart.LegendsPlacement = ChartPlacement.Outside;
            _departmentChart.Title.Text = "Department Performance";
            _departmentChart.PrimaryXAxis.Title = "Department";
            _departmentChart.PrimaryXAxis.Font = new Font("Segoe UI", 9F);
            _departmentChart.PrimaryXAxis.DrawGrid = false;
            _departmentChart.PrimaryYAxis.Title = "Variance %";
            _departmentChart.PrimaryYAxis.Font = new Font("Segoe UI", 9F);
        }

        private void ConfigureGridColumns()
        {
            if (_analyticsGrid == null) return;

            _analyticsGrid.Columns.Add(new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", MinimumWidth = 150 });
            _analyticsGrid.Columns.Add(new GridTextColumn { MappingName = "PeriodName", HeaderText = "Period", MinimumWidth = 120 });
            _analyticsGrid.Columns.Add(new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budgeted", MinimumWidth = 120 });
            _analyticsGrid.Columns.Add(new GridNumericColumn { MappingName = "ActualAmount", HeaderText = "Actual", MinimumWidth = 120 });
            _analyticsGrid.Columns.Add(new GridNumericColumn { MappingName = "VarianceAmount", HeaderText = "Variance", MinimumWidth = 120 });
            _analyticsGrid.Columns.Add(new GridTextColumn { MappingName = "VariancePercent", HeaderText = "Variance %", MinimumWidth = 100 });
            _analyticsGrid.Columns.Add(new GridTextColumn { MappingName = "Status", HeaderText = "Status", MinimumWidth = 100 });
        }

        private void BindViewModel()
        {
            // Bind filters
            try
            {
                if (_comboDepartment != null && _vm.AvailableDepartments.Any())
                {
                    _comboDepartment.DataSource = _vm.AvailableDepartments.ToList();
                }

                if (_comboDateRange != null && _vm.AvailableDateRanges.Any())
                {
                    _comboDateRange.DataSource = _vm.AvailableDateRanges.ToList();
                }
            }
            catch { }

            // Subscribe to property changes
            _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
            _vm.PropertyChanged += _viewModelPropertyChangedHandler;

            // Subscribe to collection changes
            _vm.AnalyticsData.CollectionChanged += AnalyticsData_CollectionChanged;

            // Initial UI update
            UpdateUI();
        }

        private void AnalyticsData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke((MethodInvoker)(() => AnalyticsData_CollectionChanged(sender, e)));
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

                    case nameof(_vm.AnalyticsData):
                    case nameof(_vm.SelectedDepartment):
                    case nameof(_vm.SelectedDateRange):
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
                Serilog.Log.Warning(ex, "BudgetAnalyticsPanel: PropertyChanged handler failed");
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (IsDisposed) return;

                // Update grid
                if (_analyticsGrid != null)
                {
                    try
                    {
                        _analyticsGrid.SuspendLayout();
                        var snapshot = _vm.AnalyticsData.ToList();
                        _analyticsGrid.DataSource = snapshot;
                        _analyticsGrid.ResumeLayout();
                    }
                    catch { }
                }

                // Update charts
                UpdateTrendChart();
                UpdateDepartmentChart();

                // Update summary label
                if (_summaryLabel != null)
                {
                    _summaryLabel.Text = $"Displaying {_vm.AnalyticsData.Count} records • Updated: {DateTime.Now:HH:mm:ss}";
                }

                // Show no-data overlay if needed
                if (_noDataOverlay != null)
                    _noDataOverlay.Visible = !_vm.IsLoading && !_vm.AnalyticsData.Any();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetAnalyticsPanel: UpdateUI failed");
            }
        }

        private void UpdateTrendChart()
        {
            if (_trendChart == null) return;

            try
            {
                _trendChart.Series.Clear();

                var budgetSeries = new ChartSeries("Budgeted", ChartSeriesType.Line);
                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Line);

                foreach (var data in _vm.AnalyticsData.Take(12))
                {
                    budgetSeries.Points.Add(data.PeriodName, (double)data.BudgetedAmount);
                    actualSeries.Points.Add(data.PeriodName, (double)data.ActualAmount);
                }

                _trendChart.Series.Add(budgetSeries);
                _trendChart.Series.Add(actualSeries);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetAnalyticsPanel: UpdateTrendChart failed");
            }
        }

        private void UpdateDepartmentChart()
        {
            if (_departmentChart == null) return;

            try
            {
                _departmentChart.Series.Clear();

                var varianceSeries = new ChartSeries("Variance %", ChartSeriesType.Column);

                var deptData = _vm.AnalyticsData
                    .GroupBy(x => x.DepartmentName)
                    .Select(g => new { Department = g.Key, AvgVariance = g.Average(x => double.Parse(x.VariancePercent ?? "0", System.Globalization.CultureInfo.InvariantCulture)) })
                    .Take(10);

                foreach (var dept in deptData)
                {
                    varianceSeries.Points.Add(dept.Department, dept.AvgVariance);
                }

                _departmentChart.Series.Add(varianceSeries);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetAnalyticsPanel: UpdateDepartmentChart failed");
            }
        }

        private void ComboFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            try
            {
                if (_comboDepartment?.SelectedItem is string dept)
                    _vm.SelectedDepartment = dept;

                if (_comboDateRange?.SelectedItem is string range)
                    _vm.SelectedDateRange = range;
            }
            catch { }
        }

        private void ResetFilters()
        {
            try
            {
                if (_comboDepartment != null)
                    _comboDepartment.SelectedIndex = 0;

                if (_comboDateRange != null)
                    _comboDateRange.SelectedIndex = 0;
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
                Serilog.Log.Warning(ex, "BudgetAnalyticsPanel: Refresh failed");
                MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ExportReportAsync()
        {
            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV Files|*.csv",
                    DefaultExt = "csv",
                    FileName = $"budget-analytics-{DateTime.Now:yyyyMMdd}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Department,Period,Budgeted,Actual,Variance,Variance %,Status");

                foreach (var data in _vm.AnalyticsData)
                {
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "\"{0}\",\"{1}\",{2},{3},{4},{5},\"{6}\"",
                        data.DepartmentName,
                        data.PeriodName,
                        data.BudgetedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        data.ActualAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        data.VarianceAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        data.VariancePercent,
                        data.Status));
                }

                await System.IO.File.WriteAllTextAsync(sfd.FileName, sb.ToString());
                MessageBox.Show($"Exported to {sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetAnalyticsPanel: Export failed");
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
                Serilog.Log.Warning(ex, "BudgetAnalyticsPanel: ClosePanel failed");
            }
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                // Theme is applied automatically by SfSkinManager cascade from parent form
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                // Defer sizing validation until layout is complete
                DeferSizeValidation();

                if (!DesignMode && !_vm.IsLoading)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _vm.LoadDataCommand.ExecuteAsync(null);
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, "BudgetAnalyticsPanel: OnLoad data fetch failed");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetAnalyticsPanel: OnLoad failed");
            }
        }

        private void DeferSizeValidation()
        {
            if (IsDisposed) return;

            if (IsHandleCreated)
            {
                try { BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _))); }
                catch { }
                return;
            }

            EventHandler? handleCreatedHandler = null;
            handleCreatedHandler = (s, e) =>
            {
                HandleCreated -= handleCreatedHandler;
                if (IsDisposed) return;

                try { BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _))); }
                catch { }
            };

            HandleCreated += handleCreatedHandler;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_viewModelPropertyChangedHandler != null) _vm.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }
                try { _vm.AnalyticsData.CollectionChanged -= AnalyticsData_CollectionChanged; } catch { }

                try
                {
                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    }
                }
                catch { }

                try { _analyticsGrid?.SafeClearDataSource(); } catch { }
                try { _analyticsGrid?.SafeDispose(); } catch { }
                try { _trendChart?.Dispose(); } catch { }
                try { _departmentChart?.Dispose(); } catch { }
                try { _comboDepartment?.Dispose(); } catch { }
                try { _comboDateRange?.Dispose(); } catch { }
                try { _btnApplyFilter?.Dispose(); } catch { }
                try { _btnReset?.Dispose(); } catch { }
                try { _btnExportReport?.Dispose(); } catch { }
                try { _panelHeader?.Dispose(); } catch { }
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }
                try { _errorProvider?.Dispose(); } catch { }
                try { _filterPanel?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
