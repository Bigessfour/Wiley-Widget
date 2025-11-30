using System.Globalization;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using Syncfusion.WinForms.Controls; // SfForm, SfButton
using Syncfusion.WinForms.ListView; // SfComboBox
using Syncfusion.WinForms.Input; // SfNumericTextBox
using Syncfusion.WinForms.Core; // SfSkinManager
using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.Forms;

using WileyWidget.WinForms.Theming;

internal static class AccountEditFormResources
{
    public const string LabelAccountNumber = "Account Number:";
    public const string LabelName = "Name:";
    public const string LabelDepartment = "Department:";
    public const string LabelFund = "Fund:";
    public const string LabelType = "Type:";
    public const string LabelBalance = "Balance:";
    public const string LabelBudgetAmount = "Budget Amount:";
    public const string LabelActive = "Active:";
    public const string SaveButton = "Save";
    public const string CancelButton = "Cancel";
    public const string SavingText = "Saving...";
    public const string ValidationTitle = "Validation";
    public const string ErrorTitle = "Error";
    public const string AccountNumberRequired = "Account Number is required.";
    public const string AccountNumberTooLong = "Account Number cannot exceed 20 characters.";
    public const string NameRequired = "Name is required.";
    public const string NameTooLong = "Name cannot exceed 100 characters.";
    public const string DepartmentRequired = "Please select a Department.";
    public const string NoActiveBudgetPeriod = "No active budget period found. Please create one first.";
    public const string ErrorSaving = "Failed to save account. Check logs for details.";
    public const string LoadDataErrorFormat = "Error loading data: {0}";
}

/// <summary>
/// Dialog for creating/editing municipal accounts.
/// Supports both modal dialog and docked floating tool window modes.
/// </summary>
public partial class AccountEditForm : Syncfusion.WinForms.Controls.SfForm
{
    private readonly AccountsViewModel _viewModel;
    private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;
    private readonly MunicipalAccount? _existingAccount;
    private readonly bool _isNew;

    /// <summary>
    /// DataContext property for ViewModel access when docked.
    /// </summary>
    public object? DataContext { get; private set; }

    // Form controls - using Syncfusion and standard WinForms controls
    private TextBox txtAccountNumber = null!;
    private TextBox txtName = null!;
    private Syncfusion.WinForms.ListView.SfComboBox cmbDepartment = null!;
    private Syncfusion.WinForms.ListView.SfComboBox cmbFund = null!;
    private Syncfusion.WinForms.ListView.SfComboBox cmbType = null!;
    private Syncfusion.WinForms.Input.SfNumericTextBox numBalance = null!;
    private Syncfusion.WinForms.Input.SfNumericTextBox numBudget = null!;
    private CheckBox chkActive = null!;
    private Syncfusion.WinForms.Controls.SfButton btnSave = null!;
    private Syncfusion.WinForms.Controls.SfButton btnCancel = null!;

    // View & Data Binding components - CommunityToolkit.Mvvm patterns
    private ErrorProvider _errorProvider = null!;
    private BindingSource _bindingSource = null!;

    private List<Department> _departments = new();
    private EventHandler<AppTheme>? _themeChangedHandler;

    // Default ctor for DI-friendly usage
    public AccountEditForm() : this(
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsViewModel>(Program.Services),
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Threading.IDispatcherHelper>(Program.Services))
    {
    }
    public AccountEditForm(AccountsViewModel viewModel, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null, MunicipalAccount? existingAccount = null)
    {
        _dispatcherHelper = dispatcherHelper;
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _existingAccount = existingAccount;
        _isNew = existingAccount == null;
        DataContext = viewModel;

        InitializeComponent();
        SetupUI();
        // Keep UI in sync with ViewModel loading state
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AccountsViewModel.IsLoading))
            {
                try
                {
                    // Prefer centralized UI marshaling via IDispatcherHelper when available
                    if (_dispatcherHelper != null)
                    {
                        _ = _dispatcherHelper.InvokeAsync(UpdateLoadingState);
                    }
                    else
                    {
                        if (InvokeRequired) BeginInvoke(new Action(UpdateLoadingState)); else UpdateLoadingState();
                    }
                }
                catch { }
            }
        };

        _ = LoadDataAsync();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        // Standard modal behavior
        DialogResult = DialogResult.Cancel;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { ThemeManager.ThemeChanged -= _themeChangedHandler; } catch { }
            try { txtAccountNumber?.Dispose(); } catch { }
            try { txtName?.Dispose(); } catch { }

            // Syncfusion SfComboBox has a bug in UnWireEvents() that throws NullReferenceException during disposal
            // Clear DataSource before disposing to put the control in a safer state
            try
            {
                if (cmbDepartment != null && !cmbDepartment.IsDisposed)
                {
                    try { cmbDepartment.DataSource = null; } catch { }
                    cmbDepartment.Dispose();
                }
            }
            catch (NullReferenceException) { /* Syncfusion bug - ignore */ }
            catch (ObjectDisposedException) { }
            catch { }

            try
            {
                if (cmbFund != null && !cmbFund.IsDisposed)
                {
                    try { cmbFund.DataSource = null; } catch { }
                    cmbFund.Dispose();
                }
            }
            catch (NullReferenceException) { /* Syncfusion bug - ignore */ }
            catch (ObjectDisposedException) { }
            catch { }

            try
            {
                if (cmbType != null && !cmbType.IsDisposed)
                {
                    try { cmbType.DataSource = null; } catch { }
                    cmbType.Dispose();
                }
            }
            catch (NullReferenceException) { /* Syncfusion bug - ignore */ }
            catch (ObjectDisposedException) { }
            catch { }
            try { numBalance?.Dispose(); } catch { }
            try { numBudget?.Dispose(); } catch { }
            try { chkActive?.Dispose(); } catch { }
            try { btnSave?.Dispose(); } catch { }
            try { btnCancel?.Dispose(); } catch { }
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

        // Initialize View & Data Binding components
        _errorProvider = new ErrorProvider(this) { BlinkStyle = ErrorBlinkStyle.NeverBlink };
        _bindingSource = new BindingSource();

        // Wire up form events for View & Data Binding patterns
        Load += AccountEditForm_Load;
    }

    private void SetupUI()
    {
        var padding = 16;
        var labelWidth = 120;
        var controlWidth = 320;
        var rowHeight = 36;
        var y = padding;

        // Account Number
        Controls.Add(new Label { Text = AccountEditFormResources.LabelAccountNumber, Location = new Point(padding, y + 4), AutoSize = true });
        txtAccountNumber = new TextBox
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            MaxLength = 20,
            Font = new Font("Segoe UI", 9F),
            AccessibleName = "Account Number",
            AccessibleDescription = "Enter the municipal account number (required, max 20 characters)"
        };
        Controls.Add(txtAccountNumber);
        y += rowHeight;

        // Name
        Controls.Add(new Label { Text = AccountEditFormResources.LabelName, Location = new Point(padding, y + 4), AutoSize = true });
        txtName = new TextBox
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            MaxLength = 100,
            Font = new Font("Segoe UI", 9F),
            AccessibleName = "Account Name",
            AccessibleDescription = "Enter the municipal account name (required, max 100 characters)"
        };
        Controls.Add(txtName);
        y += rowHeight;

        // Department
        Controls.Add(new Label { Text = AccountEditFormResources.LabelDepartment, Location = new Point(padding, y + 4), AutoSize = true });
        cmbDepartment = new Syncfusion.WinForms.ListView.SfComboBox
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AccessibleName = "Department",
            AccessibleDescription = "Select the department for this account"
        };
        Controls.Add(cmbDepartment);
        y += rowHeight;

        // Fund
        Controls.Add(new Label { Text = AccountEditFormResources.LabelFund, Location = new Point(padding, y + 4), AutoSize = true });
        cmbFund = new Syncfusion.WinForms.ListView.SfComboBox
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AccessibleName = "Fund Type",
            AccessibleDescription = "Select the municipal fund type"
        };
        // Use DataSource for Syncfusion combo binding (ensures consistent MVVM-style binding)
        // SfComboBox.DataSource semantics are used here
        cmbFund.DataSource = Enum.GetValues<MunicipalFundType>().Cast<object>().ToList();
        cmbFund.AccessibleDescription = "Select the municipal fund type";
        Controls.Add(cmbFund);
        y += rowHeight;

        // Type
        Controls.Add(new Label { Text = AccountEditFormResources.LabelType, Location = new Point(padding, y + 4), AutoSize = true });
        cmbType = new Syncfusion.WinForms.ListView.SfComboBox
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            AccessibleName = "Account Type",
            AccessibleDescription = "Select the account type (Asset, Liability, Equity, Revenue, Expense)"
        };
        // Use DataSource for Syncfusion combo binding (ensures consistent MVVM-style binding)
        // SfComboBox.DataSource semantics are used here
        cmbType.DataSource = Enum.GetValues<AccountType>().Cast<object>().ToList();
        cmbType.AccessibleDescription = "Select the account type (Asset, Liability, Equity, Revenue, Expense)";
        Controls.Add(cmbType);
        y += rowHeight;

        // Balance
        Controls.Add(new Label { Text = AccountEditFormResources.LabelBalance, Location = new Point(padding, y + 4), AutoSize = true });
        numBalance = new Syncfusion.WinForms.Input.SfNumericTextBox
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            NumberDecimalDigits = 2,
            MinValue = (double)decimal.MinValue,
            MaxValue = (double)decimal.MaxValue,
            NumberGroupSeparator = ",",
            AccessibleName = "Current Balance",
            AccessibleDescription = "Enter the current account balance"
        };
        Controls.Add(numBalance);
        y += rowHeight;

        // Budget Amount
        Controls.Add(new Label { Text = AccountEditFormResources.LabelBudgetAmount, Location = new Point(padding, y + 4), AutoSize = true });
        numBudget = new Syncfusion.WinForms.Input.SfNumericTextBox
        {
            Location = new Point(padding + labelWidth, y),
            Width = controlWidth,
            NumberDecimalDigits = 2,
            MinValue = (double)decimal.MinValue,
            MaxValue = (double)decimal.MaxValue,
            NumberGroupSeparator = ",",
            AccessibleName = "Budget Amount",
            AccessibleDescription = "Enter the budgeted amount for this account"
        };
        Controls.Add(numBudget);
        y += rowHeight;

        // Active
        Controls.Add(new Label { Text = AccountEditFormResources.LabelActive, Location = new Point(padding, y + 4), AutoSize = true });
        chkActive = new CheckBox
        {
            Location = new Point(padding + labelWidth, y),
            Checked = true,
            AutoSize = true,
            AccessibleName = "Is Active",
            AccessibleDescription = "Check to mark this account as active"
        };
        Controls.Add(chkActive);
        y += rowHeight + 16;

        // Buttons
        btnSave = new Syncfusion.WinForms.Controls.SfButton
        {
            Text = AccountEditFormResources.SaveButton,
            Width = 100,
            Height = 32,
            Location = new Point(padding + labelWidth, y),
            AccessibleName = "Save Account",
            AccessibleDescription = "Save the account changes and close the dialog"
        };
        btnSave.Click += BtnSave_Click;
        Controls.Add(btnSave);

        try
        {
            var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
            var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
            btnSave.Image = iconService?.GetIcon("save", theme, 14);
            btnSave.ImageAlign = ContentAlignment.MiddleLeft;
            btnSave.TextImageRelation = TextImageRelation.ImageBeforeText;

            _ = WileyWidget.WinForms.Theming.ThemeManager; // ensure static class referenced
            _ = Program.Services; // ensure Program referenced
            // Theme change handler for save button
            EventHandler<AppTheme> saveIconHandler = null!;
            saveIconHandler = (s, t) =>
            {
                try
                {
                    if (_dispatcherHelper != null)
                    {
                        _ = _dispatcherHelper.InvokeAsync(() => btnSave.Image = iconService?.GetIcon("save", t, 14));
                    }
                    else
                    {
                        if (btnSave.InvokeRequired)
                            btnSave.Invoke(() => btnSave.Image = iconService?.GetIcon("save", t, 14));
                        else
                            btnSave.Image = iconService?.GetIcon("save", t, 14);
                    }
                }
                catch { }
            };
            WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += saveIconHandler;
        }
        catch { }

        btnCancel = new Syncfusion.WinForms.Controls.SfButton
        {
            Text = AccountEditFormResources.CancelButton,
            Width = 100,
            Height = 32,
            Location = new Point(padding + labelWidth + 110, y),
            DialogResult = DialogResult.Cancel,
            AccessibleName = "Cancel",
            AccessibleDescription = "Cancel changes and close the dialog without saving"
        };
        Controls.Add(btnCancel);

        try
        {
            var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
            var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
            btnCancel.Image = iconService?.GetIcon("dismiss", theme, 14);
            btnCancel.ImageAlign = ContentAlignment.MiddleLeft;
            btnCancel.TextImageRelation = TextImageRelation.ImageBeforeText;

            // Theme change handler for cancel button
            EventHandler<AppTheme> cancelIconHandler = null!;
            cancelIconHandler = (s, t) =>
            {
                try
                {
                    if (_dispatcherHelper != null)
                    {
                        _ = _dispatcherHelper.InvokeAsync(() => btnCancel.Image = iconService?.GetIcon("dismiss", t, 14));
                    }
                    else
                    {
                        if (btnCancel.InvokeRequired)
                            btnCancel.Invoke(() => btnCancel.Image = iconService?.GetIcon("dismiss", t, 14));
                        else
                            btnCancel.Image = iconService?.GetIcon("dismiss", t, 14);
                    }
                }
                catch { }
            };
            WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += cancelIconHandler;
        }
        catch { }

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        // Apply theming and live updates
        try
        {
            // Configure Syncfusion visual style - SfSkinManager pattern
            Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(this, Syncfusion.WinForms.VisualStyle.Office2016Colorful);

            ThemeManager.ApplyTheme(this);
            _themeChangedHandler = (s, t) => ThemeManager.ApplyTheme(this);
            ThemeManager.ThemeChanged += _themeChangedHandler;
        }
        catch { }
    }

    /// <summary>
    /// Form Load event handler - implements View & Data Binding patterns
    /// CommunityToolkit.Mvvm: ObservableRecipient pattern for data binding
    /// </summary>
    private void AccountEditForm_Load(object? sender, EventArgs e)
    {
        SetupDataBinding();
        SetupValidation();
    }

    /// <summary>
    /// Sets up two-way data binding using BindingSource - View & Data Binding pattern
    /// CommunityToolkit.Mvvm: ObservableProperty pattern for property change notifications
    /// </summary>
    private void SetupDataBinding()
    {
        // Create binding model for the form
        var bindingModel = new AccountBindingModel
        {
            AccountNumber = _existingAccount?.AccountNumber?.Value ?? "",
            Name = _existingAccount?.Name ?? "",
            DepartmentId = _existingAccount?.DepartmentId ?? 0,
            Fund = _existingAccount?.Fund ?? MunicipalFundType.General,
            Type = _existingAccount?.Type ?? AccountType.Asset,
            Balance = _existingAccount?.Balance ?? 0,
            BudgetAmount = _existingAccount?.BudgetAmount ?? 0,
            IsActive = _existingAccount?.IsActive ?? true
        };

        _bindingSource.DataSource = bindingModel;

        // Two-way data binding - View & Data Binding pattern
        txtAccountNumber.DataBindings.Add("Text", _bindingSource, "AccountNumber", true, DataSourceUpdateMode.OnPropertyChanged);
        txtName.DataBindings.Add("Text", _bindingSource, "Name", true, DataSourceUpdateMode.OnPropertyChanged);
        cmbDepartment.DataBindings.Add("SelectedValue", _bindingSource, "DepartmentId", true, DataSourceUpdateMode.OnPropertyChanged);
        cmbFund.DataBindings.Add("SelectedItem", _bindingSource, "Fund", true, DataSourceUpdateMode.OnPropertyChanged);
        cmbType.DataBindings.Add("SelectedItem", _bindingSource, "Type", true, DataSourceUpdateMode.OnPropertyChanged);
        numBalance.DataBindings.Add("Value", _bindingSource, "Balance", true, DataSourceUpdateMode.OnPropertyChanged);
        numBudget.DataBindings.Add("Value", _bindingSource, "BudgetAmount", true, DataSourceUpdateMode.OnPropertyChanged);
        chkActive.DataBindings.Add("Checked", _bindingSource, "IsActive", true, DataSourceUpdateMode.OnPropertyChanged);
    }

    /// <summary>
    /// Sets up validation using ErrorProvider - View & Data Binding pattern
    /// CommunityToolkit.Mvvm: AsyncRelayCommand pattern for validation commands
    /// </summary>
    private void SetupValidation()
    {
        // Validation events - View & Data Binding pattern
        txtAccountNumber.Validating += TxtAccountNumber_Validating;
        txtAccountNumber.Validated += TxtAccountNumber_Validated;
        txtName.Validating += TxtName_Validating;
        txtName.Validated += TxtName_Validated;
        cmbDepartment.Validating += CmbDepartment_Validating;
        cmbDepartment.Validated += CmbDepartment_Validated;
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
                numBalance.Value = (double?)_existingAccount.Balance;
                numBudget.Value = (double?)_existingAccount.BudgetAmount;
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
            MessageBox.Show(string.Format(AccountEditFormResources.LoadDataErrorFormat, ex.Message), AccountEditFormResources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Prepare this dialog to be embedded inside a docking host.
    /// Makes the form non top-level and suitable for DockingManager usage.
    /// </summary>
    public void PrepareForDocking()
    {
        try
        {
            TopLevel = false;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            Dock = DockStyle.Fill;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(400, 450);

            // Cancel button should close the docked panel instead of returning DialogResult
            if (btnCancel != null)
            {
                btnCancel.DialogResult = DialogResult.None;
                btnCancel.Click -= BtnCancel_Click;
                btnCancel.Click += (s, e) =>
                {
                    // Try to close via MainForm if docked
                    var parentForm = this.FindForm();
                    if (parentForm is WileyWidget.WinForms.Forms.MainForm mainForm)
                    {
                        mainForm.ClosePanel("Account Edit");
                    }
                    else
                    {
                        Close();
                    }
                };
            }

            Serilog.Log.Debug("AccountEditForm: prepared for docking");
        }
        catch { }
    }

    // Validation event handlers - View & Data Binding pattern
    private void TxtAccountNumber_Validating(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtAccountNumber.Text))
        {
            _errorProvider.SetError(txtAccountNumber, AccountEditFormResources.AccountNumberRequired);
            e.Cancel = true;
        }
        else if (txtAccountNumber.Text.Length > 20)
        {
            _errorProvider.SetError(txtAccountNumber, AccountEditFormResources.AccountNumberTooLong);
            e.Cancel = true;
        }
        else
        {
            _errorProvider.SetError(txtAccountNumber, "");
        }
    }

    private void TxtAccountNumber_Validated(object? sender, EventArgs e)
    {
        // Additional validation logic if needed
    }

    private void TxtName_Validating(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            _errorProvider.SetError(txtName, AccountEditFormResources.NameRequired);
            e.Cancel = true;
        }
        else if (txtName.Text.Length > 100)
        {
            _errorProvider.SetError(txtName, AccountEditFormResources.NameTooLong);
            e.Cancel = true;
        }
        else
        {
            _errorProvider.SetError(txtName, "");
        }
    }

    private void TxtName_Validated(object? sender, EventArgs e)
    {
        // Additional validation logic if needed
    }

    private void CmbDepartment_Validating(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (cmbDepartment.SelectedValue == null)
        {
            _errorProvider.SetError(cmbDepartment, AccountEditFormResources.DepartmentRequired);
            e.Cancel = true;
        }
        else
        {
            _errorProvider.SetError(cmbDepartment, "");
        }
    }

    private void CmbDepartment_Validated(object? sender, EventArgs e)
    {
        // Additional validation logic if needed
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (cmbDepartment.SelectedItem is not Department selectedDept)
        {
            MessageBox.Show(AccountEditFormResources.DepartmentRequired, AccountEditFormResources.ValidationTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            MunicipalAccount accountToSave;

            if (_isNew)
            {
                var budgetPeriod = await _viewModel.GetActiveBudgetPeriodAsync();
                if (budgetPeriod == null)
                {
                    MessageBox.Show(AccountEditFormResources.NoActiveBudgetPeriod, AccountEditFormResources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                accountToSave = new MunicipalAccount
                {
                    AccountNumber = new AccountNumber(txtAccountNumber.Text.Trim()),
                    Name = txtName.Text.Trim(),
                    DepartmentId = selectedDept.Id,
                    BudgetPeriodId = budgetPeriod.Id,
                    Fund = (MunicipalFundType)cmbFund.SelectedItem!,
                    Type = (AccountType)cmbType.SelectedItem!,
                    Balance = (decimal)(numBalance.Value ?? 0),
                    BudgetAmount = (decimal)(numBudget.Value ?? 0),
                    IsActive = chkActive.Checked
                };
            }
            else
            {
                _existingAccount!.Name = txtName.Text.Trim();
                _existingAccount.DepartmentId = selectedDept.Id;
                _existingAccount.Fund = (MunicipalFundType)cmbFund.SelectedItem!;
                _existingAccount.Type = (AccountType)cmbType.SelectedItem!;
                _existingAccount.Balance = (decimal)(numBalance.Value ?? 0);
                _existingAccount.BudgetAmount = (decimal)(numBudget.Value ?? 0);
                _existingAccount.IsActive = chkActive.Checked;
                accountToSave = _existingAccount;
            }

            var validation = _viewModel.ValidateAccount(accountToSave).ToList();
            if (validation.Any())
            {
                MessageBox.Show(string.Join(Environment.NewLine, validation), AccountEditFormResources.ValidationTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var success = await _viewModel.SaveAccountAsync(accountToSave);
            if (success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(AccountEditFormResources.ErrorSaving, AccountEditFormResources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error saving account");
            MessageBox.Show(string.Format(AccountEditFormResources.LoadDataErrorFormat, ex.Message), AccountEditFormResources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateLoadingState()
    {
        try
        {
            btnSave.Enabled = !_viewModel.IsLoading;
            btnSave.Text = _viewModel.IsLoading ? AccountEditFormResources.SavingText : AccountEditFormResources.SaveButton;
        }
        catch { }
    }
}

/// <summary>
/// Binding model for AccountEditForm — now uses CommunityToolkit.Mvvm patterns.
/// </summary>

internal partial class AccountBindingModel : ObservableObject
{
    [ObservableProperty]
    private string accountNumber = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private int departmentId;

    [ObservableProperty]
    private MunicipalFundType fund;

    [ObservableProperty]
    private AccountType type;

    [ObservableProperty]
    private decimal balance;

    [ObservableProperty]
    private decimal budgetAmount;

    [ObservableProperty]
    private bool isActive = true;
}
