using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GridCheckBoxColumn = Syncfusion.WinForms.DataGrid.GridCheckBoxColumn;
using GridNumericColumn = Syncfusion.WinForms.DataGrid.GridNumericColumn;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using GradientPanelExt = Syncfusion.Windows.Forms.Tools.GradientPanelExt;
using CheckBoxAdv = Syncfusion.Windows.Forms.Tools.CheckBoxAdv;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using Syncfusion.Drawing;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Styles;
using TextBoxExt = Syncfusion.Windows.Forms.Tools.TextBoxExt;
using WileyWidget.Models;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for budget management with full CRUD operations, filtering, analysis, and export capabilities.
/// Features budget entry management, variance analysis, fiscal year management, and reporting.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class BudgetPanel : ScopedPanelBase<BudgetViewModel>
{

    // UI Controls
    private SfDataGrid? _budgetGrid;
    private SfButton? _loadBudgetsButton;
    private SfButton? _addEntryButton;
    private SfButton? _editEntryButton;
    private SfButton? _deleteEntryButton;
    private SfButton? _importCsvButton;
    private SfButton? _exportCsvButton;
    private SfButton? _exportPdfButton;
    private SfButton? _exportExcelButton;
    private TextBoxExt? _searchTextBox;
    private SfComboBox? _fiscalYearComboBox;
    private SfComboBox? _entityComboBox;
    private SfComboBox? _departmentComboBox;
    private SfComboBox? _fundTypeComboBox;
    private TextBoxExt? _varianceThresholdTextBox;
    private CheckBoxAdv? _overBudgetCheckBox;
    private CheckBoxAdv? _underBudgetCheckBox;
    private Label? _totalBudgetedLabel;
    private Label? _totalActualLabel;
    private Label? _totalVarianceLabel;
    private Label? _percentUsedLabel;
    private Label? _entriesOverBudgetLabel;
    private Label? _entriesUnderBudgetLabel;
    private GradientPanelExt? _summaryPanel;
    private GradientPanelExt? _gridPanel;
    private GradientPanelExt? _filterPanel;
    private GradientPanelExt? _buttonPanel;
    private SplitContainer? _mainSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    // Embedded CSV mapping wizard for advanced imports
    private CsvMappingWizardPanel? _mappingWizardPanel;
    private Panel? _mappingContainer;

    // Event handlers for proper cleanup
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderCloseHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetPanel"/> class.
    /// </summary>
    /// <param name="scopeFactory">Service scope factory for creating scoped dependencies.</param>
    /// <param name="logger">Logger instance for diagnostic logging.</param>
    public BudgetPanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<BudgetViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
        InitializeControls();
        ApplySyncfusionTheme();

        // Defer sizing validation until handle is created
        DeferSizeValidation();

        Logger.LogInformation("BudgetPanel initialized");
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

    /// <summary>
    /// Parameterless constructor for designer support only.
    /// DO NOT USE - use DI constructor instead.
    /// </summary>
    public BudgetPanel() : this(
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(Program.Services),
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ScopedPanelBase<BudgetViewModel>>>(Program.Services))
    {
    }

    private void InitializeControls()
    {
        if (ViewModel == null)
        {
            Logger.LogWarning("InitializeControls called with null ViewModel");
            return;
        }

        // Set up form properties
        Text = "Budget Management";
        Size = new Size(1400, 900);
        MinimumSize = new Size(1000, 600);

        // Panel header with actions
        _panelHeader = new PanelHeader { Dock = DockStyle.Top, Title = "Budget Management & Analysis" };
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Main split container
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        SafeSplitterDistanceHelper.TrySetSplitterDistance(_mainSplitContainer, 150);

        // Top panel - Summary and filters
        InitializeTopPanel();

        // Bottom panel - Budget grid and actions
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
        _loadingOverlay = new LoadingOverlay { Message = "Loading budget data..." };
        Controls.Add(_loadingOverlay);

        // No data overlay
        _noDataOverlay = new NoDataOverlay { Message = "No budget entries yet\r\nCreate a new budget to get started" };
        Controls.Add(_noDataOverlay);

        Controls.Add(_mainSplitContainer);
        Controls.Add(_statusStrip);

        // Theme changes handled by SfSkinManager cascade

        // Set tab order
        SetTabOrder();
    }

    private void InitializeTopPanel()
    {
        var themeName = ThemeColors.CurrentTheme;

        var topPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(topPanel, themeName);

        // Summary panel
        _summaryPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_summaryPanel, themeName);

        var summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2
        };

        for (int i = 0; i < 6; i++)
            summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.67f));

        _totalBudgetedLabel = new Label
        {
            Text = "Total Budgeted: $0.00",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _totalActualLabel = new Label
        {
            Text = "Total Actual: $0.00",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _totalVarianceLabel = new Label
        {
            Text = "Total Variance: $0.00",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _percentUsedLabel = new Label
        {
            Text = "Percent Used: 0.00%",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _entriesOverBudgetLabel = new Label
        {
            Text = "Over Budget: 0",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _entriesUnderBudgetLabel = new Label
        {
            Text = "Under Budget: 0",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        summaryTable.Controls.Add(_totalBudgetedLabel, 0, 0);
        summaryTable.Controls.Add(_totalActualLabel, 1, 0);
        summaryTable.Controls.Add(_totalVarianceLabel, 2, 0);
        summaryTable.Controls.Add(_percentUsedLabel, 3, 0);
        summaryTable.Controls.Add(_entriesOverBudgetLabel, 4, 0);
        summaryTable.Controls.Add(_entriesUnderBudgetLabel, 5, 0);

        _summaryPanel.Controls.Add(summaryTable);
        topPanel.Controls.Add(_summaryPanel);

        // Filter panel
        _filterPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_filterPanel, themeName);

        var filterGroup = new GradientPanelExt
        {
            Text = "Filters",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(filterGroup, themeName);

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 3
        };

        for (int i = 0; i < 6; i++)
            filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.67f));

        // Row 1: Search and Fiscal Year
        var searchLabel = new Label
        {
            Text = "Search:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _searchTextBox = new TextBoxExt
        {
            Dock = DockStyle.Fill,
            TabIndex = 1,
            AccessibleName = "Search Budget Entries",
            AccessibleDescription = "Search budget entries by account, description, or department"
        };
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;

        var fiscalYearLabel = new Label
        {
            Text = "Fiscal Year:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _fiscalYearComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 2,
            AccessibleName = "Fiscal Year Filter",
            AccessibleDescription = "Filter budget entries by fiscal year",
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
        };

        // Populate fiscal years
        var years = new List<int>();
        for (int year = DateTime.Now.Year - 5; year <= DateTime.Now.Year + 5; year++)
        {
            years.Add(year);
        }
        _fiscalYearComboBox.DataSource = years;
        _fiscalYearComboBox.SelectedItem = DateTime.Now.Year;
        _fiscalYearComboBox.SelectedIndexChanged += FiscalYearComboBox_SelectedIndexChanged;

        var entityLabel = new Label
        {
            Text = "Entity:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _entityComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 8,
            AccessibleName = "Entity Filter",
            AccessibleDescription = "Filter budget entries by entity or fund",
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
        };

        // Placeholder until data is loaded
        _entityComboBox.DataSource = new List<string> { "All Entities" };
        _entityComboBox.SelectedIndexChanged += EntityComboBox_SelectedIndexChanged;

        // Add entity controls to the filter table (columns 4 and 5)
        filterTable.Controls.Add(entityLabel, 4, 0);
        filterTable.Controls.Add(_entityComboBox, 5, 0);

        // Row 2: Department and Fund Type
        var departmentLabel = new Label
        {
            Text = "Department:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _departmentComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 3,
            AccessibleName = "Department Filter",
            AccessibleDescription = "Filter budget entries by department",
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
        };
        _departmentComboBox.DataSource = new List<string> { "All Departments" };
        _departmentComboBox.SelectedIndex = 0;
        _departmentComboBox.SelectedIndexChanged += DepartmentComboBox_SelectedIndexChanged;

        var fundTypeLabel = new Label
        {
            Text = "Fund Type:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _fundTypeComboBox = new SfComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 4,
            AccessibleName = "Fund Type Filter",
            AccessibleDescription = "Filter budget entries by fund type",
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
        };
        var fundTypes = Enum.GetNames(typeof(FundType)).ToList();
        fundTypes.Insert(0, "All Fund Types");
        _fundTypeComboBox.DataSource = fundTypes;
        _fundTypeComboBox.SelectedIndex = 0;
        _fundTypeComboBox.SelectedIndexChanged += FundTypeComboBox_SelectedIndexChanged;

        // Row 3: Variance threshold and checkboxes
        var varianceLabel = new Label
        {
            Text = "Variance >:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _varianceThresholdTextBox = new TextBoxExt
        {
            Text = "1000",
            Dock = DockStyle.Fill,
            TabIndex = 5,
            AccessibleName = "Variance Threshold",
            AccessibleDescription = "Filter entries with variance greater than this amount"
        };
        _varianceThresholdTextBox.TextChanged += VarianceThresholdTextBox_TextChanged;

        _overBudgetCheckBox = new CheckBoxAdv
        {
            Text = "Over Budget Only",
            Dock = DockStyle.Fill,
            TabIndex = 6,
            AccessibleName = "Show Over Budget Only",
            AccessibleDescription = "Show only entries that are over budget"
        };
        _overBudgetCheckBox.CheckStateChanged += OverBudgetCheckBox_CheckedChanged;

        _underBudgetCheckBox = new CheckBoxAdv
        {
            Text = "Under Budget Only",
            Dock = DockStyle.Fill,
            TabIndex = 7,
            AccessibleName = "Show Under Budget Only",
            AccessibleDescription = "Show only entries that are under budget"
        };
        _underBudgetCheckBox.CheckStateChanged += UnderBudgetCheckBox_CheckedChanged;

        filterTable.Controls.Add(searchLabel, 0, 0);
        filterTable.Controls.Add(_searchTextBox, 1, 0);
        filterTable.Controls.Add(fiscalYearLabel, 2, 0);
        filterTable.Controls.Add(_fiscalYearComboBox, 3, 0);
        filterTable.Controls.Add(departmentLabel, 0, 1);
        filterTable.Controls.Add(_departmentComboBox, 1, 1);
        filterTable.Controls.Add(fundTypeLabel, 2, 1);
        filterTable.Controls.Add(_fundTypeComboBox, 3, 1);
        filterTable.Controls.Add(varianceLabel, 0, 2);
        filterTable.Controls.Add(_varianceThresholdTextBox, 1, 2);
        filterTable.Controls.Add(_overBudgetCheckBox, 2, 2);
        filterTable.Controls.Add(_underBudgetCheckBox, 3, 2);

        filterGroup.Controls.Add(filterTable);
        _filterPanel!.Controls.Add(filterGroup);
        topPanel.Controls.Add(_filterPanel);

        _mainSplitContainer.Panel1.Controls.Add(topPanel);
    }

    private void InitializeBottomPanel()
    {
        var themeName = ThemeColors.CurrentTheme;
        var bottomPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(bottomPanel, themeName);

        // Budget grid
        _gridPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_gridPanel, themeName);

        _budgetGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = true,
            AllowResizingColumns = true,
            AllowSorting = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
            SelectionMode = GridSelectionMode.Single,
            EditMode = EditMode.SingleClick,
            TabIndex = 8,
            AccessibleName = "Budget Entries Grid"
        };

        // Configure grid columns
        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountNumber",
            HeaderText = "Account Number",
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountName",
            HeaderText = "Account Name",
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "DepartmentName",
            HeaderText = "Department",
            AllowEditing = true
        });

        // Entity / Fund column
        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "EntityName",
            HeaderText = "Entity",
            AllowEditing = false
        });

        // Keep fund type description for quick reference
        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "FundTypeDescription",
            HeaderText = "Fund Type",
            AllowEditing = false
        });

        // Totals (combined across entities)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "BudgetedAmount",
            HeaderText = "Total Budgeted",
            Format = "C2",
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "ActualAmount",
            HeaderText = "Total Actual",
            Format = "C2",
            AllowEditing = true
        });

        // Percentage of budget (Actual / Budget) - show as percent (P-format)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "PercentOfBudgetFraction",
            HeaderText = "% of Budget",
            Format = "P2",
            AllowEditing = false
        });

        // Town of Wiley specific columns (TOW)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TownOfWileyBudgetedAmount",
            HeaderText = "TOW Budgeted",
            Format = "C2",
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TownOfWileyActualAmount",
            HeaderText = "TOW Actual",
            Format = "C2",
            AllowEditing = false
        });

        // Wiley Sanitation District specific columns (WSD)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "WsdBudgetedAmount",
            HeaderText = "WSD Budgeted",
            Format = "C2",
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "WsdActualAmount",
            HeaderText = "WSD Actual",
            Format = "C2",
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "EncumbranceAmount",
            HeaderText = "Encumbrance",
            Format = "C2",
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "VarianceAmount",
            HeaderText = "Variance",
            Format = "C2",
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "VariancePercentage",
            HeaderText = "Variance %",
            Format = "P2",
            AllowEditing = false
        });
        // Percent of Budget (fraction property) - displays as percentage
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "PercentOfBudgetFraction",
            HeaderText = "% of Budget",
            Format = "P2",
            AllowEditing = false
        });

        _budgetGrid.CurrentCellActivated += BudgetGrid_CurrentCellActivated;

        // Set AutoGenerateColumns explicitly to false
        _budgetGrid.AutoGenerateColumns = false;

        _gridPanel.Controls.Add(_budgetGrid);
        bottomPanel.Controls.Add(_gridPanel);

        // Embedded mapping wizard container (hidden by default)
        _mappingContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 260,
            Visible = false
        };

        _mappingWizardPanel = new CsvMappingWizardPanel { Dock = DockStyle.Fill };
        _mappingWizardPanel.MappingApplied += MappingWizardPanel_MappingApplied;
        _mappingWizardPanel.Cancelled += MappingWizardPanel_Cancelled;
        _mappingContainer.Controls.Add(_mappingWizardPanel);
        bottomPanel.Controls.Add(_mappingContainer);

        // Button panel
        _buttonPanel = new GradientPanelExt
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, themeName);

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1
        };

        for (int i = 0; i < 8; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));

        _loadBudgetsButton = new SfButton
        {
            Text = "&Load Budgets",
            TabIndex = 9,
            AccessibleName = "Load Budgets",
            AccessibleDescription = "Load budget entries for the selected fiscal year"
        };
        _loadBudgetsButton.Click += async (s, e) =>
        {
            if (ViewModel != null)
                await ViewModel.LoadBudgetsCommand.ExecuteAsync(null);
        };

        _addEntryButton = new SfButton
        {
            Text = "&Add Entry",
            TabIndex = 10,
            AccessibleName = "Add Entry",
            AccessibleDescription = "Add a new budget entry"
        };
        _addEntryButton.Click += AddEntryButton_Click;

        _editEntryButton = new SfButton
        {
            Text = "&Edit Entry",
            TabIndex = 11,
            AccessibleName = "Edit Entry",
            AccessibleDescription = "Edit the selected budget entry"
        };
        _editEntryButton.Click += EditEntryButton_Click;

        _deleteEntryButton = new SfButton
        {
            Text = "&Delete Entry",
            TabIndex = 12,
            AccessibleName = "Delete Entry",
            AccessibleDescription = "Delete the selected budget entry"
        };
        _deleteEntryButton.Click += async (s, e) => await DeleteEntryAsync();

        _importCsvButton = new SfButton
        {
            Text = "&Import CSV",
            TabIndex = 13,
            AccessibleName = "Import CSV",
            AccessibleDescription = "Import budget entries from CSV file"
        };
        _importCsvButton.Click += ImportCsvButton_Click;

        _exportCsvButton = new SfButton
        {
            Text = "Export &CSV",
            TabIndex = 14,
            AccessibleName = "Export CSV",
            AccessibleDescription = "Export budget entries to CSV file"
        };
        _exportCsvButton.Click += ExportCsvButton_Click;

        _exportPdfButton = new SfButton
        {
            Text = "Export &PDF",
            TabIndex = 15,
            AccessibleName = "Export PDF",
            AccessibleDescription = "Export budget entries to PDF file"
        };
        _exportPdfButton.Click += ExportPdfButton_Click;

        _exportExcelButton = new SfButton
        {
            Text = "Export &Excel",
            TabIndex = 16,
            AccessibleName = "Export Excel",
            AccessibleDescription = "Export budget entries to Excel file"
        };
        _exportExcelButton.Click += ExportExcelButton_Click;

        buttonTable.Controls.Add(_loadBudgetsButton, 0, 0);
        buttonTable.Controls.Add(_addEntryButton, 1, 0);
        buttonTable.Controls.Add(_editEntryButton, 2, 0);
        buttonTable.Controls.Add(_deleteEntryButton, 3, 0);
        buttonTable.Controls.Add(_importCsvButton, 4, 0);
        buttonTable.Controls.Add(_exportCsvButton, 5, 0);
        buttonTable.Controls.Add(_exportPdfButton, 6, 0);
        buttonTable.Controls.Add(_exportExcelButton, 7, 0);

        _buttonPanel!.Controls.Add(buttonTable);
        bottomPanel.Controls.Add(_buttonPanel);

        _mainSplitContainer.Panel2.Controls.Add(bottomPanel);
    }

    /// <summary>
    /// Binds ViewModel properties to UI controls.
    /// </summary>
    private void BindViewModel()
    {
        if (ViewModel == null) return;

        // Wire up ViewModel property changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Bind grid data source - ViewModel classes should implement INotifyPropertyChanged
        // for proper ObservableCollection synchronization with SfDataGrid
        if (_budgetGrid != null)
        {
            _budgetGrid.DataSource = ViewModel.FilteredBudgetEntries;
        }

        // Bind entity combo list if available
        if (_entityComboBox != null)
        {
            _entityComboBox.DataSource = ViewModel.AvailableEntities ?? new ObservableCollection<string>(new[] { "All Entities" });
            // Default selection
            if (ViewModel.SelectedEntity == null && ViewModel.AvailableEntities != null && ViewModel.AvailableEntities.Contains("All Entities"))
                _entityComboBox.SelectedItem = "All Entities";
            else
                _entityComboBox.SelectedItem = ViewModel.SelectedEntity;

            // Ensure handler is attached (prevent duplicate handlers)
            _entityComboBox.SelectedIndexChanged -= EntityComboBox_SelectedIndexChanged;
            _entityComboBox.SelectedIndexChanged += EntityComboBox_SelectedIndexChanged;
        }
    }

    /// <summary>
    /// Applies Syncfusion Office2019Colorful theme to SfDataGrid.
    /// </summary>
    private void ApplySyncfusionTheme()
    {
        try
        {
            if (_budgetGrid == null) return;

            // Header styling
            _budgetGrid.Style.HeaderStyle.Font.Bold = true;
            _budgetGrid.Style.HeaderStyle.Font.Size = 9.5f;

            // Selection styling

            // Cell styling
            _budgetGrid.Style.CellStyle.Font.Size = 9f;

            // Add alternate row coloring via QueryCellStyle event
            _budgetGrid.QueryCellStyle += BudgetGrid_QueryCellStyle;

            Logger.LogDebug("Syncfusion theme applied successfully to BudgetPanel grid");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply Syncfusion theme to budget grid");
        }
    }

    /// <summary>
    /// Handles QueryCellStyle event for alternating row colors and variance color coding.
    /// </summary>
    private void BudgetGrid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
    {
        if (e.Column != null && e.DataRow != null)
        {
            // Alternating row styling removed - let SkinManager handle theming



            // Highlight negative variances in red
            if (e.Column.MappingName == "VarianceAmount" && e.DisplayText != null)
            {
                if (decimal.TryParse(e.DisplayText.Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal), out var variance))
                {
                    if (variance < 0)
                    {
                        e.Style.TextColor = Color.Red; // Semantic over-budget
                    }
                    else if (variance > 0)
                    {
                        e.Style.TextColor = Color.Green; // Semantic under-budget
                    }
                }
            }
            // Highlight percent-of-budget >100% in red (e.g., lift-station utilities at 125%)
            if (e.Column.MappingName == "PercentOfBudgetFraction")
            {
                try
                {
                    decimal frac = 0m;
                    if (e.CellValue is decimal d) frac = d;
                    else if (e.CellValue is double db) frac = (decimal)db;
                    else if (e.DisplayText != null && decimal.TryParse(e.DisplayText.Replace("%", "", StringComparison.Ordinal).Trim(), out var parsed))
                        frac = parsed / 100m;

                    if (frac > 1.0m)
                    {
                        e.Style.TextColor = Color.Red;
                    }
                }
                catch
                {
                    // Swallow exceptions to avoid disrupting UI rendering
                }
            }
        }
    }

    /// <summary>
    /// Applies theme to all controls.
    /// </summary>
    private void ApplyTheme(string theme)
    {
        // Theme application now handled by SfSkinManager cascade
        // UpdateButtonIcons removed - icon management via deprecated IThemeIconService is not authorized
    }

    // UpdateButtonIcons method removed - icon management via deprecated IThemeIconService is not authorized

    /// <summary>
    /// Navigates to another panel using PanelNavigationService. Navigation buttons (NavigateToDashboard, NavigateToAnalytics, etc.)
    /// have been systematically removed from BudgetPanel, AnalyticsPanel, and ChartPanel as part of UX refactor.
    /// All inter-panel navigation is now centralized through MainForm ribbon/menu via PanelNavigationService.
    /// This consolidation improves maintainability and provides consistent navigation UX across the application.
    /// </summary>
    private void NavigateToPanel<TPanel>(string panelName) where TPanel : UserControl
    {
        try
        {
            var parentForm = this.FindForm();
            if (parentForm is Forms.MainForm mf)
            {
                mf.ShowPanel<TPanel>(panelName);
                return;
            }

            // Fallback for older hosts
            var method = parentForm?.GetType().GetMethod("DockUserControlPanel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(typeof(TPanel));
                genericMethod.Invoke(parentForm, new object[] { panelName });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Navigation to {PanelName} failed", panelName);
        }
    }

    private void SetTabOrder()
    {
        // Tab order set in control initialization
    }

    /// <summary>
    /// Called when the ViewModel is resolved from the scoped provider so this panel can bind safely.
    /// </summary>
    protected override void OnViewModelResolved(BudgetViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);
        BindViewModel();
    }

    private void BudgetGrid_CurrentCellActivated(object? sender, EventArgs e)
    {
        // Handle grid selection if needed
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
            ViewModel.SearchText = _searchTextBox?.Text ?? string.Empty;
    }

    private void FiscalYearComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _fiscalYearComboBox?.SelectedItem is int year)
        {
            ViewModel.SelectedFiscalYear = year;
        }
    }

    private void EntityComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _entityComboBox?.SelectedItem is string entity && !string.Equals(entity, "All Entities", StringComparison.OrdinalIgnoreCase))
        {
            ViewModel.SelectedEntity = entity;
        }
        else if (ViewModel != null)
        {
            ViewModel.SelectedEntity = null;
        }
    }

    private void DepartmentComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Department filtering logic would go here
    }

    private void FundTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _fundTypeComboBox?.SelectedItem is string fundTypeString &&
            Enum.TryParse<FundType>(fundTypeString, out var fundType))
        {
            ViewModel.SelectedFundType = fundType;
        }
        else if (ViewModel != null)
        {
            ViewModel.SelectedFundType = null;
        }
    }

    private void VarianceThresholdTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && decimal.TryParse(_varianceThresholdTextBox?.Text, out var threshold))
        {
            ViewModel.VarianceThreshold = threshold;
        }
        else if (ViewModel != null)
        {
            ViewModel.VarianceThreshold = null;
        }
    }

    private void OverBudgetCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
            ViewModel.ShowOnlyOverBudget = _overBudgetCheckBox?.Checked ?? false;
    }

    private void UnderBudgetCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
            ViewModel.ShowOnlyUnderBudget = _underBudgetCheckBox?.Checked ?? false;
    }

    private void AddEntryButton_Click(object? sender, EventArgs e)
    {
        try
        {
            Logger.LogInformation("Add Entry button clicked");

            // Create a simple input form for adding new budget entry
            using var form = new Form
            {
                Text = "Add Budget Entry",
                Width = 500,
                Height = 400,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(10)
            };

            // Account Number
            tableLayout.Controls.Add(new Label { Text = "Account Number:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            var txtAccountNumber = new TextBox { Dock = DockStyle.Fill };
            tableLayout.Controls.Add(txtAccountNumber, 1, 0);

            // Description
            tableLayout.Controls.Add(new Label { Text = "Description:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            var txtDescription = new TextBox { Dock = DockStyle.Fill };
            tableLayout.Controls.Add(txtDescription, 1, 1);

            // Budgeted Amount
            tableLayout.Controls.Add(new Label { Text = "Budgeted Amount:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            var txtBudgeted = new TextBox { Dock = DockStyle.Fill, Text = "0.00" };
            tableLayout.Controls.Add(txtBudgeted, 1, 2);

            // Actual Amount
            tableLayout.Controls.Add(new Label { Text = "Actual Amount:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            var txtActual = new TextBox { Dock = DockStyle.Fill, Text = "0.00" };
            tableLayout.Controls.Add(txtActual, 1, 3);

            // Department ID
            tableLayout.Controls.Add(new Label { Text = "Department ID:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 4);
            var txtDepartmentId = new TextBox { Dock = DockStyle.Fill, Text = "1" };
            tableLayout.Controls.Add(txtDepartmentId, 1, 4);

            // Fund Type
            tableLayout.Controls.Add(new Label { Text = "Fund Type:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 5);
            var cmbFundType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFundType.Items.AddRange(Enum.GetNames(typeof(FundType)));
            cmbFundType.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbFundType, 1, 5);

            // Buttons
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnOK);
            tableLayout.Controls.Add(btnPanel, 1, 8);
            tableLayout.SetColumnSpan(btnPanel, 2);

            form.Controls.Add(tableLayout);
            form.AcceptButton = btnOK;
            form.CancelButton = btnCancel;

            if (form.ShowDialog() == DialogResult.OK)
            {
                if (!decimal.TryParse(txtBudgeted.Text, out var budgeted) ||
                    !decimal.TryParse(txtActual.Text, out var actual) ||
                    !int.TryParse(txtDepartmentId.Text, out var deptId))
                {
                    MessageBox.Show("Invalid input values. Please check your entries.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var entry = new BudgetEntry
                {
                    AccountNumber = txtAccountNumber.Text.Trim(),
                    Description = txtDescription.Text.Trim(),
                    BudgetedAmount = budgeted,
                    ActualAmount = actual,
                    DepartmentId = deptId,
                    FiscalYear = ViewModel!.SelectedFiscalYear,
                    FundType = Enum.Parse<FundType>(cmbFundType.SelectedItem?.ToString() ?? "GeneralFund"),
                    Variance = budgeted - actual,
                    StartPeriod = new DateTime(ViewModel.SelectedFiscalYear, 1, 1),
                    EndPeriod = new DateTime(ViewModel.SelectedFiscalYear, 12, 31),
                    CreatedAt = DateTime.UtcNow
                };

                Task.Run(async () => await ViewModel.AddEntryAsync(entry));
                UpdateStatus("Budget entry added successfully");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in AddEntryButton_Click");
            MessageBox.Show($"Error adding entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void EditEntryButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_budgetGrid?.SelectedItems == null || _budgetGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a budget entry to edit.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedEntry = _budgetGrid.SelectedItems[0] as BudgetEntry;
            if (selectedEntry == null) return;

            Logger.LogInformation("Edit Entry button clicked for entry {Id}", selectedEntry.Id);

            // Similar dialog to Add but pre-populated with existing values
            using var form = new Form
            {
                Text = "Edit Budget Entry",
                Width = 500,
                Height = 400,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(10)
            };

            // Pre-populate fields
            tableLayout.Controls.Add(new Label { Text = "Account Number:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            var txtAccountNumber = new TextBox { Dock = DockStyle.Fill, Text = selectedEntry.AccountNumber };
            tableLayout.Controls.Add(txtAccountNumber, 1, 0);

            tableLayout.Controls.Add(new Label { Text = "Description:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            var txtDescription = new TextBox { Dock = DockStyle.Fill, Text = selectedEntry.Description };
            tableLayout.Controls.Add(txtDescription, 1, 1);

            tableLayout.Controls.Add(new Label { Text = "Budgeted Amount:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            var txtBudgeted = new TextBox { Dock = DockStyle.Fill, Text = selectedEntry.BudgetedAmount.ToString("F2", System.Globalization.CultureInfo.CurrentCulture) };
            tableLayout.Controls.Add(txtBudgeted, 1, 2);

            tableLayout.Controls.Add(new Label { Text = "Actual Amount:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            var txtActual = new TextBox { Dock = DockStyle.Fill, Text = selectedEntry.ActualAmount.ToString("F2", System.Globalization.CultureInfo.CurrentCulture) };
            tableLayout.Controls.Add(txtActual, 1, 3);

            tableLayout.Controls.Add(new Label { Text = "Department ID:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 4);
            var txtDepartmentId = new TextBox { Dock = DockStyle.Fill, Text = selectedEntry.DepartmentId.ToString(System.Globalization.CultureInfo.CurrentCulture) };
            tableLayout.Controls.Add(txtDepartmentId, 1, 4);

            tableLayout.Controls.Add(new Label { Text = "Fund Type:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 5);
            var cmbFundType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFundType.Items.AddRange(Enum.GetNames(typeof(FundType)));
            cmbFundType.SelectedItem = selectedEntry.FundType.ToString();
            tableLayout.Controls.Add(cmbFundType, 1, 5);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnOK);
            tableLayout.Controls.Add(btnPanel, 1, 8);
            tableLayout.SetColumnSpan(btnPanel, 2);

            form.Controls.Add(tableLayout);
            form.AcceptButton = btnOK;
            form.CancelButton = btnCancel;

            if (form.ShowDialog() == DialogResult.OK)
            {
                if (!decimal.TryParse(txtBudgeted.Text, out var budgeted) ||
                    !decimal.TryParse(txtActual.Text, out var actual) ||
                    !int.TryParse(txtDepartmentId.Text, out var deptId))
                {
                    MessageBox.Show("Invalid input values. Please check your entries.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                selectedEntry.AccountNumber = txtAccountNumber.Text.Trim();
                selectedEntry.Description = txtDescription.Text.Trim();
                selectedEntry.BudgetedAmount = budgeted;
                selectedEntry.ActualAmount = actual;
                selectedEntry.DepartmentId = deptId;
                selectedEntry.FundType = Enum.Parse<FundType>(cmbFundType.SelectedItem?.ToString() ?? "GeneralFund");
                selectedEntry.Variance = budgeted - actual;
                selectedEntry.UpdatedAt = DateTime.UtcNow;

                _ = Task.Run(async () => await ViewModel!.UpdateEntryAsync(selectedEntry));
                UpdateStatus("Budget entry updated successfully");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in EditEntryButton_Click");
            MessageBox.Show($"Error editing entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Task DeleteEntryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_budgetGrid?.SelectedItems == null || _budgetGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a budget entry to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            var selectedEntry = _budgetGrid.SelectedItems[0] as BudgetEntry;
            if (selectedEntry == null) return Task.CompletedTask;

            var result = MessageBox.Show(
                $"Are you sure you want to delete budget entry '{selectedEntry.AccountNumber} - {selectedEntry.Description}'?\n\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Logger.LogInformation("Deleting budget entry {Id}: {AccountNumber}", selectedEntry.Id, selectedEntry.AccountNumber);
                Task.Run(async () => await ViewModel!.DeleteEntryAsync(selectedEntry.Id));
                UpdateStatus($"Deleted budget entry {selectedEntry.AccountNumber}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in DeleteEntryAsync");
            MessageBox.Show($"Error deleting entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return Task.CompletedTask;
    }

    private void ImportCsvButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;
        using var openFileDialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Import Budget Entries from CSV"
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            var file = openFileDialog.FileName;

            // Show embedded mapping wizard for user to map columns to budget fields
            if (_mappingContainer != null && _mappingWizardPanel != null)
            {
                _mappingContainer.Visible = true;
                _mappingWizardPanel.Initialize(file, ViewModel.AvailableEntities, 2025);
                _mappingContainer.BringToFront();
            }
            else
            {
                // Fallback to existing simple import
                _ = ViewModel.ImportFromCsvCommand.ExecuteAsync(file);
            }
        }
    }

    private void ExportCsvButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Export Budget Entries to CSV",
            FileName = $"Budget_Entries_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            _ = ViewModel.ExportToCsvCommand.ExecuteAsync(saveFileDialog.FileName);
        }
    }

    private async void MappingWizardPanel_MappingApplied(object? sender, MappingAppliedEventArgs e)
    {
        if (ViewModel == null) return;

        try
        {
            UpdateStatus($"Importing {Path.GetFileName(e.FilePath)}...");
            var progress = new Progress<string>(s => UpdateStatus(s));
            var selectedEntity = string.IsNullOrWhiteSpace(e.SelectedEntity) || e.SelectedEntity == "(None)" ? null : e.SelectedEntity;
            await ViewModel.ImportFromCsvWithMappingAsync(e.FilePath, e.ColumnMap, selectedEntity, e.FiscalYear, progress);
            await ViewModel.RefreshAnalysisCommand.ExecuteAsync(null);
            UpdateStatus("Import completed");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Import with mapping failed");
            UpdateStatus("Import failed");
            MessageBox.Show($"Import failed: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (_mappingContainer != null) _mappingContainer.Visible = false;
        }
    }

    private void MappingWizardPanel_Cancelled(object? sender, EventArgs e)
    {
        if (_mappingContainer != null) _mappingContainer.Visible = false;
        UpdateStatus("Import cancelled");
    }

    private void ExportPdfButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            Title = "Export Budget Entries to PDF",
            FileName = $"Budget_Report_{DateTime.Now:yyyyMMdd}.pdf"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            _ = ViewModel.ExportToPdfCommand.ExecuteAsync(saveFileDialog.FileName);
        }
    }

    private void ExportExcelButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            Title = "Export Budget Entries to Excel",
            FileName = $"Budget_Entries_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            _ = ViewModel.ExportToExcelCommand.ExecuteAsync(saveFileDialog.FileName);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(ViewModel.BudgetEntries):
                if (_budgetGrid != null) _budgetGrid.DataSource = ViewModel.BudgetEntries;
                break;

            case nameof(ViewModel.FilteredBudgetEntries):
                if (_budgetGrid != null) _budgetGrid.DataSource = ViewModel.FilteredBudgetEntries;
                break;

            case nameof(ViewModel.AvailableEntities):
                if (_entityComboBox != null && ViewModel.AvailableEntities != null)
                {
                    _entityComboBox.DataSource = ViewModel.AvailableEntities;
                    _entityComboBox.SelectedItem = string.IsNullOrWhiteSpace(ViewModel.SelectedEntity) ? "All Entities" : ViewModel.SelectedEntity;
                }
                break;

            case nameof(ViewModel.SelectedEntity):
                if (_entityComboBox != null)
                {
                    _entityComboBox.SelectedItem = string.IsNullOrWhiteSpace(ViewModel.SelectedEntity) ? "All Entities" : ViewModel.SelectedEntity;
                }
                break;

            case nameof(ViewModel.IsLoading):
                if (_loadingOverlay != null) _loadingOverlay.Visible = ViewModel.IsLoading;
                if (_noDataOverlay != null) _noDataOverlay.Visible = !ViewModel.IsLoading && !ViewModel.BudgetEntries.Any();
                break;

            case nameof(ViewModel.StatusText):
                if (_statusLabel != null) _statusLabel.Text = ViewModel.StatusText;
                break;

            case nameof(ViewModel.TotalBudgeted):
                if (_totalBudgetedLabel != null)
                    _totalBudgetedLabel.Text = $"Total Budgeted: {ViewModel.TotalBudgeted:C}";
                break;

            case nameof(ViewModel.TotalActual):
                if (_totalActualLabel != null)
                    _totalActualLabel.Text = $"Total Actual: {ViewModel.TotalActual:C}";
                break;

            case nameof(ViewModel.TotalVariance):
                if (_totalVarianceLabel != null)
                    _totalVarianceLabel.Text = $"Total Variance: {ViewModel.TotalVariance:C}";
                break;

            case nameof(ViewModel.PercentUsed):
                if (_percentUsedLabel != null)
                    _percentUsedLabel.Text = $"Percent Used: {ViewModel.PercentUsed:P}";
                break;

            case nameof(ViewModel.EntriesOverBudget):
                if (_entriesOverBudgetLabel != null)
                    _entriesOverBudgetLabel.Text = $"Over Budget: {ViewModel.EntriesOverBudget}";
                break;

            case nameof(ViewModel.EntriesUnderBudget):
                if (_entriesUnderBudgetLabel != null)
                    _entriesUnderBudgetLabel.Text = $"Under Budget: {ViewModel.EntriesUnderBudget}";
                break;
        }
    }

    private async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null) return;

        try
        {
            Logger.LogInformation("Refreshing budget data");
            UpdateStatus("Loading budget data...");

            await ViewModel.LoadBudgetsCommand.ExecuteAsync(null);
            await ViewModel.RefreshAnalysisCommand.ExecuteAsync(null);

            UpdateStatus("Data refreshed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing data");
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
            Logger.LogWarning(ex, "BudgetPanel: ClosePanel failed");
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        if (ViewModel == null) return;

        try
        {
            // Auto-load data on panel load
            Task.Run(async () => await RefreshDataAsync());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading panel data");
        }
    }

    /// <summary>
    /// Handles keyboard shortcuts for common operations.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (ViewModel == null) return base.ProcessCmdKey(ref msg, keyData);

        try
        {
            switch (keyData)
            {
                case Keys.Control | Keys.N: // Add new entry
                    AddEntryButton_Click(null, EventArgs.Empty);
                    return true;

                case Keys.Control | Keys.S: // Save/Refresh
                    _ = RefreshDataAsync();
                    return true;

                case Keys.Delete: // Delete entry
                    _ = DeleteEntryAsync();
                    return true;

                case Keys.F5: // Refresh
                    _ = RefreshDataAsync();
                    return true;

                case Keys.Escape: // Close panel
                    ClosePanel();
                    return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "ProcessCmdKey failed");
        }

        return base.ProcessCmdKey(ref msg, keyData);
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
            // Unsubscribe from events
            if (_panelHeader != null)
            {
                if (_panelHeaderRefreshHandler != null)
                    _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                if (_panelHeaderCloseHandler != null)
                    _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
            }

            // Theme service removed - SfSkinManager handles themes automatically

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Unsubscribe from grid events
            if (_budgetGrid != null)
            {
                _budgetGrid.QueryCellStyle -= BudgetGrid_QueryCellStyle;
                _budgetGrid.CurrentCellActivated -= BudgetGrid_CurrentCellActivated;
            }

            // SafeDispose for Syncfusion controls
            try { _budgetGrid?.SafeClearDataSource(); } catch { }
            try { _budgetGrid?.SafeDispose(); } catch { }

            // Dispose other controls
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
        this.SuspendLayout();

        this.components = new System.ComponentModel.Container();
        this.Name = "BudgetPanel";
        this.Size = new Size(1400, 900);
        try { this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; } catch { }
        this.MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
        this.AutoScroll = true;
        this.Padding = new Padding(8);
        this.ResumeLayout(false);

    }

    #endregion
}

