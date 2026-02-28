using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Input;
using System.ComponentModel;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.Controls.Supporting;
using Syncfusion.Windows.Forms;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using Syncfusion.WinForms.DataGrid.Events;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using Syncfusion.Drawing;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.Models;
using Syncfusion.WinForms.ListView;




using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Themes;
using WileyWidget.Services;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Panel for managing utility bills with customer billing, payment tracking, and reporting.
/// Features bill creation, status management, customer selection, financial summaries, and filtering.
/// Implements MVVM pattern with full CRUD operations, theme integration, and accessibility.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class UtilityBillPanel : ScopedPanelBase<UtilityBillViewModel>
{
    #region Fields

    private SfDataGrid? _billsGrid;
    private SfDataGrid? _customersGrid;
    private SfButton? _createBillButton;
    private SfButton? _saveBillButton;
    private SfButton? _deleteBillButton;
    private SfButton? _markPaidButton;
    private SfButton? _generateReportButton;
    private SfButton? _refreshButton;
    private SfButton? _exportExcelButton;
    private TextBoxExt? _searchTextBox;
    private SfComboBox? _statusFilterComboBox;
    private CheckBoxAdv? _overdueOnlyCheckBox;
    private Label? _totalOutstandingLabel;
    private Label? _overdueCountLabel;
    private Label? _totalRevenueLabel;
    private Label? _billsThisMonthLabel;
    private Panel? _summaryPanel;
    private Panel? _gridPanel;
    private Panel? _buttonPanel;
    private SplitContainerAdv? _mainSplitContainer;
    private TableLayoutPanel? _content;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private ToolTip? _toolTip;
    private Panel? topPanel;
    private Panel? bottomPanel;
    private readonly SyncfusionControlFactory? _factory;

    private PropertyChangedEventHandler? ViewModelPropertyChangedHandler;
    private NotifyCollectionChangedEventHandler? _billsCollectionChangedHandler;
    private NotifyCollectionChangedEventHandler? _customersCollectionChangedHandler;

    // Event handler delegates for proper cleanup
    private EventHandler? _createBillButtonClickHandler;
    private EventHandler? _saveBillButtonClickHandler;
    private EventHandler? _deleteBillButtonClickHandler;
    private EventHandler? _markPaidButtonClickHandler;
    private EventHandler? _generateReportButtonClickHandler;
    private EventHandler? _exportExcelButtonClickHandler;
    private EventHandler? _refreshButtonClickHandler;
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;

    private ErrorProvider? _errorProvider;
    private System.Threading.SynchronizationContext? _uiSyncContext;

    #endregion

    #region Constructor

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public UtilityBillPanel(UtilityBillViewModel viewModel, SyncfusionControlFactory controlFactory)
        : base(viewModel)
    {
        _factory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
    }

    private static ILogger ResolveLogger()
    {
        return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<UtilityBillPanel>>(Program.Services)
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UtilityBillPanel>.Instance;
    }

    private SyncfusionControlFactory Factory => _factory ?? ControlFactory;

    private IServiceProvider? ResolveServiceProvider()
    {
        return _scope?.ServiceProvider ?? Program.Services;
    }

    #endregion

    #region Initialization

    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is not UtilityBillViewModel)
        {
            return;
        }
        try
        {
            _uiSyncContext = System.Threading.SynchronizationContext.Current;

            SafeSuspendAndLayout(InitializeControls);
            BindViewModel();
            ApplyTheme(SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

            // Defer handle-dependent operations until the panel's window handle is created
            Load += (s, e) =>
            {
                SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _);
                LoadAsyncSafe();
            };

            Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing UtilityBillPanel");
            throw;
        }
    }

    private void InitializeControls()
    {
        SuspendLayout();

        // Set up panel properties (matches PreferredDockSize extension)
        Text = "Utility Bills";
        Size = new Size(1100, 760);
        MinimumSize = new Size(1024, 720);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = false;
        Padding = Padding.Empty;

        // Initialize tooltip
        _toolTip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 500,
            ReshowDelay = 100,
            ShowAlways = true
        };

        // Initialize error provider
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink
        };

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Utility Bill Management",
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f)
        };
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Main split container (bills top, customers bottom)
        _mainSplitContainer = Factory.CreateSplitContainerAdv(splitter =>
        {
            splitter.Dock = DockStyle.Fill;
            splitter.Orientation = Orientation.Horizontal;
            splitter.TabStop = false;
        });
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplitContainer, (int)DpiAware.LogicalToDeviceUnits(500f));

        _content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false,
            Name = "UtilityBillPanelContent"
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _content.Controls.Add(_mainSplitContainer, 0, 0);

        InitializeTopPanel();
        InitializeBottomPanel();

        Controls.Add(_content);

        // Status strip
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            Height = (int)DpiAware.LogicalToDeviceUnits(25f)
        };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay
        {
            Dock = DockStyle.Fill,
            Message = "Loading utility bill data...",
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        // No data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Dock = DockStyle.Fill,
            Message = "No utility bills yet\r\nCreate a new bill for a customer to get started",
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ResumeLayout(false);
        this.PerformLayout();
        this.Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    private void InitializeTopPanel()
    {
        topPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
        };

        // Summary panel with KPIs
        InitializeSummaryPanel();
        topPanel.Controls.Add(_summaryPanel!);

        // Button panel for actions
        InitializeButtonPanel();
        topPanel.Controls.Add(_buttonPanel!);

        // Bills grid
        InitializeBillsGrid();
        topPanel.Controls.Add(_gridPanel!);

        _mainSplitContainer!.Panel1.Controls.Add(topPanel);
    }

    private void InitializeSummaryPanel()
    {
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f),
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
        };

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };

        for (int i = 0; i < 4; i++)
            summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        _totalOutstandingLabel = CreateSummaryLabel("Total Outstanding: $0.00");
        _overdueCountLabel = CreateSummaryLabel("Overdue Bills: 0");
        _totalRevenueLabel = CreateSummaryLabel("Total Revenue: $0.00");
        _billsThisMonthLabel = CreateSummaryLabel("Bills This Month: 0");

        summaryTable.Controls.Add(_totalOutstandingLabel, 0, 0);
        summaryTable.Controls.Add(_overdueCountLabel, 1, 0);
        summaryTable.Controls.Add(_totalRevenueLabel, 2, 0);
        summaryTable.Controls.Add(_billsThisMonthLabel, 3, 0);

        _summaryPanel.Controls.Add(summaryTable);
    }

    private Label CreateSummaryLabel(string text)
    {
        return new Label
        {
            Text = text,
            // ForeColor removed - let SFSkinManager handle theming
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false
        };
    }

    private void InitializeButtonPanel()
    {
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
        };

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var buttonSize = new Size((int)DpiAware.LogicalToDeviceUnits(110f), (int)DpiAware.LogicalToDeviceUnits(32f));

        _createBillButton = CreateButton("&Create Bill", buttonSize, 1);
        _createBillButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel.CreateBillCommand);
        _createBillButton.Click += _createBillButtonClickHandler;
        _toolTip!.SetToolTip(_createBillButton, "Create a new utility bill for the selected customer (Ctrl+N)");

        _saveBillButton = CreateButton("&Save", buttonSize, 2);
        _saveBillButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel.SaveBillCommand);
        _saveBillButton.Click += _saveBillButtonClickHandler;
        _toolTip.SetToolTip(_saveBillButton, "Save changes to the selected bill (Ctrl+S)");

        _deleteBillButton = CreateButton("&Delete", buttonSize, 3);
        _deleteBillButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel.DeleteBillCommand);
        _deleteBillButton.Click += _deleteBillButtonClickHandler;
        _toolTip.SetToolTip(_deleteBillButton, "Delete the selected bill (Del)");

        _markPaidButton = CreateButton("Mark &Paid", buttonSize, 4);
        _markPaidButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel.MarkAsPaidCommand);
        _markPaidButton.Click += _markPaidButtonClickHandler;
        _toolTip.SetToolTip(_markPaidButton, "Record payment for the selected bill (Ctrl+P)");

        _generateReportButton = CreateButton("&Report", buttonSize, 5);
        _generateReportButtonClickHandler = async (s, e) => await ExecuteCommandAsync(ViewModel.GenerateReportCommand);
        _generateReportButton.Click += _generateReportButtonClickHandler;
        _toolTip.SetToolTip(_generateReportButton, "Generate utility bill report (Ctrl+R)");

        _exportExcelButton = CreateButton("E&xport", buttonSize, 6);
        _exportExcelButton.Enabled = false;
        _exportExcelButtonClickHandler = ExportExcelButton_Click;
        _exportExcelButton.Click += _exportExcelButtonClickHandler;
        _toolTip.SetToolTip(_exportExcelButton, "Export bills to Excel (Ctrl+X)");

        _refreshButton = CreateButton("Re&fresh", buttonSize, 7);
        _refreshButtonClickHandler = async (s, e) => await RefreshDataAsync();
        _refreshButton.Click += _refreshButtonClickHandler;
        _toolTip.SetToolTip(_refreshButton, "Refresh all data (F5)");

        buttonFlow.Controls.Add(_createBillButton);
        buttonFlow.Controls.Add(_saveBillButton);
        buttonFlow.Controls.Add(_deleteBillButton);
        buttonFlow.Controls.Add(_markPaidButton);
        buttonFlow.Controls.Add(_generateReportButton);
        buttonFlow.Controls.Add(_exportExcelButton);
        buttonFlow.Controls.Add(_refreshButton);

        _buttonPanel.Controls.Add(buttonFlow);
    }

    private SfButton CreateButton(string text, Size size, int tabIndex)
    {
        return Factory.CreateSfButton(text, button =>
        {
            button.Size = size;
            button.TabIndex = tabIndex;
            button.Margin = new Padding(0, 0, (int)DpiAware.LogicalToDeviceUnits(5f), 0);
        });
    }

    private void InitializeBillsGrid()
    {
        _gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
        };
        _billsGrid = Factory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowEditing = false;
            grid.AllowGrouping = true;
            grid.AllowFiltering = true;
            grid.AllowSorting = true;
            grid.AllowResizingColumns = true;
            grid.AllowDraggingColumns = true;
            grid.ShowGroupDropArea = true;
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells;
            grid.EnableDataVirtualization = true;
            grid.SelectionMode = GridSelectionMode.Single;
            grid.NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row;
            grid.ShowRowHeader = true;
            grid.HeaderRowHeight = (int)DpiAware.LogicalToDeviceUnits(35f);
            grid.RowHeight = (int)DpiAware.LogicalToDeviceUnits(28f);
            grid.AllowTriStateSorting = true;
            grid.TabIndex = 8;
            grid.AccessibleName = "Utility Bills Grid";
            grid.AccessibleDescription = "Grid displaying utility bills with filtering and sorting capabilities";
        });
        _billsGrid.PreventStringRelationalFilters(Logger, "BillNumber", "Customer.DisplayName");

        // Configure columns
        _billsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "BillNumber",
            HeaderText = "Bill Number",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f),
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Customer.DisplayName",
            HeaderText = "Customer Name",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(180f),
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = "BillDate",
            HeaderText = "Bill Date",
            Format = "MM/dd/yyyy",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = "DueDate",
            HeaderText = "Due Date",
            Format = "MM/dd/yyyy",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TotalAmount",
            HeaderText = "Total Amount",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f),
            AllowEditing = false,
        });

        _billsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "AmountDue",
            HeaderText = "Amount Due",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110f),
            AllowEditing = false,
        });

        _billsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "StatusDescription",
            HeaderText = "Status",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridCheckBoxColumn
        {
            MappingName = "IsOverdue",
            HeaderText = "Overdue",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f),
            AllowEditing = false
        });

        _billsGrid.SelectionChanged += BillsGrid_SelectionChanged;
        _billsGrid.QueryCellStyle += BillsGrid_QueryCellStyle;

        _gridPanel.Controls.Add(_billsGrid);
        topPanel.Controls.Add(_gridPanel);

        // Button panel
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = LayoutTokens.Dp(50),
            Padding = new Padding(LayoutTokens.PanelPadding),
            BorderStyle = BorderStyle.None,
        };

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1
        };

        for (int i = 0; i < 6; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.67f));

        _createBillButton = Factory.CreateSfButton("&Create Bill", button =>
        {
            button.TabIndex = 2;
            button.AccessibleName = "Create Bill";
            button.AccessibleDescription = "Create a new utility bill for the selected customer";
        });
        _createBillButton.Click += async (s, e) => await ViewModel.CreateBillCommand.ExecuteAsync(null);

        _saveBillButton = Factory.CreateSfButton("&Save Bill", button =>
        {
            button.TabIndex = 3;
            button.AccessibleName = "Save Bill";
            button.AccessibleDescription = "Save changes to the selected bill";
        });
        _saveBillButton.Click += async (s, e) => await ViewModel.SaveBillCommand.ExecuteAsync(null);

        _deleteBillButton = Factory.CreateSfButton("&Delete Bill", button =>
        {
            button.TabIndex = 4;
            button.AccessibleName = "Delete Bill";
            button.AccessibleDescription = "Delete the selected bill";
        });
        _deleteBillButton.Click += async (s, e) => await ViewModel.DeleteBillCommand.ExecuteAsync(null);

        _markPaidButton = Factory.CreateSfButton("&Mark Paid", button =>
        {
            button.TabIndex = 5;
            button.AccessibleName = "Mark Paid";
            button.AccessibleDescription = "Mark the selected bill as paid";
        });
        _markPaidButton.Click += async (s, e) => await ViewModel.MarkAsPaidCommand.ExecuteAsync(null);

        _generateReportButton = Factory.CreateSfButton("&Generate Report", button =>
        {
            button.TabIndex = 6;
            button.AccessibleName = "Generate Report";
            button.AccessibleDescription = "Generate a report of utility bills";
        });
        _generateReportButton.Click += async (s, e) => await ViewModel.GenerateReportCommand.ExecuteAsync(null);

        _refreshButton = Factory.CreateSfButton("&Refresh", button =>
        {
            button.TabIndex = 7;
            button.AccessibleName = "Refresh";
            button.AccessibleDescription = "Refresh the utility bill data";
        });
        _refreshButton.Click += async (s, e) => await RefreshDataAsync();

        buttonTable.Controls.Add(_createBillButton, 0, 0);
        buttonTable.Controls.Add(_saveBillButton, 1, 0);
        buttonTable.Controls.Add(_deleteBillButton, 2, 0);
        buttonTable.Controls.Add(_markPaidButton, 3, 0);
        buttonTable.Controls.Add(_generateReportButton, 4, 0);
        buttonTable.Controls.Add(_refreshButton, 5, 0);

        _buttonPanel!.Controls.Add(buttonTable);
        topPanel.Controls.Add(_buttonPanel);

        _mainSplitContainer.Panel1.Controls.Add(topPanel);
    }

    private void InitializeBottomPanel()
    {
        bottomPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
        };

        // Filter panel
        var filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)DpiAware.LogicalToDeviceUnits(45f),
            Padding = new Padding((int)DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
        };

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };

        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f));

        _searchTextBox = Factory.CreateTextBoxExt(textBox =>
        {
            textBox.Dock = DockStyle.Fill;
            textBox.PlaceholderText = "Search bills or customers...";
            textBox.TabIndex = 9;
            textBox.AccessibleName = "Search";
            textBox.AccessibleDescription = "Search utility bills by bill number or customer name";
        });
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;
        _toolTip!.SetToolTip(_searchTextBox, "Search by bill number, customer name, or account number");

        _statusFilterComboBox = Factory.CreateSfComboBox(combo =>
        {
            combo.Dock = DockStyle.Fill;
            combo.TabIndex = 10;
            combo.AccessibleName = "Status Filter";
            combo.AccessibleDescription = "Filter bills by payment status";
        });
        var statusItems = Enum.GetNames(typeof(BillStatus)).ToList();
        statusItems.Insert(0, "(All Statuses)");
        _statusFilterComboBox.DataSource = statusItems;
        _statusFilterComboBox.SelectedIndex = 0;
        _statusFilterComboBox.SelectedIndexChanged += StatusFilterComboBox_SelectedIndexChanged;
        _toolTip.SetToolTip(_statusFilterComboBox, "Filter bills by status");

        _overdueOnlyCheckBox = Factory.CreateCheckBoxAdv("Overdue Only", checkBox =>
        {
            checkBox.Dock = DockStyle.Fill;
            checkBox.TabIndex = 11;
            checkBox.AccessibleName = "Show Overdue Only";
            checkBox.AccessibleDescription = "Show only overdue bills";
        });
        _overdueOnlyCheckBox.CheckStateChanged += OverdueOnlyCheckBox_CheckedChanged;
        _toolTip.SetToolTip(_overdueOnlyCheckBox, "Show only bills that are past their due date");

        filterTable.Controls.Add(_searchTextBox, 0, 0);
        filterTable.Controls.Add(_statusFilterComboBox, 1, 0);
        filterTable.Controls.Add(_overdueOnlyCheckBox, 2, 0);

        filterPanel.Controls.Add(filterTable);
        bottomPanel.Controls.Add(filterPanel);

        // Customers grid
        InitializeCustomersGrid();
        bottomPanel.Controls.Add(_customersGrid!);

        _mainSplitContainer!.Panel2.Controls.Add(bottomPanel);
    }

    private void InitializeCustomersGrid()
    {
        var customersPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
        };
        _customersGrid = Factory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowEditing = false;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AllowFiltering = true;
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells;
            grid.EnableDataVirtualization = true;
            grid.SelectionMode = GridSelectionMode.Single;
            grid.NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row;
            grid.ShowRowHeader = false;
            grid.HeaderRowHeight = (int)DpiAware.LogicalToDeviceUnits(35f);
            grid.RowHeight = (int)DpiAware.LogicalToDeviceUnits(28f);
            grid.TabIndex = 12;
            grid.AccessibleName = "Customers Grid";
            grid.AccessibleDescription = "Grid displaying utility customers";
        });
        _customersGrid.PreventStringRelationalFilters(Logger, "AccountNumber", "DisplayName", "ServiceAddress", "PhoneNumber");

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountNumber",
            HeaderText = "Account Number",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f)
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "DisplayName",
            HeaderText = "Customer Name",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(200f)
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "ServiceAddress",
            HeaderText = "Service Address",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250f)
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PhoneNumber",
            HeaderText = "Phone",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f)
        });

        _customersGrid.CurrentCellActivated += CustomersGrid_CurrentCellActivated;

        /// <summary>
        /// HOT PATH: Grid cell activation handler for customer selection. Fires synchronously on every grid cell activation.
        /// Type Contract: Expects grid rows to contain UtilityCustomer objects where DisplayName = CompanyName OR (FirstName + LastName).
        /// Updates ViewModel.SelectedCustomer to trigger related bills refresh.
        /// </summary>
        void CustomersGrid_CurrentCellActivated(object? sender, Syncfusion.WinForms.DataGrid.Events.CurrentCellActivatedEventArgs e)
        {
            try
            {
                var currentCell = _customersGrid?.CurrentCell;
                if (currentCell != null && currentCell.RowIndex >= 0 && ViewModel != null)
                {
                    // Handle cell activation - load related utility bills for selected customer
                    var selectedRow = _customersGrid?.View?.Records.ElementAtOrDefault(currentCell.RowIndex);
                    if (selectedRow?.Data is UtilityCustomer customer)
                    {
                        Logger.LogDebug("Customer selected: {CustomerId} - {CustomerName}", customer.Id, customer.DisplayName);
                        ViewModel.SelectedCustomer = customer;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to handle customer selection");
            }
        }

#pragma warning disable CS8602
        customersPanel.Controls.Add(_customersGrid!);
#pragma warning restore CS8602
        bottomPanel.Controls.Add(customersPanel);

        _mainSplitContainer.Panel2.Controls.Add(bottomPanel);
    }

    #endregion

    #region Data Binding

    private void BindViewModel()
    {
        if (ViewModel == null) return;

        // Subscribe to ViewModel property changes
        ViewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += ViewModelPropertyChangedHandler;

        // Subscribe to collection changes. Post updates to the captured UI SynchronizationContext
        // to avoid creating control handles from background threads (which can throw).
        _billsCollectionChangedHandler = (s, e) =>
        {
            if (_uiSyncContext != null)
            {
                try
                {
                    _uiSyncContext.Post(_ =>
                    {
                        try { UpdateBillsGrid(); }
                        catch (Exception ex) { Logger.LogError(ex, "Error updating bills grid from UI sync context"); }
                    }, null);
                }
                catch (Exception)
                {
                    // Fallback to control-based invoke if posting fails
                    this.InvokeIfRequired(UpdateBillsGrid);
                }
            }
            else
            {
                this.InvokeIfRequired(UpdateBillsGrid);
            }
        };
        ViewModel.FilteredBills.CollectionChanged += _billsCollectionChangedHandler;

        _customersCollectionChangedHandler = (s, e) =>
        {
            if (_uiSyncContext != null)
            {
                try
                {
                    _uiSyncContext.Post(_ =>
                    {
                        try { UpdateCustomersGrid(); }
                        catch (Exception ex) { Logger.LogError(ex, "Error updating customers grid from UI sync context"); }
                    }, null);
                }
                catch (Exception)
                {
                    this.InvokeIfRequired(UpdateCustomersGrid);
                }
            }
            else
            {
                this.InvokeIfRequired(UpdateCustomersGrid);
            }
        };
        ViewModel.Customers.CollectionChanged += _customersCollectionChangedHandler;

        // Initial data binding
        UpdateBillsGrid();
        UpdateCustomersGrid();
        UpdateSummaryLabels();
        UpdateButtonStates();
    }

    private void UpdateBillsGrid()
    {
        if (_billsGrid == null || ViewModel == null) return;

        try
        {
            _billsGrid.SuspendLayout();
            var snapshot = ViewModel.FilteredBills.Cast<object?>().ToList();
            _billsGrid.DataSource = snapshot;
            _billsGrid.ResumeLayout();
            _billsGrid.Refresh();
            UpdateExportButtonState();

            UpdateNoDataOverlay();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating bills grid");
        }
    }

    private void UpdateCustomersGrid()
    {
        if (_customersGrid == null || ViewModel == null) return;

        try
        {
            _customersGrid.SuspendLayout();
            var snapshot = ViewModel.Customers.Cast<object?>().ToList();
            _customersGrid.DataSource = snapshot;
            _customersGrid.ResumeLayout();
            _customersGrid.Refresh();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating customers grid");
        }
    }

    private void UpdateSummaryLabels()
    {
        if (ViewModel == null) return;

        if (_totalOutstandingLabel != null)
            _totalOutstandingLabel.Text = $"Total Outstanding: {ViewModel.TotalOutstanding:C}";

        if (_overdueCountLabel != null)
            _overdueCountLabel.Text = $"Overdue Bills: {ViewModel.OverdueCount}";

        if (_totalRevenueLabel != null)
            _totalRevenueLabel.Text = $"Total Revenue: {ViewModel.TotalRevenue:C}";

        if (_billsThisMonthLabel != null)
            _billsThisMonthLabel.Text = $"Bills This Month: {ViewModel.BillsThisMonth}";
    }

    private void UpdateButtonStates()
    {
        if (ViewModel == null) return;

        var hasBillSelection = ViewModel.SelectedBill != null;
        var hasCustomerSelection = ViewModel.SelectedCustomer != null;

        if (_createBillButton != null)
            _createBillButton.Enabled = hasCustomerSelection;

        if (_saveBillButton != null)
            _saveBillButton.Enabled = hasBillSelection;

        if (_deleteBillButton != null)
            _deleteBillButton.Enabled = hasBillSelection;

        if (_markPaidButton != null)
            _markPaidButton.Enabled = hasBillSelection && ViewModel.SelectedBill != null && !ViewModel.SelectedBill.IsPaid;

    }

    private void UpdateExportButtonState()
    {
        if (_exportExcelButton == null || _billsGrid == null)
        {
            return;
        }

        var view = _billsGrid.View;
        var hasRows = view?.Records?.Count > 0;
        var isLoading = ViewModel?.IsLoading ?? false;
        _exportExcelButton.Enabled = view != null && hasRows && !isLoading;
    }

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.FilteredBills.Any();
    }

    #endregion

    #region ICompletablePanel Implementation

    /// <summary>
    /// Loads the panel asynchronously (ICompletablePanel implementation).
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct = default)
    {
        if (IsLoaded) return; // Prevent double-load

        try
        {
            IsBusy = true;
            UpdateStatus("Loading utility bill data...");

            var token = RegisterOperation();
            await (ViewModel.LoadBillsCommand?.ExecuteAsync(null) ?? Task.CompletedTask);
            await (ViewModel.LoadCustomersCommand?.ExecuteAsync(null) ?? Task.CompletedTask);

            SetHasUnsavedChanges(false);
            UpdateStatus("Utility bill data loaded successfully");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Load cancelled");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Load failed: {ex.Message}");
            throw;
        }
        finally
        {
            IsBusy = false;
            UpdateExportButtonState();
        }
    }

    /// <summary>
    /// Validates the panel asynchronously (ICompletablePanel implementation).
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        _errorProvider?.Clear();
        var errors = new List<ValidationItem>();

        // For now, basic validation - can be extended
        if (ViewModel?.SelectedBill != null)
        {
            // Validate selected bill if needed
        }

        return errors.Count > 0 ? ValidationResult.Failed(errors.ToArray()) : ValidationResult.Success;
    }

    /// <summary>
    /// Saves the panel asynchronously (ICompletablePanel implementation).
    /// </summary>
    public override async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            UpdateStatus("Saving utility bill data...");

            var token = RegisterOperation();
            // Individual save operations are handled by commands
            // No global save needed for this panel

            SetHasUnsavedChanges(false);
            UpdateStatus("Utility bill data saved successfully");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Save failed: {ex.Message}");
            throw;
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
        var item = ValidationErrors.FirstOrDefault();
        item?.ControlRef?.Focus();
    }

    #endregion

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Ensure all UI updates happen on the UI thread to avoid cross-thread exceptions.
        void PostToUi(System.Action a)
        {
            if (_uiSyncContext != null)
            {
                try { _uiSyncContext.Post(_ => { try { a(); } catch { } }, null); }
                catch
                {
                    // fallback to control-based invoke if posting fails
                    try { this.InvokeIfRequired(a); } catch { }
                }
            }
            else
            {
                try { this.InvokeIfRequired(a); } catch { }
            }
        }

        if (e.PropertyName == nameof(ViewModel.IsLoading))
        {
            PostToUi(() =>
            {
                if (_loadingOverlay != null)
                    _loadingOverlay.Visible = ViewModel.IsLoading;
                UpdateNoDataOverlay();
            });
        }
        else if (e.PropertyName == nameof(ViewModel.StatusText))
        {
            PostToUi(() =>
            {
                if (_statusLabel != null)
                    _statusLabel.Text = ViewModel.StatusText;
            });
        }
        else if (e.PropertyName == nameof(ViewModel.SelectedBill) || e.PropertyName == nameof(ViewModel.SelectedCustomer))
        {
            PostToUi(UpdateButtonStates);
        }
        else if (e.PropertyName == nameof(ViewModel.TotalOutstanding) ||
                 e.PropertyName == nameof(ViewModel.OverdueCount) ||
                 e.PropertyName == nameof(ViewModel.TotalRevenue) ||
                 e.PropertyName == nameof(ViewModel.BillsThisMonth))
        {
            PostToUi(UpdateSummaryLabels);
        }
    }

    private void BillsGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_billsGrid == null || ViewModel == null) return;

        try
        {
            var selectedItem = _billsGrid.SelectedItem as UtilityBill;
            ViewModel.SelectedBill = selectedItem;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling bills grid selection");
        }
    }

    private void CustomersGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_customersGrid == null || ViewModel == null) return;

        try
        {
            var selectedItem = _customersGrid.SelectedItem as UtilityCustomer;
            ViewModel.SelectedCustomer = selectedItem;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling customers grid selection");
        }
    }

    private void BillsGrid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
    {
        if (e.Column.MappingName == "AmountDue" && e.DataRow != null)
        {
            if (e.DataRow.RowData is UtilityBill bill && bill.IsOverdue)
            {
                e.Style.TextColor = Color.Red;
            }
        }
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _searchTextBox != null)
        {
            ViewModel.SearchText = _searchTextBox.Text;
        }
    }

    private void StatusFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel == null || _statusFilterComboBox == null) return;

        if (_statusFilterComboBox.SelectedIndex == 0)
        {
            ViewModel.SelectedStatus = null;
        }
        else if (_statusFilterComboBox.SelectedItem is string statusString &&
                 Enum.TryParse<BillStatus>(statusString, out var status))
        {
            ViewModel.SelectedStatus = status;
        }
    }

    private void OverdueOnlyCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _overdueOnlyCheckBox != null)
        {
            ViewModel.IsOverdueOnly = _overdueOnlyCheckBox.Checked;
        }
    }

    private void ExportExcelButton_Click(object? sender, EventArgs e)
    {
        BeginInvoke(new Func<Task>(async () =>
        {
            try
            {
                if (_billsGrid == null) return;

                var view = _billsGrid.View;
                if (view == null)
                {
                    MessageBox.Show("Grid is still loading. Please try again once data is ready.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (view.Records?.Count == 0)
                {
                    MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateStatus("No bills available to export.");
                    return;
                }

                var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                    owner: this,
                    operationKey: $"{nameof(UtilityBillPanel)}.Excel",
                    dialogTitle: "Export Utility Bills",
                    filter: "Excel Files (*.xlsx)|*.xlsx",
                    defaultExtension: "xlsx",
                    defaultFileName: $"UtilityBills_{DateTime.Now:yyyyMMdd}.xlsx",
                    exportAction: (filePath, cancellationToken) => ExportService.ExportGridToExcelAsync(_billsGrid, filePath, cancellationToken),
                    statusCallback: UpdateStatus,
                    logger: Logger,
                    cancellationToken: CancellationToken.None);

                if (result.IsSkipped)
                {
                    MessageBox.Show(result.ErrorMessage ?? "An export is already in progress.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (result.IsCancelled)
                {
                    UpdateStatus("Export cancelled.");
                    return;
                }

                if (!result.IsSuccess)
                {
                    UpdateStatus("Export failed.");
                    MessageBox.Show(result.ErrorMessage ?? "Export failed.", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                UpdateStatus("Export completed successfully");
                MessageBox.Show(
                    $"Bills exported successfully to:\n{result.FilePath}",
                    "Export Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error exporting to Excel");
                UpdateStatus("Export failed.");
                MessageBox.Show(
                    $"Error exporting to Excel: {ex.Message}",
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }));
    }

    #region Helper Methods

    private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (ViewModel == null) return;

            Logger.LogInformation("Refreshing utility bill data");
            UpdateStatus("Refreshing data...");

            await Task.WhenAll(
                ViewModel.LoadBillsCommand.ExecuteAsync(null),
                ViewModel.LoadCustomersCommand.ExecuteAsync(null));

            UpdateStatus("Data refreshed successfully");
            UpdateExportButtonState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing data");
            UpdateStatus($"Error: {ex.Message}");

            var serviceProvider = ResolveServiceProvider();
            if (serviceProvider != null)
            {
                var errorService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(serviceProvider);
                errorService?.ReportError(ex, "Failed to refresh utility bill data", showToUser: true);
            }
        }
    }

    private async Task ExecuteCommandAsync(IAsyncRelayCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (command.CanExecute(null))
            {
                await command.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing command");

            var serviceProvider = ResolveServiceProvider();
            if (serviceProvider != null)
            {
                var errorService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(serviceProvider);
                errorService?.ReportError(ex, "An error occurred while processing your request", showToUser: true);
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (MinimumSize.Width < 1024 || MinimumSize.Height < 720)
        {
            MinimumSize = new Size(1024, 720);
        }

        PerformLayout();
    }

    private void UpdateStatus(string message)
    {
        if (_uiSyncContext != null)
        {
            try { _uiSyncContext.Post(_ => { try { if (_statusLabel != null && !_statusLabel.IsDisposed) _statusLabel.Text = message ?? string.Empty; } catch { } }, null); }
            catch { this.InvokeIfRequired(() => { if (_statusLabel != null && !_statusLabel.IsDisposed) _statusLabel.Text = message ?? string.Empty; }); }
        }
        else
        {
            try { this.InvokeIfRequired(() => { if (_statusLabel != null && !_statusLabel.IsDisposed) _statusLabel.Text = message ?? string.Empty; }); }
            catch { }
        }
    }

    #endregion

    #region Theme Integration

    public override void ApplyTheme(string theme)
    {
        try
        {
            base.ApplyTheme(theme);

            // Theme is automatically applied by SfSkinManager cascade from parent
            // Button icons removed per SfSkinManager enforcement rules
            // ThemeManager subscription removed per project rules (SfSkinManager cascade only)
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error applying theme");
        }
    }

    // UpdateButtonIcons method removed - icon management via deprecated IThemeIconService is not authorized
    // SfSkinManager has sole proprietorship over all theme and color management

    #endregion

    #region Lifecycle
    // Deprecated IThemeIconService removed - SfSkinManager handles theme cascade

    protected override void OnPanelLoaded(EventArgs e)
    {
        try
        {
            if (ViewModel != null)
            {
                Task.Run(async () =>
                {
                    await RefreshDataAsync();
                }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading panel data");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Unsubscribe events
                if (ViewModel != null && ViewModelPropertyChangedHandler != null)
                    ViewModel.PropertyChanged -= ViewModelPropertyChangedHandler;

                if (ViewModel?.FilteredBills != null && _billsCollectionChangedHandler != null)
                    ViewModel.FilteredBills.CollectionChanged -= _billsCollectionChangedHandler;

                if (ViewModel?.Customers != null && _customersCollectionChangedHandler != null)
                    ViewModel.Customers.CollectionChanged -= _customersCollectionChangedHandler;

                if (_billsGrid != null)
                {
                    _billsGrid.SelectionChanged -= BillsGrid_SelectionChanged;
                    _billsGrid.QueryCellStyle -= BillsGrid_QueryCellStyle;
                    _billsGrid.SafeClearDataSource();
                }

                if (_customersGrid != null)
                {
                    _customersGrid.SelectionChanged -= CustomersGrid_SelectionChanged;
                    _customersGrid.SafeClearDataSource();
                }

                if (_panelHeader != null)
                {
                    if (_panelHeaderRefreshHandler != null)
                        _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                    if (_panelHeaderCloseHandler != null)
                        _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                }

                // Unsubscribe button handlers
                if (_createBillButton != null && _createBillButtonClickHandler != null)
                    _createBillButton.Click -= _createBillButtonClickHandler;
                if (_saveBillButton != null && _saveBillButtonClickHandler != null)
                    _saveBillButton.Click -= _saveBillButtonClickHandler;
                if (_deleteBillButton != null && _deleteBillButtonClickHandler != null)
                    _deleteBillButton.Click -= _deleteBillButtonClickHandler;
                if (_markPaidButton != null && _markPaidButtonClickHandler != null)
                    _markPaidButton.Click -= _markPaidButtonClickHandler;
                if (_generateReportButton != null && _generateReportButtonClickHandler != null)
                    _generateReportButton.Click -= _generateReportButtonClickHandler;
                if (_exportExcelButton != null && _exportExcelButtonClickHandler != null)
                    _exportExcelButton.Click -= _exportExcelButtonClickHandler;
                if (_refreshButton != null && _refreshButtonClickHandler != null)
                    _refreshButton.Click -= _refreshButtonClickHandler;

                // Safe dispose Syncfusion controls
                _billsGrid.SafeDispose();
                _customersGrid.SafeDispose();

                // Dispose other controls
                _mainSplitContainer?.Dispose();
                _statusStrip?.Dispose();
                _panelHeader?.Dispose();
                _loadingOverlay?.Dispose();
                _noDataOverlay?.Dispose();
                _toolTip?.Dispose();
                _errorProvider?.Dispose();

                Logger.LogDebug("UtilityBillPanel disposed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error disposing UtilityBillPanel");
            }
        }

        base.Dispose(disposing);
    }

    #endregion
}
