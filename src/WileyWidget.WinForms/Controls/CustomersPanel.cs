using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Syncfusion.Drawing;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.ListView.Enums;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.Dialogs;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for viewing and managing utility customers.
/// Provides customer search, add, edit, delete, and QuickBooks synchronization capabilities.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class CustomersPanel : UserControl
{
    private readonly CustomersViewModel _viewModel;
    private readonly ILogger<CustomersPanel> _logger;

    #region UI Controls

    private PanelHeader? _panelHeader;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private ToolStripStatusLabel? _countLabel;
    private ToolStripStatusLabel? _balanceLabel;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    private SfDataGrid? _customersGrid;

    // Toolbar controls
    private TableLayoutPanel? _mainLayout;
    private GradientPanelExt? _toolbarPanel;
    private TextBoxExt? _searchTextBox;
    private SfButton? _searchButton;
    private SfButton? _clearFiltersButton;
    private SfButton? _addCustomerButton;
    private SfButton? _editCustomerButton;
    private SfButton? _deleteCustomerButton;
    private SfButton? _refreshButton;
    private SfButton? _syncQuickBooksButton;
    private SfButton? _exportButton;

    // Filter controls
    private SfComboBox? _filterTypeComboBox;
    private SfComboBox? _filterLocationComboBox;
    private CheckBoxAdv? _showActiveOnlyCheckBox;

    // Summary panel
    private GradientPanelExt? _summaryPanel;
    private Label? _totalCustomersLabel;
    private Label? _activeCustomersLabel;
    private Label? _balanceSummaryLabel;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomersPanel"/> class.
    /// </summary>
    /// <param name="viewModel">The customers view model.</param>
    /// <param name="logger">Logger instance.</param>
    public CustomersPanel(
        CustomersViewModel viewModel,
        ILogger<CustomersPanel> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeControls();
        BindViewModel();
        ApplySyncfusionTheme();

        _logger.LogDebug("CustomersPanel initialized");
    }

    /// <summary>
    /// Applies Syncfusion Office2019Colorful theme to all controls.
    /// </summary>
    private void ApplySyncfusionTheme()
    {
        try
        {
            // Apply theme to Syncfusion controls
            if (_customersGrid != null)
            {
                // Header styling
                _customersGrid.Style.HeaderStyle.Font.Bold = true;
                _customersGrid.Style.HeaderStyle.Font.Size = 9.5f;

                // Selection styling

                // Cell styling
                _customersGrid.Style.CellStyle.Font.Size = 9f;

                // Add alternate row coloring via QueryCellStyle event
                _customersGrid.QueryCellStyle += (s, e) =>
                {
                    if (e.Column != null)
                    {
                        // Alternating row styling removed - let SkinManager handle theming
                    }
                };

                _logger.LogDebug("Syncfusion theme applied successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply Syncfusion theme");
        }
    }

    #region Control Initialization

    /// <summary>
    /// Initializes all UI controls and layout.
    /// </summary>
    private void InitializeControls()
    {
        SuspendLayout();

        Name = "CustomersPanel";
        Dock = DockStyle.Fill;
        AutoSize = false;

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = 50
        };
        _panelHeader.Title = "Customers Management";
        _panelHeader.RefreshClicked += async (s, e) => await RefreshCustomersAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main layout container
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1
            // BackColor removed - let SkinManager handle theming
        };
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));  // Toolbar - increased for 2 button rows
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Summary
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Grid
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Status bar

        // Create toolbar
        CreateToolbar();
        _mainLayout.Controls.Add(_toolbarPanel!, 0, 0);

        // Create summary panel
        CreateSummaryPanel();
        _mainLayout.Controls.Add(_summaryPanel!, 0, 1);

        // Create data grid
        CreateDataGrid();
        _mainLayout.Controls.Add(_customersGrid!, 0, 2);

        // Create status strip
        CreateStatusStrip();
        _mainLayout.Controls.Add(_statusStrip!, 0, 3);

        Controls.Add(_mainLayout);

        // Create overlays
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading customers...",
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No customers found. Click 'Add Customer' to create one.",
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>
    /// Creates the toolbar with search and action buttons.
    /// </summary>
    private void CreateToolbar()
    {
        _toolbarPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 10, 5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_toolbarPanel, "Office2019Colorful");

        // Main container for two rows
        var toolbarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            AutoSize = true
        };
        toolbarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // First row: Search and filters
        var searchFilterRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 5)
        };

        // Search label
        var searchLabel = new Label
        {
            Text = "Search:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 8, 5, 0)
        };
        searchFilterRow.Controls.Add(searchLabel);

        // Search textbox
        _searchTextBox = new TextBoxExt
        {
            Width = 220,
            PlaceholderText = "Name, account #, or address...",
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 5, 5, 0)
        };
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;
        _searchTextBox.KeyPress += SearchTextBox_KeyPress;
        searchFilterRow.Controls.Add(_searchTextBox);

        // Search button
        _searchButton = new SfButton
        {
            Text = "🔍 &Search",
            AutoSize = true,
            Margin = new Padding(0, 3, 5, 0)
        };
        _searchButton.Click += async (s, e) => await SearchCustomersAsync();
        searchFilterRow.Controls.Add(_searchButton);

        // Clear filters button
        _clearFiltersButton = new SfButton
        {
            Text = "Clear",
            AutoSize = true,
            Margin = new Padding(0, 3, 10, 0)
        };
        _clearFiltersButton.Click += (s, e) => _viewModel.ClearFiltersCommand.Execute(null);
        searchFilterRow.Controls.Add(_clearFiltersButton);

        // Separator
        var separator1 = new Label
        {
            Text = "|",
            AutoSize = true,
            Margin = new Padding(5, 5, 5, 0)
        };
        searchFilterRow.Controls.Add(separator1);

        // Filter: Type
        var typeLabel = new Label
        {
            Text = "Type:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 8, 5, 0)
        };
        searchFilterRow.Controls.Add(typeLabel);

        _filterTypeComboBox = new SfComboBox
        {
            Width = 120,
            DropDownStyle = DropDownStyle.DropDownList,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            DataSource = new List<string> { "All Types", "Residential", "Commercial", "Industrial" },
            Margin = new Padding(0, 5, 5, 0)
        };
        _filterTypeComboBox.SelectedIndex = 0;
        _filterTypeComboBox.SelectedIndexChanged += FilterComboBox_SelectedIndexChanged;
        searchFilterRow.Controls.Add(_filterTypeComboBox);

        // Filter: Location
        var locationLabel = new Label
        {
            Text = "Location:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 8, 5, 0)
        };
        searchFilterRow.Controls.Add(locationLabel);

        _filterLocationComboBox = new SfComboBox
        {
            Width = 140,
            DropDownStyle = DropDownStyle.DropDownList,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            DataSource = new List<string> { "All Locations", "Inside City Limits", "Outside City Limits" },
            Margin = new Padding(0, 5, 10, 0)
        };
        _filterLocationComboBox.SelectedIndex = 0;
        _filterLocationComboBox.SelectedIndexChanged += FilterComboBox_SelectedIndexChanged;
        searchFilterRow.Controls.Add(_filterLocationComboBox);

        // Show Active Only checkbox
        _showActiveOnlyCheckBox = new CheckBoxAdv
        {
            Text = "Active Only",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(0, 7, 0, 0)
        };
        _showActiveOnlyCheckBox.CheckedChanged += (s, e) =>
            _viewModel.ShowActiveOnly = _showActiveOnlyCheckBox.Checked;
        searchFilterRow.Controls.Add(_showActiveOnlyCheckBox);

        toolbarLayout.Controls.Add(searchFilterRow, 0, 0);

        // Second row: Action buttons
        var actionButtonsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 5)
        };

        // Add Customer button
        _addCustomerButton = new SfButton
        {
            Text = "➕ &Add Customer",
            AutoSize = true,
            Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
            Margin = new Padding(0, 0, 5, 0)
        };
        _addCustomerButton.Click += async (s, e) => await AddCustomerAsync();
        actionButtonsRow.Controls.Add(_addCustomerButton);

        // Edit Customer button
        _editCustomerButton = new SfButton
        {
            Text = "✏️ &Edit",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(0, 0, 5, 0)
        };
        _editCustomerButton.Click += (s, e) => EditSelectedCustomer();
        actionButtonsRow.Controls.Add(_editCustomerButton);

        // Delete Customer button
        _deleteCustomerButton = new SfButton
        {
            Text = "🗑️ &Delete",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(0, 0, 10, 0)
        };
        _deleteCustomerButton.Click += async (s, e) => await DeleteSelectedCustomerAsync();
        actionButtonsRow.Controls.Add(_deleteCustomerButton);

        // Refresh button
        _refreshButton = new SfButton
        {
            Text = "🔄 &Refresh",
            AutoSize = true,
            Margin = new Padding(0, 0, 5, 0)
        };
        _refreshButton.Click += async (s, e) => await RefreshCustomersAsync();
        actionButtonsRow.Controls.Add(_refreshButton);

        // Sync QuickBooks button
        _syncQuickBooksButton = new SfButton
        {
            Text = "📊 Sync QB",
            AutoSize = true,
            Margin = new Padding(0, 0, 5, 0)
        };
        _syncQuickBooksButton.Click += async (s, e) => await SyncWithQuickBooksAsync();
        actionButtonsRow.Controls.Add(_syncQuickBooksButton);

        // Export button
        _exportButton = new SfButton
        {
            Text = "💾 E&xport",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0)
        };
        _exportButton.Click += async (s, e) => await ExportCustomersAsync();
        actionButtonsRow.Controls.Add(_exportButton);

        toolbarLayout.Controls.Add(actionButtonsRow, 0, 1);

        _toolbarPanel.Controls.Add(toolbarLayout);
    }

    /// <summary>
    /// Creates the summary panel with customer statistics.
    /// </summary>
    private void CreateSummaryPanel()
    {
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 10, 5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, "Office2019Colorful");

        var summaryLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true
        };

        // Total Customers card
        _totalCustomersLabel = CreateSummaryLabel("Total: 0");
        summaryLayout.Controls.Add(_totalCustomersLabel);

        // Active Customers card
        _activeCustomersLabel = CreateSummaryLabel("Active: 0");
        summaryLayout.Controls.Add(_activeCustomersLabel);

        // Balance Summary card
        _balanceSummaryLabel = CreateSummaryLabel("Balance: $0.00");
        summaryLayout.Controls.Add(_balanceSummaryLabel);

        _summaryPanel.Controls.Add(summaryLayout);
    }

    /// <summary>
    /// Creates a styled summary label inside a GradientPanelExt.
    /// </summary>
    private Label CreateSummaryLabel(string text)
    {
        var cardPanel = new GradientPanelExt
        {
            Width = 180,
            Height = 40,
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            Margin = new Padding(5),
            AutoSize = false
        };
        SfSkinManager.SetVisualStyle(cardPanel, "Office2019Colorful");

        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
        };
        cardPanel.Controls.Add(label);

        return label;
    }

    /// <summary>
    /// Creates and configures the Syncfusion data grid.
    /// </summary>
    private void CreateDataGrid()
    {
        _customersGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            AllowFiltering = true,
            AllowSorting = true,
            AllowResizingColumns = true,
            SelectionMode = GridSelectionMode.Single,
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
            ShowRowHeader = true,
            RowHeight = 32,
            // AutoSizeColumnsMode for better column management
            AutoSizeColumnsMode = AutoSizeColumnsMode.None
        };

        // Apply professional styling
        ConfigureGridStyling(_customersGrid);

        // Configure grid columns
        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.AccountNumber),
            HeaderText = "Account #",
            Width = 110,
            AllowSorting = true,
            AllowFiltering = true
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.DisplayName),
            HeaderText = "Customer Name",
            Width = 250,
            AllowSorting = true,
            AllowFiltering = true
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.CustomerTypeDescription),
            HeaderText = "Type",
            Width = 120,
            AllowSorting = true,
            AllowFiltering = true
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.ServiceAddress),
            HeaderText = "Service Address",
            Width = 280,
            AllowSorting = true,
            AllowFiltering = true
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.ServiceCity),
            HeaderText = "City",
            Width = 120,
            AllowSorting = true
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.ServiceLocationDescription),
            HeaderText = "Location",
            Width = 150,
            AllowSorting = true,
            AllowFiltering = true
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.PhoneNumber),
            HeaderText = "Phone",
            Width = 130
        });

        _customersGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(UtilityCustomer.CurrentBalance),
            HeaderText = "Balance",
            Width = 120,
            Format = "C2",
            AllowSorting = true,
            AllowFiltering = true
        });

        _customersGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(UtilityCustomer.StatusDescription),
            HeaderText = "Status",
            Width = 100,
            AllowSorting = true,
            AllowFiltering = true
        });

        // Wire up events
        _customersGrid.SelectionChanged += CustomersGrid_SelectionChanged;
        _customersGrid.CellDoubleClick += CustomersGrid_CellDoubleClick;
    }

    /// <summary>
    /// Creates the status strip.
    /// </summary>
    private void CreateStatusStrip()
    {
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Fill
            // BackColor removed - let SkinManager handle theming
        };

        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip.Items.Add(_statusLabel);

        _countLabel = new ToolStripStatusLabel
        {
            Text = "0 customers",
            BorderSides = ToolStripStatusLabelBorderSides.Left,
            BorderStyle = Border3DStyle.Etched
        };
        _statusStrip.Items.Add(_countLabel);

        _balanceLabel = new ToolStripStatusLabel
        {
            Text = "Total Balance: $0.00",
            BorderSides = ToolStripStatusLabelBorderSides.Left,
            BorderStyle = Border3DStyle.Etched
        };
        _statusStrip.Items.Add(_balanceLabel);
    }

    #endregion

    #region View Model Binding

    /// <summary>
    /// Binds the view model to UI controls.
    /// </summary>
    private void BindViewModel()
    {
        // Bind grid to filtered customers
        if (_customersGrid != null)
        {
            _customersGrid.DataSource = _viewModel.FilteredCustomers;
        }

        // Subscribe to property changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Initial load
        _ = LoadCustomersAsync();
    }

    /// <summary>
    /// Configures professional styling for the data grid including header font and row appearance.
    /// </summary>
    /// <param name="grid">The SfDataGrid to style.</param>
    private void ConfigureGridStyling(SfDataGrid grid)
    {
        try
        {
            // Header styling - bold, professional appearance
            grid.Style.HeaderStyle.Font = new GridFontInfo(new Font("Segoe UI", 11f, FontStyle.Bold));
            // Note: GridBorder constructor signature may have changed in current Syncfusion version
            // grid.Style.HeaderStyle.Borders.All = new GridBorder(GridBorderStyle.Solid, 1);

            // Row styling - consistent font and sizing
            // Note: RowStyle properties may not be available in current Syncfusion version
            // grid.Style.RowStyle.Font = new GridFontInfo(new Font("Segoe UI", 10f, FontStyle.Regular));
            // grid.Style.RowStyle.TextAlignment = HorizontalAlignment.Left;

            // Alternate row coloring for readability
            // Note: AlternatingRowStyle may not be available in current Syncfusion version
            // grid.Style.AlternatingRowStyle.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);

            // Selection styling
            // Note: SelectedRowStyle may not be available in current Syncfusion version
            // grid.Style.SelectedRowStyle.Font = new GridFontInfo(new Font("Segoe UI", 10f, FontStyle.Regular));

            // Footer styling for totals if needed
            // Note: GroupCaptionStyle may not be available in current Syncfusion version
            // grid.Style.GroupCaptionStyle.Font = new GridFontInfo(new Font("Segoe UI", 10f, FontStyle.Bold));

            _logger?.LogDebug("Grid styling applied successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Failed to apply grid styling: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates and configures toolbar button text with consistent font sizing.
    /// </summary>
    private void ConfigureToolbarFonts()
    {
        try
        {
            var buttonFont = new Font("Segoe UI", 10f, FontStyle.Regular);

            foreach (var control in new[] { _addCustomerButton, _editCustomerButton, _deleteCustomerButton,
                                           _refreshButton, _exportButton, _syncQuickBooksButton,
                                           _clearFiltersButton, _searchButton })
            {
                if (control is Control c)
                    c.Font = buttonFont;
            }

            // Labels use slightly larger font for hierarchy
            var labelFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            if (_totalCustomersLabel != null) _totalCustomersLabel.Font = labelFont;
            if (_activeCustomersLabel != null) _activeCustomersLabel.Font = labelFont;
            if (_balanceSummaryLabel != null) _balanceSummaryLabel.Font = labelFont;

            // Status bar uses smaller font
            var statusFont = new Font("Segoe UI", 9f, FontStyle.Regular);
            if (_statusLabel != null) _statusLabel.Font = statusFont;
            if (_countLabel != null) _countLabel.Font = statusFont;
            if (_balanceLabel != null) _balanceLabel.Font = statusFont;

            _logger?.LogDebug("Toolbar fonts configured");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Failed to configure toolbar fonts: {ex.Message}");
        }
    }

    /// <summary>

    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => ViewModel_PropertyChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(_viewModel.IsLoading):
                UpdateLoadingState();
                break;

            case nameof(_viewModel.StatusText):
                UpdateStatus(_viewModel.StatusText ?? "Ready");
                break;

            case nameof(_viewModel.ErrorMessage):
                if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                {
                    MessageBox.Show(_viewModel.ErrorMessage, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                break;

            case nameof(_viewModel.TotalCustomers):
            case nameof(_viewModel.ActiveCustomers):
            case nameof(_viewModel.TotalOutstandingBalance):
                UpdateSummaryDisplay();
                break;

            case nameof(_viewModel.FilteredCustomers):
                UpdateNoDataOverlay();
                break;

            case nameof(_viewModel.SelectedCustomer):
                UpdateButtonStates();
                break;
        }
    }

    /// <summary>
    /// Updates the loading state and overlay visibility.
    /// </summary>
    private void UpdateLoadingState()
    {
        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = _viewModel.IsLoading;
        }

        UpdateNoDataOverlay();
    }

    /// <summary>
    /// Updates the no data overlay visibility.
    /// </summary>
    private void UpdateNoDataOverlay()
    {
        if (_noDataOverlay != null)
        {
            bool hasNoData = !_viewModel.IsLoading && _viewModel.FilteredCustomers.Count == 0;
            _noDataOverlay.Visible = hasNoData;

            if (hasNoData)
            {
                // Show action button when there's no data
                _noDataOverlay.ShowActionButton("➕ Add Customer", async (s, e) => await AddCustomerAsync());
            }
            else
            {
                _noDataOverlay.HideActionButton();
            }
        }
    }

    /// <summary>
    /// Updates the summary display labels.
    /// </summary>
    private void UpdateSummaryDisplay()
    {
        if (_totalCustomersLabel != null)
        {
            _totalCustomersLabel.Text = $"Total: {_viewModel.TotalCustomers}";
        }

        if (_activeCustomersLabel != null)
        {
            _activeCustomersLabel.Text = $"Active: {_viewModel.ActiveCustomers}";
        }

        if (_balanceSummaryLabel != null)
        {
            _balanceSummaryLabel.Text = $"Balance: {_viewModel.TotalOutstandingBalance:C2}";
        }

        if (_countLabel != null)
        {
            var displayCount = _viewModel.FilteredCustomers.Count;
            _countLabel.Text = $"{displayCount} customer{(displayCount != 1 ? "s" : "")}";
        }

        if (_balanceLabel != null)
        {
            _balanceLabel.Text = $"Total Balance: {_viewModel.TotalOutstandingBalance:C2}";
        }
    }

    /// <summary>
    /// Updates button enabled states based on selection.
    /// </summary>
    private void UpdateButtonStates()
    {
        var hasSelection = _viewModel.SelectedCustomer != null;

        if (_editCustomerButton != null)
            _editCustomerButton.Enabled = hasSelection;

        if (_deleteCustomerButton != null)
            _deleteCustomerButton.Enabled = hasSelection && _viewModel.SelectedCustomer?.Id > 0;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles search textbox text changes (debounced).
    /// </summary>
    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        // Update viewmodel property
        _viewModel.SearchText = _searchTextBox?.Text;
    }

    /// <summary>
    /// Handles Enter key press in search textbox.
    /// </summary>
    private void SearchTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
        {
            e.Handled = true;
            _ = SearchCustomersAsync();
        }
    }

    /// <summary>
    /// Handles filter combo box selection changes.
    /// </summary>
    private void FilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Update customer type filter
        if (sender == _filterTypeComboBox && _filterTypeComboBox != null)
        {
            _viewModel.FilterCustomerType = _filterTypeComboBox.SelectedIndex switch
            {
                1 => CustomerType.Residential,
                2 => CustomerType.Commercial,
                3 => CustomerType.Industrial,
                _ => null
            };
        }

        // Update location filter
        if (sender == _filterLocationComboBox && _filterLocationComboBox != null)
        {
            _viewModel.FilterServiceLocation = _filterLocationComboBox.SelectedIndex switch
            {
                1 => ServiceLocation.InsideCityLimits,
                2 => ServiceLocation.OutsideCityLimits,
                _ => null
            };
        }
    }

    /// <summary>
    /// Handles grid selection changes.
    /// </summary>
    private void CustomersGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_customersGrid?.SelectedItem is UtilityCustomer customer)
        {
            _viewModel.SelectedCustomer = customer;
            _logger.LogDebug("Selected customer: {Account} - {Name}",
                customer.AccountNumber, customer.DisplayName);
        }
    }

    /// <summary>
    /// Handles grid cell double-click to edit customer.
    /// </summary>
    private void CustomersGrid_CellDoubleClick(object? sender, Syncfusion.WinForms.DataGrid.Events.CellClickEventArgs e)
    {
        if (e.DataRow != null && _viewModel.SelectedCustomer != null)
        {
            EditSelectedCustomer();
        }
    }

    #endregion

    #region Command Implementations

    /// <summary>
    /// Loads customers from the repository.
    /// </summary>
    private async Task LoadCustomersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Loading customers");
            await _viewModel.LoadCustomersCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load customers");
            MessageBox.Show($"Failed to load customers: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Refreshes the customer list.
    /// </summary>
    private async Task RefreshCustomersAsync(CancellationToken cancellationToken = default)
    {
        await LoadCustomersAsync();
    }

    /// <summary>
    /// Searches customers based on current search text.
    /// </summary>
    private async Task SearchCustomersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Searching customers");
            await _viewModel.SearchCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            MessageBox.Show($"Search failed: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Adds a new customer.
    /// </summary>
    private async Task AddCustomerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Adding new customer");
            await _viewModel.AddCustomerCommand.ExecuteAsync(null);

            // Open edit dialog for the new customer
            if (_viewModel.SelectedCustomer != null)
            {
                EditSelectedCustomer();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add customer");
            MessageBox.Show($"Failed to add customer: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Edits the selected customer in a dialog.
    /// </summary>
    private async void EditSelectedCustomer()
    {
        if (_viewModel.SelectedCustomer == null) return;

        try
        {
            _logger.LogDebug("Editing customer {Account}", _viewModel.SelectedCustomer.AccountNumber);

            using var dialog = new CustomerEditDialog(_viewModel.SelectedCustomer, _logger);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Save the changes to the database
                await _viewModel.SaveCustomerAsync(_viewModel.SelectedCustomer);
                _logger.LogInformation("Customer {Account} updated successfully", _viewModel.SelectedCustomer.AccountNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit customer");
            MessageBox.Show($"Failed to edit customer: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Deletes the selected customer after confirmation.
    /// </summary>
    private async Task DeleteSelectedCustomerAsync(CancellationToken cancellationToken = default)
    {
        if (_viewModel.SelectedCustomer == null) return;

        try
        {
            var customer = _viewModel.SelectedCustomer;
            var result = MessageBox.Show(
                $"Are you sure you want to delete customer:\n\n{customer.DisplayName}\nAccount: {customer.AccountNumber}?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _logger.LogInformation("Deleting customer {Id} - {Account}",
                    customer.Id, customer.AccountNumber);

                var success = await _viewModel.DeleteCustomerAsync(customer.Id);

                if (success)
                {
                    MessageBox.Show("Customer deleted successfully", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete customer");
            MessageBox.Show($"Failed to delete customer: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Synchronizes customers with QuickBooks.
    /// </summary>
    private async Task SyncWithQuickBooksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = MessageBox.Show(
                "Synchronize customer data with QuickBooks?\n\nThis may take several minutes.",
                "QuickBooks Sync",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _logger.LogInformation("Starting QuickBooks sync");
                await _viewModel.SyncWithQuickBooksCommand.ExecuteAsync(null);

                MessageBox.Show(
                    _viewModel.SyncStatusMessage ?? "Sync completed successfully",
                    "QuickBooks Sync",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QuickBooks sync failed");
            MessageBox.Show($"QuickBooks sync failed: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Exports customers to CSV file.
    /// </summary>
    private async Task ExportCustomersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"Customers_{DateTime.Now:yyyyMMdd}.csv",
                Title = "Export Customers to CSV"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _logger.LogInformation("Exporting customers to {File}", dialog.FileName);
                await _viewModel.ExportToCsvCommand.ExecuteAsync(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            MessageBox.Show($"Export failed: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Closes this panel.
    /// </summary>
    private void ClosePanel()
    {
        _logger.LogDebug("Closing CustomersPanel");

        // Find the parent docking manager and hide this panel
        var dockingManager = FindDockingManager(this);
        if (dockingManager != null)
        {
            dockingManager.SetDockVisibility(this, false);
        }
        else
        {
            // Fallback: just hide the control
            Visible = false;
        }
    }

    /// <summary>
    /// Finds the DockingManager in the control hierarchy.
    /// </summary>
    private Syncfusion.Windows.Forms.Tools.DockingManager? FindDockingManager(Control control)
    {
        while (control?.Parent != null)
        {
            control = control.Parent;

            if (control is Form form)
            {
                return form.Controls
                    .OfType<Syncfusion.Windows.Forms.Tools.DockingManager>()
                    .FirstOrDefault();
            }
        }
        return null;
    }

    /// <summary>
    /// Updates the status bar message.
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
        }
        _logger.LogDebug("Status: {Message}", message);
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Handles panel load event.
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (!DesignMode)
        {
            ConfigureToolbarFonts();

            // Defer sizing validation until layout is complete
            this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));

            _ = LoadCustomersAsync();
        }
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _customersGrid?.Dispose();
            _mainLayout?.Dispose();
            _toolbarPanel?.Dispose();
            _summaryPanel?.Dispose();
            _statusStrip?.Dispose();
            _loadingOverlay?.Dispose();
            _noDataOverlay?.Dispose();
            _panelHeader?.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion
}
