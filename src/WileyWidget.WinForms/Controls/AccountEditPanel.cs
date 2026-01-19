using System.Threading;
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
using WileyWidget.WinForms.ViewModels;
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

        private Label? lblTitle = null;
        private TextBox txtAccountNumber = null!;
        private TextBox txtName = null!;
        private TextBox txtDescription = null!;
        private SfComboBox cmbDepartment = null!;
        private SfComboBox cmbFund = null!;
        private SfComboBox cmbType = null!;
        private SfNumericTextBox numBalance = null!;
        private SfNumericTextBox numBudget = null!;
        private CheckBoxAdv chkActive = null!;
        private Syncfusion.WinForms.Controls.SfButton btnSave = null!;
        private Syncfusion.WinForms.Controls.SfButton btnCancel = null!;

        private readonly AccountsViewModel _viewModel;
        private readonly MunicipalAccountEditModel _editModel;
        private readonly MunicipalAccount? _existingAccount;
        private ErrorProvider? _errorProvider;
        private ErrorProviderBinding? _errorBinding;
        private ToolTip? _toolTip;
        private BindingSource _bindingSource = null!;

        private bool _isNew;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountEditPanel"/> class.
        /// </summary>
        /// <param name="viewModel">The accounts view model.</param>
        /// <param name="existingAccount">Existing account to edit, or null to create new.</param>
        public AccountEditPanel(AccountsViewModel viewModel, MunicipalAccount? existingAccount = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _existingAccount = existingAccount;
            _isNew = existingAccount == null;
            _editModel = new MunicipalAccountEditModel(existingAccount);

            // BindingSource wraps the edit model for robust WinForms data-binding scenarios
            _bindingSource = new BindingSource { DataSource = _editModel };

            InitializeComponent();

            // Apply theme via SfSkinManager (remove any manual colors)
            SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);

            DataContext = _editModel;
            BindControls();
            SetupValidation();

            // Load data asynchronously on load event
            this.Load += AccountEditPanel_Load;
        }

        private void BindControls()
        {
            // Bind controls to _editModel properties (MVVM style)
            txtAccountNumber.DataBindings.Add(nameof(TextBox.Text), _bindingSource, nameof(MunicipalAccountEditModel.AccountNumber), true, DataSourceUpdateMode.OnPropertyChanged);
            txtName.DataBindings.Add(nameof(TextBox.Text), _bindingSource, nameof(MunicipalAccountEditModel.Name), true, DataSourceUpdateMode.OnPropertyChanged);
            txtDescription.DataBindings.Add(nameof(TextBox.Text), _bindingSource, nameof(MunicipalAccountEditModel.Description), true, DataSourceUpdateMode.OnPropertyChanged);

            // SfNumericTextBox uses Value property (double?)
            numBalance.DataBindings.Add("Value", _bindingSource, nameof(MunicipalAccountEditModel.Balance), true, DataSourceUpdateMode.OnPropertyChanged);
            numBudget.DataBindings.Add("Value", _bindingSource, nameof(MunicipalAccountEditModel.BudgetAmount), true, DataSourceUpdateMode.OnPropertyChanged);

            chkActive.DataBindings.Add(nameof(CheckBox.Checked), _bindingSource, nameof(MunicipalAccountEditModel.IsActive), true, DataSourceUpdateMode.OnPropertyChanged);

            // SfComboBox bindings - SelectedValue binds to model property
            cmbDepartment.DataBindings.Add("SelectedValue", _bindingSource, nameof(MunicipalAccountEditModel.DepartmentId), true, DataSourceUpdateMode.OnPropertyChanged);
            cmbFund.DataBindings.Add("SelectedValue", _bindingSource, nameof(MunicipalAccountEditModel.Fund), true, DataSourceUpdateMode.OnPropertyChanged);
            cmbType.DataBindings.Add("SelectedValue", _bindingSource, nameof(MunicipalAccountEditModel.Type), true, DataSourceUpdateMode.OnPropertyChanged);
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
                try
                {
                    MessageBox.Show("An error occurred while loading account data. See logs for details.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        /// <summary>
        /// Sets up the UI layout with all controls for account editing.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();

            _toolTip = new ToolTip();
            var padding = 16;
            var labelWidth = 140;
            var controlWidth = 320;
            var rowHeight = 40;
            var y = padding;

            // Title label
            lblTitle = new Label
            {
                Text = _isNew ? "Create New Account" : "Edit Account",
                Location = new Point(padding, y),
                AutoSize = false,
                Width = labelWidth + controlWidth,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(lblTitle);
            y += 40;

            // Account Number
            var lblAccountNumber = new Label
            {
                Text = "Account Number:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblAccountNumber);

            txtAccountNumber = new TextBox
            {
                Name = "txtAccountNumber",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                MaxLength = 20,
                AccessibleName = "Account Number",
                AccessibleDescription = "Enter the unique account number",
                TabIndex = 1,
                Enabled = _isNew // Disable for editing existing accounts
            };
            Controls.Add(txtAccountNumber);
            _toolTip.SetToolTip(txtAccountNumber, "Unique identifier for this account (e.g., 1000, 2100)");
            y += rowHeight;

            // Name
            var lblName = new Label
            {
                Text = "Account Name:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblName);

            txtName = new TextBox
            {
                Name = "txtName",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                MaxLength = 100,
                AccessibleName = "Account Name",
                AccessibleDescription = "Enter the descriptive name for this account",
                TabIndex = 2
            };
            Controls.Add(txtName);
            _toolTip.SetToolTip(txtName, "Descriptive name (e.g., 'Cash - General Fund')");
            y += rowHeight;

            // Description
            var lblDescription = new Label
            {
                Text = "Description:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblDescription);

            txtDescription = new TextBox
            {
                Name = "txtDescription",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                Height = 60,
                MaxLength = 500,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AccessibleName = "Description",
                AccessibleDescription = "Enter optional description",
                TabIndex = 3
            };
            Controls.Add(txtDescription);
            _toolTip.SetToolTip(txtDescription, "Optional detailed description");
            y += 70;

            // Department
            var lblDepartment = new Label
            {
                Text = "Department:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblDepartment);

            cmbDepartment = new SfComboBox
            {
                Name = "cmbDepartment",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Department",
                AccessibleDescription = "Select the department this account belongs to",
                TabIndex = 4
            };
            cmbDepartment.ThemeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            Controls.Add(cmbDepartment);
            _toolTip.SetToolTip(cmbDepartment, "Select owning department");
            y += rowHeight;

            // Fund
            var lblFund = new Label
            {
                Text = "Fund:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblFund);

            cmbFund = new SfComboBox
            {
                Name = "cmbFund",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Fund Type",
                AccessibleDescription = "Select the municipal fund type for this account",
                TabIndex = 5
            };
            cmbFund.ThemeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            Controls.Add(cmbFund);
            _toolTip.SetToolTip(cmbFund, "Select fund type (General, Enterprise, etc.)");
            y += rowHeight;

            // Type
            var lblType = new Label
            {
                Text = "Type:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblType);

            cmbType = new SfComboBox
            {
                Name = "cmbType",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Account Type",
                AccessibleDescription = "Select the account type",
                TabIndex = 6
            };
            cmbType.ThemeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            Controls.Add(cmbType);
            _toolTip.SetToolTip(cmbType, "Select account type (Asset, Liability, Revenue, Expense)");
            y += rowHeight;

            // Balance
            var lblBalance = new Label
            {
                Text = "Current Balance:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblBalance);

            numBalance = new SfNumericTextBox
            {
                Name = "numBalance",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                AllowNull = false,
                MinValue = (double)decimal.MinValue,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Balance",
                AccessibleDescription = "Enter the current account balance",
                TabIndex = 7
            };
            numBalance.ThemeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            numBalance.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency;
            Controls.Add(numBalance);
            _toolTip.SetToolTip(numBalance, "Current account balance");
            y += rowHeight;

            // Budget
            var lblBudget = new Label
            {
                Text = "Budget Amount:",
                Location = new Point(padding, y + 6),
                Width = labelWidth,
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lblBudget);

            numBudget = new SfNumericTextBox
            {
                Name = "numBudget",
                Location = new Point(padding + labelWidth + 10, y),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = controlWidth,
                AllowNull = false,
                MinValue = 0,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Budget Amount",
                AccessibleDescription = "Enter the budgeted amount for this account",
                TabIndex = 8
            };
            numBudget.ThemeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            numBudget.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency;
            Controls.Add(numBudget);
            _toolTip.SetToolTip(numBudget, "Budgeted amount for this account");
            y += rowHeight;

            // Active checkbox
            chkActive = new CheckBoxAdv
            {
                Name = "chkActive",
                Text = "Active",
                Location = new Point(padding + labelWidth + 10, y),
                AutoSize = true,
                Checked = true,
                AccessibleName = "Active Status",
                AccessibleDescription = "Check to mark this account as active",
                TabIndex = 9
            };
            Controls.Add(chkActive);
            _toolTip.SetToolTip(chkActive, "Indicates whether this account is currently active");
            y += rowHeight + 10;

            // Button panel at bottom
            var buttonPanel = new Panel
            {
                Location = new Point(padding, y),
                Width = labelWidth + controlWidth + 10,
                Height = 40,
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnSave = new SfButton
            {
                Name = "btnSave",
                Text = _isNew ? "&Create" : "&Save",
                Width = 100,
                Height = 32,
                Location = new Point(labelWidth + controlWidth - 210, 4),
                AccessibleName = _isNew ? "Create Account" : "Save Account",
                AccessibleDescription = "Save the account changes",
                TabIndex = 10
            };
            btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.Click += BtnSave_Click;
            buttonPanel.Controls.Add(btnSave);

            btnCancel = new SfButton
            {
                Name = "btnCancel",
                Text = "&Cancel",
                Width = 100,
                Height = 32,
                Location = new Point(labelWidth + controlWidth - 100, 4),
                AccessibleName = "Cancel",
                AccessibleDescription = "Cancel and discard changes",
                TabIndex = 11
            };
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Click += BtnCancel_Click;
            buttonPanel.Controls.Add(btnCancel);

            Controls.Add(buttonPanel);

            ResumeLayout(false);
            PerformLayout();
            Refresh();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            Cancel();
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

                // Map edit model properties to their corresponding controls for validation feedback
                _errorBinding.MapControl(nameof(_editModel.AccountNumber), txtAccountNumber);
                _errorBinding.MapControl(nameof(_editModel.Name), txtName);
                _errorBinding.MapControl(nameof(_editModel.DepartmentId), cmbDepartment);
                _errorBinding.MapControl(nameof(_editModel.Balance), numBalance);
                _errorBinding.MapControl(nameof(_editModel.BudgetAmount), numBudget);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountEditPanel: Failed to setup validation");
            }
        }

        /// <summary>
        /// Load supporting data like departments and populate dropdowns.
        /// </summary>
        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
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
                    MessageBox.Show("Some dropdowns may have limited options due to a data load error. See logs for details.",
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
            // Disable save button and show busy cursor to prevent re-entrancy
            var prevCursor = Cursor.Current;
            try
            {
                btnSave.Enabled = false;
                Cursor.Current = Cursors.WaitCursor;

                Serilog.Log.Information("AccountEditPanel: Save button clicked");

                if (_errorBinding != null)
                {
                    var errors = GetValidationErrors();
                    if (errors.Any())
                    {
                        Dialogs.ValidationDialog.Show(this, "Validation Error", "Account data validation failed:", errors, null);
                        return;
                    }
                }

                // Pre-save uniqueness check (client-side quick check)
                if (_isNew && !string.IsNullOrWhiteSpace(_editModel.AccountNumber) && _viewModel.Accounts != null
                    && _viewModel.Accounts.Any(a => string.Equals(a.AccountNumber, _editModel.AccountNumber, StringComparison.OrdinalIgnoreCase)))
                {
                    Dialogs.ValidationDialog.Show(this, "Validation Error", "Duplicate Account Number",
                        new[] { $"An account with number '{_editModel.AccountNumber}' already exists." }, null);
                    return;
                }

                // Convert edit model to entity
                var account = _editModel.ToEntity();

                // If editing, preserve the Id
                if (_existingAccount != null)
                {
                    account.Id = _existingAccount.Id;
                }

                // Save via view model command (viewmodel performs server-side validation/uniqueness)
                if (_isNew)
                {
                    await _viewModel.CreateAccountCommand.ExecuteAsync(account);
                }
                else
                {
                    await _viewModel.UpdateAccountCommand.ExecuteAsync(account);
                }

                // Check for errors reported by the viewmodel
                if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                {
                    try { MessageBox.Show(_viewModel.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    return;
                }

                SaveDialogResult = DialogResult.OK;

                // Set parent form result and close
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
                    MessageBox.Show("An error occurred while saving the account. See logs for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
            finally
            {
                try { btnSave.Enabled = true; } catch { }
                Cursor.Current = prevCursor;
            }
        }

        private System.Collections.Generic.IList<string> GetValidationErrors()
        {
            return _editModel.GetAllErrors();
        }

        /// <summary>
        /// Cancels the edit operation and closes the panel.
        /// </summary>
        private void Cancel()
        {
            SaveDialogResult = DialogResult.Cancel;
            var parent = this.FindForm();
            if (parent != null)
            {
                parent.DialogResult = DialogResult.Cancel;
                parent.Close();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _errorBinding?.Dispose();
                _toolTip?.Dispose();
                _errorProvider?.Dispose();
                _bindingSource?.Dispose();

                // Unbind if needed (DataBindings usually cleaned up automatically)
            }
            base.Dispose(disposing);
        }
    }
}
