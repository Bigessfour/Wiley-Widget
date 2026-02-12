#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls.Panels;

/// <summary>
/// Panel for creating/editing payment (check) entries
/// </summary>
public partial class PaymentEditPanel : ScopedPanelBase
{
    private Payment? _existingPayment;
    private bool _isNew;

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

    // Data
    private List<MunicipalAccount> _accounts = new();
    private List<Vendor> _vendors = new();

    // Helper class for account dropdown binding
    private class AccountDisplayItem
    {
        public string Display { get; set; } = string.Empty;
        public MunicipalAccount Account { get; set; } = null!;
    }

    // Helper class for vendor dropdown binding
    private class VendorDisplayItem
    {
        public string Display { get; set; } = string.Empty;
        public Vendor Vendor { get; set; } = null!;
    }

    public PaymentEditPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase> logger)
        : base(scopeFactory, logger)
    {
        _isNew = true;
        InitializeComponent();
    }

    /// <summary>
    /// Configures the panel for editing an existing payment
    /// </summary>
    public void SetExistingPayment(Payment payment)
    {
        _existingPayment = payment ?? throw new ArgumentNullException(nameof(payment));
        _isNew = false;
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

            // Load accounts for dropdown
            if (accountRepository != null)
            {
                var allAccounts = await accountRepository.GetAllAsync(cancellationToken);
                _accounts = allAccounts.Where(a => a.IsActive).OrderBy(a => a.AccountNumber?.Value).ToList();

                // Populate account dropdown with BindingList for better SfComboBox compatibility
                var accountDisplayList = new BindingList<AccountDisplayItem>(
                    _accounts.Select(a => new AccountDisplayItem
                    {
                        Display = $"{a.AccountNumber?.Value} - {a.Name}",
                        Account = a
                    }).ToList()
                );

                // Clear any existing binding before rebinding
                _cmbAccount.DataSource = null;
                _cmbAccount.DataSource = accountDisplayList;
                _cmbAccount.DisplayMember = "Display";
                _cmbAccount.ValueMember = "Account";

                // Set "None" as default selection initially
                _cmbAccount.SelectedIndex = -1;
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
                        Display = v.Name,
                        Vendor = v
                    }).ToList()
                );

                _cmbPayee.DataSource = null;
                _cmbPayee.DataSource = vendorDisplayList;
                _cmbPayee.DisplayMember = "Display";
                _cmbPayee.ValueMember = "Vendor";
                _cmbPayee.SelectedIndex = -1;
            }

            // Load existing payment data if editing
            if (_existingPayment != null && !_isNew)
            {
                _txtCheckNumber.Text = _existingPayment.CheckNumber;
                _txtCheckNumber.Enabled = false; // Don't allow changing check number
                _dtpPaymentDate.Value = _existingPayment.PaymentDate;

                // Select vendor in dropdown
                if (!string.IsNullOrWhiteSpace(_existingPayment.Payee) && _cmbPayee.DataSource is BindingList<VendorDisplayItem> vendorItems)
                {
                    var vendorIndex = vendorItems.ToList().FindIndex(x => x.Vendor.Name == _existingPayment.Payee);
                    if (vendorIndex >= 0)
                    {
                        _cmbPayee.SelectedIndex = vendorIndex;
                    }
                    else
                    {
                        _cmbPayee.SelectedIndex = -1;
                    }
                }
                _numAmount.Value = (double)_existingPayment.Amount;
                _txtDescription.Text = _existingPayment.Description;
                _txtMemo.Text = _existingPayment.Memo ?? string.Empty;
                _cmbStatus.SelectedItem = _existingPayment.Status;
                // Checkbox state is set automatically via Status changed event

                // Select the associated account if one exists
                if (_existingPayment.MunicipalAccountId.HasValue)
                {
                    var account = _accounts.FirstOrDefault(a => a.Id == _existingPayment.MunicipalAccountId.Value);
                    if (account != null && _cmbAccount.DataSource is BindingList<AccountDisplayItem> items)
                    {
                        var itemIndex = items.ToList().FindIndex(x => x.Account.Id == account.Id);
                        if (itemIndex >= 0)
                        {
                            _cmbAccount.SelectedIndex = itemIndex;
                        }
                    }
                }
                else
                {
                    _cmbAccount.SelectedIndex = -1;
                }

                // Show delete button for existing payments
                _btnDelete.Visible = true;
            }
            else
            {
                // Set defaults for new payment
                _dtpPaymentDate.Value = DateTime.Now;
                _cmbStatus.SelectedItem = "Pending";
                _cmbAccount.SelectedIndex = -1; // No account selected
                // Checkbox state is set automatically via Status changed event
                _btnDelete.Visible = false;
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
        Size = new System.Drawing.Size(900, 1050);
        Padding = new Padding(0);
        AutoScaleMode = AutoScaleMode.Dpi;  // High-DPI display support

        // === MODERN FLUENT DESIGN LAYOUT ===
        // Base spacing unit: 8px (multiples of 8 for consistency)
        const int ROW_SPACING = 24;        // 3 * 8px
        const int SECTION_GAP = 48;        // 6 * 8px
        const int CONTROL_HEIGHT = 40;     // Fluent-recommended touch target
        const int MULTILINE_HEIGHT = 96;   // 12 * 8px
        const int LABEL_WIDTH = 160;       // 20 * 8px

        // Main container with header
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); // Header
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Form content

        // === HEADER ===
        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16) // Base-8 padding
        };
        // Apply theme to header panel instead of hard-coded background color
        SfSkinManager.SetVisualStyle(headerPanel, themeName);

        var headerLabel = new Label
        {
            Text = _isNew ? "New Payment" : "Edit Payment",
            Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.White,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            AutoSize = false
        };
        headerPanel.Controls.Add(headerLabel);
        mainContainer.Controls.Add(headerPanel, 0, 0);

        // === SCROLLABLE FORM CONTENT ===
        var formPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 24, 32, 24), // 4*BASE_UNIT, 3*BASE_UNIT
            AutoScroll = true
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LABEL_WIDTH));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;

        // === SECTION 1: CHECK INFORMATION (48px top gap) ===
        AddSectionHeader(mainLayout, ref row, "Check Information", SECTION_GAP);

        // Check Number (width constraint: 180px)
        AddLabeledControl(mainLayout, ref row, "Check Number",
            _txtCheckNumber = new TextBoxExt
            {
                MaxLength = 20,
                ThemeName = themeName,
                CharacterCasing = System.Windows.Forms.CharacterCasing.Upper,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = CONTROL_HEIGHT,
                Anchor = AnchorStyles.Left,
                Width = 180
            }, ROW_SPACING);

        // Payment Date (width constraint: 200px)
        AddLabeledControl(mainLayout, ref row, "Payment Date",
            _dtpPaymentDate = new DateTimePickerAdv
            {
                ThemeName = themeName,
                Format = System.Windows.Forms.DateTimePickerFormat.Short,
                ShowCheckBox = false,
                ShowUpDown = false,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = CONTROL_HEIGHT,
                Anchor = AnchorStyles.Left,
                Width = 200
            }, ROW_SPACING);

        // === SECTION 2: PAYEE & AMOUNT (48px gap) ===
        AddSectionHeader(mainLayout, ref row, "Payee & Amount", SECTION_GAP);

        // Payee with Add Vendor button
        var payeeContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = false,
            Height = CONTROL_HEIGHT
        };
        payeeContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        payeeContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, CONTROL_HEIGHT));

        _cmbPayee = new SfComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDown,
            ThemeName = themeName,
            AllowNull = true,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSuggestMode = AutoCompleteSuggestMode.Contains,
            AllowCaseSensitiveOnAutoComplete = false,
            Font = new System.Drawing.Font("Segoe UI", 11F),
            Height = CONTROL_HEIGHT,
            Margin = new Padding(0, 0, 8, 0) // 8px gap before button
        };
        payeeContainer.Controls.Add(_cmbPayee, 0, 0);

        _btnAddVendor = new SfButton
        {
            Text = "+",
            ThemeName = themeName,
            Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold),
            Dock = DockStyle.Fill,
            AccessibleName = "Add New Vendor",
            Image = LoadIcon("Add32")
        };
        _btnAddVendor.Click += BtnAddVendor_Click;
        payeeContainer.Controls.Add(_btnAddVendor, 1, 0);

        AddLabeledControl(mainLayout, ref row, "Payee", payeeContainer, ROW_SPACING);

        // Amount (width constraint: 200px)
        AddLabeledControl(mainLayout, ref row, "Amount",
            _numAmount = new SfNumericTextBox
            {
                FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency,
                Value = 0,
                MinValue = 0,
                MaxValue = 9999999.99,
                ThemeName = themeName,
                AllowNull = false,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = CONTROL_HEIGHT,
                Anchor = AnchorStyles.Left,
                Width = 200
            }, ROW_SPACING);

        // === SECTION 3: ACCOUNT & DESCRIPTION (48px gap) ===
        AddSectionHeader(mainLayout, ref row, "Account & Description", SECTION_GAP);

        // Account (fills width)
        AddLabeledControl(mainLayout, ref row, "Account",
            _cmbAccount = new SfComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDown,
                ThemeName = themeName,
                AllowNull = true,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSuggestMode = AutoCompleteSuggestMode.Contains,
                AllowCaseSensitiveOnAutoComplete = false,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = CONTROL_HEIGHT
            }, ROW_SPACING);

        // Description (fixed 96px height, scrollable)
        AddLabeledControl(mainLayout, ref row, "Description",
            _txtDescription = new TextBoxExt
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                MaxLength = 500,
                ThemeName = themeName,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = MULTILINE_HEIGHT,
                Padding = new Padding(8) // Internal padding for comfort
            }, ROW_SPACING);

        // Memo (fixed 96px height, scrollable)
        AddLabeledControl(mainLayout, ref row, "Memo",
            _txtMemo = new TextBoxExt
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                MaxLength = 1000,
                ThemeName = themeName,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = MULTILINE_HEIGHT,
                Padding = new Padding(8)
            }, ROW_SPACING);

        // === SECTION 4: STATUS (48px gap) ===
        AddSectionHeader(mainLayout, ref row, "Status", SECTION_GAP);

        // Status dropdown (width constraint: 200px)
        AddLabeledControl(mainLayout, ref row, "Payment Status",
            _cmbStatus = new SfComboBox
            {
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                DataSource = new[] { "Pending", "Cleared", "Void", "Cancelled" },
                ThemeName = themeName,
                AllowNull = false,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = CONTROL_HEIGHT,
                Anchor = AnchorStyles.Left,
                Width = 200
            }, ROW_SPACING);

        // Cleared checkbox
        AddLabeledControl(mainLayout, ref row, string.Empty,
            _chkCleared = new CheckBoxAdv
            {
                Text = "Check has cleared the bank",
                ThemeName = themeName,
                CheckState = CheckState.Unchecked,
                Font = new System.Drawing.Font("Segoe UI", 11F),
                Height = CONTROL_HEIGHT,
                AutoSize = true
            }, ROW_SPACING);

        // Wire up event handlers for checkbox/status synchronization
        _chkCleared.CheckedChanged += ChkCleared_CheckedChanged;
        _cmbStatus.SelectedIndexChanged += CmbStatus_SelectedIndexChanged;

        // === ACTION BUTTONS (48px top gap, right-aligned) ===
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, SECTION_GAP, 0, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false
        };

        _btnSave = new SfButton
        {
            Text = "Save",
            Width = 120,
            Height = 40,
            ThemeName = themeName,
            Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
            Margin = new Padding(8, 0, 0, 0),
            Image = LoadIcon("Save32")
        };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new SfButton
        {
            Text = "Cancel",
            Width = 120,
            Height = 40,
            ThemeName = themeName,
            Font = new System.Drawing.Font("Segoe UI", 11F),
            Margin = new Padding(8, 0, 0, 0),
            Image = LoadIcon("Close32")
        };
        _btnCancel.Click += (s, e) => ParentForm?.Close();

        _btnDelete = new SfButton
        {
            Text = "Delete",
            Width = 120,
            Height = 40,
            ThemeName = themeName,
            Font = new System.Drawing.Font("Segoe UI", 11F),
            Visible = false,
            Margin = new Padding(8, 0, 0, 0),
            Image = LoadIcon("Delete32")
        };
        _btnDelete.Click += BtnDelete_Click;

        buttonPanel.Controls.Add(_btnCancel);
        buttonPanel.Controls.Add(_btnDelete);
        buttonPanel.Controls.Add(_btnSave);

        mainLayout.Controls.Add(buttonPanel, 0, row);
        mainLayout.SetColumnSpan(buttonPanel, 2);
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        formPanel.Controls.Add(mainLayout);
        mainContainer.Controls.Add(formPanel, 0, 1);
        Controls.Add(mainContainer);

        // Wire up autocomplete filters
        _cmbPayee.Filter = FilterVendors;
        _cmbAccount.Filter = FilterAccounts;
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
            Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(0, 102, 204),
            AutoSize = true,
            Margin = new Padding(0, topMargin, 0, 16), // Consistent gap below header
            Padding = new Padding(0)
        };
        layout.Controls.Add(header, 0, row);
        layout.SetColumnSpan(header, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row++;
    }

    /// <summary>
    /// Adds a labeled control with consistent spacing
    /// </summary>
    private void AddLabeledControl(TableLayoutPanel layout, ref int row, string labelText, Control control, int bottomMargin)
    {
        if (!string.IsNullOrEmpty(labelText))
        {
            var label = new Label
            {
                Text = labelText + ":",
                Font = new System.Drawing.Font("Segoe UI", 11F),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Right,
                AutoSize = true,
                Padding = new Padding(0, 10, 12, 0) // Align vertically with 40px control
            };
            layout.Controls.Add(label, 0, row);
        }
        else
        {
            layout.Controls.Add(new Label { Text = string.Empty }, 0, row);
        }

        control.Margin = new Padding(0, 0, 0, bottomMargin);
        layout.Controls.Add(control, 1, row);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row++;
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

            // Create a simple dialog to add a new vendor
            var dialog = new Form
            {
                Text = "Add New Vendor",
                Width = 540,
                Height = 600,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                AutoScaleMode = AutoScaleMode.Dpi  // High-DPI display support
            };

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

            static Label CreateDialogLabel(string text)
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

            static TextBox CreateDialogTextBox(int maxLength)
            {
                return new TextBox
                {
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Segoe UI", 10F),
                    MaxLength = maxLength,
                    Margin = new Padding(0, 2, 0, 2),
                    MinimumSize = new System.Drawing.Size(0, 26)
                };
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

            var btnSaveVendor = new Button
            {
                Text = "Save",
                Width = 80,
                Height = 35,
                Font = new System.Drawing.Font("Segoe UI", 10F),
                DialogResult = DialogResult.OK
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 35,
                Font = new System.Drawing.Font("Segoe UI", 10F),
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(5, 0, 0, 0)
            };

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
                            Display = v.Name,
                            Vendor = v
                        }).ToList()
                    );

                    _cmbPayee.DataSource = null;
                    _cmbPayee.DataSource = vendorDisplayList;
                    _cmbPayee.DisplayMember = "Display";
                    _cmbPayee.ValueMember = "Vendor";

                    // Select the newly created vendor
                    var newIndex = vendorDisplayList.ToList().FindIndex(x => x.Vendor.Id == createdVendor.Id);
                    if (newIndex >= 0)
                    {
                        _cmbPayee.SelectedIndex = newIndex;
                    }

                    MessageBox.Show("Vendor created successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            dialog.Dispose();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error adding vendor");
            MessageBox.Show($"Error adding vendor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            IsBusy = true;

            // Validate
            if (string.IsNullOrWhiteSpace(_txtCheckNumber.Text))
            {
                MessageBox.Show("Check number is required", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_cmbPayee.SelectedItem == null && string.IsNullOrWhiteSpace(_cmbPayee.Text))
            {
                MessageBox.Show("Please select or enter a payee (vendor)", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_numAmount.Value <= 0)
            {
                MessageBox.Show("Amount must be greater than zero", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtDescription.Text))
            {
                MessageBox.Show("Description is required", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                payment.Payee = selectedVendor.Vendor.Name;
                payment.VendorId = selectedVendor.Vendor.Id;
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
            payment.Amount = (decimal)(_numAmount.Value ?? 0);
            payment.Description = _txtDescription.Text.Trim();
            payment.Memo = _txtMemo.Text.Trim();
            payment.Status = _cmbStatus.SelectedItem?.ToString() ?? "Pending";
            payment.IsCleared = payment.Status == "Cleared";

            // Set the associated account
            if (_cmbAccount.SelectedIndex >= 0 && _cmbAccount.SelectedItem is AccountDisplayItem selectedItem)
            {
                payment.MunicipalAccountId = selectedItem.Account.Id;
            }
            else
            {
                payment.MunicipalAccountId = null;
            }

            if (_isNew)
            {
                await repository.AddAsync(payment, CancellationToken.None);
                Logger?.LogInformation("PaymentEditPanel: Payment created successfully - CheckNumber: {CheckNumber}, Amount: {Amount}, Payee: {Payee}",
                    payment.CheckNumber, payment.Amount, payment.Payee);
                MessageBox.Show("Payment created successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                await repository.UpdateAsync(payment, CancellationToken.None);
                Logger?.LogInformation("PaymentEditPanel: Payment updated successfully - ID: {Id}, CheckNumber: {CheckNumber}, Amount: {Amount}",
                    payment.Id, payment.CheckNumber, payment.Amount);
                MessageBox.Show("Payment updated successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (ParentForm is Form parentForm)
            {
                parentForm.DialogResult = DialogResult.OK;
                Logger?.LogDebug("PaymentEditPanel: DialogResult set to OK");
            }
            ParentForm?.Close();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error saving payment");
            MessageBox.Show($"Error saving payment: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show("Cannot delete a new payment", "Invalid Operation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete payment {_existingPayment.CheckNumber} to {_existingPayment.Payee}?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
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

            MessageBox.Show("Payment deleted successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (ParentForm is Form parentForm)
            {
                parentForm.DialogResult = DialogResult.OK;
            }
            ParentForm?.Close();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error deleting payment");
            MessageBox.Show($"Error deleting payment: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _btnSave?.Dispose();
            _btnCancel?.Dispose();
            _btnDelete?.Dispose();
            _txtCheckNumber?.Dispose();
            _cmbPayee?.Dispose();
            _btnAddVendor?.Dispose();
            _txtDescription?.Dispose();
            _txtMemo?.Dispose();
            _dtpPaymentDate?.Dispose();
            _numAmount?.Dispose();
            _cmbStatus?.Dispose();
            _cmbAccount?.Dispose();
            _chkCleared?.Dispose();
        }
        base.Dispose(disposing);
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

                // Re-hook the event handler
                _chkCleared.CheckedChanged += ChkCleared_CheckedChanged;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "PaymentEditPanel: Error in CmbStatus_SelectedIndexChanged");
        }
    }
}
