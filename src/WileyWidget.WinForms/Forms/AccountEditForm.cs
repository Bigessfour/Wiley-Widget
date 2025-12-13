using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Dialogs;
using System.Text.RegularExpressions;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Modal form for creating and editing MunicipalAccount entities.
    /// Follows MVVM pattern with service-layer validation.
    /// </summary>
    public sealed class AccountEditForm : Form
    {
        private readonly IAccountService _accountService;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly ILogger<AccountEditForm> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MunicipalAccount? _existingAccount;
        private readonly bool _isEditMode;

        private CancellationTokenSource? _cts;

        // Form controls (Syncfusion)
        private Syncfusion.Windows.Forms.Tools.TextBoxExt? _accountNumberTextBox;
        private Syncfusion.Windows.Forms.Tools.TextBoxExt? _nameTextBox;
        private Syncfusion.WinForms.ListView.SfComboBox? _typeComboBox;
        private Syncfusion.WinForms.ListView.SfComboBox? _fundComboBox;
        private Syncfusion.WinForms.ListView.SfComboBox? _departmentComboBox;
        private Syncfusion.WinForms.ListView.SfComboBox? _budgetPeriodComboBox;
        private Syncfusion.WinForms.ListView.SfComboBox? _parentAccountComboBox;
        private SfNumericTextBox? _balanceNumeric;
        private SfNumericTextBox? _budgetAmountNumeric;
        private Syncfusion.Windows.Forms.Tools.CheckBoxAdv? _isActiveCheckBox;
        private SfButton? _saveButton;
        private SfButton? _cancelButton;
        private ErrorProvider? _errorProvider;

        private List<Department> _departments = new();
        private List<BudgetPeriod> _budgetPeriods = new();
        private List<MunicipalAccount> _potentialParents = new();

        /// <summary>
        /// The edited account (available after successful save).
        /// </summary>
        public MunicipalAccount? EditedAccount { get; private set; }

        /// <summary>
        /// Creates an account edit dialog.
        /// </summary>
        /// <param name="accountService">Account service for validation/saving</param>
        /// <param name="departmentRepository">Department repository for dropdown</param>
        /// <param name="scopeFactory">Scope factory for DbContext access</param>
        /// <param name="logger">Logger</param>
        /// <param name="existingAccount">Existing account for edit mode (null for create)</param>
        public AccountEditForm(
            IAccountService accountService,
            IDepartmentRepository departmentRepository,
            IServiceScopeFactory scopeFactory,
            ILogger<AccountEditForm> logger,
            MunicipalAccount? existingAccount = null)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _existingAccount = existingAccount;
            _isEditMode = existingAccount != null;

            _cts = new CancellationTokenSource();

            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            InitializeDialog();
            ThemeColors.ApplyTheme(this);

            Load += async (s, e) =>
            {
                await AsyncEventHelper.ExecuteAsync(
                    async ct => await LoadReferenceDataAsync(ct),
                    _cts,
                    this,
                    _logger,
                    "Loading reference data");
            };

            FormClosing += (s, e) =>
            {
                AsyncEventHelper.CancelAndDispose(ref _cts);
            };

            _logger.LogDebug("AccountEditDialog created in {Mode} mode", _isEditMode ? "Edit" : "Create");
        }

        private void InitializeDialog()
        {
            // Form properties
            Text = _isEditMode ? "Edit Account" : "New Account";
            Size = new Size(600, 650);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            // Main layout
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 12,
                Padding = new Padding(20),
                AutoScroll = true
            };

            // Column styles: Label column (35%), Input column (65%)
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

            // Row styles (all auto-size except last which is for buttons)
            for (int i = 0; i < 11; i++)
            {
                mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            int row = 0;

            // Account Number
            mainPanel.Controls.Add(CreateLabel("Account Number:*"), 0, row);
            _accountNumberTextBox = new Syncfusion.Windows.Forms.Tools.TextBoxExt
            {
                Name = "txtAccountNumber",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Account Number",
                AccessibleDescription = "Enter account number (e.g., 405, 405.1, 101-1000)",
                MaxLength = 20
            };
            SfSkinManager.SetVisualStyle(_accountNumberTextBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            // Watermark for placeholder text
            // _accountNumberTextBox.Watermark = "e.g., 405, 405.1, 101-1000";  // Not available in Syncfusion v31.2.15
            mainPanel.Controls.Add(_accountNumberTextBox, 1, row++);

            // Account Name
            mainPanel.Controls.Add(CreateLabel("Account Name:*"), 0, row);
            _nameTextBox = new Syncfusion.Windows.Forms.Tools.TextBoxExt
            {
                Name = "txtAccountName",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Account Name",
                AccessibleDescription = "Enter descriptive account name",
                MaxLength = 100
            };
            SfSkinManager.SetVisualStyle(_nameTextBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            // _nameTextBox.Watermark = "Descriptive account name";  // Not available in Syncfusion v31.2.15
            mainPanel.Controls.Add(_nameTextBox, 1, row++);

            // Account Type
            mainPanel.Controls.Add(CreateLabel("Account Type:*"), 0, row);
            _typeComboBox = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbAccountType",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Account Type",
                AccessibleDescription = "Select account type",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
            };
            SfSkinManager.SetVisualStyle(_typeComboBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _typeComboBox.DataSource = Enum.GetNames(typeof(AccountType)).ToList();
            mainPanel.Controls.Add(_typeComboBox, 1, row++);

            // Fund Type
            mainPanel.Controls.Add(CreateLabel("Fund Type:*"), 0, row);
            _fundComboBox = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbFundType",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Fund Type",
                AccessibleDescription = "Select fund type",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
            };
            SfSkinManager.SetVisualStyle(_fundComboBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _fundComboBox.DataSource = Enum.GetNames(typeof(MunicipalFundType)).ToList();
            mainPanel.Controls.Add(_fundComboBox, 1, row++);

            // Department
            mainPanel.Controls.Add(CreateLabel("Department:*"), 0, row);
            _departmentComboBox = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbDepartment",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Department",
                AccessibleDescription = "Select department",
                DisplayMember = "Name",
                ValueMember = "Id",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
            };
            SfSkinManager.SetVisualStyle(_departmentComboBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            mainPanel.Controls.Add(_departmentComboBox, 1, row++);

            // Budget Period
            mainPanel.Controls.Add(CreateLabel("Budget Period:*"), 0, row);
            _budgetPeriodComboBox = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbBudgetPeriod",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Budget Period",
                AccessibleDescription = "Select budget period",
                DisplayMember = "Name",
                ValueMember = "Id",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
            };
            SfSkinManager.SetVisualStyle(_budgetPeriodComboBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            mainPanel.Controls.Add(_budgetPeriodComboBox, 1, row++);

            // Parent Account
            mainPanel.Controls.Add(CreateLabel("Parent Account:"), 0, row);
            _parentAccountComboBox = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Name = "cmbParentAccount",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Parent Account",
                AccessibleDescription = "Select parent account (optional)",
                DisplayMember = "Text",
                ValueMember = "Value",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList
            };
            SfSkinManager.SetVisualStyle(_parentAccountComboBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            // Note: Items will be added after DataSource is populated
            mainPanel.Controls.Add(_parentAccountComboBox, 1, row++);

            // Balance
            mainPanel.Controls.Add(CreateLabel("Current Balance:"), 0, row);
            _balanceNumeric = new SfNumericTextBox
            {
                Name = "numBalance",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Current Balance",
                AccessibleDescription = "Enter current account balance",
                FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency,
                // DecimalPlaces = 2,  // Not available in Syncfusion v31.2.15
                MinValue = (double)decimal.MinValue,
                MaxValue = (double)decimal.MaxValue,
                AllowNull = false
            };
            SfSkinManager.SetVisualStyle(_balanceNumeric, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            mainPanel.Controls.Add(_balanceNumeric, 1, row++);

            // Budget Amount
            mainPanel.Controls.Add(CreateLabel("Budget Amount:*"), 0, row);
            _budgetAmountNumeric = new SfNumericTextBox
            {
                Name = "numBudgetAmount",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 5, 0, 10),
                AccessibleName = "Budget Amount",
                AccessibleDescription = "Enter budget amount",
                FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency,
                // DecimalPlaces = 2,  // Not available in Syncfusion v31.2.15
                MinValue = 0,
                MaxValue = (double)decimal.MaxValue,
                AllowNull = false
            };
            SfSkinManager.SetVisualStyle(_budgetAmountNumeric, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            mainPanel.Controls.Add(_budgetAmountNumeric, 1, row++);

            // Is Active
            mainPanel.Controls.Add(CreateLabel("Active:"), 0, row);
            _isActiveCheckBox = new Syncfusion.Windows.Forms.Tools.CheckBoxAdv
            {
                Name = "chkIsActive",
                Checked = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 0, 10),
                Text = "Account is active",
                AccessibleName = "Account Active",
                AccessibleDescription = "Check if account is active"
            };
            SfSkinManager.SetVisualStyle(_isActiveCheckBox, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            mainPanel.Controls.Add(_isActiveCheckBox, 1, row++);

            // Required field note
            var noteLabel = new Label
            {
                Text = "* Required fields",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 10)
            };
            mainPanel.SetColumnSpan(noteLabel, 2);
            mainPanel.Controls.Add(noteLabel, 0, row++);

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 0)
            };

            _cancelButton = new SfButton
            {
                Name = "btnCancel",
                Text = "Cancel",
                Size = new Size(100, 35),
                AccessibleName = "Cancel",
                AccessibleDescription = "Cancel and close dialog",
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0)
            };
            SfSkinManager.SetVisualStyle(_cancelButton, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            buttonPanel.Controls.Add(_cancelButton);

            _saveButton = new SfButton
            {
                Name = "btnSave",
                Text = "Save",
                Size = new Size(100, 35),
                AccessibleName = "Save",
                AccessibleDescription = "Save account changes",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0)
            };
            SfSkinManager.SetVisualStyle(_saveButton, WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            _saveButton.Click += async (s, e) => await SaveButton_Click();
            buttonPanel.Controls.Add(_saveButton);

            // TODO: Apply icons via IThemeIconService when implemented
            // _saveButton.Image = iconService?.GetIcon("save", theme, 16);
            // _cancelButton.Image = iconService?.GetIcon("cancel", theme, 16);

            mainPanel.SetColumnSpan(buttonPanel, 2);
            mainPanel.Controls.Add(buttonPanel, 0, row);

            Controls.Add(mainPanel);

            CancelButton = _cancelButton;
        }

        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 10, 10)
            };
        }

        private async Task LoadReferenceDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Loading reference data for AccountEditDialog");

                // Load departments
                _departments = (await _departmentRepository.GetAllAsync()).ToList();
                _departmentComboBox!.DataSource = _departments;
                _logger.LogDebug("Loaded {Count} departments", _departments.Count);

                // Load budget periods and parent accounts from DbContext
                using var scope = _scopeFactory.CreateScope();
                var context = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scope.ServiceProvider);

                _budgetPeriods = await context.BudgetPeriods
                    .Where(bp => bp.IsActive)
                    .OrderByDescending(bp => bp.Year)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                _budgetPeriodComboBox!.DataSource = _budgetPeriods;
                _logger.LogDebug("Loaded {Count} budget periods", _budgetPeriods.Count);

                // Load potential parent accounts (exclude self in edit mode)
                var parentQuery = context.MunicipalAccounts
                    .Where(a => a.IsActive)
                    .AsNoTracking();

                if (_isEditMode && _existingAccount?.Id > 0)
                {
                    parentQuery = parentQuery.Where(a => a.Id != _existingAccount.Id);
                }

                _potentialParents = await parentQuery
                    .OrderBy(a => a.AccountNumber!.Value)
                    .Take(1000)  // Limit for performance
                    .ToListAsync(cancellationToken);

                // Build parent account list with "(None)" option
                var parentList = new List<ComboBoxItem> { new ComboBoxItem { Text = "(None)", Value = 0 } };
                parentList.AddRange(_potentialParents.Select(p => new ComboBoxItem
                {
                    Text = $"{p.AccountNumber?.Value} - {p.Name}",
                    Value = p.Id
                }));
                _parentAccountComboBox!.DataSource = parentList;
                _logger.LogDebug("Loaded {Count} potential parent accounts", _potentialParents.Count);

                // Populate with existing account data in edit mode
                if (_isEditMode && _existingAccount != null)
                {
                    PopulateExistingData();
                }
                else
                {
                    // Set defaults for create mode
                    if (_typeComboBox!.DataSource is List<string> typeList && typeList.Count > 0)
                        _typeComboBox.SelectedIndex = 0;
                    if (_fundComboBox!.DataSource is List<string> fundList && fundList.Count > 0)
                        _fundComboBox.SelectedIndex = 0;
                    if (_departmentComboBox!.DataSource is System.Collections.IList deptList && deptList.Count > 0)
                        _departmentComboBox.SelectedIndex = 0;
                    if (_budgetPeriodComboBox!.DataSource is System.Collections.IList periodList && periodList.Count > 0)
                        _budgetPeriodComboBox.SelectedIndex = 0;
                    _parentAccountComboBox!.SelectedIndex = 0;  // "(None)"
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load reference data");
                ValidationDialog.Show(this, "Error Loading Data",
                    new[] { "Failed to load departments and budget periods.", ex.Message },
                    null);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void PopulateExistingData()
        {
            if (_existingAccount == null) return;

            _accountNumberTextBox!.Text = _existingAccount.AccountNumber?.Value ?? "";
            _nameTextBox!.Text = _existingAccount.Name;

            // For SfComboBox with string DataSource (enum values)
            if (_typeComboBox!.DataSource is List<string>)
            {
                _typeComboBox.SelectedItem = _existingAccount.Type.ToString();
            }
            if (_fundComboBox!.DataSource is List<string>)
            {
                _fundComboBox.SelectedItem = _existingAccount.Fund.ToString();
            }

            _balanceNumeric!.Value = (double)_existingAccount.Balance;
            _budgetAmountNumeric!.Value = (double)_existingAccount.BudgetAmount;
            _isActiveCheckBox!.Checked = _existingAccount.IsActive;

            // Set department
            var dept = _departments.FirstOrDefault(d => d.Id == _existingAccount.DepartmentId);
            if (dept != null)
                _departmentComboBox!.SelectedItem = dept;

            // Set budget period
            var period = _budgetPeriods.FirstOrDefault(bp => bp.Id == _existingAccount.BudgetPeriodId);
            if (period != null)
                _budgetPeriodComboBox!.SelectedItem = period;

            // Set parent account
            if (_existingAccount.ParentAccountId.HasValue && _parentAccountComboBox!.DataSource is List<ComboBoxItem> parentItems)
            {
                var parentItem = parentItems.FirstOrDefault(item => (int)item.Value == _existingAccount.ParentAccountId.Value);
                if (parentItem != null)
                    _parentAccountComboBox.SelectedItem = parentItem;
            }
            else
            {
                _parentAccountComboBox!.SelectedIndex = 0;  // "(None)"
            }

            _logger.LogDebug("Populated dialog with existing account {AccountNumber}",
                _existingAccount.AccountNumber?.Value);
        }

        private async Task SaveButton_Click()
        {
            try
            {
                _saveButton!.Enabled = false;
                _saveButton.Text = "Saving...";

                // Clear any previous field errors and perform client-side field validation
                _errorProvider?.Clear();
                var fieldErrors = new List<string>();
                if (string.IsNullOrWhiteSpace(_accountNumberTextBox?.Text))
                {
                    _errorProvider?.SetError(_accountNumberTextBox!, "Account Number is required");
                    fieldErrors.Add("Account Number is required");
                }
                if (string.IsNullOrWhiteSpace(_nameTextBox?.Text))
                {
                    _errorProvider?.SetError(_nameTextBox!, "Account Name is required");
                    fieldErrors.Add("Account Name is required");
                }
                if (_budgetAmountNumeric != null && _budgetAmountNumeric.Value < 0)
                {
                    _errorProvider?.SetError(_budgetAmountNumeric, "Budget amount must be zero or greater");
                    fieldErrors.Add("Budget amount must be zero or greater");
                }
                if (!(_departmentComboBox?.SelectedItem is Department))
                {
                    _errorProvider?.SetError(_departmentComboBox!, "Department is required");
                    fieldErrors.Add("Department is required");
                }
                if (!(_budgetPeriodComboBox?.SelectedItem is BudgetPeriod))
                {
                    _errorProvider?.SetError(_budgetPeriodComboBox!, "Budget period is required");
                    fieldErrors.Add("Budget period is required");
                }

                if (fieldErrors.Any())
                {
                    ValidationDialog.Show(this, "Validation Error", "Please correct the highlighted fields:", fieldErrors, _logger);
                    return;
                }

                // Build account from form inputs
                var account = _isEditMode && _existingAccount != null
                    ? _existingAccount
                    : new MunicipalAccount();

                // Map form values
                var accountNumberText = _accountNumberTextBox!.Text.Trim();
                if (!string.IsNullOrEmpty(accountNumberText))
                {
                    account.AccountNumber = new AccountNumber(accountNumberText);
                }

                account.Name = _nameTextBox!.Text.Trim();

                // Parse enum values from SfComboBox SelectedItem
                var selectedType = _typeComboBox!.SelectedItem?.ToString() ?? "Asset";
                account.Type = Enum.Parse<AccountType>(selectedType);

                var selectedFund = _fundComboBox!.SelectedItem?.ToString() ?? "General";
                account.Fund = Enum.Parse<MunicipalFundType>(selectedFund);

                account.Balance = (decimal)(_balanceNumeric?.Value ?? 0.0);
                account.BudgetAmount = (decimal)(_budgetAmountNumeric?.Value ?? 0.0);
                account.IsActive = _isActiveCheckBox!.Checked;

                if (_departmentComboBox!.SelectedItem is Department dept)
                    account.DepartmentId = dept.Id;

                if (_budgetPeriodComboBox!.SelectedItem is BudgetPeriod period)
                    account.BudgetPeriodId = period.Id;

                // Parent account
                if (_parentAccountComboBox!.SelectedItem is ComboBoxItem parentItem)
                {
                    account.ParentAccountId = (int)parentItem.Value;
                }
                else if (_parentAccountComboBox.SelectedIndex == 0)  // "(None)"
                {
                    account.ParentAccountId = null;
                }

                // Validate via service
                var validationErrors = _accountService.ValidateAccount(account).ToList();
                if (validationErrors.Any())
                {
                    _logger.LogWarning("Account validation failed with {Count} errors", validationErrors.Count);
                    var unmapped = ApplyValidationMessages(validationErrors);
                    if (unmapped.Any())
                    {
                        ValidationDialog.Show(this, "Validation Error", "Please correct the highlighted fields:", unmapped, _logger);
                    }
                    return;
                }

                // Save via service
                _logger.LogInformation("Saving account {AccountNumber}", account.AccountNumber?.Value);
                var result = await _accountService.SaveAccountAsync(account, _cts?.Token ?? CancellationToken.None);

                if (!result.Success)
                {
                    var errors = result.ValidationErrors ?? Enumerable.Empty<string>();
                    _logger.LogWarning("Account save failed: {Errors}", string.Join(", ", errors));
                    var unmapped = ApplyValidationMessages(errors);
                    if (unmapped.Any())
                    {
                        ValidationDialog.Show(this, "Save Failed", unmapped, null);
                    }
                    return;
                }

                // Success
                EditedAccount = account;
                _logger.LogInformation("Account {AccountNumber} saved successfully", account.AccountNumber?.Value);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Account save canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save account");
                ValidationDialog.Show(this, "Error Saving Account", new[] { ex.Message }, null);
            }
            finally
            {
                if (_saveButton != null)
                {
                    _saveButton.Enabled = true;
                    _saveButton.Text = "Save";
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                AsyncEventHelper.CancelAndDispose(ref _cts);
                _accountNumberTextBox?.Dispose();
                _nameTextBox?.Dispose();
                _typeComboBox?.Dispose();
                _fundComboBox?.Dispose();
                _departmentComboBox?.Dispose();
                _budgetPeriodComboBox?.Dispose();
                _parentAccountComboBox?.Dispose();
                _balanceNumeric?.Dispose();
                _budgetAmountNumeric?.Dispose();
                _isActiveCheckBox?.Dispose();
                _saveButton?.Dispose();
                _cancelButton?.Dispose();
                _errorProvider?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Apply validation messages returned from the service to the form's controls using heuristics.
        /// Returns any messages that could not be mapped to a specific control.
        /// </summary>
        private List<string> ApplyValidationMessages(IEnumerable<string> messages)
        {
            _errorProvider?.Clear();
            var unmapped = new List<string>();

            var map = new Dictionary<string, Control?>(StringComparer.OrdinalIgnoreCase)
            {
                { NormalizeKey("AccountNumber"), _accountNumberTextBox },
                { NormalizeKey("Account Number"), _accountNumberTextBox },
                { NormalizeKey("Name"), _nameTextBox },
                { NormalizeKey("AccountName"), _nameTextBox },
                { NormalizeKey("Type"), _typeComboBox },
                { NormalizeKey("AccountType"), _typeComboBox },
                { NormalizeKey("Fund"), _fundComboBox },
                { NormalizeKey("FundType"), _fundComboBox },
                { NormalizeKey("Department"), _departmentComboBox },
                { NormalizeKey("BudgetAmount"), _budgetAmountNumeric },
                { NormalizeKey("Budget Amount"), _budgetAmountNumeric },
                { NormalizeKey("Balance"), _balanceNumeric },
                { NormalizeKey("Parent"), _parentAccountComboBox },
                { NormalizeKey("ParentAccount"), _parentAccountComboBox },
                { NormalizeKey("BudgetPeriod"), _budgetPeriodComboBox },
                { NormalizeKey("IsActive"), _isActiveCheckBox }
            };

            foreach (var raw in messages)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var (field, msg) = ParseValidationMessage(raw);
                if (!string.IsNullOrEmpty(field))
                {
                    var key = NormalizeKey(field);
                    if (map.TryGetValue(key, out var ctl) && ctl != null)
                    {
                        try
                        {
                            _errorProvider?.SetError(ctl, msg);
                        }
                        catch { }
                        continue;
                    }
                }

                // Heuristic: look for known tokens inside message
                var found = false;
                foreach (var kv in map)
                {
                    var token = kv.Key;
                    if (raw.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (kv.Value != null)
                        {
                            try { _errorProvider?.SetError(kv.Value, raw); } catch { }
                            found = true; break;
                        }
                    }
                }

                if (!found)
                {
                    unmapped.Add(raw);
                }
            }

            // Focus first control with an error, if any
            var firstErrorControl = map.Values.FirstOrDefault(c => c != null && !string.IsNullOrEmpty(_errorProvider?.GetError(c)));
            try { firstErrorControl?.Focus(); } catch { }

            return unmapped;
        }

        private static string NormalizeKey(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            // remove non-alphanumeric and lower-case
            var cleaned = Regex.Replace(s, "[^a-zA-Z0-9]", "");
            return cleaned.ToLowerInvariant();
        }

        private static (string? field, string message) ParseValidationMessage(string raw)
        {
            var s = raw?.Trim() ?? string.Empty;
            // Common patterns: "Field: message" or "Field - message"
            var m = Regex.Match(s, @"^\s*(?<field>[A-Za-z0-9 _-]+?)\s*[:\-]\s*(?<msg>.+)$");
            if (m.Success)
            {
                return (m.Groups["field"].Value.Trim(), m.Groups["msg"].Value.Trim());
            }

            // Pattern: "message (Field)"
            m = Regex.Match(s, @"^(?<msg>.+)\((?<field>[A-Za-z0-9 _-]+)\)\s*$");
            if (m.Success)
            {
                return (m.Groups["field"].Value.Trim(), m.Groups["msg"].Value.Trim());
            }

            // No explicit field, return null field and full message
            return (null, s);
        }

        /// <summary>
        /// Helper class for combo box items with value.
        /// </summary>
        private class ComboBoxItem
        {
            public string Text { get; set; } = "";
            public object Value { get; set; } = 0;
            public override string ToString() => Text;
        }
    }
}
