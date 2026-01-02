using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Extensions;
using Syncfusion.WinForms.Themes;

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

        private TextBox txtAccountNumber = null!;
        private TextBox txtName = null!;
        private TextBox txtDescription = null!;
        private SfComboBox cmbDepartment = null!;
        private SfComboBox cmbFund = null!;
        private SfComboBox cmbType = null!;
        private SfNumericTextBox numBalance = null!;
        private SfNumericTextBox numBudget = null!;
        private CheckBox chkActive = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;
        private EventHandler<AppTheme>? _themeChangedHandler;

        private readonly AccountsViewModel _viewModel;
        private readonly MunicipalAccountEditModel _editModel;
        private readonly MunicipalAccount? _existingAccount;
        private ErrorProvider? _errorProvider;
        private ErrorProviderBinding? _errorBinding;
        private ToolTip? _toolTip;

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
            SetupUI(existingAccount);
            SetupValidation();

            // Apply centralized theming and subscribe to theme changes
            _themeChangedHandler = OnThemeChanged;
            ThemeManager.ThemeChanged += _themeChangedHandler;
            ThemeManager.ApplyThemeToControl(this);

            // Load data asynchronously on load event
            this.Load += AccountEditPanel_Load;
        }

        private void InitializeComponent()
        {
            Name = "AccountEditPanel";
            AccessibleName = _isNew ? "Create Account" : "Edit Account";
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(520, 580);
            Padding = new Padding(16);
            // BackColor removed - let SkinManager handle theming

            _toolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 200,
                ShowAlways = true
            };
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
        /// Sets up the UI layout with all controls for account editing.
        /// </summary>
        /// <param name="existing">Existing account data to populate, or null for new account.</param>
        private void SetupUI(MunicipalAccount? existing)
        {
            var padding = 16;
            var labelWidth = 140;
            var controlWidth = 320;
            var rowHeight = 40;
            var y = padding;

            // Title label
            var lblTitle = new Label
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
                Width = controlWidth,
                MaxLength = 20,
                AccessibleName = "Account Number",
                AccessibleDescription = "Enter the unique account number",
                TabIndex = 1,
                Enabled = _isNew // Disable for editing existing accounts
            };
            Controls.Add(txtAccountNumber);
            _toolTip?.SetToolTip(txtAccountNumber, "Unique identifier for this account (e.g., 1000, 2100)");
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
                Width = controlWidth,
                MaxLength = 100,
                AccessibleName = "Account Name",
                AccessibleDescription = "Enter the descriptive name for this account",
                TabIndex = 2
            };
            Controls.Add(txtName);
            _toolTip?.SetToolTip(txtName, "Descriptive name (e.g., 'Cash - General Fund')");
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
            _toolTip?.SetToolTip(txtDescription, "Optional detailed description");
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
                Width = controlWidth,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Department",
                AccessibleDescription = "Select the department this account belongs to",
                TabIndex = 4
            };
            Controls.Add(cmbDepartment);
            _toolTip?.SetToolTip(cmbDepartment, "Select owning department");
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
                Width = controlWidth,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Fund Type",
                AccessibleDescription = "Select the municipal fund type for this account",
                TabIndex = 5
            };
            Controls.Add(cmbFund);
            _toolTip?.SetToolTip(cmbFund, "Select fund type (General, Enterprise, etc.)");
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
                Width = controlWidth,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Account Type",
                AccessibleDescription = "Select the account type",
                TabIndex = 6
            };
            Controls.Add(cmbType);
            _toolTip?.SetToolTip(cmbType, "Select account type (Asset, Liability, Revenue, Expense)");
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
                Width = controlWidth,
                AllowNull = false,
                MinValue = (double)decimal.MinValue,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Balance",
                AccessibleDescription = "Enter the current account balance",
                TabIndex = 7
            };
            numBalance.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency;
            Controls.Add(numBalance);
            _toolTip?.SetToolTip(numBalance, "Current account balance");
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
                Width = controlWidth,
                AllowNull = false,
                MinValue = 0,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Budget Amount",
                AccessibleDescription = "Enter the budgeted amount for this account",
                TabIndex = 8
            };
            numBudget.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency;
            Controls.Add(numBudget);
            _toolTip?.SetToolTip(numBudget, "Budgeted amount for this account");
            y += rowHeight;

            // Active checkbox
            chkActive = new CheckBox
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
            _toolTip?.SetToolTip(chkActive, "Indicates whether this account is currently active");
            y += rowHeight + 10;

            // Button panel at bottom
            var buttonPanel = new Panel
            {
                Location = new Point(padding, y),
                Width = labelWidth + controlWidth + 10,
                Height = 40,
                Dock = DockStyle.None
            };

            btnSave = new Button
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
            btnSave.Click += BtnSave_Click;
            buttonPanel.Controls.Add(btnSave);

            btnCancel = new Button
            {
                Name = "btnCancel",
                Text = "&Cancel",
                Width = 100,
                Height = 32,
                Location = new Point(labelWidth + controlWidth - 100, 4),
                DialogResult = DialogResult.Cancel,
                AccessibleName = "Cancel",
                AccessibleDescription = "Cancel and discard changes",
                TabIndex = 11
            };
            btnCancel.Click += (s, e) => Cancel();
            buttonPanel.Controls.Add(btnCancel);

            Controls.Add(buttonPanel);

            // Populate initial values if editing existing account
            if (existing != null)
            {
                txtAccountNumber.Text = existing.AccountNumber?.Value ?? "";
                txtName.Text = existing.Name ?? "";
                txtDescription.Text = existing.FundDescription ?? "";
                numBalance.Value = (double)existing.Balance;
                numBudget.Value = (double)existing.BudgetAmount;
                chkActive.Checked = existing.IsActive;
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
                ThemeManager.ApplyThemeToControl(this);
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
                    try { BeginInvoke(new Action(() => OnThemeChanged(sender, theme))); } catch { }
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
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
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
                try { if (_themeChangedHandler != null) ThemeManager.ThemeChanged -= _themeChangedHandler; } catch { }
                try { this.Load -= AccountEditPanel_Load; } catch { }
                _errorBinding?.Dispose();
                _errorProvider?.Dispose();
                _toolTip?.Dispose();
                txtAccountNumber?.Dispose();
                txtName?.Dispose();
                txtDescription?.Dispose();
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
    }
}
