using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls.Base;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
using WileyWidget.WinForms.Controls.Supporting;
using System.Collections.Generic;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using System.Windows.Forms;









using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Dialogs;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Panel for viewing and managing utility customers with DI-scoped lifecycle.
/// Provides customer search, add, edit, delete, and QuickBooks synchronization capabilities.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class CustomersPanel : ScopedPanelBase
{
    // Strongly-typed ViewModel (this is what you use in your code)
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new CustomersViewModel? ViewModel
    {
        get => (CustomersViewModel?)base.ViewModel;
        set => base.ViewModel = value;
    }

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
    private LegacyGradientPanel? _toolbarPanel;
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
    private LegacyGradientPanel? _summaryPanel;
    private Label? _totalCustomersLabel;
    private Label? _activeCustomersLabel;
    private Label? _balanceSummaryLabel;

    // Event handler backing fields
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;
    private EventHandler? _searchButtonClickHandler;
    private EventHandler? _clearFiltersClickHandler;
    private EventHandler? _showActiveOnlyChangedHandler;
    private EventHandler? _addCustomerClickHandler;
    private EventHandler? _editCustomerClickHandler;
    private EventHandler? _deleteCustomerClickHandler;
    private EventHandler? _refreshClickHandler;
    private EventHandler? _syncQuickBooksClickHandler;
    private EventHandler? _exportClickHandler;
    private EventHandler? _noDataOverlayActionHandler;

    private ErrorProvider? _errorProvider;
    private ToolTip? _toolTip;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomersPanel"/> class with DI scope factory.
    /// </summary>
    /// <param name="scopeFactory">Factory for creating DI scopes.</param>
    /// <param name="logger">Logger instance.</param>
    public CustomersPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase> logger)
        : base(scopeFactory, logger)
    {
        // NOTE: InitializeControls() moved to OnViewModelResolved()
        ApplySyncfusionTheme();

        _logger?.LogDebug("CustomersPanel initialized");
    }

    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is CustomersViewModel)
        {
            InitializeControls();
            WireupToolbarEventHandlers();
        }
    }

    /// <summary>
    /// Wires up toolbar event handlers that depend on the ViewModel.
    /// Must be called after ViewModel is resolved.
    /// </summary>
    private void WireupToolbarEventHandlers()
    {
        if (_clearFiltersButton != null)
        {
            _clearFiltersClickHandler = (s, e) => ViewModel?.ClearFiltersCommand.Execute(null);
            _clearFiltersButton.Click += _clearFiltersClickHandler;
        }

        if (_showActiveOnlyCheckBox != null)
        {
            _showActiveOnlyChangedHandler = (s, e) =>
            {
                if (ViewModel != null)
                    ViewModel.ShowActiveOnly = _showActiveOnlyCheckBox.Checked;
            };
            _showActiveOnlyCheckBox.CheckedChanged += _showActiveOnlyChangedHandler;
        }
    }

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        if (ViewModel != null)
        {
            await ViewModel.LoadAsync(ct);
        }
        _isLoaded = true;
    }

    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        // Clear any existing panel-level validation errors
        ClearValidationErrors();

        if (ViewModel != null)
        {
            var result = await ViewModel.ValidateAsync(ct);
            if (result.IsValid)
            {
                return ValidationResult.Success;
            }

            // Propagate ViewModel validation items to the panel state for UI consumption
            SetValidationErrors(result.Errors);
            return ValidationResult.Failed(result.Errors.ToArray());
        }

        return ValidationResult.Success;
    }

    public override async Task<ValidationResult> SaveAsync(CancellationToken ct = default)
    {
        if (ViewModel != null)
        {
            var result = await ViewModel.SaveAsync(ct);
            if (result.IsValid)
            {
                SetHasUnsavedChanges(false);
                ClearValidationErrors();
                return ValidationResult.Success;
            }

            SetValidationErrors(result.Errors);
            return ValidationResult.Failed(result.Errors.ToArray());
        }
        return ValidationResult.Success;
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
                        // Alternating row styling removed - let SFSkinManager handle theming
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

        // Initialize UI helpers
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
            ContainerControl = this
        };

        _toolTip = new ToolTip();

        // Panel header
        _panelHeader = new PanelHeader
        {
            Dock = DockStyle.Top,
            Height = 50
        };
        _panelHeader.Title = "Customers Management";
        _panelHeaderRefreshHandler = (s, e) => { _ = RefreshCustomersAsync(); };
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Main layout container
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1
            // BackColor removed - let SFSkinManager handle theming
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
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = new NoDataOverlay
        {
            Message = "No customers found. Click 'Add Customer' to create one.",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();

        ResumeLayout(false);
        this.PerformLayout();
        this.Refresh();

        _logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    /// <summary>
    /// Creates the toolbar with search and action buttons.
    /// </summary>
    private void CreateToolbar()
    {
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        _toolbarPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 10, 5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_toolbarPanel, currentTheme);

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
        _searchTextBox.AccessibleName = "Customer search";
        _searchTextBox.TabIndex = 10;
        _toolTip?.SetToolTip(_searchTextBox, "Search customers by name, account number, or address.");
        searchFilterRow.Controls.Add(_searchTextBox);

        // Search button
        _searchButton = new SfButton
        {
            Text = "🔍 &Search",
            AutoSize = false,
            Size = new Size(95, 32),
            Margin = new Padding(0, 3, 5, 0)
        };
        SfSkinManager.SetVisualStyle(_searchButton, currentTheme);
        _searchButton.AccessibleName = "Search Button";
        _searchButton.TabIndex = 11;
        _toolTip?.SetToolTip(_searchButton, "Execute search");
        _searchButtonClickHandler = (s, e) => { _ = SearchCustomersAsync(); };
        _searchButton.Click += _searchButtonClickHandler;
        searchFilterRow.Controls.Add(_searchButton);

        // Clear filters button
        _clearFiltersButton = new SfButton
        {
            Text = "Clear",
            AutoSize = true,
            Margin = new Padding(0, 3, 10, 0)
        };
        SfSkinManager.SetVisualStyle(_clearFiltersButton, currentTheme);
        _clearFiltersButton.AccessibleName = "Clear Filters";
        _clearFiltersButton.TabIndex = 12;
        _toolTip?.SetToolTip(_clearFiltersButton, "Clear all filters");
        // NOTE: Event handler will be wired up in WireupToolbarEventHandlers() after ViewModel is resolved
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
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            DataSource = new List<string> { "All Types", "Residential", "Commercial", "Industrial" },
            Margin = new Padding(0, 5, 5, 0)
        };
        _filterTypeComboBox.SelectedIndex = 0;
        SfSkinManager.SetVisualStyle(_filterTypeComboBox, currentTheme);
        _filterTypeComboBox.AccessibleName = "Filter by Type";
        _filterTypeComboBox.TabIndex = 13;
        _toolTip?.SetToolTip(_filterTypeComboBox, "Filter customers by type");
        _filterTypeComboBox.SelectedIndexChanged += FilterComboBox_SelectedIndexChanged;
        searchFilterRow.Controls.Add(_filterTypeComboBox);

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
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            DataSource = new List<string> { "All Locations", "Inside City Limits", "Outside City Limits" },
            Margin = new Padding(0, 5, 10, 0)
        };
        _filterLocationComboBox.SelectedIndex = 0;
        SfSkinManager.SetVisualStyle(_filterLocationComboBox, currentTheme);
        _filterLocationComboBox.AccessibleName = "Filter by Location";
        _filterLocationComboBox.TabIndex = 14;
        _toolTip?.SetToolTip(_filterLocationComboBox, "Filter customers by service location");
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
        _showActiveOnlyCheckBox.AccessibleName = "Show active only";
        _showActiveOnlyCheckBox.TabIndex = 15;
        _toolTip?.SetToolTip(_showActiveOnlyCheckBox, "Toggle to show only active customers");
        // NOTE: Event handler will be wired up in WireupToolbarEventHandlers() after ViewModel is resolved
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
        SfSkinManager.SetVisualStyle(_addCustomerButton, currentTheme);
        _addCustomerButton.AccessibleName = "Add Customer";
        _addCustomerButton.TabIndex = 20;
        _toolTip?.SetToolTip(_addCustomerButton, "Add a new customer");
        _addCustomerClickHandler = (s, e) => { _ = AddCustomerAsync(); };
        _addCustomerButton.Click += _addCustomerClickHandler;
        actionButtonsRow.Controls.Add(_addCustomerButton);

        // Edit Customer button
        _editCustomerButton = new SfButton
        {
            Text = "✏️ &Edit",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(0, 0, 5, 0)
        };
        SfSkinManager.SetVisualStyle(_editCustomerButton, currentTheme);
        _editCustomerButton.AccessibleName = "Edit Customer";
        _editCustomerButton.TabIndex = 21;
        _toolTip?.SetToolTip(_editCustomerButton, "Edit the selected customer");
        _editCustomerClickHandler = (s, e) => EditSelectedCustomer();
        _editCustomerButton.Click += _editCustomerClickHandler;
        actionButtonsRow.Controls.Add(_editCustomerButton);

        // Delete Customer button
        _deleteCustomerButton = new SfButton
        {
            Text = "🗑️ &Delete",
            AutoSize = false,
            Size = new Size(95, 32),
            Enabled = false,
            Margin = new Padding(0, 0, 10, 0)
        };
        SfSkinManager.SetVisualStyle(_deleteCustomerButton, currentTheme);
        _deleteCustomerButton.AccessibleName = "Delete Customer";
        _deleteCustomerButton.TabIndex = 22;
        _toolTip?.SetToolTip(_deleteCustomerButton, "Delete the selected customer");
        _deleteCustomerClickHandler = (s, e) => { _ = DeleteSelectedCustomerAsync(); };
        _deleteCustomerButton.Click += _deleteCustomerClickHandler;
        actionButtonsRow.Controls.Add(_deleteCustomerButton);

        // Refresh button
        _refreshButton = new SfButton
        {
            Text = "🔄 &Refresh",
            AutoSize = false,
            Size = new Size(100, 32),
            Margin = new Padding(0, 0, 5, 0)
        };
        SfSkinManager.SetVisualStyle(_refreshButton, currentTheme);
        _refreshButton.AccessibleName = "Refresh";
        _refreshButton.TabIndex = 23;
        _toolTip?.SetToolTip(_refreshButton, "Refresh the customer list");
        _refreshClickHandler = (s, e) => { _ = RefreshCustomersAsync(); };
        _refreshButton.Click += _refreshClickHandler;
        actionButtonsRow.Controls.Add(_refreshButton);

        // Sync QuickBooks button
        _syncQuickBooksButton = new SfButton
        {
            Text = "📊 Sync QB",
            AutoSize = false,
            Size = new Size(110, 32),
            Margin = new Padding(0, 0, 5, 0)
        };
        SfSkinManager.SetVisualStyle(_syncQuickBooksButton, currentTheme);
        _syncQuickBooksButton.AccessibleName = "Sync QuickBooks";
        _syncQuickBooksButton.TabIndex = 24;
        _toolTip?.SetToolTip(_syncQuickBooksButton, "Synchronize customers with QuickBooks");
        _syncQuickBooksClickHandler = (s, e) => { _ = SyncWithQuickBooksAsync(); };
        _syncQuickBooksButton.Click += _syncQuickBooksClickHandler;
        actionButtonsRow.Controls.Add(_syncQuickBooksButton);

        // Export button
        _exportButton = new SfButton
        {
            Text = "💾 E&xport",
            AutoSize = false,
            Size = new Size(100, 32),
            Margin = new Padding(0, 0, 0, 0)
        };
        SfSkinManager.SetVisualStyle(_exportButton, currentTheme);
        _exportButton.AccessibleName = "Export Customers";
        _exportButton.TabIndex = 25;
        _toolTip?.SetToolTip(_exportButton, "Export customers to CSV");
        _exportClickHandler = (s, e) => { _ = ExportCustomersAsync(); };
        _exportButton.Click += _exportClickHandler;
        actionButtonsRow.Controls.Add(_exportButton);

        toolbarLayout.Controls.Add(actionButtonsRow, 0, 1);

        _toolbarPanel.Controls.Add(toolbarLayout);
    }

    private void CreateSummaryPanel()
    {
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        _summaryPanel = new LegacyGradientPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 10, 5),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, currentTheme);

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
        var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        var cardPanel = new LegacyGradientPanel
        {
            Width = 180,
            Height = 40,
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            Margin = new Padding(5),
            AutoSize = false
        };
        SfSkinManager.SetVisualStyle(cardPanel, currentTheme);

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
            // BackColor removed - let SFSkinManager handle theming
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
    /// Binds the view model to UI controls using DataBindings for two-way binding.
    /// Called after ViewModel is resolved and toolbar event handlers are wired up.
    /// </summary>
    private void BindViewModel()
    {
        // ViewModel is guaranteed non-null at this point because OnViewModelResolved only calls this after resolution
        // ViewModel property is set in base class and never null after OnViewModelResolved

        if (ViewModel == null)
        {
            _logger?.LogWarning("BindViewModel called with null ViewModel");
            return;
        }

        // Create BindingSource for the ViewModel
        var viewModelBinding = new BindingSource
        {
            DataSource = ViewModel
        };

        // Bind grid to filtered customers collection
        if (_customersGrid != null && ViewModel?.FilteredCustomers != null)
        {
            _customersGrid.DataSource = ViewModel.FilteredCustomers;
        }
        else if (_customersGrid != null)
        {
            _logger?.LogWarning("CustomersPanel: FilteredCustomers is null, initializing with empty collection");
            _customersGrid.DataSource = new List<UtilityCustomer>();
        }

        // Bind search text box to ViewModel.SearchText with two-way binding
        if (_searchTextBox != null)
        {
            _searchTextBox.DataBindings.Add(
                nameof(_searchTextBox.Text),
                viewModelBinding,
                nameof(CustomersViewModel.SearchText),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind show active only checkbox
        if (_showActiveOnlyCheckBox != null)
        {
            _showActiveOnlyCheckBox.DataBindings.Add(
                nameof(_showActiveOnlyCheckBox.Checked),
                viewModelBinding,
                nameof(CustomersViewModel.ShowActiveOnly),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind summary labels
        if (_totalCustomersLabel != null)
        {
            _totalCustomersLabel.DataBindings.Add(
                nameof(_totalCustomersLabel.Text),
                viewModelBinding,
                nameof(CustomersViewModel.TotalCustomers),
                true,
                DataSourceUpdateMode.OnPropertyChanged,
                null,
                "Total: {0}");
        }

        if (_activeCustomersLabel != null)
        {
            _activeCustomersLabel.DataBindings.Add(
                nameof(_activeCustomersLabel.Text),
                viewModelBinding,
                nameof(CustomersViewModel.ActiveCustomers),
                true,
                DataSourceUpdateMode.OnPropertyChanged,
                null,
                "Active: {0}");
        }

        if (_balanceSummaryLabel != null)
        {
            _balanceSummaryLabel.DataBindings.Add(
                nameof(_balanceSummaryLabel.Text),
                viewModelBinding,
                nameof(CustomersViewModel.TotalOutstandingBalance),
                true,
                DataSourceUpdateMode.OnPropertyChanged,
                null,
                "Balance: {0:C}");
        }

        // Bind status strip labels
        if (_statusLabel != null)
        {
            _statusLabel.DataBindings.Add(
                nameof(_statusLabel.Text),
                viewModelBinding,
                nameof(CustomersViewModel.StatusText),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        if (_countLabel != null)
        {
            _countLabel.DataBindings.Add(
                nameof(_countLabel.Text),
                viewModelBinding,
                nameof(CustomersViewModel.TotalCustomers),
                true,
                DataSourceUpdateMode.OnPropertyChanged,
                null,
                "{0} customers");
        }

        // Subscribe to property changes for complex UI updates (only once)
        // ViewModel is guaranteed non-null at this point
        ViewModel!.PropertyChanged += ViewModel_PropertyChanged;

        _logger?.LogDebug("CustomersPanel ViewModel bound with DataBindings");

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
            grid.Style.HeaderStyle.Font = new Syncfusion.WinForms.DataGrid.Styles.GridFontInfo(new Font("Segoe UI", 11f, FontStyle.Bold));
            // Note: GridBorder constructor signature may have changed in current Syncfusion version
            // grid.Style.HeaderStyle.Borders.All = new GridBorder(GridBorderStyle.Solid, 1);

            // Row styling - consistent font and sizing
            // Note: Direct font assignment is used instead of GridFontInfo (deprecated in newer Syncfusion versions)

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
    /// Handles ViewModel property changes to update UI state.
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
            case nameof(CustomersViewModel.IsLoading):
                UpdateLoadingState();
                break;

            case nameof(CustomersViewModel.StatusText):
                UpdateStatus(ViewModel?.StatusText ?? "Ready");
                break;

            case nameof(CustomersViewModel.ErrorMessage):
                if (!string.IsNullOrEmpty(ViewModel?.ErrorMessage))
                {
                    MessageBox.Show(ViewModel.ErrorMessage, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                break;

            case nameof(CustomersViewModel.TotalCustomers):
            case nameof(CustomersViewModel.ActiveCustomers):
            case nameof(CustomersViewModel.TotalOutstandingBalance):
                UpdateSummaryDisplay();
                break;

            case nameof(CustomersViewModel.FilteredCustomers):
                UpdateNoDataOverlay();
                break;

            case nameof(CustomersViewModel.SelectedCustomer):
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
            _loadingOverlay.Visible = ViewModel?.IsLoading ?? false;
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
            var hasNoData = (ViewModel?.IsLoading == false) && (ViewModel?.FilteredCustomers.Count == 0);
            _noDataOverlay.Visible = hasNoData;

            if (hasNoData)
            {
                // Show action button when there's no data
                _noDataOverlayActionHandler = (s, e) => { _ = AddCustomerAsync(); };
                _noDataOverlay.ShowActionButton("➕ Add Customer", _noDataOverlayActionHandler);
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
            _totalCustomersLabel.Text = $"Total: {ViewModel?.TotalCustomers ?? 0}";
        }

        if (_activeCustomersLabel != null)
        {
            _activeCustomersLabel.Text = $"Active: {ViewModel?.ActiveCustomers ?? 0}";
        }

        if (_balanceSummaryLabel != null)
        {
            _balanceSummaryLabel.Text = $"Balance: {ViewModel?.TotalOutstandingBalance ?? 0:C2}";
        }

        if (_countLabel != null)
        {
            var displayCount = ViewModel?.FilteredCustomers.Count ?? 0;
            _countLabel.Text = $"{displayCount} customer{(displayCount != 1 ? "s" : "")}";
        }

        if (_balanceLabel != null)
        {
            _balanceLabel.Text = $"Total Balance: {ViewModel?.TotalOutstandingBalance ?? 0:C2}";
        }
    }

    /// <summary>
    /// Updates button enabled states based on selection.
    /// </summary>
    private void UpdateButtonStates()
    {
        var hasSelection = ViewModel?.SelectedCustomer != null;

        if (_editCustomerButton != null)
            _editCustomerButton.Enabled = hasSelection;

        if (_deleteCustomerButton != null)
            _deleteCustomerButton.Enabled = hasSelection && ViewModel?.SelectedCustomer?.Id > 0;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles search textbox text changes (debounced).
    /// </summary>
    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        // Update viewmodel property
        if (ViewModel != null)
        {
            ViewModel.SearchText = _searchTextBox?.Text;
        }
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
            if (ViewModel != null)
            {
                ViewModel.FilterCustomerType = _filterTypeComboBox.SelectedIndex switch
                {
                    1 => CustomerType.Residential,
                    2 => CustomerType.Commercial,
                    3 => CustomerType.Industrial,
                    _ => null
                };
            }
        }

        // Update location filter
        if (sender == _filterLocationComboBox && _filterLocationComboBox != null)
        {
            if (ViewModel != null)
            {
                ViewModel.FilterServiceLocation = _filterLocationComboBox.SelectedIndex switch
                {
                    1 => ServiceLocation.InsideCityLimits,
                    2 => ServiceLocation.OutsideCityLimits,
                    _ => null
                };
            }
        }
    }

    /// <summary>
    /// Handles grid selection changes.
    /// </summary>
    private void CustomersGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_customersGrid?.SelectedItem is UtilityCustomer customer && ViewModel != null)
        {
            ViewModel.SelectedCustomer = customer;
            _logger.LogDebug("Selected customer: {Account} - {Name}",
                customer.AccountNumber, customer.DisplayName);
        }
    }

    /// <summary>
    /// Handles grid cell double-click to edit customer.
    /// </summary>
    private void CustomersGrid_CellDoubleClick(object? sender, Syncfusion.WinForms.DataGrid.Events.CellClickEventArgs e)
    {
        if (e.DataRow != null && ViewModel?.SelectedCustomer != null)
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
            if (ViewModel != null)
            {
                await ViewModel.LoadCustomersCommand.ExecuteAsync(null);
            }
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
            if (ViewModel != null)
            {
                await ViewModel.SearchCommand.ExecuteAsync(null);
            }
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
            if (ViewModel != null)
            {
                await ViewModel.AddCustomerCommand.ExecuteAsync(null);

                // Open edit dialog for the new customer
                if (ViewModel.SelectedCustomer != null)
                {
                    EditSelectedCustomer();
                }
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
    private void EditSelectedCustomer()
    {
        if (ViewModel?.SelectedCustomer == null) return;

        BeginInvoke(new Func<Task>(async () =>
        {
            try
            {
                _logger.LogDebug("Editing customer {Account}", ViewModel.SelectedCustomer.AccountNumber);

                using var dialog = new CustomerEditDialog(ViewModel.SelectedCustomer, _logger);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Save the changes to the database
                    await ViewModel.SaveCustomerAsync(ViewModel.SelectedCustomer);
                    _logger.LogDebug("Customer {Account} updated successfully", ViewModel.SelectedCustomer.AccountNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to edit customer");
                MessageBox.Show($"Failed to edit customer: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }));
    }

    /// <summary>
    /// Deletes the selected customer after confirmation.
    /// </summary>
    private async Task DeleteSelectedCustomerAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel?.SelectedCustomer == null) return;

        try
        {
            var customer = ViewModel.SelectedCustomer;
            var message = $"Are you sure you want to delete customer:\n\n{customer.DisplayName}\nAccount: {customer.AccountNumber}?";
            var detail = "This action cannot be undone. All associated data will be permanently removed.";

            using var dialog = Dialogs.DeleteConfirmationDialog.Create(
                "Delete Customer",
                message,
                detail,
                _logger as Microsoft.Extensions.Logging.ILogger ?? null);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _logger.LogDebug("Deleting customer {Id} - {Account}",
                    customer.Id, customer.AccountNumber);

                var success = await ViewModel.DeleteCustomerAsync(customer.Id);

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
                _logger.LogDebug("Starting QuickBooks sync");
                if (ViewModel != null)
                {
                    await ViewModel.SyncWithQuickBooksCommand.ExecuteAsync(null);

                    MessageBox.Show(
                        ViewModel.SyncStatusMessage ?? "Sync completed successfully",
                        "QuickBooks Sync",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
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
                _logger.LogDebug("Exporting customers to {File}", dialog.FileName);
                if (ViewModel != null)
                {
                    await ViewModel.ExportToCsvCommand.ExecuteAsync(dialog.FileName);
                }
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
        this.InvokeIfRequired(() =>
        {
            try
            {
                if (_statusLabel != null && !_statusLabel.IsDisposed)
                    _statusLabel.Text = message ?? string.Empty;
            }
            catch { }
        });
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
            WireupToolbarEventHandlers();

            if (ViewModel != null)
            {
                BindViewModel();
            }

            // Defer sizing validation until layout is complete
            this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));

        }
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe view model and UI events
            try
            {
                if (_panelHeader != null)
                {
                    if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                    if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                }

                if (_searchButton != null && _searchButtonClickHandler != null) _searchButton.Click -= _searchButtonClickHandler;
                if (_clearFiltersButton != null && _clearFiltersClickHandler != null) _clearFiltersButton.Click -= _clearFiltersClickHandler;
                if (_showActiveOnlyCheckBox != null && _showActiveOnlyChangedHandler != null) _showActiveOnlyCheckBox.CheckedChanged -= _showActiveOnlyChangedHandler;
                if (_addCustomerButton != null && _addCustomerClickHandler != null) _addCustomerButton.Click -= _addCustomerClickHandler;
                if (_editCustomerButton != null && _editCustomerClickHandler != null) _editCustomerButton.Click -= _editCustomerClickHandler;
                if (_deleteCustomerButton != null && _deleteCustomerClickHandler != null) _deleteCustomerButton.Click -= _deleteCustomerClickHandler;
                if (_refreshButton != null && _refreshClickHandler != null) _refreshButton.Click -= _refreshClickHandler;
                if (_syncQuickBooksButton != null && _syncQuickBooksClickHandler != null) _syncQuickBooksButton.Click -= _syncQuickBooksClickHandler;
                if (_exportButton != null && _exportClickHandler != null) _exportButton.Click -= _exportClickHandler;

                if (_noDataOverlay != null && _noDataOverlayActionHandler != null)
                {
                    _noDataOverlay.HideActionButton();
                    // Assuming HideActionButton also removes registered handler; stored reference kept for safety
                }

                if (_customersGrid != null)
                {
                    _customersGrid.SelectionChanged -= CustomersGrid_SelectionChanged;
                    _customersGrid.CellDoubleClick -= CustomersGrid_CellDoubleClick;
                }

                if (ViewModel != null)
                {
                    ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                }
            }
            catch
            {
                // Best-effort unsubscribe; swallowing to avoid throwing in Dispose
            }

            // Dispose helpers and UI
            _errorProvider?.Dispose();
            _toolTip?.Dispose();
            _currentOperationCts?.Dispose();

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
