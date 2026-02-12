using System.Threading;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls.Base;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
using WileyWidget.WinForms.Controls.Supporting;
using Syncfusion.WinForms.DataGrid;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Controls.Panels
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
    /// Budget overview panel displaying budget vs actual analysis with
    /// fiscal year filtering, variance tracking, and CSV export capabilities.
    /// Designed for embedding in DockingManager with DI-scoped lifecycle.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class BudgetOverviewPanel : ScopedPanelBase<BudgetOverviewViewModel>
    {
        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        // DataContext managed by ScopedPanelBase

        // Controls
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private LegacyGradientPanel? _topPanel;
        private LegacyGradientPanel? _summaryPanel;
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
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        private EventHandler? _fiscalYearSelectedChangedHandler;
        private EventHandler? _refreshButtonClickHandler;
        private EventHandler? _exportButtonClickHandler;

        // Data binding sources
        private BindingSource? _fiscalYearBindingSource;
        private BindingSource? _summaryBindingSource;

        /// <summary>
        /// Constructor using DI scope factory for proper lifecycle management.
        /// </summary>
        public BudgetOverviewPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<BudgetOverviewViewModel>>? logger)
            : base(scopeFactory, logger)
        {
            InitializeComponent();

            // Apply theme via SfSkinManager (single source of truth)
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme); } catch { }
            SetupUI();
            // BindViewModel() moved to OnViewModelResolved to ensure ViewModel is resolved first
        }

        /// <summary>
        /// Called after ViewModel is resolved from DI scope. Binds UI controls to ViewModel properties.
        /// </summary>
        protected override void OnViewModelResolved(object? viewModel)
        {
            base.OnViewModelResolved(viewModel);
            if (viewModel is not BudgetOverviewViewModel)
            {
                return;
            }
            BindViewModel();
        }

        /// <summary>
        /// Heavy async initialization - loads budget data after panel is displayed.
        /// </summary>
        protected override async Task OnHandleCreatedAsync()
        {
            await base.OnHandleCreatedAsync();
            await LoadAsync(CancellationToken.None);
        }

        /// <summary>
        /// Loads budget data for the initially selected fiscal year.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            try
            {
                if (ViewModel != null)
                {
                    await ViewModel.LoadDataCommand.ExecuteAsync(null);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
        {
            // Validate that fiscal years are available
            if (ViewModel == null)
                return await Task.FromResult(ValidationResult.Failed(new ValidationItem("ViewModel", "ViewModel not initialized", ValidationSeverity.Error)));

            if (!ViewModel.AvailableFiscalYears.Any())
                return await Task.FromResult(ValidationResult.Failed(new ValidationItem("FiscalYear", "No fiscal year data available", ValidationSeverity.Error)));

            if (!ViewModel.Metrics.Any())
                return await Task.FromResult(ValidationResult.Failed(new ValidationItem("Metrics", "No budget metrics available", ValidationSeverity.Warning)));

            return await Task.FromResult(ValidationResult.Success);
        }

        public override async Task<ValidationResult> SaveAsync(CancellationToken ct = default)
        {
            // Budget overview is read-only, no save needed
            return await Task.FromResult(ValidationResult.Success);
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
            // Retrieve current theme and initialize UI components
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            var tooltips = new ToolTip();

            // Initialize binding sources for MVVM data binding
            _fiscalYearBindingSource = new BindingSource();
            _summaryBindingSource = new BindingSource();
            _panelHeader = new PanelHeader { Dock = DockStyle.Top };
            _panelHeader.Title = BudgetOverviewPanelResources.PanelTitle;
            _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
            _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
            _panelHeaderCloseHandler = (s, e) => ClosePanel();
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            Controls.Add(_panelHeader);

            // Top toolbar using TableLayoutPanel for proper responsive layout
            _topPanel = new LegacyGradientPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8, 4, 8, 4),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(_topPanel, currentTheme);

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
                Margin = new Padding(0, 2, 0, 2),
                DataSource = _fiscalYearBindingSource,
                DisplayMember = null,  // Use default ToString for List<int> integers
                ValueMember = null,    // Use value itself without explicit member mapping
                ThemeName = currentTheme,
                Font = new Font("Segoe UI", 10F),
                AllowNull = false      // Require selection
            };
            tooltips.SetToolTip(_comboFiscalYear, "Select a fiscal year to view budget data for that period");
            _fiscalYearSelectedChangedHandler = ComboFiscalYear_SelectedIndexChanged;
            _comboFiscalYear.SelectedIndexChanged += _fiscalYearSelectedChangedHandler;
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
                Margin = new Padding(0, 2, 0, 2),
                ThemeName = currentTheme
            };
            tooltips.SetToolTip(_btnRefresh, "Reload the budget data from the database (Ctrl+R)");
            SetupRefreshButtonIcon();
            _refreshButtonClickHandler = OnRefreshButtonClick;
            _btnRefresh.Click += _refreshButtonClickHandler;
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
                Margin = new Padding(0, 2, 0, 2),
                ThemeName = currentTheme
            };
            tooltips.SetToolTip(_btnExportCsv, "Export all budget metrics to a CSV file for use in spreadsheets or reports");
            SetupExportButtonIcon();
            _exportButtonClickHandler = OnExportCsvClick;
            _btnExportCsv.Click += _exportButtonClickHandler;
            toolbar.Controls.Add(_btnExportCsv, 5, 0);

            _topPanel.Controls.Add(toolbar);
            Controls.Add(_topPanel);

            // Summary panel with KPI tiles
            _summaryPanel = new LegacyGradientPanel
            {
                Dock = DockStyle.Top,
                Height = 80,  // Reduced to give more space to grid headers
                Padding = new Padding(8),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            SfSkinManager.SetVisualStyle(_summaryPanel, currentTheme);
            var summaryFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            // Create summary tiles with data bindings
            _lblTotalBudget = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.TotalBudgetLabel, "$0", Color.DodgerBlue);
            _lblTotalBudget.DataBindings.Add("Text", _summaryBindingSource, nameof(BudgetOverviewViewModel.TotalBudget), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
            _lblTotalBudget.AccessibleName = "Total Budget";
            tooltips.SetToolTip(_lblTotalBudget, "Total budgeted amount across all departments for the selected fiscal year");

            _lblTotalActual = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.TotalActualLabel, "$0", Color.Green);
            _lblTotalActual.DataBindings.Add("Text", _summaryBindingSource, nameof(BudgetOverviewViewModel.TotalActual), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
            _lblTotalActual.AccessibleName = "Total Actual";
            tooltips.SetToolTip(_lblTotalActual, "Total actual spending across all departments in the selected fiscal year");

            _lblVariance = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.VarianceLabel, "$0", Color.Orange);
            _lblVariance.DataBindings.Add("Text", _summaryBindingSource, nameof(BudgetOverviewViewModel.TotalVariance), true, DataSourceUpdateMode.OnPropertyChanged, "$0", "C0");
            _lblVariance.AccessibleName = "Total Variance";
            tooltips.SetToolTip(_lblVariance, "Budget vs actual variance across all departments (positive = under budget, negative = over budget)");

            _lblOverBudgetCount = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.OverBudgetLabel, "0", Color.Red);
            _lblOverBudgetCount.DataBindings.Add("Text", _summaryBindingSource, nameof(BudgetOverviewViewModel.OverBudgetCount), true, DataSourceUpdateMode.OnPropertyChanged, "0", "N0");
            _lblOverBudgetCount.AccessibleName = "Over Budget Count";
            tooltips.SetToolTip(_lblOverBudgetCount, "Number of departments over budget");

            _lblUnderBudgetCount = CreateSummaryTile(summaryFlow, BudgetOverviewPanelResources.UnderBudgetLabel, "0", Color.Green);
            _lblUnderBudgetCount.DataBindings.Add("Text", _summaryBindingSource, nameof(BudgetOverviewViewModel.UnderBudgetCount), true, DataSourceUpdateMode.OnPropertyChanged, "0", "N0");
            _lblUnderBudgetCount.AccessibleName = "Under Budget Count";
            tooltips.SetToolTip(_lblUnderBudgetCount, "Number of departments under budget");

            _summaryPanel.Controls.Add(summaryFlow);
            Controls.Add(_summaryPanel);

            // Main content area - split between chart and grid
            var mainSplit = new SplitContainerAdv
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal
            };
            // Allocate 250px for chart, rest to grid so headers are visible
            SafeSplitterDistanceHelper.ConfigureSafeSplitContainer(mainSplit, 250, 200, 250);

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

            // Metrics grid with data binding
            var gridBindingSource = new BindingSource();
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
                AccessibleDescription = "Displays budget metrics by department",
                ThemeName = currentTheme,
                DataSource = gridBindingSource
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
                Text = "Last updated:",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                Padding = new Padding(0, 0, 8, 0)
            };
            Controls.Add(_lblLastUpdated);

            // Loading and no-data overlays
            _loadingOverlay = new LoadingOverlay
            {
                Message = "Loading budget data...",
                Dock = DockStyle.Fill,
                Visible = false
            };
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();

            _noDataOverlay = new NoDataOverlay
            {
                Message = "No budget data available\r\nAdd budget entries and accounts to see analysis",
                Dock = DockStyle.Fill,
                Visible = false
            };
            Controls.Add(_noDataOverlay);
            _noDataOverlay.BringToFront();

            this.PerformLayout();
            this.Refresh();
        }

        private Label CreateSummaryTile(FlowLayoutPanel parent, string title, string value, Color accentColor)
        {
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            var tile = new LegacyGradientPanel
            {
                Width = 150,
                Height = 80,
                Margin = new Padding(4),
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                AccessibleName = $"{title} summary card",
                AccessibleDescription = $"Displays {title.ToLower(CultureInfo.CurrentCulture)} metric"
            };
            SfSkinManager.SetVisualStyle(tile, currentTheme);

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

            // Priority 2 Enhancements: Enable tooltips and interactive features for variance visualization
            _varianceChart.ShowToolTips = true;   // Show tooltips for data points
            _varianceChart.AutoHighlight = true;  // Highlight series on hover

            // Configure X axis (Departments)
            _varianceChart.PrimaryXAxis.Title = "Department";
            _varianceChart.PrimaryXAxis.Font = new Font("Segoe UI", 9F);
            _varianceChart.PrimaryXAxis.DrawGrid = false;
            _varianceChart.PrimaryXAxis.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);

            // Configure Y axis (Currency amounts)
            _varianceChart.PrimaryYAxis.Title = "Amount (Currency)";
            _varianceChart.PrimaryYAxis.Font = new Font("Segoe UI", 9F);
            _varianceChart.PrimaryYAxis.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            _varianceChart.PrimaryYAxis.DrawGrid = true;
        }

        private void ConfigureGridColumns()
        {
            if (_metricsGrid == null) return;

            var deptCol = new GridTextColumn { MappingName = "DepartmentName", HeaderText = "Department", MinimumWidth = 150 };
            _metricsGrid.Columns.Add(deptCol);

            var budgetCol = new GridNumericColumn { MappingName = "BudgetedAmount", HeaderText = "Budgeted", MinimumWidth = 120, Format = "C2" };
            _metricsGrid.Columns.Add(budgetCol);

            var actualCol = new GridNumericColumn { MappingName = "Amount", HeaderText = "Actual", MinimumWidth = 120, Format = "C2" };
            _metricsGrid.Columns.Add(actualCol);

            var varianceCol = new GridNumericColumn { MappingName = "Variance", HeaderText = "Variance", MinimumWidth = 120, Format = "C2" };
            _metricsGrid.Columns.Add(varianceCol);

            var varPctCol = new GridTextColumn { MappingName = "VariancePercent", HeaderText = "Variance %", MinimumWidth = 100 };
            _metricsGrid.Columns.Add(varPctCol);
        }

        private void SetupRefreshButtonIcon()
        {
            try
            {
                if (_btnRefresh != null)
                {
                    _btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                    _btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;
                }
            }
            catch { }
        }

        private void SetupExportButtonIcon()
        {
            try
            {
                if (_btnExportCsv != null)
                {
                    _btnExportCsv.ImageAlign = ContentAlignment.MiddleLeft;
                    _btnExportCsv.TextImageRelation = TextImageRelation.ImageBeforeText;
                }
            }
            catch { }
        }

        private void UpdateButtonIcon(SfButton? button, Image? icon)
        {
            if (button == null) return;
            if (button.InvokeRequired)
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
            // Guard: ensure ViewModel is resolved before binding
            if (ViewModel == null)
            {
                return;
            }

            // Bind fiscal years combo via BindingSource
            // Convert to list to ensure combo can display values properly with proper int handling
            BindFiscalYearData();

            // Subscribe to collection changes so dropdown updates when new years are discovered during data load
            if (ViewModel != null)
            {
                ViewModel.AvailableFiscalYears.CollectionChanged += (s, e) =>
                {
                    if (InvokeRequired)
                    {
                        if (IsHandleCreated && !IsDisposed)
                        {
                            BeginInvoke((MethodInvoker)(() => BindFiscalYearData()));
                        }
                    }
                    else
                    {
                        BindFiscalYearData();
                    }
                };
            }

            // Bind summary tiles via BindingSource (currency/count formatting handled by DataBindings)
            if (_summaryBindingSource != null && ViewModel != null)
            {
                _summaryBindingSource.DataSource = ViewModel;
            }

            // Bind metrics grid via BindingSource with performance optimization
            if (ViewModel?.Metrics != null && _metricsGrid != null)
            {
                try
                {
                    _metricsGrid.BeginUpdate();
                    var gridBinding = _metricsGrid.DataSource as BindingSource;
                    if (gridBinding != null)
                    {
                        gridBinding.DataSource = ViewModel.Metrics;
                    }
                }
                finally
                {
                    _metricsGrid.EndUpdate();
                }
            }

            // Subscribe to property changes for UI-only updates (IsLoading, ErrorMessage)
            _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
            if (ViewModel != null)
                ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

            // Subscribe to metrics collection changes for overlay visibility
            if (ViewModel != null)
            {
                ViewModel.Metrics.CollectionChanged -= (s, e) => UpdateUI();  // Remove old
                ViewModel.Metrics.CollectionChanged += BudgetMetrics_CollectionChanged;  // Add safe handler
            }

            // Initial UI update for overlays
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
                if (IsDisposed || ViewModel == null) return;

                if (InvokeRequired)
                {
                    BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
                    return;
                }

                switch (e.PropertyName)
                {
                    case nameof(ViewModel.IsLoading):
                        if (_loadingOverlay != null) _loadingOverlay.Visible = ViewModel.IsLoading;
                        break;

                    case nameof(ViewModel.Metrics):
                    case nameof(ViewModel.TotalBudget):
                    case nameof(ViewModel.TotalActual):
                    case nameof(ViewModel.TotalVariance):
                    case nameof(ViewModel.OverBudgetCount):
                    case nameof(ViewModel.UnderBudgetCount):
                    case nameof(ViewModel.LastUpdated):
                        UpdateUI();
                        break;

                    case nameof(ViewModel.ErrorMessage):
                        if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                        {
                            _errorProvider?.SetError(this, ViewModel.ErrorMessage);
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
                if (IsDisposed || ViewModel == null) return;

                // Summary tiles are now bound via BindingSource - no manual update needed
                // Just update semantic color for variance (red = over, green = under)
                if (_lblVariance != null)
                {
                    _lblVariance.ForeColor = ViewModel.TotalVariance >= 0 ? Color.Red : Color.Green;  // Semantic: red for over, green for under
                }

                // Update last-updated timestamp
                if (_lblLastUpdated != null)
                    _lblLastUpdated.Text = $"Last updated: {ViewModel.LastUpdated:yyyy-MM-dd HH:mm:ss}";

                // Update chart
                UpdateChart();

                // Update grid with no-data check (grid data is bound via BindingSource)
                UpdateMetricsGrid();

                // Show no-data overlay if needed
                if (_noDataOverlay != null)
                    _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.Metrics.Any();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: UpdateUI failed");
            }
        }

        private void UpdateMetricsGrid()
        {
            if (_metricsGrid == null || ViewModel == null) return;

            try
            {
                // Grid is bound via BindingSource which auto-updates on collection changes
                // This method now only handles layout refresh if needed
                _metricsGrid.SuspendLayout();
                _metricsGrid.ResumeLayout();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: UpdateMetricsGrid failed");
            }
        }

        private void UpdateChart()
        {
            if (_varianceChart == null || ViewModel == null) return;

            try
            {
                _varianceChart.Series.Clear();

                var budgetSeries = new ChartSeries("Budgeted", ChartSeriesType.Column);
                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Column);

                foreach (var metric in ViewModel.Metrics.Take(10)) // Limit to top 10 for chart readability
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
                // Defensive null checks
                if (_comboFiscalYear == null)
                {
                    Serilog.Log.Warning("BudgetOverviewPanel: ComboFiscalYear_SelectedIndexChanged fired but _comboFiscalYear is null");
                    return;
                }

                if (ViewModel == null)
                {
                    Serilog.Log.Warning("BudgetOverviewPanel: ComboFiscalYear_SelectedIndexChanged fired but ViewModel is null");
                    return;
                }

                // Validate selected item is an integer before casting
                if (_comboFiscalYear.SelectedItem is not int year)
                {
                    Serilog.Log.Debug("BudgetOverviewPanel: ComboFiscalYear_SelectedIndexChanged - invalid selected item type: {ItemType}",
                        _comboFiscalYear.SelectedItem?.GetType().Name ?? "null");
                    return;
                }

                // Only refresh if the year actually changed
                if (year == ViewModel.SelectedFiscalYear)
                {
                    Serilog.Log.Debug("BudgetOverviewPanel: ComboFiscalYear selection unchanged ({Year})", year);
                    return;
                }

                Serilog.Log.Debug("BudgetOverviewPanel: ComboFiscalYear selection changed to {Year}", year);
                ViewModel.SelectedFiscalYear = year;

                // Reload data for the newly selected fiscal year
                _ = RefreshDataAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetOverviewPanel: ComboFiscalYear_SelectedIndexChanged handler failed");
            }
        }

        private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (ViewModel != null)
                    await ViewModel.RefreshCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "BudgetOverviewPanel: Refresh failed");
                MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ExportToCsvAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (ViewModel == null) return;

                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV Files|*.csv",
                    DefaultExt = "csv",
                    FileName = $"budget-overview-{ViewModel.SelectedFiscalYear}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                // Sanitize file path
                var filePath = Path.GetFullPath(sfd.FileName);
                if (!IsValidFilePath(filePath))
                {
                    MessageBox.Show("Invalid file path", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Department,Budgeted,Actual,Variance,Variance %,Over Budget");

                foreach (var metric in ViewModel.Metrics)
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
                    ViewModel.TotalBudget.ToString(CultureInfo.InvariantCulture), ",",
                    ViewModel.TotalActual.ToString(CultureInfo.InvariantCulture), ",",
                    ViewModel.TotalVariance.ToString(CultureInfo.InvariantCulture), ",",
                    ViewModel.OverallVariancePercent.ToString("F2", CultureInfo.InvariantCulture), ","));

                await File.WriteAllTextAsync(filePath, sb.ToString());
                MessageBox.Show($"Exported to {filePath}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetOverviewPanel: Export to CSV failed");
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsValidFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var fi = new FileInfo(path);
                return fi.DirectoryName != null && Directory.Exists(fi.DirectoryName) &&
                       !Path.GetInvalidPathChars().Any(c => fi.Name.Contains(c));
            }
            catch
            {
                return false;
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

        private void OnRefreshButtonClick(object? sender, EventArgs e)
        {
            BeginInvoke(new Func<Task>(async () => await RefreshDataAsync()));
        }

        private void OnExportCsvClick(object? sender, EventArgs e)
        {
            BeginInvoke(new Func<Task>(async () => await ExportToCsvAsync()));
        }

        private void OnThemeChanged(object? sender, string theme)
        {
            try
            {
                if (IsDisposed) return;
                if (InvokeRequired)
                {
                    if (!IsHandleCreated)
                    {
                        return;
                    }

                    BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme)));
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

        /// <summary>
        /// Binds fiscal year data to the combo box with comprehensive error handling and validation.
        /// Properly handles List&lt;int&gt; binding with null/empty checks and thread safety.
        /// </summary>
        private void BindFiscalYearData()
        {
            try
            {
                // Defensive checks
                if (_fiscalYearBindingSource == null)
                {
                    Serilog.Log.Warning("BudgetOverviewPanel: BindFiscalYearData called but _fiscalYearBindingSource is null");
                    return;
                }

                if (_comboFiscalYear == null)
                {
                    Serilog.Log.Warning("BudgetOverviewPanel: BindFiscalYearData called but _comboFiscalYear is null");
                    return;
                }

                if (ViewModel == null)
                {
                    Serilog.Log.Warning("BudgetOverviewPanel: BindFiscalYearData called but ViewModel is null");
                    return;
                }

                // Create a sorted snapshot of years to ensure consistent ordering
                var yearsList = new List<int>(ViewModel.AvailableFiscalYears);

                if (yearsList.Count == 0)
                {
                    Serilog.Log.Debug("BudgetOverviewPanel: No fiscal years available to bind");
                    _comboFiscalYear.SelectedIndex = -1;
                    return;
                }

                yearsList.Sort(); // Ensure years are in ascending order for better UX

                // Set data source (will reset existing selection)
                _fiscalYearBindingSource.DataSource = yearsList;

                // Validate binding source is properly populated
                if (_fiscalYearBindingSource.List == null || _fiscalYearBindingSource.List.Count == 0)
                {
                    Serilog.Log.Warning("BudgetOverviewPanel: BindingSource List is empty after DataSource assignment");
                    _comboFiscalYear.SelectedIndex = -1;
                    return;
                }

                // Select current or first available year
                var selectedYear = ViewModel.SelectedFiscalYear;
                var selectedIndex = yearsList.IndexOf(selectedYear);

                if (selectedIndex >= 0)
                {
                    _comboFiscalYear.SelectedIndex = selectedIndex;
                    Serilog.Log.Debug("BudgetOverviewPanel: Selected fiscal year {Year} at index {Index}", selectedYear, selectedIndex);
                }
                else if (yearsList.Count > 0)
                {
                    // Default to first available year if current selection not found
                    _comboFiscalYear.SelectedIndex = 0;
                    Serilog.Log.Debug("BudgetOverviewPanel: Selected first available fiscal year (not found: {Year})", selectedYear);
                }
                else
                {
                    _comboFiscalYear.SelectedIndex = -1;
                    Serilog.Log.Warning("BudgetOverviewPanel: No fiscal years available to select");
                }

                Serilog.Log.Debug("BudgetOverviewPanel: Fiscal year binding complete. Available years: {Years}, Selected: {SelectedYear}",
                    string.Join(", ", yearsList), _comboFiscalYear.SelectedItem);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "BudgetOverviewPanel: Error binding fiscal year data");
                _comboFiscalYear.SelectedIndex = -1; // Clear selection on error
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                if (!DesignMode)
                {
                    // Note: Data loading is now handled by ILazyLoadViewModel via DockingManager events
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
                try { if (_viewModelPropertyChangedHandler != null && ViewModel != null) ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }
                try { if (ViewModel != null) ViewModel.Metrics.CollectionChanged -= BudgetMetrics_CollectionChanged; } catch { }

                try
                {
                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    }
                    if (_comboFiscalYear != null && _fiscalYearSelectedChangedHandler != null)
                    {
                        _comboFiscalYear.SelectedIndexChanged -= _fiscalYearSelectedChangedHandler;
                    }
                    if (_btnRefresh != null && _refreshButtonClickHandler != null)
                    {
                        _btnRefresh.Click -= _refreshButtonClickHandler;
                    }
                    if (_btnExportCsv != null && _exportButtonClickHandler != null)
                    {
                        _btnExportCsv.Click -= _exportButtonClickHandler;
                    }
                }
                catch { }

                // Dispose binding sources
                try { _fiscalYearBindingSource?.Dispose(); } catch { }
                try { _summaryBindingSource?.Dispose(); } catch { }
                _fiscalYearBindingSource = null;
                _summaryBindingSource = null;

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
