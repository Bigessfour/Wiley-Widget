using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for viewing and managing utility customers.
/// Provides customer search, add, edit, and management capabilities.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class CustomersPanel : UserControl
{
    private readonly CustomersViewModel _viewModel;
    private readonly ILogger<CustomersPanel> _logger;

    // UI Controls
    private PanelHeader? _panelHeader;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    private DataGridView? _customersGrid;
    private TextBox? _searchTextBox;
    private Button? _searchButton;
    private Button? _addCustomerButton;
    private Button? _refreshButton;
    private TableLayoutPanel? _mainLayout;

    public CustomersPanel(
        CustomersViewModel viewModel,
        ILogger<CustomersPanel> logger)
    {
        // InitializeComponent(); // Not needed for UserControl

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeControls();
        BindViewModel();

        _logger.LogDebug("CustomersPanel initialized");
    }

    private void InitializeControls()
    {
        Name = "CustomersPanel";
        Size = new Size(1200, 800);
        Dock = DockStyle.Fill;

        // Panel header
        _panelHeader = new PanelHeader { Dock = DockStyle.Top };
        _panelHeader.Title = "Customers";
        _panelHeader.RefreshClicked += async (s, e) => await RefreshCustomersAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main layout
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // Toolbar
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Status

        // Toolbar panel
        var toolbarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        // Search controls
        var searchLabel = new Label
        {
            Text = "Search:",
            Location = new Point(10, 15),
            Size = new Size(50, 25),
            TextAlign = ContentAlignment.MiddleLeft
        };
        toolbarPanel.Controls.Add(searchLabel);

        _searchTextBox = new TextBox
        {
            Location = new Point(65, 10),
            Size = new Size(200, 25)
        };
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;
        toolbarPanel.Controls.Add(_searchTextBox);

        _searchButton = new Button
        {
            Text = "&Search",
            Location = new Point(275, 10),
            Size = new Size(80, 30)
        };
        _searchButton.Click += async (s, e) => await SearchCustomersAsync();
        toolbarPanel.Controls.Add(_searchButton);

        // Add customer button
        _addCustomerButton = new Button
        {
            Text = "&Add Customer",
            Location = new Point(365, 10),
            Size = new Size(100, 30)
        };
        _addCustomerButton.Click += async (s, e) => await AddCustomerAsync();
        toolbarPanel.Controls.Add(_addCustomerButton);

        // Refresh button
        _refreshButton = new Button
        {
            Text = "&Refresh",
            Location = new Point(475, 10),
            Size = new Size(80, 30)
        };
        _refreshButton.Click += async (s, e) => await RefreshCustomersAsync();
        toolbarPanel.Controls.Add(_refreshButton);

        _mainLayout.Controls.Add(toolbarPanel, 0, 0);

        // Customers grid
        _customersGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        // Configure grid columns
        _customersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "CustomerId",
            HeaderText = "ID",
            DataPropertyName = "Id",
            Width = 80
        });
        _customersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "CustomerName",
            HeaderText = "Name",
            DataPropertyName = "DisplayName",
            Width = 200
        });
        _customersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "AccountNumber",
            HeaderText = "Account #",
            DataPropertyName = "AccountNumber",
            Width = 120
        });
        _customersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ServiceAddress",
            HeaderText = "Service Address",
            DataPropertyName = "ServiceAddress",
            Width = 250
        });
        _customersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "BillingAddress",
            HeaderText = "Billing Address",
            DataPropertyName = "BillingAddress",
            Width = 250
        });

        _customersGrid.SelectionChanged += CustomersGrid_SelectionChanged;
        _customersGrid.DoubleClick += CustomersGrid_DoubleClick;

        _mainLayout.Controls.Add(_customersGrid, 0, 1);

        // Status strip
        _statusStrip = new StatusStrip { Dock = DockStyle.Fill };
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);
        _mainLayout.Controls.Add(_statusStrip, 0, 2);

        Controls.Add(_mainLayout);

        // Loading overlay
        _loadingOverlay = new LoadingOverlay { Message = "Loading customers..." };
        Controls.Add(_loadingOverlay);

        // No data overlay
        _noDataOverlay = new NoDataOverlay { Message = "No customers found" };
        Controls.Add(_noDataOverlay);

        // Wire up ViewModel events
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Load initial data
        _ = LoadCustomersAsync();
    }

    private void BindViewModel()
    {
        if (_customersGrid != null)
        {
            _customersGrid.DataSource = _viewModel.Customers;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.IsLoading):
                if (_loadingOverlay != null)
                    _loadingOverlay.Visible = _viewModel.IsLoading;
                if (_noDataOverlay != null)
                    _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.Customers.Any();
                break;
            case nameof(_viewModel.ErrorMessage):
                UpdateStatus(_viewModel.ErrorMessage ?? "Ready");
                break;
        }
    }

    private async Task LoadCustomersAsync()
    {
        try
        {
            UpdateStatus("Loading customers...");
            await _viewModel.LoadCustomersCommand.ExecuteAsync(null);
            UpdateStatus($"Loaded {_viewModel.Customers.Count} customers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load customers");
            UpdateStatus($"Failed to load customers: {ex.Message}");
        }
    }

    private async Task SearchCustomersAsync()
    {
        try
        {
            if (_searchTextBox?.Text != _viewModel.SearchText)
            {
                _viewModel.SearchText = _searchTextBox?.Text;
            }
            UpdateStatus("Searching customers...");
            await _viewModel.SearchCommand.ExecuteAsync(null);
            UpdateStatus($"Found {_viewModel.Customers.Count} customers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search customers");
            UpdateStatus($"Search failed: {ex.Message}");
        }
    }

    private async Task AddCustomerAsync()
    {
        try
        {
            UpdateStatus("Adding new customer...");
            await _viewModel.AddCustomerCommand.ExecuteAsync(null);
            UpdateStatus("Customer added successfully");
            await LoadCustomersAsync(); // Refresh the list
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add customer");
            UpdateStatus($"Failed to add customer: {ex.Message}");
        }
    }

    private async Task RefreshCustomersAsync()
    {
        await LoadCustomersAsync();
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        // Debounce search - could implement timer here for auto-search
    }

    private void CustomersGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_customersGrid?.SelectedRows.Count > 0)
        {
            var selectedRow = _customersGrid.SelectedRows[0];
            if (selectedRow.DataBoundItem is WileyWidget.Models.UtilityCustomer customer)
            {
                _viewModel.SelectedCustomer = customer;
                UpdateStatus($"Selected: {customer.DisplayName}");
            }
        }
    }

    private void CustomersGrid_DoubleClick(object? sender, EventArgs e)
    {
        // Could open customer details dialog here
        _logger.LogDebug("Customer double-clicked - details dialog not implemented yet");
    }

    private void ClosePanel()
    {
        // Find the parent docking manager and hide this panel
        var dockingManager = FindDockingManager(this);
        if (dockingManager != null)
        {
            dockingManager.SetDockVisibility(this, false);
        }
    }

    private Syncfusion.Windows.Forms.Tools.DockingManager? FindDockingManager(Control? control)
    {
        while (control != null)
        {
            if (control is Form form)
            {
                // Try to find DockingManager in the form's components or controls
                return form.Controls.OfType<Syncfusion.Windows.Forms.Tools.DockingManager>().FirstOrDefault();
            }
            control = control.Parent;
        }
        return null;
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
        }
    }
}
