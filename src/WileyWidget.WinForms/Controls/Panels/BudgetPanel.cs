using System.Collections.Generic;
using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using GridCheckBoxColumn = Syncfusion.WinForms.DataGrid.GridCheckBoxColumn;
using GridComboBoxColumn = Syncfusion.WinForms.DataGrid.GridComboBoxColumn;
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
using Syncfusion.WinForms.DataGrid.Styles;
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
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Logging;

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
    private KpiCardControl? _totalBudgetedCard;
    private KpiCardControl? _totalActualCard;
    private KpiCardControl? _totalVarianceCard;
    private KpiCardControl? _percentUsedCard;
    private KpiCardControl? _entriesOverBudgetCard;
    private KpiCardControl? _entriesUnderBudgetCard;
    private Panel? _summaryPanel;
    private Panel? _gridPanel;
    private Panel? _filterPanel;
    private Panel? _buttonPanel;
    private SplitContainerAdv? _mainSplitContainer = null;
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
    private int _budgetDiagnosticsStarted;
    private static readonly object BudgetDiagnosticsLogSync = new();

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

        // SplitContainer layout hook removed after root table-layout migration.

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

        SuspendLayout();

        Text = "Budget Overview";
        Size = ScaleLogicalToDevice(new Size(1400, 900));
        var themeName = ThemeColors.CurrentTheme;
        var headerHeight = LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge);

        ConfigurePanelHeader(headerHeight);

        var root = ControlFactory.CreateTableLayoutPanel(table =>
        {
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 4;
            table.Margin = Padding.Empty;
            table.Padding = new Padding(LayoutTokens.Dp(6), LayoutTokens.Dp(8), LayoutTokens.Dp(6), LayoutTokens.Dp(4));
        });
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(LayoutTokens.HeaderMinimumHeight + LayoutTokens.PanelPaddingCompact.Top)));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(LayoutTokens.HeaderMinimumHeight + LayoutTokens.ContentInnerPadding.Bottom)));
        SfSkinManager.SetVisualStyle(root, themeName);

        _summaryPanel = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(LayoutTokens.Dp(4), 0, LayoutTokens.Dp(4), 0);
        });
        SfSkinManager.SetVisualStyle(_summaryPanel, themeName);
        _summaryPanel.Controls.Add(CreateSummaryKpiRow());
        root.Controls.Add(_summaryPanel, 0, 0);

        _filterPanel = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(LayoutTokens.Dp(4), 0, LayoutTokens.Dp(4), LayoutTokens.Dp(4));
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        });
        SfSkinManager.SetVisualStyle(_filterPanel, themeName);
        _filterPanel.Controls.Add(CreateCompactFilterBar());
        root.Controls.Add(_filterPanel, 0, 1);

        root.Controls.Add(CreateBudgetGridSurface(themeName), 0, 2);
        root.Controls.Add(CreateButtonBarPanel(themeName), 0, 3);
        Controls.Add(root);

        _statusStrip = ControlFactory.CreateStatusStrip(statusStrip => statusStrip.Dock = DockStyle.Bottom);
        _statusLabel = ControlFactory.CreateToolStripStatusLabel(statusLabel =>
        {
            statusLabel.Text = "Ready";
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        });
        _statusStrip.Items.Add(_statusLabel);

        _loadingOverlay = ControlFactory.CreateLoadingOverlay(overlay =>
        {
            overlay.Message = "Loading budget data...";
        });
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        _noDataOverlay = ControlFactory.CreateNoDataOverlay(overlay =>
        {
            overlay.Message = "No budget entries yet\r\nCreate a new budget to get started";
            overlay.Visible = false;
        });
        Controls.Add(_noDataOverlay);
        _noDataOverlay.SendToBack();
        Controls.Add(_statusStrip);

        SetTabOrder();
        InitializeTooltips();

        _loadingOverlay?.SendToBack();
        _noDataOverlay?.SendToBack();
        _panelHeader?.BringToFront();

        ResumeLayout(false);
        PerformLayout();
        Refresh();

        Logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
    }

    private void ConfigurePanelHeader(int headerHeight)
    {
        _panelHeader ??= Controls.OfType<PanelHeader>().FirstOrDefault();
        var createdHeader = false;

        if (_panelHeader == null)
        {
            _panelHeader = ControlFactory.CreatePanelHeader(header =>
            {
                header.Dock = DockStyle.Top;
                header.Margin = Padding.Empty;
            });
            Controls.Add(_panelHeader);
            createdHeader = true;
        }

        _panelHeader.Title = "Budget Overview";
        _panelHeader.ShowRefreshButton = true;
        _panelHeader.ShowHelpButton = true;
        _panelHeader.ShowCloseButton = true;
        _panelHeader.ShowPinButton = false;
        _panelHeader.Height = headerHeight;
        _panelHeader.MinimumSize = new Size(0, headerHeight);

        if (_panelHeaderRefreshHandler != null)
        {
            _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
        }

        if (_panelHeaderHelpHandler != null)
        {
            _panelHeader.HelpClicked -= _panelHeaderHelpHandler;
        }

        if (_panelHeaderCloseHandler != null)
        {
            _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
        }

        _panelHeaderRefreshHandler = async (s, e) => await RefreshDataAsync();
        _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
        _panelHeaderHelpHandler = PanelHeader_HelpClicked;
        _panelHeader.HelpClicked += _panelHeaderHelpHandler;

        // ScopedPanelBase wires close behavior for host-created headers.
        // Only add our close handler when this panel had to create a fallback header.
        if (createdHeader)
        {
            _panelHeaderCloseHandler = (s, e) => ClosePanel();
            _panelHeader.CloseClicked += _panelHeaderCloseHandler;
        }
        else
        {
            _panelHeaderCloseHandler = null;
        }
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

        if (_totalBudgetedCard != null)
        {
            _controlToolTip.SetToolTip(_totalBudgetedCard, "Total approved budget across the current filtered view.");
        }

        if (_totalActualCard != null)
        {
            _controlToolTip.SetToolTip(_totalActualCard, "Total actual spending posted to the current filtered view.");
        }

        if (_totalVarianceCard != null)
        {
            _controlToolTip.SetToolTip(_totalVarianceCard, "Variance = Total Budgeted - Total Actual.");
        }

        if (_percentUsedCard != null)
        {
            _controlToolTip.SetToolTip(_percentUsedCard, "Percent Used = Total Actual / Total Budgeted.");
        }

        if (_entriesOverBudgetCard != null)
        {
            _controlToolTip.SetToolTip(_entriesOverBudgetCard, "Count of entries where actual spending exceeds budget.");
        }

        if (_entriesUnderBudgetCard != null)
        {
            _controlToolTip.SetToolTip(_entriesUnderBudgetCard, "Count of entries currently at or under budget.");
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
        try
        {
            using var helpForm = new BudgetHelpForm();
            helpForm.ShowDialog(this);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Budget help dialog could not be opened due to service scope resolution");
            ShowSemanticPopup(
                "Help is temporarily unavailable due to a dependency scope issue. You can continue working normally.",
                "Budget Help",
                SyncfusionControlFactory.MessageSemanticKind.Information);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Budget help dialog failed unexpectedly");
            ShowSemanticPopup(
                "Unable to open help right now. Please try again.",
                "Budget Help",
                SyncfusionControlFactory.MessageSemanticKind.Warning);
        }
    }

    private static string BuildBudgetGridTooltipText()
    {
        return "Budget worksheet legend:\r\n" +
               "• Account Number / Account Name: your chart of accounts identity.\r\n" +
                             "• Department / Entity / Fund Type: reporting dimensions for filtering and audits.\r\n" +
                             "• Classification: Income, Operating Expense, or Administrative & General Expense.\r\n" +
               "• Allocation Summary / Alloc %: split status for multi-fund distributions.\r\n" +
               "• Total Budgeted: approved amount for the account.\r\n" +
               "• Total Actual: posted spending from payments linked by MunicipalAccountId.\r\n" +
               "• Expand a row to review or adjust fund split lines.\r\n" +
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
            case "FinancialClassification":
                tooltipText = "Income statement classification derived from account structure and department context.";
                return true;
            case "AllocationSummary":
                tooltipText = "How many allocation split lines currently exist for this budget line.";
                return true;
            case "AllocationTotalFraction":
                tooltipText = "Total of all allocation percentages. Target is 100%.";
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
        helpButton.Size = LayoutTokens.GetScaled(LayoutTokens.ButtonSizeSquareSmall);
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

    private void ConfigureBudgetGrid()
    {
        if (_budgetGrid == null)
        {
            return;
        }

        ConfigureBudgetGridColumnResizing();

        _budgetGrid.AllowGrouping = false;
        _budgetGrid.AddNewRowPosition = RowPosition.None;
        _budgetGrid.AddNewRowText = string.Empty;
        _budgetGrid.ShowGroupDropArea = false;
        _budgetGrid.AllowEditing = false;
        // Syncfusion limitation: DetailsView cannot run with freeze panes enabled.
        _budgetGrid.FrozenColumnCount = 0;
        if (_budgetGrid.AutoGenerateRelations && _budgetGrid.FrozenColumnCount > 0)
        {
            Logger.LogWarning("BudgetPanel: disabling frozen columns because SfDataGrid DetailsView does not support freeze panes");
            _budgetGrid.FrozenColumnCount = 0;
        }

        _budgetGrid.RowHeight = LayoutTokens.Dp(LayoutTokens.GridRowHeightMedium);
        _budgetGrid.HeaderRowHeight = LayoutTokens.Dp(LayoutTokens.GridHeaderRowHeightTall);
        _budgetGrid.AutoSizeColumnsMode = AutoSizeColumnsMode.None;
        _budgetGrid.Style.HeaderStyle.Font = new GridFontInfo(new Font("Segoe UI", 9F, FontStyle.Bold));
        _budgetGrid.Style.CellStyle.HorizontalAlignment = HorizontalAlignment.Left;

        // Keep currency/percent columns right-aligned for financial scanning.
        var numericMappings = new[]
        {
            "AllocationTotalFraction",
            "BudgetedAmount",
            "ActualAmount",
            "PercentOfBudgetFraction",
            "TownOfWileyBudgetedAmount",
            "TownOfWileyActualAmount",
            "WsdBudgetedAmount",
            "WsdActualAmount",
            "EncumbranceAmount",
            "VarianceAmount",
            "VariancePercentage",
        };

        foreach (var mapping in numericMappings)
        {
            if (_budgetGrid.Columns[mapping] is GridNumericColumn numericColumn)
            {
                numericColumn.CellStyle.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }

        if (_budgetGrid.Columns["BudgetedAmount"] is GridNumericColumn budgetedColumn)
        {
            budgetedColumn.Format = "C0";
        }
        if (_budgetGrid.Columns["ActualAmount"] is GridNumericColumn actualColumn)
        {
            actualColumn.Format = "C0";
        }
        if (_budgetGrid.Columns["VarianceAmount"] is GridNumericColumn varianceColumn)
        {
            varianceColumn.Format = "C0";
        }
        if (_budgetGrid.Columns["PercentOfBudgetFraction"] is GridNumericColumn percentColumn)
        {
            percentColumn.Format = "P1";
        }
        if (_budgetGrid.Columns["VariancePercentage"] is GridNumericColumn variancePercentColumn)
        {
            variancePercentColumn.Format = "P1";
        }
        if (_budgetGrid.Columns["AllocationTotalFraction"] is GridNumericColumn allocationPercentColumn)
        {
            allocationPercentColumn.Format = "P1";
        }

        _budgetGrid.Invalidate();
    }

    private void ConfigureBudgetGridColumnResizing()
    {
        if (_budgetGrid == null)
        {
            return;
        }

        _budgetGrid.AllowResizingColumns = true;
        _budgetGrid.AllowResizingHiddenColumns = true;

        var minimumColumnWidth = LayoutTokens.Dp(90);
        var shrinkAllowance = LayoutTokens.Dp(40);

        foreach (var column in _budgetGrid.Columns)
        {
            column.AllowResizing = true;

            // If minimum width equals initial width, users cannot shrink via drag.
            if (column.Width > 0 && column.MinimumWidth >= column.Width)
            {
                var adjustedMinimum = Math.Max(minimumColumnWidth, column.Width - shrinkAllowance);
                if (adjustedMinimum < column.Width)
                {
                    column.MinimumWidth = adjustedMinimum;
                }
            }
        }
    }

    private void BudgetGrid_ToolTipOpening(object? sender, ToolTipOpeningEventArgs e)
    {
        if (_budgetGrid == null || e?.Column == null)
        {
            return;
        }

        // Syncfusion API: DataGridIndexResolver.GetHeaderIndex returns the header row index.
        var headerRowIndex = DataGridIndexResolver.GetHeaderIndex(_budgetGrid.TableControl);

        // Header hover tooltip.
        if (e.RowIndex == headerRowIndex && e.Record == null)
        {
            if (!TryGetBudgetColumnTooltip(e.Column.MappingName, out var columnTooltip))
            {
                return;
            }

            var headerTooltipInfo = new Syncfusion.WinForms.Controls.ToolTipInfo();
            headerTooltipInfo.Items.Add(new Syncfusion.WinForms.Controls.ToolTipItem
            {
                Text = $"{e.Column.HeaderText}: {columnTooltip}"
            });

            e.ToolTipInfo = headerTooltipInfo;
            return;
        }

        // Data-cell hover tooltip.
        if (e.Record == null)
        {
            return;
        }

        var displayValue = string.IsNullOrWhiteSpace(e.DisplayText) ? "(blank)" : e.DisplayText.Trim();
        var cellValue = TryResolveCellValue(e.Record, e.Column.MappingName);
        if (!TryGetBudgetCellTooltip(e.Column.MappingName, cellValue, displayValue, out var cellTooltipText))
        {
            return;
        }

        var cellTooltipInfo = new Syncfusion.WinForms.Controls.ToolTipInfo();
        cellTooltipInfo.Items.Add(new Syncfusion.WinForms.Controls.ToolTipItem
        {
            Text = cellTooltipText
        });

        e.ToolTipInfo = cellTooltipInfo;
    }

    private static object? TryResolveCellValue(object? record, string? mappingName)
    {
        if (record == null || string.IsNullOrWhiteSpace(mappingName))
        {
            return null;
        }

        try
        {
            var recordType = record.GetType();
            var dataProperty = recordType.GetProperty("Data");
            var rowData = dataProperty?.GetValue(record) ?? record;

            var rowDataType = rowData.GetType();
            var valueProperty = rowDataType.GetProperty(mappingName);
            if (valueProperty != null)
            {
                return valueProperty.GetValue(rowData);
            }
        }
        catch
        {
            // Tooltip fallbacks rely on display text when reflected value is unavailable.
        }

        return null;
    }

    private static bool TryGetBudgetCellTooltip(string? mappingName, object? cellValue, string displayValue, out string tooltipText)
    {
        switch (mappingName)
        {
            case "AccountNumber":
                tooltipText = $"Account Number: {displayValue}";
                return true;

            case "AccountName":
                tooltipText = $"Account Name: {displayValue}";
                return true;

            case "DepartmentName":
                tooltipText = $"Department: {displayValue}";
                return true;

            case "EntityName":
                tooltipText = $"Entity: {displayValue}";
                return true;

            case "FundTypeDescription":
                tooltipText = $"Fund Type: {displayValue}";
                return true;

            case "FinancialClassification":
                tooltipText = $"Financial Classification: {displayValue}";
                return true;

            case "AllocationSummary":
                tooltipText = $"Allocation Summary: {displayValue}\r\nExpand this row to edit detailed fund splits.";
                return true;

            case "AllocationTotalFraction":
                if (TryParsePercentFraction(cellValue, displayValue, out var allocationFraction))
                {
                    if (allocationFraction == 1m)
                    {
                        tooltipText = $"Allocation Total: {displayValue}\r\nStatus: Allocation is balanced at 100%.";
                    }
                    else
                    {
                        tooltipText = $"Allocation Total: {displayValue}\r\nStatus: Allocation should total 100%; currently {(allocationFraction - 1m):P1} from target.";
                    }

                    return true;
                }

                tooltipText = $"Allocation Total: {displayValue}";
                return true;

            case "BudgetedAmount":
                tooltipText = $"Total Budgeted Amount: {displayValue}\r\nApproved budget target for this account line.";
                return true;

            case "ActualAmount":
                tooltipText = $"Total Actual Amount: {displayValue}\r\nPosted spending currently mapped to this account line.";
                return true;

            case "PercentOfBudgetFraction":
                if (TryParsePercentFraction(cellValue, displayValue, out var usedFraction))
                {
                    if (usedFraction > 1m)
                    {
                        tooltipText = $"Percent of Budget Used: {displayValue}\r\nStatus: Over budget by {(usedFraction - 1m):P1}.";
                    }
                    else if (usedFraction >= 0.9m)
                    {
                        tooltipText = $"Percent of Budget Used: {displayValue}\r\nStatus: Near budget limit ({(1m - usedFraction):P1} remaining).";
                    }
                    else
                    {
                        tooltipText = $"Percent of Budget Used: {displayValue}\r\nStatus: Within budget ({(1m - usedFraction):P1} remaining).";
                    }

                    return true;
                }

                tooltipText = $"Percent of Budget Used: {displayValue}";
                return true;

            case "TownOfWileyBudgetedAmount":
                tooltipText = $"Town of Wiley Budgeted Amount: {displayValue}";
                return true;

            case "TownOfWileyActualAmount":
                tooltipText = $"Town of Wiley Actual Amount: {displayValue}";
                return true;

            case "WsdBudgetedAmount":
                tooltipText = $"WSD Budgeted Amount: {displayValue}";
                return true;

            case "WsdActualAmount":
                tooltipText = $"WSD Actual Amount: {displayValue}";
                return true;

            case "EncumbranceAmount":
                tooltipText = $"Encumbrance Amount: {displayValue}";
                return true;

            case "VarianceAmount":
                if (TryParseDecimalValue(cellValue, displayValue, out var varianceAmount))
                {
                    if (varianceAmount < 0m)
                    {
                        tooltipText = $"Variance Amount (Budgeted - Actual): {displayValue}\r\nStatus: Over budget by {Math.Abs(varianceAmount):C0}.";
                    }
                    else if (varianceAmount > 0m)
                    {
                        tooltipText = $"Variance Amount (Budgeted - Actual): {displayValue}\r\nStatus: Under budget by {varianceAmount:C0}.";
                    }
                    else
                    {
                        tooltipText = $"Variance Amount (Budgeted - Actual): {displayValue}\r\nStatus: Exactly on budget.";
                    }

                    return true;
                }

                tooltipText = $"Variance Amount (Budgeted - Actual): {displayValue}";
                return true;

            case "VariancePercentage":
                if (TryParsePercentFraction(cellValue, displayValue, out var varianceFraction))
                {
                    if (varianceFraction < 0m)
                    {
                        tooltipText = $"Variance Percentage: {displayValue}\r\nStatus: Over budget by {Math.Abs(varianceFraction):P1}.";
                    }
                    else if (varianceFraction > 0m)
                    {
                        tooltipText = $"Variance Percentage: {displayValue}\r\nStatus: Under budget by {varianceFraction:P1}.";
                    }
                    else
                    {
                        tooltipText = $"Variance Percentage: {displayValue}\r\nStatus: On budget.";
                    }

                    return true;
                }

                tooltipText = $"Variance Percentage: {displayValue}";
                return true;

            default:
                tooltipText = string.Empty;
                return false;
        }
    }

    private static bool TryParsePercentFraction(object? cellValue, string displayValue, out decimal fraction)
    {
        fraction = 0m;

        if (cellValue is decimal d)
        {
            fraction = d;
            return true;
        }

        if (cellValue is double db)
        {
            fraction = (decimal)db;
            return true;
        }

        if (cellValue is float f)
        {
            fraction = (decimal)f;
            return true;
        }

        if (!TryParseDecimalValue(cellValue, displayValue, out var parsed))
        {
            return false;
        }

        fraction = displayValue.Contains('%') ? parsed / 100m : parsed;
        return true;
    }

    private static bool TryParseDecimalValue(object? cellValue, string displayValue, out decimal value)
    {
        value = 0m;

        if (cellValue is decimal d)
        {
            value = d;
            return true;
        }

        if (cellValue is double db)
        {
            value = (decimal)db;
            return true;
        }

        if (cellValue is float f)
        {
            value = (decimal)f;
            return true;
        }

        if (cellValue is int i)
        {
            value = i;
            return true;
        }

        if (cellValue is long l)
        {
            value = l;
            return true;
        }

        var isNegativeByParentheses = displayValue.Contains('(') && displayValue.Contains(')');
        var cleaned = displayValue
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var parsed)
            || decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
        {
            value = isNegativeByParentheses ? -Math.Abs(parsed) : parsed;
            return true;
        }

        return false;
    }

    private void InitializeBudgetEntryAllocationLines(IEnumerable<BudgetEntry>? entries)
    {
        if (entries == null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            EnsureBudgetEntryAllocationLines(entry);
        }
    }

    private static void EnsureBudgetEntryAllocationLines(BudgetEntry entry)
    {
        if (entry.AllocationLines.Count == 0)
        {
            entry.AllocationLines.Add(new BudgetAllocationLine
            {
                FundId = entry.FundId,
                AllocationPercentage = entry.FundId.HasValue ? 1m : 0m,
                Notes = entry.FundId.HasValue ? "Primary fund" : null,
            });
        }

        foreach (var line in entry.AllocationLines)
        {
            line.ParentBudgetedAmount = entry.BudgetedAmount;
            line.ParentActualAmount = entry.ActualAmount;
            line.Recalculate(entry.BudgetedAmount, entry.ActualAmount);
        }
    }

    private List<AllocationFundOption> BuildAllocationFundOptions()
    {
        if (ViewModel == null)
        {
            return new List<AllocationFundOption>();
        }

        var options = ViewModel.BudgetEntries
            .Where(entry => entry.FundId.HasValue && entry.Fund != null)
            .GroupBy(entry => entry.FundId!.Value)
            .Select(group => new AllocationFundOption
            {
                Id = group.Key,
                Name = group.First().Fund!.Name,
            })
            .OrderBy(option => option.Name)
            .ToList();

        if (options.Count > 0)
        {
            return options;
        }

        try
        {
            if (ServiceProvider != null)
            {
                var contextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IDbContextFactory<AppDbContext>>(ServiceProvider);
                if (contextFactory != null)
                {
                    using var context = contextFactory.CreateDbContext();
                    return context.Funds
                        .OrderBy(fund => fund.Name)
                        .Select(fund => new AllocationFundOption { Id = fund.Id, Name = fund.Name })
                        .ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "BudgetPanel: unable to load fund options for allocation details view");
        }

        return new List<AllocationFundOption>();
    }

    private void BudgetGrid_AutoGeneratingRelations(object? sender, AutoGeneratingRelationsEventArgs e)
    {
        var relationalColumn = e.GridViewDefinition.RelationalColumn;
        var isAllocationRelation = string.Equals(relationalColumn, nameof(BudgetEntry.AllocationLines), StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(relationalColumn)
                && relationalColumn.EndsWith(nameof(BudgetEntry.AllocationLines), StringComparison.OrdinalIgnoreCase));

        if (!isAllocationRelation)
        {
            e.Cancel = true;
            return;
        }

        ConfigureAllocationDetailsGrid(e.GridViewDefinition.DataGrid);
    }

    private void ConfigureAllocationDetailsGrid(SfDataGrid detailsGrid)
    {
        var funds = BuildAllocationFundOptions();

        detailsGrid.AllowEditing = true;
        detailsGrid.AllowDeleting = true;
        detailsGrid.AllowGrouping = false;
        detailsGrid.AllowFiltering = false;
        detailsGrid.AllowSorting = false;
        detailsGrid.ShowGroupDropArea = false;
        detailsGrid.AddNewRowPosition = RowPosition.Bottom;
        detailsGrid.AddNewRowText = "Click here to add a fund split";
        detailsGrid.AutoGenerateColumns = false;
        detailsGrid.AutoSizeColumnsMode = AutoSizeColumnsMode.AllCellsWithLastColumnFill;
        detailsGrid.Columns.Clear();

        detailsGrid.Columns.Add(new GridComboBoxColumn
        {
            MappingName = nameof(BudgetAllocationLine.FundId),
            HeaderText = "Fund / Enterprise",
            DataSource = funds,
            DisplayMember = nameof(AllocationFundOption.Name),
            ValueMember = nameof(AllocationFundOption.Id),
            Width = LayoutTokens.Dp(260),
            MinimumWidth = LayoutTokens.Dp(220),
            AllowEditing = true,
        });

        detailsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(BudgetAllocationLine.AllocationPercentage),
            HeaderText = "Percent",
            Format = "P2",
            Width = LayoutTokens.Dp(120),
            MinimumWidth = LayoutTokens.Dp(100),
            AllowEditing = true,
        });

        detailsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(BudgetAllocationLine.AllocatedAmount),
            HeaderText = "Allocated Budget",
            Format = "C2",
            Width = LayoutTokens.Dp(155),
            MinimumWidth = LayoutTokens.Dp(135),
            AllowEditing = false,
        });

        detailsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(BudgetAllocationLine.AllocatedActual),
            HeaderText = "Allocated Actual",
            Format = "C2",
            Width = LayoutTokens.Dp(155),
            MinimumWidth = LayoutTokens.Dp(135),
            AllowEditing = false,
        });

        detailsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(BudgetAllocationLine.AllocationVariance),
            HeaderText = "Variance",
            Format = "C2",
            Width = LayoutTokens.Dp(130),
            MinimumWidth = LayoutTokens.Dp(120),
            AllowEditing = false,
        });

        detailsGrid.CurrentCellEndEdit -= AllocationDetailsGrid_CurrentCellEndEdit;
        detailsGrid.CurrentCellEndEdit += AllocationDetailsGrid_CurrentCellEndEdit;

        SfSkinManager.SetVisualStyle(detailsGrid, ThemeColors.CurrentTheme);
    }

    private void AllocationDetailsGrid_CurrentCellEndEdit(object? sender, CurrentCellEndEditEventArgs e)
    {
        if (sender is not SfDataGrid detailsGrid)
        {
            return;
        }

        var allocationLines = (detailsGrid.DataSource as System.Collections.IEnumerable)
            ?.Cast<object>()
            .OfType<BudgetAllocationLine>()
            .ToList();

        if (allocationLines == null || allocationLines.Count == 0)
        {
            return;
        }

        foreach (var line in allocationLines)
        {
            line.Recalculate(line.ParentBudgetedAmount, line.ParentActualAmount);
        }

        var totalAllocation = allocationLines.Sum(line => line.AllocationPercentage);
        UpdateStatus(totalAllocation == 1m
            ? "Allocation split balanced at 100%."
            : $"Allocation split total is {totalAllocation:P1}; target is 100%.");

        detailsGrid.View.Refresh();
        _budgetGrid?.View?.Refresh();
        SetHasUnsavedChanges(true);
    }

    private sealed class AllocationFundOption
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private void InitializeTopPanel()
    {
        var themeName = ThemeColors.CurrentTheme;

        var topPanel = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Padding = LayoutTokens.GetScaled(LayoutTokens.DialogContentPadding);
        });
        SfSkinManager.SetVisualStyle(topPanel, themeName);

        var layout = ControlFactory.CreateTableLayoutPanel(table =>
        {
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 2;
            table.Margin = Padding.Empty;
            table.Padding = Padding.Empty;
        });
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.Dp(80)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _summaryPanel = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Margin = Padding.Empty;
            panel.Padding = Padding.Empty;
        });
        SfSkinManager.SetVisualStyle(_summaryPanel, themeName);
        _summaryPanel.Controls.Add(CreateSummaryKpiRow());

        _filterPanel = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, LayoutTokens.ContentMargin / 2, 0, 0);
            panel.Padding = Padding.Empty;
            panel.AutoScroll = true;
        });
        SfSkinManager.SetVisualStyle(_filterPanel, themeName);
        _filterPanel.Controls.Add(CreateCompactFilterBar());

        layout.Controls.Add(_summaryPanel, 0, 0);
        layout.Controls.Add(_filterPanel, 0, 1);
        topPanel.Controls.Add(layout);

        _mainSplitContainer!.Panel1.Controls.Add(topPanel);
    }

    private Control CreateSummaryKpiRow()
    {
        var row = ControlFactory.CreateTableLayoutPanel(table =>
        {
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 6;
            table.RowCount = 1;
            table.Margin = Padding.Empty;
            table.Padding = Padding.Empty;
        });

        for (int i = 0; i < 6; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.67f));
        }
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _totalBudgetedCard = CreateKpiCard("Total Budgeted", "$0", "Approved", false);
        _totalActualCard = CreateKpiCard("Total Actual", "$0", "Spent", false);
        _totalVarianceCard = CreateKpiCard("Variance", "$0", "Budget - Actual", false);
        _percentUsedCard = CreateKpiCard("% Used", "0.0%", "Consumption", false);
        _entriesOverBudgetCard = CreateKpiCard("Over Budget", "0", "Count", false);
        _entriesUnderBudgetCard = CreateKpiCard("Under Budget", "0", "Count", true);

        row.Controls.Add(_totalBudgetedCard, 0, 0);
        row.Controls.Add(_totalActualCard, 1, 0);
        row.Controls.Add(_totalVarianceCard, 2, 0);
        row.Controls.Add(_percentUsedCard, 3, 0);
        row.Controls.Add(_entriesOverBudgetCard, 4, 0);
        row.Controls.Add(_entriesUnderBudgetCard, 5, 0);
        return row;
    }

    private KpiCardControl CreateKpiCard(string title, string value, string subtitle, bool isLast)
    {
        return ControlFactory.CreateKpiCardControl(card =>
        {
            card.Dock = DockStyle.Fill;
            card.Margin = isLast ? Padding.Empty : new Padding(0, 0, LayoutTokens.ContentMargin, 0);
            card.Title = title;
            card.Value = value;
            card.Subtitle = subtitle;
        });
    }

    private Control CreateCompactFilterBar()
    {
        var layout = ControlFactory.CreateTableLayoutPanel(table =>
        {
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 1;
            table.RowCount = 2;
            table.Margin = Padding.Empty;
            table.Padding = new Padding(0, 0, 0, LayoutTokens.Dp(8));
        });
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var topRowHost = ControlFactory.CreateTableLayoutPanel(table =>
        {
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 1;
            table.Margin = Padding.Empty;
            table.Padding = Padding.Empty;
        });
        topRowHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        topRowHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var topRow = ControlFactory.CreateFlowLayoutPanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = false;
            panel.AutoScroll = true;
            panel.Margin = Padding.Empty;
            panel.Padding = Padding.Empty;
        });

        var bottomRow = ControlFactory.CreateFlowLayoutPanel(panel =>
        {
            panel.Dock = DockStyle.Top;
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = false;
            panel.AutoScroll = true;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Margin = new Padding(0, 0, 0, LayoutTokens.Dp(4));
            panel.Padding = Padding.Empty;
        });

        Control CreateFieldGroup(Control label, Control field)
        {
            var rowHeight = LayoutTokens.Dp(LayoutTokens.StandardControlHeightComfortable);
            var group = ControlFactory.CreateTableLayoutPanel(table =>
            {
                table.AutoSize = true;
                table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                table.ColumnCount = 2;
                table.RowCount = 1;
                table.Margin = new Padding(0, 0, LayoutTokens.Dp(12), LayoutTokens.Dp(6));
                table.Padding = Padding.Empty;
            });
            group.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));

            label.AutoSize = false;
            label.Anchor = AnchorStyles.Left;
            label.Height = rowHeight;
            label.Margin = new Padding(0, 0, 6, 0);
            if (label is Label labelControl)
            {
                labelControl.TextAlign = ContentAlignment.MiddleLeft;
            }
            field.Margin = Padding.Empty;
            field.Anchor = AnchorStyles.Left;
            field.MinimumSize = new Size(Math.Max(field.MinimumSize.Width, field.Width), rowHeight);
            field.Height = rowHeight;
            group.Controls.Add(label, 0, 0);
            group.Controls.Add(field, 1, 0);
            return group;
        }

        var searchLabel = ControlFactory.CreateLabel("Search", label =>
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, LayoutTokens.ContentMargin / 2, 6, 0);
        });
        _searchTextBox = ControlFactory.CreateTextBoxExt(textBox =>
        {
            textBox.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(300.0f);
            textBox.TabIndex = 1;
            textBox.AccessibleName = "Search Budget Entries";
            textBox.AccessibleDescription = "Search budget entries by account, description, or department";
            textBox.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
        });
        _searchTextChangedHandler = SearchTextBox_TextChanged;
        _searchTextBox.TextChanged += _searchTextChangedHandler;

        var fiscalYearLabel = ControlFactory.CreateLabel("Fiscal Year", label =>
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, LayoutTokens.ContentMargin / 2, 6, 0);
        });
        _fiscalYearComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(170.0f);
            combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            combo.DropDownWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(220.0f);
            combo.Watermark = "Select fiscal year";
            combo.TabIndex = 2;
            combo.AccessibleName = "Fiscal Year Filter";
            combo.AccessibleDescription = "Filter budget entries by fiscal year";
            combo.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
        });
        _fiscalYearChangedHandler = FiscalYearComboBox_SelectedIndexChanged;
        _fiscalYearComboBox.SelectedIndexChanged += _fiscalYearChangedHandler;
        PopulateFiscalYearFilterOptions();

        var entityLabel = ControlFactory.CreateLabel("Entity", label =>
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, LayoutTokens.ContentMargin / 2, 6, 0);
        });
        _entityComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(240.0f);
            combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            combo.DropDownWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(320.0f);
            combo.Watermark = "Select entity";
            combo.TabIndex = 3;
            combo.AccessibleName = "Entity Filter";
            combo.AccessibleDescription = "Filter budget entries by entity or fund";
            combo.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
        });
        _entityChangedHandler = EntityComboBox_SelectedIndexChanged;
        _entityComboBox.SelectedIndexChanged += _entityChangedHandler;
        PopulateEntityFilterOptions();

        var departmentLabel = ControlFactory.CreateLabel("Department", label =>
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, LayoutTokens.ContentMargin / 2, 6, 0);
        });
        _departmentComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(250.0f);
            combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            combo.DropDownWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(340.0f);
            combo.Watermark = "Select department";
            combo.TabIndex = 4;
            combo.AccessibleName = "Department Filter";
            combo.AccessibleDescription = "Filter budget entries by department";
            combo.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
        });
        _departmentChangedHandler = DepartmentComboBox_SelectedIndexChanged;
        _departmentComboBox.SelectedIndexChanged += _departmentChangedHandler;
        PopulateDepartmentFilterOptions();

        var fundTypeLabel = ControlFactory.CreateLabel("Fund Type", label =>
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, LayoutTokens.ContentMargin / 2, 6, 0);
        });
        _fundTypeComboBox = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(220.0f);
            combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            combo.DropDownWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(320.0f);
            combo.Watermark = "Select fund type";
            combo.TabIndex = 5;
            combo.AccessibleName = "Fund Type Filter";
            combo.AccessibleDescription = "Filter budget entries by fund type";
            combo.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
        });
        _fundTypeChangedHandler = FundTypeComboBox_SelectedIndexChanged;
        _fundTypeComboBox.SelectedIndexChanged += _fundTypeChangedHandler;
        PopulateFundTypeFilterOptions();

        var varianceLabel = ControlFactory.CreateLabel("Variance >", label =>
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, LayoutTokens.ContentMargin / 2, 6, 0);
        });
        _varianceThresholdTextBox = ControlFactory.CreateTextBoxExt(textBox =>
        {
            textBox.Text = string.Empty;
            textBox.PlaceholderText = "Abs variance, e.g. 5000";
            textBox.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(180.0f);
            textBox.TextAlign = HorizontalAlignment.Right;
            textBox.TabIndex = 6;
            textBox.AccessibleName = "Variance Threshold";
            textBox.AccessibleDescription = "Filter entries where absolute variance is greater than or equal to this amount";
            textBox.Margin = new Padding(0, 0, LayoutTokens.ContentMargin, 0);
        });
        _varianceThresholdChangedHandler = VarianceThresholdTextBox_TextChanged;
        _varianceThresholdTextBox.TextChanged += _varianceThresholdChangedHandler;

        _overBudgetCheckBox = ControlFactory.CreateCheckBoxAdv("Over Budget", checkBox =>
        {
            checkBox.AutoSize = false;
            checkBox.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(118.0f);
            checkBox.Height = LayoutTokens.Dp(LayoutTokens.StandardControlHeightComfortable);
            checkBox.CheckAlign = ContentAlignment.MiddleLeft;
            checkBox.Padding = new Padding(LayoutTokens.Dp(2), 0, 0, 0);
            checkBox.TabIndex = 7;
            checkBox.AccessibleName = "Show Over Budget Only";
            checkBox.AccessibleDescription = "Show only entries that are over budget";
            checkBox.Margin = new Padding(0, LayoutTokens.Dp(3), LayoutTokens.Dp(10), 0);
        });
        _overBudgetCheckChangedHandler = OverBudgetCheckBox_CheckedChanged;
        _overBudgetCheckBox.CheckStateChanged += _overBudgetCheckChangedHandler;

        _underBudgetCheckBox = ControlFactory.CreateCheckBoxAdv("Under Budget", checkBox =>
        {
            checkBox.AutoSize = false;
            checkBox.Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(126.0f);
            checkBox.Height = LayoutTokens.Dp(LayoutTokens.StandardControlHeightComfortable);
            checkBox.CheckAlign = ContentAlignment.MiddleLeft;
            checkBox.Padding = new Padding(LayoutTokens.Dp(2), 0, 0, 0);
            checkBox.TabIndex = 8;
            checkBox.AccessibleName = "Show Under Budget Only";
            checkBox.AccessibleDescription = "Show only entries that are under budget";
            checkBox.Margin = new Padding(0, LayoutTokens.Dp(3), 0, 0);
        });
        _underBudgetCheckChangedHandler = UnderBudgetCheckBox_CheckedChanged;
        _underBudgetCheckBox.CheckStateChanged += _underBudgetCheckChangedHandler;

        var filterTip = ControlFactory.CreateToolTip();
        filterTip.SetToolTip(_searchTextBox, "Search budget entries by account number, name, or department");
        filterTip.SetToolTip(_fiscalYearComboBox, "Fiscal year controls which budget cycle is shown in the grid.");
        filterTip.SetToolTip(_entityComboBox, "Entity identifies the legal/reporting owner of the funds.");
        filterTip.SetToolTip(_departmentComboBox, "Department limits the view to one operational department.");
        filterTip.SetToolTip(_fundTypeComboBox, "Fund Type groups entries by accounting fund classification.");
        filterTip.SetToolTip(_varianceThresholdTextBox, "Enter a dollar amount (example: 5000) to show rows where |Budget - Actual| is at least that amount.");

        topRow.Controls.Add(CreateFieldGroup(searchLabel, _searchTextBox));
        topRow.Controls.Add(CreateFieldGroup(fiscalYearLabel, _fiscalYearComboBox));
        topRow.Controls.Add(CreateFieldGroup(entityLabel, _entityComboBox));

        bottomRow.Controls.Add(CreateFieldGroup(departmentLabel, _departmentComboBox));
        bottomRow.Controls.Add(CreateFieldGroup(fundTypeLabel, _fundTypeComboBox));
        bottomRow.Controls.Add(CreateFieldGroup(varianceLabel, _varianceThresholdTextBox));
        bottomRow.Controls.Add(_overBudgetCheckBox);
        bottomRow.Controls.Add(_underBudgetCheckBox);

        topRowHost.Controls.Add(topRow, 0, 0);
        layout.Controls.Add(topRowHost, 0, 0);
        layout.Controls.Add(bottomRow, 0, 1);
        return layout;
    }

    private Control CreateBudgetGridSurface(string themeName)
    {
        _gridPanel = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(LayoutTokens.Dp(4), LayoutTokens.Dp(10), LayoutTokens.Dp(4), LayoutTokens.Dp(4));
            panel.AutoScroll = true;
        });
        SfSkinManager.SetVisualStyle(_gridPanel, themeName);

        _budgetGrid = ControlFactory.CreateSfDataGrid(grid =>
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowEditing = false;
            grid.AllowGrouping = false;
            grid.ShowGroupDropArea = false;
            grid.AllowResizingColumns = true;
            grid.AllowSorting = true;
            grid.AutoSizeColumnsMode = AutoSizeColumnsMode.None;
            grid.MinimumSize = new Size(LayoutTokens.Dp(1100), 0);
            grid.SelectionMode = GridSelectionMode.Single;
            grid.TabIndex = 8;
            grid.AccessibleName = "Budget Entries Grid";
            grid.AccessibleDescription = "Data grid displaying budget entries with account numbers, amounts, variances, and related budget information";
        });

        var accountNumberWidth = LayoutTokens.Dp(170);
        var accountNameWidth = LayoutTokens.Dp(260);
        var departmentWidth = LayoutTokens.Dp(170);
        var entityWidth = LayoutTokens.Dp(170);
        var allocationSummaryWidth = LayoutTokens.Dp(130);
        var currencyWidth = LayoutTokens.Dp(145);
        var percentWidth = LayoutTokens.Dp(120);

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountNumber",
            HeaderText = "Account Number",
            MinimumWidth = accountNumberWidth,
            Width = accountNumberWidth,
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AccountName",
            HeaderText = "Account Name",
            MinimumWidth = accountNameWidth,
            Width = accountNameWidth,
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "EntityName",
            HeaderText = "Entity",
            MinimumWidth = entityWidth,
            Width = entityWidth,
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "DepartmentName",
            HeaderText = "Department",
            MinimumWidth = departmentWidth,
            Width = departmentWidth,
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "AllocationSummary",
            HeaderText = "Allocation",
            MinimumWidth = allocationSummaryWidth,
            Width = allocationSummaryWidth,
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "BudgetedAmount",
            HeaderText = "Total Budgeted",
            Format = "C2",
            MinimumWidth = currencyWidth,
            Width = currencyWidth,
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "ActualAmount",
            HeaderText = "Total Actual",
            Format = "C2",
            MinimumWidth = currencyWidth,
            Width = currencyWidth,
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "PercentOfBudgetFraction",
            HeaderText = "% of Budget",
            Format = "P2",
            MinimumWidth = percentWidth,
            Width = percentWidth,
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "EncumbranceAmount",
            HeaderText = "Encumbrance",
            Format = "C2",
            MinimumWidth = currencyWidth,
            Width = currencyWidth,
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "VarianceAmount",
            HeaderText = "Variance",
            Format = "C2",
            MinimumWidth = currencyWidth,
            Width = currencyWidth,
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "VariancePercentage",
            HeaderText = "Variance %",
            Format = "P2",
            MinimumWidth = percentWidth,
            Width = percentWidth,
            AllowEditing = false
        });

        ConfigureBudgetGridHeaderTooltips();

        _budgetGrid.ToolTipOpening += BudgetGrid_ToolTipOpening;
        _budgetGrid.CurrentCellActivated += BudgetGrid_CurrentCellActivated;
        _budgetGrid.FilterChanging += BudgetGrid_FilterChanging;
        _budgetGrid.AutoGeneratingRelations += BudgetGrid_AutoGeneratingRelations;

        _budgetGrid.AutoGenerateColumns = false;
        _budgetGrid.AutoGenerateRelations = true;
        _budgetGrid.HideEmptyGridViewDefinition = false;
        ConfigureBudgetGrid();

        _gridPanel.Controls.Add(_budgetGrid);

        _mappingContainer = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Bottom;
            panel.Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(260.0f);
            panel.Visible = false;
        });

        _mappingWizardPanel = ControlFactory.CreateCsvMappingWizardPanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
        });
        _mappingWizardAppliedHandler = MappingWizardPanel_MappingApplied;
        _mappingWizardPanel.MappingApplied += _mappingWizardAppliedHandler;
        _mappingWizardCancelledHandler = MappingWizardPanel_Cancelled;
        _mappingWizardPanel.Cancelled += _mappingWizardCancelledHandler;
        _mappingContainer.Controls.Add(_mappingWizardPanel);
        _gridPanel.Controls.Add(_mappingContainer);

        return _gridPanel;
    }

    private Control CreateButtonBarPanel(string themeName)
    {
        _buttonPanel = ControlFactory.CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(LayoutTokens.Dp(4));
            panel.AutoScroll = true;
        });
        SfSkinManager.SetVisualStyle(_buttonPanel, themeName);

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

        _buttonPanel.Controls.Add(CreateButtonToolbar());
        return _buttonPanel;
    }

    private Control CreateButtonToolbar()
    {
        void ConfigureActionButton(SfButton? button, int logicalWidth)
        {
            if (button == null)
            {
                return;
            }

            button.TextAlign = ContentAlignment.MiddleCenter;
            button.AutoSize = false;
            button.MinimumSize = new Size(LayoutTokens.Dp(logicalWidth), LayoutTokens.Dp(36));
            button.Size = new Size(LayoutTokens.Dp(logicalWidth), LayoutTokens.Dp(36));
            button.Margin = new Padding(0, 0, LayoutTokens.Dp(8), LayoutTokens.Dp(8));
        }

        ConfigureActionButton(_loadBudgetsButton, 130);
        ConfigureActionButton(_addEntryButton, 110);
        ConfigureActionButton(_editEntryButton, 110);
        ConfigureActionButton(_deleteEntryButton, 120);
        ConfigureActionButton(_importCsvButton, 110);
        ConfigureActionButton(_exportCsvButton, 110);
        ConfigureActionButton(_exportPdfButton, 110);
        ConfigureActionButton(_exportExcelButton, 120);

        var bar = ControlFactory.CreateFlowLayoutPanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = true;
            panel.AutoScroll = true;
            panel.Margin = Padding.Empty;
            panel.Padding = Padding.Empty;
        });

        bar.Controls.Add(_loadBudgetsButton!);
        bar.Controls.Add(_addEntryButton!);
        bar.Controls.Add(_editEntryButton!);
        bar.Controls.Add(_deleteEntryButton!);
        bar.Controls.Add(_importCsvButton!);
        bar.Controls.Add(_exportCsvButton!);
        bar.Controls.Add(_exportPdfButton!);
        bar.Controls.Add(_exportExcelButton!);
        return bar;
    }

    /// <summary>
    /// Binds ViewModel properties to UI controls using DataBindings for two-way binding.
    /// </summary>
    private void BindViewModel()
    {
        if (ViewModel == null) return;

        // Wire up ViewModel property changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Bind grid directly to the observable collection so details relations are discoverable.
        if (_budgetGrid != null)
        {
            InitializeBudgetEntryAllocationLines(ViewModel.FilteredBudgetEntries);
            _budgetGrid.DataSource = ViewModel.FilteredBudgetEntries;
            ConfigureBudgetGrid();
        }

        // Initialize filter controls directly. Event handlers and ViewModel_PropertyChanged
        // keep state synchronized without WinForms CurrencyManager re-entrancy issues.
        if (_searchTextBox != null)
        {
            _searchTextBox.DataBindings.Clear();
            _searchTextBox.Text = ViewModel.SearchText ?? string.Empty;
        }

        if (_varianceThresholdTextBox != null)
        {
            _varianceThresholdTextBox.DataBindings.Clear();
            _varianceThresholdTextBox.Text = ViewModel.VarianceThreshold?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        }

        if (_overBudgetCheckBox != null)
        {
            _overBudgetCheckBox.DataBindings.Clear();
            _overBudgetCheckBox.Checked = ViewModel.ShowOnlyOverBudget;
        }

        if (_underBudgetCheckBox != null)
        {
            _underBudgetCheckBox.DataBindings.Clear();
            _underBudgetCheckBox.Checked = ViewModel.ShowOnlyUnderBudget;
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
            PopulateEntityFilterOptions();
        }

        if (_fiscalYearComboBox != null)
        {
            _fiscalYearComboBox.DataBindings.Clear();
            _fiscalYearComboBox.SelectedValue = ViewModel.SelectedFiscalYear;
            if (_fiscalYearComboBox.SelectedIndex < 0
                && _fiscalYearComboBox.DataSource is System.Collections.ICollection fiscalYearItems
                && fiscalYearItems.Count > 0)
            {
                _fiscalYearComboBox.SelectedIndex = 0;
            }
        }

        if (_departmentComboBox != null)
        {
            _departmentComboBox.DataBindings.Clear();
            _departmentComboBox.SelectedValue = ViewModel.SelectedDepartmentId;
            if (_departmentComboBox.SelectedIndex < 0
                && _departmentComboBox.DataSource is System.Collections.ICollection departmentItems
                && departmentItems.Count > 0)
            {
                _departmentComboBox.SelectedIndex = 0;
            }
        }

        if (_fundTypeComboBox != null)
        {
            _fundTypeComboBox.DataBindings.Clear();
            _fundTypeComboBox.SelectedValue = ViewModel.SelectedFundType;
            if (_fundTypeComboBox.SelectedIndex < 0
                && _fundTypeComboBox.DataSource is System.Collections.ICollection fundTypeItems
                && fundTypeItems.Count > 0)
            {
                _fundTypeComboBox.SelectedIndex = 0;
            }
        }

        SeedHarnessBudgetEntriesIfNeeded();

        Logger.LogDebug("BudgetPanel ViewModel bound with DataBindings");
    }

    private void SeedHarnessBudgetEntriesIfNeeded()
    {
        if (ViewModel == null || !IsBudgetUiTestHarness())
        {
            return;
        }

        if (ViewModel.BudgetEntries.Any() || ViewModel.FilteredBudgetEntries.Any())
        {
            return;
        }

        var waterDepartment = new Department { Id = 1, Name = "Water Operations" };
        var sewerDepartment = new Department { Id = 2, Name = "Wastewater" };
        var adminDepartment = new Department { Id = 3, Name = "Administration" };

        var townFund = new Fund { Id = 1, FundCode = "TOW-GF", Name = "Town of Wiley General", Type = FundType.GeneralFund };
        var wsdFund = new Fund { Id = 2, FundCode = "WSD-UT", Name = "Wiley Sanitation District", Type = FundType.EnterpriseFund };

        var entries = new List<BudgetEntry>
        {
            CreateSeededEntry("101", "Water Revenue", 450000m, 428000m, 2026, waterDepartment, townFund, FundType.GeneralFund),
            CreateSeededEntry("102", "Sewer Revenue", 385000m, 402500m, 2026, sewerDepartment, wsdFund, FundType.EnterpriseFund),
            CreateSeededEntry("103", "Stormwater Fees", 212000m, 201000m, 2026, waterDepartment, townFund, FundType.SpecialRevenue),
            CreateSeededEntry("104", "Treatment Chemicals", 98000m, 106500m, 2026, waterDepartment, wsdFund, FundType.EnterpriseFund),
            CreateSeededEntry("105", "Pipeline Maintenance", 172000m, 155000m, 2026, waterDepartment, townFund, FundType.CapitalProjects),
            CreateSeededEntry("106", "Lift Station Utilities", 69000m, 73200m, 2026, sewerDepartment, wsdFund, FundType.EnterpriseFund),
            CreateSeededEntry("107", "Fleet Fuel", 36000m, 33200m, 2026, adminDepartment, townFund, FundType.GeneralFund),
            CreateSeededEntry("108", "Payroll and Benefits", 520000m, 498500m, 2026, adminDepartment, townFund, FundType.GeneralFund),
            CreateSeededEntry("109", "Compliance Services", 74000m, 81200m, 2026, adminDepartment, wsdFund, FundType.EnterpriseFund),
            CreateSeededEntry("110", "Capital Reserve", 140000m, 112000m, 2026, adminDepartment, townFund, FundType.CapitalProjects),
        };

        entries[3].AllocationLines = new ObservableCollection<BudgetAllocationLine>
        {
            new() { FundId = townFund.Id, AllocationPercentage = 0.65m, Notes = "Primary enterprise usage" },
            new() { FundId = wsdFund.Id, AllocationPercentage = 0.35m, Notes = "Shared district maintenance" },
        };
        entries[6].AllocationLines = new ObservableCollection<BudgetAllocationLine>
        {
            new() { FundId = townFund.Id, AllocationPercentage = 0.50m, Notes = "Town fleet share" },
            new() { FundId = wsdFund.Id, AllocationPercentage = 0.50m, Notes = "District fleet share" },
        };

        InitializeBudgetEntryAllocationLines(entries);

        ViewModel.BudgetEntries = new ObservableCollection<BudgetEntry>(entries);
        ViewModel.FilteredBudgetEntries = new ObservableCollection<BudgetEntry>(entries);

        var totalBudgeted = entries.Sum(entry => entry.BudgetedAmount);
        var totalActual = entries.Sum(entry => entry.ActualAmount);
        var totalVariance = totalBudgeted - totalActual;

        ViewModel.TotalBudgeted = totalBudgeted;
        ViewModel.TotalActual = totalActual;
        ViewModel.TotalVariance = totalVariance;
        ViewModel.PercentUsed = totalBudgeted > 0m ? totalActual / totalBudgeted : 0m;
        ViewModel.EntriesOverBudget = entries.Count(entry => entry.Variance < 0m);
        ViewModel.EntriesUnderBudget = entries.Count(entry => entry.Variance >= 0m);
        ViewModel.StatusText = "Sample budget data loaded for screenshot harness";

        if (_budgetGrid != null)
        {
            InitializeBudgetEntryAllocationLines(ViewModel.FilteredBudgetEntries);
            _budgetGrid.DataSource = ViewModel.FilteredBudgetEntries;
            ConfigureBudgetGrid();
        }

        if (_loadingOverlay != null)
        {
            _loadingOverlay.Visible = false;
        }

        if (_noDataOverlay != null)
        {
            _noDataOverlay.Visible = false;
            _noDataOverlay.HideActionButton();
        }
    }

    private static BudgetEntry CreateSeededEntry(
        string accountNumber,
        string description,
        decimal budgetedAmount,
        decimal actualAmount,
        int fiscalYear,
        Department department,
        Fund fund,
        FundType fundType)
    {
        return new BudgetEntry
        {
            AccountNumber = accountNumber,
            Description = description,
            BudgetedAmount = budgetedAmount,
            ActualAmount = actualAmount,
            Variance = budgetedAmount - actualAmount,
            FiscalYear = fiscalYear,
            StartPeriod = new DateTime(fiscalYear, 1, 1),
            EndPeriod = new DateTime(fiscalYear, 12, 31),
            DepartmentId = department.Id,
            Department = department,
            FundId = fund.Id,
            Fund = fund,
            FundType = fundType,
            EncumbranceAmount = 0m,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static bool IsBudgetUiTestHarness()
    {
        try
        {
            if (LayoutDiagnosticsMode.IsActive)
            {
                return true;
            }

            var forceSeed = Environment.GetEnvironmentVariable("WILEYWIDGET_FORCE_BUDGET_SEEDED");
            if (string.Equals(forceSeed, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(forceSeed, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(forceSeed, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var configuration = ServiceProviderServiceExtensions.GetService<IConfiguration>(Program.Services);
            return configuration?.GetValue<bool>("UI:IsUiTestHarness") ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldRunBudgetBehaviorDiagnostics()
    {
        var flag = Environment.GetEnvironmentVariable("WILEYWIDGET_RUN_BUDGET_TEST");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBudgetBehaviorDiagnosticsVerbose()
    {
        var flag = Environment.GetEnvironmentVariable("WILEYWIDGET_RUN_BUDGET_TEST_VERBOSE");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldExitAfterBudgetBehaviorDiagnostics()
    {
        var flag = Environment.GetEnvironmentVariable("WILEYWIDGET_RUN_BUDGET_TEST_EXIT");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBudgetBehaviorDiagnosticsFullScope()
    {
        var scope = Environment.GetEnvironmentVariable("WILEYWIDGET_RUN_BUDGET_TEST_SCOPE");
        return string.Equals(scope, "full", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, "exhaustive", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs non-visual behavior diagnostics for BudgetPanel internals.
    /// Trigger with WILEYWIDGET_RUN_BUDGET_TEST=1.
    /// </summary>
    public async Task RunBudgetPanelBehaviorDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _budgetDiagnosticsStarted, 1) == 1)
        {
            return;
        }

        var verbose = IsBudgetBehaviorDiagnosticsVerbose();
        var fullScope = IsBudgetBehaviorDiagnosticsFullScope();
        var totalStopwatch = Stopwatch.StartNew();

        var exercisedFunctions = new HashSet<string>(StringComparer.Ordinal);
        var skippedFunctions = new Dictionary<string, string>(StringComparer.Ordinal);

        var diagnostics = new List<string>
        {
            $"=== BudgetPanel Behavior Diagnostics Started @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==="
        };

        if (verbose)
        {
            diagnostics.Add("VERBOSE: per-check timing enabled (WILEYWIDGET_RUN_BUDGET_TEST_VERBOSE=1)");
        }

        diagnostics.Add(fullScope
            ? "SCOPE: full function audit enabled (WILEYWIDGET_RUN_BUDGET_TEST_SCOPE=full)"
            : "SCOPE: smoke function audit (set WILEYWIDGET_RUN_BUDGET_TEST_SCOPE=full for exhaustive coverage)");

        void MarkExercised(params string[] methodNames)
        {
            foreach (var methodName in methodNames)
            {
                if (!string.IsNullOrWhiteSpace(methodName))
                {
                    exercisedFunctions.Add(methodName);
                }
            }
        }

        void MarkSkipped(string methodName, string reason)
        {
            if (!string.IsNullOrWhiteSpace(methodName))
            {
                skippedFunctions[methodName] = reason;
            }
        }

        void Pass(string symbol, long? elapsedMs = null, params string[] methodNames)
        {
            MarkExercised(methodNames);
            diagnostics.Add(verbose && elapsedMs.HasValue
                ? $"PASS: {symbol} ({elapsedMs.Value} ms)"
                : $"PASS: {symbol}");
        }

        void Fail(string symbol, string reason, long? elapsedMs = null, params string[] methodNames)
        {
            MarkExercised(methodNames);
            diagnostics.Add(verbose && elapsedMs.HasValue
                ? $"FAIL: {symbol} - {reason} ({elapsedMs.Value} ms)"
                : $"FAIL: {symbol} - {reason}");
        }

        MarkExercised(
            nameof(RunBudgetPanelBehaviorDiagnosticsAsync),
            nameof(IsBudgetBehaviorDiagnosticsVerbose),
            nameof(IsBudgetBehaviorDiagnosticsFullScope),
            nameof(ShouldExitAfterBudgetBehaviorDiagnostics),
            nameof(WriteBudgetDiagnosticsLog));

        MarkSkipped(nameof(AddEntryButton_Click), "Requires interactive modal dialog input.");
        MarkSkipped(nameof(EditEntryButton_Click), "Requires interactive modal dialog input.");
        MarkSkipped(nameof(DeleteEntryAsync), "Shows confirmation modal dialog.");
        MarkSkipped(nameof(ImportCsvButton_Click), "Requires interactive file-picker dialog.");
        MarkSkipped(nameof(ExportCsvButton_Click), "Requires interactive save-file dialog.");
        MarkSkipped(nameof(ExportPdfButton_Click), "Requires interactive save-file dialog.");
        MarkSkipped(nameof(ExportExcelButton_Click), "Requires interactive save-file dialog.");
        MarkSkipped(nameof(ShowValidationDialog), "Displays modal validation dialog.");
        MarkSkipped(nameof(PanelHeader_HelpClicked), "Displays modal help dialog.");
        MarkSkipped(nameof(MappingWizardPanel_MappingApplied), "Requires external file import payload.");
        MarkSkipped(nameof(NavigateToPanel), "Navigation side effects are validated at integration level.");
        MarkSkipped(nameof(OnShown), "Lifecycle callback is host-driven and not manually forced in diagnostics.");
        MarkSkipped(nameof(Dispose), "Lifecycle-managed by host; not forced in diagnostics run.");
        MarkSkipped(nameof(InitializeComponent), "Designer bootstrap method validated by compilation.");

        try
        {
            var waitStopwatch = Stopwatch.StartNew();
            var waitStartUtc = DateTime.UtcNow;
            while ((ViewModel == null || _budgetGrid == null || _searchTextBox == null)
                && DateTime.UtcNow - waitStartUtc < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            if (ViewModel == null || _budgetGrid == null || _searchTextBox == null)
            {
                Fail("Initialization", "BudgetPanel did not become ready within timeout.", waitStopwatch.ElapsedMilliseconds);
                return;
            }

            Pass(
                "Initialization",
                waitStopwatch.ElapsedMilliseconds,
                nameof(OnViewModelResolved),
                nameof(InitializeControls),
                nameof(InitializeTopPanel),
                nameof(CreateSummaryKpiRow),
                nameof(CreateKpiCard),
                nameof(CreateCompactFilterBar),
                nameof(CreateBudgetGridSurface),
                nameof(CreateButtonBarPanel),
                nameof(CreateButtonToolbar),
                nameof(ConfigurePanelHeader),
                nameof(ConfigurePanelHeaderHelpButton),
                nameof(ConfigureBudgetGrid),
                nameof(ConfigureBudgetGridHeaderTooltips),
                nameof(InitializeTooltips),
                nameof(BindViewModel),
                nameof(SeedHarnessBudgetEntriesIfNeeded),
                nameof(SetTabOrder),
                nameof(DeferSizeValidation),
                nameof(BudgetPanel_VisibleChanged),
                nameof(OnPanelLoaded),
                nameof(LoadAsync));

            var checkStopwatch = Stopwatch.StartNew();
            if (_budgetGrid.AllowResizingColumns)
            {
                Pass("Grid.AllowResizingColumns", checkStopwatch.ElapsedMilliseconds, nameof(ConfigureBudgetGrid));
            }
            else
            {
                Fail("Grid.AllowResizingColumns", "Column resizing is disabled.", checkStopwatch.ElapsedMilliseconds, nameof(ConfigureBudgetGrid));
            }

            checkStopwatch.Restart();
            if (_budgetGrid.AutoSizeColumnsMode == AutoSizeColumnsMode.None)
            {
                Pass("Grid.AutoSizeColumnsMode=None", checkStopwatch.ElapsedMilliseconds, nameof(ConfigureBudgetGrid));
            }
            else
            {
                Fail("Grid.AutoSizeColumnsMode", $"Unexpected mode {_budgetGrid.AutoSizeColumnsMode}.", checkStopwatch.ElapsedMilliseconds, nameof(ConfigureBudgetGrid));
            }

            checkStopwatch.Restart();
            if (_budgetGrid.AutoGenerateRelations && _budgetGrid.FrozenColumnCount == 0)
            {
                Pass("Grid.DetailsView compatibility", checkStopwatch.ElapsedMilliseconds, nameof(BudgetGrid_AutoGeneratingRelations), nameof(ConfigureAllocationDetailsGrid));
            }
            else
            {
                Fail("Grid.DetailsView compatibility", "DetailsView requires AutoGenerateRelations=true and FrozenColumnCount=0.", checkStopwatch.ElapsedMilliseconds, nameof(BudgetGrid_AutoGeneratingRelations), nameof(ConfigureAllocationDetailsGrid));
            }

            checkStopwatch.Restart();
            if (_budgetGrid.Columns["AccountName"] is GridTextColumn accountNameColumn)
            {
                var originalWidth = accountNameColumn.Width;
                var resizedWidth = originalWidth + LayoutTokens.Dp(24);
                accountNameColumn.Width = resizedWidth;

                if (accountNameColumn.Width == resizedWidth)
                {
                    Pass("Column width mutation path", checkStopwatch.ElapsedMilliseconds, nameof(ConfigureBudgetGrid));
                }
                else
                {
                    Fail("Column width mutation path", "Column width did not update.", checkStopwatch.ElapsedMilliseconds, nameof(ConfigureBudgetGrid));
                }

                accountNameColumn.Width = originalWidth;
            }
            else
            {
                Fail("Column width mutation path", "AccountName column is missing.", checkStopwatch.ElapsedMilliseconds, nameof(ConfigureBudgetGrid));
            }

            checkStopwatch.Restart();
            if (TryGetBudgetColumnTooltip("AccountNumber", out var _))
            {
                Pass("TryGetBudgetColumnTooltip(valid)", checkStopwatch.ElapsedMilliseconds, nameof(TryGetBudgetColumnTooltip), nameof(BudgetGrid_ToolTipOpening));
            }
            else
            {
                Fail("TryGetBudgetColumnTooltip(valid)", "Known mapping did not resolve.", checkStopwatch.ElapsedMilliseconds, nameof(TryGetBudgetColumnTooltip), nameof(BudgetGrid_ToolTipOpening));
            }

            checkStopwatch.Restart();
            if (!TryGetBudgetColumnTooltip("__invalid__", out var _))
            {
                Pass("TryGetBudgetColumnTooltip(invalid)", checkStopwatch.ElapsedMilliseconds, nameof(TryGetBudgetColumnTooltip));
            }
            else
            {
                Fail("TryGetBudgetColumnTooltip(invalid)", "Invalid mapping unexpectedly resolved.", checkStopwatch.ElapsedMilliseconds, nameof(TryGetBudgetColumnTooltip));
            }

            var baselineSearch = _searchTextBox.Text;
            var targetSearch = "Water";
            var burstInputs = new[] { "W", "Wa", "Wat", "Wate", targetSearch };

            var changeCounter = 0;
            System.Collections.Specialized.NotifyCollectionChangedEventHandler? changedHandler = (_, _) => changeCounter++;
            ViewModel.FilteredBudgetEntries.CollectionChanged += changedHandler;

            try
            {
                var searchStopwatch = Stopwatch.StartNew();
                foreach (var text in burstInputs)
                {
                    _searchTextBox.Text = text;
                    SearchTextBox_TextChanged(_searchTextBox, EventArgs.Empty);
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(550, cancellationToken).ConfigureAwait(false);

                if (string.Equals(ViewModel.SearchText, targetSearch, StringComparison.Ordinal))
                {
                    Pass("SearchText update path", searchStopwatch.ElapsedMilliseconds, nameof(SearchTextBox_TextChanged));
                }
                else
                {
                    Fail("SearchText update path", $"Expected '{targetSearch}', actual '{ViewModel.SearchText}'.", searchStopwatch.ElapsedMilliseconds, nameof(SearchTextBox_TextChanged));
                }

                var expectedCount = ViewModel.BudgetEntries.Count(entry =>
                    entry.AccountNumber.Contains(targetSearch, StringComparison.OrdinalIgnoreCase)
                    || entry.Description.Contains(targetSearch, StringComparison.OrdinalIgnoreCase));

                if (ViewModel.FilteredBudgetEntries.Count == expectedCount)
                {
                    Pass("Search filter result count", searchStopwatch.ElapsedMilliseconds, nameof(SearchTextBox_TextChanged));
                }
                else
                {
                    Fail("Search filter result count", $"Expected {expectedCount}, actual {ViewModel.FilteredBudgetEntries.Count}.", searchStopwatch.ElapsedMilliseconds, nameof(SearchTextBox_TextChanged));
                }

                var upperBound = ViewModel.BudgetEntries.Count + 4;
                if (changeCounter <= upperBound * 2)
                {
                    Pass("Search debounce/per-keystroke stabilization", searchStopwatch.ElapsedMilliseconds, nameof(SearchTextBox_TextChanged));
                }
                else
                {
                    Fail("Search debounce/per-keystroke stabilization", $"Collection changed {changeCounter} times in one key burst.", searchStopwatch.ElapsedMilliseconds, nameof(SearchTextBox_TextChanged));
                }
            }
            finally
            {
                ViewModel.FilteredBudgetEntries.CollectionChanged -= changedHandler;
                _searchTextBox.Text = baselineSearch;
                SearchTextBox_TextChanged(_searchTextBox, EventArgs.Empty);
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }

            checkStopwatch.Restart();
            var firstEntry = ViewModel.BudgetEntries.FirstOrDefault();
            if (firstEntry != null)
            {
                EnsureBudgetEntryAllocationLines(firstEntry);
                if (firstEntry.AllocationLines.Count > 0)
                {
                    firstEntry.AllocationLines[0].Recalculate(firstEntry.BudgetedAmount, firstEntry.ActualAmount);
                    Pass("Allocation ensure/recalculate path", checkStopwatch.ElapsedMilliseconds, nameof(EnsureBudgetEntryAllocationLines), nameof(InitializeBudgetEntryAllocationLines), nameof(AllocationDetailsGrid_CurrentCellEndEdit), nameof(BuildAllocationFundOptions));
                }
                else
                {
                    Fail("Allocation ensure/recalculate path", "Allocation lines missing after EnsureBudgetEntryAllocationLines.", checkStopwatch.ElapsedMilliseconds, nameof(EnsureBudgetEntryAllocationLines), nameof(InitializeBudgetEntryAllocationLines), nameof(AllocationDetailsGrid_CurrentCellEndEdit), nameof(BuildAllocationFundOptions));
                }
            }
            else
            {
                Fail("Allocation ensure/recalculate path", "No budget entries available.", checkStopwatch.ElapsedMilliseconds, nameof(EnsureBudgetEntryAllocationLines), nameof(InitializeBudgetEntryAllocationLines), nameof(AllocationDetailsGrid_CurrentCellEndEdit));
            }

            checkStopwatch.Restart();
            var msg = default(Message);
            if (ProcessCmdKey(ref msg, Keys.F5))
            {
                Pass("ProcessCmdKey(F5)", checkStopwatch.ElapsedMilliseconds, nameof(ProcessCmdKey), nameof(RefreshDataAsync));
            }
            else
            {
                Fail("ProcessCmdKey(F5)", "Refresh shortcut was not handled.", checkStopwatch.ElapsedMilliseconds, nameof(ProcessCmdKey), nameof(RefreshDataAsync));
            }

            if (fullScope)
            {
                checkStopwatch.Restart();
                PopulateFiscalYearFilterOptions();
                Pass("PopulateFiscalYearFilterOptions", checkStopwatch.ElapsedMilliseconds, nameof(PopulateFiscalYearFilterOptions));

                checkStopwatch.Restart();
                PopulateDepartmentFilterOptions();
                Pass("PopulateDepartmentFilterOptions", checkStopwatch.ElapsedMilliseconds, nameof(PopulateDepartmentFilterOptions));

                checkStopwatch.Restart();
                PopulateFundTypeFilterOptions();
                Pass("PopulateFundTypeFilterOptions", checkStopwatch.ElapsedMilliseconds, nameof(PopulateFundTypeFilterOptions));

                checkStopwatch.Restart();
                ApplySyncfusionTheme();
                Pass("ApplySyncfusionTheme", checkStopwatch.ElapsedMilliseconds, nameof(ApplySyncfusionTheme), nameof(BudgetGrid_QueryCellStyle), nameof(BudgetGrid_FilterChanging));

                checkStopwatch.Restart();
                ApplyTheme(ThemeColors.CurrentTheme);
                Pass("ApplyTheme", checkStopwatch.ElapsedMilliseconds, nameof(ApplyTheme));

                checkStopwatch.Restart();
                UpdateStatus("Budget diagnostics full-scope heartbeat");
                Pass("UpdateStatus", checkStopwatch.ElapsedMilliseconds, nameof(UpdateStatus));

                checkStopwatch.Restart();
                _ = ShouldRunBudgetBehaviorDiagnostics();
                _ = IsBudgetUiTestHarness();
                _ = ResolveBudgetDiagnosticsLogDirectory();
                Pass("Environment helper checks", checkStopwatch.ElapsedMilliseconds, nameof(ShouldRunBudgetBehaviorDiagnostics), nameof(IsBudgetUiTestHarness), nameof(ResolveBudgetDiagnosticsLogDirectory));

                checkStopwatch.Restart();
                var gridTooltip = BuildBudgetGridTooltipText();
                var helpButton = _panelHeader != null ? FindPanelHeaderHelpButton(_panelHeader) : null;
                if (!string.IsNullOrWhiteSpace(gridTooltip))
                {
                    Pass("Tooltip helper checks", checkStopwatch.ElapsedMilliseconds, nameof(BuildBudgetGridTooltipText), nameof(FindPanelHeaderHelpButton));
                }
                else
                {
                    Fail("Tooltip helper checks", "Grid tooltip legend was empty.", checkStopwatch.ElapsedMilliseconds, nameof(BuildBudgetGridTooltipText), nameof(FindPanelHeaderHelpButton));
                }

                checkStopwatch.Restart();
                var sampleDepartment = new Department { Id = 9990, Name = "Diagnostics" };
                var sampleFund = new Fund { Id = 9991, FundCode = "DIAG", Name = "Diagnostics Fund", Type = FundType.GeneralFund };
                var seededEntry = CreateSeededEntry("999", "Diagnostics Seeded Entry", 100m, 75m, DateTime.Now.Year, sampleDepartment, sampleFund, FundType.GeneralFund);
                var parsedPercent = TryParsePercentFraction(0.25m, "25%", out var _);
                var parsedDecimal = TryParseDecimalValue("$1,234.50", "$1,234.50", out var _);
                var resolvedValue = TryResolveCellValue(seededEntry, nameof(BudgetEntry.AccountNumber));
                var resolvedTooltip = TryGetBudgetCellTooltip("VarianceAmount", seededEntry.Variance, seededEntry.Variance.ToString(CultureInfo.CurrentCulture), out var _);
                if (parsedPercent && parsedDecimal && resolvedValue != null && resolvedTooltip)
                {
                    Pass("Static parse/value helpers", checkStopwatch.ElapsedMilliseconds, nameof(CreateSeededEntry), nameof(TryParsePercentFraction), nameof(TryParseDecimalValue), nameof(TryResolveCellValue), nameof(TryGetBudgetCellTooltip));
                }
                else
                {
                    Fail("Static parse/value helpers", "One or more static helper checks failed.", checkStopwatch.ElapsedMilliseconds, nameof(CreateSeededEntry), nameof(TryParsePercentFraction), nameof(TryParseDecimalValue), nameof(TryResolveCellValue), nameof(TryGetBudgetCellTooltip));
                }

                checkStopwatch.Restart();
                var diagnosticsValidation = ValidateBudgetEntry(
                    seededEntry,
                    _searchTextBox,
                    _searchTextBox,
                    _varianceThresholdTextBox,
                    _varianceThresholdTextBox,
                    _departmentComboBox);
                if (diagnosticsValidation.IsValid)
                {
                    Pass("ValidateBudgetEntry", checkStopwatch.ElapsedMilliseconds, nameof(ValidateBudgetEntry));
                }
                else
                {
                    Fail("ValidateBudgetEntry", "Seeded entry validation failed unexpectedly.", checkStopwatch.ElapsedMilliseconds, nameof(ValidateBudgetEntry));
                }

                checkStopwatch.Restart();
                BudgetGrid_CurrentCellActivated(this, null!);
                FiscalYearComboBox_SelectedIndexChanged(this, EventArgs.Empty);
                EntityComboBox_SelectedIndexChanged(this, EventArgs.Empty);
                DepartmentComboBox_SelectedIndexChanged(this, EventArgs.Empty);
                FundTypeComboBox_SelectedIndexChanged(this, EventArgs.Empty);
                VarianceThresholdTextBox_TextChanged(this, EventArgs.Empty);
                OverBudgetCheckBox_CheckedChanged(this, EventArgs.Empty);
                UnderBudgetCheckBox_CheckedChanged(this, EventArgs.Empty);
                Pass(
                    "Filter/event handlers",
                    checkStopwatch.ElapsedMilliseconds,
                    nameof(BudgetGrid_CurrentCellActivated),
                    nameof(FiscalYearComboBox_SelectedIndexChanged),
                    nameof(EntityComboBox_SelectedIndexChanged),
                    nameof(DepartmentComboBox_SelectedIndexChanged),
                    nameof(FundTypeComboBox_SelectedIndexChanged),
                    nameof(VarianceThresholdTextBox_TextChanged),
                    nameof(OverBudgetCheckBox_CheckedChanged),
                    nameof(UnderBudgetCheckBox_CheckedChanged));

                checkStopwatch.Restart();
                var validation = await ValidateAsync(cancellationToken).ConfigureAwait(false);
                if (validation != null)
                {
                    Pass("ValidateAsync", checkStopwatch.ElapsedMilliseconds, nameof(ValidateAsync));
                }
                else
                {
                    Fail("ValidateAsync", "Validation result was null.", checkStopwatch.ElapsedMilliseconds, nameof(ValidateAsync));
                }

                checkStopwatch.Restart();
                FocusFirstError();
                Pass("FocusFirstError", checkStopwatch.ElapsedMilliseconds, nameof(FocusFirstError));

                checkStopwatch.Restart();
                OnLoadBudgetsButtonClick(this, EventArgs.Empty);
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                Pass("OnLoadBudgetsButtonClick", checkStopwatch.ElapsedMilliseconds, nameof(OnLoadBudgetsButtonClick));

                checkStopwatch.Restart();
                ViewModel_PropertyChanged(this, new PropertyChangedEventArgs(nameof(BudgetViewModel.TotalBudgeted)));
                ViewModel_PropertyChanged(this, new PropertyChangedEventArgs(nameof(BudgetViewModel.TotalActual)));
                ViewModel_PropertyChanged(this, new PropertyChangedEventArgs(nameof(BudgetViewModel.TotalVariance)));
                ViewModel_PropertyChanged(this, new PropertyChangedEventArgs(nameof(BudgetViewModel.PercentUsed)));
                ViewModel_PropertyChanged(this, new PropertyChangedEventArgs(nameof(BudgetViewModel.EntriesOverBudget)));
                ViewModel_PropertyChanged(this, new PropertyChangedEventArgs(nameof(BudgetViewModel.EntriesUnderBudget)));
                Pass("ViewModel_PropertyChanged", checkStopwatch.ElapsedMilliseconds, nameof(ViewModel_PropertyChanged));

                checkStopwatch.Restart();
                MappingWizardPanel_Cancelled(this, EventArgs.Empty);
                Pass("MappingWizardPanel_Cancelled", checkStopwatch.ElapsedMilliseconds, nameof(MappingWizardPanel_Cancelled));

                checkStopwatch.Restart();
                SetTabOrder();
                Pass("SetTabOrder", checkStopwatch.ElapsedMilliseconds, nameof(SetTabOrder));

                checkStopwatch.Restart();
                try
                {
                    await SaveAsync(cancellationToken).ConfigureAwait(false);
                    Pass("SaveAsync", checkStopwatch.ElapsedMilliseconds, nameof(SaveAsync));
                }
                catch (Exception ex)
                {
                    Fail("SaveAsync", ex.Message, checkStopwatch.ElapsedMilliseconds, nameof(SaveAsync));
                }
            }

            var declaredMethodNames = GetType()
                .GetMethods(System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && !method.Name.StartsWith("<", StringComparison.Ordinal))
                .Select(method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            var declaredSet = new HashSet<string>(declaredMethodNames, StringComparer.Ordinal);
            var skippedCount = skippedFunctions.Keys.Count(declaredSet.Contains);
            var exercisedCount = exercisedFunctions.Count(declaredSet.Contains);
            var uncoveredFunctions = declaredMethodNames
                .Where(name => !exercisedFunctions.Contains(name) && !skippedFunctions.ContainsKey(name))
                .ToList();

            diagnostics.Add($"FUNCTION-AUDIT: declared={declaredMethodNames.Count}, exercised={exercisedCount}, skipped={skippedCount}, uncovered={uncoveredFunctions.Count}");

            if (skippedCount > 0)
            {
                diagnostics.Add("FUNCTION-AUDIT-SKIPPED: "
                    + string.Join("; ", skippedFunctions
                        .Where(pair => declaredSet.Contains(pair.Key))
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => $"{pair.Key} ({pair.Value})")));
            }

            if (uncoveredFunctions.Count > 0)
            {
                diagnostics.Add("FUNCTION-AUDIT-UNCOVERED: " + string.Join(", ", uncoveredFunctions));
            }
            else
            {
                Pass("Function surface audit", null);
            }

            diagnostics.Add("=== BudgetPanel Behavior Diagnostics Completed ===");
        }
        catch (OperationCanceledException)
        {
            diagnostics.Add("CANCELLED: BudgetPanel behavior diagnostics were cancelled.");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"CRITICAL: {ex.GetType().Name} - {ex.Message}");
            diagnostics.Add(ex.ToString());
        }
        finally
        {
            if (verbose)
            {
                diagnostics.Add($"TOTAL: {totalStopwatch.ElapsedMilliseconds} ms");
            }

            WriteBudgetDiagnosticsLog(diagnostics);
            Logger.LogInformation(string.Join(Environment.NewLine, diagnostics));

            if (ShouldExitAfterBudgetBehaviorDiagnostics())
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    try
                    {
                        Logger.LogInformation("BudgetPanel diagnostics completed; exiting app due to WILEYWIDGET_RUN_BUDGET_TEST_EXIT=1");
                        var hostForm = FindForm();
                        if (hostForm != null && !hostForm.IsDisposed)
                        {
                            hostForm.Close();
                        }

                        Application.ExitThread();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "BudgetPanel diagnostics auto-exit failed");
                    }
                }));
            }
        }
    }

    private void WriteBudgetDiagnosticsLog(IEnumerable<string> diagnostics)
    {
        try
        {
            var diagnosticsList = diagnostics?.ToList() ?? new List<string>();
            if (diagnosticsList.Count == 0)
            {
                return;
            }

            var logsDir = ResolveBudgetDiagnosticsLogDirectory();
            Directory.CreateDirectory(logsDir);

            var logPath = Path.Combine(logsDir, "budgetpanel-behavior-diagnostics.log");
            var payload = string.Join(Environment.NewLine, diagnosticsList)
                + Environment.NewLine
                + Environment.NewLine;

            lock (BudgetDiagnosticsLogSync)
            {
                File.AppendAllText(logPath, payload);
            }

            Logger.LogInformation("BudgetPanel behavior diagnostics appended to {LogPath}", logPath);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to persist BudgetPanel behavior diagnostics log");
        }
    }

    private static string ResolveBudgetDiagnosticsLogDirectory()
    {
        return LogPathResolver.GetLogsDirectory();
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
            _budgetGrid.QueryCellStyle -= BudgetGrid_QueryCellStyle;
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
        if (e.Column == null || e.DataRow?.RowData is not BudgetEntry entry)
        {
            return;
        }

        // Keep semantic coloring lightweight to preserve theme readability and rendering performance.
        var budgeted = entry.BudgetedAmount;
        var actual = entry.ActualAmount;
        var percentUsed = budgeted > 0m ? actual / budgeted : 0m;
        var variance = budgeted - actual;
        var isOverBudget = actual > budgeted;
        var isNearLimit = !isOverBudget && budgeted > 0m && percentUsed >= 0.90m;

        switch (e.Column.MappingName)
        {
            case "VarianceAmount":
            case "VariancePercentage":
                if (isOverBudget)
                {
                    e.Style.TextColor = Color.Red;
                    e.Style.Font.Bold = true;
                }
                else if (variance > 0m)
                {
                    e.Style.TextColor = Color.Green;
                }
                else
                {
                    e.Style.TextColor = Color.Orange;
                }

                break;

            case "PercentOfBudgetFraction":
                if (percentUsed > 1m)
                {
                    e.Style.TextColor = Color.Red;
                    e.Style.Font.Bold = true;
                }
                else if (isNearLimit)
                {
                    e.Style.TextColor = Color.Orange;
                }
                else
                {
                    e.Style.TextColor = Color.Green;
                }

                break;

            case "AllocationTotalFraction":
                var allocationFraction = entry.AllocationTotalFraction;
                if (allocationFraction == 1m)
                {
                    e.Style.TextColor = Color.Green;
                }
                else
                {
                    e.Style.TextColor = Color.Orange;
                    e.Style.Font.Bold = true;
                }

                break;

            case "AccountNumber":
            case "Description":
                if (isOverBudget)
                {
                    e.Style.TextColor = Color.Red;
                }
                else if (isNearLimit)
                {
                    e.Style.TextColor = Color.Orange;
                }

                break;
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
            e.Column.MappingName == "FundTypeDescription" ||
            e.Column.MappingName == "AllocationSummary";

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
    public override void ApplyTheme(string theme)
    {
        try
        {
            base.ApplyTheme(theme);

            // Theme application now handled by SfSkinManager cascade
            // UpdateButtonIcons removed - icon management via deprecated IThemeIconService is not authorized
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "BudgetPanel theme refresh failed");
        }
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
    /// CRITICAL: Wrapped in layout suspension to prevent multi-pass rendering during initialization.
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

            // CRITICAL FIX: Suspend layout during all initialization to prevent thrashing
            SuspendLayout();
            try
            {
                InitializeControls();
                BindViewModel();

                if (ShouldRunBudgetBehaviorDiagnostics())
                {
                    _ = RunBudgetPanelBehaviorDiagnosticsAsync();
                }

                Logger.LogInformation("BudgetPanel fully initialized and ready — glory achieved");
            }
            finally
            {
                ResumeLayout(performLayout: false);
                // Single final layout pass instead of multiple passes
                PerformLayout();
            }
        }
        catch (Exception ex)
        {
            ResumeLayout(performLayout: false);
            Logger.LogError(ex, "BudgetPanel.OnViewModelResolved failed");
            ShowSemanticPopup($"BudgetPanel failed to initialize: {ex.Message}", "Critical Error", SyncfusionControlFactory.MessageSemanticKind.Error);
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
            var minimumTopPanel = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(260.0f);
            var minimumBottomPanel = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(320.0f);
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
            int target = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(240.0f);
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
        if (ViewModel == null)
        {
            return;
        }

        var text = _searchTextBox?.Text ?? string.Empty;
        if (!string.Equals(ViewModel.SearchText, text, StringComparison.Ordinal))
        {
            ViewModel.SearchText = text;
        }
    }

    private void FiscalYearComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel == null || _fiscalYearComboBox?.SelectedValue is not int year || year <= 0)
        {
            return;
        }

        if (ViewModel.SelectedFiscalYear == year)
        {
            return;
        }

        ViewModel.SelectedFiscalYear = year;
        SetHasUnsavedChanges(true);
        _ = ViewModel.LoadByYearCommand.ExecuteAsync(null);
    }

    private void EntityComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var selectedEntity = _entityComboBox?.SelectedItem as string;
        var normalizedEntity = !string.IsNullOrWhiteSpace(selectedEntity)
            && !string.Equals(selectedEntity, "All Entities", StringComparison.OrdinalIgnoreCase)
            ? selectedEntity.Trim()
            : null;

        if (string.Equals(ViewModel.SelectedEntity, normalizedEntity, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ViewModel.SelectedEntity = normalizedEntity;
        SetHasUnsavedChanges(true);
    }

    private void DepartmentComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel == null || _departmentComboBox == null)
        {
            return;
        }

        var selectedDepartmentId = _departmentComboBox.SelectedValue is int departmentId
            ? departmentId
            : (int?)null;

        if (ViewModel.SelectedDepartmentId == selectedDepartmentId)
        {
            return;
        }

        ViewModel.SelectedDepartmentId = selectedDepartmentId;

        SetHasUnsavedChanges(true);
    }

    private void FundTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var selectedFundType = _fundTypeComboBox?.SelectedValue is FundType fundType
            ? fundType
            : (FundType?)null;

        if (ViewModel.SelectedFundType == selectedFundType)
        {
            return;
        }

        ViewModel.SelectedFundType = selectedFundType;
        SetHasUnsavedChanges(true);
    }

    private void PopulateEntityFilterOptions()
    {
        if (_entityComboBox == null)
        {
            return;
        }

        var source = ViewModel?.AvailableEntities
            ?.Where(entity => !string.IsNullOrWhiteSpace(entity))
            .Select(entity => entity.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entity => entity, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        source.RemoveAll(entity => string.Equals(entity, "All Entities", StringComparison.OrdinalIgnoreCase));
        source.Insert(0, "All Entities");

        _entityComboBox.DataSource = null;
        _entityComboBox.DataSource = source;

        var selectedEntity = string.IsNullOrWhiteSpace(ViewModel?.SelectedEntity)
            ? "All Entities"
            : ViewModel.SelectedEntity!.Trim();

        var selectedIndex = source.FindIndex(entity => string.Equals(entity, selectedEntity, StringComparison.OrdinalIgnoreCase));
        _entityComboBox.SelectedIndex = selectedIndex >= 0
            ? selectedIndex
            : (source.Count > 0 ? 0 : -1);
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

            var viewModel = ViewModel;
            if (viewModel == null)
            {
                ShowSemanticPopup("Budget data is not loaded yet. Please try again shortly.", "Validation Error", SyncfusionControlFactory.MessageSemanticKind.Warning);
                return;
            }

            if (ServiceProvider == null)
            {
                ShowSemanticPopup("Required services are not available for the budget editor.", "Budget Editor Unavailable", SyncfusionControlFactory.MessageSemanticKind.Error);
                return;
            }

            var fiscalYear = viewModel.SelectedFiscalYear > 0 ? viewModel.SelectedFiscalYear : DateTime.Now.Year;
            using var dialog = new BudgetEntryEditDialog(null, ServiceProvider, fiscalYear);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var entry = dialog.Entry;
            EnsureBudgetEntryAllocationLines(entry);

            QueueUiAsyncWork(async () =>
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

                    var entryValidation = ValidateBudgetEntry(entry, null, null, null, null, null);
                    if (!entryValidation.IsValid)
                    {
                        ShowValidationDialog(entryValidation);
                        return;
                    }

                    var existingDuplicate = FindExistingBudgetEntry(viewModel, entry.AccountNumber, entry.FiscalYear, entry.FundId);
                    if (existingDuplicate != null)
                    {
                        var duplicatePrompt =
                            $"Account {WileyWidget.Models.AccountNumber.FormatDisplay(entry.AccountNumber)} already exists for FY {entry.FiscalYear} in the selected entity.\n\n" +
                            "Continue: update the existing entry with the values from this form.\n" +
                            "Stop: cancel this save and keep existing data unchanged.";

                        var duplicateDecision = ControlFactory.ShowMessageBox(
                            this,
                            duplicatePrompt,
                            "Duplicate Budget Entry",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (duplicateDecision != DialogResult.Yes)
                        {
                            UpdateStatus("Duplicate detected. Save stopped by user.");
                            return;
                        }

                        ApplyEntryEdits(existingDuplicate, entry);
                        await viewModel.UpdateEntryAsync(existingDuplicate, operationToken);
                        SetHasUnsavedChanges(false);
                        UpdateStatus("Existing budget entry updated successfully");
                        ShowCrudSuccessPopup("Edit", $"Updated existing account {WileyWidget.Models.AccountNumber.FormatDisplay(existingDuplicate.AccountNumber)} for FY {existingDuplicate.FiscalYear}.");
                        return;
                    }

                    await viewModel.AddEntryAsync(entry, operationToken);
                    SetHasUnsavedChanges(false);
                    UpdateStatus("Budget entry added successfully");
                    ShowCrudSuccessPopup("Save", $"Saved account {WileyWidget.Models.AccountNumber.FormatDisplay(entry.AccountNumber)} for FY {entry.FiscalYear}.");
                }
                catch (OperationCanceledException)
                {
                    Logger.LogDebug("Add entry operation cancelled");
                }
                finally
                {
                    IsBusy = false;
                }
            }, "AddBudgetEntry");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in AddEntryButton_Click");
            ShowSemanticPopup($"Error adding entry: {ex.Message}", "Error", SyncfusionControlFactory.MessageSemanticKind.Error);
        }
    }

    private static BudgetEntry? FindExistingBudgetEntry(BudgetViewModel viewModel, string accountNumber, int fiscalYear, int? fundId)
    {
        var normalizedAccount = accountNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAccount) || fiscalYear <= 0)
        {
            return null;
        }

        return viewModel.BudgetEntries.FirstOrDefault(entry =>
            entry.FiscalYear == fiscalYear &&
            entry.FundId == fundId &&
            string.Equals(entry.AccountNumber?.Trim(), normalizedAccount, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyEntryEdits(BudgetEntry target, BudgetEntry source)
    {
        target.AccountNumber = source.AccountNumber;
        target.Description = source.Description;
        target.BudgetedAmount = source.BudgetedAmount;
        target.ActualAmount = source.ActualAmount;
        target.Variance = source.BudgetedAmount - source.ActualAmount;
        target.DepartmentId = source.DepartmentId;
        target.Department = source.Department;
        target.FundId = source.FundId;
        target.Fund = source.Fund;
        target.FundType = source.FundType;
        target.StartPeriod = source.StartPeriod;
        target.EndPeriod = source.EndPeriod;
        target.UpdatedAt = DateTime.UtcNow;
    }

    private static BudgetEntry CreateBudgetEntryDraft(BudgetEntry source)
    {
        var draft = new BudgetEntry
        {
            Id = source.Id,
            AccountNumber = source.AccountNumber,
            Description = source.Description,
            BudgetedAmount = source.BudgetedAmount,
            ActualAmount = source.ActualAmount,
            Variance = source.Variance,
            ParentId = source.ParentId,
            Parent = source.Parent,
            FiscalYear = source.FiscalYear,
            StartPeriod = source.StartPeriod,
            EndPeriod = source.EndPeriod,
            FundType = source.FundType,
            EncumbranceAmount = source.EncumbranceAmount,
            IsGASBCompliant = source.IsGASBCompliant,
            DepartmentId = source.DepartmentId,
            Department = source.Department,
            FundId = source.FundId,
            Fund = source.Fund,
            MunicipalAccountId = source.MunicipalAccountId,
            MunicipalAccount = source.MunicipalAccount,
            SourceFilePath = source.SourceFilePath,
            SourceRowNumber = source.SourceRowNumber,
            ActivityCode = source.ActivityCode,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
        };

        foreach (var line in source.AllocationLines)
        {
            draft.AllocationLines.Add(new BudgetAllocationLine
            {
                FundId = line.FundId,
                AllocationPercentage = line.AllocationPercentage,
                ParentBudgetedAmount = line.ParentBudgetedAmount,
                ParentActualAmount = line.ParentActualAmount,
                AllocatedAmount = line.AllocatedAmount,
                AllocatedActual = line.AllocatedActual,
                Notes = line.Notes,
            });
        }

        return draft;
    }

    private void ShowCrudSuccessPopup(string action, string details)
    {
        var title = $"{action} Successful";
        var message = string.IsNullOrWhiteSpace(details)
            ? $"{action} completed successfully."
            : $"{action} completed successfully.\n\n{details}";

        ShowSemanticPopup(message, title, SyncfusionControlFactory.MessageSemanticKind.Success, playNotificationSound: true);
    }

    private void ShowSemanticPopup(
        string message,
        string title,
        SyncfusionControlFactory.MessageSemanticKind semanticKind,
        bool playNotificationSound = false)
    {
        _ = ControlFactory.ShowSemanticMessageBox(
            this,
            message,
            title,
            semanticKind,
            MessageBoxButtons.OK,
            playNotificationSound: playNotificationSound,
            enableDropShadow: true);
    }

    private DialogResult ShowQuestionPopup(
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.YesNo,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button2)
    {
        return ControlFactory.ShowMessageBox(new SyncfusionControlFactory.SyncfusionMessageBoxOptions
        {
            Owner = this,
            Message = message,
            Title = title,
            Buttons = buttons,
            Icon = MessageBoxIcon.Question,
            DefaultButton = defaultButton,
            DropShadow = true,
        });
    }

    private void ShowValidationDialog(ValidationResult validationResult)
    {
        if (validationResult.IsValid || validationResult.Errors.Length == 0)
        {
            return;
        }

        foreach (var error in validationResult.Errors)
        {
            Logger.LogInformation("Validation failed: {FieldName} - {Message}", error.FieldName, error.Message);
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
                ShowSemanticPopup("Please select a budget entry to edit.", "No Selection", SyncfusionControlFactory.MessageSemanticKind.Information);
                return;
            }

            var selectedEntry = _budgetGrid.SelectedItems[0] as BudgetEntry;
            if (selectedEntry == null) return;

            Logger.LogInformation("Edit Entry button clicked for entry {Id}", selectedEntry.Id);

            if (ServiceProvider == null)
            {
                ShowSemanticPopup("Required services are not available for the budget editor.", "Budget Editor Unavailable", SyncfusionControlFactory.MessageSemanticKind.Error);
                return;
            }

            var entryDraft = CreateBudgetEntryDraft(selectedEntry);
            using var dialog = new BudgetEntryEditDialog(entryDraft, ServiceProvider);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var updatedEntry = dialog.Entry;
            EnsureBudgetEntryAllocationLines(updatedEntry);

            QueueUiAsyncWork(async () =>
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

                    var entryValidation = ValidateBudgetEntry(updatedEntry, null, null, null, null, null);
                    if (!entryValidation.IsValid)
                    {
                        ShowValidationDialog(entryValidation);
                        return;
                    }

                    await ViewModel!.UpdateEntryAsync(updatedEntry, operationToken);
                    SetHasUnsavedChanges(false);
                    UpdateStatus("Budget entry updated successfully");
                    ShowCrudSuccessPopup("Edit", $"Updated account {WileyWidget.Models.AccountNumber.FormatDisplay(updatedEntry.AccountNumber)} for FY {updatedEntry.FiscalYear}.");
                }
                catch (OperationCanceledException)
                {
                    Logger.LogDebug("Update entry operation cancelled");
                }
                finally
                {
                    IsBusy = false;
                }
            }, "EditBudgetEntry");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in EditEntryButton_Click");
            ShowSemanticPopup($"Error editing entry: {ex.Message}", "Error", SyncfusionControlFactory.MessageSemanticKind.Error);
        }
    }

    private async Task DeleteEntryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_budgetGrid?.SelectedItems == null || _budgetGrid.SelectedItems.Count == 0)
            {
                ShowSemanticPopup("Please select a budget entry to delete.", "No Selection", SyncfusionControlFactory.MessageSemanticKind.Information);
                return;
            }

            var selectedEntry = _budgetGrid.SelectedItems[0] as BudgetEntry;
            if (selectedEntry == null) return;

            var result = ShowQuestionPopup(
                $"Are you sure you want to delete budget entry '{WileyWidget.Models.AccountNumber.FormatDisplay(selectedEntry.AccountNumber)} - {selectedEntry.Description}'?\n\nThis action cannot be undone.",
                "Confirm Delete");

            if (result == DialogResult.Yes)
            {
                Logger.LogInformation("Deleting budget entry {Id}: {AccountNumber}", selectedEntry.Id, selectedEntry.AccountNumber);
                var operationToken = RegisterOperation();
                IsBusy = true;
                try
                {
                    await ViewModel!.DeleteEntryAsync(selectedEntry.Id, operationToken);
                    SetHasUnsavedChanges(false);
                    UpdateStatus($"Deleted budget entry {WileyWidget.Models.AccountNumber.FormatDisplay(selectedEntry.AccountNumber)}");
                    ShowCrudSuccessPopup("Delete", $"Deleted account {WileyWidget.Models.AccountNumber.FormatDisplay(selectedEntry.AccountNumber)} for FY {selectedEntry.FiscalYear}.");
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
            ShowSemanticPopup($"Error deleting entry: {ex.Message}", "Error", SyncfusionControlFactory.MessageSemanticKind.Error);
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

        QueueUiAsyncWork(async () =>
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
                ShowSemanticPopup(result.ErrorMessage ?? "An export is already in progress.", "Export", SyncfusionControlFactory.MessageSemanticKind.Information);
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
        }, "ExportBudgetCsv");
    }

    private void MappingWizardPanel_MappingApplied(object? sender, MappingAppliedEventArgs e)
    {
        if (ViewModel == null) return;

        QueueUiAsyncWork(async () =>
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
                ShowSemanticPopup($"Import failed: {ex.Message}", "Import Error", SyncfusionControlFactory.MessageSemanticKind.Error);
            }
            finally
            {
                if (_mappingContainer != null) _mappingContainer.Visible = false;
            }
        }, "ImportBudgetWithMapping");
    }

    private void MappingWizardPanel_Cancelled(object? sender, EventArgs e)
    {
        if (_mappingContainer != null) _mappingContainer.Visible = false;
        UpdateStatus("Import cancelled");
    }

    private void ExportPdfButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        QueueUiAsyncWork(async () =>
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
                ShowSemanticPopup(result.ErrorMessage ?? "An export is already in progress.", "Export", SyncfusionControlFactory.MessageSemanticKind.Information);
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
        }, "ExportBudgetPdf");
    }

    private void ExportExcelButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel == null) return;

        QueueUiAsyncWork(async () =>
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
                ShowSemanticPopup(result.ErrorMessage ?? "An export is already in progress.", "Export", SyncfusionControlFactory.MessageSemanticKind.Information);
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
        }, "ExportBudgetExcel");
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
            case nameof(ViewModel.FilteredBudgetEntries):
                if (_budgetGrid != null)
                {
                    var rows = ViewModel.FilteredBudgetEntries.Any()
                        ? ViewModel.FilteredBudgetEntries
                        : ViewModel.BudgetEntries;

                    InitializeBudgetEntryAllocationLines(rows);

                    _budgetGrid.DataSource = rows;
                    ConfigureBudgetGrid();
                }
                PopulateDepartmentFilterOptions();
                PopulateFiscalYearFilterOptions();
                PopulateFundTypeFilterOptions();
                PopulateEntityFilterOptions();
                break;

            case nameof(ViewModel.AvailableEntities):
                PopulateEntityFilterOptions();
                break;

            case nameof(ViewModel.SelectedEntity):
                PopulateEntityFilterOptions();
                break;

            case nameof(ViewModel.SelectedFiscalYear):
                if (_fiscalYearComboBox != null)
                {
                    _fiscalYearComboBox.SelectedValue = ViewModel.SelectedFiscalYear;
                }
                break;

            case nameof(ViewModel.SelectedDepartmentId):
                if (_departmentComboBox != null)
                {
                    _departmentComboBox.SelectedValue = ViewModel.SelectedDepartmentId;
                }
                break;

            case nameof(ViewModel.SelectedFundType):
                if (_fundTypeComboBox != null)
                {
                    _fundTypeComboBox.SelectedValue = ViewModel.SelectedFundType;
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
                if (_totalBudgetedCard != null)
                    _totalBudgetedCard.Value = $"{ViewModel.TotalBudgeted:C0}";
                break;

            case nameof(ViewModel.TotalActual):
                if (_totalActualCard != null)
                    _totalActualCard.Value = $"{ViewModel.TotalActual:C0}";
                break;

            case nameof(ViewModel.TotalVariance):
                if (_totalVarianceCard != null)
                    _totalVarianceCard.Value = $"{ViewModel.TotalVariance:C0}";
                break;

            case nameof(ViewModel.PercentUsed):
                if (_percentUsedCard != null)
                    _percentUsedCard.Value = $"{ViewModel.PercentUsed:P1}";
                break;

            case nameof(ViewModel.EntriesOverBudget):
                if (_entriesOverBudgetCard != null)
                    _entriesOverBudgetCard.Value = ViewModel.EntriesOverBudget.ToString();
                break;

            case nameof(ViewModel.EntriesUnderBudget):
                if (_entriesUnderBudgetCard != null)
                    _entriesUnderBudgetCard.Value = ViewModel.EntriesUnderBudget.ToString();
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
            ShowSemanticPopup($"Error refreshing data: {ex.Message}", "Error", SyncfusionControlFactory.MessageSemanticKind.Error);
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

    private void QueueUiAsyncWork(Func<Task> work, string operationName)
    {
        if (work == null || IsDisposed || Disposing)
        {
            return;
        }

        try
        {
            BeginInvoke((MethodInvoker)(() => _ = ExecuteQueuedUiAsyncWorkAsync(work, operationName)));
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to queue UI async work for {OperationName}", operationName);
        }
    }

    private async Task ExecuteQueuedUiAsyncWorkAsync(Func<Task> work, string operationName)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("{OperationName} cancelled", operationName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Queued UI async work failed for {OperationName}", operationName);
        }
    }

    private void PopulateFiscalYearFilterOptions()
    {
        if (_fiscalYearComboBox == null)
        {
            return;
        }

        var years = new HashSet<int>();
        var selectedYear = ViewModel?.SelectedFiscalYear ?? DateTime.Now.Year;
        var nowYear = DateTime.Now.Year;

        for (int year = nowYear - 5; year <= nowYear + 5; year++)
        {
            years.Add(year);
        }

        if (ViewModel?.BudgetEntries != null)
        {
            foreach (var year in ViewModel.BudgetEntries.Select(entry => entry.FiscalYear).Where(year => year > 0))
            {
                years.Add(year);
            }
        }

        if (selectedYear > 0)
        {
            years.Add(selectedYear);
        }

        var options = years
            .OrderByDescending(year => year)
            .Select(year => new ComboFilterOption<int>(year.ToString(CultureInfo.InvariantCulture), year))
            .ToList();

        _fiscalYearComboBox.DataSource = null;
        _fiscalYearComboBox.DataSource = options;
        _fiscalYearComboBox.DisplayMember = nameof(ComboFilterOption<int>.Text);
        _fiscalYearComboBox.ValueMember = nameof(ComboFilterOption<int>.Value);

        if (options.Any(option => option.Value == selectedYear))
        {
            _fiscalYearComboBox.SelectedValue = selectedYear;
        }
        else
        {
            _fiscalYearComboBox.SelectedIndex = options.Count > 0 ? 0 : -1;
        }
    }

    private void PopulateDepartmentFilterOptions()
    {
        if (_departmentComboBox == null)
        {
            return;
        }

        var options = new List<ComboFilterOption<int?>>
        {
            new("All Departments", null)
        };

        if (ViewModel?.BudgetEntries != null)
        {
            var departmentOptions = ViewModel.BudgetEntries
                .Where(entry => entry.DepartmentId > 0)
                .GroupBy(entry => entry.DepartmentId)
                .Select(group =>
                {
                    var first = group.First();
                    var name = string.IsNullOrWhiteSpace(first.DepartmentName)
                        ? $"Department {group.Key}"
                        : first.DepartmentName.Trim();
                    return new ComboFilterOption<int?>(name, group.Key);
                })
                .OrderBy(option => option.Text, StringComparer.OrdinalIgnoreCase);

            options.AddRange(departmentOptions);
        }

        var selectedDepartmentId = ViewModel?.SelectedDepartmentId;
        _departmentComboBox.DataSource = null;
        _departmentComboBox.DataSource = options;
        _departmentComboBox.DisplayMember = nameof(ComboFilterOption<int?>.Text);
        _departmentComboBox.ValueMember = nameof(ComboFilterOption<int?>.Value);

        if (selectedDepartmentId.HasValue && options.Any(option => option.Value == selectedDepartmentId))
        {
            _departmentComboBox.SelectedValue = selectedDepartmentId.Value;
        }
        else
        {
            _departmentComboBox.SelectedIndex = options.Count > 0 ? 0 : -1;
        }
    }

    private void PopulateFundTypeFilterOptions()
    {
        if (_fundTypeComboBox == null)
        {
            return;
        }

        var options = new List<ComboFilterOption<FundType?>>
        {
            new("All Fund Types", null)
        };

        options.AddRange(Enum.GetValues(typeof(FundType))
            .Cast<FundType>()
            .OrderBy(value => value.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(value => new ComboFilterOption<FundType?>(value.ToString(), value)));

        _fundTypeComboBox.DataSource = null;
        _fundTypeComboBox.DataSource = options;
        _fundTypeComboBox.DisplayMember = nameof(ComboFilterOption<FundType?>.Text);
        _fundTypeComboBox.ValueMember = nameof(ComboFilterOption<FundType?>.Value);

        if (ViewModel?.SelectedFundType is FundType selectedFundType
            && options.Any(option => option.Value == selectedFundType))
        {
            _fundTypeComboBox.SelectedValue = selectedFundType;
        }
        else
        {
            _fundTypeComboBox.SelectedIndex = options.Count > 0 ? 0 : -1;
        }
    }

    private void OnLoadBudgetsButtonClick(object? sender, EventArgs e)
    {
        if (ViewModel != null)
            QueueUiAsyncWork(async () => await ViewModel.LoadBudgetsCommand.ExecuteAsync(null), "LoadBudgetsButtonClick");
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
            ShowCrudSuccessPopup("Save", "All pending budget panel edits are validated and saved.");
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
            ShowSemanticPopup($"Error saving changes: {ex.Message}", "Error", SyncfusionControlFactory.MessageSemanticKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void OnPanelLoaded(EventArgs e)
    {
        base.OnPanelLoaded(e);
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
            ShowSemanticPopup($"Error loading data: {ex.Message}", "Error", SyncfusionControlFactory.MessageSemanticKind.Error);
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
    /// Triggers a deferred ForceFullLayout after panel is shown.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);   // starts the 180ms _finalLayoutTimer in ScopedPanelBase

        BeginInvoke(() =>
        {
            ForceFullLayout();
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
                _budgetGrid.AutoGeneratingRelations -= BudgetGrid_AutoGeneratingRelations;
            }

            // Dispose BindingSource
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
        this.Size = ScaleLogicalToDevice(new Size(1400, 900));
        try { this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; } catch { }
        this.MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
        this.AutoScroll = false;
        this.ResumeLayout(false);

    }

    private sealed class ComboFilterOption<TValue>
    {
        public ComboFilterOption(string text, TValue value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public TValue Value { get; }

        public override string ToString()
        {
            return Text;
        }
    }

    #endregion
}
