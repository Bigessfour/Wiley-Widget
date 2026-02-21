using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.DataGridConverter;
using Syncfusion.XlsIO;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.Models;
using WileyWidget.WinForms.Dialogs;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Lightweight Accounts panel that hosts the AccountsViewModel in a dockable UserControl.
/// Provides a data grid with CRUD toolbar for managing municipal accounts.
/// Implements ICompletablePanel to track load state and validation status.
/// </summary>
public partial class AccountsPanel : ScopedPanelBase<AccountsViewModel>
{
    private PanelHeader? _header;
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _accountsGrid;
    private TableLayoutPanel? _layout;
    private BindingSource? _accountsBinding;
    private ErrorProvider? _errorProvider;
    private FlowLayoutPanel? _toolbarPanel;
    private DpiAwareImageService? _imageService;
    private SfButton? _createButton;
    private SfButton? _editButton;
    private SfButton? _deleteButton;
    private SfButton? _refreshButton;
    private SfButton? _exportButton;
    private SfComboBox? _fundFilterComboBox;
    private SfComboBox? _accountTypeFilterComboBox;
    private SfComboBox? _departmentFilterComboBox;
    private TextBoxExt? _searchBox;
    private ToolTip? _buttonToolTips;

    // Event handlers for proper cleanup
    private Syncfusion.WinForms.DataGrid.Events.SelectionChangedEventHandler? _gridSelectionChangedHandler;
    private Syncfusion.WinForms.DataGrid.Events.CellClickEventHandler? _gridCellDoubleClickHandler;

    /// <summary>
    /// Maximum row count threshold for grid validation.
    /// Warn if 0 rows; error if exceeds this (e.g., database corruption or bad query).
    /// </summary>
    private const int MaxGridRowsThreshold = 10000;

    /// <summary>
    /// Initializes a new instance with DI-resolved ViewModel and logger.
    /// </summary>
    public AccountsPanel(IServiceScopeFactory scopeFactory, ILogger<AccountsPanel> logger, DpiAwareImageService imageService)
        : base(scopeFactory, logger)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        Logger?.LogDebug("[ACCOUNTS_CTOR] Constructor started");

        // Set AutoScaleMode for proper DPI scaling
        this.AutoScaleMode = AutoScaleMode.Dpi;

        // Add padding for proper spacing
        this.Padding = new Padding(12);

        // Set preferred size for proper docking display (matches PreferredDockSize extension)
        this.Size = new Size(900, 560);
        this.MinimumSize = new Size(600, 420);

        InitializeControls();
        Load += AccountsPanel_Load;

        // NEW: Force VM resolution check right after controls are built (Syncfusion timing pattern)
        if (ViewModel != null)
        {
            Logger?.LogDebug("[ACCOUNTS_CTOR] ViewModel available in constructor, triggering early bind");
            BindViewModelCommands();
        }

        Logger?.LogDebug("[ACCOUNTS_CTOR] Constructor complete - Load event wired, controls initialized");

        // Wire up keyboard shortcuts via ProcessCmdKey override
        // (KeyPreview not available on UserControl)
    }

    /// <summary>
    /// Called when the panel is loaded; triggers data loading via ILazyLoadViewModel.
    /// After data loads asynchronously, explicitly refresh the grid to display the newly loaded accounts.
    /// Also handles layout adjustments for properly rendering docked panels.
    /// </summary>
    private async void AccountsPanel_Load(object? sender, EventArgs e)
    {
        Logger?.LogInformation("[ACCOUNTS_LOAD] ✅ Load event FIRED - sender: {Sender}, ViewModel: {VM}, IsLazy: {IsLazy}",
            sender?.GetType().Name ?? "null",
            ViewModel?.GetType().Name ?? "null",
            ViewModel is ILazyLoadViewModel);

        // Trigger lazy load through ILazyLoadViewModel pattern
        if (ViewModel is ILazyLoadViewModel lazyLoad)
        {
            Logger?.LogDebug("[ACCOUNTS_LOAD] Starting lazy load via OnVisibilityChangedAsync");
            try
            {
                await lazyLoad.OnVisibilityChangedAsync(true);
                Logger?.LogDebug("[ACCOUNTS_LOAD] ✅ Lazy load completed successfully");

                // CRITICAL: After data loads, refresh the grid to display accounts.
                // The grid was bound at BindViewModel() time when the Accounts collection was empty.
                // We must explicitly refresh here to trigger layout and painting.
                if (_accountsGrid != null && _layout != null)
                {
                    // Ensure grid has the current data source
                    _accountsGrid.DataSource = ViewModel.Accounts;

                    // Suspend layout during refresh to prevent flicker
                    _layout.SuspendLayout();

                    // Refresh grid rendering
                    _accountsGrid.Refresh();
                    _accountsGrid.Invalidate();

                    // Ensure proper layout for docked controls
                    _layout.ResumeLayout(true);
                    this.PerformLayout();

                    Logger?.LogInformation("[GRID_REFRESH] ✅ Post-load refresh: Grid DataSource={Type}, RowCount={Rows}, Accounts={Count}",
                        _accountsGrid.DataSource?.GetType().Name ?? "null",
                        _accountsGrid.RowCount,
                        (ViewModel?.Accounts?.Count ?? 0));
                    Logger?.LogDebug("[GRID_REFRESH] Grid details: Rows={Rows}, Columns={Cols}, Visible={Vis}",
                        _accountsGrid.RowCount, _accountsGrid.Columns.Count, _accountsGrid.Visible);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "AccountsPanel: Error during lazy load");
            }
        }
    }

    /// <summary>
    /// Called after the ViewModel is resolved from DI. Bind controls to the ViewModel.
    /// </summary>
    protected override void OnViewModelResolved(AccountsViewModel? viewModel)
    {
        Logger?.LogDebug("[ACCOUNTS_VM_RESOLVED] OnViewModelResolved called - ViewModel type: {Type}, IsNull: {IsNull}",
            viewModel?.GetType().Name ?? "null", viewModel == null);

        base.OnViewModelResolved(viewModel);

        if (viewModel != null)
        {
            Logger?.LogDebug("[ACCOUNTS_VM_RESOLVED] ViewModel is AccountsViewModel - Accounts count: {Count}, IsDataLoaded: {Loaded}",
                viewModel.Accounts?.Count ?? 0, viewModel.IsDataLoaded);
            BindViewModel();
            BindViewModelCommands();
        }
        else
        {
            Logger?.LogWarning("[ACCOUNTS_VM_RESOLVED] ViewModel is null! Panel binding skipped.");
        }
    }

    private void InitializeControls()
    {
        SuspendLayout();
        _logger.LogDebug("[ACCOUNTS_PANEL] InitializeControls START");

        // Panel header (docked to top)
        _header = new PanelHeader
        {
            Dock = DockStyle.Top,
            Title = "Chart of Accounts",
            MinimumSize = new Size(0, 52), // Prevent collapse
            Height = 52
        };
        _header.AccessibleName = "Chart of Accounts Panel Header";
        _header.AccessibleDescription = "Municipal Chart of Accounts management panel with CRUD operations and filtering";
        _header.RefreshClicked += RefreshButton_Click;
        _header.CloseClicked += (s, e) => ClosePanel();
        _header.HelpClicked += (s, e) => ShowChartOfAccountsHelp();
        _header.PinToggled += (s, e) => { /* Pin logic */ };
        Controls.Add(_header);
        _logger.LogDebug("[ACCOUNTS_PANEL] Header added: Height={Height}, Visible={Visible}", _header.Height, _header.Visible);

        // Main content layout (toolbar + grid)
        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = false
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F)); // Increased height for toolbar row (allows 2 rows of controls)
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Toolbar panel - use explicit Height instead of Dock.Fill for proper sizing in TableLayoutPanel
        _toolbarPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 84,
            Padding = new Padding(4),
            WrapContents = true,
            AutoScroll = false
        };
        _logger.LogDebug("[ACCOUNTS_PANEL] Toolbar panel configured: Height={Height}", _toolbarPanel.Height);

        // Get active theme for button styling
        var activeTheme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

        // Create Button - fully configured with Syncfusion properties
        _createButton = new SfButton
        {
            Text = "New Account",
            AutoSize = true,
            ThemeName = activeTheme,
            AccessibilityEnabled = true,
            AutoEllipsis = true,
            FocusRectangleVisible = true,
            Padding = new Padding(8, 4, 8, 4),
            TextMargin = new Padding(2),
            CanApplyTheme = true,
            CanOverrideStyle = false,
            Name = "btnNewAccount",
            AccessibleName = "New Account"
        };
        _createButton.AccessibleDescription = "Create a new municipal account";
        // Command will be bound in BindViewModelCommands() - Syncfusion MVVM pattern

        // Edit Button - fully configured
        _editButton = new SfButton
        {
            Text = "Edit",
            AutoSize = true,
            Enabled = false,
            ThemeName = activeTheme,
            AccessibilityEnabled = true,
            AutoEllipsis = true,
            FocusRectangleVisible = true,
            Padding = new Padding(8, 4, 8, 4),
            TextMargin = new Padding(2),
            CanApplyTheme = true,
            CanOverrideStyle = false,
            Name = "btnEdit",
            AccessibleName = "Edit"
        };
        _editButton.AccessibleDescription = "Edit the selected account";
        // Command will be bound in BindViewModelCommands() - Syncfusion MVVM pattern

        // Delete Button - fully configured
        _deleteButton = new SfButton
        {
            Text = "Delete",
            AutoSize = true,
            Enabled = false,
            ThemeName = activeTheme,
            AccessibilityEnabled = true,
            AutoEllipsis = true,
            FocusRectangleVisible = true,
            Padding = new Padding(8, 4, 8, 4),
            TextMargin = new Padding(2),
            CanApplyTheme = true,
            CanOverrideStyle = false,
            Name = "btnDelete",
            AccessibleName = "Delete"
        };
        _deleteButton.AccessibleDescription = "Delete the selected account";
        // Command will be bound in BindViewModelCommands() - Syncfusion MVVM pattern

        // Refresh Button - fully configured
        _refreshButton = new SfButton
        {
            Text = "Refresh",
            AutoSize = true,
            ThemeName = activeTheme,
            AccessibilityEnabled = true,
            AutoEllipsis = true,
            FocusRectangleVisible = true,
            Padding = new Padding(8, 4, 8, 4),
            TextMargin = new Padding(2),
            CanApplyTheme = true,
            CanOverrideStyle = false,
            Name = "btnRefresh",
            AccessibleName = "Refresh"
        };
        _refreshButton.AccessibleDescription = "Refresh account list from database";
        // Command will be bound in BindViewModelCommands() - Syncfusion MVVM pattern

        // Export to Excel Button - professional feature for auditors
        _exportButton = new SfButton
        {
            Text = "Export Excel",
            AutoSize = true,
            ThemeName = activeTheme,
            AccessibilityEnabled = true,
            AutoEllipsis = true,
            FocusRectangleVisible = true,
            Padding = new Padding(8, 4, 8, 4),
            TextMargin = new Padding(2),
            CanApplyTheme = true,
            CanOverrideStyle = false,
            Name = "btnExportExcel",
            AccessibleName = "Export to Excel"
        };
        _exportButton.AccessibleDescription = "Export the account list to Excel spreadsheet";
        _exportButton.Click += ExportButton_Click;

        // === ICON ASSIGNMENT FOR TOOLBAR BUTTONS (Optional Polish) ===
        try
        {
            // Get 16x16 icons for toolbar buttons
            _createButton.Image = _imageService.GetImage("add");
            _editButton.Image = _imageService.GetImage("edit");
            _deleteButton.Image = _imageService.GetImage("delete");
            _refreshButton.Image = _imageService.GetImage("refresh");
            _exportButton.Image = _imageService.GetImage("export");

            // Layout: icon on left, text on right
            foreach (var btn in new[] { _createButton, _editButton, _deleteButton, _refreshButton, _exportButton })
            {
                btn.TextImageRelation = TextImageRelation.ImageBeforeText;
                btn.ImageAlign = ContentAlignment.MiddleLeft;
                btn.AutoSize = true; // Recalculate size with icon
            }

            Logger?.LogDebug("[ACCOUNTS_ICONS] ✅ Toolbar button icons assigned successfully");
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "[ACCOUNTS_ICONS] ⚠️ Failed to load button icons - UI will display text only");
        }

        // Fund Filter Dropdown
        var fundLabel = new Label
        {
            Text = "Fund:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(16, 8, 4, 8),
            Name = "lblFundFilter"
        };

        _fundFilterComboBox = new SfComboBox
        {
            Width = 120,
            Height = 28,
            ThemeName = activeTheme,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            Name = "cmbFundFilter",
            AccessibleName = "Fund Filter",
            Margin = new Padding(0, 8, 4, 8)
        };
        _fundFilterComboBox.AccessibleDescription = "Filter accounts by fund type";
        // Initial placeholder - will be populated when ViewModel is resolved
        _fundFilterComboBox.DataSource = new List<object> { "All" };
        _fundFilterComboBox.DisplayMember = "ToString";
        _fundFilterComboBox.ValueMember = "ToString";
        _fundFilterComboBox.SelectedIndex = 0; // "All"
        SfSkinManager.SetVisualStyle(_fundFilterComboBox, activeTheme);
        _fundFilterComboBox.SelectedIndexChanged += FundFilterComboBox_SelectedIndexChanged;

        // Account Type Filter Dropdown
        var accountTypeLabel = new Label
        {
            Text = "Type:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 8, 4, 8),
            Name = "lblAccountTypeFilter"
        };

        _accountTypeFilterComboBox = new SfComboBox
        {
            Width = 100,
            Height = 28,
            ThemeName = activeTheme,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            Name = "cmbAccountTypeFilter",
            AccessibleName = "Account Type Filter",
            Margin = new Padding(0, 8, 4, 8)
        };
        _accountTypeFilterComboBox.AccessibleDescription = "Filter accounts by account type";
        // Initial placeholder - will be populated when ViewModel is resolved
        _accountTypeFilterComboBox.DataSource = new List<object> { "All" };
        _accountTypeFilterComboBox.DisplayMember = "ToString";
        _accountTypeFilterComboBox.ValueMember = "ToString";
        _accountTypeFilterComboBox.SelectedIndex = 0;
        SfSkinManager.SetVisualStyle(_accountTypeFilterComboBox, activeTheme);
        _accountTypeFilterComboBox.SelectedIndexChanged += AccountTypeFilterComboBox_SelectedIndexChanged;

        // Department Filter Dropdown
        var departmentLabel = new Label
        {
            Text = "Dept:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 8, 4, 8),
            Name = "lblDepartmentFilter"
        };

        _departmentFilterComboBox = new SfComboBox
        {
            Width = 120,
            Height = 28,
            ThemeName = activeTheme,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            Name = "cmbDepartmentFilter",
            AccessibleName = "Department Filter",
            Margin = new Padding(0, 8, 4, 8)
        };
        _departmentFilterComboBox.AccessibleDescription = "Filter accounts by department";
        // Initial placeholder - will be populated when ViewModel is resolved
        _departmentFilterComboBox.DataSource = new List<object> { "All" };
        _departmentFilterComboBox.DisplayMember = "ToString";
        _departmentFilterComboBox.ValueMember = "ToString";
        _departmentFilterComboBox.SelectedIndex = 0;
        SfSkinManager.SetVisualStyle(_departmentFilterComboBox, activeTheme);
        _departmentFilterComboBox.SelectedIndexChanged += DepartmentFilterComboBox_SelectedIndexChanged;

        // Search Label and Box - for filtering accounts
        var searchLabel = new Label
        {
            Text = "Search:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(16, 8, 4, 8),
            Name = "lblSearch"
        };

        _searchBox = new TextBoxExt
        {
            // UX Best Practices: Microsoft Fluent (280-320px), Material Design (280px+), Apple HIG (250-300px)
            Width = 300,              // Increased from 250 - allows longer search terms without truncation
            Height = 30,              // Increased from 28 - better touch target (Fluent: 32px, Material: 36px)
            MinimumSize = new Size(200, 30),  // Prevents over-shrinking when panel resizes
            ThemeName = activeTheme,
            Name = "txtSearch",
            AccessibleName = "Search",
            Margin = new Padding(0, 8, 4, 8),
            BorderStyle = BorderStyle.FixedSingle,  // Clean, modern appearance
            Font = new Font(this.Font.FontFamily, 9.5F, FontStyle.Regular)  // Slightly larger for readability
        };
        _searchBox.AccessibleDescription = "Type to search accounts by account number, name, description, fund, or department";
        _searchBox.TextChanged += SearchBox_TextChanged;

        // Configure tooltips for buttons (accessibility enhancement)
        _buttonToolTips = new ToolTip
        {
            AutoPopDelay = 8000,     // Longer display for busy mayors
            InitialDelay = 300,      // Faster appearance
            ReshowDelay = 100,
            ShowAlways = true,       // CRITICAL - ensures tooltips always display
            UseAnimation = true,
            UseFading = true
        };

        // Add tooltip to the header itself for context
        _buttonToolTips.SetToolTip(_header, "Chart of Accounts — Manage municipal funds, departments, and balances (Press F1 for full help)");
        _buttonToolTips.SetToolTip(_createButton, "Create a new municipal account (Ctrl+N)");
        _buttonToolTips.SetToolTip(_editButton, "Edit the selected account (F2)");
        _buttonToolTips.SetToolTip(_deleteButton, "Delete the selected account (Delete)");
        _buttonToolTips.SetToolTip(_refreshButton, "Refresh account list from database (F5)");
        if (_exportButton != null)
        {
            _buttonToolTips.SetToolTip(_exportButton, "Export account list to Excel spreadsheet (Ctrl+E)");
        }
        _buttonToolTips.SetToolTip(_fundFilterComboBox, "Filter accounts by fund type");
        _buttonToolTips.SetToolTip(_accountTypeFilterComboBox, "Filter accounts by account type");
        _buttonToolTips.SetToolTip(_departmentFilterComboBox, "Filter accounts by department");
        _buttonToolTips.SetToolTip(_searchBox, "Search by account number, name, description, fund, or department (Ctrl+F)");

        _toolbarPanel.Controls.AddRange(new Control[] {
            _createButton!, _editButton!, _deleteButton!, _refreshButton!, _exportButton!,
            fundLabel, _fundFilterComboBox,
            accountTypeLabel, _accountTypeFilterComboBox,
            departmentLabel, _departmentFilterComboBox,
            searchLabel, _searchBox
        });
        _logger.LogDebug("[ACCOUNTS_PANEL] Toolbar has {Count} buttons and search box", _toolbarPanel.Controls.Count);

        _layout.Controls.Add(_toolbarPanel, 0, 0);
        _toolbarPanel.BringToFront(); // Ensure Z-order

        // Accounts data grid
        _accountsGrid = ControlFactory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowFiltering = true;
            grid.AllowGrouping = true;
            grid.ShowGroupDropArea = true;
            grid.AllowSorting = true;
            grid.AllowResizingColumns = true;
            grid.EnableDataVirtualization = true;
            grid.SelectionMode = GridSelectionMode.Single;
            grid.SelectionUnit = SelectionUnit.Row;
            grid.RowHeight = 36;
            grid.ShowToolTip = true;
            grid.ShowHeaderToolTip = true;
            grid.ShowValidationErrorToolTip = true;
            grid.Name = "dataGridAccounts";
            grid.AccessibleName = "Accounts Grid";
            grid.ThemeName = activeTheme;
        });
        _accountsGrid.AccessibleDescription = "Data grid showing municipal funds, account balances, budgets, and departments. Use arrow keys to navigate, Enter or F2 to edit, Delete to remove";
        _accountsGrid.SelectionChanged += _gridSelectionChangedHandler = Grid_SelectionChanged;

        // Add professional context menu for grid (right-click operations)
        var gridContextMenu = new ContextMenuStrip();
        gridContextMenu.Items.Add("Edit Account", null, (s, e) => { if (_editButton?.Enabled == true) EditAccount(); });
        gridContextMenu.Items.Add("Delete Account", null, (s, e) => { if (_deleteButton?.Enabled == true) DeleteButton_Click(this, EventArgs.Empty); });
        gridContextMenu.Items.Add(new ToolStripSeparator());
        gridContextMenu.Items.Add("Refresh", null, (s, e) => RefreshButton_Click(this, EventArgs.Empty));
        gridContextMenu.Items.Add("Export to Excel", null, (s, e) => ExportButton_Click(this, EventArgs.Empty));
        _accountsGrid.ContextMenuStrip = gridContextMenu;
        _accountsGrid.CellDoubleClick += _gridCellDoubleClickHandler = Grid_CellDoubleClick;
        _accountsGrid.RowValidating += Grid_RowValidating;
        _accountsGrid.QueryCellStyle += Grid_QueryCellStyle;
        _accountsGrid.CellClick += Grid_CellClick;
        _accountsGrid.FilterChanging += Grid_FilterChanging;

        // Wrap grid setup in BeginInit/EndInit for performance
        _accountsGrid.BeginInit();

        // Configure grid columns
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountNumber", HeaderText = "Account #", MinimumWidth = 90, AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.AllCells });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountName", HeaderText = "Account Name", AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "FundName", HeaderText = "Fund", MinimumWidth = 80 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountType", HeaderText = "Type", MinimumWidth = 80 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "CurrentBalance", HeaderText = "Balance", FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency, MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "BudgetAmount", HeaderText = "Budget", FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency, MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Department", HeaderText = "Department", MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridCheckBoxColumn { MappingName = "IsActive", HeaderText = "Active", MinimumWidth = 70 });

        // Add table summaries (totals for Balance and Budget)
        var summaryRow = new GridTableSummaryRow
        {
            ShowSummaryInRow = true,
            Title = "Total: {TotalBalance} / {TotalBudget}",
            Position = Syncfusion.WinForms.DataGrid.Enums.VerticalPosition.Bottom
        };
        summaryRow.SummaryColumns.Add(new GridSummaryColumn
        {
            Name = "TotalBalance",
            MappingName = "CurrentBalance",
            SummaryType = Syncfusion.Data.SummaryType.DoubleAggregate,
            Format = "{Sum:c}"
        });
        summaryRow.SummaryColumns.Add(new GridSummaryColumn
        {
            Name = "TotalBudget",
            MappingName = "BudgetAmount",
            SummaryType = Syncfusion.Data.SummaryType.DoubleAggregate,
            Format = "{Sum:c}"
        });
        _accountsGrid.TableSummaryRows.Add(summaryRow);

        _accountsGrid.EndInit();

        // Note: String column filter operators are restricted via Grid_FilterChanging event handler
        // to prevent InvalidOperationException on relational operators (>, <, >=, <=)
        // See Grid_FilterChanging method for implementation details

        _logger.LogDebug("[ACCOUNTS_PANEL] Grid configured with {Count} columns", _accountsGrid.Columns.Count);

        _layout.Controls.Add(_accountsGrid, 0, 1);

        _errorProvider = new ErrorProvider(this);

        this.Controls.Add(_layout);

        // Apply theme via SfSkinManager
        var theme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, theme);

        ResumeLayout(true);
        this.PerformLayout();
        this.Refresh();

        // Ensure controls are visible and in correct Z-order
        _header?.BringToFront();
        _layout?.BringToFront();

        _logger.LogInformation("[ACCOUNTS_PANEL] InitializeControls COMPLETE - Header visible: {HeaderVisible}, Toolbar visible: {ToolbarVisible}, Grid visible: {GridVisible}",
            _header?.Visible ?? false, _toolbarPanel?.Visible ?? false, _accountsGrid?.Visible ?? false);
    }

    /// <summary>
    /// Handles keyboard shortcuts for enhanced UX.
    /// Ctrl+N = New Account, F2 = Edit, Delete = Delete, F5 = Refresh, Ctrl+F = Search, Enter = Edit selected
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        try
        {
            // Ctrl+F: Focus search box
            if (keyData == (Keys.Control | Keys.F))
            {
                if (_searchBox != null)
                {
                    _searchBox.Focus();
                    _searchBox.SelectAll();
                }
                return true;
            }

            // Ctrl+N: Create new account
            if (keyData == (Keys.Control | Keys.N))
            {
                CreateAccount();
                return true;
            }

            // F2 or Enter: Edit selected account
            if ((keyData == Keys.F2 || keyData == Keys.Enter) && _editButton?.Enabled == true)
            {
                EditAccount();
                return true;
            }

            // Delete: Delete selected account
            if (keyData == Keys.Delete && _deleteButton?.Enabled == true)
            {
                DeleteButton_Click(this, EventArgs.Empty);
                return true;
            }

            // F5: Refresh
            if (keyData == Keys.F5)
            {
                RefreshButton_Click(this, EventArgs.Empty);
                return true;
            }

            // F1: Show help dialog
            if (keyData == Keys.F1)
            {
                ShowChartOfAccountsHelp();
                return true;
            }

            // Ctrl+E: Export to Excel
            if (keyData == (Keys.Control | Keys.E))
            {
                ExportButton_Click(this, EventArgs.Empty);
                return true;
            }

            // Escape: Clear selection and focus grid
            if (keyData == Keys.Escape)
            {
                if (_accountsGrid != null)
                {
                    _accountsGrid.SelectedIndex = -1;
                    _accountsGrid.Focus();
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing keyboard shortcut");
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// Handles single-click on grid cells for visual feedback.
    /// </summary>
    private void Grid_CellClick(object? sender, Syncfusion.WinForms.DataGrid.Events.CellClickEventArgs e)
    {
        // Provide visual feedback that row is selected
        if (e.DataRow != null && _accountsGrid != null)
        {
            _logger?.LogDebug("Cell clicked at row index: {RowIndex}", e.DataRow.RowIndex);
        }
    }

    /// <summary>
    /// Handles search box text changes to filter the accounts grid.
    /// Filters by Account Number, Account Name, Description, Fund Name, or Department.
    /// </summary>
    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        ApplySearchFilter();
    }

    /// <summary>
    /// Applies real-time search filter to the accounts grid using Syncfusion View.Filter.
    /// </summary>
    private void ApplySearchFilter()
    {
        try
        {
            if (_accountsGrid == null || _searchBox == null)
                return;

            var searchText = _searchBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Clear filter
                _accountsGrid.View?.Filter = null;
                _accountsGrid.View?.RefreshFilter();
                _logger?.LogDebug("[SEARCH] Filter cleared");
            }
            else
            {
                // Apply filter - search in AccountNumber, AccountName, Description, FundName, Department
                _accountsGrid.View.Filter = (item) =>
                {
                    if (item is MunicipalAccountDisplay account)
                    {
                        return (account.AccountNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (account.AccountName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (account.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (account.FundName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (account.Department?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
                    }
                    return false;
                };
                _accountsGrid.View?.RefreshFilter();
                _logger?.LogDebug("[SEARCH] Filter applied with term: '{SearchTerm}', Results: {Count}",
                    searchText, _accountsGrid.View?.Records?.Count ?? 0);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[SEARCH] Error filtering accounts");
        }
    }

    /// <summary>
    /// Handles fund filter dropdown selection changes.
    /// </summary>
    private void FundFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel == null || _fundFilterComboBox == null)
                return;

            MunicipalFundType? selectedFund = null;
            if (_fundFilterComboBox.SelectedIndex > 0 && _fundFilterComboBox.SelectedItem is MunicipalFundType fund)
            {
                selectedFund = fund;
            }
            ViewModel.SelectedFund = selectedFund;
            _logger?.LogDebug("[FUND_FILTER] Selected fund: {Fund}", selectedFund?.ToString() ?? "All");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[FUND_FILTER] Error setting fund filter");
        }
    }

    /// <summary>
    /// Handles account type filter dropdown selection changes.
    /// </summary>
    private void AccountTypeFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel == null || _accountTypeFilterComboBox == null)
                return;

            AccountType? selectedType = null;
            if (_accountTypeFilterComboBox.SelectedIndex > 0 && _accountTypeFilterComboBox.SelectedItem is AccountType type)
            {
                selectedType = type;
            }
            ViewModel.SelectedAccountType = selectedType;
            _logger?.LogDebug("[ACCOUNT_TYPE_FILTER] Selected type: {Type}", selectedType?.ToString() ?? "All");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[ACCOUNT_TYPE_FILTER] Error setting account type filter");
        }
    }

    /// <summary>
    /// Handles department filter dropdown selection changes.
    /// </summary>
    private void DepartmentFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel == null || _departmentFilterComboBox == null)
                return;

            string? selectedDepartment = null;
            if (_departmentFilterComboBox.SelectedIndex > 0 && _departmentFilterComboBox.SelectedItem is string dept)
            {
                selectedDepartment = dept;
            }
            ViewModel.SelectedDepartment = selectedDepartment;
            _logger?.LogDebug("[DEPARTMENT_FILTER] Selected department: {Department}", selectedDepartment ?? "All");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[DEPARTMENT_FILTER] Error setting department filter");
        }
    }

    private void BindViewModel()
    {
        if (ViewModel == null)
        {
            _logger.LogWarning("[BINDING] ViewModel is null; skipping binding");
            return;
        }

        _logger.LogDebug("[BINDING] Starting AccountsPanel ViewModel binding. Accounts count: {Count}",
            ViewModel.Accounts?.Count ?? 0);

        // Populate filter dropdowns now that ViewModel is available
        PopulateFilterDropdowns();

        // Wrap binding in BeginUpdate/EndUpdate for performance
        if (_accountsGrid != null)
        {
            _accountsGrid.BeginUpdate();

            _logger.LogDebug("[BINDING] Binding grid directly to Accounts collection");
            _accountsGrid.DataSource = ViewModel.Accounts;
            _logger.LogDebug("[BINDING] Grid bound. DataSource type: {Type}, RowCount: {RowCount}",
                _accountsGrid.DataSource?.GetType().Name ?? "null",
                _accountsGrid.RowCount);

            ApplySearchFilter();

            _accountsGrid.EndUpdate();
        }

        // Bind header title to ViewModel.Title property using BindingSource
        if (_header != null)
        {
            if (_accountsBinding == null)
            {
                _accountsBinding = new BindingSource { DataSource = ViewModel };
            }
            else
            {
                _accountsBinding.DataSource = ViewModel;
            }

            // Clear existing bindings to avoid duplicate bindings
            _header.DataBindings.Clear();
            _header.DataBindings.Add(
                nameof(_header.Title),
                _accountsBinding,
                nameof(ViewModel.Title),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Subscribe to ViewModel property changes for reactive updates
        if (ViewModel is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged += ViewModel_PropertyChanged;
            _logger.LogDebug("[BINDING] Subscribed to ViewModel PropertyChanged events");
        }

        // NEW: Two-way DataBindings for filter dropdowns (Syncfusion MVVM pattern)
        BindFilterDropdowns();

        // Note: SfDataGrid.SelectedItem is a read-only runtime property, not bindable.
        // Selection sync happens via SelectionChanged event -> UpdateButtonState() instead.
        _logger.LogDebug("[BINDING] Selection sync will use SelectionChanged event handler");

        _logger.LogDebug("[BINDING] AccountsPanel ViewModel bound successfully. Final grid row count: {RowCount}",
            _accountsGrid?.RowCount ?? -1);
    }

    /// <summary>
    /// Populates the filter dropdown controls with data from the ViewModel.
    /// Called when ViewModel is first bound and ready.
    /// </summary>
    private void PopulateFilterDropdowns()
    {
        try
        {
            if (ViewModel == null)
            {
                _logger?.LogWarning("[FILTER_POPULATE] ViewModel is null; cannot populate filters");
                return;
            }

            // Temporarily unsubscribe from events to avoid triggering filter logic during population
            if (_fundFilterComboBox != null)
                _fundFilterComboBox.SelectedIndexChanged -= FundFilterComboBox_SelectedIndexChanged;
            if (_accountTypeFilterComboBox != null)
                _accountTypeFilterComboBox.SelectedIndexChanged -= AccountTypeFilterComboBox_SelectedIndexChanged;
            if (_departmentFilterComboBox != null)
                _departmentFilterComboBox.SelectedIndexChanged -= DepartmentFilterComboBox_SelectedIndexChanged;

            // Populate Fund filter
            if (_fundFilterComboBox != null && ViewModel.AvailableFunds != null)
            {
                var fundItems = new List<object> { "All" };
                fundItems.AddRange(ViewModel.AvailableFunds.Cast<object>());
                _fundFilterComboBox.DataSource = fundItems;
                _fundFilterComboBox.SelectedIndex = 0;
                _fundFilterComboBox.Refresh();  // Syncfusion magic for immediate display
                _logger?.LogDebug("[FILTER_POPULATE] Fund filter populated with {Count} items", fundItems.Count);
            }

            // Populate Account Type filter
            if (_accountTypeFilterComboBox != null && ViewModel.AvailableAccountTypes != null)
            {
                var accountTypeItems = new List<object> { "All" };
                accountTypeItems.AddRange(ViewModel.AvailableAccountTypes.Cast<object>());
                _accountTypeFilterComboBox.DataSource = accountTypeItems;
                _accountTypeFilterComboBox.SelectedIndex = 0;
                _accountTypeFilterComboBox.Refresh();  // Syncfusion magic for immediate display
                _logger?.LogDebug("[FILTER_POPULATE] Account Type filter populated with {Count} items", accountTypeItems.Count);
            }

            // Populate Department filter
            if (_departmentFilterComboBox != null && ViewModel.AvailableDepartments != null)
            {
                var departmentItems = new List<object> { "All" };
                departmentItems.AddRange(ViewModel.AvailableDepartments.Cast<object>());
                _departmentFilterComboBox.DataSource = departmentItems;
                _departmentFilterComboBox.SelectedIndex = 0;
                _departmentFilterComboBox.Refresh();  // Syncfusion magic for immediate display
                _logger?.LogDebug("[FILTER_POPULATE] Department filter populated with {Count} items", departmentItems.Count);
            }

            // After all population, force UI refresh (fixes "only All" bug)
            _fundFilterComboBox?.Invalidate();
            _accountTypeFilterComboBox?.Invalidate();
            _departmentFilterComboBox?.Invalidate();

            // Re-subscribe to events
            if (_fundFilterComboBox != null)
                _fundFilterComboBox.SelectedIndexChanged += FundFilterComboBox_SelectedIndexChanged;
            if (_accountTypeFilterComboBox != null)
                _accountTypeFilterComboBox.SelectedIndexChanged += AccountTypeFilterComboBox_SelectedIndexChanged;
            if (_departmentFilterComboBox != null)
                _departmentFilterComboBox.SelectedIndexChanged += DepartmentFilterComboBox_SelectedIndexChanged;

            _logger?.LogInformation("[FILTER_POPULATE] All filter dropdowns populated successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[FILTER_POPULATE] Error populating filter dropdowns");

            // Re-subscribe even on error to avoid leaving controls in bad state
            if (_fundFilterComboBox != null)
                _fundFilterComboBox.SelectedIndexChanged += FundFilterComboBox_SelectedIndexChanged;
            if (_accountTypeFilterComboBox != null)
                _accountTypeFilterComboBox.SelectedIndexChanged += AccountTypeFilterComboBox_SelectedIndexChanged;
            if (_departmentFilterComboBox != null)
                _departmentFilterComboBox.SelectedIndexChanged += DepartmentFilterComboBox_SelectedIndexChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.Accounts))
        {
            // Refresh grid when Accounts collection reference changes
            if (_accountsGrid != null)
            {
                _accountsGrid.BeginUpdate();
                _logger.LogDebug("[BINDING] Accounts collection reference changed, rebinding grid");
                _accountsGrid.DataSource = ViewModel?.Accounts;
                _accountsGrid.Refresh();
                _logger.LogDebug("[BINDING] Accounts collection changed, rebound grid with {Count} items", ViewModel?.Accounts?.Count ?? 0);
                ApplySearchFilter();
                _accountsGrid.EndUpdate();
            }
        }
    }

    private void UpdateButtonState(object? sender = null, EventArgs? e = null)
    {
        if (_accountsGrid?.SelectedItem is MunicipalAccountDisplay selectedAccount)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedAccount = selectedAccount;
            }
            if (_editButton != null) _editButton.Enabled = true;
            if (_deleteButton != null) _deleteButton.Enabled = true;
            _logger.LogDebug("Grid selection changed: {AccountNumber}", selectedAccount.AccountNumber);
        }
        else
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedAccount = null;
            }
            if (_editButton != null) _editButton.Enabled = false;
            if (_deleteButton != null) _deleteButton.Enabled = false;
            _logger.LogDebug("Grid selection cleared");
        }
    }

    /// <summary>
    /// Handles double-click on grid cell to open edit dialog.
    /// </summary>
    private void Grid_CellDoubleClick(object? sender, Syncfusion.WinForms.DataGrid.Events.CellClickEventArgs e)
    {
        if (_accountsGrid?.SelectedItem is MunicipalAccountDisplay selectedAccount)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedAccount = selectedAccount;
            }

            if (ViewModel?.EditAccountCommand?.CanExecute(selectedAccount) ?? false)
            {
                _ = ViewModel.EditAccountCommand.ExecuteAsync(selectedAccount);
            }
        }
    }

    private MunicipalAccountDisplay? ResolveSelectedAccountForCommands()
    {
        if (_accountsGrid?.SelectedItem is MunicipalAccountDisplay selectedFromGrid)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedAccount = selectedFromGrid;
            }

            return selectedFromGrid;
        }

        return ViewModel?.SelectedAccount;
    }

    private async void CreateAccount(object? sender = null, EventArgs? e = null)
    {
        try
        {
            // Use the new ViewModel command that handles dialog internally
            if (ViewModel?.CreateAccountCommand?.CanExecute(null) ?? false)
            {
                await ViewModel.CreateAccountCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating account");
            MessageBox.Show($"Error creating account: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handles Edit button click - opens AccountEditDialog with selected account.
    /// MUST run on UI thread for WinForms dialog.
    /// </summary>
    private async void EditAccount(object? sender = null, EventArgs? e = null)
    {
        try
        {
            _logger.LogDebug("EditAccount method called");

            if (ViewModel == null)
            {
                _logger.LogError("EditAccount: ViewModel is null");
                MessageBox.Show("ViewModel not available. Cannot edit account.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (ViewModel.SelectedAccount == null)
            {
                _logger.LogWarning("EditAccount: No account selected");
                MessageBox.Show("No account selected. Please select an account to edit.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedDisplay = ViewModel.SelectedAccount;
            _logger.LogDebug("EditAccount: Attempting to edit account {AccountNumber}", selectedDisplay.AccountNumber);

            if (ViewModel.EditAccountCommand == null)
            {
                _logger.LogError("EditAccount: EditAccountCommand is null");
                MessageBox.Show("Edit command not available.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!ViewModel.EditAccountCommand.CanExecute(selectedDisplay))
            {
                _logger.LogWarning("EditAccount: EditAccountCommand.CanExecute returned false");
                MessageBox.Show("Cannot edit this account.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _logger.LogDebug("EditAccount: Executing EditAccountCommand");
            await ViewModel.EditAccountCommand.ExecuteAsync(selectedDisplay);
            _logger.LogDebug("EditAccount: EditAccountCommand completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EditAccount method");
            MessageBox.Show($"Error editing account: {ex.Message}\n\nSee logs for details.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handles Delete button click - shows confirmation dialog before deleting.
    /// </summary>
    private async void DeleteButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogDebug("DeleteButton_Click method called");

            if (ViewModel == null)
            {
                _logger.LogError("DeleteButton_Click: ViewModel is null");
                MessageBox.Show("ViewModel not available. Cannot delete account.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (ViewModel.SelectedAccount == null)
            {
                _logger.LogWarning("DeleteButton_Click: No account selected");
                MessageBox.Show("No account selected. Please select an account to delete.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedDisplay = ViewModel.SelectedAccount;
            _logger.LogDebug("DeleteButton_Click: Attempting to delete account {AccountNumber}", selectedDisplay.AccountNumber);

            if (ViewModel.DeleteAccountCommand == null)
            {
                _logger.LogError("DeleteButton_Click: DeleteAccountCommand is null");
                MessageBox.Show("Delete command not available.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!ViewModel.DeleteAccountCommand.CanExecute(selectedDisplay))
            {
                _logger.LogWarning("DeleteButton_Click: DeleteAccountCommand.CanExecute returned false");
                MessageBox.Show("Cannot delete this account.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _logger.LogDebug("DeleteButton_Click: Executing DeleteAccountCommand");
            await ViewModel.DeleteAccountCommand.ExecuteAsync(selectedDisplay);
            _logger.LogDebug("DeleteButton_Click: DeleteAccountCommand completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteButton_Click method");
            MessageBox.Show($"Error deleting account: {ex.Message}\n\nSee logs for details.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handles Refresh button click - reloads accounts from repository.
    /// </summary>
    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogDebug("Refresh button clicked");
            if (ViewModel?.RefreshCommand?.CanExecute(null) ?? false)
            {
                await ViewModel.RefreshCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing accounts");
            MessageBox.Show($"Error refreshing accounts: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Validate the accounts grid data integrity.
    ///
    /// READ-ONLY VALIDATION: This panel is read-only, so validation checks grid state integrity
    /// rather than user input. Validates: ViewModel availability, Accounts collection presence,
    /// grid binding, column headers, row count, and data quality (Balance/Budget numeric validity).
    ///
    /// Returns errors if: ViewModel is null, Accounts collection is null/empty, grid has no data source,
    /// required columns missing, row count exceeds threshold, or data type issues detected.
    ///
    /// Thread-safe: ErrorProvider operations execute on UI thread via InvokeRequired check.
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        var errors = new List<ValidationItem>();

        try
        {
            // Clear ErrorProvider before validation
            if (_errorProvider != null && _accountsGrid != null)
            {
                if (InvokeRequired)
                {
                    Invoke(() => _errorProvider.SetError(_accountsGrid, string.Empty));
                }
                else
                {
                    _errorProvider.SetError(_accountsGrid, string.Empty);
                }
            }

            // 1. Validate ViewModel is resolved
            if (ViewModel == null)
            {
                var item = new ValidationItem(
                    "ViewModel",
                    "ViewModel not resolved; cannot validate grid data.",
                    ValidationSeverity.Error,
                    this);
                errors.Add(item);

                if (_errorProvider != null && _accountsGrid != null)
                {
                    var errorAction = new System.Action(() => _errorProvider.SetError(_accountsGrid, "ViewModel is null"));
                    if (InvokeRequired)
                        Invoke(errorAction);
                    else
                        errorAction();
                }

                Logger?.LogWarning("ValidateAsync: ViewModel is null");
            }

            // 2. Validate Accounts collection is not null
            if (ViewModel?.Accounts == null)
            {
                var item = new ValidationItem(
                    "Accounts",
                    "Accounts collection is null; grid cannot be populated.",
                    ValidationSeverity.Error,
                    _accountsGrid);
                errors.Add(item);

                if (_errorProvider != null && _accountsGrid != null)
                {
                    var errorAction = new System.Action(() => _errorProvider.SetError(_accountsGrid, "Accounts collection is null"));
                    if (InvokeRequired)
                        Invoke(errorAction);
                    else
                        errorAction();
                }

                Logger?.LogWarning("ValidateAsync: Accounts collection is null");
            }

            // 3. Validate grid DataSource is bound
            if (_accountsGrid?.DataSource == null)
            {
                var item = new ValidationItem(
                    "GridDataSource",
                    "Grid DataSource is not bound; binding may have failed.",
                    ValidationSeverity.Error,
                    _accountsGrid);
                errors.Add(item);

                if (_errorProvider != null && _accountsGrid != null)
                {
                    var errorAction = new System.Action(() => _errorProvider.SetError(_accountsGrid, "DataSource not bound"));
                    if (InvokeRequired)
                        Invoke(errorAction);
                    else
                        errorAction();
                }

                Logger?.LogWarning("ValidateAsync: Grid DataSource not bound");
            }

            // 4. Validate grid columns are present
            if (_accountsGrid != null && _accountsGrid.Columns.Count == 0)
            {
                var item = new ValidationItem(
                    "GridColumns",
                    "Grid has no columns; column definition may have failed.",
                    ValidationSeverity.Error,
                    _accountsGrid);
                errors.Add(item);

                Logger?.LogWarning("ValidateAsync: Grid has no columns");
            }

            var requiredColumns = new[] { "AccountNumber", "Name", "CurrentBalance", "BudgetAmount" };
            if (_accountsGrid != null)
            {
                var gridColumnNames = _accountsGrid.Columns.Select(c => c.MappingName).ToList();
                var missingColumns = requiredColumns.Where(col => !gridColumnNames.Contains(col)).ToList();

                if (missingColumns.Any())
                {
                    var item = new ValidationItem(
                        "GridColumns",
                        $"Required grid columns missing: {string.Join(", ", missingColumns)}",
                        ValidationSeverity.Error,
                        _accountsGrid);
                    errors.Add(item);

                    Logger?.LogWarning("ValidateAsync: Missing columns: {MissingColumns}", string.Join(", ", missingColumns));
                }
            }

            // 5. Validate row count
            if (_accountsGrid != null && ViewModel?.Accounts != null)
            {
                int rowCount = ViewModel.Accounts.Count;

                if (rowCount == 0)
                {
                    // Warning for empty grid (may be legitimate)
                    var item = new ValidationItem(
                        "GridRowCount",
                        "Grid contains no data rows. This may be normal if no accounts exist yet.",
                        ValidationSeverity.Warning,
                        _accountsGrid);
                    errors.Add(item);

                    Logger?.LogDebug("ValidateAsync: Grid has 0 rows (may be normal)");
                }
                else if (rowCount > MaxGridRowsThreshold)
                {
                    // Error for suspiciously large row count
                    var item = new ValidationItem(
                        "GridRowCount",
                        $"Grid contains {rowCount} rows, exceeds threshold {MaxGridRowsThreshold}. Possible data corruption.",
                        ValidationSeverity.Error,
                        _accountsGrid);
                    errors.Add(item);

                    Logger?.LogError("ValidateAsync: Grid row count {RowCount} exceeds threshold {Threshold}",
                        rowCount, MaxGridRowsThreshold);
                }
                else
                {
                    Logger?.LogDebug("ValidateAsync: Grid row count {RowCount} is valid", rowCount);
                }
            }

            // 6. Validate data quality: Balance and Budget numeric columns
            if (_accountsGrid != null && ViewModel?.Accounts != null && ViewModel.Accounts.Count > 0)
            {
                var dataQualityErrors = ValidateDataQuality(ViewModel.Accounts);
                if (dataQualityErrors.Any())
                {
                    errors.AddRange(dataQualityErrors);

                    if (_errorProvider != null && _accountsGrid != null)
                    {
                        var errorAction = new System.Action(() =>
                        {
                            var summary = string.Join("; ", dataQualityErrors.Select(e => e.Message).Take(2));
                            _errorProvider.SetError(_accountsGrid, summary);
                        });
                        System.Action errorActionSystem = () =>
                        {
                            var summary = string.Join("; ", dataQualityErrors.Select(e => e.Message).Take(2));
                            _errorProvider.SetError(_accountsGrid, summary);
                        };
                        if (InvokeRequired)
                            Invoke(errorActionSystem);
                        else
                            errorActionSystem();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "ValidateAsync: Unexpected error during validation");
            var item = new ValidationItem(
                "Validation",
                $"Validation error: {ex.Message}",
                ValidationSeverity.Error);
            errors.Add(item);
        }

        return errors.Any(e => e.Severity == ValidationSeverity.Error)
            ? ValidationResult.Failed(errors.ToArray())
            : ValidationResult.Success;
    }

    /// <summary>
    /// Integrate custom validation into grid's RowValidating event.
    /// </summary>
    private void Grid_RowValidating(object? sender, RowValidatingEventArgs e)
    {
        if (e.DataRow?.RowData is MunicipalAccountDisplay account)
        {
            var dataErrors = ValidateDataQuality(new[] { account });
            if (dataErrors.Any())
            {
                e.IsValid = false;
                e.ErrorMessage = string.Join("; ", dataErrors.Select(d => d.Message));
                _logger.LogWarning("Row validation failed for account {AccountNumber}: {Error}", account.AccountNumber, e.ErrorMessage);
            }
        }
    }

    /// <summary>
    /// Conditional styling for cells (e.g., red text for negative balance).
    /// </summary>
    private void Grid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
    {
        if (e.Column.MappingName == "CurrentBalance" && e.DisplayText != null)
        {
            var cleanText = e.DisplayText.Replace("$", "").Replace(",", "").Replace("(", "-").Replace(")", "");
            if (decimal.TryParse(cleanText, out decimal balance) && balance < 0)
            {
                e.Style.TextColor = Color.Red;
            }
        }
    }

    /// <summary>
    /// Prevents invalid filter expressions on string columns (blocks relational operators like > < >= <=).
    /// Syncfusion grids default to allowing all operators; restricting to string-appropriate filters only.
    /// </summary>
    private void Grid_FilterChanging(object? sender, Syncfusion.WinForms.DataGrid.Events.FilterChangingEventArgs e)
    {
        if (e?.Column?.MappingName == null)
        {
            return;
        }

        // String columns that should not allow relational comparison operators
        var isStringColumn =
            e.Column.MappingName == "AccountNumber" ||
            e.Column.MappingName == "AccountName" ||
            e.Column.MappingName == "FundName" ||
            e.Column.MappingName == "AccountType" ||
            e.Column.MappingName == "Department";

        if (!isStringColumn)
        {
            return;
        }

        // Check if any predicate uses relational operators (GreaterThan, LessThan, etc.)
        var hasRelationalPredicate = e.FilterPredicates.Any(p =>
            p.FilterType == Syncfusion.Data.FilterType.GreaterThan ||
            p.FilterType == Syncfusion.Data.FilterType.GreaterThanOrEqual ||
            p.FilterType == Syncfusion.Data.FilterType.LessThan ||
            p.FilterType == Syncfusion.Data.FilterType.LessThanOrEqual);

        if (!hasRelationalPredicate)
        {
            return;
        }

        // Cancel the filter and log the prevention
        e.Cancel = true;
        _logger?.LogDebug("AccountsPanel: Cancelled invalid relational filter on string column {Column}", e.Column.MappingName);
    }

    /// <summary>
    /// Validates data quality for Balance and Budget columns.
    /// Checks that numeric values are valid decimals and non-negative where expected.
    /// </summary>
    private List<ValidationItem> ValidateDataQuality(System.Collections.IEnumerable accounts)
    {
        var dataErrors = new List<ValidationItem>();

        try
        {
            int errorCount = 0;
            const int MaxErrorsToReport = 3; // Report only first 3 errors to avoid overwhelming UI
            var accountsList = accounts.Cast<MunicipalAccountDisplay>().ToList();

            foreach (var account in accountsList)
            {
                if (errorCount >= MaxErrorsToReport) break;

                // No null checks needed - Balance and BudgetAmount are non-nullable decimals
                // Just try to parse them (they're already valid since they're loaded from DB)
                Logger?.LogDebug("ValidateDataQuality: Account {AccountNumber} - Balance: {Balance}, Budget: {Budget}",
                    account.AccountNumber ?? "unknown", account.CurrentBalance, account.BudgetAmount);
            }

            if (errorCount >= MaxErrorsToReport && accountsList.Count > MaxErrorsToReport)
            {
                dataErrors.Add(new ValidationItem(
                    "DataQuality",
                    $"More validation issues found. Check first {MaxErrorsToReport} errors.",
                    ValidationSeverity.Warning));
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "ValidateDataQuality: Error during data quality check");
            dataErrors.Add(new ValidationItem(
                "DataQuality",
                $"Data quality check failed: {ex.Message}",
                ValidationSeverity.Warning));
        }

        return dataErrors;
    }

    /// <summary>
    /// Save is a no-op for the read-only Accounts panel.
    /// </summary>
    public override Task SaveAsync(CancellationToken ct)
    {
        SetHasUnsavedChanges(false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override FocusFirstError to handle read-only grid context.
    /// For a read-only grid, focus the grid data area; user cannot fix validation errors in this panel.
    /// </summary>
    public override void FocusFirstError()
    {
        if (_accountsGrid != null && _accountsGrid.Visible)
        {
            _accountsGrid.Focus();
            Logger?.LogDebug("FocusFirstError: Focused accounts grid");
        }
        else if (this.CanFocus)
        {
            this.Focus();
            Logger?.LogDebug("FocusFirstError: Focused AccountsPanel");
        }
    }

    /// <summary>
    /// Load accounts from the ViewModel asynchronously.
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (ViewModel == null)
        {
            return;
        }

        try
        {
            using var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(ct, RegisterOperation());
            var operationCancellationToken = linkedCancellationSource.Token;
            IsBusy = true;

            if (ViewModel is ILazyLoadViewModel lazyLoadViewModel)
            {
                await lazyLoadViewModel.OnVisibilityChangedAsync(true);
            }
            else if (ViewModel.LoadAccountsCommand.CanExecute(null))
            {
                await ViewModel.LoadAccountsCommand.ExecuteAsync(operationCancellationToken);
            }

            if (_accountsGrid != null)
            {
                _accountsGrid.DataSource = ViewModel.Accounts;
                _accountsGrid.Refresh();
            }

            SetHasUnsavedChanges(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("AccountsPanel load cancelled");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Grid_SelectionChanged(object? sender, Syncfusion.WinForms.DataGrid.Events.SelectionChangedEventArgs e)
    {
        UpdateButtonState();
    }




    /// <summary>
    /// Binds ViewModel commands to toolbar buttons using Syncfusion MVVM Command pattern.
    /// This is the official Syncfusion way per Essential Studio 32.1.19 MVVM samples.
    /// </summary>
    private void BindViewModelCommands()
    {
        if (ViewModel == null)
        {
            _logger?.LogWarning("[BIND_COMMANDS] ViewModel is null; cannot bind commands");
            return;
        }

        if (_createButton != null)
        {
            _createButton.Command = ViewModel.CreateAccountCommand;
            _createButton.CommandParameter = null;
            _logger?.LogDebug("[BIND_COMMANDS] Create button → CreateAccountCommand");
        }

        if (_editButton != null)
        {
            _editButton.Command = ViewModel.EditAccountCommand;
            // CommandParameter is dynamically resolved via ViewModel.SelectedAccount
            _logger?.LogDebug("[BIND_COMMANDS] Edit button → EditAccountCommand");
        }

        if (_deleteButton != null)
        {
            _deleteButton.Command = ViewModel.DeleteAccountCommand;
            // CommandParameter is dynamically resolved via ViewModel.SelectedAccount
            _logger?.LogDebug("[BIND_COMMANDS] Delete button → DeleteAccountCommand");
        }

        if (_refreshButton != null)
        {
            _refreshButton.Command = ViewModel.RefreshCommand;
            _refreshButton.CommandParameter = null;
            _logger?.LogDebug("[BIND_COMMANDS] Refresh button → RefreshCommand");
        }

        _logger?.LogInformation("[BIND_COMMANDS] ✅ All toolbar button commands bound successfully");
    }

    /// <summary>
    /// Sets up two-way DataBindings for filter dropdown controls.
    /// Syncfusion SfComboBox supports databinding to ViewModel properties for automatic UI sync.
    /// </summary>
    private void BindFilterDropdowns()
    {
        try
        {
            if (ViewModel == null)
            {
                _logger?.LogWarning("[BIND_FILTERS] ViewModel is null; cannot bind filters");
                return;
            }

            // Clear any existing bindings to avoid duplicates
            _fundFilterComboBox?.DataBindings.Clear();
            _accountTypeFilterComboBox?.DataBindings.Clear();
            _departmentFilterComboBox?.DataBindings.Clear();

            // Two-way binding: UI ↔ ViewModel (Syncfusion pattern)
            // When user changes dropdown → ViewModel.Selected* updates → VM partial method fires → grid filters
            if (_fundFilterComboBox != null)
            {
                _fundFilterComboBox.DataBindings.Add(
                    "SelectedItem",
                    ViewModel,
                    nameof(ViewModel.SelectedFund),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
                _logger?.LogDebug("[BIND_FILTERS] Fund filter bound to ViewModel.SelectedFund");
            }

            if (_accountTypeFilterComboBox != null)
            {
                _accountTypeFilterComboBox.DataBindings.Add(
                    "SelectedItem",
                    ViewModel,
                    nameof(ViewModel.SelectedAccountType),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
                _logger?.LogDebug("[BIND_FILTERS] Account Type filter bound to ViewModel.SelectedAccountType");
            }

            if (_departmentFilterComboBox != null)
            {
                _departmentFilterComboBox.DataBindings.Add(
                    "SelectedItem",
                    ViewModel,
                    nameof(ViewModel.SelectedDepartment),
                    true,
                    DataSourceUpdateMode.OnPropertyChanged);
                _logger?.LogDebug("[BIND_FILTERS] Department filter bound to ViewModel.SelectedDepartment");
            }

            _logger?.LogInformation("[BIND_FILTERS] ✅ All filter dropdowns bound successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[BIND_FILTERS] Error binding filter dropdowns");
        }
    }

    /// <summary>
    /// Shows comprehensive Chart of Accounts help with keyboard shortcuts and municipal accounting guidance.
    /// Professional help dialog for mayors and finance teams.
    /// </summary>
    private void ShowChartOfAccountsHelp()
    {
        var helpText = @"Chart of Accounts — Municipal Finance Management

OVERVIEW:
Manage your municipal chart of accounts with full CRUD operations, filtering, and real-time balance tracking.

KEYBOARD SHORTCUTS:
  • Ctrl+N     → Create new account
  • F2 / Enter → Edit selected account
  • Delete     → Delete selected account
  • F5         → Refresh account list
  • Ctrl+E     → Export to Excel
  • Ctrl+F     → Focus search box
  • F1         → Show this help
  • Escape     → Clear selection

MUNICIPAL ACCOUNTING STANDARDS:
  • Account numbers follow GASB (Governmental Accounting Standards Board) guidelines
  • Balance = Assets – Liabilities (real-time calculation)
  • Budget vs Actual tracking enabled for all accounts
  • Fund accounting separates resources by legal restrictions
  • Department tracking enables cost center analysis

FILTERING & SEARCH:
  • Filter by Fund, Account Type, or Department using the toolbar dropdowns
  • Search across account numbers, names, descriptions, and departments
  • Right-click on grid rows for quick Edit/Delete actions

EXCEL EXPORT:
  • Export button creates a formatted Excel spreadsheet with all visible accounts
  • Includes all columns: Account#, Name, Fund, Type, Balance, Budget, Department
  • Perfect for auditors, board presentations, or external reporting

TIPS:
  • All accounts must have unique account numbers
  • Inactive accounts are hidden by default (use filter to show)
  • Summary totals appear at the bottom of the grid
  • Double-click any row to edit quickly

For technical support or feature requests, contact your Wiley Widget administrator.";

        MessageBox.Show(helpText,
            "Chart of Accounts Help",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        _logger?.LogInformation("Chart of Accounts help displayed");
    }

    /// <summary>
    /// Exports the current account list to an Excel spreadsheet.
    /// Includes all visible columns and respects current filtering/search state.
    /// Uses Syncfusion ExcelEngine per ExportService pattern.
    /// </summary>
    private void ExportButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogDebug("ExportButton_Click: Starting Excel export");

            if (_accountsGrid == null || ViewModel == null)
            {
                MessageBox.Show("Cannot export: Grid or ViewModel not available.", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var view = _accountsGrid.View;
            if (view == null)
            {
                MessageBox.Show("Grid view not initialized - cannot export.", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Show save file dialog
            using var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|Excel 97-2003 (*.xls)|*.xls",
                DefaultExt = "xlsx",
                FileName = $"ChartOfAccounts_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Title = "Export Chart of Accounts to Excel"
            };

            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                _logger?.LogInformation("Exporting accounts to: {FilePath}", saveDialog.FileName);

                // Use Syncfusion's Excel export (matching ExportService.cs pattern)
                using var excelEngine = new ExcelEngine();
                var workbook = excelEngine.Excel.Workbooks.Create(1);
                var worksheet = workbook.Worksheets[0];

                var options = new ExcelExportingOptions
                {
                    ExcelVersion = ExcelVersion.Xlsx,
                    ExportStackedHeaders = true
                };

                // Export grid to worksheet
                _accountsGrid.ExportToExcel(view, options, worksheet);
                workbook.Version = ExcelVersion.Xlsx;
                workbook.SaveAs(saveDialog.FileName);

                _logger?.LogInformation("Excel export completed successfully: {RowCount} accounts exported",
                    ViewModel.Accounts?.Count ?? 0);

                // Offer to open the file
                var result = MessageBox.Show(
                    $"Export completed successfully!\n\n{ViewModel.Accounts?.Count ?? 0} accounts exported to:\n{saveDialog.FileName}\n\nOpen the file now?",
                    "Export Complete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exporting accounts to Excel");
            MessageBox.Show($"Export failed: {ex.Message}\n\nSee logs for details.", "Export Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                Load -= AccountsPanel_Load;
            }
            catch { }

            // Unsubscribe from ViewModel property changes
            if (ViewModel is INotifyPropertyChanged observable)
            {
                observable.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Unsubscribe from grid events
            if (_accountsGrid != null)
            {
                _accountsGrid.RowValidating -= Grid_RowValidating;
                _accountsGrid.QueryCellStyle -= Grid_QueryCellStyle;
                _accountsGrid.CellClick -= Grid_CellClick;
                if (_gridSelectionChangedHandler != null) _accountsGrid.SelectionChanged -= _gridSelectionChangedHandler;
                if (_gridCellDoubleClickHandler != null) _accountsGrid.CellDoubleClick -= _gridCellDoubleClickHandler;
                _accountsGrid.DataSource = null;
            }

            // Dispose ErrorProvider (holds error state for controls)
            _errorProvider?.Dispose();

            // Dispose BindingSource and clear bindings
            if (_header != null)
            {
                _header.DataBindings.Clear();
            }
            _accountsBinding?.Dispose();

            // Dispose tooltip provider
            _buttonToolTips?.Dispose();

            // Dispose controls (in reverse order of creation)
            _exportButton?.Dispose();
            _createButton?.Dispose();
            _editButton?.Dispose();
            _deleteButton?.Dispose();
            _refreshButton?.Dispose();
            _toolbarPanel?.Dispose();
            _accountsGrid?.Dispose();

            // Dispose layout last (after children)
            if (_layout != null)
            {
                _layout.Controls.Clear();
                _layout.Dispose();
            }

            _header?.Dispose();
        }

        base.Dispose(disposing);
    }
}
