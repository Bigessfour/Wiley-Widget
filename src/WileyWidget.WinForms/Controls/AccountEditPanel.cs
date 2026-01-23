using System.Threading;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using WileyWidget.WinForms.Controls;
using Action = System.Action;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Panel for creating or editing municipal accounts with full validation, data binding, and MVVM support.
    /// Inherits from ScopedPanelBase to support proper DI and ICompletablePanel lifecycle.
    ///
    /// ARCHITECTURE:
    /// - Theme: 100% delegated to SfSkinManager (no manual colors, no Font assignments)
    /// - Layout: TableLayoutPanel for responsive resize support
    /// - MVVM: BindingSource → _editModel, commands via ViewModel
    /// - Validation: ErrorProvider with field mapping, cross-thread safe
    /// - Lifecycle: Proper Dispose cleanup, event handler tracking, IsBusy/HasUnsavedChanges
    /// </summary>
    public partial class AccountEditPanel : ScopedPanelBase<AccountsViewModel>
    {
        /// <summary>
        /// Gets the dialog result after save/cancel operations.
        /// </summary>
        public DialogResult SaveDialogResult { get; private set; } = DialogResult.None;

        // === UI CONTROLS ===
        private Label? lblTitle = null;
        private Label? lblAccountNumber = null;
        private TextBoxExt txtAccountNumber = null!;
        private Label? lblName = null;
        private TextBoxExt txtName = null!;
        private Label? lblDescription = null;
        private TextBoxExt txtDescription = null!;
        private Label? lblDepartment = null;
        private SfComboBox cmbDepartment = null!;
        private Label? lblFund = null;
        private SfComboBox cmbFund = null!;
        private Label? lblType = null;
        private SfComboBox cmbType = null!;
        private Label? lblBalance = null;
        private SfNumericTextBox numBalance = null!;
        private Label? lblBudget = null;
        private SfNumericTextBox numBudget = null!;
        private Label? lblActive = null;
        private CheckBoxAdv chkActive = null!;
        private SfButton btnSave = null!;
        private SfButton btnCancel = null!;
        private TableLayoutPanel _mainLayout = null!;
        private Panel _buttonPanel = null!;

        // === DATA MANAGEMENT ===
        private MunicipalAccountEditModel _editModel = null!;
        private readonly MunicipalAccount? _existingAccount;
        private ErrorProvider? _errorProvider;
        private ErrorProviderBinding? _errorBinding;
        private ToolTip? _toolTip;
        private BindingSource _bindingSource = null!;
        private bool _isNew;

        // === EVENT HANDLER STORAGE FOR CLEANUP ===
        private EventHandler? _saveHandler;
        private EventHandler? _cancelHandler;
        private BindingCompleteEventHandler? _bindingCompleteHandler;

        // === CURRENCY FORMAT ===
        private static readonly NumberFormatInfo CurrencyFormat = new NumberFormatInfo
        {
            CurrencySymbol = "$",
            CurrencyDecimalDigits = 2,
            NumberGroupSizes = new[] { 3 },
            NumberGroupSeparator = ",",
            CurrencyDecimalSeparator = ".",
            NegativeSign = "-"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountEditPanel"/> class.
        /// For new account creation, use the DI constructor.
        /// For editing, create via DI and call SetExistingAccount() to configure.
        /// </summary>
        public AccountEditPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<AccountsViewModel>> logger)
            : base(scopeFactory, logger)
        {
            _existingAccount = null;
            _isNew = true;
            _editModel = new MunicipalAccountEditModel(null);
            _bindingSource = new BindingSource { DataSource = _editModel };
            InitializeComponent();
        }

        /// <summary>
        /// Configures the panel for editing an existing account.
        /// Call after construction when editing.
        /// </summary>
        public void SetExistingAccount(MunicipalAccount existingAccount)
        {
            _isNew = false;
            _editModel = new MunicipalAccountEditModel(existingAccount);
            _bindingSource.DataSource = _editModel;
        }

        /// <summary>
        /// Gets the data context for MVVM binding.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        public new object? DataContext { get; private set; }

        private void BindControls()
        {
            // Bind controls to _editModel properties (MVVM style)
            txtAccountNumber.DataBindings.Add(
                nameof(TextBoxExt.Text),
                _bindingSource,
                nameof(MunicipalAccountEditModel.AccountNumber),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            txtName.DataBindings.Add(
                nameof(TextBoxExt.Text),
                _bindingSource,
                nameof(MunicipalAccountEditModel.Name),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            txtDescription.DataBindings.Add(
                nameof(TextBoxExt.Text),
                _bindingSource,
                nameof(MunicipalAccountEditModel.Description),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            // SfNumericTextBox uses Value property (double?)
            numBalance.DataBindings.Add(
                "Value",
                _bindingSource,
                nameof(MunicipalAccountEditModel.Balance),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            numBudget.DataBindings.Add(
                "Value",
                _bindingSource,
                nameof(MunicipalAccountEditModel.BudgetAmount),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            chkActive.DataBindings.Add(
                nameof(CheckBoxAdv.Checked),
                _bindingSource,
                nameof(MunicipalAccountEditModel.IsActive),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            // SfComboBox bindings - SelectedValue binds to model property
            cmbDepartment.DataBindings.Add(
                "SelectedValue",
                _bindingSource,
                nameof(MunicipalAccountEditModel.DepartmentId),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            cmbFund.DataBindings.Add(
                "SelectedValue",
                _bindingSource,
                nameof(MunicipalAccountEditModel.Fund),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            cmbType.DataBindings.Add(
                "SelectedValue",
                _bindingSource,
                nameof(MunicipalAccountEditModel.Type),
                true,
                DataSourceUpdateMode.OnPropertyChanged);

            // Wire binding complete to track unsaved changes for ICompletablePanel
            _bindingCompleteHandler = (s, e) => SetHasUnsavedChanges(true);
            _bindingSource.BindingComplete += _bindingCompleteHandler;
        }

        /// <summary>
        /// Implements ICompletablePanel lifecycle: ValidateAsync
        /// Integrates with ErrorProvider to show visual feedback.
        /// Thread-safe: ErrorProvider operations execute on UI thread.
        /// </summary>
        public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            // Clear all existing error indicators first
            if (_errorProvider != null)
            {
                // Safe access to UI controls from validation context
                if (InvokeRequired)
                {
                    Invoke(() => ClearErrorIndicators());
                }
                else
                {
                    ClearErrorIndicators();
                }
            }

            var errors = GetValidationErrors();
            if (errors.Any())
            {
                // Map errors to controls via ErrorProvider for visual feedback
                if (_errorProvider != null)
                {
                    var controlMap = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "AccountNumber", txtAccountNumber },
                        { "Name", txtName },
                        { "Description", txtDescription },
                        { "DepartmentId", cmbDepartment },
                        { "Fund", cmbFund },
                        { "Type", cmbType },
                        { "Balance", numBalance },
                        { "BudgetAmount", numBudget }
                    };

                    var errorAction = new Action(() =>
                    {
                        foreach (var error in errors)
                        {
                            // Extract field name from error message
                            var fieldName = controlMap.Keys
                                .FirstOrDefault(k => error.Contains(k, StringComparison.OrdinalIgnoreCase));

                            if (fieldName != null && controlMap.TryGetValue(fieldName, out var control))
                            {
                                _errorProvider.SetError(control, error);
                            }
                        }
                    });

                    if (InvokeRequired)
                    {
                        Invoke(errorAction);
                    }
                    else
                    {
                        errorAction();
                    }
                }

                var validationItems = errors
                    .Select(e => new ValidationItem("Account", e, ValidationSeverity.Error))
                    .ToList();

                return ValidationResult.Failed(validationItems.ToArray());
            }

            return ValidationResult.Success;
        }

        /// <summary>
        /// Clear all ErrorProvider error indicators.
        /// </summary>
        private void ClearErrorIndicators()
        {
            if (_errorProvider == null) return;

            foreach (Control control in Controls)
            {
                _errorProvider.SetError(control, string.Empty);
            }

            // Recursively clear nested controls
            ClearErrorsRecursive(control: this, provider: _errorProvider);
        }

        private static void ClearErrorsRecursive(Control control, ErrorProvider provider)
        {
            foreach (Control child in control.Controls)
            {
                provider.SetError(child, string.Empty);
                ClearErrorsRecursive(child, provider);
            }
        }

        /// <summary>
        /// Implements ICompletablePanel lifecycle: LoadAsync
        /// </summary>
        public override async Task LoadAsync(CancellationToken ct)
        {
            DataContext = _editModel;
            BindControls();
            SetupValidation();
            await LoadDataAsync(ct);
        }

        /// <summary>
        /// Implements ICompletablePanel lifecycle: SaveAsync
        /// </summary>
        public override async Task SaveAsync(CancellationToken ct)
        {
            await BtnSave_ClickAsync();
        }

        /// <summary>
        /// Sets up the UI layout with all controls for account editing.
        /// Uses TableLayoutPanel for responsive resizing and proper layout management.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();

            // === APPLY THEME IMMEDIATELY ===
            // This ensures all controls inherit the theme automatically via cascade
            var themeName = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            try
            {
                SfSkinManager.SetVisualStyle(this, themeName);
                Logger?.LogDebug("Applied theme {ThemeName} to AccountEditPanel", themeName);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to apply theme {ThemeName}", themeName);
            }

            _toolTip = new ToolTip();
            this.Padding = new Padding(16);
            this.AutoScroll = true;

            // === CREATE MAIN TABLE LAYOUT ===
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = false,
                Padding = new Padding(16)
            };

            // Set column widths: label (140px) + control (320px) + padding
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int rowIndex = 0;

            // === TITLE ROW ===
            lblTitle = new Label
            {
                Text = _isNew ? "Create New Account" : "Edit Account",
                AutoSize = false,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                // Theme cascade handles Font/Color - do NOT override
            };
            _mainLayout.Controls.Add(lblTitle, 0, rowIndex);
            _mainLayout.SetColumnSpan(lblTitle, 2);
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            rowIndex++;

            // === ACCOUNT NUMBER ===
            lblAccountNumber = new Label
            {
                Text = "Account Number:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblAccountNumber, 0, rowIndex);

            txtAccountNumber = new TextBoxExt
            {
                Name = "txtAccountNumber",
                MaxLength = 20,
                AccessibleName = "Account Number",
                AccessibleDescription = "Enter the unique account number",
                Enabled = _isNew,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 1,
                ThemeName = themeName
            };
            _mainLayout.Controls.Add(txtAccountNumber, 1, rowIndex);
            _toolTip.SetToolTip(txtAccountNumber, "Unique identifier for this account (e.g., 1000, 2100)");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === NAME ===
            lblName = new Label
            {
                Text = "Account Name:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblName, 0, rowIndex);

            txtName = new TextBoxExt
            {
                Name = "txtName",
                MaxLength = 100,
                AccessibleName = "Account Name",
                AccessibleDescription = "Enter the descriptive name for this account",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 2,
                ThemeName = themeName
            };
            _mainLayout.Controls.Add(txtName, 1, rowIndex);
            _toolTip.SetToolTip(txtName, "Descriptive name (e.g., 'Cash - General Fund')");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === DESCRIPTION (multiline) ===
            lblDescription = new Label
            {
                Text = "Description:",
                TextAlign = ContentAlignment.TopRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblDescription, 0, rowIndex);

            txtDescription = new TextBoxExt
            {
                Name = "txtDescription",
                MaxLength = 500,
                AccessibleName = "Description",
                AccessibleDescription = "Enter optional description",
                Multiline = true,
                Height = 80,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 3,
                ThemeName = themeName
            };
            _mainLayout.Controls.Add(txtDescription, 1, rowIndex);
            _toolTip.SetToolTip(txtDescription, "Optional detailed description");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
            rowIndex++;

            // === DEPARTMENT ===
            lblDepartment = new Label
            {
                Text = "Department:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblDepartment, 0, rowIndex);

            cmbDepartment = new SfComboBox
            {
                Name = "cmbDepartment",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Department",
                AccessibleDescription = "Select the department this account belongs to",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 4,
                ThemeName = themeName
            };
            _mainLayout.Controls.Add(cmbDepartment, 1, rowIndex);
            _toolTip.SetToolTip(cmbDepartment, "Select owning department");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === FUND ===
            lblFund = new Label
            {
                Text = "Fund:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblFund, 0, rowIndex);

            cmbFund = new SfComboBox
            {
                Name = "cmbFund",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Fund Type",
                AccessibleDescription = "Select the municipal fund type for this account",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 5,
                ThemeName = themeName
            };
            _mainLayout.Controls.Add(cmbFund, 1, rowIndex);
            _toolTip.SetToolTip(cmbFund, "Select fund type (General, Enterprise, etc.)");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === TYPE ===
            lblType = new Label
            {
                Text = "Type:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblType, 0, rowIndex);

            cmbType = new SfComboBox
            {
                Name = "cmbType",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AccessibleName = "Account Type",
                AccessibleDescription = "Select the account type",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 6,
                ThemeName = themeName
            };
            _mainLayout.Controls.Add(cmbType, 1, rowIndex);
            _toolTip.SetToolTip(cmbType, "Select account type (Asset, Liability, Revenue, Expense)");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === BALANCE (Currency) ===
            lblBalance = new Label
            {
                Text = "Current Balance:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblBalance, 0, rowIndex);

            numBalance = new SfNumericTextBox
            {
                Name = "numBalance",
                AllowNull = false,
                MinValue = (double)decimal.MinValue,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Balance",
                AccessibleDescription = "Enter the current account balance",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 7,
                ThemeName = themeName,
                FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency,
                NumberFormatInfo = CurrencyFormat
            };
            _mainLayout.Controls.Add(numBalance, 1, rowIndex);
            _toolTip.SetToolTip(numBalance, "Current account balance");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === BUDGET (Currency) ===
            lblBudget = new Label
            {
                Text = "Budget Amount:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5)
            };
            _mainLayout.Controls.Add(lblBudget, 0, rowIndex);

            numBudget = new SfNumericTextBox
            {
                Name = "numBudget",
                AllowNull = false,
                MinValue = 0,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Budget Amount",
                AccessibleDescription = "Enter the budgeted amount for this account",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 8,
                ThemeName = themeName,
                FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency,
                NumberFormatInfo = CurrencyFormat
            };
            _mainLayout.Controls.Add(numBudget, 1, rowIndex);
            _toolTip.SetToolTip(numBudget, "Budgeted amount for this account");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === ACTIVE CHECKBOX ===
            lblActive = new Label
            {
                Text = " ",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            _mainLayout.Controls.Add(lblActive, 0, rowIndex);

            chkActive = new CheckBoxAdv
            {
                Name = "chkActive",
                Text = "Active",
                Checked = true,
                AutoSize = true,
                AccessibleName = "Active Status",
                AccessibleDescription = "Check to mark this account as active",
                Margin = new Padding(0, 5, 0, 5),
                TabIndex = 9,
                ThemeName = themeName
            };
            _mainLayout.Controls.Add(chkActive, 1, rowIndex);
            _toolTip.SetToolTip(chkActive, "Indicates whether this account is currently active");
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rowIndex++;

            // === BUTTON PANEL (stretches to bottom) ===
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(0, 10, 0, 0)
            };

            btnSave = new SfButton
            {
                Name = "btnSave",
                Text = _isNew ? "&Create" : "&Save",
                Width = 100,
                Height = 32,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = _isNew ? "Create Account" : "Save Account",
                AccessibleDescription = "Save the account changes",
                TabIndex = 10,
                ThemeName = themeName,
                Margin = new Padding(0, 0, 10, 0)
            };
            _saveHandler = BtnSave_Click;
            btnSave.Click += _saveHandler;
            _buttonPanel.Controls.Add(btnSave);

            btnCancel = new SfButton
            {
                Name = "btnCancel",
                Text = "&Cancel",
                Width = 100,
                Height = 32,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = "Cancel",
                AccessibleDescription = "Cancel and discard changes",
                TabIndex = 11,
                ThemeName = themeName,
                Margin = new Padding(0)
            };
            _cancelHandler = BtnCancel_Click;
            btnCancel.Click += _cancelHandler;
            _buttonPanel.Controls.Add(btnCancel);

            Controls.Add(_mainLayout);
            Controls.Add(_buttonPanel);

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

                Logger?.LogDebug("AccountEditPanel: Validation setup complete");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "AccountEditPanel: Failed to setup validation");
            }
        }

        /// <summary>
        /// Load supporting data like departments and populate dropdowns.
        /// </summary>
        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger?.LogDebug("AccountEditPanel: Loading departments and fund/type data");

                if (ViewModel == null)
                {
                    Logger?.LogWarning("AccountEditPanel: ViewModel is null, cannot load data");
                    return;
                }

                // Load departments
                var depts = await ViewModel.GetDepartmentsAsync();
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
                    Logger?.LogWarning("AccountEditPanel: Using sample departments (repository returned no data)");
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

                Logger?.LogDebug("AccountEditPanel: Data loaded successfully");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "AccountEditPanel.LoadDataAsync failed");
                try
                {
                    MessageBox.Show(
                        "Some dropdowns may have limited options due to a data load error. See logs for details.",
                        "Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                catch { }
            }
        }

        /// <summary>
        /// Handles the Save button click to validate and save the account.
        /// </summary>
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            BeginInvoke(new Func<Task>(BtnSave_ClickAsync));
        }

        /// <summary>
        /// Executes save operation with proper state management and error handling.
        /// Updates ICompletablePanel state (IsBusy, HasUnsavedChanges) throughout lifecycle.
        /// </summary>
        private async Task BtnSave_ClickAsync()
        {
            try
            {
                // Set busy state - automatically disables UI via base class
                IsBusy = true;
                var ct = RegisterOperation(); // Get cancellation token

                Logger?.LogInformation("AccountEditPanel: Save initiated (Mode: {Mode})", _isNew ? "Create" : "Edit");

                // Run validation via ICompletablePanel interface
                var validationResult = await ValidateAsync(ct);
                if (!validationResult.IsValid)
                {
                    Dialogs.ValidationDialog.Show(
                        this,
                        "Validation Error",
                        "Account data validation failed:",
                        validationResult.Errors.Select(e => e.Message).ToArray(),
                        null);
                    return;
                }

                if (ViewModel == null)
                {
                    MessageBox.Show(
                        "ViewModel not available. Cannot save.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Pre-save uniqueness check (client-side quick check)
                if (_isNew && !string.IsNullOrWhiteSpace(_editModel.AccountNumber)
                    && ViewModel.Accounts != null
                    && ViewModel.Accounts.Any(a =>
                        string.Equals(a.AccountNumber, _editModel.AccountNumber, StringComparison.OrdinalIgnoreCase)))
                {
                    Dialogs.ValidationDialog.Show(
                        this,
                        "Validation Error",
                        "Duplicate Account Number",
                        new[] { $"An account with number '{_editModel.AccountNumber}' already exists." },
                        null);
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
                    await ViewModel.CreateAccountCommand.ExecuteAsync(account);
                }
                else
                {
                    await ViewModel.UpdateAccountCommand.ExecuteAsync(account);
                }

                // Check for errors reported by the viewmodel
                if (!string.IsNullOrEmpty(ViewModel.ErrorMessage))
                {
                    MessageBox.Show(ViewModel.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Clear unsaved changes flag after successful save
                SetHasUnsavedChanges(false);
                SaveDialogResult = DialogResult.OK;

                // Set parent form result and close
                var parent = this.FindForm();
                if (parent != null)
                {
                    parent.DialogResult = DialogResult.OK;
                    parent.Close();
                }

                Logger?.LogInformation("AccountEditPanel: Account saved successfully - {AccountNumber}", _editModel.AccountNumber);
            }
            catch (OperationCanceledException)
            {
                Logger?.LogDebug("AccountEditPanel: Save operation cancelled");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "AccountEditPanel: Save operation failed");
                MessageBox.Show(
                    "An error occurred while saving the account. See logs for details.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // IsBusy automatically re-enables UI
                IsBusy = false;
            }
        }

        /// <summary>
        /// Gets all validation errors from the edit model.
        /// </summary>
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

        /// <summary>
        /// Focuses the first control that has a validation error.
        /// Implements ICompletablePanel.FocusFirstError pattern.
        /// </summary>
        public override void FocusFirstError()
        {
            // Try to find a control with an ErrorProvider error set
            var errorControl = Controls
                .Cast<Control>()
                .FirstOrDefault(c => _errorProvider != null && _errorProvider.GetError(c).Length > 0);

            if (errorControl != null)
            {
                errorControl.Focus();
            }
            else if (txtAccountNumber != null)
            {
                // Default to first editable control if no errors set
                txtAccountNumber.Focus();
            }
        }

        /// <summary>
        /// Properly disposes all resources including event handlers, ErrorProvider, and bindings.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Detach all event handlers
                if (_saveHandler != null && btnSave != null)
                {
                    btnSave.Click -= _saveHandler;
                }

                if (_cancelHandler != null && btnCancel != null)
                {
                    btnCancel.Click -= _cancelHandler;
                }

                if (_bindingCompleteHandler != null && _bindingSource != null)
                {
                    _bindingSource.BindingComplete -= _bindingCompleteHandler;
                }

                // Clear all data bindings
                if (txtAccountNumber != null) txtAccountNumber.DataBindings.Clear();
                if (txtName != null) txtName.DataBindings.Clear();
                if (txtDescription != null) txtDescription.DataBindings.Clear();
                if (numBalance != null) numBalance.DataBindings.Clear();
                if (numBudget != null) numBudget.DataBindings.Clear();
                if (chkActive != null) chkActive.DataBindings.Clear();
                if (cmbDepartment != null) cmbDepartment.DataBindings.Clear();
                if (cmbFund != null) cmbFund.DataBindings.Clear();
                if (cmbType != null) cmbType.DataBindings.Clear();

                // Dispose resources
                _errorBinding?.Dispose();
                _toolTip?.Dispose();
                _errorProvider?.Dispose();
                _bindingSource?.Dispose();
                _mainLayout?.Dispose();
                _buttonPanel?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
