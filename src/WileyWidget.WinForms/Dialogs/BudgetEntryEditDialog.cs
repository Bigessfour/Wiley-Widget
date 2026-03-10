using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utilities;

namespace WileyWidget.WinForms.Dialogs;

public partial class BudgetEntryEditDialog : SfForm
{
    public BudgetEntry Entry { get; private set; }

    public bool IsNew { get; private set; }

    private readonly IServiceProvider _serviceProvider;
    private readonly List<Department> _departments = new();
    private readonly List<Fund> _funds = new();
    private readonly ToolTip _tooltip = new() { AutoPopDelay = 5000, InitialDelay = 500 };

    private SfButton btnOK = null!;
    private SfButton btnCancel = null!;
    private TextBox txtAccountNumber = null!;
    private TextBox txtDescription = null!;
    private TextBox txtAccountType = null!;
    private TextBox txtFiscalYear = null!;
    private TextBox txtBudgetedAmount = null!;
    private TextBox txtActualAmount = null!;
    private ComboBox cmbDepartment = null!;
    private ComboBox cmbFund = null!;
    private ComboBox cmbFundType = null!;
    private TableLayoutPanel editorTable = null!;
    private FlowLayoutPanel buttonPanel = null!;

    public BudgetEntryEditDialog(BudgetEntry? entryToEdit, IServiceProvider serviceProvider, int? fiscalYear = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        IsNew = entryToEdit == null;
        Entry = entryToEdit ?? new BudgetEntry
        {
            FiscalYear = fiscalYear ?? DateTime.Now.Year,
            CreatedAt = DateTime.UtcNow,
            IsGASBCompliant = true,
        };

        InitializeComponent();
        Text = IsNew ? "Add Budget Entry" : "Edit Budget Entry";
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        Size = LayoutTokens.GetScaled(new Size(860, 660));
        MinimumSize = LayoutTokens.GetScaled(new Size(760, 620));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        KeyDown += HandleKeyDown;

        Style.Border = new Pen(SystemColors.WindowFrame, 1);
        Style.InactiveBorder = new Pen(SystemColors.GrayText, 1);

        WileyWidget.WinForms.Themes.ThemeColors.ApplyTheme(this);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = LayoutTokens.GetScaled(LayoutTokens.DialogShellPadding),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        rootLayout.Controls.Add(CreateHeaderLayout(), 0, 0);

        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = new Padding(0),
        };

        editorTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = LayoutTokens.GetScaled(LayoutTokens.DialogContentPadding),
        };
        editorTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LayoutTokens.GetScaled(180)));
        editorTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        BuildEditorRows();
        contentHost.Controls.Add(editorTable);
        rootLayout.Controls.Add(contentHost, 0, 1);

        buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, LayoutTokens.GetScaled(14), 0, 0),
            Margin = new Padding(0),
        };

        var buttonSize = LayoutTokens.GetScaled(LayoutTokens.DefaultButtonSize);
        btnOK = new SfButton
        {
            Text = IsNew ? "Save Entry" : "Save Changes",
            Size = buttonSize,
        };
        btnOK.Click += btnOK_Click;

        btnCancel = new SfButton
        {
            Text = "Cancel",
            Size = buttonSize,
            Margin = new Padding(0, 0, LayoutTokens.GetScaled(10), 0),
        };
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnOK);
        rootLayout.Controls.Add(buttonPanel, 0, 2);

        Controls.Add(rootLayout);

        AcceptButton = btnOK;
        CancelButton = btnCancel;
        Load += BudgetEntryEditDialog_Load;

        ResumeLayout(performLayout: true);
    }

    private TableLayoutPanel CreateHeaderLayout()
    {
        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, LayoutTokens.GetScaled(12)),
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = IsNew ? "Create budget entry" : "Update budget entry",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Margin = new Padding(0),
        };
        headerLayout.Controls.Add(titleLabel, 0, 0);

        var subtitleLabel = new Label
        {
            AutoSize = true,
            Text = "Capture the account, classification, and financial amounts in one place.",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Margin = new Padding(0, LayoutTokens.GetScaled(4), 0, 0),
        };
        headerLayout.Controls.Add(subtitleLabel, 0, 1);

        return headerLayout;
    }

    private void BuildEditorRows()
    {
        var row = 0;

        AddSectionHeader("Account details", row++, "Use the official account number and display description used in reports.");

        txtAccountNumber = CreateTextBox();
        AddControlRow("Account Number", txtAccountNumber, row++);
        AddTooltip(txtAccountNumber, "Unique account identifier such as 101-001.");

        txtDescription = CreateTextBox();
        AddControlRow("Description", txtDescription, row++);
        AddTooltip(txtDescription, "Human-readable name shown across the budget grid and reports.");

        txtAccountType = CreateReadOnlyTextBox();
        AddControlRow("Account Type", txtAccountType, row++);
        AddTooltip(txtAccountType, "Pulled from the chart of accounts after you enter an account number.");

        txtFiscalYear = CreateReadOnlyTextBox();
        AddControlRow("Fiscal Year", txtFiscalYear, row++);
        AddTooltip(txtFiscalYear, "The dialog saves this entry into the currently selected fiscal year.");

        AddSectionHeader("Classification", row++, "Assign the operating department, fund, and fund type.");

        cmbDepartment = CreateLookupComboBox();
        cmbDepartment.DisplayMember = "Name";
        cmbDepartment.ValueMember = "Id";
        AddControlRow("Department", cmbDepartment, row++);
        AddTooltip(cmbDepartment, "Select the department responsible for the budget line.");

        cmbFund = CreateLookupComboBox();
        cmbFund.DisplayMember = "Name";
        cmbFund.ValueMember = "Id";
        AddControlRow("Fund", cmbFund, row++);
        AddTooltip(cmbFund, "Choose the fund where this budget line belongs.");

        cmbFundType = CreateDropDownListComboBox();
        cmbFundType.Items.AddRange(Enum.GetNames(typeof(FundType)));
        AddControlRow("Fund Type", cmbFundType, row++);
        AddTooltip(cmbFundType, "Categorizes the entry for financial reporting.");

        AddSectionHeader("Financial amounts", row++, "Enter approved budget and actual spend values for this line.");

        txtBudgetedAmount = CreateAmountTextBox();
        AddControlRow("Budgeted Amount", txtBudgetedAmount, row++);
        AddTooltip(txtBudgetedAmount, "Approved amount for the selected fiscal year.");

        txtActualAmount = CreateAmountTextBox();
        AddControlRow("Actual Amount", txtActualAmount, row++);
        AddTooltip(txtActualAmount, "Actual amount posted so far.");
    }

    private static TextBox CreateTextBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, LayoutTokens.GetScaled(4), 0, LayoutTokens.GetScaled(8)),
            MinimumSize = LayoutTokens.GetScaled(new Size(240, 32)),
        };
    }

    private static TextBox CreateReadOnlyTextBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            TabStop = false,
            Margin = new Padding(0, LayoutTokens.GetScaled(4), 0, LayoutTokens.GetScaled(8)),
            MinimumSize = LayoutTokens.GetScaled(new Size(240, 32)),
        };
    }

    private static TextBox CreateAmountTextBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, LayoutTokens.GetScaled(4), 0, LayoutTokens.GetScaled(8)),
            MinimumSize = LayoutTokens.GetScaled(new Size(160, 32)),
            Text = "0.00",
            TextAlign = HorizontalAlignment.Right,
        };
    }

    private static ComboBox CreateLookupComboBox()
    {
        return new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            Margin = new Padding(0, LayoutTokens.GetScaled(4), 0, LayoutTokens.GetScaled(8)),
            MinimumSize = LayoutTokens.GetScaled(new Size(240, 32)),
        };
    }

    private static ComboBox CreateDropDownListComboBox()
    {
        return new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, LayoutTokens.GetScaled(4), 0, LayoutTokens.GetScaled(8)),
            MinimumSize = LayoutTokens.GetScaled(new Size(240, 32)),
        };
    }

    private void AddTooltip(Control control, string text)
    {
        _tooltip.SetToolTip(control, text);
    }

    private void AddSectionHeader(string title, int row, string? description = null)
    {
        editorTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var sectionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = description == null ? 1 : 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, row == 0 ? 0 : LayoutTokens.GetScaled(12), 0, LayoutTokens.GetScaled(4)),
        };
        sectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        sectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (description != null)
        {
            sectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        sectionLayout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(0),
        }, 0, 0);

        if (description != null)
        {
            sectionLayout.Controls.Add(new Label
            {
                AutoSize = true,
                Text = description,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, LayoutTokens.GetScaled(2), 0, 0),
            }, 0, 1);
        }

        editorTable.Controls.Add(sectionLayout, 0, row);
        editorTable.SetColumnSpan(sectionLayout, 2);
    }

    private void AddControlRow(string labelText, Control control, int row)
    {
        editorTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            AutoSize = true,
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, LayoutTokens.GetScaled(10), LayoutTokens.GetScaled(16), 0),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        };

        editorTable.Controls.Add(label, 0, row);
        editorTable.Controls.Add(control, 1, row);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private async void BudgetEntryEditDialog_Load(object? sender, EventArgs e)
    {
        btnOK.Enabled = false;

        try
        {
            await LoadDepartmentsAndFundsAsync();
            BindControls();
            txtAccountNumber.Leave += txtAccountNumber_Leave;

            if (!string.IsNullOrWhiteSpace(txtAccountNumber.Text))
            {
                await RefreshAccountMetadataAsync(txtAccountNumber.Text.Trim());
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error loading dialog data: {ex.Message}", "Budget Entry", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnOK.Enabled = true;
        }
    }

    private async Task LoadDepartmentsAndFundsAsync()
    {
        var contextFactory = ServiceProviderServiceExtensions.GetRequiredService<IDbContextFactory<AppDbContext>>(_serviceProvider);
        await using var ctx = await contextFactory.CreateDbContextAsync();

        var departments = await ctx.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync();

        var funds = await ctx.Funds
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync();

        _departments.Clear();
        _departments.AddRange(departments);

        _funds.Clear();
        _funds.AddRange(funds);
    }

    private void BindControls()
    {
        cmbDepartment.DataSource = null;
        cmbDepartment.DataSource = new BindingSource { DataSource = _departments };

        cmbFund.DataSource = null;
        cmbFund.DataSource = new BindingSource { DataSource = _funds };

        txtFiscalYear.Text = Entry.FiscalYear.ToString();
        txtAccountNumber.Text = Entry.AccountNumber;
        txtDescription.Text = Entry.Description;
        txtBudgetedAmount.Text = Entry.BudgetedAmount.ToString("F2");
        txtActualAmount.Text = Entry.ActualAmount.ToString("F2");
        txtAccountType.Text = Entry.MunicipalAccount?.Type.ToString() ?? "Look up from chart of accounts";

        if (cmbFundType.Items.Count > 0)
        {
            cmbFundType.SelectedItem = Entry.FundType.ToString();
            if (cmbFundType.SelectedIndex < 0)
            {
                cmbFundType.SelectedIndex = 0;
            }
        }

        if (Entry.DepartmentId > 0)
        {
            cmbDepartment.SelectedValue = Entry.DepartmentId;
        }
        else if (_departments.Count > 0)
        {
            cmbDepartment.SelectedIndex = 0;
        }

        if (Entry.FundId.HasValue)
        {
            cmbFund.SelectedValue = Entry.FundId.Value;
        }
        else if (_funds.Count > 0)
        {
            cmbFund.SelectedIndex = 0;
        }
    }

    private async void txtAccountNumber_Leave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtAccountNumber.Text))
        {
            Entry.MunicipalAccountId = null;
            txtAccountType.Text = "Look up from chart of accounts";
            return;
        }

        await RefreshAccountMetadataAsync(txtAccountNumber.Text.Trim());
    }

    private async Task RefreshAccountMetadataAsync(string accountNumber)
    {
        try
        {
            var contextFactory = ServiceProviderServiceExtensions.GetRequiredService<IDbContextFactory<AppDbContext>>(_serviceProvider);
            await using var ctx = await contextFactory.CreateDbContextAsync();

            var account = await ctx.MunicipalAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountNumber_Value == accountNumber);

            Entry.MunicipalAccountId = account?.Id;
            txtAccountType.Text = account?.Type.ToString() ?? "Unknown account number";
        }
        catch
        {
            txtAccountType.Text = "Unable to load account type";
        }
    }

    private void btnOK_Click(object? sender, EventArgs e)
    {
        if (!ValidateEntry())
        {
            return;
        }

        UpdateEntryFromControls();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateEntryFromControls()
    {
        Entry.AccountNumber = txtAccountNumber.Text.Trim();
        Entry.Description = txtDescription.Text.Trim();

        if (decimal.TryParse(txtBudgetedAmount.Text, out var budgetedAmount))
        {
            Entry.BudgetedAmount = budgetedAmount;
        }

        if (decimal.TryParse(txtActualAmount.Text, out var actualAmount))
        {
            Entry.ActualAmount = actualAmount;
        }

        if (cmbDepartment.SelectedItem is Department department)
        {
            Entry.DepartmentId = department.Id;
            Entry.Department = department;
        }
        else if (cmbDepartment.SelectedValue is int departmentId)
        {
            Entry.DepartmentId = departmentId;
        }

        if (cmbFund.SelectedItem is Fund fund)
        {
            Entry.FundId = fund.Id;
            Entry.Fund = fund;
        }
        else if (cmbFund.SelectedValue is int fundId)
        {
            Entry.FundId = fundId;
        }

        if (cmbFundType.SelectedItem != null && Enum.TryParse<FundType>(cmbFundType.SelectedItem.ToString(), out var fundType))
        {
            Entry.FundType = fundType;
        }

        Entry.Variance = Entry.BudgetedAmount - Entry.ActualAmount;
        Entry.StartPeriod = new DateTime(Entry.FiscalYear, 1, 1);
        Entry.EndPeriod = new DateTime(Entry.FiscalYear, 12, 31);
        Entry.IsGASBCompliant = true;
        Entry.CreatedAt = Entry.CreatedAt == default ? DateTime.UtcNow : Entry.CreatedAt;
        Entry.UpdatedAt = IsNew ? Entry.UpdatedAt : DateTime.UtcNow;
    }

    private bool ValidateEntry()
    {
        if (string.IsNullOrWhiteSpace(txtAccountNumber.Text))
        {
            MessageBox.Show(this, "Enter an account number before saving.", "Account Number Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtAccountNumber.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(txtDescription.Text))
        {
            MessageBox.Show(this, "Enter a description for the budget line before saving.", "Description Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtDescription.Focus();
            return false;
        }

        if (!decimal.TryParse(txtBudgetedAmount.Text, out var budgetedAmount) || budgetedAmount < 0)
        {
            MessageBox.Show(this, "Budgeted amount must be a valid number zero or greater.", "Invalid Budgeted Amount", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtBudgetedAmount.Focus();
            return false;
        }

        if (!decimal.TryParse(txtActualAmount.Text, out var actualAmount) || actualAmount < 0)
        {
            MessageBox.Show(this, "Actual amount must be zero or greater.", "Invalid Actual Amount", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtActualAmount.Focus();
            return false;
        }

        if (cmbDepartment.SelectedItem == null && cmbDepartment.SelectedValue == null)
        {
            MessageBox.Show(this, "Select a department before saving.", "Department Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            cmbDepartment.Focus();
            return false;
        }

        if (cmbFund.SelectedItem == null && cmbFund.SelectedValue == null)
        {
            MessageBox.Show(this, "Select a fund before saving.", "Fund Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            cmbFund.Focus();
            return false;
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tooltip.Dispose();
        }

        base.Dispose(disposing);
    }
}
