using System.Threading;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using WileyWidget.WinForms.Controls;

namespace WileyWidget.WinForms.Controls.Analytics
{
    /// <summary>
    /// Budget analytics panel displaying budget variance trends, department performance,
    /// and financial forecasting. Uses proper Syncfusion API (Dock-based layout, GradientPanelExt per docs).
    /// Implements ICompletablePanel for proper async lifecycle management.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class BudgetAnalyticsPanel : ScopedPanelBase
    {
        // Strongly-typed ViewModel (this is what you use in your code)
        public new BudgetAnalyticsViewModel? ViewModel
        {
            get => (BudgetAnalyticsViewModel?)base.ViewModel;
            set => base.ViewModel = value;
        }
        // Controls
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private GradientPanelExt? _filterPanel;
        private ErrorProvider? _errorProvider;
        private BindingSource? _bindingSource;

        // Filter controls
        private SfComboBox? _comboDepartment;
        private SfComboBox? _comboDateRange;
        private SfButton? _btnApplyFilter;
        private SfButton? _btnReset;
        private SfButton? _btnExportReport;

        // Binding adapters (bridge ObservableCollection to WinForms BindingList)
        private ObservableCollectionToBindingListAdapter<string>? _departmentAdapter;
        private ObservableCollectionToBindingListAdapter<string>? _dateRangeAdapter;

        // Analytics controls
        private ChartControl? _trendChart;
        private ChartControl? _departmentChart;
        private SfDataGrid? _analyticsGrid;
        private Label? _summaryLabel;
        private ToolTip? _tooltips;

        // Event handlers (stored for cleanup)
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        private EventHandler? _comboDepartmentSelectedHandler;
        private EventHandler? _comboDateRangeSelectedHandler;
        private EventHandler? _btnApplyFilterClickHandler;
        private EventHandler? _btnResetClickHandler;
        private EventHandler? _btnExportReportClickHandler;
        private NotifyCollectionChangedEventHandler? _analyticsDataCollectionChangedHandler;

        /// <summary>
        /// Initializes a new instance of BudgetAnalyticsPanel.
        /// </summary>
        public BudgetAnalyticsPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase> logger)
            : base(scopeFactory, logger)
        {
            // No need to assign _scopeFactory; handled by base class
        }

        #region ICompletablePanel Overrides

        /// <summary>
        /// Loads the panel asynchronously (ICompletablePanel implementation).
        /// Initializes ViewModel and loads analytics data.
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            if (IsLoaded) return;

            try
            {
                IsBusy = true;

                if (ViewModel != null && !DesignMode)
                {
                    if (ViewModel.LoadDataCommand.CanExecute(null))
                    {
                        await ViewModel.LoadDataCommand.ExecuteAsync(null);
                    }
                }

                _logger?.LogDebug("BudgetAnalyticsPanel loaded successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("BudgetAnalyticsPanel load cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load BudgetAnalyticsPanel");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Saves the panel asynchronously (ICompletablePanel implementation).
        /// Budget analytics panel is read-only, so this is a no-op.
        /// </summary>
        public override async Task SaveAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;

                // Budget analytics panel is view-only; no persistence required.
                await Task.CompletedTask;
                _logger?.LogDebug("BudgetAnalyticsPanel save completed");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("BudgetAnalyticsPanel save cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save BudgetAnalyticsPanel");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Validates the panel asynchronously (ICompletablePanel implementation).
        /// Ensures analytics data is available.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            try
            {
                IsBusy = true;

                var errors = new List<ValidationItem>();

                if (ViewModel == null)
                {
                    errors.Add(new ValidationItem("ViewModel", "ViewModel not initialized", ValidationSeverity.Error));
                    _errorProvider?.SetError(this, "ViewModel not initialized");
                }
                else if (!ViewModel.AnalyticsData.Any())
                {
                    errors.Add(new ValidationItem("Data", "No analytics data available", ValidationSeverity.Warning));
                    if (_analyticsGrid != null)
                        _errorProvider?.SetError(_analyticsGrid, "No analytics data available");
                }
                else
                {
                    _errorProvider?.SetError(this, string.Empty);
                }

                await Task.CompletedTask;

                return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failed(errors.ToArray());
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("BudgetAnalyticsPanel validation cancelled");
                return ValidationResult.Failed(new ValidationItem("Cancelled", "Validation was cancelled", ValidationSeverity.Info));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Validation error in BudgetAnalyticsPanel");
                _errorProvider?.SetError(this, "Validation failed");
                return ValidationResult.Failed(new ValidationItem("Validation", ex.Message, ValidationSeverity.Error));
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Focuses the first validation error control (ICompletablePanel implementation).
        /// </summary>
        public override void FocusFirstError()
        {
            if (_comboDepartment?.Visible == true) _comboDepartment.Focus();
            else if (_comboDateRange?.Visible == true) _comboDateRange.Focus();
            else if (_analyticsGrid?.Visible == true) _analyticsGrid.Focus();
            else _filterPanel?.Focus();
        }

        #endregion

        /// <summary>
        /// Called after the ViewModel has been resolved. Initializes UI and bindings.
        /// </summary>
        protected override void OnViewModelResolved(BudgetAnalyticsViewModel viewModel)
        {
            base.OnViewModelResolved(viewModel);

            SetupUI();
            BindViewModel();

            _logger?.LogDebug("BudgetAnalyticsPanel: ViewModel resolved and UI initialized");
        }

        /// <summary>
        /// Loads the panel and initializes the ViewModel.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (ViewModel != null && !DesignMode)
            {
                // Queue async loading on the UI thread
                BeginInvoke(new Func<Task>(async () =>
                {
                    try
                    {
                        if (ViewModel.LoadDataCommand.CanExecute(null))
                        {
                            await ViewModel.LoadDataCommand.ExecuteAsync(null);
                        }
                        UpdateUI();
                        UpdateNoDataOverlay();

                        // Defer sizing validation
                        DeferSizeValidation();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to initialize BudgetAnalyticsPanel");
                    }
                }));
            }
        }

        #region UI Setup

        private void SetupUI()
        {
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            // Create root TableLayoutPanel
            var rootTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                AutoSize = true,
                Padding = new Padding(8),
                AccessibleName = "Budget Analytics Layout"
            };

            // Configure rows
            rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: Header and filter panel
            rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50F)); // Row 2: Charts (50% of space)
            rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50F)); // Row 3: Grid (50% of space)

            // Row 1: Panel header and filter panel
            var headerTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                AutoSize = true,
                AccessibleName = "Header and Filter Layout"
            };
            headerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            headerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Filter panel

            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Fill,
                TabIndex = 0,
                AccessibleName = "Budget Analytics Header",
                AccessibleDescription = "Header with refresh and help buttons"
            };
            _panelHeader.Title = "Budget Analytics";
            _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
            _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
            _panelHeader.HelpClicked += (s, e) => Dialogs.ChartWizardFaqDialog.ShowModal(this);
            _panelHeaderCloseHandler = (s, e) => ClosePanel();
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            headerTable.Controls.Add(_panelHeader, 0, 0);

            // Filter panel
            CreateFilterPanel();
            headerTable.Controls.Add(_filterPanel!, 0, 1);

            rootTable.Controls.Add(headerTable, 0, 0);

            // Row 2: Charts side-by-side
            var chartsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
                AccessibleName = "Charts Layout"
            };
            chartsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Trend chart
            chartsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Department chart

            _trendChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Budget trend chart",
                AccessibleDescription = "Displays budget vs actual trend over time"
            };
            ConfigureTrendChart();
            chartsTable.Controls.Add(_trendChart, 0, 0);

            _departmentChart = new ChartControl
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Department performance chart",
                AccessibleDescription = "Displays department budget performance"
            };
            ConfigureDepartmentChart();
            chartsTable.Controls.Add(_departmentChart, 1, 0);

            rootTable.Controls.Add(chartsTable, 0, 1);

            // Row 3: Analytics grid
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
            rootTable.Controls.Add(_analyticsGrid, 0, 2);

            // Add root table to panel
            Controls.Add(rootTable);

            // Summary label (bottom of panel)
            _summaryLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "Ready",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                Padding = new Padding(0, 0, 8, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_summaryLabel);

            // Overlays
            _loadingOverlay = new LoadingOverlay { Message = "Loading analytics..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay
            {
                Message = "No analytics data available\r\nAdd budget entries and accounts to generate analytics",
                Dock = DockStyle.Fill
            };
            Controls.Add(_noDataOverlay);

            this.PerformLayout();
            this.Refresh();
            _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
        }

        /// <summary>
        /// Creates the filter panel with proper TableLayoutPanel layout.
        /// </summary>
        private void CreateFilterPanel()
        {
            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            _filterPanel = new GradientPanelExt
            {
                Dock = DockStyle.Fill,
                Height = 100,
                Padding = new Padding(8),
                BorderStyle = BorderStyle.FixedSingle,
                CornerRadius = 2,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
                AccessibleName = "Filter Panel"
            };
            SfSkinManager.SetVisualStyle(_filterPanel, currentTheme);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true
            };

            // Tooltip manager for all controls
            _tooltips = new ToolTip();

            // Department filter
            var lblDept = new Label
            {
                Text = "Department:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 4, 0)
            };
            _tooltips.SetToolTip(lblDept, "Select department to filter analytics");
            flow.Controls.Add(lblDept);

            _comboDepartment = new SfComboBox
            {
                Width = 180,
                Height = 28,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Department filter",
                AccessibleDescription = "Filter analytics by department"
            };
            _comboDepartmentSelectedHandler = ComboFilter_SelectedIndexChanged;
            _comboDepartment.SelectedIndexChanged += _comboDepartmentSelectedHandler;
            _tooltips.SetToolTip(_comboDepartment, "Select department to filter analytics by department");
            flow.Controls.Add(_comboDepartment);

            // Date range filter
            var lblDate = new Label
            {
                Text = "Date Range:",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 6, 4, 0)
            };
            _tooltips.SetToolTip(lblDate, "Select date range to filter analytics");
            flow.Controls.Add(lblDate);

            _comboDateRange = new SfComboBox
            {
                Width = 180,
                Height = 28,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Date range filter",
                AccessibleDescription = "Filter analytics by date range"
            };
            _comboDateRangeSelectedHandler = ComboFilter_SelectedIndexChanged;
            _comboDateRange.SelectedIndexChanged += _comboDateRangeSelectedHandler;
            _tooltips.SetToolTip(_comboDateRange, "Select date range to filter analytics by date range");
            flow.Controls.Add(_comboDateRange);

            // Buttons using FlowLayoutPanel
            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Size = new Size(320, 88),
                WrapContents = false
            };

            _btnApplyFilter = new SfButton
            {
                Text = "Apply Filter",
                AutoSize = false,
                Size = new Size(95, 32),
                Margin = new Padding(3),
                AccessibleName = "Apply analytics filter",
                AccessibleDescription = "Apply selected filters to analytics"
            };
            _btnApplyFilterClickHandler = async (s, e) => await RefreshDataAsync();
            _btnApplyFilter.Click += _btnApplyFilterClickHandler;
            _tooltips.SetToolTip(_btnApplyFilter, "Apply selected filters to analytics data");
            btnFlow.Controls.Add(_btnApplyFilter);

            _btnReset = new SfButton
            {
                Text = "Reset",
                AutoSize = false,
                Size = new Size(70, 32),
                Margin = new Padding(3),
                AccessibleName = "Reset analytics filters",
                AccessibleDescription = "Clear all filter selections"
            };
            _btnResetClickHandler = (s, e) => ResetFilters();
            _btnReset.Click += _btnResetClickHandler;
            _tooltips.SetToolTip(_btnReset, "Clear all filter selections");
            btnFlow.Controls.Add(_btnReset);

            _btnExportReport = new SfButton
            {
                Text = "📊 Export Report",
                AutoSize = false,
                Size = new Size(130, 32),
                Margin = new Padding(3),
                AccessibleName = "Export analytics report",
                AccessibleDescription = "Export analytics data to CSV"
            };
            _btnExportReportClickHandler = async (s, e) => await ExportReportAsync();
            _btnExportReport.Click += _btnExportReportClickHandler;
            _tooltips.SetToolTip(_btnExportReport, "Export analytics data to CSV file");
            btnFlow.Controls.Add(_btnExportReport);

            flow.Controls.Add(btnFlow);
            _filterPanel.Controls.Add(flow);
        }

        private void ConfigureTrendChart()
        {
            if (_trendChart == null) return;

            ChartControlDefaults.Apply(_trendChart, logger: _logger);
            _trendChart.ShowLegend = true;
            _trendChart.LegendsPlacement = ChartPlacement.Outside;  // Legend below the chart for better readability
            _trendChart.Title.Text = "Budget Trend (Currency $)";
            _trendChart.Title.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _trendChart.PrimaryXAxis.Title = "Period";
            _trendChart.PrimaryXAxis.Font = new Font("Segoe UI", 9F);
            _trendChart.PrimaryXAxis.DrawGrid = false;
            _trendChart.PrimaryYAxis.Title = "Amount ($)";
            _trendChart.PrimaryYAxis.Font = new Font("Segoe UI", 9F);
            _trendChart.PrimaryYAxis.TitleFont = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Configure Y-axis to display currency formatting
            try
            {
                var yAxis = _trendChart.PrimaryYAxis;
                var propNumFormat = yAxis.GetType().GetProperty("NumberFormat");
                if (propNumFormat != null && propNumFormat.CanWrite)
                {
                    propNumFormat.SetValue(yAxis, "C0");  // Currency format like $100K
                }
            }
            catch { /* Gracefully handle if property doesn't exist */ }

            _trendChart.TabIndex = 10;
        }

        private void ConfigureDepartmentChart()
        {
            if (_departmentChart == null) return;

            ChartControlDefaults.Apply(_departmentChart, logger: _logger);
            _departmentChart.ShowLegend = true;
            _departmentChart.LegendsPlacement = ChartPlacement.Outside;  // Legend below the chart for consistency
            _departmentChart.Title.Text = "Department Performance";
            _departmentChart.Title.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _departmentChart.PrimaryXAxis.Title = "Department";
            _departmentChart.PrimaryXAxis.Font = new Font("Segoe UI", 9F);
            _departmentChart.PrimaryXAxis.DrawGrid = false;
            _departmentChart.PrimaryYAxis.Title = "Variance %";
            _departmentChart.PrimaryYAxis.Font = new Font("Segoe UI", 9F);
            _departmentChart.PrimaryYAxis.TitleFont = new Font("Segoe UI", 9F, FontStyle.Bold);

            // Configure Y-axis to display percentage formatting
            try
            {
                var yAxis = _departmentChart.PrimaryYAxis;
                var propNumFormat = yAxis.GetType().GetProperty("NumberFormat");
                if (propNumFormat != null && propNumFormat.CanWrite)
                {
                    propNumFormat.SetValue(yAxis, "P0");  // Percentage format like 5%
                }
            }
            catch { /* Gracefully handle if property doesn't exist */ }

            _departmentChart.TabIndex = 11;
        }

        private void ConfigureGridColumns()
        {
            if (_analyticsGrid == null) return;

            // Department column - Text, left aligned
            _analyticsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "DepartmentName",
                HeaderText = "Department",
                MinimumWidth = 150,
                AllowSorting = true,
                AllowResizing = true
            });

            // Period column - Text, left aligned
            _analyticsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "PeriodName",
                HeaderText = "Period",
                MinimumWidth = 100,
                AllowSorting = true,
                AllowResizing = true
            });

            // Budgeted Amount - Currency, right aligned
            _analyticsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "BudgetedAmount",
                HeaderText = "Budgeted",
                MinimumWidth = 140,
                Format = "C2",
                AllowSorting = true,
                AllowResizing = true
            });

            // Actual Amount - Currency, right aligned
            _analyticsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "ActualAmount",
                HeaderText = "Actual",
                MinimumWidth = 140,
                Format = "C2",
                AllowSorting = true,
                AllowResizing = true
            });

            // Variance Amount - Currency, right aligned
            _analyticsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "VarianceAmount",
                HeaderText = "Variance ($)",
                MinimumWidth = 140,
                Format = "C2",
                AllowSorting = true,
                AllowResizing = true
            });

            // Variance Percent - Text, center aligned (status indicator)
            _analyticsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "VariancePercent",
                HeaderText = "Variance %",
                MinimumWidth = 110,
                AllowSorting = true,
                AllowResizing = true
            });

            // Status column - Text, center aligned
            _analyticsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Status",
                HeaderText = "Status",
                MinimumWidth = 120,
                AllowSorting = true,
                AllowResizing = true
            });

            _analyticsGrid.TabIndex = 12;
        }

        private void BindViewModel()
        {
            if (ViewModel == null) return;

            try
            {
                // Create BindingSource for MVVM grid binding
                _bindingSource = new BindingSource { DataSource = ViewModel };
                if (_analyticsGrid != null) _analyticsGrid.DataSource = _bindingSource;

                // Bind filter dropdowns using adapters for live collection updates
                // When ObservableCollection items are added/removed in ViewModel, UI automatically updates
                if (_comboDepartment != null)
                {
                    _departmentAdapter = new ObservableCollectionToBindingListAdapter<string>(ViewModel.AvailableDepartments);
                    _comboDepartment.DataSource = _departmentAdapter;
                    _comboDepartment.DisplayMember = ""; // Strings don't need member binding
                    _comboDepartment.ValueMember = "";
                }

                if (_comboDateRange != null)
                {
                    _dateRangeAdapter = new ObservableCollectionToBindingListAdapter<string>(ViewModel.AvailableDateRanges);
                    _comboDateRange.DataSource = _dateRangeAdapter;
                    _comboDateRange.DisplayMember = "";
                    _comboDateRange.ValueMember = "";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to bind filter controls");
            }

            // Subscribe to property changes
            _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

            // Subscribe to collection changes
            _analyticsDataCollectionChangedHandler = AnalyticsData_CollectionChanged;
            ViewModel.AnalyticsData.CollectionChanged += _analyticsDataCollectionChangedHandler;

            // Initial UI update
            UpdateUI();

            _logger?.LogDebug("BudgetAnalyticsPanel: ViewModel bound to UI");
        }

        private void AnalyticsData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)(() => AnalyticsData_CollectionChanged(sender, e)));
                    }
                    catch { /* Control disposed */ }
                }
                return;
            }

            if (!IsDisposed)
            {
                UpdateUI();
                UpdateNoDataOverlay();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                if (InvokeRequired)
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        try
                        {
                            BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
                        }
                        catch { /* Control disposed */ }
                    }
                    return;
                }

                if (ViewModel == null) return;

                switch (e.PropertyName)
                {
                    case nameof(ViewModel.IsLoading):
                        if (_loadingOverlay != null) _loadingOverlay.Visible = ViewModel.IsLoading;
                        break;

                    case nameof(ViewModel.AnalyticsData):
                    case nameof(ViewModel.SelectedDepartment):
                    case nameof(ViewModel.SelectedDateRange):
                        UpdateUI();
                        UpdateNoDataOverlay();
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
                _logger?.LogWarning(ex, "BudgetAnalyticsPanel: PropertyChanged handler failed");
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (IsDisposed || ViewModel == null) return;

                // Refresh grid via BindingSource
                if (_analyticsGrid != null && _bindingSource != null)
                {
                    try
                    {
                        _analyticsGrid.SuspendLayout();
                        _bindingSource.DataSource = ViewModel.AnalyticsData.ToList();
                        _analyticsGrid.ResumeLayout();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to update grid");
                    }
                }

                // Update charts
                UpdateTrendChart();
                UpdateDepartmentChart();

                // Update summary label
                if (_summaryLabel != null)
                {
                    _summaryLabel.Text = $"Displaying {ViewModel.AnalyticsData.Count} records • Updated: {DateTime.Now:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "BudgetAnalyticsPanel: UpdateUI failed");
            }
        }

        private void UpdateNoDataOverlay()
        {
            if (_noDataOverlay == null || ViewModel == null) return;

            var hasData = ViewModel.AnalyticsData.Count > 0;
            _noDataOverlay.Visible = !hasData && !ViewModel.IsLoading;
        }

        private void UpdateTrendChart()
        {
            if (_trendChart == null || ViewModel == null) return;

            try
            {
                _trendChart.Series.Clear();

                var budgetSeries = new ChartSeries("Budgeted", ChartSeriesType.Line);
                var actualSeries = new ChartSeries("Actual", ChartSeriesType.Line);

                foreach (var data in ViewModel.AnalyticsData.Take(12))
                {
                    budgetSeries.Points.Add(data.PeriodName, (double)data.BudgetedAmount);
                    actualSeries.Points.Add(data.PeriodName, (double)data.ActualAmount);
                }

                _trendChart.Series.Add(budgetSeries);
                _trendChart.Series.Add(actualSeries);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "BudgetAnalyticsPanel: UpdateTrendChart failed");
            }
        }

        private void UpdateDepartmentChart()
        {
            if (_departmentChart == null || ViewModel == null) return;

            try
            {
                _departmentChart.Series.Clear();

                var varianceSeries = new ChartSeries("Variance %", ChartSeriesType.Column);

                var deptData = ViewModel.AnalyticsData
                    .GroupBy(x => x.DepartmentName)
                    .Select(g => new { Department = g.Key, AvgVariance = g.Average(x => double.Parse(x.VariancePercent ?? "0", CultureInfo.InvariantCulture)) })
                    .Take(10);

                foreach (var dept in deptData)
                {
                    varianceSeries.Points.Add(dept.Department, dept.AvgVariance);
                }

                _departmentChart.Series.Add(varianceSeries);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "BudgetAnalyticsPanel: UpdateDepartmentChart failed");
            }
        }

        private void ComboFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            try
            {
                if (ViewModel == null) return;

                if (_comboDepartment?.SelectedItem is string dept)
                    ViewModel.SelectedDepartment = dept;

                if (_comboDateRange?.SelectedItem is string range)
                    ViewModel.SelectedDateRange = range;

                // Reload data with applied filters
                if (ViewModel.LoadDataCommand.CanExecute(null))
                {
                    _ = ViewModel.LoadDataCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update filter selection");
            }
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

        /// <summary>
        /// Updates the status label with current operation state.
        /// </summary>
        private void UpdateStatus(string message)
        {
            this.InvokeIfRequired(() =>
            {
                try
                {
                    if (_summaryLabel != null && !_summaryLabel.IsDisposed)
                        _summaryLabel.Text = message ?? "Ready";
                }
                catch { }
            });
        }

        private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
        {
            if (ViewModel == null) return;

            try
            {
                UpdateStatus("Refreshing analytics data...");
                if (ViewModel.RefreshCommand.CanExecute(null))
                {
                    await ViewModel.RefreshCommand.ExecuteAsync(null);
                }
                UpdateStatus("Analytics refreshed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Refresh cancelled");
                UpdateStatus("Refresh cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "BudgetAnalyticsPanel: Refresh failed");
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ExportReportAsync(CancellationToken cancellationToken = default)
        {
            if (ViewModel == null) return;

            try
            {
                UpdateStatus("Preparing export...");
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"budget-analytics-{DateTime.Now:yyyyMMdd}.csv",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Department,Period,Budgeted,Actual,Variance,Variance %,Status");

                foreach (var data in ViewModel.AnalyticsData)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "\"{0}\",\"{1}\",{2},{3},{4},{5},\"{6}\"",
                        data.DepartmentName,
                        data.PeriodName,
                        data.BudgetedAmount.ToString(CultureInfo.InvariantCulture),
                        data.ActualAmount.ToString(CultureInfo.InvariantCulture),
                        data.VarianceAmount.ToString(CultureInfo.InvariantCulture),
                        data.VariancePercent,
                        data.Status));
                }

                await System.IO.File.WriteAllTextAsync(sfd.FileName, sb.ToString(), cancellationToken);
                UpdateStatus($"Export complete: {sfd.FileName}");
                MessageBox.Show($"Exported to {sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Export cancelled");
                UpdateStatus("Export cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "BudgetAnalyticsPanel: Export failed");
                UpdateStatus($"Export failed: {ex.Message}");
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
                _logger?.LogWarning(ex, "BudgetAnalyticsPanel: ClosePanel failed");
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

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from ViewModel events
                try
                {
                    if (_viewModelPropertyChangedHandler != null && ViewModel != null)
                        ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
                }
                catch { }

                try
                {
                    if (_analyticsDataCollectionChangedHandler != null && ViewModel?.AnalyticsData != null)
                        ViewModel.AnalyticsData.CollectionChanged -= _analyticsDataCollectionChangedHandler;
                }
                catch { }

                // Unsubscribe from Panel Header events
                try
                {
                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    }
                }
                catch { }

                // Unsubscribe from Filter controls events
                try
                {
                    if (_comboDepartment != null && _comboDepartmentSelectedHandler != null)
                        _comboDepartment.SelectedIndexChanged -= _comboDepartmentSelectedHandler;
                }
                catch { }

                try
                {
                    if (_comboDateRange != null && _comboDateRangeSelectedHandler != null)
                        _comboDateRange.SelectedIndexChanged -= _comboDateRangeSelectedHandler;
                }
                catch { }

                // Unsubscribe from Button events
                try
                {
                    if (_btnApplyFilter != null && _btnApplyFilterClickHandler != null)
                        _btnApplyFilter.Click -= _btnApplyFilterClickHandler;
                }
                catch { }

                try
                {
                    if (_btnReset != null && _btnResetClickHandler != null)
                        _btnReset.Click -= _btnResetClickHandler;
                }
                catch { }

                try
                {
                    if (_btnExportReport != null && _btnExportReportClickHandler != null)
                        _btnExportReport.Click -= _btnExportReportClickHandler;
                }
                catch { }

                // Dispose binding adapters (must be before control disposal)
                try { _departmentAdapter?.Dispose(); } catch { }
                try { _dateRangeAdapter?.Dispose(); } catch { }

                // Dispose controls
                try { _bindingSource?.Dispose(); } catch { }
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
                try { _tooltips?.Dispose(); } catch { }
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
