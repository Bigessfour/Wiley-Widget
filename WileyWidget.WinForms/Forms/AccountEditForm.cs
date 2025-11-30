using System.Globalization;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

using WileyWidget.WinForms.Theming;

/// <summary>
/// Dialog for creating/editing municipal accounts
/// </summary>
public partial class AccountEditForm : Form
{
    private readonly AccountsViewModel _viewModel;
    private readonly MunicipalAccount? _existingAccount;
    private readonly bool _isNew;

    // Form controls
    private TextBox txtAccountNumber = null!;
    private TextBox txtName = null!;
    private ComboBox cmbDepartment = null!;
    private ComboBox cmbFund = null!;
    private ComboBox cmbType = null!;
    private NumericUpDown numBalance = null!;
    private NumericUpDown numBudget = null!;
    private CheckBox chkActive = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    private List<Department> _departments = new();

    public AccountEditForm(AccountsViewModel viewModel, MunicipalAccount? existingAccount = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _existingAccount = existingAccount;
        _isNew = existingAccount == null;

        InitializeComponent();
        SetupUI();
        _ = LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            txtAccountNumber?.Dispose();
            txtName?.Dispose();
            cmbDepartment?.Dispose();
            cmbFund?.Dispose();
            cmbType?.Dispose();
            numBalance?.Dispose();
            numBudget?.Dispose();
            chkActive?.Dispose();
            btnSave?.Dispose();
            btnCancel?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        Text = _isNew ? "New Account" : "Edit Account";
        Size = new Size(500, 480);
        try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
    }

    private void SetupUI()
    {
        var padding = 16;
        var labelWidth = 120;
        var controlWidth = 320;
        var rowHeight = 36;
        var y = padding;

        // Account Number
        Controls.Add(new Label { Text = "Account Number:", Location = new Point(padding, y + 4), AutoSize = true });
        txtAccountNumber = new TextBox { Location = new Point(padding + labelWidth, y), Width = controlWidth, MaxLength = 20 };
        Controls.Add(txtAccountNumber);
        y += rowHeight;

        // Name
        Controls.Add(new Label { Text = "Name:", Location = new Point(padding, y + 4), AutoSize = true });
        txtName = new TextBox { Location = new Point(padding + labelWidth, y), Width = controlWidth, MaxLength = 100 };
        Controls.Add(txtName);
        y += rowHeight;

        // Department
        Controls.Add(new Label { Text = "Department:", Location = new Point(padding, y + 4), AutoSize = true });
        cmbDepartment = new ComboBox { Location = new Point(padding + labelWidth, y), Width = controlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
        Controls.Add(cmbDepartment);
        y += rowHeight;

        // Fund
        Controls.Add(new Label { Text = "Fund:", Location = new Point(padding, y + 4), AutoSize = true });
        cmbFund = new ComboBox { Location = new Point(padding + labelWidth, y), Width = controlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbFund.Items.AddRange(Enum.GetValues<MunicipalFundType>().Cast<object>().ToArray());
        Controls.Add(cmbFund);
        y += rowHeight;

        // Type
        Controls.Add(new Label { Text = "Type:", Location = new Point(padding, y + 4), AutoSize = true });
        cmbType = new ComboBox { Location = new Point(padding + labelWidth, y), Width = controlWidth, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbType.Items.AddRange(Enum.GetValues<AccountType>().Cast<object>().ToArray());
        Controls.Add(cmbType);
        y += rowHeight;

        // Balance
        Controls.Add(new Label { Text = "Balance:", Location = new Point(padding, y + 4), AutoSize = true });
        numBalance = new NumericUpDown
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            DecimalPlaces = 2,
            Minimum = decimal.MinValue,
            Maximum = decimal.MaxValue,
            ThousandsSeparator = true
        };
        Controls.Add(numBalance);
        y += rowHeight;

        // Budget Amount
        Controls.Add(new Label { Text = "Budget Amount:", Location = new Point(padding, y + 4), AutoSize = true });
        numBudget = new NumericUpDown
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            DecimalPlaces = 2,
            Minimum = decimal.MinValue,
            Maximum = decimal.MaxValue,
            ThousandsSeparator = true
        };
        Controls.Add(numBudget);
        y += rowHeight;

        // Active
        Controls.Add(new Label { Text = "Active:", Location = new Point(padding, y + 4), AutoSize = true });
        chkActive = new CheckBox { Location = new Point(padding + labelWidth, y), Checked = true };
        Controls.Add(chkActive);
        y += rowHeight + 16;

        // Buttons
        btnSave = new Button { Text = "Save", Width = 100, Height = 32, Location = new Point(padding + labelWidth, y) };
        btnSave.Click += BtnSave_Click;
        Controls.Add(btnSave);

        btnCancel = new Button { Text = "Cancel", Width = 100, Height = 32, Location = new Point(padding + labelWidth + 110, y), DialogResult = DialogResult.Cancel };
        Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        // Apply theming and live updates
        try
        {
            ThemeManager.ApplyTheme(this);
            ThemeManager.ThemeChanged += (s, t) => ThemeManager.ApplyTheme(this);
        }
        catch { }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Load departments
            _departments = await _viewModel.GetDepartmentsAsync();
            cmbDepartment.DataSource = _departments;
            cmbDepartment.DisplayMember = "Name";
            cmbDepartment.ValueMember = "Id";

            // Populate form if editing
            if (_existingAccount != null)
            {
                txtAccountNumber.Text = _existingAccount.AccountNumber?.Value ?? "";
                txtAccountNumber.Enabled = false; // Don't allow changing account number
                txtName.Text = _existingAccount.Name;

                // Set department
                var dept = _departments.FirstOrDefault(d => d.Id == _existingAccount.DepartmentId);
                if (dept != null) cmbDepartment.SelectedItem = dept;

                cmbFund.SelectedItem = _existingAccount.Fund;
                cmbType.SelectedItem = _existingAccount.Type;
                numBalance.Value = _existingAccount.Balance;
                numBudget.Value = _existingAccount.BudgetAmount;
                chkActive.Checked = _existingAccount.IsActive;
            }
            else
            {
                // Defaults for new account
                cmbFund.SelectedIndex = 0;
                cmbType.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load form data");
            MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(txtAccountNumber.Text))
        {
            MessageBox.Show("Account Number is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtAccountNumber.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtName.Focus();
            return;
        }

        if (cmbDepartment.SelectedItem is not Department selectedDept)
        {
            MessageBox.Show("Please select a Department.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            btnSave.Enabled = false;
            btnSave.Text = "Saving...";

            bool success;

            if (_isNew)
            {
                // Get active budget period
                var budgetPeriod = await _viewModel.GetActiveBudgetPeriodAsync();
                if (budgetPeriod == null)
                {
                    MessageBox.Show("No active budget period found. Please create one first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var newAccount = new MunicipalAccount
                {
                    AccountNumber = new AccountNumber(txtAccountNumber.Text.Trim()),
                    Name = txtName.Text.Trim(),
                    DepartmentId = selectedDept.Id,
                    BudgetPeriodId = budgetPeriod.Id,
                    Fund = (MunicipalFundType)cmbFund.SelectedItem!,
                    Type = (AccountType)cmbType.SelectedItem!,
                    Balance = numBalance.Value,
                    BudgetAmount = numBudget.Value,
                    IsActive = chkActive.Checked
                };

                success = await _viewModel.CreateAccountAsync(newAccount);
            }
            else
            {
                _existingAccount!.Name = txtName.Text.Trim();
                _existingAccount.DepartmentId = selectedDept.Id;
                _existingAccount.Fund = (MunicipalFundType)cmbFund.SelectedItem!;
                _existingAccount.Type = (AccountType)cmbType.SelectedItem!;
                _existingAccount.Balance = numBalance.Value;
                _existingAccount.BudgetAmount = numBudget.Value;
                _existingAccount.IsActive = chkActive.Checked;

                success = await _viewModel.UpdateAccountAsync(_existingAccount);
            }

            if (success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show("Failed to save account. Check logs for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error saving account");
            MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnSave.Enabled = true;
            btnSave.Text = "Save";
        }
    }
}
