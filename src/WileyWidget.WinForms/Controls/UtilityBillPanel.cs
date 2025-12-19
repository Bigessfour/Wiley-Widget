using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for managing utility bills with customer billing, payment tracking, and reporting.
/// Features bill creation, status management, customer selection, and financial summaries.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class UtilityBillPanel : UserControl
{
    private readonly UtilityBillViewModel _viewModel;
    private readonly ILogger<UtilityBillPanel> _logger;

    // UI Controls
    private SfDataGrid? _billsGrid;
    private SfDataGrid? _customersGrid;
    private Button? _createBillButton;
    private Button? _saveBillButton;
    private Button? _deleteBillButton;
    private Button? _markPaidButton;
    private Button? _generateReportButton;
    private Button? _refreshButton;
    private TextBox? _searchTextBox;
    private ComboBox? _statusFilterComboBox;
    private CheckBox? _overdueOnlyCheckBox;
    private Label? _totalOutstandingLabel;
    private Label? _overdueCountLabel;
    private Label? _totalRevenueLabel;
    private Label? _billsThisMonthLabel;
    private Panel? _summaryPanel;
    private Panel? _gridPanel;
    private Panel? _buttonPanel;
    private SplitContainer? _mainSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    public UtilityBillPanel(
        UtilityBillViewModel viewModel,
        ILogger<UtilityBillPanel> logger)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeControls();
    }

    private void InitializeControls()
    {
        // Apply theme to this control - handled by parent form cascade
        // ThemeColors.ApplyTheme(this);

        // Set up form properties
        Text = "Utility Bills";
        Size = new Size(1400, 900);
        MinimumSize = new Size(1000, 600);

        // Panel header with actions
        _panelHeader = new PanelHeader { Dock = DockStyle.Top, Title = "Utility Bill Management" };
        _panelHeader.RefreshClicked += async (s, e) => await RefreshDataAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main split container
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 400
        };

        // Top panel - Bills grid and controls
        InitializeTopPanel();

        // Bottom panel - Customers grid
        InitializeBottomPanel();

        // Status strip for operation feedback
        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay { Message = "Loading utility bill data..." };
        Controls.Add(_loadingOverlay);

        // No data overlay
        _noDataOverlay = new NoDataOverlay { Message = "No utility bills found" };
        Controls.Add(_noDataOverlay);

        Controls.Add(_mainSplitContainer);
        Controls.Add(_statusStrip);

        // Wire up ViewModel events
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Set tab order
        SetTabOrder();
    }

    private void InitializeTopPanel()
    {
        var topPanel = new Panel { Dock = DockStyle.Fill };

        // Summary panel
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(10)
        };

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2
        };

        summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _totalOutstandingLabel = new Label
        {
            Text = "Total Outstanding: $0.00",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _overdueCountLabel = new Label
        {
            Text = "Overdue Bills: 0",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _totalRevenueLabel = new Label
        {
            Text = "Total Revenue: $0.00",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _billsThisMonthLabel = new Label
        {
            Text = "Bills This Month: 0",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        summaryTable.Controls.Add(_totalOutstandingLabel, 0, 0);
        summaryTable.Controls.Add(_overdueCountLabel, 1, 0);
        summaryTable.Controls.Add(_totalRevenueLabel, 2, 0);
        summaryTable.Controls.Add(_billsThisMonthLabel, 3, 0);

        _summaryPanel.Controls.Add(summaryTable);
        topPanel.Controls.Add(_summaryPanel);

        // Bills grid
        _gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _billsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = true,
            AllowResizingColumns = true,
            AllowSorting = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            EditMode = EditMode.SingleClick,
            TabIndex = 1,
            AccessibleName = "Utility Bills Grid"
        };

        // Configure grid columns
        _billsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "BillNumber",
            HeaderText = "Bill Number",
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Customer.AccountNumber",
            HeaderText = "Customer Account",
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "Customer.DisplayName",
            HeaderText = "Customer Name",
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = "BillDate",
            HeaderText = "Bill Date",
            Format = "MM/dd/yyyy"
        });

        _billsGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = "DueDate",
            HeaderText = "Due Date",
            Format = "MM/dd/yyyy"
        });

        _billsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TotalAmount",
            HeaderText = "Total Amount",
            Format = "C2",
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "AmountDue",
            HeaderText = "Amount Due",
            Format = "C2",
            AllowEditing = false
        });

        _billsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "StatusDescription",
            HeaderText = "Status",
            AllowEditing = false
        });

        _billsGrid.CurrentCellActivated += BillsGrid_CurrentCellActivated;
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

        _buttonPanel.Controls.Add(buttonTable);
        topPanel.Controls.Add(_buttonPanel);

        _mainSplitContainer.Panel1.Controls.Add(topPanel);
    }

    private void InitializeBottomPanel()
    {
        var bottomPanel = new Panel { Dock = DockStyle.Fill };

        // Filter controls
        var filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(10)
        };

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };

        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

        _searchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 8,
            AccessibleName = "Search Customers",
            AccessibleDescription = "Search customers by name or account number"
        };
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;

        _statusFilterComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 9,
            AccessibleName = "Filter by Status",
            AccessibleDescription = "Filter bills by payment status"
        };
        _statusFilterComboBox.Items.AddRange(Enum.GetNames(typeof(BillStatus)));
        _statusFilterComboBox.SelectedIndexChanged += StatusFilterComboBox_SelectedIndexChanged;

        _overdueOnlyCheckBox = new CheckBox
        {
            Text = "Overdue Only",
            Dock = DockStyle.Fill,
            TabIndex = 10,
            AccessibleName = "Show Overdue Only",
            AccessibleDescription = "Show only overdue bills"
        };
        _overdueOnlyCheckBox.CheckedChanged += OverdueOnlyCheckBox_CheckedChanged;

        filterTable.Controls.Add(_searchTextBox, 0, 0);
        filterTable.Controls.Add(_statusFilterComboBox, 1, 0);
        filterTable.Controls.Add(_overdueOnlyCheckBox, 2, 0);

        filterPanel.Controls.Add(filterTable);
        bottomPanel.Controls.Add(filterPanel);

        // Customers grid
        var customersPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _customersGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = false,
            AllowResizingColumns = true,
            AllowSorting = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            TabIndex = 11,
            AccessibleName = "Customers Grid"
        };

        // Configure customer grid columns
        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountNumber",
            HeaderText = "Account Number"
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "DisplayName",
            HeaderText = "Customer Name"
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "ServiceAddress",
            HeaderText = "Service Address"
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "PhoneNumber",
            HeaderText = "Phone"
        });

        _customersGrid.CurrentCellActivated += CustomersGrid_CurrentCellActivated;
        customersPanel.Controls.Add(_customersGrid);
        bottomPanel.Controls.Add(customersPanel);

        _mainSplitContainer.Panel2.Controls.Add(bottomPanel);
    }

    private void SetTabOrder()
    {
        // Tab order is set in the control initialization above
        // Bills grid (1), buttons (2-7), search (8), status filter (9), overdue checkbox (10), customers grid (11)
    }

    private void BillsGrid_CurrentCellActivated(object? sender, EventArgs e)
    {
        // Handle grid selection if needed
        // TODO: Implement proper event args handling when Syncfusion types are available
    }

    private void CustomersGrid_CurrentCellActivated(object? sender, EventArgs e)
    {
        // Handle grid selection if needed
        // TODO: Implement proper event args handling when Syncfusion types are available
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        _viewModel.SearchText = _searchTextBox?.Text ?? string.Empty;
    }

    private void StatusFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_statusFilterComboBox?.SelectedItem is string statusString &&
            Enum.TryParse<BillStatus>(statusString, out var status))
        {
            _viewModel.FilterStatus = status;
        }
        else
        {
            _viewModel.FilterStatus = null;
        }
    }

    private void OverdueOnlyCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _viewModel.ShowOverdueOnly = _overdueOnlyCheckBox?.Checked ?? false;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.UtilityBills):
                if (_billsGrid != null) _billsGrid.DataSource = _viewModel.UtilityBills;
                break;

            case nameof(_viewModel.Customers):
                if (_customersGrid != null) _customersGrid.DataSource = _viewModel.Customers;
                break;

            case nameof(_viewModel.IsLoading):
                if (_loadingOverlay != null) _loadingOverlay.Visible = _viewModel.IsLoading;
                if (_noDataOverlay != null) _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.UtilityBills.Any();
                break;

            case nameof(_viewModel.StatusText):
                if (_statusLabel != null) _statusLabel.Text = _viewModel.StatusText;
                break;

            case nameof(_viewModel.TotalOutstanding):
                if (_totalOutstandingLabel != null)
                    _totalOutstandingLabel.Text = $"Total Outstanding: {_viewModel.TotalOutstanding:C}";
                break;

            case nameof(_viewModel.OverdueCount):
                if (_overdueCountLabel != null)
                    _overdueCountLabel.Text = $"Overdue Bills: {_viewModel.OverdueCount}";
                break;

            case nameof(_viewModel.TotalRevenue):
                if (_totalRevenueLabel != null)
                    _totalRevenueLabel.Text = $"Total Revenue: {_viewModel.TotalRevenue:C}";
                break;

            case nameof(_viewModel.BillsThisMonth):
                if (_billsThisMonthLabel != null)
                    _billsThisMonthLabel.Text = $"Bills This Month: {_viewModel.BillsThisMonth}";
                break;
        }
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing utility bill data");
            UpdateStatus("Loading utility bill data...");

            await _viewModel.LoadBillsCommand.ExecuteAsync(null);
            await _viewModel.LoadCustomersCommand.ExecuteAsync(null);

            UpdateStatus("Data refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing data");
            UpdateStatus($"Error: {ex.Message}");
            MessageBox.Show($"Error refreshing data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null) _statusLabel.Text = message;
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
            _logger.LogWarning(ex, "UtilityBillPanel: ClosePanel failed");
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            // Auto-load data on panel load
            Task.Run(async () => await RefreshDataAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading panel data");
        }
    }

    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer? components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel?.Dispose();

            _billsGrid?.Dispose();
            _customersGrid?.Dispose();
            _mainSplitContainer?.Dispose();
            _statusStrip?.Dispose();
            _panelHeader?.Dispose();
            _loadingOverlay?.Dispose();
            _noDataOverlay?.Dispose();

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.Name = "UtilityBillPanel";
        this.Size = new Size(1400, 900);
    }

    #endregion
}
