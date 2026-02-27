using System.Collections.Generic;
using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using GridCheckBoxColumn = Syncfusion.WinForms.DataGrid.GridCheckBoxColumn;
using GridNumericColumn = Syncfusion.WinForms.DataGrid.GridNumericColumn;
using GridTextColumn = Syncfusion.WinForms.DataGrid.GridTextColumn;
using CheckBoxAdv = Syncfusion.Windows.Forms.Tools.CheckBoxAdv;
using SfButton = Syncfusion.WinForms.Controls.SfButton;
using SfComboBox = Syncfusion.WinForms.ListView.SfComboBox;
using SfDataGrid = Syncfusion.WinForms.DataGrid.SfDataGrid;
using SfSkinManager = Syncfusion.WinForms.Controls.SfSkinManager;
using Syncfusion.Drawing;
using WileyWidget.WinForms.Controls.Base;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using System.ComponentModel;
using WileyWidget.WinForms.Controls.Supporting;
using SplitContainerAdv = Syncfusion.Windows.Forms.Tools.SplitContainerAdv;
using Syncfusion.WinForms.DataGrid.Enums;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Data;
using WileyWidget.WinForms.Dialogs;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels;

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
    private BindingSource? _budgetBindingSource;
    private Label? _totalBudgetedLabel;
    private Label? _totalActualLabel;
    private Label? _totalVarianceLabel;
    private Label? _percentUsedLabel;
    private Label? _entriesOverBudgetLabel;
    private Label? _entriesUnderBudgetLabel;
    private Panel? _summaryPanel;
    private Panel? _gridPanel;
    private Panel? _filterPanel;
    private Panel? _buttonPanel;
    private SplitContainerAdv? _mainSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;
    // Embedded CSV mapping wizard for advanced imports
    private WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel? _mappingWizardPanel;
    private Panel? _mappingContainer;

    // Event handlers for proper cleanup
    private EventHandler? _panelHeaderRefreshHandler;
    private EventHandler? _panelHeaderHelpHandler;
    private EventHandler? _panelHeaderCloseHandler;
    private EventHandler? _searchTextChangedHandler;
    private EventHandler? _fiscalYearChangedHandler;
    private EventHandler? _entityChangedHandler;
    private EventHandler? _departmentChangedHandler;
    private EventHandler? _fundTypeChangedHandler;
    private EventHandler? _varianceThresholdChangedHandler;
    private EventHandler? _overBudgetCheckChangedHandler;
    private EventHandler? _underBudgetCheckChangedHandler;
    private EventHandler<MappingAppliedEventArgs>? _mappingWizardAppliedHandler;
    private EventHandler? _mappingWizardCancelledHandler;
    private ToolTip? _controlToolTip;

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
        SafeSuspendAndLayout(InitializeComponent);

        // Apply DPI-aware minimum size before layout initialization
        MinimumSize = new Size(
            (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1200.0f),
            (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(720.0f)
        );

        // NOTE: InitializeControls() moved to OnViewModelResolved()
        // because it requires ViewModel to be non-null
        ApplySyncfusionTheme();

        // Defer sizing validation until handle is created
        DeferSizeValidation();

        // Wire up VisibleChanged event for SplitContainer configuration (happens when panel becomes visible)
        this.VisibleChanged += BudgetPanel_VisibleChanged;

        Logger.LogInformation("BudgetPanel constructor completed — waiting for OnViewModelResolved");
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
    internal BudgetPanel() : this(
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(Program.Services),
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ScopedPanelBase<BudgetViewModel>>>(Program.Services))
    {
    }

    private void InitializeControls()
    {
        Logger.LogInformation("BudgetPanel.InitializeControls: START - IsDisposed={IsDisposed}, IsHandleCreated={IsHandleCreated}",
            IsDisposed, IsHandleCreated);

        // Batch layout initialization for better performance
        SuspendLayout();

        // Set up form properties
        Text = "Budget Management";
        Size = new Size(1400, 900);

        // Panel header with actions
        _panelHeader = new PanelHeader { Dock = DockStyle.Top, Title = "Budget Management & Analysis" };
        _panelHeader.ShowHelpButton = true;
        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderHelpHandler = PanelHeader_HelpClicked;
        _panelHeader.HelpClicked += _panelHeaderHelpHandler;
        _panelHeaderCloseHandler = (s, e) => ClosePanel();
        _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        Controls.Add(_panelHeader);

        // Main split container - top/bottom layout
        // Ensures summary+filters remain visible while preserving space for the grid
        _mainSplitContainer = ControlFactory.CreateSplitContainerAdv(split =>
        {
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250.0f);
        });
        // Note: SafeSplitterDistanceHelper.ConfigureSafeSplitContainer is deferred to avoid sizing exceptions
        // during initialization. The helper will configure min sizes when the container is properly sized.

        Controls.Add(_mainSplitContainer);

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
        _loadingOverlay = new LoadingOverlay
        {
            Message = "Loading budget data...",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        // No data overlay
        _noDataOverlay = new NoDataOverlay
        {
            Message = "No budget entries yet\r\nCreate a new budget to get started",
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(_noDataOverlay);
        _noDataOverlay.BringToFront();
        Controls.Add(_statusStrip);

        // Theme changes handled by SfSkinManager cascade

        // Set tab order
        SetTabOrder();

        // Rich, plain-language tooltips for key controls and workflows
        InitializeTooltips();

        // Explicit overlay Z-order management: LoadingOverlay on bottom, NoDataOverlay on top
        _loadingOverlay?.SendToBack();
        _noDataOverlay?.BringToFront();

        ResumeLayout(false);
        this.PerformLayout();
        this.Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);

        // SplitContainer configuration moved to OnShown event to avoid early initialization issues
        // See: https://github.com/dotnet/winforms/issues/4000 for timing issues with SplitContainer
    }

    private void InitializeTooltips()
    {
        _controlToolTip ??= new ToolTip
        {
            AutoPopDelay = 15000,
            InitialDelay = 350,
            ReshowDelay = 120,
            ShowAlways = true
        };

        ConfigurePanelHeaderHelpButton();

        if (_searchTextBox != null)
        {
            _controlToolTip.SetToolTip(_searchTextBox, "Type to filter by account number or description");
        }

        if (_fiscalYearComboBox != null)
        {
            _controlToolTip.SetToolTip(_fiscalYearComboBox, "Filter budget rows to a specific fiscal year.");
        }

        if (_entityComboBox != null)
        {
            _controlToolTip.SetToolTip(_entityComboBox, "Filter by legal entity or fund owner (for example Town of Wiley or WSD).");
        }

        if (_departmentComboBox != null)
        {
            _controlToolTip.SetToolTip(_departmentComboBox, "Filter results to one department.");
        }

        if (_fundTypeComboBox != null)
        {
            _controlToolTip.SetToolTip(_fundTypeComboBox, "Filter by fund type classification from your chart of accounts.");
        }

        if (_varianceThresholdTextBox != null)
        {
            _controlToolTip.SetToolTip(_varianceThresholdTextBox, "Show only rows with variance greater than this amount.");
        }

        if (_overBudgetCheckBox != null)
        {
            _controlToolTip.SetToolTip(_overBudgetCheckBox, "Limit view to accounts where actual spending is above budget.");
        }

        if (_underBudgetCheckBox != null)
        {
            _controlToolTip.SetToolTip(_underBudgetCheckBox, "Limit view to accounts that are currently under budget.");
        }

        if (_budgetGrid != null)
        {
            _controlToolTip.SetToolTip(_budgetGrid, BuildBudgetGridTooltipText());
        }

        if (_mappingWizardPanel != null)
        {
            _controlToolTip.SetToolTip(_mappingWizardPanel,
                "CSV Mapping Wizard: map incoming columns to WileyWidget fields before import. " +
                "Use this to align your chart of accounts as the single source of truth.");
        }

        if (_loadBudgetsButton != null)
        {
            _controlToolTip.SetToolTip(_loadBudgetsButton, "Reload budget data for the selected filters and fiscal year.");
        }

        if (_addEntryButton != null)
        {
            _controlToolTip.SetToolTip(_addEntryButton, "Create a new budget line item in the selected fiscal year.");
        }

        if (_editEntryButton != null)
        {
            _controlToolTip.SetToolTip(_editEntryButton, "Edit the currently selected budget line.");
        }

        if (_deleteEntryButton != null)
        {
            _controlToolTip.SetToolTip(_deleteEntryButton, "Delete the selected budget line item.");
        }

        if (_importCsvButton != null)
        {
            _controlToolTip.SetToolTip(_importCsvButton, "Import your full Chart of Accounts from Deb's PDF (uses the mapping wizard)");
        }

        if (_exportCsvButton != null)
        {
            _controlToolTip.SetToolTip(_exportCsvButton, "Export the current grid view as CSV for downstream analysis.");
        }

        if (_exportPdfButton != null)
        {
            _controlToolTip.SetToolTip(_exportPdfButton, "Generate a professional PDF report for Town Council");
        }

        if (_exportExcelButton != null)
        {
            _controlToolTip.SetToolTip(_exportExcelButton, "Export the current budget analysis to Excel for further review.");
        }
    }

    private void PanelHeader_HelpClicked(object? sender, EventArgs e)
    {
        using var helpForm = new BudgetHelpForm();
        helpForm.ShowDialog(this);
    }

    private static string BuildBudgetGridTooltipText()
    {
        return "Budget worksheet legend:\r\n" +
               "• Account Number / Account Name: your chart of accounts identity.\r\n" +
               "• Department / Entity / Fund Type: reporting dimensions for filtering and audits.\r\n" +
               "• Total Budgeted: approved amount for the account.\r\n" +
               "• Total Actual: posted spending from payments linked by MunicipalAccountId.\r\n" +
               "• % of Budget: actual ÷ budgeted.\r\n" +
               "• TOW Budgeted / TOW Actual: Town of Wiley slice.\r\n" +
               "• WSD Budgeted / WSD Actual: Wiley Sanitation District slice.\r\n" +
               "• Encumbrance: committed but not yet paid amount.\r\n" +
               "• Variance / Variance %: difference between budget and actual.";
    }

    private static bool TryGetBudgetColumnTooltip(string? mappingName, out string tooltipText)
    {
        switch (mappingName)
        {
            case "AccountNumber":
                tooltipText = "Official chart of accounts number used for posting and rollups.";
                return true;
            case "AccountName":
                tooltipText = "Human-readable account description from your chart of accounts.";
                return true;
            case "DepartmentName":
                tooltipText = "Department responsible for the account line.";
                return true;
            case "EntityName":
                tooltipText = "Entity/fund owner (for example Town of Wiley or WSD).";
                return true;
            case "FundTypeDescription":
                tooltipText = "Fund classification used for financial reporting.";
                return true;
            case "BudgetedAmount":
                tooltipText = "Approved total budget for this account.";
                return true;
            case "ActualAmount":
                tooltipText = "Actual posted spending linked from payments via MunicipalAccountId.";
                return true;
            case "PercentOfBudgetFraction":
                tooltipText = "Percent consumed: actual divided by budgeted amount.";
                return true;
            case "TownOfWileyBudgetedAmount":
                tooltipText = "Town of Wiley portion of the budgeted amount.";
                return true;
            case "TownOfWileyActualAmount":
                tooltipText = "Town of Wiley portion of posted actual spending.";
                return true;
            case "WsdBudgetedAmount":
                tooltipText = "Wiley Sanitation District portion of the budgeted amount.";
                return true;
            case "WsdActualAmount":
                tooltipText = "Wiley Sanitation District portion of posted actual spending.";
                return true;
            case "EncumbranceAmount":
                tooltipText = "Committed amount not yet fully paid.";
                return true;
            case "VarianceAmount":
                tooltipText = "Budgeted minus actual spending for this line.";
                return true;
            case "VariancePercentage":
                tooltipText = "Variance as a percentage relative to budgeted amount.";
                return true;
            default:
                tooltipText = string.Empty;
                return false;
        }
    }

    private void ConfigurePanelHeaderHelpButton()
    {
        if (_panelHeader == null || _controlToolTip == null)
        {
            return;
        }

        var helpButton = FindPanelHeaderHelpButton(_panelHeader);
        if (helpButton == null)
        {
            return;
        }

        helpButton.Image = null;
        helpButton.AutoSize = false;
        helpButton.Size = new Size(32, 32);
        helpButton.Text = "?";
        helpButton.AccessibleName = "Help";
        helpButton.AccessibleDescription = "How This Budget Panel Works";
        _controlToolTip.SetToolTip(helpButton, "How This Budget Panel Works");
    }

    private static SfButton? FindPanelHeaderHelpButton(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is SfButton button &&
                string.Equals(button.AccessibleName, "Help", StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }

            if (child.HasChildren)
            {
                var nestedMatch = FindPanelHeaderHelpButton(child);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }
        }

        return null;
    }

    private void ConfigureBudgetGridHeaderTooltips()
    {
        if (_budgetGrid == null || _budgetGrid.Columns.Count == 0)
        {
            return;
        }

        _budgetGrid.ShowToolTip = true;
        _budgetGrid.ShowHeaderToolTip = true;

        foreach (var column in _budgetGrid.Columns)
        {
            column.ShowToolTip = true;
            column.ShowHeaderToolTip = true;
        }
    }

    private void BudgetGrid_ToolTipOpening(object? sender, ToolTipOpeningEventArgs e)
    {
        if (_budgetGrid == null || e?.Column == null)
        {
            return;
        }

        // Customize only header hover tooltips.
        // Syncfusion API: DataGridIndexResolver.GetHeaderIndex returns the header row index.
        var headerRowIndex = DataGridIndexResolver.GetHeaderIndex(_budgetGrid.TableControl);
        if (e.RowIndex != headerRowIndex || e.Record != null)
        {
            return;
        }

        if (!TryGetBudgetColumnTooltip(e.Column.MappingName, out var columnTooltip))
        {
            return;
        }

        var tooltipInfo = new Syncfusion.WinForms.Controls.ToolTipInfo();
        tooltipInfo.Items.Add(new Syncfusion.WinForms.Controls.ToolTipItem
        {
            Text = $"{e.Column.HeaderText}: {columnTooltip}"
        });

        e.ToolTipInfo = tooltipInfo;
    }

    private void InitializeTopPanel()
    {
        var themeName = ThemeColors.CurrentTheme;

        var topPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(topPanel, themeName);

        // Summary panel
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
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

        var tooltip = new ToolTip();

        _totalBudgetedLabel = new Label
        {
            Text = "Total Budgeted: $0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            AccessibleName = "Total Budgeted",
            AccessibleDescription = "Sum of all budgeted amounts across entries"
        };
        tooltip.SetToolTip(_totalBudgetedLabel, "Total budgeted amount for all entries");

        _totalActualLabel = new Label
        {
            Text = "Total Actual: $0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            AccessibleName = "Total Actual",
            AccessibleDescription = "Sum of all actual amounts across entries"
        };
        tooltip.SetToolTip(_totalActualLabel, "Total actual spending for all entries");

        _totalVarianceLabel = new Label
        {
            Text = "Total Variance: $0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            AccessibleName = "Total Variance",
            AccessibleDescription = "Difference between budgeted and actual amounts"
        };
        tooltip.SetToolTip(_totalVarianceLabel, "Total variance (budgeted - actual)");

        _percentUsedLabel = new Label
        {
            Text = "Percent Used: 0.00%",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            AccessibleName = "Percent Used",
            AccessibleDescription = "Percentage of budget consumed by actual spending"
        };
        tooltip.SetToolTip(_percentUsedLabel, "Percentage of budget used (actual / budgeted)");

        _entriesOverBudgetLabel = new Label
        {
            Text = "Over Budget: 0",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            AccessibleName = "Over Budget Count",
            AccessibleDescription = "Number of entries exceeding budget"
        };
        tooltip.SetToolTip(_entriesOverBudgetLabel, "Number of entries over budget");

        _entriesUnderBudgetLabel = new Label
        {
            Text = "Under Budget: 0",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            AccessibleName = "Under Budget Count",
            AccessibleDescription = "Number of entries within budget"
        };
        tooltip.SetToolTip(_entriesUnderBudgetLabel, "Number of entries under budget");

        summaryTable.Controls.Add(_totalBudgetedLabel, 0, 0);
        summaryTable.Controls.Add(_totalActualLabel, 1, 0);
        summaryTable.Controls.Add(_totalVarianceLabel, 2, 0);
        summaryTable.Controls.Add(_percentUsedLabel, 3, 0);
        summaryTable.Controls.Add(_entriesOverBudgetLabel, 4, 0);
        summaryTable.Controls.Add(_entriesUnderBudgetLabel, 5, 0);

        _summaryPanel.Controls.Add(summaryTable);
        topPanel.Controls.Add(_summaryPanel);

        // Filter panel - Fixed height (120px) to prevent unbounded growth when grid is shrunk
        // NO manual BackColor; let SfSkinManager handle it
        _filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120.0f),
            Padding = new Padding(10),
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_filterPanel, themeName);

        var filterGroup = new Panel
        {
            Text = "Filters",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(filterGroup, themeName);

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 3
        };

        // Label/Input column pairs with wider input regions to reduce truncation.
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
        filterTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));
        filterTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        filterTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

        // Row 1: Search and Fiscal Year
        var searchLabel = new Label
        {
            Text = "Search:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        _searchTextBox = ControlFactory.CreateTextBoxExt(textBox =>
        {
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(5);
            textBox.TabIndex = 1;
            textBox.AccessibleName = "Search Budget Entries";
            textBox.AccessibleDescription = "Search budget entries by account, description, or department";
        });
        _searchTextChangedHandler = SearchTextBox_TextChanged;
        _searchTextBox.TextChanged += _searchTextChangedHandler;

        var toolTip = new ToolTip();
        toolTip.SetToolTip(_searchTextBox, "Search budget entries by account number, name, or department");

        var fiscalYearLabel = new Label
        {
            Text = "Fiscal Year:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        _fiscalYearComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(5);
            combo.TabIndex = 2;
            combo.AccessibleName = "Fiscal Year Filter";
            combo.AccessibleDescription = "Filter budget entries by fiscal year";
        });

        // Populate fiscal years
        var years = new List<int>();
        for (int year = DateTime.Now.Year - 5; year <= DateTime.Now.Year + 5; year++)
        {
            years.Add(year);
        }
        _fiscalYearComboBox.DataSource = years;
        _fiscalYearComboBox.SelectedItem = DateTime.Now.Year;
        _fiscalYearChangedHandler = FiscalYearComboBox_SelectedIndexChanged;
        _fiscalYearComboBox.SelectedIndexChanged += _fiscalYearChangedHandler;

        var entityLabel = new Label
        {
            Text = "Entity:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        _entityComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(5);
            combo.TabIndex = 8;
            combo.AccessibleName = "Entity Filter";
            combo.AccessibleDescription = "Filter budget entries by entity or fund";
        });

        // Placeholder until data is loaded
        _entityComboBox.DataSource = new List<string> { "All Entities" };
        _entityChangedHandler = EntityComboBox_SelectedIndexChanged;
        _entityComboBox.SelectedIndexChanged += _entityChangedHandler;

        // Add entity controls to the filter table (columns 4 and 5)
        filterTable.Controls.Add(entityLabel, 4, 0);
        filterTable.Controls.Add(_entityComboBox, 5, 0);

        // Row 2: Department and Fund Type
        var departmentLabel = new Label
        {
            Text = "Department:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        _departmentComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(5);
            combo.TabIndex = 3;
            combo.AccessibleName = "Department Filter";
            combo.AccessibleDescription = "Filter budget entries by department";
        });
        _departmentComboBox.DataSource = new List<string> { "All Departments" };
        _departmentComboBox.SelectedIndex = 0;
        _departmentChangedHandler = DepartmentComboBox_SelectedIndexChanged;
        _departmentComboBox.SelectedIndexChanged += _departmentChangedHandler;

        var fundTypeLabel = new Label
        {
            Text = "Fund Type:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        _fundTypeComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(5);
            combo.TabIndex = 4;
            combo.AccessibleName = "Fund Type Filter";
            combo.AccessibleDescription = "Filter budget entries by fund type";
        });
        var fundTypes = Enum.GetNames(typeof(FundType)).ToList();
        fundTypes.Insert(0, "All Fund Types");
        _fundTypeComboBox.DataSource = fundTypes;
        _fundTypeComboBox.SelectedIndex = 0;
        _fundTypeChangedHandler = FundTypeComboBox_SelectedIndexChanged;
        _fundTypeComboBox.SelectedIndexChanged += _fundTypeChangedHandler;

        // Row 3: Variance threshold and checkboxes
        var varianceLabel = new Label
        {
            Text = "Variance >:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        _varianceThresholdTextBox = ControlFactory.CreateTextBoxExt(textBox =>
        {
            textBox.Text = string.Empty;
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(5);
            textBox.TabIndex = 5;
            textBox.AccessibleName = "Variance Threshold";
            textBox.AccessibleDescription = "Filter entries with variance greater than this amount";
        });
        _varianceThresholdChangedHandler = VarianceThresholdTextBox_TextChanged;
        _varianceThresholdTextBox.TextChanged += _varianceThresholdChangedHandler;

        _overBudgetCheckBox = ControlFactory.CreateCheckBoxAdv("Over Budget Only", checkBox =>
        {
            checkBox.Dock = DockStyle.Fill;
            checkBox.Margin = new Padding(5);
            checkBox.TabIndex = 6;
            checkBox.AccessibleName = "Show Over Budget Only";
            checkBox.AccessibleDescription = "Show only entries that are over budget";
        });
        _overBudgetCheckChangedHandler = OverBudgetCheckBox_CheckedChanged;
        _overBudgetCheckBox.CheckStateChanged += _overBudgetCheckChangedHandler;

        _underBudgetCheckBox = ControlFactory.CreateCheckBoxAdv("Under Budget Only", checkBox =>
        {
            checkBox.Dock = DockStyle.Fill;
            checkBox.Margin = new Padding(5);
            checkBox.TabIndex = 7;
            checkBox.AccessibleName = "Show Under Budget Only";
            checkBox.AccessibleDescription = "Show only entries that are under budget";
        });
        _underBudgetCheckChangedHandler = UnderBudgetCheckBox_CheckedChanged;
        _underBudgetCheckBox.CheckStateChanged += _underBudgetCheckChangedHandler;

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
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(bottomPanel, themeName);

        // Budget grid with AutoScroll enabled for overflow content
        _gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoScroll = true,
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_gridPanel, themeName);

        _budgetGrid = ControlFactory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowEditing = true;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.Fill;
            grid.SelectionMode = GridSelectionMode.Single;
            grid.EditMode = EditMode.SingleClick;
            grid.TabIndex = 8;
            grid.AccessibleName = "Budget Entries Grid";
            grid.AccessibleDescription = "Data grid displaying budget entries with account numbers, amounts, variances, and related budget information";
        });

        // Configure grid columns
        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountNumber",
            HeaderText = "Account Number",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountName",
            HeaderText = "Account Name",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(220.0f),
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "DepartmentName",
            HeaderText = "Department",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(140.0f),
            AllowEditing = false
        });

        // Entity / Fund column
        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "EntityName",
            HeaderText = "Entity",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = false
        });

        // Keep fund type description for quick reference
        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "FundTypeDescription",
            HeaderText = "Fund Type",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = false
        });

        // Totals (combined across entities)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "BudgetedAmount",
            HeaderText = "Total Budgeted",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "ActualAmount",
            HeaderText = "Total Actual",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = true
        });

        // Percentage of budget (Actual / Budget) - show as percent (P-format)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "PercentOfBudgetFraction",
            HeaderText = "% of Budget",
            Format = "P2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110.0f),
            AllowEditing = false
        });

        // Town of Wiley specific columns (TOW)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TownOfWileyBudgetedAmount",
            HeaderText = "TOW Budgeted",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "TownOfWileyActualAmount",
            HeaderText = "TOW Actual",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = false
        });

        // Wiley Sanitation District specific columns (WSD)
        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "WsdBudgetedAmount",
            HeaderText = "WSD Budgeted",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "WsdActualAmount",
            HeaderText = "WSD Actual",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130.0f),
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "EncumbranceAmount",
            HeaderText = "Encumbrance",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120.0f),
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "VarianceAmount",
            HeaderText = "Variance",
            Format = "C2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120.0f),
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "VariancePercentage",
            HeaderText = "Variance %",
            Format = "P2",
            MinimumWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(110.0f),
            AllowEditing = false
        });

        ConfigureBudgetGridHeaderTooltips();

        _budgetGrid.ToolTipOpening += BudgetGrid_ToolTipOpening;
        _budgetGrid.CurrentCellActivated += BudgetGrid_CurrentCellActivated;
        _budgetGrid.FilterChanging += BudgetGrid_FilterChanging;

        // Set AutoGenerateColumns explicitly to false
        _budgetGrid.AutoGenerateColumns = false;

        _gridPanel.Controls.Add(_budgetGrid);
        bottomPanel.Controls.Add(_gridPanel);

        // Embedded mapping wizard container (hidden by default)
        // Mapping wizard container - hidden by default; expands with proportional sizing (Option B)
        _mappingContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(260.0f),
            Visible = false
        };

        _mappingWizardPanel = new WileyWidget.WinForms.Controls.Supporting.CsvMappingWizardPanel(Logger) { Dock = DockStyle.Fill };
        _mappingWizardAppliedHandler = MappingWizardPanel_MappingApplied;
        _mappingWizardPanel.MappingApplied += _mappingWizardAppliedHandler;
        _mappingWizardCancelledHandler = MappingWizardPanel_Cancelled;
        _mappingWizardPanel.Cancelled += _mappingWizardCancelledHandler;
        _mappingContainer.Controls.Add(_mappingWizardPanel);
        bottomPanel.Controls.Add(_mappingContainer);

        // Button panel with AutoScroll for high DPI button overflow (Option A)
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50.0f),
            Padding = new Padding(10),
            AutoScroll = true,
            BorderStyle = BorderStyle.None,
        };
        SfSkinManager.SetVisualStyle(_buttonPanel, themeName);

        var buttonTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            RowCount = 1,
            AutoSize = false,
            MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(960.0f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(30.0f))
        };

        // Keep consistent button widths while preserving a stretch region to avoid clipping drift.
        for (int i = 0; i < 8; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120.0f)));
        buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _loadBudgetsButton = ControlFactory.CreateSfButton("&Load Budgets", button =>
        {
            button.TabIndex = 9;
            button.AccessibleName = "Load Budgets";
            button.AccessibleDescription = "Load budget entries for the selected fiscal year";
        });
        _loadBudgetsButton.Click += OnLoadBudgetsButtonClick;

        _addEntryButton = ControlFactory.CreateSfButton("&Add Entry", button =>
        {
            button.TabIndex = 10;
            button.AccessibleName = "Add Entry";
            button.AccessibleDescription = "Add a new budget entry";
        });
        _addEntryButton.Click += AddEntryButton_Click;

        _editEntryButton = ControlFactory.CreateSfButton("&Edit Entry", button =>
        {
            button.TabIndex = 11;
            button.AccessibleName = "Edit Entry";
            button.AccessibleDescription = "Edit the selected budget entry";
        });
        _editEntryButton.Click += EditEntryButton_Click;

        _deleteEntryButton = ControlFactory.CreateSfButton("&Delete Entry", button =>
        {
            button.TabIndex = 12;
            button.AccessibleName = "Delete Entry";
            button.AccessibleDescription = "Delete the selected budget entry";
        });
        _deleteEntryButton.Click += async (s, e) => await DeleteEntryAsync();

        _importCsvButton = ControlFactory.CreateSfButton("&Import CSV", button =>
        {
            button.TabIndex = 13;
            button.AccessibleName = "Import CSV";
            button.AccessibleDescription = "Import budget entries from CSV file";
        });
        _importCsvButton.Click += ImportCsvButton_Click;

        _exportCsvButton = ControlFactory.CreateSfButton("Export &CSV", button =>
        {
            button.TabIndex = 14;
            button.AccessibleName = "Export CSV";
            button.AccessibleDescription = "Export budget entries to CSV file";
        });
        _exportCsvButton.Click += ExportCsvButton_Click;

        _exportPdfButton = ControlFactory.CreateSfButton("Export &PDF", button =>
        {
            button.TabIndex = 15;
            button.AccessibleName = "Export PDF";
            button.AccessibleDescription = "Export budget entries to PDF file";
        });
        _exportPdfButton.Click += ExportPdfButton_Click;

        _exportExcelButton = ControlFactory.CreateSfButton("Export &Excel", button =>
        {
            button.TabIndex = 16;
            button.AccessibleName = "Export Excel";
            button.AccessibleDescription = "Export budget entries to Excel file";
        });
        _exportExcelButton.Click += ExportExcelButton_Click;

        buttonTable.Controls.Add(_loadBudgetsButton, 0, 0);
        buttonTable.Controls.Add(_addEntryButton, 1, 0);
        buttonTable.Controls.Add(_editEntryButton, 2, 0);
        buttonTable.Controls.Add(_deleteEntryButton, 3, 0);
        buttonTable.Controls.Add(_importCsvButton, 4, 0);
        buttonTable.Controls.Add(_exportCsvButton, 5, 0);
        buttonTable.Controls.Add(_exportPdfButton, 6, 0);
        buttonTable.Controls.Add(_exportExcelButton, 7, 0);
        buttonTable.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }, 8, 0);

        _buttonPanel!.Controls.Add(buttonTable);
        bottomPanel.Controls.Add(_buttonPanel);

        _mainSplitContainer.Panel2.Controls.Add(bottomPanel);
    }

    /// <summary>
    /// Binds ViewModel properties to UI controls using DataBindings for two-way binding.
    /// </summary>
    private void BindViewModel()
    {
        if (ViewModel == null) return;

        // Wire up ViewModel property changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Create BindingSource for the ViewModel
        var viewModelBinding = new BindingSource
        {
            DataSource = ViewModel
        };

        // Initialize BindingSource for grid data
        if (_budgetBindingSource == null)
        {
            _budgetBindingSource = new BindingSource();
        }

        // Bind grid data source through BindingSource
        if (_budgetGrid != null)
        {
            _budgetBindingSource.DataSource = ViewModel.FilteredBudgetEntries;
            _budgetGrid.DataSource = _budgetBindingSource;
        }

        // Bind search textbox with two-way binding
        if (_searchTextBox != null)
        {
            _searchTextBox.DataBindings.Add(
                nameof(_searchTextBox.Text),
                viewModelBinding,
                nameof(ViewModel.SearchText),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind variance threshold textbox
        if (_varianceThresholdTextBox != null)
        {
            _varianceThresholdTextBox.DataBindings.Add(
                nameof(_varianceThresholdTextBox.Text),
                viewModelBinding,
                nameof(ViewModel.VarianceThreshold),
                true,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind over budget checkbox
        if (_overBudgetCheckBox != null)
        {
            _overBudgetCheckBox.DataBindings.Add(
                nameof(_overBudgetCheckBox.Checked),
                viewModelBinding,
                nameof(ViewModel.ShowOnlyOverBudget),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind under budget checkbox
        if (_underBudgetCheckBox != null)
        {
            _underBudgetCheckBox.DataBindings.Add(
                nameof(_underBudgetCheckBox.Checked),
                viewModelBinding,
                nameof(ViewModel.ShowOnlyUnderBudget),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind summary labels (handled in PropertyChanged for thread safety)
        // Removed DataBindings for TotalBudgeted, TotalActual, TotalVariance to prevent cross-thread issues

        // Bind status strip
        if (_statusLabel != null)
        {
            // Removed DataBindings.Add for StatusText to handle manually in PropertyChanged for thread safety
            // _statusLabel.DataBindings.Add(
            //     nameof(_statusLabel.Text),
            //     viewModelBinding,
            //     nameof(ViewModel.StatusText),
            //     false,
            //     DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind entity combo list if available
        if (_entityComboBox != null)
        {
            _entityComboBox.DataSource = ViewModel.AvailableEntities ?? new ObservableCollection<string>(new[] { "All Entities" });

            // Bind selected entity with two-way binding
            _entityComboBox.DataBindings.Add(
                nameof(_entityComboBox.SelectedItem),
                viewModelBinding,
                nameof(ViewModel.SelectedEntity),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind fiscal year combo
        if (_fiscalYearComboBox != null)
        {
            _fiscalYearComboBox.DataBindings.Add(
                nameof(_fiscalYearComboBox.SelectedItem),
                viewModelBinding,
                nameof(ViewModel.SelectedFiscalYear),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind department combo (using DepartmentId)
        if (_departmentComboBox != null)
        {
            _departmentComboBox.DataBindings.Add(
                nameof(_departmentComboBox.SelectedValue),
                viewModelBinding,
                nameof(ViewModel.SelectedDepartmentId),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Bind fund type combo
        if (_fundTypeComboBox != null)
        {
            _fundTypeComboBox.DataBindings.Add(
                nameof(_fundTypeComboBox.SelectedItem),
                viewModelBinding,
                nameof(ViewModel.SelectedFundType),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        Logger.LogDebug("BudgetPanel ViewModel bound with DataBindings");
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
            // Alternating row styling removed - let SFSkinManager handle theming



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
    /// Cancels invalid relational filters on string columns to prevent expression runtime errors.
    /// </summary>
    private void BudgetGrid_FilterChanging(object? sender, FilterChangingEventArgs e)
    {
        if (e?.Column?.MappingName == null)
        {
            return;
        }

        var isStringColumn =
            e.Column.MappingName == "AccountNumber" ||
            e.Column.MappingName == "AccountName" ||
            e.Column.MappingName == "DepartmentName" ||
            e.Column.MappingName == "EntityName" ||
            e.Column.MappingName == "FundTypeDescription";

        if (!isStringColumn)
        {
            return;
        }

        var hasRelationalPredicate = e.FilterPredicates.Any(p =>
            p.FilterType == Syncfusion.Data.FilterType.GreaterThan ||
            p.FilterType == Syncfusion.Data.FilterType.GreaterThanOrEqual ||
            p.FilterType == Syncfusion.Data.FilterType.LessThan ||
            p.FilterType == Syncfusion.Data.FilterType.LessThanOrEqual);

        if (!hasRelationalPredicate)
        {
            return;
        }

        e.Cancel = true;
        Logger.LogDebug("BudgetPanel: Cancelled invalid relational filter on string column {Column}", e.Column.MappingName);
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
            // Use the modern panel navigation service
            var panelNavigator = ServiceProvider is IServiceProvider provider
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IPanelNavigationService>(provider)
                : null;
            if (panelNavigator != null)
            {
                panelNavigator.ShowPanel<TPanel>(panelName);
                return;
            }

            // Fallback for older hosts
            var parentForm = this.FindForm();
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
    protected override void OnViewModelResolved(BudgetViewModel? viewModel)
    {
        base.OnViewModelResolved(viewModel);

        if (viewModel is null)
        {
            Logger.LogWarning("BudgetPanel.OnViewModelResolved — ViewModel was null");
            return;
        }

        try
        {
            Logger.LogInformation("BudgetPanel.OnViewModelResolved — building UI");

            InitializeControls();
            BindViewModel();
            _ = LoadAsync();

            Logger.LogInformation("BudgetPanel fully initialized and ready — glory achieved");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "BudgetPanel.OnViewModelResolved failed");
            MessageBox.Show($"BudgetPanel failed to initialize: {ex.Message}", "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Called when the panel becomes visible. This happens after layout is complete,
    /// handle is created, and size is final. Safe to configure SplitContainer here.
    /// </summary>
    private void BudgetPanel_VisibleChanged(object? sender, EventArgs e)
    {
        // Only configure once when becoming visible
        if (!Visible || _mainSplitContainer == null || _mainSplitContainer.IsDisposed || !_mainSplitContainer.IsHandleCreated)
            return;

        // Unsubscribe to avoid repeated configuration
        this.VisibleChanged -= BudgetPanel_VisibleChanged;

        try
        {
            // Clamp mins first (Syncfusion default min 25) using HEIGHT because orientation is Horizontal.
            var minimumTopPanel = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(220.0f);
            var minimumBottomPanel = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(260.0f);
            var splitterWidth = _mainSplitContainer.SplitterWidth;
            var availableHeight = _mainSplitContainer.Height;

            if (availableHeight <= minimumTopPanel + minimumBottomPanel + splitterWidth)
            {
                minimumTopPanel = Math.Max(25, (availableHeight - splitterWidth) / 2);
                minimumBottomPanel = Math.Max(25, availableHeight - splitterWidth - minimumTopPanel);
            }

            _mainSplitContainer.Panel1MinSize = minimumTopPanel;
            _mainSplitContainer.Panel2MinSize = minimumBottomPanel;

            // Safe distance
            int minDist = _mainSplitContainer.Panel1MinSize;
            int maxDist = _mainSplitContainer.Height - _mainSplitContainer.Panel2MinSize - _mainSplitContainer.SplitterWidth;
            int target = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250.0f);
            int safeDist = Math.Clamp(target, minDist, Math.Max(minDist, maxDist));

            _mainSplitContainer.SplitterDistance = safeDist;

            // Force a layout pass on the inner panels so their hosted controls render correctly
            // instead of collapsing to 0×0 during the initial docking resize pass.
            _mainSplitContainer.PerformLayout();
            _mainSplitContainer.Panel1?.PerformLayout();
            _mainSplitContainer.Panel2?.PerformLayout();

            Logger.LogInformation(
                "BudgetPanel SplitContainer configured in VisibleChanged: Panel1Min={P1}, Panel2Min={P2}, Distance={D}, Height={H}",
                _mainSplitContainer.Panel1MinSize, _mainSplitContainer.Panel2MinSize, safeDist, _mainSplitContainer.Height);

            // Queue a full recursive layout pass so any deeply nested controls also render.
            BeginInvoke(new System.Action(ForceFullLayout));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "BudgetPanel SplitContainer config failed in VisibleChanged - using fallback");
            try
            {
                _mainSplitContainer.SplitterDistance = _mainSplitContainer.Height / 2;  // safe fallback
            }
            catch
            {
                // Ignore fallback failures
            }
        }
    }

    private void BudgetGrid_CurrentCellActivated(object? sender, CurrentCellActivatedEventArgs e)
    {
        // Track that user is editing grid cell
        SetHasUnsavedChanges(true);
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SearchText = _searchTextBox?.Text ?? string.Empty;
            SetHasUnsavedChanges(true);
        }
    }

    private void FiscalYearComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _fiscalYearComboBox?.SelectedItem is int year)
        {
            ViewModel.SelectedFiscalYear = year;
            SetHasUnsavedChanges(true);
        }
    }

    private void EntityComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _entityComboBox?.SelectedItem is string entity && !string.Equals(entity, "All Entities", StringComparison.OrdinalIgnoreCase))
        {
            ViewModel.SelectedEntity = entity;
            SetHasUnsavedChanges(true);
        }
        else if (ViewModel != null)
        {
            ViewModel.SelectedEntity = null;
            SetHasUnsavedChanges(true);
        }
    }

    private void DepartmentComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Department filtering logic would go here
        SetHasUnsavedChanges(true);
    }

    private void FundTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && _fundTypeComboBox?.SelectedItem is string fundTypeString &&
            Enum.TryParse<FundType>(fundTypeString, out var fundType))
        {
            ViewModel.SelectedFundType = fundType;
            SetHasUnsavedChanges(true);
        }
        else if (ViewModel != null)
        {
            ViewModel.SelectedFundType = null;
            SetHasUnsavedChanges(true);
        }
    }

    private void VarianceThresholdTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null && decimal.TryParse(_varianceThresholdTextBox?.Text, out var threshold))
        {
            ViewModel.VarianceThreshold = threshold;
            SetHasUnsavedChanges(true);
        }
        else if (ViewModel != null)
        {
            ViewModel.VarianceThreshold = null;
            SetHasUnsavedChanges(true);
        }
    }

    private void OverBudgetCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ShowOnlyOverBudget = _overBudgetCheckBox?.Checked ?? false;
            SetHasUnsavedChanges(true);
        }
    }

    private void UnderBudgetCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ShowOnlyUnderBudget = _underBudgetCheckBox?.Checked ?? false;
            SetHasUnsavedChanges(true);
        }
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
                RowCount = 11,
                Padding = new Padding(10)
            };

            // Load Departments and Funds from database
            var departments = new List<Department>();
            var funds = new List<Fund>();
            try
            {
                if (ServiceProvider != null)
                {
                    var contextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IDbContextFactory<AppDbContext>>(ServiceProvider);
                    if (contextFactory != null)
                    {
                        using var context = contextFactory.CreateDbContext();
                        departments = context.Departments.OrderBy(d => d.Name).ToList();
                        funds = context.Funds.OrderBy(f => f.Name).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load departments and funds");
            }

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

            // Department Dropdown
            tableLayout.Controls.Add(new Label { Text = "Department:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 4);
            var cmbDepartment = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };
            cmbDepartment.DataSource = departments;
            if (departments.Count > 0) cmbDepartment.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbDepartment, 1, 4);

            // Fund Dropdown
            tableLayout.Controls.Add(new Label { Text = "Fund:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 5);
            var cmbFund = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };
            cmbFund.DataSource = funds;
            if (funds.Count > 0) cmbFund.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbFund, 1, 5);

            // Fund Type
            tableLayout.Controls.Add(new Label { Text = "Fund Type:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 6);
            var cmbFundType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFundType.Items.AddRange(Enum.GetNames(typeof(FundType)));
            cmbFundType.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbFundType, 1, 6);

            // Buttons
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var btnOK = new Syncfusion.WinForms.Controls.SfButton { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
            var btnCancel = new Syncfusion.WinForms.Controls.SfButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnOK);
            tableLayout.Controls.Add(btnPanel, 1, 10);
            tableLayout.SetColumnSpan(btnPanel, 2);

            form.Controls.Add(tableLayout);
            form.AcceptButton = btnOK;
            form.CancelButton = btnCancel;

            if (form.ShowDialog() == DialogResult.OK)
            {
                if (!decimal.TryParse(txtBudgeted.Text, out var budgeted) ||
                    !decimal.TryParse(txtActual.Text, out var actual))
                {
                    MessageBox.Show("Invalid input values. Please check your entries.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var viewModel = ViewModel;
                if (viewModel == null)
                {
                    MessageBox.Show("Budget data is not loaded yet. Please try again shortly.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var selectedDepartment = cmbDepartment.SelectedItem as Department;
                var selectedFund = cmbFund.SelectedItem as Fund;
                if (selectedDepartment == null || selectedFund == null)
                {
                    MessageBox.Show("Please select both Department and Fund.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var fiscalYear = viewModel.SelectedFiscalYear;
                var fundTypeName = cmbFundType.SelectedItem?.ToString();
                var fundType = Enum.TryParse<FundType>(fundTypeName, out var parsedFundType) ? parsedFundType : FundType.GeneralFund;

                var entry = new BudgetEntry
                {
                    AccountNumber = txtAccountNumber.Text.Trim(),
                    Description = txtDescription.Text.Trim(),
                    BudgetedAmount = budgeted,
                    ActualAmount = actual,
                    DepartmentId = selectedDepartment.Id,
                    FundId = selectedFund.Id,
                    FiscalYear = fiscalYear,
                    FundType = fundType,
                    Variance = budgeted - actual,
                    StartPeriod = new DateTime(fiscalYear, 1, 1),
                    EndPeriod = new DateTime(fiscalYear, 12, 31),
                    CreatedAt = DateTime.UtcNow
                };

                BeginInvoke(new Func<Task>(async () =>
                {
                    var operationToken = RegisterOperation();
                    IsBusy = true;
                    try
                    {
                        var panelValidation = await ValidateAsync(operationToken);
                        if (!panelValidation.IsValid)
                        {
                            ShowValidationDialog(panelValidation);
                            return;
                        }

                        var entryValidation = ValidateBudgetEntry(entry, txtAccountNumber, txtDescription, txtBudgeted, txtActual, null);
                        if (!entryValidation.IsValid)
                        {
                            ShowValidationDialog(entryValidation);
                            return;
                        }

                        await viewModel.AddEntryAsync(entry, operationToken);
                        SetHasUnsavedChanges(false);
                        UpdateStatus("Budget entry added successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.LogDebug("Add entry operation cancelled");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in AddEntryButton_Click");
            MessageBox.Show($"Error adding entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowValidationDialog(ValidationResult validationResult)
    {
        if (validationResult.IsValid || validationResult.Errors.Length == 0)
        {
            return;
        }

        foreach (var error in validationResult.Errors)
        {
            Logger.LogWarning("Validation failed: {FieldName} - {Message}", error.FieldName, error.Message);
        }

        var formattedErrors = validationResult.Errors
            .Select(error => string.IsNullOrWhiteSpace(error.FieldName) ? error.Message : $"{error.FieldName}: {error.Message}")
            .ToList();

        ValidationDialog.Show(this, "Validation Error", "Please resolve the following issues before continuing:", formattedErrors, Logger);

        var focusControl = validationResult.Errors.Select(error => error.ControlRef).FirstOrDefault(control => control != null);
        focusControl?.Focus();
    }

    private ValidationResult ValidateBudgetEntry(BudgetEntry entry, Control? accountControl, Control? descriptionControl, Control? budgetControl, Control? actualControl, Control? departmentControl)
    {
        var errors = new List<ValidationItem>();

        if (string.IsNullOrWhiteSpace(entry.AccountNumber))
        {
            errors.Add(new ValidationItem("Account Number", "Account number is required.", ValidationSeverity.Error, accountControl));
        }

        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            errors.Add(new ValidationItem("Description", "Description is required.", ValidationSeverity.Error, descriptionControl));
        }

        if (entry.BudgetedAmount < 0)
        {
            errors.Add(new ValidationItem("Budgeted Amount", "Budgeted amount must be zero or greater.", ValidationSeverity.Error, budgetControl));
        }

        if (entry.ActualAmount < 0)
        {
            errors.Add(new ValidationItem("Actual Amount", "Actual amount must be zero or greater.", ValidationSeverity.Error, actualControl));
        }

        if (entry.DepartmentId <= 0)
        {
            errors.Add(new ValidationItem("Department ID", "Department ID must be a positive integer.", ValidationSeverity.Error, departmentControl));
        }

        return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failed(errors.ToArray());
    }

    private void EditEntryButton_Click(object? sender, EventArgs e)
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
                RowCount = 11,
                Padding = new Padding(10)
            };

            // Load Departments and Funds from database
            var departments = new List<Department>();
            var funds = new List<Fund>();
            try
            {
                if (ServiceProvider != null)
                {
                    var contextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IDbContextFactory<AppDbContext>>(ServiceProvider);
                    if (contextFactory != null)
                    {
                        using var context = contextFactory.CreateDbContext();
                        departments = context.Departments.OrderBy(d => d.Name).ToList();
                        funds = context.Funds.OrderBy(f => f.Name).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load departments and funds");
            }

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

            // Department Dropdown
            tableLayout.Controls.Add(new Label { Text = "Department:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 4);
            var cmbDepartment = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };
            cmbDepartment.DataSource = departments;
            if (departments.Any())
            {
                var selectedDept = departments.FirstOrDefault(d => d.Id == selectedEntry.DepartmentId);
                if (selectedDept != null) cmbDepartment.SelectedItem = selectedDept;
            }
            tableLayout.Controls.Add(cmbDepartment, 1, 4);

            // Fund Dropdown
            tableLayout.Controls.Add(new Label { Text = "Fund:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 5);
            var cmbFund = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };
            cmbFund.DataSource = funds;
            if (funds.Any() && selectedEntry.FundId.HasValue)
            {
                var selectedFund = funds.FirstOrDefault(f => f.Id == selectedEntry.FundId.Value);
                if (selectedFund != null) cmbFund.SelectedItem = selectedFund;
            }
            tableLayout.Controls.Add(cmbFund, 1, 5);

            // Fund Type
            tableLayout.Controls.Add(new Label { Text = "Fund Type:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 6);
            var cmbFundType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFundType.Items.AddRange(Enum.GetNames(typeof(FundType)));
            cmbFundType.SelectedItem = selectedEntry.FundType.ToString();
            tableLayout.Controls.Add(cmbFundType, 1, 6);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var btnOK = new Syncfusion.WinForms.Controls.SfButton { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
            var btnCancel = new Syncfusion.WinForms.Controls.SfButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnOK);
            tableLayout.Controls.Add(btnPanel, 1, 10);
            tableLayout.SetColumnSpan(btnPanel, 2);

            form.Controls.Add(tableLayout);
            form.AcceptButton = btnOK;
            form.CancelButton = btnCancel;

            if (form.ShowDialog() == DialogResult.OK)
            {
                if (!decimal.TryParse(txtBudgeted.Text, out var budgeted) ||
                    !decimal.TryParse(txtActual.Text, out var actual))
                {
                    MessageBox.Show("Invalid input values. Please check your entries.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var selectedDepartment = cmbDepartment.SelectedItem as Department;
                var selectedFund = cmbFund.SelectedItem as Fund;
                if (selectedDepartment == null || selectedFund == null)
                {
                    MessageBox.Show("Please select both Department and Fund.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                selectedEntry.AccountNumber = txtAccountNumber.Text.Trim();
                selectedEntry.Description = txtDescription.Text.Trim();
                selectedEntry.BudgetedAmount = budgeted;
                selectedEntry.ActualAmount = actual;
                selectedEntry.DepartmentId = selectedDepartment.Id;
                selectedEntry.FundId = selectedFund.Id;
                selectedEntry.FundType = Enum.Parse<FundType>(cmbFundType.SelectedItem?.ToString() ?? "GeneralFund");
                selectedEntry.Variance = budgeted - actual;
                selectedEntry.UpdatedAt = DateTime.UtcNow;

                BeginInvoke(new Func<Task>(async () =>
                {
                    var operationToken = RegisterOperation();
                    IsBusy = true;
                    try
                    {
                        await ViewModel!.UpdateEntryAsync(selectedEntry, operationToken);
                        SetHasUnsavedChanges(false);
                        UpdateStatus("Budget entry updated successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.LogDebug("Update entry operation cancelled");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in EditEntryButton_Click");
            MessageBox.Show($"Error editing entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteEntryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_budgetGrid?.SelectedItems == null || _budgetGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a budget entry to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedEntry = _budgetGrid.SelectedItems[0] as BudgetEntry;
            if (selectedEntry == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete budget entry '{selectedEntry.AccountNumber} - {selectedEntry.Description}'?\n\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Logger.LogInformation("Deleting budget entry {Id}: {AccountNumber}", selectedEntry.Id, selectedEntry.AccountNumber);
                var operationToken = RegisterOperation();
                IsBusy = true;
                try
                {
                    await ViewModel!.DeleteEntryAsync(selectedEntry.Id, operationToken);
                    SetHasUnsavedChanges(false);
                    UpdateStatus($"Deleted budget entry {selectedEntry.AccountNumber}");
                }
                catch (OperationCanceledException)
                {
                    Logger.LogDebug("Delete entry operation cancelled");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in DeleteEntryAsync");
            MessageBox.Show($"Error deleting entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

        BeginInvoke(new Func<Task>(async () =>
        {
            var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                owner: this,
                operationKey: $"{nameof(BudgetPanel)}.Csv",
                dialogTitle: "Export Budget Entries to CSV",
                filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                defaultExtension: "csv",
                defaultFileName: $"Budget_Entries_{DateTime.Now:yyyyMMdd}.csv",
                exportAction: async (filePath, cancellationToken) =>
                {
                    IsBusy = true;
                    try
                    {
                        await ViewModel.ExportToCsvCommand.ExecuteAsync(filePath);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                },
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
                Logger.LogDebug("CSV export cancelled");
                UpdateStatus("CSV export cancelled");
                return;
            }

            if (!result.IsSuccess)
            {
                Logger.LogError("CSV export failed: {ErrorMessage}", result.ErrorMessage);
                UpdateStatus($"CSV export failed: {result.ErrorMessage}");
                return;
            }

            UpdateStatus("CSV export completed successfully");
        }));
    }

    private void MappingWizardPanel_MappingApplied(object? sender, MappingAppliedEventArgs e)
    {
        if (ViewModel == null) return;

        BeginInvoke(new Func<Task>(async () =>
        {
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
        }));
    }

    private void MappingWizardPanel_Cancelled(object? sender, EventArgs e)
    {
        if (_mappingContainer != null) _mappingContainer.Visible = false;
        UpdateStatus("Import cancelled");
    }

    private void ExportPdfButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        BeginInvoke(new Func<Task>(async () =>
        {
            var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                owner: this,
                operationKey: $"{nameof(BudgetPanel)}.Pdf",
                dialogTitle: "Export Budget Entries to PDF",
                filter: "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
                defaultExtension: "pdf",
                defaultFileName: $"Budget_Report_{DateTime.Now:yyyyMMdd}.pdf",
                exportAction: async (filePath, cancellationToken) =>
                {
                    IsBusy = true;
                    try
                    {
                        await ViewModel.ExportToPdfCommand.ExecuteAsync(filePath);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                },
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
                Logger.LogDebug("PDF export cancelled");
                UpdateStatus("PDF export cancelled");
                return;
            }

            if (!result.IsSuccess)
            {
                Logger.LogError("PDF export failed: {ErrorMessage}", result.ErrorMessage);
                UpdateStatus($"PDF export failed: {result.ErrorMessage}");
                return;
            }

            UpdateStatus("PDF export completed successfully");
        }));
    }

    private void ExportExcelButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        BeginInvoke(new Func<Task>(async () =>
        {
            var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
                owner: this,
                operationKey: $"{nameof(BudgetPanel)}.Excel",
                dialogTitle: "Export Budget Entries to Excel",
                filter: "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                defaultExtension: "xlsx",
                defaultFileName: $"Budget_Entries_{DateTime.Now:yyyyMMdd}.xlsx",
                exportAction: async (filePath, cancellationToken) =>
                {
                    IsBusy = true;
                    try
                    {
                        await ViewModel.ExportToExcelCommand.ExecuteAsync(filePath);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                },
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
                Logger.LogDebug("Excel export cancelled");
                UpdateStatus("Excel export cancelled");
                return;
            }

            if (!result.IsSuccess)
            {
                Logger.LogError("Excel export failed: {ErrorMessage}", result.ErrorMessage);
                UpdateStatus($"Excel export failed: {result.ErrorMessage}");
                return;
            }

            UpdateStatus("Excel export completed successfully");
        }));
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        // Ensure UI updates happen on the UI thread (PropertyChanged can fire from background threads)
        if (InvokeRequired)
        {
            Invoke(new System.Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

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
                // Show "no data" overlay when not loading and no entries exist
                if (_noDataOverlay != null)
                {
                    _noDataOverlay.Visible = !ViewModel.IsLoading && (ViewModel.BudgetEntries == null || !ViewModel.BudgetEntries.Any());
                }
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
        this.InvokeIfRequired(() =>
        {
            try
            {
                if (_statusLabel != null && !_statusLabel.IsDisposed)
                    _statusLabel.Text = message ?? string.Empty;
            }
            catch { }
        });
    }

    private void OnLoadBudgetsButtonClick(object? sender, EventArgs e)
    {
        if (ViewModel != null)
            BeginInvoke(new Func<Task>(async () => await ViewModel.LoadBudgetsCommand.ExecuteAsync(null)));
    }

    /// <summary>
    /// Overrides ValidateAsync to validate budget entries and panel state.
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        var errors = new List<ValidationItem>();

        // Check ViewModel is loaded
        if (ViewModel == null)
        {
            errors.Add(new ValidationItem("ViewModel", "Budget data not loaded", ValidationSeverity.Error, this));
            return ValidationResult.Failed(errors.ToArray());
        }

        // Check grid has data
        if (ViewModel.BudgetEntries == null || !ViewModel.BudgetEntries.Any())
        {
            errors.Add(new ValidationItem("Entries", "No budget entries available", ValidationSeverity.Warning, _budgetGrid));
        }

        // Validate that fiscal year is selected
        if (ViewModel.SelectedFiscalYear <= 0)
        {
            errors.Add(new ValidationItem("FiscalYear", "Fiscal year must be selected", ValidationSeverity.Error, _fiscalYearComboBox));
        }

        return await Task.FromResult(
            errors.Count > 0 ? ValidationResult.Failed(errors.ToArray()) : ValidationResult.Success);
    }

    /// <summary>
    /// Overrides FocusFirstError to focus the first control with validation error.
    /// </summary>
    public override void FocusFirstError()
    {
        if (ValidationErrors.Count == 0) return;

        var firstError = ValidationErrors.FirstOrDefault();
        if (firstError?.ControlRef is Control control && !control.IsDisposed)
        {
            control.Focus();
        }
    }

    /// <summary>
    /// Overrides SaveAsync to persist changes with proper validation and error handling.
    /// </summary>
    public override async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (ViewModel == null) return;

        var operationToken = RegisterOperation();
        IsBusy = true;
        try
        {
            Logger.LogInformation("Saving budget changes");
            UpdateStatus("Saving changes...");

            // Validate before saving
            var validation = await ValidateAsync(operationToken);
            if (!validation.IsValid)
            {
                ShowValidationDialog(validation);
                FocusFirstError();
                return;
            }

            // Budget data is persisted via CRUD operations (Add/Edit/Delete)
            // No explicit save command needed - entries are saved individually
            UpdateStatus("Changes saved successfully");
            SetHasUnsavedChanges(false);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Save operation cancelled");
            UpdateStatus("Save cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving changes");
            UpdateStatus($"Error: {ex.Message}");
            MessageBox.Show($"Error saving changes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (ViewModel == null) return;

        try
        {
            // Auto-load data on panel load via LoadAsync override - fire-and-forget with error handling
            LoadAsyncSafe();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading panel data");
        }
    }

    /// <summary>
    /// Overrides LoadAsync to load budget data with proper token and error handling.
    /// </summary>
    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded || IsBusy) return; // Prevent double-load and re-entrant loads

        if (ViewModel == null) return;

        var operationToken = RegisterOperation();
        IsBusy = true;
        try
        {
            Logger.LogInformation("Loading budget data");
            UpdateStatus("Loading budget data...");

            await ViewModel.LoadBudgetsCommand.ExecuteAsync(null);
            await ViewModel.RefreshAnalysisCommand.ExecuteAsync(null);

            UpdateStatus("Data loaded successfully");
            IsLoaded = true;
            SetHasUnsavedChanges(false); // Clear dirty flag after load
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Load operation cancelled");
            UpdateStatus("Load cancelled");
            IsLoaded = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading data");
            UpdateStatus($"Error: {ex.Message}");
            MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            IsLoaded = false;
        }
        finally
        {
            IsBusy = false;
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
    /// Triggers a deferred ForceFullLayout after DockingManager finishes its resize pass.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);   // starts the 180ms _finalLayoutTimer in ScopedPanelBase

        BeginInvoke(() =>
        {
            ForceFullLayout();
            if (_mainSplitContainer != null)
            {
                var target = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250.0f);
                var maxDist = _mainSplitContainer.Height - _mainSplitContainer.Panel2MinSize - _mainSplitContainer.SplitterWidth;
                _mainSplitContainer.SplitterDistance = Math.Clamp(target, _mainSplitContainer.Panel1MinSize, Math.Max(_mainSplitContainer.Panel1MinSize, maxDist));
            }
            Logger.LogDebug("[{Panel}] FINAL layout pass after docking — controls now visible", GetType().Name);
        });
    }

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
                if (_panelHeaderHelpHandler != null)
                    _panelHeader.HelpClicked -= _panelHeaderHelpHandler;
                if (_panelHeaderCloseHandler != null)
                    _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
            }
            if (_searchTextBox != null && _searchTextChangedHandler != null)
            {
                _searchTextBox.TextChanged -= _searchTextChangedHandler;
            }
            if (_fiscalYearComboBox != null && _fiscalYearChangedHandler != null)
            {
                _fiscalYearComboBox.SelectedIndexChanged -= _fiscalYearChangedHandler;
            }
            if (_entityComboBox != null && _entityChangedHandler != null)
            {
                _entityComboBox.SelectedIndexChanged -= _entityChangedHandler;
            }
            if (_departmentComboBox != null && _departmentChangedHandler != null)
            {
                _departmentComboBox.SelectedIndexChanged -= _departmentChangedHandler;
            }
            if (_fundTypeComboBox != null && _fundTypeChangedHandler != null)
            {
                _fundTypeComboBox.SelectedIndexChanged -= _fundTypeChangedHandler;
            }
            if (_varianceThresholdTextBox != null && _varianceThresholdChangedHandler != null)
            {
                _varianceThresholdTextBox.TextChanged -= _varianceThresholdChangedHandler;
            }
            if (_overBudgetCheckBox != null && _overBudgetCheckChangedHandler != null)
            {
                _overBudgetCheckBox.CheckStateChanged -= _overBudgetCheckChangedHandler;
            }
            if (_underBudgetCheckBox != null && _underBudgetCheckChangedHandler != null)
            {
                _underBudgetCheckBox.CheckStateChanged -= _underBudgetCheckChangedHandler;
            }
            if (_mappingWizardPanel != null)
            {
                if (_mappingWizardAppliedHandler != null)
                {
                    _mappingWizardPanel.MappingApplied -= _mappingWizardAppliedHandler;
                }
                if (_mappingWizardCancelledHandler != null)
                {
                    _mappingWizardPanel.Cancelled -= _mappingWizardCancelledHandler;
                }
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
                _budgetGrid.ToolTipOpening -= BudgetGrid_ToolTipOpening;
                _budgetGrid.CurrentCellActivated -= BudgetGrid_CurrentCellActivated;
                _budgetGrid.FilterChanging -= BudgetGrid_FilterChanging;
            }

            // Dispose BindingSource
            try { _budgetBindingSource?.Dispose(); } catch { }
            try { _controlToolTip?.Dispose(); } catch { }

            // SafeDispose for Syncfusion controls
            _budgetGrid?.SafeClearDataSource();
            _budgetGrid?.SafeDispose();

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
        this.AutoScroll = false;
        this.Padding = Padding.Empty;
        this.ResumeLayout(false);

    }

    #endregion
}
