using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using System.ComponentModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using Syncfusion.WinForms.DataGrid.Events;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.Models;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using WileyWidget.Services;
using WileyWidget.WinForms.Utils;

namespace WileyWidget.WinForms.Controls;

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
    private GradientPanelExt? _summaryPanel;
    private GradientPanelExt? _gridPanel;
    private GradientPanelExt? _buttonPanel;
    private SplitContainer? _mainSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private ToolTip? _toolTip;


    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private NotifyCollectionChangedEventHandler? _billsCollectionChangedHandler;
    private NotifyCollectionChangedEventHandler? _customersCollectionChangedHandler;

    #endregion

    #region Constructor

    public UtilityBillPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<UtilityBillViewModel>> logger)
        : base(scopeFactory, logger)
    {
    }

    #endregion

    #region Initialization

    protected override void OnViewModelResolved(UtilityBillViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);

        try
        {
            InitializeControls();
            BindViewModel();

            // Defer sizing validation until layout is complete
            this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));

            Logger.LogDebug("UtilityBillPanel initialized successfully");
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

        // Set up panel properties
        Text = "Utility Bills";
        Size = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1400f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(900f));
        MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
        AutoScroll = true;
        Padding = new Padding(8);

        // Initialize tooltip
        _toolTip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 500,
            ReshowDelay = 100,
            ShowAlways = true
        };

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Utility Bill Management",
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f)
        };
        _panelHeader.RefreshClicked += async (s, e) => await RefreshDataAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main split container (bills top, customers bottom)
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            TabStop = false
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplitContainer, (int)DpiAware.LogicalToDeviceUnits(500f));

        InitializeTopPanel();
        InitializeBottomPanel();

        Controls.Add(_mainSplitContainer);

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
            Message = "Loading utility bill data...",
            Visible = false
        };
        Controls.Add(_loadingOverlay);

        // No data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Message = "No utility bills yet\r\nCreate a new bill for a customer to get started",
            Visible = false
        };
        Controls.Add(_noDataOverlay);

        ResumeLayout(false);
        PerformLayout();
    }

    private void InitializeTopPanel()
    {
        var topPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(topPanel, "Office2019Colorful");

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
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f),
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, "Office2019Colorful");

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
            Font = new Font(Font.FontFamily, DpiAware.LogicalToDeviceUnits(10f), FontStyle.Bold),
            // ForeColor removed - let SkinManager handle theming
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false
        };
    }

    private void InitializeButtonPanel()
    {
        _buttonPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, "Office2019Colorful");

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
        _createBillButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel.CreateBillCommand);
        _toolTip!.SetToolTip(_createBillButton, "Create a new utility bill for the selected customer (Ctrl+N)");

        _saveBillButton = CreateButton("&Save", buttonSize, 2);
        _saveBillButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel.SaveBillCommand);
        _toolTip.SetToolTip(_saveBillButton, "Save changes to the selected bill (Ctrl+S)");

        _deleteBillButton = CreateButton("&Delete", buttonSize, 3);
        _deleteBillButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel.DeleteBillCommand);
        _toolTip.SetToolTip(_deleteBillButton, "Delete the selected bill (Del)");

        _markPaidButton = CreateButton("Mark &Paid", buttonSize, 4);
        _markPaidButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel.MarkAsPaidCommand);
        _toolTip.SetToolTip(_markPaidButton, "Record payment for the selected bill (Ctrl+P)");

        _generateReportButton = CreateButton("&Report", buttonSize, 5);
        _generateReportButton.Click += async (s, e) => await ExecuteCommandAsync(ViewModel.GenerateReportCommand);
        _toolTip.SetToolTip(_generateReportButton, "Generate utility bill report (Ctrl+R)");

        _exportExcelButton = CreateButton("E&xport", buttonSize, 6);
        _exportExcelButton.Click += ExportExcelButton_Click;
        _toolTip.SetToolTip(_exportExcelButton, "Export bills to Excel (Ctrl+X)");

        _refreshButton = CreateButton("Re&fresh", buttonSize, 7);
        _refreshButton.Click += async (s, e) => await RefreshDataAsync();
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
        return new SfButton
        {
            Text = text,
            Size = size,
            TabIndex = tabIndex,
            Margin = new Padding(0, 0, (int)DpiAware.LogicalToDeviceUnits(5f), 0)
        };
    }

    private void InitializeBillsGrid()
    {
        _gridPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_gridPanel, "Office2019Colorful");

        _billsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            AllowGrouping = true,
            AllowFiltering = true,
            AllowSorting = true,
            AllowResizingColumns = true,
            AllowDraggingColumns = true,
            ShowGroupDropArea = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
            ShowRowHeader = true,
            HeaderRowHeight = (int)DpiAware.LogicalToDeviceUnits(35f),
            RowHeight = (int)DpiAware.LogicalToDeviceUnits(28f),
            AllowTriStateSorting = true,
            TabIndex = 8,
            AccessibleName = "Utility Bills Grid",
            AccessibleDescription = "Grid displaying utility bills with filtering and sorting capabilities"
        };

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
            Height = 50,
            Padding = new Padding(10)
        };

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1
        };

        for (int i = 0; i < 6; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.67f));

        _createBillButton = new Button
        {
            Text = "&Create Bill",
            TabIndex = 2,
            AccessibleName = "Create Bill",
            AccessibleDescription = "Create a new utility bill for the selected customer"
        };
        _createBillButton.Click += async (s, e) => await _viewModel.CreateBillCommand.ExecuteAsync(null);

        _saveBillButton = new Button
        {
            Text = "&Save Bill",
            TabIndex = 3,
            AccessibleName = "Save Bill",
            AccessibleDescription = "Save changes to the selected bill"
        };
        _saveBillButton.Click += async (s, e) => await _viewModel.SaveBillCommand.ExecuteAsync(null);

        _deleteBillButton = new Button
        {
            Text = "&Delete Bill",
            TabIndex = 4,
            AccessibleName = "Delete Bill",
            AccessibleDescription = "Delete the selected bill"
        };
        _deleteBillButton.Click += async (s, e) => await _viewModel.DeleteBillCommand.ExecuteAsync(null);

        _markPaidButton = new Button
        {
            Text = "&Mark Paid",
            TabIndex = 5,
            AccessibleName = "Mark Paid",
            AccessibleDescription = "Mark the selected bill as paid"
        };
        _markPaidButton.Click += async (s, e) => await _viewModel.MarkAsPaidCommand.ExecuteAsync(null);

        _generateReportButton = new Button
        {
            Text = "&Generate Report",
            TabIndex = 6,
            AccessibleName = "Generate Report",
            AccessibleDescription = "Generate a report of utility bills"
        };
        _generateReportButton.Click += async (s, e) => await _viewModel.GenerateReportCommand.ExecuteAsync(null);

        _refreshButton = new Button
        {
            Text = "&Refresh",
            TabIndex = 7,
            AccessibleName = "Refresh",
            AccessibleDescription = "Refresh the utility bill data"
        };
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
        var bottomPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(bottomPanel, "Office2019Colorful");

        // Filter panel
        var filterPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = (int)DpiAware.LogicalToDeviceUnits(45f),
            Padding = new Padding((int)DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(filterPanel, "Office2019Colorful");

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

        _searchTextBox = new TextBoxExt
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Search bills or customers...",
            TabIndex = 9,
            AccessibleName = "Search",
            AccessibleDescription = "Search utility bills by bill number or customer name"
        };
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;
        _toolTip!.SetToolTip(_searchTextBox, "Search by bill number, customer name, or account number");

        _statusFilterComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            TabIndex = 10,
            AccessibleName = "Status Filter",
            AccessibleDescription = "Filter bills by payment status"
        };
        var statusItems = Enum.GetNames(typeof(BillStatus)).ToList();
        statusItems.Insert(0, "(All Statuses)");
        _statusFilterComboBox.DataSource = statusItems;
        _statusFilterComboBox.SelectedIndex = 0;
        _statusFilterComboBox.SelectedIndexChanged += StatusFilterComboBox_SelectedIndexChanged;
        _toolTip.SetToolTip(_statusFilterComboBox, "Filter bills by status");

        _overdueOnlyCheckBox = new CheckBoxAdv
        {
            Text = "Overdue Only",
            Dock = DockStyle.Fill,
            TabIndex = 11,
            AccessibleName = "Show Overdue Only",
            AccessibleDescription = "Show only overdue bills"
        };
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
        var customersPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f)),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(customersPanel, "Office2019Colorful");

        _customersGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AllowFiltering = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
            ShowRowHeader = false,
            HeaderRowHeight = (int)DpiAware.LogicalToDeviceUnits(35f),
            RowHeight = (int)DpiAware.LogicalToDeviceUnits(28f),
            TabIndex = 12,
            AccessibleName = "Customers Grid",
            AccessibleDescription = "Grid displaying utility customers"
        };

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
        _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Subscribe to collection changes
        _billsCollectionChangedHandler = (s, e) => UpdateBillsGrid();
        ViewModel.FilteredBills.CollectionChanged += _billsCollectionChangedHandler;

        _customersCollectionChangedHandler = (s, e) => UpdateCustomersGrid();
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

    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay == null || ViewModel == null) return;

        _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.FilteredBills.Any();
    }

    #endregion

    #region Event Handlers

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsLoading))
        {
            if (_loadingOverlay != null)
                _loadingOverlay.Visible = ViewModel.IsLoading;
            UpdateNoDataOverlay();
        }
        else if (e.PropertyName == nameof(ViewModel.StatusText))
        {
            if (_statusLabel != null)
                _statusLabel.Text = ViewModel.StatusText;
        }
        else if (e.PropertyName == nameof(ViewModel.SelectedBill) || e.PropertyName == nameof(ViewModel.SelectedCustomer))
        {
            UpdateButtonStates();
        }
        else if (e.PropertyName == nameof(ViewModel.TotalOutstanding) ||
                 e.PropertyName == nameof(ViewModel.OverdueCount) ||
                 e.PropertyName == nameof(ViewModel.TotalRevenue) ||
                 e.PropertyName == nameof(ViewModel.BillsThisMonth))
        {
            UpdateSummaryLabels();
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

    private async void ExportExcelButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_billsGrid == null) return;

            using var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Export Utility Bills",
                FileName = $"UtilityBills_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                UpdateStatus("Exporting to Excel...");
                await ExportService.ExportGridToExcelAsync(_billsGrid, saveDialog.FileName);
                UpdateStatus("Export completed successfully");

                MessageBox.Show(
                    $"Bills exported successfully to:\n{saveDialog.FileName}",
                    "Export Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error exporting to Excel");
            MessageBox.Show(
                $"Error exporting to Excel: {ex.Message}",
                "Export Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    #endregion

    #region Helper Methods

    private async Task RefreshDataAsync()
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
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing data");
            UpdateStatus($"Error: {ex.Message}");

            if (ServiceProvider != null)
            {
                var errorService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(ServiceProvider);
                errorService?.ReportError(ex, "Failed to refresh utility bill data", showToUser: true);
            }
        }
    }

    private async Task ExecuteCommandAsync(IAsyncRelayCommand command)
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

            if (ServiceProvider != null)
            {
                var errorService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(ServiceProvider);
                errorService?.ReportError(ex, "An error occurred while processing your request", showToUser: true);
            }
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
        }
    }

    private void ClosePanel()
    {
        try
        {
            var parentForm = FindForm();
            if (parentForm is Forms.MainForm mainForm)
            {
                mainForm.ClosePanel(Name);
                return;
            }

            var method = parentForm?.GetType().GetMethod("ClosePanel",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            method?.Invoke(parentForm, new object[] { Name });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "UtilityBillPanel: ClosePanel failed");
        }
    }

    #endregion

    #region Theme Integration

    private void ApplyTheme(AppTheme theme)
    {
        try
        {
            // Theme is automatically applied by SfSkinManager cascade from parent
            // Update button icons based on theme
            IThemeIconService? iconService = null;
            if (ServiceProvider != null)
            {
                iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeIconService>(ServiceProvider);
            }
            if (iconService != null)
            {
                UpdateButtonIcons(iconService, theme);
            }

            // ThemeManager subscription removed per project rules (SfSkinManager cascade only)
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error applying theme");
        }
    }

    private void UpdateButtonIcons(IThemeIconService iconService, AppTheme theme)
    {
        try
        {
            var iconSize = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(16f);

            if (_refreshButton != null)
                _refreshButton.Image = iconService.GetIcon("refresh", theme, iconSize);

            if (_createBillButton != null)
                _createBillButton.Image = iconService.GetIcon("add", theme, iconSize);

            if (_saveBillButton != null)
                _saveBillButton.Image = iconService.GetIcon("save", theme, iconSize);

            if (_deleteBillButton != null)
                _deleteBillButton.Image = iconService.GetIcon("delete", theme, iconSize);

            if (_exportExcelButton != null)
                _exportExcelButton.Image = iconService.GetIcon("export", theme, iconSize);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error updating button icons");
        }
    }

    #endregion

    #region Lifecycle

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

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
                if (ViewModel != null && _viewModelPropertyChangedHandler != null)
                    ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;

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
                    _panelHeader.RefreshClicked -= async (s, e) => await RefreshDataAsync();
                    _panelHeader.CloseClicked -= (s, e) => ClosePanel();
                }

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
