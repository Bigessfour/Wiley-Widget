using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Models;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.ViewModels;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using Action = System.Action;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Panel for creating or editing municipal accounts with full validation and data binding.
    /// </summary>
    public partial class AccountEditPanel : UserControl
    {
        /// <summary>
        /// Gets the data context for MVVM binding.
        /// </summary>
        public new object? DataContext { get; private set; }

        /// <summary>
        /// Gets the dialog result after save/cancel operations.
        /// </summary>
        public DialogResult SaveDialogResult { get; private set; } = DialogResult.None;

        private readonly AccountsViewModel _viewModel;
        private readonly MunicipalAccountEditModel _editModel;
        private readonly MunicipalAccount? _existingAccount;
        private ErrorProvider? _errorProvider;
        private ErrorProviderBinding? _errorBinding;

        private bool _isNew;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountEditPanel"/> class.
        /// </summary>
        /// <param name="viewModel">The accounts view model.</param>
        /// <param name="existingAccount">Existing account to edit, or null to create new.</param>
        public AccountEditPanel(AccountsViewModel viewModel, MunicipalAccount? existingAccount = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;
            _existingAccount = existingAccount;
            _isNew = existingAccount == null;

            // Create the edit model from existing entity or as a new model
            _editModel = existingAccount != null
                ? MunicipalAccountEditModel.FromEntity(existingAccount)
                : new MunicipalAccountEditModel
                {
                    IsActive = true,
                    Balance = 0m,
                    BudgetAmount = 0m
                };

            InitializeComponent();
            SetupValidation();

            // Dynamic setup
            lblTitle.Text = _isNew ? "Create New Account" : "Edit Account";
            txtAccountNumber.Enabled = _isNew;
            btnSave.Text = _isNew ? "&Create" : "&Save";
            btnSave.AccessibleName = _isNew ? "Create Account" : "Save Account";
            if (_existingAccount != null)
            {
                txtAccountNumber.Text = _existingAccount.AccountNumber?.Value ?? "";
                txtName.Text = _existingAccount.Name ?? "";
                txtDescription.Text = _existingAccount.FundDescription ?? "";
                numBalance.Value = (double)_existingAccount.Balance;
                numBudget.Value = (double)_existingAccount.BudgetAmount;
                chkActive.Checked = _existingAccount.IsActive;
            }

            // Theme is applied by SfSkinManager cascade from parent form

            // Load data asynchronously on load event
            this.Load += AccountEditPanel_Load;
        }

        private async void AccountEditPanel_Load(object? sender, EventArgs e)
        {
            try
            {
                if (IsDisposed) return;
                await LoadDataAsync();
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("AccountEditPanel disposed during load");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "AccountEditPanel_Load: unexpected error");
                MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Sets up validation binding using ErrorProviderBinding for MVVM-style validation.
        /// </summary>
        private void SetupValidation()
        {
            try
            {
                _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
                _errorBinding = new ErrorProviderBinding(_errorProvider, _editModel);

                // Map edit model properties to their corresponding controls
                _errorBinding.MapControl(nameof(_editModel.AccountNumber), txtAccountNumber);
                _errorBinding.MapControl(nameof(_editModel.Name), txtName);
                _errorBinding.MapControl(nameof(_editModel.DepartmentId), cmbDepartment);
                _errorBinding.MapControl(nameof(_editModel.Balance), numBalance);
                _errorBinding.MapControl(nameof(_editModel.BudgetAmount), numBudget);

                // Wire up text changed events to update the edit model
                txtAccountNumber.TextChanged += (s, e) => _editModel.AccountNumber = txtAccountNumber.Text;
                txtName.TextChanged += (s, e) => _editModel.Name = txtName.Text;
                txtDescription.TextChanged += (s, e) => _editModel.Description = txtDescription.Text;
                numBalance.ValueChanged += (s, e) => _editModel.Balance = numBalance.Value.HasValue ? (decimal)numBalance.Value.Value : 0m;
                numBudget.ValueChanged += (s, e) => _editModel.BudgetAmount = numBudget.Value.HasValue ? (decimal)numBudget.Value.Value : 0m;
                chkActive.CheckedChanged += (s, e) => _editModel.IsActive = chkActive.Checked;

                // Department and fund/type selection
                cmbDepartment.SelectedValueChanged += (s, e) =>
                {
                    if (cmbDepartment.SelectedValue is int deptId)
                        _editModel.DepartmentId = deptId;
                };

                cmbFund.SelectedValueChanged += (s, e) =>
                {
                    if (cmbFund.SelectedValue is MunicipalFundType fund)
                        _editModel.Fund = fund;
                };

                cmbType.SelectedValueChanged += (s, e) =>
                {
                    if (cmbType.SelectedValue is AccountType type)
                        _editModel.Type = type;
                };
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountEditPanel: Failed to setup validation");
            }
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                // Theme is applied by SfSkinManager cascade
            }
            catch { }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            try
            {
                if (IsDisposed) return;

                if (InvokeRequired)
                {
                    // Check if handle exists before BeginInvoke
                    if (IsHandleCreated)
                    {
                        try { BeginInvoke(new Action(() => OnThemeChanged(sender, theme))); } catch { }
                    }
                    return;
                }

                ApplyCurrentTheme();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Serilog.Log.Warning(ex, "AccountEditPanel: OnThemeChanged failed"); }
        }

        /// <summary>
        /// Load supporting data like departments and populate dropdowns.
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                Serilog.Log.Debug("AccountEditPanel: Loading departments and fund/type data");

                // Load departments
                var depts = await _viewModel.GetDepartmentsAsync();
                if (depts != null && depts.Count > 0)
                {
                    cmbDepartment.DataSource = depts;
                    cmbDepartment.DisplayMember = "Name";
                    cmbDepartment.ValueMember = "Id";

                    // Select existing department if editing
                    if (_existingAccount?.Department != null)
                    {
                        cmbDepartment.SelectedValue = _existingAccount.Department.Id;
                    }
                }
                else
                {
                    // Provide sample departments if repository fails
                    var sampleDepts = new[]
                    {
                        new Department { Id = 1, Name = "Finance" },
                        new Department { Id = 2, Name = "Public Works" },
                        new Department { Id = 3, Name = "Water Department" },
                        new Department { Id = 4, Name = "Tax Collector" },
                        new Department { Id = 5, Name = "Human Resources" }
                    };
                    cmbDepartment.DataSource = sampleDepts;
                    cmbDepartment.DisplayMember = "Name";
                    cmbDepartment.ValueMember = "Id";
                    Serilog.Log.Warning("AccountEditPanel: Using sample departments (repository returned no data)");
                }

                // Set fund types
                cmbFund.DataSource = Enum.GetValues(typeof(MunicipalFundType));
                if (_existingAccount != null)
                {
                    cmbFund.SelectedItem = _existingAccount.Fund;
                }

                // Set account types
                cmbType.DataSource = Enum.GetValues(typeof(AccountType));
                if (_existingAccount != null)
                {
                    cmbType.SelectedItem = _existingAccount.Type;
                }

                Serilog.Log.Debug("AccountEditPanel: Data loaded successfully");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "AccountEditPanel.LoadDataAsync failed");
                try
                {
                    MessageBox.Show($"Error loading data: {ex.Message}\n\nSome dropdowns may have limited options.",
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch { }
            }
        }

        /// <summary>
        /// Handles the Save button click to validate and save the account.
        /// </summary>
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                Serilog.Log.Information("AccountEditPanel: Save button clicked");

                // Sync all values from controls to edit model
                _editModel.AccountNumber = txtAccountNumber.Text;
                _editModel.Name = txtName.Text;
                _editModel.Description = txtDescription.Text;
                _editModel.Balance = numBalance.Value.HasValue ? (decimal)numBalance.Value.Value : 0m;
                _editModel.BudgetAmount = numBudget.Value.HasValue ? (decimal)numBudget.Value.Value : 0m;
                _editModel.IsActive = chkActive.Checked;

                if (cmbDepartment.SelectedValue is int deptId)
                    _editModel.DepartmentId = deptId;
                if (cmbFund.SelectedValue is MunicipalFundType fund)
                    _editModel.Fund = fund;
                if (cmbType.SelectedValue is AccountType type)
                    _editModel.Type = type;

                // Validate the edit model using data annotations
                if (!_editModel.ValidateAll())
                {
                    var errors = _editModel.GetAllErrors();
                    if (errors.Count > 0)
                    {
                        _errorBinding?.RefreshAllErrors();
                        MessageBox.Show(
                            $"Please correct the following errors:\n\n{string.Join("\n", errors)}",
                            "Validation Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                // Convert edit model to entity
                var account = _editModel.ToEntity();

                // If editing, preserve the Id
                if (_existingAccount != null)
                {
                    account.Id = _existingAccount.Id;
                }

                // Save via view model command
                if (_isNew)
                {
                    await _viewModel.CreateAccountCommand.ExecuteAsync(account);
                }
                else
                {
                    await _viewModel.UpdateAccountCommand.ExecuteAsync(account);
                }

                // Check for errors from viewmodel
                if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                {
                    MessageBox.Show(_viewModel.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                SaveDialogResult = DialogResult.OK;

                // If hosted in a parent Form, set its DialogResult
                var parent = this.FindForm();
                if (parent != null)
                {
                    parent.DialogResult = DialogResult.OK;
                    parent.Close();
                }

                Serilog.Log.Information("AccountEditPanel: Account saved successfully - {AccountNumber}", _editModel.AccountNumber);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "AccountEditPanel: BtnSave_Click failed");
                try
                {
                    MessageBox.Show($"Error saving account: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        /// <summary>
        /// Cancels the edit operation and closes the panel.
        /// </summary>
        private void Cancel(object? sender, EventArgs e)
        {
            SaveDialogResult = DialogResult.Cancel;
            var parent = this.FindForm();
            if (parent != null)
            {
                parent.DialogResult = DialogResult.Cancel;
                parent.Close();
            }
        }
    }
}
