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

        // UI Controls
        private Label lblTitle = null!;
        private TextBox txtAccountNumber = null!;
        private TextBox txtName = null!;
        private TextBox txtDescription = null!;
        private NumericUpDown numBalance = null!;
        private NumericUpDown numBudget = null!;
        private CheckBox chkActive = null!;
        private Button btnSave = null!;
        private ComboBox cmbDepartment = null!;
        private ComboBox cmbFund = null!;
        private ComboBox cmbType = null!;
        private TableLayoutPanel _mainLayout = null!;

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

            // Apply theme via SfSkinManager (single source of truth)
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, "Office2019Colorful"); } catch { }
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
                numBalance.Value = _existingAccount.Balance;
                numBudget.Value = _existingAccount.BudgetAmount;
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
                numBalance.ValueChanged += (s, e) => _editModel.Balance = numBalance.Value;
                numBudget.ValueChanged += (s, e) => _editModel.BudgetAmount = numBudget.Value;
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
                _editModel.Balance = numBalance.Value;
                _editModel.BudgetAmount = numBudget.Value;
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

        /// <summary>
        /// Initializes the UI components for the account edit panel.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Main layout panel
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(12),
                AutoSize = true
            };

            // Configure columns (labels and controls)
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // Label column
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Control column

            // Configure rows
            for (int i = 0; i < 10; i++)
            {
                _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            // Title label (spans both columns)
            lblTitle = new Label
            {
                Text = "Account Details",
                Font = new Font(this.Font.FontFamily, 12F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 12),
                AccessibleName = "Account Details Header"
            };
            _mainLayout.Controls.Add(lblTitle, 0, 0);
            _mainLayout.SetColumnSpan(lblTitle, 2);

            // Account Number
            var lblAccountNumber = new Label
            {
                Text = "Account Number:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Account Number Label"
            };
            _mainLayout.Controls.Add(lblAccountNumber, 0, 1);

            txtAccountNumber = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                AccessibleName = "Account Number",
                AccessibleDescription = "Enter the account number"
            };
            _mainLayout.Controls.Add(txtAccountNumber, 1, 1);

            // Name
            var lblName = new Label
            {
                Text = "Name:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Account Name Label"
            };
            _mainLayout.Controls.Add(lblName, 0, 2);

            txtName = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                AccessibleName = "Account Name",
                AccessibleDescription = "Enter the account name"
            };
            _mainLayout.Controls.Add(txtName, 1, 2);

            // Description
            var lblDescription = new Label
            {
                Text = "Description:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Description Label"
            };
            _mainLayout.Controls.Add(lblDescription, 0, 3);

            txtDescription = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                AccessibleName = "Account Description",
                AccessibleDescription = "Enter the account description"
            };
            _mainLayout.Controls.Add(txtDescription, 1, 3);

            // Department
            var lblDepartment = new Label
            {
                Text = "Department:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Department Label"
            };
            _mainLayout.Controls.Add(lblDepartment, 0, 4);

            cmbDepartment = new ComboBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "Department Selection",
                AccessibleDescription = "Select the department for this account"
            };
            _mainLayout.Controls.Add(cmbDepartment, 1, 4);

            // Fund
            var lblFund = new Label
            {
                Text = "Fund:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Fund Label"
            };
            _mainLayout.Controls.Add(lblFund, 0, 5);

            cmbFund = new ComboBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "Fund Selection",
                AccessibleDescription = "Select the fund type for this account"
            };
            _mainLayout.Controls.Add(cmbFund, 1, 5);

            // Type
            var lblType = new Label
            {
                Text = "Type:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Account Type Label"
            };
            _mainLayout.Controls.Add(lblType, 0, 6);

            cmbType = new ComboBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "Account Type Selection",
                AccessibleDescription = "Select the account type"
            };
            _mainLayout.Controls.Add(cmbType, 1, 6);

            // Balance
            var lblBalance = new Label
            {
                Text = "Balance:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Balance Label"
            };
            _mainLayout.Controls.Add(lblBalance, 0, 7);

            numBalance = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                DecimalPlaces = 2,
                Minimum = decimal.MinValue,
                Maximum = decimal.MaxValue,
                AccessibleName = "Account Balance",
                AccessibleDescription = "Enter the current account balance"
            };
            _mainLayout.Controls.Add(numBalance, 1, 7);

            // Budget
            var lblBudget = new Label
            {
                Text = "Budget:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 6, 3),
                AccessibleName = "Budget Label"
            };
            _mainLayout.Controls.Add(lblBudget, 0, 8);

            numBudget = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                DecimalPlaces = 2,
                Minimum = decimal.MinValue,
                Maximum = decimal.MaxValue,
                AccessibleName = "Budget Amount",
                AccessibleDescription = "Enter the budget amount for this account"
            };
            _mainLayout.Controls.Add(numBudget, 1, 8);

            // Active checkbox and Save button (spans both columns)
            var buttonPanel = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 12, 0, 0),
                AutoSize = true
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            chkActive = new CheckBox
            {
                Text = "Active",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 12, 3),
                Checked = true,
                AccessibleName = "Account Active Status",
                AccessibleDescription = "Check to mark this account as active"
            };
            buttonPanel.Controls.Add(chkActive, 0, 0);

            btnSave = new Button
            {
                Text = "Save",
                Dock = DockStyle.Right,
                Width = 100,
                Margin = new Padding(0, 3, 0, 3),
                AccessibleName = "Save Account",
                AccessibleDescription = "Save the account changes"
            };
            btnSave.Click += BtnSave_Click;
            buttonPanel.Controls.Add(btnSave, 1, 0);

            _mainLayout.Controls.Add(buttonPanel, 0, 9);
            _mainLayout.SetColumnSpan(buttonPanel, 2);

            // Add main layout to the control
            this.Controls.Add(_mainLayout);

            // Configure the form
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.MinimumSize = new Size(500, 400);
            this.AccessibleName = "Account Edit Panel";
            this.AccessibleDescription = "Panel for creating or editing municipal accounts";

            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
