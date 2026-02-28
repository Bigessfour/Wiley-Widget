using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Input.Enums;
using Syncfusion.WinForms.ListView;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.UI.Helpers;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Audit Log viewer panel — displays audit entries with date/action/user filtering,
/// CSV export, an events-over-time chart, and optional auto-refresh.
/// Follows the Sacred Panel Skeleton: factory-created controls, SfSkinManager theme cascade.
/// </summary>
public partial class AuditLogPanel : ScopedPanelBase<AuditLogViewModel>
{
    // --- Sacred Skeleton fields -----------------------------------------------
    private readonly AuditLogViewModel _vm;
    private readonly SyncfusionControlFactory _factory;
    private PanelHeader? _header;
    private TableLayoutPanel? _content;
    private LoadingOverlay? _loader;

    // --- AuditLog-specific controls -------------------------------------------
    private NoDataOverlay? _noDataOverlay;
    private LoadingOverlay? _chartLoadingOverlay;
    private Panel? _filterPanel;
    private SfDataGrid? _auditGrid;
    private SplitContainer? _mainSplit;          // native — SafeSplitterDistanceHelper requires SplitContainer
    private Panel? _chartHostPanel;
    private ChartControl? _chartControl;
    private SfButton? _btnRefresh;
    private SfButton? _btnExportCsv;
    private SfButton? _btnUpdateChart;
    private CheckBoxAdv? _chkAutoRefresh;
    private SfDateTimeEdit? _dtpStartDate;
    private SfDateTimeEdit? _dtpEndDate;
    private SfComboBox? _cmbActionType;
    private SfComboBox? _cmbUser;
    private SfComboBox? _cmbChartGroupBy;
    private Label? _lblChartSummary;
    private BindingSource? _bindingSource;
    private System.Windows.Forms.Timer? _autoRefreshTimer;

    // --- Event-handler fields (for clean Dispose) -----------------------------
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _entriesCollectionChangedHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _chartDataCollectionChangedHandler;
    private EventHandler? _btnRefreshClickHandler;
    private EventHandler? _btnExportCsvClickHandler;
    private EventHandler? _btnUpdateChartClickHandler;
    private EventHandler? _chkAutoRefreshCheckedChangedHandler;
    private Syncfusion.WinForms.Input.Events.DateTimeValueChangedEventHandler? _dtpStartDateValueChangedHandler;
    private Syncfusion.WinForms.Input.Events.DateTimeValueChangedEventHandler? _dtpEndDateValueChangedHandler;
    private EventHandler? _cmbActionTypeSelectedIndexChangedHandler;
    private EventHandler? _cmbUserSelectedIndexChangedHandler;
    private EventHandler? _cmbChartGroupBySelectedIndexChangedHandler;

    // =========================================================================
    #region Construction
    // =========================================================================

    /// <summary>
    /// Initializes the panel with directly-injected dependencies (Sacred Skeleton ctor).
    /// </summary>
    public AuditLogPanel(
        AuditLogViewModel vm,
        SyncfusionControlFactory factory)
        : base(vm, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<AuditLogPanel>>(Program.Services))
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        MinimumSize = new Size(RecommendedDockedPanelMinimumLogicalWidth,
                               RecommendedDockedPanelMinimumLogicalHeight);
        Dock = DockStyle.Fill;
        AutoScaleMode = AutoScaleMode.Dpi;
        SafeSuspendAndLayout(InitializeLayout);
        BindViewModel();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MinimumSize = RecommendedDockedPanelMinimumLogicalSize;
        PerformLayout();
    }

    /// <summary>
    /// Called by base constructor before InitializeLayout — controls are not yet built here.
    /// All ViewModel binding is deferred to BindViewModel().
    /// </summary>
    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        // Intentionally empty: controls not available until InitializeLayout completes.
    }

    #endregion

    // =========================================================================
    #region Layout
    // =========================================================================

    private void InitializeLayout()
    {
        // Panel header
        _header = new PanelHeader(_factory)
        {
            Title = "Audit Log",
            AccessibleName = "Audit Log panel header"
        };
        _header.RefreshClicked += async (s, e) =>
        {
            await _vm.LoadEntriesAsync();
            await _vm.LoadChartDataAsync();
        };

        _content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Filter panel
        _filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(8),
            BorderStyle = BorderStyle.None,
            AccessibleName = "Audit log filters"
        };

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            AutoSize = true
        };
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Row 0: date range
        filterTable.Controls.Add(new Label
        {
            Text = "Start Date:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Start date label"
        }, 0, 0);

        _dtpStartDate = _factory.CreateSfDateTimeEdit(d =>
        {
            d.Width = 140;
            d.DateTimePattern = DateTimePattern.ShortDate;
            d.AccessibleName = "Start date filter";
            d.AccessibleDescription = "Filter audit entries from this date";
        });
        _dtpStartDateValueChangedHandler = (s, e) => ApplyFilters();
        _dtpStartDate.ValueChanged += _dtpStartDateValueChangedHandler;
        filterTable.Controls.Add(_dtpStartDate, 1, 0);

        filterTable.Controls.Add(new Label
        {
            Text = "End Date:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "End date label"
        }, 2, 0);

        _dtpEndDate = _factory.CreateSfDateTimeEdit(d =>
        {
            d.Width = 140;
            d.DateTimePattern = DateTimePattern.ShortDate;
            d.AccessibleName = "End date filter";
            d.AccessibleDescription = "Filter audit entries until this date";
        });
        _dtpEndDateValueChangedHandler = (s, e) => ApplyFilters();
        _dtpEndDate.ValueChanged += _dtpEndDateValueChangedHandler;
        filterTable.Controls.Add(_dtpEndDate, 3, 0);

        // Row 1: action type and user
        filterTable.Controls.Add(new Label
        {
            Text = "Action Type:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Action type label"
        }, 0, 1);

        _cmbActionType = _factory.CreateSfComboBox(c =>
        {
            c.Width = 140;
            c.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            c.MaxDropDownItems = 10;
            c.AllowNull = true;
            c.Watermark = "All Actions";
            c.AccessibleName = "Action type filter";
            c.AccessibleDescription = "Filter audit entries by action type";
        });
        _cmbActionTypeSelectedIndexChangedHandler = (s, e) => ApplyFilters();
        _cmbActionType.SelectedIndexChanged += _cmbActionTypeSelectedIndexChangedHandler;
        filterTable.Controls.Add(_cmbActionType, 1, 1);

        filterTable.Controls.Add(new Label
        {
            Text = "User:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "User label"
        }, 2, 1);

        _cmbUser = _factory.CreateSfComboBox(c =>
        {
            c.Width = 140;
            c.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            c.MaxDropDownItems = 10;
            c.AllowNull = true;
            c.Watermark = "All Users";
            c.AccessibleName = "User filter";
            c.AccessibleDescription = "Filter audit entries by user";
        });
        _cmbUserSelectedIndexChangedHandler = (s, e) => ApplyFilters();
        _cmbUser.SelectedIndexChanged += _cmbUserSelectedIndexChangedHandler;
        filterTable.Controls.Add(_cmbUser, 3, 1);

        // Buttons
        _btnRefresh = _factory.CreateSfButton("Refresh", b =>
        {
            b.Width = 100;
            b.Height = 32;
            b.AccessibleName = "Refresh audit log";
            b.AccessibleDescription = "Reload audit entries from database";
        });
        _btnRefreshClickHandler = async (s, e) =>
        {
            await _vm.LoadEntriesAsync();
            await _vm.LoadChartDataAsync();
        };
        _btnRefresh.Click += _btnRefreshClickHandler;
        filterTable.Controls.Add(_btnRefresh, 4, 0);

        _chkAutoRefresh = _factory.CreateCheckBoxAdv("Auto-refresh", c =>
        {
            c.AutoSize = true;
            c.AccessibleName = "Auto-refresh toggle";
            c.AccessibleDescription = "Automatically refresh audit log every 30 seconds";
        });
        _chkAutoRefreshCheckedChangedHandler = (s, e) => ToggleAutoRefresh();
        _chkAutoRefresh.CheckedChanged += _chkAutoRefreshCheckedChangedHandler;
        filterTable.Controls.Add(_chkAutoRefresh, 4, 1);

        _btnExportCsv = _factory.CreateSfButton("Export CSV", b =>
        {
            b.Width = 100;
            b.Height = 32;
            b.AccessibleName = "Export to CSV";
            b.AccessibleDescription = "Export filtered audit entries to CSV file";
        });
        _btnExportCsvClickHandler = async (s, e) => await ExportToCsvAsync();
        _btnExportCsv.Click += _btnExportCsvClickHandler;
        filterTable.Controls.Add(_btnExportCsv, 5, 0);

        _filterPanel.Controls.Add(filterTable);

        // Chart options toolbar
        var chartOptionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            AccessibleName = "Chart options flow"
        };

        chartOptionsFlow.Controls.Add(new Label
        {
            Text = "Chart Period:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 8, 8, 8),
            AccessibleName = "Chart grouping label"
        });

        _cmbChartGroupBy = _factory.CreateSfComboBox(c =>
        {
            c.Width = 120;
            c.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            c.AllowNull = false;
            c.Watermark = "Group";
            c.AccessibleName = "Chart grouping";
            c.AccessibleDescription = "Select chart grouping period (Day, Week, Month)";
        });
        _cmbChartGroupBy.DataSource = Enum.GetNames(typeof(AuditLogViewModel.ChartGroupingPeriod));
        _cmbChartGroupBySelectedIndexChangedHandler = async (s, e) => await _vm.LoadChartDataAsync();
        _cmbChartGroupBy.SelectedIndexChanged += _cmbChartGroupBySelectedIndexChangedHandler;
        chartOptionsFlow.Controls.Add(_cmbChartGroupBy);

        _btnUpdateChart = _factory.CreateSfButton("Update Chart", b =>
        {
            b.Width = 100;
            b.Height = 28;
            b.AccessibleName = "Update chart";
            b.AccessibleDescription = "Refresh chart with current filters";
        });
        _btnUpdateChartClickHandler = async (s, e) => await _vm.LoadChartDataAsync();
        _btnUpdateChart.Click += _btnUpdateChartClickHandler;
        chartOptionsFlow.Controls.Add(_btnUpdateChart);

        _filterPanel.Controls.Add(chartOptionsFlow);
        _content.Controls.Add(_filterPanel, 0, 0);

        // Main split (native SplitContainer - SafeSplitterDistanceHelper dependency)
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            AccessibleName = "Audit grid and chart split container"
        };
        ConfigureMainSplitContainer();

        // Left: audit grid
        _auditGrid = _factory.CreateSfDataGrid(g =>
        {
            g.Dock = DockStyle.Fill;
            g.AutoGenerateColumns = false;
            g.AllowFiltering = true;
            g.AllowSorting = true;
            g.AllowGrouping = false;
            g.ShowRowHeader = false;
            g.SelectionMode = GridSelectionMode.Single;
            g.AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells;
            g.EnableDataVirtualization = true;
            g.RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f);
            g.HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32.0f);
            g.AllowResizingColumns = true;
            g.AllowTriStateSorting = true;
            g.AccessibleName = "Audit log entries grid";
            g.AccessibleDescription = "Grid displaying audit log entries with timestamp, user, action, and details";
        });
        _auditGrid.PreventStringRelationalFilters(Logger, "User", "Action", "EntityType", "EntityId", "Changes");
        ConfigureGridColumns();
        _mainSplit.Panel1.Controls.Add(_auditGrid);

        // Right: chart host
        _chartHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            AccessibleName = "Chart host panel",
            AccessibleDescription = "Displays chart of audit events"
        };

        _chartControl = _factory.CreateChartControl("Audit Events", c =>
        {
            c.Dock = DockStyle.Fill;
            c.AccessibleName = "Audit events chart";
            c.AccessibleDescription = "Chart showing counts of audit events over time";
        });
        ConfigureChartForAudit();
        _chartHostPanel.Controls.Add(_chartControl);

        _chartLoadingOverlay = _factory.CreateLoadingOverlay(o =>
        {
            o.Message = "Loading chart data...";
            o.AccessibleName = "Chart loading overlay";
        });
        _chartHostPanel.Controls.Add(_chartLoadingOverlay);

        _lblChartSummary = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            AccessibleName = "Chart summary",
            AccessibleDescription = "Summary statistics for chart data"
        };
        _chartHostPanel.Controls.Add(_lblChartSummary);
        _mainSplit.Panel2.Controls.Add(_chartHostPanel);
        _content.Controls.Add(_mainSplit, 0, 1);

        Controls.Add(_content);
        Controls.Add(_header);
        _header.BringToFront();

        // Overlays
        _loader = _factory.CreateLoadingOverlay(o =>
        {
            o.Message = "Loading audit entries...";
            o.Dock = DockStyle.Fill;
            o.AccessibleName = "Loading overlay";
        });
        Controls.Add(_loader);
        _loader.BringToFront();

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No audit entries found",
            Dock = DockStyle.Fill,
            AccessibleName = "No data overlay"
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        _bindingSource = new BindingSource();
    }

    #endregion

    // =========================================================================
    #region ViewModel binding
    // =========================================================================

    private void BindViewModel()
    {
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        _vm.PropertyChanged += _viewModelPropertyChangedHandler;

        _entriesCollectionChangedHandler = (s, e) => UpdateGridData();
        _vm.Entries.CollectionChanged += _entriesCollectionChangedHandler;

        _chartDataCollectionChangedHandler = (s, e) => UpdateChart();
        _vm.ChartData.CollectionChanged += _chartDataCollectionChangedHandler;

        if (_cmbChartGroupBy != null)
        {
            try { _cmbChartGroupBy.SelectedItem = _vm.ChartGrouping.ToString(); } catch { }
        }

        InitializeFilters();
    }

    private void InitializeFilters()
    {
        var endDate = DateTime.Now;
        var startDate = endDate.AddDays(-30);

        if (_dtpStartDate != null) _dtpStartDate.Value = startDate;
        if (_dtpEndDate != null) _dtpEndDate.Value = endDate;

        _vm.StartDate = startDate;
        _vm.EndDate = endDate;

        _ = PopulateActionTypesAsync();
        _ = PopulateUsersAsync();

        try
        {
            if (_cmbChartGroupBy != null)
                _cmbChartGroupBy.SelectedItem = AuditLogViewModel.ChartGroupingPeriod.Month.ToString();
        }
        catch { }
    }

    #endregion

    // =========================================================================
    #region Grid / Chart configuration
    // =========================================================================

    private void ConfigureMainSplitContainer()
    {
        if (_mainSplit == null) return;

        const int panelMinSize = 300;
        const int defaultDistance = 520;

        SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(
            _mainSplit,
            panel1MinSize: panelMinSize,
            panel2MinSize: panelMinSize,
            desiredDistance: defaultDistance);

        _mainSplit.SizeChanged += (s, e) =>
        {
            if (_mainSplit.IsDisposed) return;
            var desired = (int)(_mainSplit.Width * 0.65);
            SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplit, desired);
        };
    }

    private void ConfigureGridColumns()
    {
        if (_auditGrid == null) return;

        _auditGrid.Columns.Clear();

        _auditGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = nameof(AuditEntry.Timestamp),
            HeaderText = "Timestamp",
            MinimumWidth = 150,
            Format = "yyyy-MM-dd HH:mm:ss",
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.User),
            HeaderText = "User",
            MinimumWidth = 120,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.Action),
            HeaderText = "Action",
            MinimumWidth = 120,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.EntityType),
            HeaderText = "Entity Type",
            MinimumWidth = 120,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.EntityId),
            HeaderText = "Entity ID",
            MinimumWidth = 100,
            AllowSorting = true,
            AllowFiltering = true
        });

        _auditGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(AuditEntry.Changes),
            HeaderText = "Details",
            MinimumWidth = 200,
            AllowSorting = false,
            AllowFiltering = true
        });
    }

    /// <summary>Configures ChartControl per Syncfusion API for audit events display.</summary>
    private void ConfigureChartForAudit()
    {
        if (_chartControl == null) return;

        try
        {
            _chartControl.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            _chartControl.BorderAppearance.SkinStyle = ChartBorderSkinStyle.None;
            _chartControl.ElementsSpacing = 5;

            _chartControl.PrimaryXAxis.ValueType = ChartValueType.DateTime;
            _chartControl.PrimaryXAxis.Title = "Date";
            _chartControl.PrimaryXAxis.LabelRotate = true;
            _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
            _chartControl.PrimaryXAxis.DrawGrid = true;

            _chartControl.PrimaryYAxis.Title = "Events";

            _chartControl.ShowToolTips = true;
            _chartControl.ShowLegend = false;

            try
            {
                var propZoom = _chartControl.GetType().GetProperty("EnableZooming");
                if (propZoom?.CanWrite == true) propZoom.SetValue(_chartControl, true);
            }
            catch { }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AUDIT_PANEL] ConfigureChartForAudit failed");
        }
    }

    #endregion

    // =========================================================================
    #region VM event handlers and data updates
    // =========================================================================

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(_vm.IsLoading):
                    if (_loader != null) _loader.Visible = _vm.IsLoading;
                    break;

                case nameof(_vm.IsChartLoading):
                    if (_chartLoadingOverlay != null) _chartLoadingOverlay.Visible = _vm.IsChartLoading;
                    break;

                case nameof(_vm.Entries):
                    UpdateGridData();
                    UpdateNoDataOverlay();
                    break;

                case nameof(_vm.TotalEvents):
                case nameof(_vm.PeakEvents):
                case nameof(_vm.LastChartUpdated):
                    UpdateChartSummary();
                    break;

                case nameof(_vm.ErrorMessage):
                    if (!string.IsNullOrEmpty(_vm.ErrorMessage))
                        SfDialogHelper.ShowErrorDialog(this, "Error", _vm.ErrorMessage, logger: Logger);
                    break;
            }
        }
        catch (ObjectDisposedException) { }
    }

    private void UpdateGridData()
    {
        if (_auditGrid == null) return;

        try
        {
            _auditGrid.SuspendLayout();
            _bindingSource ??= new BindingSource();

            var snapshot = _vm.FilteredAuditEntries.ToList();
            _bindingSource.DataSource = snapshot;
            _auditGrid.DataSource = _bindingSource;

            _auditGrid.ResumeLayout();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AUDIT_PANEL] UpdateGridData failed");
        }
    }

    /// <summary>Updates chart series from ViewModel.ChartData.</summary>
    private void UpdateChart()
    {
        if (_chartControl == null) return;

        if (_chartControl.InvokeRequired)
        {
            _chartControl.SafeInvoke(() => UpdateChart());
            return;
        }

        try
        {
            _chartControl.Series.Clear();

            if (!_vm.ChartData.Any())
            {
                _chartControl.Refresh();
                return;
            }

            var colSeries = new ChartSeries("Events", ChartSeriesType.Column);
            colSeries.Style.Border.Width = 1;

            foreach (var p in _vm.ChartData)
                colSeries.Points.Add(p.Period, (double)p.Count);

            colSeries.PointsToolTipFormat = "{1:N0}";
            _chartControl.Series.Add(colSeries);

            try
            {
                var xAxis = _chartControl.PrimaryXAxis;
                var propFmt = xAxis.GetType().GetProperty("DateTimeFormat");
                if (propFmt?.CanWrite == true)
                {
                    var fmt = _vm.ChartGrouping switch
                    {
                        AuditLogViewModel.ChartGroupingPeriod.Month => "MMM yyyy",
                        AuditLogViewModel.ChartGroupingPeriod.Day => "MMM dd",
                        _ => "yyyy-MM-dd"
                    };
                    propFmt.SetValue(xAxis, fmt);
                }
            }
            catch { }

            _chartControl.Refresh();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AUDIT_PANEL] UpdateChart failed");
        }
    }

    private void UpdateChartSummary()
    {
        if (_lblChartSummary == null) return;
        try
        {
            _lblChartSummary.SafeInvoke(() =>
                _lblChartSummary.Text =
                    $"Total: {_vm.TotalEvents:N0}  Peak: {_vm.PeakEvents:N0}  " +
                    $"Last updated: {_vm.LastChartUpdated:yyyy-MM-dd HH:mm}");
        }
        catch { }
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null) return;
        try
        {
            if (!_noDataOverlay.IsDisposed)
                _noDataOverlay.SafeInvoke(() =>
                    _noDataOverlay.Visible = !_vm.IsLoading && !_vm.Entries.Any());
        }
        catch { }
    }

    #endregion

    // =========================================================================
    #region Filter helpers
    // =========================================================================

    private async Task PopulateActionTypesAsync(CancellationToken cancellationToken = default)
    {
        if (_cmbActionType == null) return;

        try
        {
            var actionTypes = await _vm.GetDistinctActionTypesAsync();
            var allActions = new List<string> { "All" };
            allActions.AddRange(actionTypes);

            if (_cmbActionType.InvokeRequired)
                _cmbActionType.Invoke(() => _cmbActionType.DataSource = allActions);
            else
                _cmbActionType.DataSource = allActions;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AUDIT_PANEL] Failed to populate action types");
            _cmbActionType.DataSource = new List<string> { "All", "CREATE", "UPDATE", "DELETE", "LOGIN", "LOGOUT" };
        }
    }

    private async Task PopulateUsersAsync(CancellationToken cancellationToken = default)
    {
        if (_cmbUser == null) return;

        try
        {
            var users = await _vm.GetDistinctUsersAsync();
            var allUsers = new List<string> { "All" };
            allUsers.AddRange(users);

            if (_cmbUser.InvokeRequired)
                _cmbUser.Invoke(() => _cmbUser.DataSource = allUsers);
            else
                _cmbUser.DataSource = allUsers;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AUDIT_PANEL] Failed to populate users");
            _cmbUser.DataSource = new List<string> { "All" };
        }
    }

    private void ApplyFilters()
    {
        try
        {
            _vm.StartDate = _dtpStartDate?.Value ?? DateTime.MinValue;
            _vm.EndDate = _dtpEndDate?.Value ?? DateTime.MaxValue;
            _vm.SelectedActionType = _cmbActionType?.SelectedItem?.ToString() == "All"
                ? null : _cmbActionType?.SelectedItem?.ToString();
            _vm.SelectedUser = _cmbUser?.SelectedItem?.ToString() == "All"
                ? null : _cmbUser?.SelectedItem?.ToString();

            _ = _vm.LoadEntriesAsync();
            _ = _vm.LoadChartDataAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AUDIT_PANEL] ApplyFilters failed");
        }
    }

    private void ToggleAutoRefresh()
    {
        try
        {
            if (_chkAutoRefresh?.Checked == true)
            {
                if (_autoRefreshTimer == null)
                {
                    _autoRefreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
                    _autoRefreshTimer.Tick += async (s, e) =>
                    {
                        await _vm.LoadEntriesAsync();
                        await _vm.LoadChartDataAsync();
                    };
                }
                _autoRefreshTimer.Start();
                Logger.LogDebug("[AUDIT_PANEL] Auto-refresh enabled (30s interval)");
            }
            else
            {
                _autoRefreshTimer?.Stop();
                Logger.LogDebug("[AUDIT_PANEL] Auto-refresh disabled");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AUDIT_PANEL] ToggleAutoRefresh failed");
        }
    }

    #endregion

    // =========================================================================
    #region ICompletablePanel
    // =========================================================================

    public override async Task LoadAsync(CancellationToken ct)
    {
        if (IsLoaded) return;
        try
        {
            IsBusy = true;
            if (_loader != null)
            {
                _loader.Visible = true;
                _loader.BringToFront();
            }
            await _vm.LoadEntriesAsync();
            await _vm.LoadChartDataAsync();
            Logger.LogDebug("[AUDIT_PANEL] Loaded successfully");
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("[AUDIT_PANEL] Load cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[AUDIT_PANEL] Load failed");
        }
        finally
        {
            if (_loader != null)
            {
                _loader.Visible = false;
            }

            IsBusy = false;
        }
    }

    /// <summary>Read-only panel — SaveAsync is a no-op.</summary>
    public override async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            await Task.CompletedTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Logger.LogError(ex, "[AUDIT_PANEL] Save failed"); }
        finally { IsBusy = false; }
    }

    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            var errors = new List<ValidationItem>();

            if (_vm.Entries.Count == 0)
                errors.Add(new ValidationItem("Data", "No audit entries available", ValidationSeverity.Warning));

            await Task.CompletedTask;
            return errors.Count == 0
                ? ValidationResult.Success
                : ValidationResult.Failed(errors.ToArray());
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("[AUDIT_PANEL] Validation cancelled");
            return ValidationResult.Failed(new ValidationItem("Cancelled", "Validation was cancelled", ValidationSeverity.Info));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[AUDIT_PANEL] Validation error");
            return ValidationResult.Failed(new ValidationItem("Validation", ex.Message, ValidationSeverity.Error));
        }
        finally { IsBusy = false; }
    }

    public override void FocusFirstError() => _auditGrid?.Focus();

    #endregion

    // =========================================================================
    #region CSV export
    // =========================================================================

    private async Task ExportToCsvAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                owner: this,
                operationKey: $"{nameof(AuditLogPanel)}.Csv",
                dialogTitle: "Export Audit Log to CSV",
                filter: "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                defaultExtension: "csv",
                defaultFileName: $"audit_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                exportAction: (filePath, ct) =>
                {
                    if (_vm.ExportToCsvCommand?.CanExecute(filePath) == true)
                        _vm.ExportToCsvCommand.Execute(filePath);
                    return Task.CompletedTask;
                },
                logger: Logger,
                cancellationToken: cancellationToken);

            if (result.IsSkipped)
            {
                MessageBox.Show(result.ErrorMessage ?? "An export is already in progress.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (result.IsCancelled) return;

            if (!result.IsSuccess)
            {
                MessageBox.Show(result.ErrorMessage ?? "Export failed.",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Logger.LogInformation("[AUDIT_PANEL] CSV export completed: {RowCount} entries exported",
                _vm.Entries.Count);

            MessageBox.Show(
                $"Successfully exported {_vm.Entries.Count} audit entries to:\n{result.FilePath}",
                "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}",
                "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    // =========================================================================
    #region Lifecycle
    // =========================================================================

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        BeginInvoke(() =>
        {
            ConfigureMainSplitContainer();
            Logger.LogDebug("[{Panel}] Splitter configured after layout", GetType().Name);
        });
    }

    #endregion

    // =========================================================================
    #region Dispose
    // =========================================================================

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_viewModelPropertyChangedHandler != null)
                _vm.PropertyChanged -= _viewModelPropertyChangedHandler;
            if (_entriesCollectionChangedHandler != null)
                _vm.Entries.CollectionChanged -= _entriesCollectionChangedHandler;
            if (_chartDataCollectionChangedHandler != null)
                _vm.ChartData.CollectionChanged -= _chartDataCollectionChangedHandler;

            if (_btnRefresh != null && _btnRefreshClickHandler != null)
                _btnRefresh.Click -= _btnRefreshClickHandler;
            if (_btnExportCsv != null && _btnExportCsvClickHandler != null)
                _btnExportCsv.Click -= _btnExportCsvClickHandler;
            if (_btnUpdateChart != null && _btnUpdateChartClickHandler != null)
                _btnUpdateChart.Click -= _btnUpdateChartClickHandler;
            if (_chkAutoRefresh != null && _chkAutoRefreshCheckedChangedHandler != null)
                _chkAutoRefresh.CheckedChanged -= _chkAutoRefreshCheckedChangedHandler;
            if (_dtpStartDate != null && _dtpStartDateValueChangedHandler != null)
                _dtpStartDate.ValueChanged -= _dtpStartDateValueChangedHandler;
            if (_dtpEndDate != null && _dtpEndDateValueChangedHandler != null)
                _dtpEndDate.ValueChanged -= _dtpEndDateValueChangedHandler;
            if (_cmbActionType != null && _cmbActionTypeSelectedIndexChangedHandler != null)
                _cmbActionType.SelectedIndexChanged -= _cmbActionTypeSelectedIndexChangedHandler;
            if (_cmbUser != null && _cmbUserSelectedIndexChangedHandler != null)
                _cmbUser.SelectedIndexChanged -= _cmbUserSelectedIndexChangedHandler;
            if (_cmbChartGroupBy != null && _cmbChartGroupBySelectedIndexChangedHandler != null)
                _cmbChartGroupBy.SelectedIndexChanged -= _cmbChartGroupBySelectedIndexChangedHandler;

            try
            {
                if (_autoRefreshTimer != null)
                {
                    _autoRefreshTimer.Stop();
                    _autoRefreshTimer.Dispose();
                    _autoRefreshTimer = null;
                }
            }
            catch { }

            try { _bindingSource?.Dispose(); } catch { }

            try { _auditGrid?.SafeClearDataSource(); } catch { }
            try { _auditGrid?.SafeDispose(); } catch { }
            try { _chartControl?.Dispose(); } catch { }
            try { _chartHostPanel?.Dispose(); } catch { }
            try { _mainSplit?.Dispose(); } catch { }
            try { _lblChartSummary?.Dispose(); } catch { }
            try { _chartLoadingOverlay?.Dispose(); } catch { }
            try { _loader?.Dispose(); } catch { }
            try { _noDataOverlay?.Dispose(); } catch { }
            try { _header?.Dispose(); } catch { }
            try { _filterPanel?.Dispose(); } catch { }
            try { _btnRefresh?.Dispose(); } catch { }
            try { _btnExportCsv?.Dispose(); } catch { }
            try { _btnUpdateChart?.Dispose(); } catch { }
            try { _chkAutoRefresh?.Dispose(); } catch { }
            try { _dtpStartDate?.Dispose(); } catch { }
            try { _dtpEndDate?.Dispose(); } catch { }
            try { _cmbActionType?.SafeClearDataSource(); _cmbActionType?.SafeDispose(); } catch { }
            try { _cmbUser?.SafeClearDataSource(); _cmbUser?.SafeDispose(); } catch { }
            try { _cmbChartGroupBy?.SafeClearDataSource(); _cmbChartGroupBy?.SafeDispose(); } catch { }
        }

        base.Dispose(disposing);
    }

    #endregion
}
