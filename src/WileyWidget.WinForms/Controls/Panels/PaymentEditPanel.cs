#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.ListView.Enums;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.ViewModels;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Panel for creating/editing payment (check) entries
/// </summary>
public partial class PaymentEditPanel : ScopedPanelBase<PaymentsViewModel>
{
    private static readonly Size DefaultHostedDialogLogicalSize = new(800, 660);
    private static readonly Size MinimumHostedDialogLogicalSize = new(720, 600);

    private Payment? _existingPayment;
    private bool _isNew;

    public bool HasSavedPayments { get; private set; }

    // Controls
    private TextBoxExt _txtCheckNumber = null!;
    private DateTimePickerAdv _dtpPaymentDate = null!;
    private SfComboBox _cmbPayee = null!;
    private SfButton _btnAddVendor = null!;
    private SfNumericTextBox _numAmount = null!;
    private TextBoxExt _txtDescription = null!;
    private TextBoxExt _txtMemo = null!;
    private SfComboBox _cmbStatus = null!;
    private SfComboBox _cmbAccount = null!;
    private CheckBoxAdv _chkCleared = null!;
    private SfButton _btnSave = null!;
    private SfButton _btnCancel = null!;
    private SfButton _btnDelete = null!;
    private PanelHeader _panelHeader = null!;
    private Label _feedbackLabel = null!;
    private ToolTip _toolTip = null!;

    // Data
    private List<MunicipalAccount> _accounts = new();
    private List<Vendor> _vendors = new();

    // Helper class for account dropdown binding
    private class AccountDisplayItem
    {
        public string Display { get; set; } = string.Empty;
        public MunicipalAccount? Account { get; set; }
        public int AccountId { get; set; }
    }

    // Helper class for vendor dropdown binding
    private class VendorDisplayItem
    {
        public string Display { get; set; } = string.Empty;
        public Vendor? Vendor { get; set; }
        public int? VendorId { get; set; }
        public string PayeeName { get; set; } = string.Empty;
    }

    public PaymentEditPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<PaymentsViewModel>> logger)
        : base(scopeFactory, logger)
    {
        _isNew = true;
        SafeSuspendAndLayout(InitializeComponent);
    }

    /// <summary>
    /// Applies the standard payment editor dialog shell sizing.
    /// </summary>
    public static void ConfigureHostedDialog(Form dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        dialog.Size = LayoutTokens.GetScaled(DefaultHostedDialogLogicalSize);
        dialog.MinimumSize = LayoutTokens.GetScaled(MinimumHostedDialogLogicalSize);
        dialog.FormBorderStyle = FormBorderStyle.Sizable;
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.MinimizeBox = false;
        dialog.MaximizeBox = true;
        dialog.AutoScaleMode = AutoScaleMode.Dpi;
        dialog.ShowIcon = false;
        dialog.ShowInTaskbar = false;
    }

    internal static Size GetHostedDialogSize() => LayoutTokens.GetScaled(DefaultHostedDialogLogicalSize);

    internal static Size GetHostedDialogMinimumSize() => LayoutTokens.GetScaled(MinimumHostedDialogLogicalSize);

    /// <summary>
    /// Configures the panel for editing an existing payment
    /// </summary>
    public void SetExistingPayment(Payment payment)
    {
        _existingPayment = payment ?? throw new ArgumentNullException(nameof(payment));
        _isNew = false;

        if (_panelHeader != null)
        {
            _panelHeader.Title = "Edit Payment";
        }

        if (_btnSave != null)
        {
            _btnSave.Text = "&Save Payment";
            _btnSave.AccessibleName = "Save Payment";
        }

        if (_feedbackLabel != null)
        {
            _feedbackLabel.Visible = false;
        }
    }

    public async Task LoadDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;

            // Resolve repositories
            if (ServiceProvider == null)
            {
                Logger?.LogError("PaymentEditPanel: ServiceProvider is null");
                return;
            }

            var accountRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IMunicipalAccountRepository>(ServiceProvider);
            var vendorRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IVendorRepository>(ServiceProvider);

            // Load budget accounts for dropdown
            if (accountRepository != null)
            {
                var budgetAccounts = (await accountRepository.GetBudgetAccountsAsync(cancellationToken)).ToList();

                if (budgetAccounts.Count == 0)
                {
                    Logger?.LogWarning("PaymentEditPanel: No budget accounts found; falling back to active municipal accounts for payment mapping");
                    budgetAccounts = (await accountRepository.GetAllAsync(cancellationToken))
                        .Where(a => a.IsActive)
                        .ToList();
                }

                _accounts = budgetAccounts
                    .OrderBy(a => a.AccountNumber?.Value)
                    .ThenBy(a => a.Name)
                    .ToList();

                // Populate account dropdown with BindingList for better SfComboBox compatibility
                var accountDisplayList = new BindingList<AccountDisplayItem>(
                    _accounts.Select(a => new AccountDisplayItem
                    {
                        Display = BuildAccountDisplay(a),
                        Account = a,
                        AccountId = a.Id
                    }).ToList()
                );

                EnsureExistingAccountSelectionIsVisible(accountDisplayList);

                BindAccountOptions(accountDisplayList);
                ClearComboSelection(_cmbAccount);
            }

            // Load vendors for dropdown
            if (vendorRepository != null)
            {
                var allVendors = await vendorRepository.GetActiveAsync(cancellationToken);
                _vendors = allVendors.OrderBy(v => v.Name).ToList();

                // Populate vendor dropdown
                var vendorDisplayList = new BindingList<VendorDisplayItem>(
                    _vendors.Select(v => new VendorDisplayItem
                    {
                        Display = BuildVendorDisplay(v.Name),
                        Vendor = v,
                        VendorId = v.Id,
                        PayeeName = v.Name
                    }).ToList()
                );

                EnsureExistingVendorSelectionIsVisible(vendorDisplayList);

                BindVendorOptions(vendorDisplayList);
                ClearComboSelection(_cmbPayee);
            }

            // Load existing payment data if editing
            if (_existingPayment != null && !_isNew)
            {
                _txtCheckNumber.Text = _existingPayment.CheckNumber;
                _txtCheckNumber.Enabled = false; // Don't allow changing check number
                _dtpPaymentDate.Value = _existingPayment.PaymentDate;

                // Select vendor in dropdown
                SelectVendorByIdentity(_existingPayment.VendorId, _existingPayment.Payee);
                _numAmount.Value = (double)_existingPayment.Amount;
                _txtDescription.Text = _existingPayment.Description;
                _txtMemo.Text = _existingPayment.Memo ?? string.Empty;
                _cmbStatus.SelectedItem = NormalizeEditableStatus(_existingPayment.Status);
                // Checkbox state is set automatically via Status changed event

                // Select the associated account if one exists
                SelectAccountById(_existingPayment.MunicipalAccountId);

                // Show delete button for existing payments
                _btnDelete.Visible = true;
                _feedbackLabel.Visible = false;
            }
            else
            {
                // Set defaults for new payment
                _dtpPaymentDate.Value = DateTime.Now;
                _cmbStatus.SelectedItem = "Pending";
                ClearComboSelection(_cmbAccount);
                // Checkbox state is set automatically via Status changed event
                _btnDelete.Visible = false;
                _feedbackLabel.Visible = false;
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: LoadDataAsync failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InitializeComponent()
    {
        var themeName = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(this, themeName);

        Name = "PaymentEditPanel";
        Size = ScaleLogicalToDevice(DefaultHostedDialogLogicalSize);
        MinimumSize = ScaleLogicalToDevice(MinimumHostedDialogLogicalSize);
        Dock = DockStyle.Fill;
        Padding = new Padding(0);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        _toolTip = ControlFactory.CreateToolTip(toolTip =>
        {
            toolTip.AutoPopDelay = 8000;
            toolTip.InitialDelay = 250;
            toolTip.ReshowDelay = 100;
            toolTip.ShowAlways = true;
        });

        var bodyFont = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        var sectionHeaderFont = new Font("Segoe UI", 11F, FontStyle.Bold);
        var actionFont = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        var primaryActionFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        var feedbackFont = new Font("Segoe UI", 9.75F, FontStyle.Regular);

        var rowSpacing = LayoutTokens.GetScaled(6);
        var firstSectionGap = 0;
        var sectionGap = LayoutTokens.GetScaled(12);
        var controlHeight = LayoutTokens.GetScaled(LayoutTokens.DialogButtonHeight);
        var multilineHeight = LayoutTokens.GetScaled(84);
        var labelWidth = LayoutTokens.GetScaled(168);
        var compactFieldWidth = LayoutTokens.GetScaled(236);
        var wideFieldMinWidth = LayoutTokens.GetScaled(320);
        var payeeButtonWidth = LayoutTokens.GetScaled(132);
        var headerHeight = LayoutTokens.GetScaled(LayoutTokens.HeaderMinimumHeight);

        var currencyFormat = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        currencyFormat.CurrencyDecimalDigits = 2;

        // Main container with header
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false
        };
        mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, headerHeight)); // Header
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Form content

        // === HEADER ===
        _panelHeader = ControlFactory.CreatePanelHeader(header =>
        {
            header.Title = _isNew ? "New Payment" : "Edit Payment";
            header.Dock = DockStyle.Fill;
            header.Margin = Padding.Empty;
            header.ShowRefreshButton = false;
            header.ShowPinButton = false;
            header.ShowHelpButton = false;
            header.ShowCloseButton = false;
        });
        mainContainer.Controls.Add(_panelHeader, 0, 0);

        // === SCROLLABLE FORM CONTENT ===
        var formPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = LayoutTokens.GetScaled(new Padding(16, 8, 16, 10)),
            Margin = Padding.Empty,
            AutoScroll = true
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;

        // === SECTION 1: CHECK INFORMATION (48px top gap) ===
        AddSectionHeader(mainLayout, ref row, "Check Information", firstSectionGap);

        // Check Number (width constraint: 180px)
        AddLabeledControl(mainLayout, ref row, "Check Number",
            _txtCheckNumber = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.MaxLength = 20;
                textBox.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
                textBox.Font = bodyFont;
                textBox.Height = controlHeight;
                textBox.Anchor = AnchorStyles.Left;
                textBox.Width = compactFieldWidth;
                textBox.MinimumSize = new Size(compactFieldWidth, controlHeight);
                textBox.AccessibleName = "Check Number";
            }), rowSpacing);

        // Payment Date (width constraint: 200px)
        AddLabeledControl(mainLayout, ref row, "Payment Date",
            _dtpPaymentDate = ControlFactory.CreateDateTimePickerAdv(datePicker =>
            {
                datePicker.Format = System.Windows.Forms.DateTimePickerFormat.Short;
                datePicker.ShowCheckBox = false;
                datePicker.ShowUpDown = false;
                datePicker.Font = bodyFont;
                datePicker.Height = controlHeight;
                datePicker.Anchor = AnchorStyles.Left;
                datePicker.Width = compactFieldWidth;
                datePicker.MinimumSize = new Size(compactFieldWidth, controlHeight);
                datePicker.AccessibleName = "Payment Date";
            }), rowSpacing);

        // === SECTION 2: PAYEE & AMOUNT (48px gap) ===
        AddSectionHeader(mainLayout, ref row, "Payee & Amount", sectionGap);

        // Payee with Add Vendor button
        var payeeRowHeight = controlHeight + LayoutTokens.GetScaled(4);
        var payeeContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = false,
            Height = payeeRowHeight,
            MinimumSize = new Size(0, payeeRowHeight)
        };
        payeeContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        payeeContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, payeeButtonWidth));

        _cmbPayee = ControlFactory.CreateSfComboBox(combo =>
        {
            combo.Dock = DockStyle.Fill;
            combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDown;
            combo.AllowNull = true;
            combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            combo.AutoCompleteSuggestMode = AutoCompleteSuggestMode.Contains;
            combo.AllowCaseSensitiveOnAutoComplete = false;
            combo.Font = bodyFont;
            combo.Height = controlHeight;
            combo.Margin = Padding.Empty;
            combo.MinimumSize = new Size(wideFieldMinWidth, controlHeight);
            combo.DropDownWidth = LayoutTokens.GetScaled(560);
            combo.MaxDropDownItems = 12;
            combo.Watermark = "Select or enter a payee";
            combo.AccessibleName = "Payee";
        });
        payeeContainer.Controls.Add(_cmbPayee, 0, 0);

        _btnAddVendor = ControlFactory.CreateSfButton("New Vendor", button =>
        {
            button.Font = actionFont;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(LayoutTokens.GetScaled(8), 0, 0, 0);
            button.AccessibleName = "Add New Vendor";
            button.Image = LoadIcon("Add32");
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.MinimumSize = new Size(payeeButtonWidth, controlHeight);
        });
        _btnAddVendor.Click += BtnAddVendor_Click;
        payeeContainer.Controls.Add(_btnAddVendor, 1, 0);

        AddLabeledControl(mainLayout, ref row, "Payee", payeeContainer, rowSpacing);

        // Amount (width constraint: 200px)
        AddLabeledControl(mainLayout, ref row, "Amount",
            _numAmount = ControlFactory.CreateSfNumericTextBox(textBox =>
            {
                textBox.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency;
                textBox.Value = 0;
                textBox.MinValue = 0;
                textBox.MaxValue = 9999999.99;
                textBox.AllowNull = false;
                textBox.Font = bodyFont;
                textBox.Height = controlHeight;
                textBox.Anchor = AnchorStyles.Left;
                textBox.Width = compactFieldWidth;
                textBox.MinimumSize = new Size(compactFieldWidth, controlHeight);
                textBox.TextAlign = HorizontalAlignment.Right;
                textBox.ReadOnly = false;
                textBox.NumberFormatInfo = currencyFormat;
                textBox.NumberFormatInfo.CurrencyDecimalDigits = 2;
                textBox.TabStop = true;
                textBox.AccessibleName = "Amount";
            }), rowSpacing);
        _numAmount.Enter += NumAmount_Enter;

        // === SECTION 3: BUDGET ACCOUNT & DESCRIPTION (48px gap) ===
        AddSectionHeader(mainLayout, ref row, "Budget Account & Description", sectionGap);

        // Budget account (fills width)
        AddLabeledControl(mainLayout, ref row, "Budget Account",
            _cmbAccount = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.Dock = DockStyle.Fill;
                combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDown;
                combo.AllowNull = true;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSuggestMode = AutoCompleteSuggestMode.Contains;
                combo.AllowCaseSensitiveOnAutoComplete = false;
                combo.Font = bodyFont;
                combo.Height = controlHeight;
                combo.MinimumSize = new Size(wideFieldMinWidth, controlHeight);
                combo.DropDownWidth = LayoutTokens.GetScaled(620);
                combo.MaxDropDownItems = 14;
                combo.Watermark = "Select a budget account";
                combo.AccessibleName = "Budget Account";
            }), rowSpacing);

        // Description (fixed 96px height, scrollable)
        AddLabeledControl(mainLayout, ref row, "Description",
            _txtDescription = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Dock = DockStyle.Fill;
                textBox.Multiline = true;
                textBox.MaxLength = 500;
                textBox.ScrollBars = ScrollBars.Vertical;
                textBox.Font = bodyFont;
                textBox.Height = multilineHeight;
                textBox.MinimumSize = new Size(wideFieldMinWidth, multilineHeight);
                textBox.Padding = LayoutTokens.GetScaled(LayoutTokens.InputTextPadding);
                textBox.AccessibleName = "Description";
            }), rowSpacing, alignTop: true);

        // Memo (fixed 96px height, scrollable)
        AddLabeledControl(mainLayout, ref row, "Memo",
            _txtMemo = ControlFactory.CreateTextBoxExt(textBox =>
            {
                textBox.Dock = DockStyle.Fill;
                textBox.Multiline = true;
                textBox.MaxLength = 1000;
                textBox.ScrollBars = ScrollBars.Vertical;
                textBox.Font = bodyFont;
                textBox.Height = multilineHeight;
                textBox.MinimumSize = new Size(wideFieldMinWidth, multilineHeight);
                textBox.Padding = LayoutTokens.GetScaled(LayoutTokens.InputTextPadding);
                textBox.AccessibleName = "Memo";
            }), rowSpacing, alignTop: true);

        // === SECTION 4: STATUS (48px gap) ===
        AddSectionHeader(mainLayout, ref row, "Status", sectionGap);

        // Status dropdown (width constraint: 200px)
        AddLabeledControl(mainLayout, ref row, "Payment Status",
            _cmbStatus = ControlFactory.CreateSfComboBox(combo =>
            {
                combo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
                combo.DataSource = new[] { "Pending", "Cleared", "Void", "Cancelled" };
                combo.AllowNull = false;
                combo.Font = bodyFont;
                combo.Height = controlHeight;
                combo.Dock = DockStyle.Fill;
                combo.MinimumSize = new Size(wideFieldMinWidth, controlHeight);
                combo.DropDownWidth = LayoutTokens.GetScaled(240);
                combo.AccessibleName = "Payment Status";
            }), rowSpacing);

        // Cleared checkbox
        AddLabeledControl(mainLayout, ref row, string.Empty,
            _chkCleared = ControlFactory.CreateCheckBoxAdv("Check has cleared the bank", checkBox =>
            {
                checkBox.CheckState = CheckState.Unchecked;
                checkBox.Font = bodyFont;
                checkBox.Height = controlHeight;
                checkBox.AutoSize = true;
            }), rowSpacing);

        // Wire up event handlers for checkbox/status synchronization
        _chkCleared.CheckedChanged += ChkCleared_CheckedChanged;
        _cmbStatus.SelectedIndexChanged += CmbStatus_SelectedIndexChanged;

        // === ACTION BUTTONS (48px top gap, right-aligned) ===
        _feedbackLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            MaximumSize = new Size(LayoutTokens.GetScaled(760), 0),
            Padding = new Padding(0),
            Visible = false,
            Font = feedbackFont,
            AccessibleName = "Payment feedback"
        };

        var footerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2,
            Padding = new Padding(0, sectionGap, 0, 0),
            Margin = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        footerLayout.Controls.Add(_feedbackLabel, 0, 0);
        footerLayout.SetColumnSpan(_feedbackLabel, 2);

        var destructiveButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false
        };

        var actionButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Anchor = AnchorStyles.Right
        };

        _btnSave = ControlFactory.CreateSfButton("&Create Payment", button =>
        {
            button.Width = LayoutTokens.GetScaled(156);
            button.Height = LayoutTokens.GetScaled(LayoutTokens.DialogButtonHeight);
            button.Font = primaryActionFont;
            button.Margin = new Padding(LayoutTokens.GetScaled(8), 0, 0, 0);
            button.Image = LoadIcon("Save32");
            button.AccessibleName = "Create Payment";
        });
        _btnSave.Click += BtnSave_Click;
        _toolTip.SetToolTip(_btnSave, "Save this payment (Ctrl+S). New entries stay open for additional payments.");

        _btnCancel = ControlFactory.CreateSfButton("&Cancel", button =>
        {
            button.Width = LayoutTokens.GetScaled(120);
            button.Height = LayoutTokens.GetScaled(LayoutTokens.DialogButtonHeight);
            button.Font = actionFont;
            button.Margin = new Padding(LayoutTokens.GetScaled(8), 0, 0, 0);
            button.Image = LoadIcon("Close32");
            button.AccessibleName = "Cancel Edit";
        });
        _btnCancel.Click += (s, e) => ParentForm?.Close();
        _toolTip.SetToolTip(_btnCancel, "Close without saving changes (Esc).");

        _btnDelete = ControlFactory.CreateSfButton("&Delete Payment", button =>
        {
            button.Width = LayoutTokens.GetScaled(156);
            button.Height = LayoutTokens.GetScaled(LayoutTokens.DialogButtonHeight);
            button.Font = actionFont;
            button.Visible = false;
            button.Margin = new Padding(0);
            button.Image = LoadIcon("Delete32");
            button.AccessibleName = "Delete Payment";
        });
        _btnDelete.Click += BtnDelete_Click;
        _toolTip.SetToolTip(_btnDelete, "Delete this payment entry.");

        destructiveButtonPanel.Controls.Add(_btnDelete);
        actionButtonPanel.Controls.Add(_btnSave);
        actionButtonPanel.Controls.Add(_btnCancel);

        footerLayout.Controls.Add(destructiveButtonPanel, 0, 1);
        footerLayout.Controls.Add(actionButtonPanel, 1, 1);

        mainLayout.Controls.Add(footerLayout, 0, row);
        mainLayout.SetColumnSpan(footerLayout, 2);
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        formPanel.Controls.Add(mainLayout);
        mainContainer.Controls.Add(formPanel, 0, 1);
        Controls.Add(mainContainer);
        ApplyProfessionalPanelLayout();

        // Wire up autocomplete filters
        _cmbPayee.Filter = FilterVendors;
        _cmbAccount.Filter = FilterAccounts;

        _toolTip.SetToolTip(_cmbPayee, "Select or enter the payee.");
        _toolTip.SetToolTip(_btnAddVendor, "Create a vendor without leaving the payment form.");
        _toolTip.SetToolTip(_txtCheckNumber, "Enter the printed check number.");
        _toolTip.SetToolTip(_dtpPaymentDate, "Choose the payment date shown on the check.");
        _toolTip.SetToolTip(_numAmount, "Enter payment amount.");
        _toolTip.SetToolTip(_cmbAccount, "Select the budget account for this payment. Changing it here changes which budget line this payment will post and reconcile against.");
        _toolTip.SetToolTip(_txtDescription, "Describe what this payment covers.");
        _toolTip.SetToolTip(_txtMemo, "Optional internal notes for this payment.");
        _toolTip.SetToolTip(_cmbStatus, "Select the current payment status.");
    }

    // === HELPER METHODS FOR MODERN LAYOUT ===

    /// <summary>
    /// Adds a section header with consistent styling and spacing
    /// </summary>
    private void AddSectionHeader(TableLayoutPanel layout, ref int row, string text, int topMargin)
    {
        var header = new Label
        {
            Text = text,
            Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, topMargin, 0, 8),
            Padding = new Padding(0),
            UseMnemonic = false
        };
        layout.Controls.Add(header, 0, row);
        layout.SetColumnSpan(header, 2);
        var headerRowHeight = header.GetPreferredSize(Size.Empty).Height + topMargin + LayoutTokens.GetScaled(8);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, headerRowHeight));
        row++;
    }

    /// <summary>
    /// Adds a labeled control with consistent spacing
    /// </summary>
    private void AddLabeledControl(TableLayoutPanel layout, ref int row, string labelText, Control control, int bottomMargin, bool alignTop = false)
    {
        var labelGap = LayoutTokens.GetScaled(14);

        if (!string.IsNullOrEmpty(labelText))
        {
            var label = new Label
            {
                Text = labelText + ":",
                Font = new System.Drawing.Font("Segoe UI", 10.5F),
                TextAlign = alignTop ? System.Drawing.ContentAlignment.TopRight : System.Drawing.ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Padding = new Padding(0),
                Margin = new Padding(0, 0, labelGap, bottomMargin)
            };
            layout.Controls.Add(label, 0, row);
        }
        else
        {
            layout.Controls.Add(new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, labelGap, bottomMargin)
            }, 0, row);
        }

        control.Margin = new Padding(0, 0, 0, bottomMargin);
        layout.Controls.Add(control, 1, row);
        var preferredHeight = control.GetPreferredSize(new Size(control.Width > 0 ? control.Width : control.MinimumSize.Width, 0)).Height;
        var contentHeight = Math.Max(Math.Max(control.Height, control.MinimumSize.Height), preferredHeight);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, contentHeight + bottomMargin));
        row++;
    }

    private void EnsureExistingVendorSelectionIsVisible(BindingList<VendorDisplayItem> items)
    {
        if (_existingPayment == null || _isNew || string.IsNullOrWhiteSpace(_existingPayment.Payee))
        {
            return;
        }

        var alreadyPresent = items.Any(x =>
            string.Equals(x.PayeeName, _existingPayment.Payee, StringComparison.OrdinalIgnoreCase) ||
            (x.VendorId.HasValue && x.VendorId == _existingPayment.VendorId));

        if (alreadyPresent)
        {
            return;
        }

        items.Insert(0, new VendorDisplayItem
        {
            Display = BuildVendorDisplay(_existingPayment.Payee, isInactiveFallback: true),
            VendorId = _existingPayment.VendorId,
            PayeeName = _existingPayment.Payee
        });
    }

    private void BindVendorOptions(BindingList<VendorDisplayItem> items)
    {
        _cmbPayee.DataSource = null;
        _cmbPayee.DisplayMember = nameof(VendorDisplayItem.Display);
        _cmbPayee.ValueMember = nameof(VendorDisplayItem.VendorId);
        _cmbPayee.DataSource = items;
    }

    private void EnsureExistingAccountSelectionIsVisible(BindingList<AccountDisplayItem> items)
    {
        if (_existingPayment?.MunicipalAccountId is not int accountId || _isNew)
        {
            return;
        }

        if (items.Any(x => x.AccountId == accountId))
        {
            return;
        }

        var existingAccount = _existingPayment.MunicipalAccount;
        items.Insert(0, new AccountDisplayItem
        {
            Display = BuildAccountDisplay(existingAccount, accountId, isInactiveFallback: true),
            Account = existingAccount,
            AccountId = accountId
        });
    }

    private void BindAccountOptions(BindingList<AccountDisplayItem> items)
    {
        _cmbAccount.DataSource = null;
        _cmbAccount.DisplayMember = nameof(AccountDisplayItem.Display);
        _cmbAccount.ValueMember = nameof(AccountDisplayItem.AccountId);
        _cmbAccount.DataSource = items;
    }

    private static void ClearComboSelection(SfComboBox comboBox)
    {
        comboBox.SelectedIndex = -1;
        comboBox.Text = string.Empty;
    }

    private void SelectVendorByIdentity(int? vendorId, string? payeeName)
    {
        if (vendorId.HasValue)
        {
            _cmbPayee.SelectedValue = vendorId.Value;
            if (_cmbPayee.SelectedItem is VendorDisplayItem)
            {
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(payeeName) && _cmbPayee.DataSource is BindingList<VendorDisplayItem> vendorItems)
        {
            var vendorIndex = vendorItems.ToList().FindIndex(x =>
                string.Equals(x.PayeeName, payeeName, StringComparison.OrdinalIgnoreCase) ||
                (vendorId.HasValue && x.VendorId == vendorId));
            if (vendorIndex >= 0)
            {
                _cmbPayee.SelectedIndex = vendorIndex;
                return;
            }

            _cmbPayee.SelectedIndex = -1;
            _cmbPayee.Text = payeeName.Trim();
            return;
        }

        ClearComboSelection(_cmbPayee);
    }

    private void SelectAccountById(int? accountId)
    {
        if (!accountId.HasValue)
        {
            ClearComboSelection(_cmbAccount);
            return;
        }

        _cmbAccount.SelectedValue = accountId.Value;
        if (_cmbAccount.SelectedItem is AccountDisplayItem)
        {
            return;
        }

        if (_cmbAccount.DataSource is BindingList<AccountDisplayItem> items)
        {
            var itemIndex = items.ToList().FindIndex(x => x.AccountId == accountId.Value);
            if (itemIndex >= 0)
            {
                _cmbAccount.SelectedIndex = itemIndex;
                return;
            }
        }

        ClearComboSelection(_cmbAccount);
    }

    private static string NormalizeEditableStatus(string? status)
    {
        return IsVoidStatus(status) ? "Void" : status?.Trim() ?? "Pending";
    }

    private static string BuildVendorDisplay(string payeeName, bool isInactiveFallback = false)
    {
        return isInactiveFallback ? $"{payeeName} [inactive vendor]" : payeeName;
    }

    private static string BuildAccountDisplay(MunicipalAccount account)
    {
        return BuildAccountDisplay(account, account.Id, isInactiveFallback: false);
    }

    private static string BuildAccountDisplay(MunicipalAccount? account, int accountId, bool isInactiveFallback)
    {
        var accountNumber = account?.AccountNumber?.DisplayValue;
        var accountName = account?.Name;

        string baseText;
        if (!string.IsNullOrWhiteSpace(accountNumber) && !string.IsNullOrWhiteSpace(accountName))
        {
            baseText = $"{accountNumber} - {accountName}";
        }
        else if (!string.IsNullOrWhiteSpace(accountName))
        {
            baseText = accountName;
        }
        else
        {
            baseText = $"Historical Account #{accountId}";
        }

        return isInactiveFallback ? $"[Inactive] {baseText}" : baseText;
    }

    private void ShowInlineFeedback(string message, Color textColor)
    {
        _feedbackLabel.Text = message;
        _feedbackLabel.ForeColor = textColor;
        _feedbackLabel.Visible = !string.IsNullOrWhiteSpace(message);
    }

    private DialogResult ShowWarningDialog(string message, string title, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        return ControlFactory.ShowSemanticMessageBox(this, message, title, SyncfusionControlFactory.MessageSemanticKind.Warning, buttons, defaultButton);
    }

    private DialogResult ShowErrorDialog(string message, string title, string? details = null)
    {
        return ControlFactory.ShowSemanticMessageBox(this, message, title, SyncfusionControlFactory.MessageSemanticKind.Error, MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, details: details);
    }

    /// <summary>
    /// Filter predicate for vendor dropdown autocomplete
    /// </summary>
    private bool FilterVendors(object item)
    {
        if (item is VendorDisplayItem vendorItem)
        {
            return vendorItem.Display.Contains(_cmbPayee.Text, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// Filter predicate for account dropdown autocomplete
    /// </summary>
    private bool FilterAccounts(object item)
    {
        if (item is AccountDisplayItem accountItem)
        {
            return accountItem.Display.Contains(_cmbAccount.Text, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// Helper to load icon from resources
    /// </summary>
    private System.Drawing.Image? LoadIcon(string iconName)
    {
        try
        {
            var type = typeof(PaymentEditPanel);
            var stream =
                type.Assembly.GetManifestResourceStream(
                    $"WileyWidget.WinForms.Resources.FlatIcons.{iconName}flatflat.png") ??
                type.Assembly.GetManifestResourceStream(
                    $"WileyWidget.WinForms.Resources.FlatIcons.{iconName}.png") ??
                type.Assembly.GetManifestResourceStream(
                    $"WileyWidget.WinForms.Resources.FlatIcons.{iconName}flat.png");

            if (stream != null)
                return System.Drawing.Image.FromStream(stream);
        }
        catch { }
        return null;
    }

    private async void BtnAddVendor_Click(object? sender, EventArgs e)
    {
        try
        {
            if (ServiceProvider == null)
            {
                Logger?.LogError("PaymentEditPanel: ServiceProvider is null");
                return;
            }

            var themeName = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;

            // Create a simple dialog to add a new vendor
            using var dialog = new Form
            {
                Text = "Add New Vendor",
                Width = LayoutTokens.GetScaled(680),
                Height = LayoutTokens.GetScaled(660),
                MinimumSize = LayoutTokens.GetScaled(new Size(640, 620)),
                FormBorderStyle = FormBorderStyle.Sizable,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = true,
                AutoScaleMode = AutoScaleMode.Dpi,
                ShowIcon = false,
                ShowInTaskbar = false
            };
            SfSkinManager.SetVisualStyle(dialog, themeName);

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 13,
                ColumnCount = 2,
                Padding = new Padding(18, 16, 18, 16),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Section header: Vendor Details
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Name
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Contact
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Email
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Phone
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Section header: Mailing Address
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Address line 1
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Address line 2
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // City
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // State
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Postal code
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Country
            tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

            var dialogLabelWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(170f);
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, dialogLabelWidth));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label CreateDialogLabel(string text)
            {
                return new Label
                {
                    Text = text,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Font = new System.Drawing.Font("Segoe UI", 10F),
                    Padding = new Padding(0, 4, 10, 4),
                    Margin = new Padding(0, 4, 10, 4),
                    UseMnemonic = false
                };
            }

            TextBoxExt CreateDialogTextBox(int maxLength)
            {
                return ControlFactory.CreateTextBoxExt(textBox =>
                {
                    textBox.Dock = DockStyle.Fill;
                    textBox.Font = new System.Drawing.Font("Segoe UI", 10F);
                    textBox.MaxLength = maxLength;
                    textBox.Margin = new Padding(0, 2, 0, 2);
                    textBox.MinimumSize = new System.Drawing.Size(0, LayoutTokens.GetScaled(32));
                });
            }

            // Section Header: Vendor Details
            var vendorDetailsLabel = new Label
            {
                Text = "Vendor Details",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(0, 6, 0, 6),
                Margin = new Padding(0, 0, 0, 6),
                UseMnemonic = false
            };
            tableLayout.Controls.Add(vendorDetailsLabel, 0, 0);
            tableLayout.SetColumnSpan(vendorDetailsLabel, 2);

            // Vendor Name
            var nameLabel = CreateDialogLabel("Vendor Name:");
            tableLayout.Controls.Add(nameLabel, 0, 1);

            var nameTxt = CreateDialogTextBox(100);
            tableLayout.Controls.Add(nameTxt, 1, 1);

            // Contact Name / Notes
            var contactLabel = CreateDialogLabel("Contact Name / Notes:");
            tableLayout.Controls.Add(contactLabel, 0, 2);

            var contactTxt = CreateDialogTextBox(200);
            tableLayout.Controls.Add(contactTxt, 1, 2);

            // Email
            var emailLabel = CreateDialogLabel("Email:");
            tableLayout.Controls.Add(emailLabel, 0, 3);

            var emailTxt = CreateDialogTextBox(200);
            tableLayout.Controls.Add(emailTxt, 1, 3);

            // Phone
            var phoneLabel = CreateDialogLabel("Phone:");
            tableLayout.Controls.Add(phoneLabel, 0, 4);

            var phoneTxt = CreateDialogTextBox(50);
            tableLayout.Controls.Add(phoneTxt, 1, 4);

            // Section Header: Mailing Address
            var mailingAddressLabel = new Label
            {
                Text = "Mailing Address",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(0, 10, 0, 6),
                Margin = new Padding(0, 4, 0, 6),
                UseMnemonic = false
            };
            tableLayout.Controls.Add(mailingAddressLabel, 0, 5);
            tableLayout.SetColumnSpan(mailingAddressLabel, 2);

            // Address Line 1
            var addressLine1Label = CreateDialogLabel("Address Line 1:");
            tableLayout.Controls.Add(addressLine1Label, 0, 6);

            var addressLine1Txt = CreateDialogTextBox(200);
            tableLayout.Controls.Add(addressLine1Txt, 1, 6);

            // Address Line 2
            var addressLine2Label = CreateDialogLabel("Address Line 2:");
            tableLayout.Controls.Add(addressLine2Label, 0, 7);

            var addressLine2Txt = CreateDialogTextBox(200);
            tableLayout.Controls.Add(addressLine2Txt, 1, 7);

            // City
            var cityLabel = CreateDialogLabel("City:");
            tableLayout.Controls.Add(cityLabel, 0, 8);

            var cityTxt = CreateDialogTextBox(100);
            tableLayout.Controls.Add(cityTxt, 1, 8);

            // State / Province
            var stateLabel = CreateDialogLabel("State / Province:");
            tableLayout.Controls.Add(stateLabel, 0, 9);

            var stateTxt = CreateDialogTextBox(50);
            tableLayout.Controls.Add(stateTxt, 1, 9);

            // Postal Code
            var postalLabel = CreateDialogLabel("Postal Code:");
            tableLayout.Controls.Add(postalLabel, 0, 10);

            var postalTxt = CreateDialogTextBox(20);
            tableLayout.Controls.Add(postalTxt, 1, 10);

            // Country
            var countryLabel = CreateDialogLabel("Country:");
            tableLayout.Controls.Add(countryLabel, 0, 11);

            var countryTxt = CreateDialogTextBox(100);
            tableLayout.Controls.Add(countryTxt, 1, 11);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0)
            };

            var btnSaveVendor = ControlFactory.CreateSfButton("&Save Vendor", button =>
            {
                button.Width = LayoutTokens.GetScaled(132);
                button.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                button.Font = new System.Drawing.Font("Segoe UI", 10F);
                button.DialogResult = DialogResult.OK;
            });

            var btnCancel = ControlFactory.CreateSfButton("&Cancel", button =>
            {
                button.Width = LayoutTokens.GetScaled(112);
                button.Height = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
                button.Font = new System.Drawing.Font("Segoe UI", 10F);
                button.DialogResult = DialogResult.Cancel;
                button.Margin = new Padding(LayoutTokens.GetScaled(8), 0, 0, 0);
            });

            buttonPanel.Controls.Add(btnCancel);
            buttonPanel.Controls.Add(btnSaveVendor);

            tableLayout.Controls.Add(buttonPanel, 0, 12);
            tableLayout.SetColumnSpan(buttonPanel, 2);

            dialog.Controls.Add(tableLayout);
            dialog.AcceptButton = btnSaveVendor;
            dialog.CancelButton = btnCancel;

            if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(nameTxt.Text))
            {
                static string? NormalizeOptional(string value)
                {
                    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                }

                // Create new vendor
                var newVendor = new Vendor
                {
                    Name = nameTxt.Text.Trim(),
                    ContactInfo = NormalizeOptional(contactTxt.Text),
                    Email = NormalizeOptional(emailTxt.Text),
                    Phone = NormalizeOptional(phoneTxt.Text),
                    MailingAddressLine1 = NormalizeOptional(addressLine1Txt.Text),
                    MailingAddressLine2 = NormalizeOptional(addressLine2Txt.Text),
                    MailingAddressCity = NormalizeOptional(cityTxt.Text),
                    MailingAddressState = NormalizeOptional(stateTxt.Text),
                    MailingAddressPostalCode = NormalizeOptional(postalTxt.Text),
                    MailingAddressCountry = NormalizeOptional(countryTxt.Text),
                    IsActive = true
                };

                var vendorRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IVendorRepository>(ServiceProvider);
                if (vendorRepository != null)
                {
                    var createdVendor = await vendorRepository.AddAsync(newVendor, CancellationToken.None);

                    // Reload vendors dropdown
                    var allVendors = await vendorRepository.GetActiveAsync(CancellationToken.None);
                    _vendors = allVendors.OrderBy(v => v.Name).ToList();

                    var vendorDisplayList = new BindingList<VendorDisplayItem>(
                        _vendors.Select(v => new VendorDisplayItem
                        {
                            Display = BuildVendorDisplay(v.Name),
                            Vendor = v,
                            VendorId = v.Id,
                            PayeeName = v.Name
                        }).ToList()
                    );

                    BindVendorOptions(vendorDisplayList);
                    SelectVendorByIdentity(createdVendor.Id, createdVendor.Name);

                    _ = ControlFactory.ShowSemanticMessageBox(
                        this,
                        "Vendor created successfully",
                        "Success",
                        SyncfusionControlFactory.MessageSemanticKind.Success,
                        MessageBoxButtons.OK,
                        playNotificationSound: true);
                }
            }

        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error adding vendor");
            ShowErrorDialog("Unable to add vendor.", "Vendor Error", ex.Message);
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            IsBusy = true;

            var selectedStatus = _cmbStatus.SelectedItem?.ToString() ?? "Pending";
            var isVoidStatus = IsVoidStatus(selectedStatus);
            var amount = (decimal)(_numAmount.Value ?? 0);
            ShowInlineFeedback(string.Empty, Color.Green);

            // Validate
            if (string.IsNullOrWhiteSpace(_txtCheckNumber.Text))
            {
                ShowWarningDialog("Check number is required.", "Validation Error");
                _txtCheckNumber.Focus();
                return;
            }

            if (_cmbPayee.SelectedItem == null && string.IsNullOrWhiteSpace(_cmbPayee.Text))
            {
                ShowWarningDialog("Please select or enter a payee (vendor).", "Validation Error");
                _cmbPayee.Focus();
                return;
            }

            if (isVoidStatus)
            {
                if (amount != 0m)
                {
                    ShowWarningDialog("Voided checks must have an amount of 0.", "Validation Error");
                    _numAmount.Value = 0;
                    _numAmount.Focus();
                    return;
                }
            }
            else if (amount <= 0)
            {
                ShowWarningDialog("Amount must be greater than zero.", "Validation Error");
                _numAmount.Focus();
                return;
            }

            if (_cmbAccount.SelectedIndex < 0)
            {
                ShowWarningDialog("Please select a budget account.", "Validation Error");
                _cmbAccount.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtDescription.Text))
            {
                ShowWarningDialog("Description is required.", "Validation Error");
                _txtDescription.Focus();
                return;
            }

            // Save
            if (ServiceProvider == null)
            {
                Logger?.LogError("PaymentEditPanel: ServiceProvider is null");
                return;
            }

            var repository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPaymentRepository>(ServiceProvider);

            var payment = _existingPayment ?? new Payment();
            payment.CheckNumber = _txtCheckNumber.Text.Trim();
            payment.PaymentDate = _dtpPaymentDate.Value;

            // Get payee from selected vendor or typed text
            if (_cmbPayee.SelectedItem is VendorDisplayItem selectedVendor)
            {
                payment.Payee = selectedVendor.PayeeName;
                payment.VendorId = selectedVendor.VendorId;
            }
            else if (!string.IsNullOrWhiteSpace(_cmbPayee.Text))
            {
                payment.Payee = _cmbPayee.Text.Trim();
                payment.VendorId = null;
            }
            else
            {
                payment.Payee = string.Empty;
                payment.VendorId = null;
            }
            payment.Amount = isVoidStatus ? 0m : amount;
            payment.Description = _txtDescription.Text.Trim();
            payment.Memo = _txtMemo.Text.Trim();
            payment.Status = selectedStatus;
            payment.IsCleared = string.Equals(payment.Status, "Cleared", StringComparison.OrdinalIgnoreCase);

            // Set the associated account
            if (_cmbAccount.SelectedIndex >= 0 && _cmbAccount.SelectedItem is AccountDisplayItem selectedItem)
            {
                payment.MunicipalAccountId = selectedItem.AccountId;
            }
            else
            {
                payment.MunicipalAccountId = null;
            }

            if (_isNew)
            {
                await repository.AddAsync(payment, CancellationToken.None);
                HasSavedPayments = true;

                Logger?.LogInformation("PaymentEditPanel: Payment created successfully - CheckNumber: {CheckNumber}, Amount: {Amount}, Payee: {Payee}",
                    payment.CheckNumber, payment.Amount, payment.Payee);

                PrepareForNextNewPaymentEntry();
                ShowInlineFeedback($"Payment {payment.CheckNumber} created successfully. Adjust the budget account here any time you need to reroute a payment to a different budget line.", Color.Green);
                return;
            }
            else
            {
                await repository.UpdateAsync(payment, CancellationToken.None);
                HasSavedPayments = true;

                Logger?.LogInformation("PaymentEditPanel: Payment updated successfully - ID: {Id}, CheckNumber: {CheckNumber}, Amount: {Amount}",
                    payment.Id, payment.CheckNumber, payment.Amount);
                _ = ControlFactory.ShowSemanticMessageBox(
                    this,
                    "Payment updated successfully",
                    "Update Successful",
                    SyncfusionControlFactory.MessageSemanticKind.Success,
                    MessageBoxButtons.OK,
                    playNotificationSound: true);
            }

            if (ParentForm is Form parentForm)
            {
                parentForm.DialogResult = DialogResult.OK;
                Logger?.LogDebug("PaymentEditPanel: DialogResult set to OK");
            }
            ParentForm?.Close();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            Logger?.LogWarning(ex, "PaymentEditPanel: Duplicate check number {CheckNumber}", _txtCheckNumber.Text?.Trim());
            ShowWarningDialog(
                "That check number already exists. Enter a unique check number or edit the existing payment record.",
                "Duplicate Check Number");
            _txtCheckNumber.Focus();
            _txtCheckNumber.SelectAll();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error saving payment");
            ShowErrorDialog("Unable to save payment.", "Save Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_existingPayment == null || _isNew)
        {
            ShowWarningDialog("Cannot delete a new payment.", "Invalid Operation");
            return;
        }

        var result = ShowWarningDialog(
            $"Are you sure you want to delete payment {_existingPayment.CheckNumber} to {_existingPayment.Payee}?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
            return;

        try
        {
            IsBusy = true;

            if (ServiceProvider == null)
            {
                Logger?.LogError("PaymentEditPanel: ServiceProvider is null");
                return;
            }

            var repository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IPaymentRepository>(ServiceProvider);

            await repository.DeleteAsync(_existingPayment.Id, CancellationToken.None);

            _ = ControlFactory.ShowSemanticMessageBox(
                this,
                "Payment deleted successfully",
                "Delete Successful",
                SyncfusionControlFactory.MessageSemanticKind.Success,
                MessageBoxButtons.OK,
                playNotificationSound: true);

            if (ParentForm is Form parentForm)
            {
                parentForm.DialogResult = DialogResult.OK;
            }
            ParentForm?.Close();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error deleting payment");
            ShowErrorDialog("Unable to delete payment.", "Delete Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            BtnSave_Click(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.Escape)
        {
            ParentForm?.Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _numAmount.Enter -= NumAmount_Enter;
            try { _btnSave?.SafeDispose(); } catch { }
            try { _btnCancel?.SafeDispose(); } catch { }
            try { _btnDelete?.SafeDispose(); } catch { }
            try { _txtCheckNumber?.SafeDispose(); } catch { }
            try { _cmbPayee?.SafeClearDataSource(); _cmbPayee?.SafeDispose(); } catch { }
            try { _btnAddVendor?.SafeDispose(); } catch { }
            try { _txtDescription?.SafeDispose(); } catch { }
            try { _txtMemo?.SafeDispose(); } catch { }
            try { _dtpPaymentDate?.SafeDispose(); } catch { }
            try { _numAmount?.SafeDispose(); } catch { }
            try { _cmbStatus?.SafeClearDataSource(); _cmbStatus?.SafeDispose(); } catch { }
            try { _cmbAccount?.SafeClearDataSource(); _cmbAccount?.SafeDispose(); } catch { }
            try { _chkCleared?.SafeDispose(); } catch { }
            try { _toolTip?.Dispose(); } catch { }
        }
        base.Dispose(disposing);
    }

    private void NumAmount_Enter(object? sender, EventArgs e)
    {
        try
        {
            _numAmount.SelectAll();
        }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "PaymentEditPanel: Unable to select amount text on focus");
        }
    }

    /// <summary>
    /// Event handler for Cleared checkbox changes.
    /// Synchronizes checkbox state with Status dropdown.
    /// </summary>
    private void ChkCleared_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            // Temporarily unhook status change handler to prevent infinite loop
            if (_cmbStatus != null)
            {
                _cmbStatus.SelectedIndexChanged -= CmbStatus_SelectedIndexChanged;

                if (_chkCleared.Checked)
                {
                    _cmbStatus.SelectedItem = "Cleared";
                }
                else
                {
                    // If unchecking and status is Cleared, revert to Pending
                    if (_cmbStatus.SelectedItem?.ToString() == "Cleared")
                    {
                        _cmbStatus.SelectedItem = "Pending";
                    }
                }

                // Re-hook the event handler
                _cmbStatus.SelectedIndexChanged += CmbStatus_SelectedIndexChanged;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error in ChkCleared_CheckedChanged");
        }
    }

    /// <summary>
    /// Event handler for Status dropdown changes.
    /// Synchronizes Status dropdown with checkbox state.
    /// </summary>
    private void CmbStatus_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            // Temporarily unhook checkbox change handler to prevent infinite loop
            if (_chkCleared != null)
            {
                _chkCleared.CheckedChanged -= ChkCleared_CheckedChanged;

                var status = _cmbStatus.SelectedItem?.ToString();
                _chkCleared.Checked = status == "Cleared";

                if (IsVoidStatus(status))
                {
                    _numAmount.Value = 0;
                }

                // Re-hook the event handler
                _chkCleared.CheckedChanged += ChkCleared_CheckedChanged;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error in CmbStatus_SelectedIndexChanged");
        }
    }

    private void PrepareForNextNewPaymentEntry()
    {
        _txtCheckNumber.Text = string.Empty;
        _txtCheckNumber.Enabled = true;
        _dtpPaymentDate.Value = DateTime.Now;
        ClearComboSelection(_cmbPayee);
        _numAmount.Value = 0;
        _txtDescription.Text = string.Empty;
        _txtMemo.Text = string.Empty;
        ClearComboSelection(_cmbAccount);
        _cmbStatus.SelectedItem = "Pending";
        _chkCleared.Checked = false;
        _txtCheckNumber.Focus();
    }

    private static bool IsVoidStatus(string? status)
    {
        return string.Equals(status?.Trim(), "Void", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status?.Trim(), "Voided", StringComparison.OrdinalIgnoreCase);
    }
}
