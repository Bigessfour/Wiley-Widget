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
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.WinForms.Input;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using Action = System.Action;
using Syncfusion.WinForms.DataGrid;
using WileyWidget.WinForms.Services;
using WileyWidget.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace WileyWidget.WinForms.Controls.Panels
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
    public partial class AccountEditPanel : ScopedPanelBase
    {
        // Strongly-typed ViewModel (this is what you use in your code)
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.DefaultValue(null)]
        public new AccountsViewModel? ViewModel
        {
            get => (AccountsViewModel?)base.ViewModel;
            set => base.ViewModel = value;
        }
        /// <summary>
        /// Gets the dialog result after save/cancel operations.
        /// </summary>
        public DialogResult SaveDialogResult { get; private set; } = DialogResult.None;

        /// <summary>
        /// Event raised when the save operation completes successfully.
        /// </summary>
        public event EventHandler? SaveCompleted;

        /// <summary>
        /// Event raised when the cancel operation is requested.
        /// </summary>
        public event EventHandler? CancelRequested;

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
        private Supporting.ErrorProviderBinding? _errorBinding;
        private ToolTip? _toolTip;
        private BindingSource _bindingSource = null!;
        private DpiAwareImageService? _imageService;
        private bool _isNew;
        private BindingList<Fund>? _fundBindingList;

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
        public AccountEditPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase> logger, DpiAwareImageService imageService)
            : base(scopeFactory, logger)
        {
            _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));

            // Set AutoScaleMode for proper DPI scaling
            this.AutoScaleMode = AutoScaleMode.Dpi;

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
                nameof(MunicipalAccountEditModel.FundType),
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
            this.Padding = new Padding(8);
            this.AutoScroll = true;
            this.MinimumSize = new Size(640, 720);
            this.Size = new Size(640, 720);

            // === CREATE MAIN TABLE LAYOUT ===
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 11, // Title + 9 fields + button panel
                AutoSize = false,
                Padding = new Padding(6)
            };

            // Responsive columns: labels auto-size, editor column takes remaining width
            _mainLayout.ColumnStyles.Clear();
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); // Title
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Account Number
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Name
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104)); // Description
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Department
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Fund
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Type
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Balance
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Budget
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Active
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68)); // Button Panel

            // === TITLE ROW (Row 0) ===
            lblTitle = new Label
            {
                Text = _isNew ? "Create New Account" : "Edit Account",
                AutoSize = false,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
            _mainLayout.Controls.Add(lblTitle, 0, 0);
            _mainLayout.SetColumnSpan(lblTitle, 2);

            // === ACCOUNT NUMBER (Row 1) ===
            lblAccountNumber = new Label
            {
                Text = "Account Number:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblAccountNumber, 0, 1);

            txtAccountNumber = new TextBoxExt
            {
                Name = "txtAccountNumber",
                MaxLength = 20,
                AccessibleName = "Account Number",
                AccessibleDescription = "Enter the unique account number",
                Enabled = _isNew,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 1,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(txtAccountNumber, 1, 1);
            _toolTip.SetToolTip(txtAccountNumber, "Unique identifier for this account (e.g., 1000, 2100)");

            // === NAME (Row 2) ===
            lblName = new Label
            {
                Text = "Account Name:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblName, 0, 2);

            txtName = new TextBoxExt
            {
                Name = "txtName",
                MaxLength = 100,
                AccessibleName = "Account Name",
                AccessibleDescription = "Enter the descriptive name for this account",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 2,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(txtName, 1, 2);
            _toolTip.SetToolTip(txtName, "Descriptive name (e.g., 'Cash - General Fund')");

            // === DESCRIPTION (Row 3) ===
            lblDescription = new Label
            {
                Text = "Description:",
                TextAlign = ContentAlignment.TopRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblDescription, 0, 3);

            txtDescription = new TextBoxExt
            {
                Name = "txtDescription",
                MaxLength = 500,
                AccessibleName = "Description",
                AccessibleDescription = "Enter optional description",
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 3,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(txtDescription, 1, 3);
            _toolTip.SetToolTip(txtDescription, "Optional detailed description");

            // === DEPARTMENT (Row 4) ===
            lblDepartment = new Label
            {
                Text = "Department:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblDepartment, 0, 4);

            cmbDepartment = new SfComboBox
            {
                Name = "cmbDepartment",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                ComboBoxMode = Syncfusion.WinForms.ListView.Enums.ComboBoxMode.SingleSelection,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                MaxDropDownItems = 10,
                DropDownWidth = 300,
                AccessibleName = "Department",
                AccessibleDescription = "Select the department this account belongs to",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 4,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(cmbDepartment, 1, 4);
            _toolTip.SetToolTip(cmbDepartment, "Select owning department");

            // === FUND (Row 5) ===
            lblFund = new Label
            {
                Text = "Fund:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblFund, 0, 5);

            cmbFund = new SfComboBox
            {
                Name = "cmbFund",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDown,
                ComboBoxMode = Syncfusion.WinForms.ListView.Enums.ComboBoxMode.SingleSelection,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                MaxDropDownItems = 10,
                DropDownWidth = 300,
                AccessibleName = "Fund Type",
                AccessibleDescription = "Select a municipal fund type for this account",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 5,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(cmbFund, 1, 5);
            _toolTip.SetToolTip(cmbFund, "Select existing fund from database");

            // === TYPE (Row 6) ===
            lblType = new Label
            {
                Text = "Type:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblType, 0, 6);

            cmbType = new SfComboBox
            {
                Name = "cmbType",
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                ComboBoxMode = Syncfusion.WinForms.ListView.Enums.ComboBoxMode.SingleSelection,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                MaxDropDownItems = 10,
                DropDownWidth = 250,
                AccessibleName = "Account Type",
                AccessibleDescription = "Select the account type",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 6,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(cmbType, 1, 6);
            _toolTip.SetToolTip(cmbType, "Select account type (Asset, Liability, Revenue, Expense)");

            // === BALANCE (Row 7) ===
            lblBalance = new Label
            {
                Text = "Current Balance:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblBalance, 0, 7);

            numBalance = new SfNumericTextBox
            {
                Name = "numBalance",
                AllowNull = false,
                Value = 0,
                MinValue = (double)decimal.MinValue,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Balance",
                AccessibleDescription = "Enter the current account balance",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 7,
                ThemeName = themeName,
                FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency,
                NumberFormatInfo = CurrencyFormat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            // Set decimal digits via NumberFormatInfo
            numBalance.NumberFormatInfo.CurrencyDecimalDigits = 2;
            _mainLayout.Controls.Add(numBalance, 1, 7);
            _toolTip.SetToolTip(numBalance, "Current account balance");

            // === BUDGET (Row 8) ===
            lblBudget = new Label
            {
                Text = "Budget Amount:",
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(lblBudget, 0, 8);

            numBudget = new SfNumericTextBox
            {
                Name = "numBudget",
                AllowNull = false,
                Value = 0,
                MinValue = 0,
                MaxValue = (double)decimal.MaxValue,
                AccessibleName = "Budget Amount",
                AccessibleDescription = "Enter the budgeted amount for this account",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 8,
                ThemeName = themeName,
                FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency,
                NumberFormatInfo = CurrencyFormat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            // Set decimal digits via NumberFormatInfo
            numBudget.NumberFormatInfo.CurrencyDecimalDigits = 2;
            _mainLayout.Controls.Add(numBudget, 1, 8);
            _toolTip.SetToolTip(numBudget, "Budgeted amount for this account");

            // === ACTIVE (Row 9) ===
            lblActive = new Label
            {
                Text = " ",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            _mainLayout.Controls.Add(lblActive, 0, 9);

            chkActive = new CheckBoxAdv
            {
                Name = "chkActive",
                Text = "Is Active",
                Checked = true,
                AutoSize = false,
                Height = 30,
                AccessibleName = "Active Status",
                AccessibleDescription = "Check to mark this account as active",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                TabIndex = 9,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _mainLayout.Controls.Add(chkActive, 1, 9);
            _toolTip.SetToolTip(chkActive, "Indicates whether this account is currently active");

            // === BUTTON PANEL (Row 10) ===
            _buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };

            btnSave = new SfButton
            {
                Name = "btnSave",
                Text = _isNew ? "&Create" : "&Save",
                Width = 112,
                Height = 36,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = _isNew ? "Create Account" : "Save Account",
                AccessibleDescription = "Save the account changes",
                TabIndex = 10,
                ThemeName = themeName,
                Margin = new Padding(0, 0, 10, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _saveHandler = BtnSave_Click;
            btnSave.Click += _saveHandler;

            btnCancel = new SfButton
            {
                Name = "btnCancel",
                Text = "&Cancel",
                Width = 112,
                Height = 36,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = "Cancel",
                AccessibleDescription = "Cancel and discard changes",
                TabIndex = 11,
                ThemeName = themeName,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _cancelHandler = BtnCancel_Click;
            btnCancel.Click += _cancelHandler;

            // Position buttons: Cancel first (rightmost), then Save to its left
            btnCancel.Left = _buttonPanel.ClientSize.Width - btnCancel.Width - 10;
            btnCancel.Top = 10;
            btnSave.Left = btnCancel.Left - btnSave.Width - 10;
            btnSave.Top = 10;

            _buttonPanel.Controls.Add(btnCancel);
            _buttonPanel.Controls.Add(btnSave);

            // === ICON ASSIGNMENT FOR DIALOG BUTTONS (Optional Polish) ===
            try
            {
                // Get compact icons for dialog buttons
                btnSave.Image = _imageService?.GetScaledImage("save", new Size(16, 16));
                btnCancel.Image = _imageService?.GetScaledImage("close", new Size(16, 16));

                // Layout: compact horizontal button content
                btnSave.TextImageRelation = TextImageRelation.ImageBeforeText;
                btnCancel.TextImageRelation = TextImageRelation.ImageBeforeText;
                btnSave.ImageAlign = ContentAlignment.MiddleLeft;
                btnCancel.ImageAlign = ContentAlignment.MiddleLeft;

                Logger?.LogDebug("[ACCOUNT_EDIT_ICONS] ✅ Dialog button icons assigned successfully");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[ACCOUNT_EDIT_ICONS] ⚠️ Failed to load dialog button icons - UI will display text only");
            }

            _mainLayout.Controls.Add(_buttonPanel, 0, 10);
            _mainLayout.SetColumnSpan(_buttonPanel, 2);

            Controls.Add(_mainLayout);

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
                _errorBinding = new Supporting.ErrorProviderBinding(_errorProvider, _editModel);

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

                // Load funds from database with BindingList for dynamic updates
                List<Fund> funds = new();
                if (ServiceProvider != null)
                {
                    using var scope = ServiceProvider.CreateScope();
                    var dbContextFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IDbContextFactory<AppDbContext>>(scope.ServiceProvider);
                    if (dbContextFactory != null)
                    {
                        using var context = await dbContextFactory.CreateDbContextAsync();
                        funds = await context.Funds.OrderBy(f => f.Name).ToListAsync();
                    }
                }

                if (funds.Count > 0)
                {
                    // Wrap in BindingList for dynamic add/remove capability
                    _fundBindingList = new BindingList<Fund>(funds);
                    cmbFund.DataSource = _fundBindingList;
                    cmbFund.DisplayMember = "Name";
                    cmbFund.ValueMember = "Id";
                    if (_existingAccount?.FundId != null)
                    {
                        cmbFund.SelectedValue = _existingAccount.FundId.Value;
                    }
                }
                else
                {
                    // Fallback to enum if database load fails
                    cmbFund.DataSource = Enum.GetValues(typeof(MunicipalFundType));
                    if (_existingAccount != null)
                    {
                        cmbFund.SelectedItem = _existingAccount.FundType;
                    }
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

        private void SyncEditModelFromControls()
        {
            _bindingSource?.EndEdit();

            if (txtAccountNumber != null)
            {
                _editModel.AccountNumber = txtAccountNumber.Text?.Trim() ?? string.Empty;
            }

            if (txtName != null)
            {
                _editModel.Name = txtName.Text?.Trim() ?? string.Empty;
            }

            if (txtDescription != null)
            {
                _editModel.Description = string.IsNullOrWhiteSpace(txtDescription.Text)
                    ? null
                    : txtDescription.Text.Trim();
            }

            if (cmbDepartment != null)
            {
                if (cmbDepartment.SelectedValue is int deptId && deptId > 0)
                {
                    _editModel.DepartmentId = deptId;
                }
                else if (cmbDepartment.SelectedItem is Department department && department.Id > 0)
                {
                    _editModel.DepartmentId = department.Id;
                }
            }

            if (cmbFund != null)
            {
                if (cmbFund.SelectedValue is int fundId && fundId > 0)
                {
                    _editModel.FundId = fundId;
                }
                else if (cmbFund.SelectedItem is MunicipalFundType fundType)
                {
                    _editModel.FundId = null;
                    _editModel.FundType = fundType;
                }
            }

            if (cmbType != null)
            {
                if (cmbType.SelectedItem is AccountType accountType)
                {
                    _editModel.Type = accountType;
                }
                else if (cmbType.SelectedValue is AccountType selectedType)
                {
                    _editModel.Type = selectedType;
                }
            }

            if (numBalance?.Value != null)
            {
                _editModel.Balance = Convert.ToDecimal(numBalance.Value, CultureInfo.InvariantCulture);
            }

            if (numBudget?.Value != null)
            {
                _editModel.BudgetAmount = Convert.ToDecimal(numBudget.Value, CultureInfo.InvariantCulture);
            }

            if (chkActive != null)
            {
                _editModel.IsActive = chkActive.Checked;
            }
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

                SyncEditModelFromControls();

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

                // Convert edit model to entity (with validation)
                MunicipalAccount account;
                try
                {
                    account = _editModel.ToEntity();
                }
                catch (InvalidOperationException ex)
                {
                    // ToEntity throws InvalidOperationException if DepartmentId is not set
                    Logger?.LogWarning(ex, "AccountEditPanel: ToEntity validation failed");
                    MessageBox.Show(
                        ex.Message,
                        "Validation Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    // Focus the department combobox to help user fix the issue
                    cmbDepartment?.Focus();
                    return;
                }

                // If editing, preserve the Id
                if (_existingAccount != null)
                {
                    account.Id = _existingAccount.Id;
                }

                // Save via view model command (viewmodel performs server-side validation/uniqueness)
                if (_isNew)
                {
                    await ViewModel.CreateAccountFromEditorAsync(account, ct);
                }
                else
                {
                    await ViewModel.UpdateAccountFromEditorAsync(account, ct);
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

                // Raise save completed event instead of directly closing form
                SaveCompleted?.Invoke(this, EventArgs.Empty);

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
            // Raise cancel requested event instead of directly closing form
            CancelRequested?.Invoke(this, EventArgs.Empty);
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
        /// Adds a new fund to the dropdown list dynamically at runtime.
        /// </summary>
        /// <param name="fund">The fund to add</param>
        public void AddFundToDropdown(Fund fund)
        {
            if (_fundBindingList != null && fund != null)
            {
                _fundBindingList.Add(fund);
                // Select the newly added fund
                cmbFund.SelectedValue = fund.Id;
                Logger?.LogInformation("Added fund '{FundName}' (ID: {FundId}) to dropdown", fund.Name, fund.Id);
            }
            else
            {
                Logger?.LogWarning("Cannot add fund - BindingList not initialized or fund is null");
            }
        }

        /// <summary>
        /// Removes a fund from the dropdown list dynamically at runtime.
        /// </summary>
        /// <param name="fundId">The ID of the fund to remove</param>
        public void RemoveFundFromDropdown(int fundId)
        {
            if (_fundBindingList != null)
            {
                var fund = _fundBindingList.FirstOrDefault(f => f.Id == fundId);
                if (fund != null)
                {
                    _fundBindingList.Remove(fund);
                    Logger?.LogInformation("Removed fund '{FundName}' (ID: {FundId}) from dropdown", fund.Name, fundId);
                }
                else
                {
                    Logger?.LogWarning("Fund with ID {FundId} not found in dropdown", fundId);
                }
            }
            else
            {
                Logger?.LogWarning("Cannot remove fund - BindingList not initialized");
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

        /// <summary>
        /// Resolves the AccountsViewModel from the service provider.
        /// </summary>
        protected override object? ResolveViewModel(IServiceProvider serviceProvider)
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<AccountsViewModel>(serviceProvider);
            if (vm == null)
            {
                Logger?.LogWarning("AccountEditPanel: Failed to resolve AccountsViewModel from service provider");
            }
            else
            {
                Logger?.LogDebug("AccountEditPanel: Successfully resolved AccountsViewModel");
            }
            return vm;
        }
    }
}
