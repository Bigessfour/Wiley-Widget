using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Data;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Dialogs;

public partial class BudgetEntryEditDialog : SfForm
{
    public BudgetEntry Entry { get; private set; }
    public bool IsNew { get; private set; }

    private readonly IServiceProvider _serviceProvider;
    private readonly List<Department> _departments = new List<Department>();
    private readonly List<Fund> _funds = new List<Fund>();
    private ToolTip _tooltip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 500 };

    // Designer controls
    private SfButton btnOK;
    private SfButton btnCancel;
    private TextBox txtAccountNumber;
    private TextBox txtDescription;
    private TextBox txtBudgetedAmount;
    private TextBox txtActualAmount;
    private ComboBox cmbDepartment;
    private ComboBox cmbFund;
    private ComboBox cmbFundType;
    private TableLayoutPanel tableLayout;
    private FlowLayoutPanel buttonPanel;

    public BudgetEntryEditDialog(BudgetEntry? entryToEdit, IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        IsNew = entryToEdit == null;
        Entry = entryToEdit ?? new BudgetEntry { FiscalYear = DateTime.Now.Year, CreatedAt = DateTime.UtcNow };

        Text = IsNew ? "Add New Budget Entry" : "Edit Budget Entry";

        // Load data asynchronously
        LoadAsync();
    }

    // Simple manual InitializeComponent to avoid needing a separate .Designer.cs file for now
    private void InitializeComponent()
    {
        this.Size = new Size(550, 520);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.KeyPreview = true;
        this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

        tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(15),
            AutoSize = false
        };
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));

        // Account Information Section (visual grouping)
        var acctLabel = new Label { Text = "Account Information", Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), ForeColor = Color.FromArgb(0, 120, 215) };
        tableLayout.Controls.Add(acctLabel, 0, 0);

        // Account Number
        AddControlRow(tableLayout, "Account Number:", txtAccountNumber = new TextBox { Dock = DockStyle.Fill }, 1);
        AddTooltip(txtAccountNumber, "Unique account identifier (e.g., 101-001)");

        // Description
        AddControlRow(tableLayout, "Account Name:", txtDescription = new TextBox { Dock = DockStyle.Fill }, 2);
        AddTooltip(txtDescription, "Human-readable account name for displays and reports");

        // Account Type (auto-filled from Chart of Accounts — never editable here)
        var txtAccountType = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(240, 240, 240) };
        AddControlRow(tableLayout, "Account Type:", txtAccountType, 3);
        AddTooltip(txtAccountType, "Automatically pulled from your official Chart of Accounts (Revenue or Expenditure)");

        // Classification Section
        var classLabel = new Label { Text = "Budget Classification", Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 10, 0, 0) };
        tableLayout.Controls.Add(classLabel, 0, 4);

        // Department
        cmbDepartment = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            DisplayMember = "Name",
            ValueMember = "Id",
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        AddControlRow(tableLayout, "Department:", cmbDepartment, 5);
        AddTooltip(cmbDepartment, "Select the department responsible for this budget");

        // Fund
        cmbFund = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            DisplayMember = "Name",
            ValueMember = "Id",
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        AddControlRow(tableLayout, "Fund:", cmbFund, 6);
        AddTooltip(cmbFund, "Choose the fund this budget belongs to");

        // Fund Type
        cmbFundType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbFundType.Items.AddRange(Enum.GetNames(typeof(FundType)));
        if (cmbFundType.Items.Count > 0) cmbFundType.SelectedIndex = 0;
        AddControlRow(tableLayout, "Fund Type:", cmbFundType, 7);
        AddTooltip(cmbFundType, "Categorizes this entry by fund type");

        // Amounts Section
        var amtLabel = new Label { Text = "Budget Amounts", Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 10, 0, 0) };
        tableLayout.Controls.Add(amtLabel, 0, 8);

        // Budgeted Amount
        AddControlRow(tableLayout, "Budgeted Amount:", txtBudgetedAmount = new TextBox { Dock = DockStyle.Fill, Text = "0.00", TextAlign = HorizontalAlignment.Right }, 9);
        AddTooltip(txtBudgetedAmount, "Total budgeted amount for this entry (must be a valid number)");

        // Actual Amount
        AddControlRow(tableLayout, "Actual Amount:", txtActualAmount = new TextBox { Dock = DockStyle.Fill, Text = "0.00", TextAlign = HorizontalAlignment.Right }, 10);
        AddTooltip(txtActualAmount, "Amount actually spent to date");

        // Buttons
        buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(15, 10, 15, 10)
        };

        btnOK = new SfButton { Text = "Save Entry", Width = 100, Height = 36 };
        btnOK.Click += btnOK_Click;

        btnCancel = new SfButton { Text = "Cancel", Width = 90, Height = 36 };
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnOK);

        this.Controls.Add(tableLayout);
        this.Controls.Add(buttonPanel);
    }

    private void AddTooltip(Control control, string text)
    {
        _tooltip.SetToolTip(control, text);
    }

    private void AddControlRow(TableLayoutPanel panel, string labelText, Control control, int row)
    {
        panel.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, AutoSize = true }, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private async void LoadAsync()
    {
        await LoadDepartmentsAndFundsAsync();

        // Ensure UI is updated on UI thread
        if (IsHandleCreated)
        {
            Invoke(new Action(BindControls));
        }
        else
        {
            BindControls();
        }

        txtAccountNumber.Leave += txtAccountNumber_Leave;
    }

    private async Task LoadDepartmentsAndFundsAsync()
    {
        try
        {
            var contextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDbContextFactory<AppDbContext>>(_serviceProvider);
            using var ctx = await contextFactory.CreateDbContextAsync();

            var depts = await ctx.Departments.OrderBy(d => d.Name).ToListAsync();
            var funds = await ctx.Funds.OrderBy(f => f.Name).ToListAsync();

            _departments.Clear();
            _departments.AddRange(depts);

            _funds.Clear();
            _funds.AddRange(funds);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BindControls()
    {
        // Bind combos
        cmbDepartment.DataSource = new BindingSource { DataSource = _departments };
        cmbFund.DataSource = new BindingSource { DataSource = _funds };

        if (IsNew)
        {
            // Set defaults for new entry
            cmbFundType.SelectedIndex = 0;
        }
        else
        {
            // Populate fields for existing entry
            txtAccountNumber.Text = Entry.AccountNumber;
            txtDescription.Text = Entry.Description;
            txtBudgetedAmount.Text = Entry.BudgetedAmount.ToString("F2");
            txtActualAmount.Text = Entry.ActualAmount.ToString("F2");

            if (Entry.DepartmentId > 0)
                cmbDepartment.SelectedValue = Entry.DepartmentId;

            if (Entry.FundId.HasValue)
                cmbFund.SelectedValue = Entry.FundId.Value;

            cmbFundType.SelectedItem = Entry.FundType.ToString();
        }
    }

    private async void txtAccountNumber_Leave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtAccountNumber.Text)) return;

        try
        {
            var contextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDbContextFactory<AppDbContext>>(_serviceProvider);
            await using var ctx = await contextFactory.CreateDbContextAsync();

            var acct = await ctx.MunicipalAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountNumber_Value == txtAccountNumber.Text.Trim());

            // Update the Entry so the grid gets the correct Type when saved
            Entry.MunicipalAccountId = acct?.Id;

            // Find our read-only type textbox
            var typeBox = tableLayout.Controls.OfType<TextBox>()
                .FirstOrDefault(t => t.ReadOnly && t.BackColor.R == 240);

            if (typeBox != null)
                typeBox.Text = acct?.Type.ToString() ?? "Unknown (add to Chart of Accounts first)";
        }
        catch { /* silent — user can still save */ }
    }

    private async void btnOK_Click(object? sender, EventArgs e)
    {
        if (!ValidateEntry()) return;

        // NEW: Check for duplicate BEFORE saving
        if (IsNew && await IsDuplicateAsync())
        {
            var result = MessageBox.Show(
                $"Account {Entry.AccountNumber} already has a budget for FY {Entry.FiscalYear}.\n\n" +
                "A duplicate entry already exists. Please edit the existing entry instead.",
                "Duplicate Budget Entry",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return; // Stay in dialog so user can fix the duplicate
        }

        UpdateEntryFromControls();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateEntryFromControls()
    {
        Entry.AccountNumber = txtAccountNumber.Text.Trim();
        Entry.Description = txtDescription.Text.Trim();

        if (decimal.TryParse(txtBudgetedAmount.Text, out var budgeted))
            Entry.BudgetedAmount = budgeted;

        if (decimal.TryParse(txtActualAmount.Text, out var actual))
            Entry.ActualAmount = actual;

        if (cmbDepartment.SelectedValue is int deptId)
            Entry.DepartmentId = deptId;
        else if (cmbDepartment.SelectedItem is Department dept)
            Entry.DepartmentId = dept.Id;

        if (cmbFund.SelectedValue is int fundId)
            Entry.FundId = fundId;
        else if (cmbFund.SelectedItem is Fund fund)
            Entry.FundId = fund.Id;

        if (cmbFundType.SelectedItem != null && Enum.TryParse<FundType>(cmbFundType.SelectedItem.ToString(), out var fundType))
            Entry.FundType = fundType;
    }

    private bool ValidateEntry()
    {
        if (string.IsNullOrWhiteSpace(txtAccountNumber.Text))
        {
            MessageBox.Show("Please enter an account number. This uniquely identifies the account.", "Account Number Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtAccountNumber.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(txtDescription.Text))
        {
            MessageBox.Show("Please enter an account name for display purposes.", "Account Name Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtDescription.Focus();
            return false;
        }

        if (!decimal.TryParse(txtBudgetedAmount.Text, out _))
        {
            MessageBox.Show("Please enter a valid dollar amount for the budgeted amount.", "Invalid Amount", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtBudgetedAmount.Focus();
            return false;
        }

        return true;
    }

    private async Task<bool> IsDuplicateAsync()
    {
        if (string.IsNullOrWhiteSpace(Entry.AccountNumber) || Entry.FiscalYear <= 0) return false;

        try
        {
            var contextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDbContextFactory<AppDbContext>>(_serviceProvider);
            await using var ctx = await contextFactory.CreateDbContextAsync();

            return await ctx.BudgetEntries
                .AnyAsync(b => b.AccountNumber == Entry.AccountNumber.Trim() &&
                               b.FiscalYear == Entry.FiscalYear);
        }
        catch
        {
            return false; // if DB down, let them save (worst case duplicate error)
        }
    }
}
