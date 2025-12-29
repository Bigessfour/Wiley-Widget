using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Panel for budget management with full CRUD operations, filtering, analysis, and export capabilities.
/// Features budget entry management, variance analysis, fiscal year management, and reporting.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class BudgetPanel : UserControl
{
    private readonly BudgetViewModel _viewModel;
    private readonly ILogger<BudgetPanel> _logger;

    // UI Controls
    private SfDataGrid? _budgetGrid;
    private Button? _loadBudgetsButton;
    private Button? _addEntryButton;
    private Button? _editEntryButton;
    private Button? _deleteEntryButton;
    private Button? _importCsvButton;
    private Button? _exportCsvButton;
    private Button? _exportPdfButton;
    private Button? _exportExcelButton;
    private TextBox? _searchTextBox;
    private ComboBox? _fiscalYearComboBox;
    private ComboBox? _departmentComboBox;
    private ComboBox? _fundTypeComboBox;
    private TextBox? _varianceThresholdTextBox;
    private CheckBox? _overBudgetCheckBox;
    private CheckBox? _underBudgetCheckBox;
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
    private SplitContainer? _mainSplitContainer;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private PanelHeader? _panelHeader;
    private LoadingOverlay? _loadingOverlay;
    private NoDataOverlay? _noDataOverlay;

    public BudgetPanel(
        BudgetViewModel viewModel,
        ILogger<BudgetPanel> logger)
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
        Text = "Budget Management";
        Size = new Size(1400, 900);
        MinimumSize = new Size(1000, 600);

        // Panel header with actions
        _panelHeader = new PanelHeader { Dock = DockStyle.Top, Title = "Budget Management & Analysis" };
        _panelHeader.RefreshClicked += async (s, e) => await RefreshDataAsync();
        _panelHeader.CloseClicked += (s, e) => ClosePanel();
        Controls.Add(_panelHeader);

        // Main split container
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 150
        };

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
        _noDataOverlay = new NoDataOverlay { Message = "No budget entries found" };
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
        _filterPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var filterGroup = new GroupBox
        {
            Text = "Filters",
            Dock = DockStyle.Fill
        };

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

        _searchTextBox = new TextBox
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

        _fiscalYearComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 2,
            AccessibleName = "Fiscal Year Filter",
            AccessibleDescription = "Filter budget entries by fiscal year"
        };

        // Populate fiscal years
        for (int year = DateTime.Now.Year - 5; year <= DateTime.Now.Year + 5; year++)
        {
            _fiscalYearComboBox.Items.Add(year);
        }
        _fiscalYearComboBox.SelectedItem = DateTime.Now.Year;
        _fiscalYearComboBox.SelectedIndexChanged += FiscalYearComboBox_SelectedIndexChanged;

        // Row 2: Department and Fund Type
        var departmentLabel = new Label
        {
            Text = "Department:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _departmentComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 3,
            AccessibleName = "Department Filter",
            AccessibleDescription = "Filter budget entries by department"
        };
        _departmentComboBox.Items.Add("All Departments");
        _departmentComboBox.SelectedIndex = 0;
        _departmentComboBox.SelectedIndexChanged += DepartmentComboBox_SelectedIndexChanged;

        var fundTypeLabel = new Label
        {
            Text = "Fund Type:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _fundTypeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            TabIndex = 4,
            AccessibleName = "Fund Type Filter",
            AccessibleDescription = "Filter budget entries by fund type"
        };
        _fundTypeComboBox.Items.AddRange(Enum.GetNames(typeof(FundType)));
        _fundTypeComboBox.Items.Insert(0, "All Fund Types");
        _fundTypeComboBox.SelectedIndex = 0;
        _fundTypeComboBox.SelectedIndexChanged += FundTypeComboBox_SelectedIndexChanged;

        // Row 3: Variance threshold and checkboxes
        var varianceLabel = new Label
        {
            Text = "Variance >:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        _varianceThresholdTextBox = new TextBox
        {
            Text = "1000",
            Dock = DockStyle.Fill,
            TabIndex = 5,
            AccessibleName = "Variance Threshold",
            AccessibleDescription = "Filter entries with variance greater than this amount"
        };
        _varianceThresholdTextBox.TextChanged += VarianceThresholdTextBox_TextChanged;

        _overBudgetCheckBox = new CheckBox
        {
            Text = "Over Budget Only",
            Dock = DockStyle.Fill,
            TabIndex = 6,
            AccessibleName = "Show Over Budget Only",
            AccessibleDescription = "Show only entries that are over budget"
        };
        _overBudgetCheckBox.CheckedChanged += OverBudgetCheckBox_CheckedChanged;

        _underBudgetCheckBox = new CheckBox
        {
            Text = "Under Budget Only",
            Dock = DockStyle.Fill,
            TabIndex = 7,
            AccessibleName = "Show Under Budget Only",
            AccessibleDescription = "Show only entries that are under budget"
        };
        _underBudgetCheckBox.CheckedChanged += UnderBudgetCheckBox_CheckedChanged;

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
#pragma warning disable CS8602
        _filterPanel!.Controls.Add(filterGroup);
#pragma warning restore CS8602
        topPanel.Controls.Add(_filterPanel);

        _mainSplitContainer.Panel1.Controls.Add(topPanel);
    }

    private void InitializeBottomPanel()
    {
        var bottomPanel = new Panel { Dock = DockStyle.Fill };

        // Budget grid
        _gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

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

        _budgetGrid.Columns.Add(new GridTextColumn
        {
            MappingName = "FundTypeDescription",
            HeaderText = "Fund Type",
            AllowEditing = false
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "BudgetedAmount",
            HeaderText = "Budgeted",
            Format = "C2",
            AllowEditing = true
        });

        _budgetGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = "ActualAmount",
            HeaderText = "Actual",
            Format = "C2",
            AllowEditing = true
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

        _budgetGrid.CurrentCellActivated += BudgetGrid_CurrentCellActivated;
        _gridPanel.Controls.Add(_budgetGrid);
        bottomPanel.Controls.Add(_gridPanel);

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
            ColumnCount = 8,
            RowCount = 1
        };

        for (int i = 0; i < 8; i++)
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));

        _loadBudgetsButton = new Button
        {
            Text = "&Load Budgets",
            TabIndex = 9,
            AccessibleName = "Load Budgets",
            AccessibleDescription = "Load budget entries for the selected fiscal year"
        };
        _loadBudgetsButton.Click += async (s, e) => await _viewModel.LoadBudgetsCommand.ExecuteAsync(null);

        _addEntryButton = new Button
        {
            Text = "&Add Entry",
            TabIndex = 10,
            AccessibleName = "Add Entry",
            AccessibleDescription = "Add a new budget entry"
        };
        _addEntryButton.Click += AddEntryButton_Click;

        _editEntryButton = new Button
        {
            Text = "&Edit Entry",
            TabIndex = 11,
            AccessibleName = "Edit Entry",
            AccessibleDescription = "Edit the selected budget entry"
        };
        _editEntryButton.Click += EditEntryButton_Click;

        _deleteEntryButton = new Button
        {
            Text = "&Delete Entry",
            TabIndex = 12,
            AccessibleName = "Delete Entry",
            AccessibleDescription = "Delete the selected budget entry"
        };
        _deleteEntryButton.Click += async (s, e) => await DeleteEntryAsync();

        _importCsvButton = new Button
        {
            Text = "&Import CSV",
            TabIndex = 13,
            AccessibleName = "Import CSV",
            AccessibleDescription = "Import budget entries from CSV file"
        };
        _importCsvButton.Click += ImportCsvButton_Click;

        _exportCsvButton = new Button
        {
            Text = "Export &CSV",
            TabIndex = 14,
            AccessibleName = "Export CSV",
            AccessibleDescription = "Export budget entries to CSV file"
        };
        _exportCsvButton.Click += ExportCsvButton_Click;

        _exportPdfButton = new Button
        {
            Text = "Export &PDF",
            TabIndex = 15,
            AccessibleName = "Export PDF",
            AccessibleDescription = "Export budget entries to PDF file"
        };
        _exportPdfButton.Click += ExportPdfButton_Click;

        _exportExcelButton = new Button
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

#pragma warning disable CS8602
        _buttonPanel!.Controls.Add(buttonTable);
#pragma warning restore CS8602
        bottomPanel.Controls.Add(_buttonPanel);

        _mainSplitContainer.Panel2.Controls.Add(bottomPanel);
    }

    private void SetTabOrder()
    {
        // Tab order set in control initialization
    }

    private void BudgetGrid_CurrentCellActivated(object? sender, EventArgs e)
    {
        // Handle grid selection if needed
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        _viewModel.SearchText = _searchTextBox?.Text ?? string.Empty;
    }

    private void FiscalYearComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_fiscalYearComboBox?.SelectedItem is int year)
        {
            _viewModel.SelectedFiscalYear = year;
        }
    }

    private void DepartmentComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Department filtering logic would go here
    }

    private void FundTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_fundTypeComboBox?.SelectedItem is string fundTypeString &&
            Enum.TryParse<FundType>(fundTypeString, out var fundType))
        {
            _viewModel.SelectedFundType = fundType;
        }
        else
        {
            _viewModel.SelectedFundType = null;
        }
    }

    private void VarianceThresholdTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (decimal.TryParse(_varianceThresholdTextBox?.Text, out var threshold))
        {
            _viewModel.VarianceThreshold = threshold;
        }
        else
        {
            _viewModel.VarianceThreshold = null;
        }
    }

    private void OverBudgetCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _viewModel.ShowOnlyOverBudget = _overBudgetCheckBox?.Checked ?? false;
    }

    private void UnderBudgetCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _viewModel.ShowOnlyUnderBudget = _underBudgetCheckBox?.Checked ?? false;
    }

    private void AddEntryButton_Click(object? sender, EventArgs e)
    {
        // Add entry dialog would be implemented here
        MessageBox.Show("Add Entry functionality not yet implemented", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void EditEntryButton_Click(object? sender, EventArgs e)
    {
        // Edit entry dialog would be implemented here
        MessageBox.Show("Edit Entry functionality not yet implemented", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private Task DeleteEntryAsync()
    {
        if (_viewModel.BudgetEntries.Count == 0)
        {
            MessageBox.Show("No budget entries to delete", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        var result = MessageBox.Show(
            "Are you sure you want to delete the selected budget entry?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            // Delete logic would go here
            MessageBox.Show("Delete functionality not yet implemented", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        return Task.CompletedTask;
    }

    private void ImportCsvButton_Click(object? sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Import Budget Entries from CSV"
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            _ = _viewModel.ImportFromCsvCommand.ExecuteAsync(openFileDialog.FileName);
        }
    }

    private void ExportCsvButton_Click(object? sender, EventArgs e)
    {
        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Export Budget Entries to CSV",
            FileName = $"Budget_Entries_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            _ = _viewModel.ExportToCsvCommand.ExecuteAsync(saveFileDialog.FileName);
        }
    }

    private void ExportPdfButton_Click(object? sender, EventArgs e)
    {
        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            Title = "Export Budget Entries to PDF",
            FileName = $"Budget_Report_{DateTime.Now:yyyyMMdd}.pdf"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            _ = _viewModel.ExportToPdfCommand.ExecuteAsync(saveFileDialog.FileName);
        }
    }

    private void ExportExcelButton_Click(object? sender, EventArgs e)
    {
        using var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            Title = "Export Budget Entries to Excel",
            FileName = $"Budget_Entries_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            _ = _viewModel.ExportToExcelCommand.ExecuteAsync(saveFileDialog.FileName);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.BudgetEntries):
                if (_budgetGrid != null) _budgetGrid.DataSource = _viewModel.BudgetEntries;
                break;

            case nameof(_viewModel.FilteredBudgetEntries):
                if (_budgetGrid != null) _budgetGrid.DataSource = _viewModel.FilteredBudgetEntries;
                break;

            case nameof(_viewModel.IsLoading):
                if (_loadingOverlay != null) _loadingOverlay.Visible = _viewModel.IsLoading;
                if (_noDataOverlay != null) _noDataOverlay.Visible = !_viewModel.IsLoading && !_viewModel.BudgetEntries.Any();
                break;

            case nameof(_viewModel.StatusText):
                if (_statusLabel != null) _statusLabel.Text = _viewModel.StatusText;
                break;

            case nameof(_viewModel.TotalBudgeted):
                if (_totalBudgetedLabel != null)
                    _totalBudgetedLabel.Text = $"Total Budgeted: {_viewModel.TotalBudgeted:C}";
                break;

            case nameof(_viewModel.TotalActual):
                if (_totalActualLabel != null)
                    _totalActualLabel.Text = $"Total Actual: {_viewModel.TotalActual:C}";
                break;

            case nameof(_viewModel.TotalVariance):
                if (_totalVarianceLabel != null)
                    _totalVarianceLabel.Text = $"Total Variance: {_viewModel.TotalVariance:C}";
                break;

            case nameof(_viewModel.PercentUsed):
                if (_percentUsedLabel != null)
                    _percentUsedLabel.Text = $"Percent Used: {_viewModel.PercentUsed:P}";
                break;

            case nameof(_viewModel.EntriesOverBudget):
                if (_entriesOverBudgetLabel != null)
                    _entriesOverBudgetLabel.Text = $"Over Budget: {_viewModel.EntriesOverBudget}";
                break;

            case nameof(_viewModel.EntriesUnderBudget):
                if (_entriesUnderBudgetLabel != null)
                    _entriesUnderBudgetLabel.Text = $"Under Budget: {_viewModel.EntriesUnderBudget}";
                break;
        }
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing budget data");
            UpdateStatus("Loading budget data...");

            await _viewModel.LoadBudgetsCommand.ExecuteAsync(null);
            await _viewModel.RefreshAnalysisCommand.ExecuteAsync(null);

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
            _logger.LogWarning(ex, "BudgetPanel: ClosePanel failed");
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

            _budgetGrid?.Dispose();
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
        this.Name = "BudgetPanel";
        this.Size = new Size(1400, 900);
    }

    #endregion
}
