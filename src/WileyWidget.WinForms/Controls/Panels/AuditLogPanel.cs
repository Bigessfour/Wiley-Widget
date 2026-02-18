using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Syncfusion.Drawing;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;



using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;


using Syncfusion.WinForms.ListView;

using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Input.Enums;

using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Extensions;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
using WileyWidget.WinForms.Controls.Supporting;








using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Services;
// using WileyWidget.WinForms.Utils; // Consolidated
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Audit Log viewer panel displaying audit entries with filtering and export capabilities.
/// Inherits from ScopedPanelBase to ensure proper DI lifetime management for scoped dependencies.
/// </summary>
public partial class AuditLogPanel : ScopedPanelBase<AuditLogViewModel>
{
    // UI Controls
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private LoadingOverlay? _chartLoadingOverlay;
    private LegacyGradientPanel? _filterPanel;

    // Main content
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _auditGrid;
    private SplitContainer? _mainSplit;
    private Panel? _chartHostPanel;
    private ChartControl? _chartControl;

    // Toolbar & controls
    private Syncfusion.WinForms.Controls.SfButton? _btnRefresh;
    private Syncfusion.WinForms.Controls.SfButton? _btnExportCsv;
    private Syncfusion.WinForms.Controls.SfButton? _btnUpdateChart;
    private CheckBoxAdv? _chkAutoRefresh;

    // Filter controls
    private SfDateTimeEdit? _dtpStartDate;
    private SfDateTimeEdit? _dtpEndDate;
    private SfComboBox? _cmbActionType;
    private SfComboBox? _cmbUser;
    private SfComboBox? _cmbChartGroupBy;
    private Label? _lblChartSummary;

    // Data binding source for grid
    private BindingSource? _bindingSource;

    // Auto-refresh timer
    private System.Windows.Forms.Timer? _autoRefreshTimer;

    // Event handlers for cleanup
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _entriesCollectionChangedHandler;
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _chartDataCollectionChangedHandler;
    private EventHandler? _panelHeaderRefreshHandler;

    // Event handler fields for proper cleanup
    private EventHandler? _panelHeaderCloseHandler;
    private EventHandler? _btnRefreshClickHandler;
    private EventHandler? _btnExportCsvClickHandler;
    private EventHandler? _btnUpdateChartClickHandler;
    private EventHandler? _chkAutoRefreshCheckedChangedHandler;
    private Syncfusion.WinForms.Input.Events.DateTimeValueChangedEventHandler? _dtpStartDateValueChangedHandler;
    private Syncfusion.WinForms.Input.Events.DateTimeValueChangedEventHandler? _dtpEndDateValueChangedHandler;
    private EventHandler? _cmbActionTypeSelectedIndexChangedHandler;
    private EventHandler? _cmbUserSelectedIndexChangedHandler;
    private EventHandler? _cmbChartGroupBySelectedIndexChangedHandler;
    private EventHandler<string>? _themeChangedHandler;

    /// <summary>
    /// Initializes a new instance with required DI dependencies.
    /// </summary>
    public AuditLogPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<AuditLogViewModel>> logger)
        : base(scopeFactory, logger)
    {
        // InitializeComponent(); // replaced by BuildProgrammaticLayout
        BuildProgrammaticLayout();

        // Apply theme via SfSkinManager (single source of truth)
        try { var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme; Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, theme); } catch { }
        SetupRuntime();
        SubscribeToThemeChanges();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        Name = "AuditLogPanel";
        // Set preferred size for proper docking display (matches PreferredDockSize extension)
        Size = new Size(520, 380);
        MinimumSize = new Size(420, 360);
        AutoScroll = true;
        Padding = new Padding(8);
        Dock = DockStyle.Fill;
        AccessibleName = "Audit Log Panel";
        AccessibleDescription = "Displays audit log entries with filtering and export capabilities";

        try
        {
            AutoScaleMode = AutoScaleMode.Dpi;
        }
        catch
        {
            // Fall back if DPI scaling not supported
        }

        // InitializeComponent moved to AuditLogPanel.Designer.cs for designer support
        this.ResumeLayout(false);

    }

    private void SetupUI()
    {
        SuspendLayout();

        // Panel header with title and actions
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Audit Log",
            AccessibleName = "Audit Log header"
        };
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Filter panel - theme applied via SfSkinManager cascade (no manual BackgroundColor)
        _filterPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(8),
            BorderStyle = BorderStyle.None,
            AccessibleName = "Audit log filters"
        };
        var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(_filterPanel, theme);

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            AutoSize = true
        }; // Standard WinForms (no Syncfusion TableLayoutPanel in v32.1.19)
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Start Date label
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // Start Date picker
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // End Date label
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // End Date picker
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Action Type label
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Action Type combo

        // Row 1: Date filters
        filterTable.Controls.Add(new Label { Text = "Start Date:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "Start date label" }, 0, 0);
        _dtpStartDate = new SfDateTimeEdit
        {
            Width = 140,
            DateTimePattern = DateTimePattern.ShortDate,
            AccessibleName = "Start date filter",
            AccessibleDescription = "Filter audit entries from this date"
        };
        _dtpStartDateValueChangedHandler = async (s, e) => ApplyFilters();
        _dtpStartDate.ValueChanged += _dtpStartDateValueChangedHandler;
        filterTable.Controls.Add(_dtpStartDate, 1, 0);

        filterTable.Controls.Add(new Label { Text = "End Date:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "End date label" }, 2, 0);
        _dtpEndDate = new SfDateTimeEdit
        {
            Width = 140,
            DateTimePattern = DateTimePattern.ShortDate,
            AccessibleName = "End date filter",
            AccessibleDescription = "Filter audit entries until this date"
        };
        _dtpEndDateValueChangedHandler = async (s, e) => ApplyFilters();
        _dtpEndDate.ValueChanged += _dtpEndDateValueChangedHandler;
        filterTable.Controls.Add(_dtpEndDate, 3, 0);

        // Row 2: Action Type and User filters
        filterTable.Controls.Add(new Label { Text = "Action Type:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "Action type label" }, 0, 1);
        _cmbActionType = new SfComboBox
        {
            Width = 140,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AllowDropDownResize = false,
            MaxDropDownItems = 10,
            AllowNull = true,
            Watermark = "All Actions",
            AccessibleName = "Action type filter",
            AccessibleDescription = "Filter audit entries by action type"
        };
        _cmbActionTypeSelectedIndexChangedHandler = async (s, e) => ApplyFilters();
        _cmbActionType.SelectedIndexChanged += _cmbActionTypeSelectedIndexChangedHandler;
        filterTable.Controls.Add(_cmbActionType, 1, 1);

        filterTable.Controls.Add(new Label { Text = "User:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "User label" }, 2, 1);
        _cmbUser = new SfComboBox
        {
            Width = 140,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AllowDropDownResize = false,
            MaxDropDownItems = 10,
            AllowNull = true,
            Watermark = "All Users",
            AccessibleName = "User filter",
            AccessibleDescription = "Filter audit entries by user"
        };
        _cmbUserSelectedIndexChangedHandler = async (s, e) => ApplyFilters();
        _cmbUser.SelectedIndexChanged += _cmbUserSelectedIndexChangedHandler;
        filterTable.Controls.Add(_cmbUser, 3, 1);

        // Buttons
        _btnRefresh = new SfButton
        {
            Text = "&Refresh",
            Width = 100,
            Height = 32,
            AccessibleName = "Refresh audit log",
            AccessibleDescription = "Reload audit entries from database"
        };
        _btnRefreshClickHandler = async (s, e) => await RefreshDataAsync();
        _btnRefresh.Click += _btnRefreshClickHandler;
        filterTable.Controls.Add(_btnRefresh, 4, 0);

        _chkAutoRefresh = new CheckBoxAdv
        {
            Text = "Auto-refresh",
            AutoSize = true,
            AccessibleName = "Auto-refresh toggle",
            AccessibleDescription = "Automatically refresh audit log every 30 seconds"
        };
        _chkAutoRefreshCheckedChangedHandler = (s, e) => ToggleAutoRefresh();
        _chkAutoRefresh.CheckedChanged += _chkAutoRefreshCheckedChangedHandler;
        filterTable.Controls.Add(_chkAutoRefresh, 4, 1);

        _btnExportCsv = new SfButton
        {
            Text = "&Export CSV",
            Width = 100,
            Height = 32,
            AccessibleName = "Export to CSV",
            AccessibleDescription = "Export filtered audit entries to CSV file"
        };
        _btnExportCsvClickHandler = async (s, e) => await ExportToCsvAsync();
        _btnExportCsv.Click += _btnExportCsvClickHandler;
        filterTable.Controls.Add(_btnExportCsv, 5, 0);

        _filterPanel.Controls.Add(filterTable);

        // Chart options toolbar - standard WinForms (no Syncfusion FlowLayoutPanel in v32.1.19)
        var chartOptionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            AccessibleName = "Chart options flow"
        };

        var lblChartGroup = new Label
        {
            Text = "Chart Period:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 8, 8, 8),
            AccessibleName = "Chart grouping label"
        };

        _cmbChartGroupBy = new SfComboBox
        {
            Width = 120,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AllowNull = false,
            Watermark = "Group",
            AccessibleName = "Chart grouping",
            AccessibleDescription = "Select chart grouping period (Day, Week, Month)"
        };
        _cmbChartGroupBy.DataSource = Enum.GetNames(typeof(AuditLogViewModel.ChartGroupingPeriod));
        _cmbChartGroupBySelectedIndexChangedHandler = async (s, e) => await ViewModel!.LoadChartDataAsync();
        _cmbChartGroupBy.SelectedIndexChanged += _cmbChartGroupBySelectedIndexChangedHandler;

        _btnUpdateChart = new Syncfusion.WinForms.Controls.SfButton
        {
            Text = "Update Chart",
            Width = 100,
            Height = 28,
            AccessibleName = "Update chart",
            AccessibleDescription = "Refresh chart with current filters"
        };
        _btnUpdateChartClickHandler = async (s, e) => await ViewModel!.LoadChartDataAsync();
        _btnUpdateChart.Click += _btnUpdateChartClickHandler;

        chartOptionsFlow.Controls.Add(lblChartGroup);
        chartOptionsFlow.Controls.Add(_cmbChartGroupBy);
        chartOptionsFlow.Controls.Add(_btnUpdateChart);

        _filterPanel.Controls.Add(chartOptionsFlow);
        Controls.Add(_filterPanel);

        // Main split container: left grid, right chart/details - standard WinForms
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            AccessibleName = "Audit grid and chart split container"
        }; // Standard WinForms (no Syncfusion SplitContainer in v32.1.19)
        ConfigureMainSplitContainer();

        // Audit grid (left)
        _auditGrid = new SfDataGrid
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
            AccessibleName = "Audit log entries grid",
            AccessibleDescription = "Grid displaying audit log entries with timestamp, user, action, and details"
        }.PreventStringRelationalFilters(_logger, "User", "Action", "EntityType", "EntityId", "Changes");

        ConfigureGridColumns();
        _mainSplit.Panel1.Controls.Add(_auditGrid);

        // Chart host (right) - standard WinForms Panel
        _chartHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            AccessibleName = "Chart host panel",
            AccessibleDescription = "Displays chart of audit events"
        }; // Standard WinForms (no Syncfusion Panel equivalent in v32.1.19)

        _chartControl = new ChartControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Audit events chart",
            AccessibleDescription = "Chart showing counts of audit events over time"
        };

        ConfigureChartForAudit();

        _chartHostPanel.Controls.Add(_chartControl);

        _chartLoadingOverlay = new LoadingOverlay
        {
            Message = "Loading chart data...",
            AccessibleName = "Chart loading overlay"
        };
        _chartHostPanel.Controls.Add(_chartLoadingOverlay);

        _lblChartSummary = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular),
            Padding = new Padding(6, 0, 0, 0),
            AccessibleName = "Chart summary",
            AccessibleDescription = "Summary statistics for chart data"
        };
        _chartHostPanel.Controls.Add(_lblChartSummary);

        _mainSplit.Panel2.Controls.Add(_chartHostPanel);

        Controls.Add(_mainSplit);

        // Loading and no-data overlays
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading audit entries...",
            Dock = DockStyle.Fill,
            AccessibleName = "Loading overlay"
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No audit entries found",
            Dock = DockStyle.Fill,
            AccessibleName = "No data overlay"
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ResumeLayout(false);
        this.PerformLayout();
        this.Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    private void SetupRuntime()
    {
        try
        {
            if (_panelHeader != null)
            {
                _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
                _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
                _panelHeaderCloseHandler = (s, e) => ClosePanel();
                _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            }

            if (_dtpStartDate != null)
            {
                _dtpStartDateValueChangedHandler = async (s, e) => ApplyFilters();
                _dtpStartDate.ValueChanged += _dtpStartDateValueChangedHandler;
            }

            if (_dtpEndDate != null)
            {
                _dtpEndDateValueChangedHandler = async (s, e) => ApplyFilters();
                _dtpEndDate.ValueChanged += _dtpEndDateValueChangedHandler;
            }

            if (_cmbActionType != null)
            {
                _cmbActionTypeSelectedIndexChangedHandler = (s, e) => ApplyFilters();
                _cmbActionType.SelectedIndexChanged += _cmbActionTypeSelectedIndexChangedHandler;
            }

            if (_cmbUser != null)
            {
                _cmbUserSelectedIndexChangedHandler = (s, e) => ApplyFilters();
                _cmbUser.SelectedIndexChanged += _cmbUserSelectedIndexChangedHandler;
            }

            if (_btnRefresh != null)
            {
                _btnRefreshClickHandler = async (s, e) => await RefreshDataAsync();
                _btnRefresh.Click += _btnRefreshClickHandler;
            }

            if (_btnExportCsv != null)
            {
                _btnExportCsvClickHandler = async (s, e) => await ExportToCsvAsync();
                _btnExportCsv.Click += _btnExportCsvClickHandler;
            }

            if (_btnUpdateChart != null)
            {
                _btnUpdateChartClickHandler = async (s, e) => await ViewModel!.LoadChartDataAsync();
                _btnUpdateChart.Click += _btnUpdateChartClickHandler;
            }

            if (_cmbChartGroupBy != null)
            {
                _cmbChartGroupBy.DataSource = Enum.GetNames(typeof(AuditLogViewModel.ChartGroupingPeriod));
                _cmbChartGroupBySelectedIndexChangedHandler = async (s, e) => await ViewModel!.LoadChartDataAsync();
                _cmbChartGroupBy.SelectedIndexChanged += _cmbChartGroupBySelectedIndexChangedHandler;
            }

            ConfigureMainSplitContainer();
            ConfigureGridColumns();
            ConfigureChartForAudit();

            _bindingSource ??= new BindingSource();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to setup runtime UI handlers");
        }
    }

    private void ConfigureMainSplitContainer()
    {
        if (_mainSplit == null) return;

        const int panelMinSize = 300;
        const int defaultDistance = 520; // 65% of minimum 800px width

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

    /// <summary>
    /// Configures ChartControl for audit events display per Syncfusion API.
    /// </summary>
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
            _chartControl.PrimaryXAxis.Font = new System.Drawing.Font("Segoe UI", 9F);
            _chartControl.PrimaryXAxis.LabelRotate = true;
            _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
            _chartControl.PrimaryXAxis.DrawGrid = true;

            _chartControl.PrimaryYAxis.Title = "Events";
            _chartControl.PrimaryYAxis.Font = new System.Drawing.Font("Segoe UI", 9F);

            _chartControl.ShowToolTips = true;
            _chartControl.ShowLegend = false;

            try
            {
                var chartType = _chartControl.GetType();
                var propZoom = chartType.GetProperty("EnableZooming");
                if (propZoom != null && propZoom.CanWrite)
                {
                    propZoom.SetValue(_chartControl, true);
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: ConfigureChartForAudit failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Called after ViewModel is resolved from scoped service provider.
    /// Binds ViewModel data and initiates data load.
    /// </summary>
    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is not AuditLogViewModel typedViewModel)
        {
            return;
        }

        // Subscribe to ViewModel property changes
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        typedViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Subscribe to Entries collection changes
        _entriesCollectionChangedHandler = (s, e) => UpdateGridData();
        typedViewModel.Entries.CollectionChanged += _entriesCollectionChangedHandler;

        // Initialize filters
        InitializeFilters();

        // Initial UI update
        UpdateUI();

        // Subscribe to ChartData collection changes
        _chartDataCollectionChangedHandler = (s, e) => UpdateChart();
        typedViewModel.ChartData.CollectionChanged += _chartDataCollectionChangedHandler;

        // Initialize chart grouping selection
        if (_cmbChartGroupBy != null)
        {
            try { _cmbChartGroupBy.SelectedItem = typedViewModel.ChartGrouping.ToString(); } catch { }
        }

        // Note: Data loading is now handled by ILazyLoadViewModel via DockingManager events
    }

    private void InitializeFilters()
    {
        if (ViewModel == null) return;

        // Set default date range (last 30 days)
        var endDate = DateTime.Now;
        var startDate = endDate.AddDays(-30);

        _dtpStartDate!.Value = startDate;
        _dtpEndDate!.Value = endDate;

        ViewModel.StartDate = startDate;
        ViewModel.EndDate = endDate;

        // Populate action types dynamically from ViewModel
        _ = PopulateActionTypesAsync();

        // Populate users dynamically from ViewModel
        _ = PopulateUsersAsync();

        // Default chart grouping to Month for readability
        try { if (_cmbChartGroupBy != null) _cmbChartGroupBy.SelectedItem = AuditLogViewModel.ChartGroupingPeriod.Month.ToString(); } catch { }
    }

    /// <summary>
    /// Populates action type filter from ViewModel data.
    /// </summary>
    private async Task PopulateActionTypesAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null || _cmbActionType == null) return;

        try
        {
            var actionTypes = await ViewModel.GetDistinctActionTypesAsync();
            var allActions = new List<string> { "All" };
            allActions.AddRange(actionTypes);

            if (_cmbActionType.InvokeRequired)
            {
                _cmbActionType.Invoke(() => _cmbActionType.DataSource = allActions);
            }
            else
            {
                _cmbActionType.DataSource = allActions;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: Failed to populate action types: {ex.Message}");
            // Fallback to common actions
            _cmbActionType.DataSource = new List<string> { "All", "CREATE", "UPDATE", "DELETE", "LOGIN", "LOGOUT" };
        }
    }

    /// <summary>
    /// Populates user filter from ViewModel data.
    /// </summary>
    private async Task PopulateUsersAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null || _cmbUser == null) return;

        try
        {
            var users = await ViewModel.GetDistinctUsersAsync();
            var allUsers = new List<string> { "All" };
            allUsers.AddRange(users);

            if (_cmbUser.InvokeRequired)
            {
                _cmbUser.Invoke(() => _cmbUser.DataSource = allUsers);
            }
            else
            {
                _cmbUser.DataSource = allUsers;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: Failed to populate users: {ex.Message}");
            // Fallback to placeholder
            _cmbUser.DataSource = new List<string> { "All" };
        }
    }

    private async Task LoadDataSafeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadEntriesAsync();
                await ViewModel.LoadChartDataAsync();
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
            $"Failed to load audit entries: {ex.Message}",
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

                case nameof(ViewModel.IsChartLoading):
                    if (_chartLoadingOverlay != null)
                        _chartLoadingOverlay.Visible = ViewModel.IsChartLoading;
                    break;

                case nameof(ViewModel.Entries):
                    UpdateGridData();
                    UpdateNoDataOverlay();
                    break;

                case nameof(ViewModel.TotalEvents):
                case nameof(ViewModel.PeakEvents):
                case nameof(ViewModel.LastChartUpdated):
                    UpdateChartSummary();
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
            UpdateGridData();
            UpdateChart();
            UpdateChartSummary();
            UpdateNoDataOverlay();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed
        }
    }

    private void UpdateGridData()
    {
        if (_auditGrid == null || ViewModel == null) return;

        try
        {
            _auditGrid.SuspendLayout();

            // Use BindingSource for improved filtering/sorting support
            if (_bindingSource == null)
            {
                _bindingSource = new BindingSource();
            }

            // Create snapshot to avoid collection modification issues
            var snapshot = ViewModel.Entries.ToList();
            _bindingSource.DataSource = snapshot;
            _auditGrid.DataSource = _bindingSource;

            _auditGrid.ResumeLayout();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: UpdateGridData failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates chart series based on ViewModel.ChartData.
    /// </summary>
    private void UpdateChart()
    {
        if (_chartControl == null || ViewModel == null) return;

        if (_chartControl.InvokeRequired)
        {
            _chartControl.SafeInvoke(() => UpdateChart());
            return;
        }

        try
        {
            _chartControl.Series.Clear();

            if (!ViewModel.ChartData.Any())
            {
                _chartControl.Refresh();
                return;
            }

            var colSeries = new ChartSeries("Events", ChartSeriesType.Column);
            colSeries.Style.Border.Width = 1;

            foreach (var p in ViewModel.ChartData)
            {
                colSeries.Points.Add(p.Period, (double)p.Count);
            }

            colSeries.PointsToolTipFormat = "{1:N0}";
            _chartControl.Series.Add(colSeries);

            // Adjust X-axis DateTime format based on grouping
            try
            {
                var xAxis = _chartControl.PrimaryXAxis;
                var propDateFormat = xAxis.GetType().GetProperty("DateTimeFormat");
                if (propDateFormat != null && propDateFormat.CanWrite)
                {
                    var fmt = "yyyy-MM-dd";
                    if (ViewModel.ChartGrouping == AuditLogViewModel.ChartGroupingPeriod.Month) fmt = "MMM yyyy";
                    else if (ViewModel.ChartGrouping == AuditLogViewModel.ChartGroupingPeriod.Day) fmt = "MMM dd";
                    propDateFormat.SetValue(xAxis, fmt);
                }
            }
            catch { }

            _chartControl.Refresh();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: UpdateChart failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates chart summary information shown under the chart.
    /// </summary>
    private void UpdateChartSummary()
    {
        if (_lblChartSummary == null || ViewModel == null) return;
        try
        {
            _lblChartSummary.SafeInvoke(() =>
            {
                _lblChartSummary.Text = $"Total: {ViewModel.TotalEvents:N0}  Peak: {ViewModel.PeakEvents:N0}  Last updated: {ViewModel.LastChartUpdated:yyyy-MM-dd HH:mm}";
            });
        }
        catch { }
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        try
        {
            if (!_noDataOverlay.IsDisposed)
            {
                _noDataOverlay.SafeInvoke(() => _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.Entries.Any());
            }
        }
        catch
        {
            // Ignore
        }
    }

    #region ICompletablePanel Implementation

    /// <summary>
    /// Loads the panel and initializes audit log data asynchronously.
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (IsLoaded) return;
        try
        {
            IsBusy = true;
            await RefreshDataAsync(ct);
            _logger?.LogDebug("AuditLogPanel loaded successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("AuditLogPanel load cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load AuditLogPanel");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Saves panel state. AuditLogPanel is read-only, so this is a no-op.
    /// </summary>
    public override async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            await Task.CompletedTask;
            _logger?.LogDebug("AuditLogPanel save completed");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("AuditLogPanel save cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save AuditLogPanel");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Validates panel data. Ensures ViewModel is initialized and entries are accessible.
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
            }
            else
            {
                // Validate that grid has data binding
                if (ViewModel.Entries.Count == 0)
                {
                    errors.Add(new ValidationItem("Data", "No audit entries available", ValidationSeverity.Warning));
                }
            }

            await Task.CompletedTask;
            return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failed(errors.ToArray());
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("AuditLogPanel validation cancelled");
            return ValidationResult.Failed(new ValidationItem("Cancelled", "Validation was cancelled", ValidationSeverity.Info));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Validation error in AuditLogPanel");
            return ValidationResult.Failed(new ValidationItem("Validation", ex.Message, ValidationSeverity.Error));
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Focuses the first control on the panel.
    /// </summary>
    public override void FocusFirstError()
    {
        _auditGrid?.Focus();
    }

    #endregion

    private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (ViewModel != null)
            {
                await ViewModel.LoadEntriesAsync();
                await ViewModel.LoadChartDataAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to refresh audit entries: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ApplyFilters()
    {
        if (ViewModel == null) return;

        try
        {
            ViewModel.StartDate = _dtpStartDate?.Value ?? DateTime.MinValue;
            ViewModel.EndDate = _dtpEndDate?.Value ?? DateTime.MaxValue;
            ViewModel.SelectedActionType = _cmbActionType?.SelectedItem?.ToString() == "All" ? null : _cmbActionType?.SelectedItem?.ToString();
            ViewModel.SelectedUser = _cmbUser?.SelectedItem?.ToString() == "All" ? null : _cmbUser?.SelectedItem?.ToString();

            // Trigger reload with filters
            _ = ViewModel.LoadEntriesAsync();
            _ = ViewModel.LoadChartDataAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: ApplyFilters failed: {ex.Message}");
        }
    }

    private void ToggleAutoRefresh()
    {
        try
        {
            if (_chkAutoRefresh?.Checked == true)
            {
                // Start timer - refresh every 30 seconds
                if (_autoRefreshTimer == null)
                {
                    _autoRefreshTimer = new System.Windows.Forms.Timer
                    {
                        Interval = 30000 // 30 seconds
                    };
                    _autoRefreshTimer.Tick += async (s, e) => await RefreshDataAsync();
                }

                _autoRefreshTimer.Start();
                Console.WriteLine("AuditLogPanel: Auto-refresh enabled (30s interval)");
            }
            else
            {
                // Stop timer
                if (_autoRefreshTimer != null)
                {
                    _autoRefreshTimer.Stop();
                    Console.WriteLine("AuditLogPanel: Auto-refresh disabled");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AuditLogPanel: ToggleAutoRefresh failed: {ex.Message}");
        }
    }

    private async Task ExportToCsvAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Files|*.csv|All Files|*.*",
                DefaultExt = "csv",
                FileName = $"audit_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Export Audit Log to CSV"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            // Use ViewModel's export command
            if (ViewModel?.ExportToCsvCommand?.CanExecute(sfd.FileName) == true)
            {
                ViewModel.ExportToCsvCommand.Execute(sfd.FileName);
                MessageBox.Show(
                    $"Successfully exported {ViewModel.Entries.Count} audit entries to:\n{sfd.FileName}",
                    "Export Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Export failed: {ex.Message}",
                "Export Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SubscribeToThemeChanges()
    {
        // Legacy theme subscription removed - SfSkinManager handles themes automatically
        _themeChangedHandler = null;
    }

    private void UnsubscribeFromThemeChanges()
    {
        // REMOVED: ThemeManager.ThemeChanged unsubscription - deprecated service
        // Theme changes are now handled via SfSkinManager.ApplicationVisualTheme
        if (_themeChangedHandler != null)
        {
            // Legacy cleanup - no-op since ThemeManager is deprecated
            _themeChangedHandler = null;
        }
    }

    private void ApplyTheme()
    {
        try
        {
            // Theme is applied automatically by SFSkinManager cascade from parent form
            // No manual color assignments needed
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
            // Unsubscribe all event handlers
            if (_panelHeader != null && _panelHeaderCloseHandler != null)
            {
                _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                _panelHeaderCloseHandler = null;
            }
            if (_panelHeader != null && _panelHeaderRefreshHandler != null)
            {
                _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                _panelHeaderRefreshHandler = null;
            }

            if (_btnRefresh != null && _btnRefreshClickHandler != null) _btnRefresh.Click -= _btnRefreshClickHandler;
            if (_btnExportCsv != null && _btnExportCsvClickHandler != null) _btnExportCsv.Click -= _btnExportCsvClickHandler;
            if (_btnUpdateChart != null && _btnUpdateChartClickHandler != null) _btnUpdateChart.Click -= _btnUpdateChartClickHandler;
            if (_chkAutoRefresh != null && _chkAutoRefreshCheckedChangedHandler != null) _chkAutoRefresh.CheckedChanged -= _chkAutoRefreshCheckedChangedHandler;
            if (_dtpStartDate != null && _dtpStartDateValueChangedHandler != null) _dtpStartDate.ValueChanged -= _dtpStartDateValueChangedHandler;
            if (_dtpEndDate != null && _dtpEndDateValueChangedHandler != null) _dtpEndDate.ValueChanged -= _dtpEndDateValueChangedHandler;
            if (_cmbActionType != null && _cmbActionTypeSelectedIndexChangedHandler != null) _cmbActionType.SelectedIndexChanged -= _cmbActionTypeSelectedIndexChangedHandler;
            if (_cmbUser != null && _cmbUserSelectedIndexChangedHandler != null) _cmbUser.SelectedIndexChanged -= _cmbUserSelectedIndexChangedHandler;
            if (_cmbChartGroupBy != null && _cmbChartGroupBySelectedIndexChangedHandler != null) _cmbChartGroupBy.SelectedIndexChanged -= _cmbChartGroupBySelectedIndexChangedHandler;

            // Stop and dispose auto-refresh timer
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

            // Dispose BindingSource
            try { _bindingSource?.Dispose(); } catch { }

            // Dispose controls using SafeDispose pattern
            try { _auditGrid?.SafeClearDataSource(); } catch { }
            try { _auditGrid?.SafeDispose(); } catch { }
            try { _chartControl?.Dispose(); } catch { }
            try { _chartHostPanel?.Dispose(); } catch { }
            try { _mainSplit?.Dispose(); } catch { }
            try { _lblChartSummary?.Dispose(); } catch { }
            try { _chartLoadingOverlay?.Dispose(); } catch { }
            try { _panelHeader?.Dispose(); } catch { }
            try { _loadingOverlay?.Dispose(); } catch { }
            try { _noDataOverlay?.Dispose(); } catch { }
            try { _filterPanel?.Dispose(); } catch { }
            try { _btnRefresh?.Dispose(); } catch { }
            try { _btnExportCsv?.Dispose(); } catch { }
            try { _btnUpdateChart?.Dispose(); } catch { }
            try { _chkAutoRefresh?.Dispose(); } catch { }
            try { _dtpStartDate?.Dispose(); } catch { }
            try { _dtpEndDate?.Dispose(); } catch { }
            try { _cmbActionType?.SafeClearDataSource(); } catch { }
            try { _cmbActionType?.SafeDispose(); } catch { }
            try { _cmbUser?.SafeClearDataSource(); } catch { }
            try { _cmbUser?.SafeDispose(); } catch { }
            try { _cmbChartGroupBy?.SafeClearDataSource(); } catch { }
            try { _cmbChartGroupBy?.SafeDispose(); } catch { }
        }

        base.Dispose(disposing);
    }
}
