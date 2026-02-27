using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.ListView;
using WileyWidget.Models;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Dialog for editing customer information.
    /// </summary>
    public sealed class CustomerEditDialog : SfForm
    {
        private readonly ILogger? _logger;
        private readonly UtilityCustomer _customer;
        private TextBox? _accountNumberTextBox;
        private TextBox? _firstNameTextBox;
        private TextBox? _lastNameTextBox;
        private TextBox? _companyNameTextBox;
        private ComboBox? _customerTypeComboBox;
        private TextBox? _serviceAddressTextBox;
        private TextBox? _serviceCityTextBox;
        private TextBox? _serviceStateTextBox;
        private TextBox? _serviceZipCodeTextBox;
        private TextBox? _mailingAddressTextBox;
        private TextBox? _mailingCityTextBox;
        private TextBox? _mailingStateTextBox;
        private TextBox? _mailingZipCodeTextBox;
        private TextBox? _phoneNumberTextBox;
        private TextBox? _emailAddressTextBox;
        private TextBox? _meterNumberTextBox;
        private ComboBox? _serviceLocationComboBox;
        private ComboBox? _statusComboBox;
        private TextBox? _notesTextBox;
        private SfButton? _saveButton;
        private SfButton? _cancelButton;

        /// <summary>
        /// Gets whether the dialog was saved successfully.
        /// </summary>
        public bool IsSaved { get; private set; }

        /// <summary>
        /// Creates a customer edit dialog.
        /// </summary>
        /// <param name="customer">The customer to edit (will be modified if saved)</param>
        /// <param name="logger">Optional logger</param>
        public CustomerEditDialog(UtilityCustomer customer, ILogger? logger = null)
        {
            _logger = logger;
            _customer = customer ?? throw new ArgumentNullException(nameof(customer));

            InitializeDialog();
            LoadCustomerData();

            this.PerformLayout();
            this.Refresh();
            _logger?.LogDebug("[DIALOG] {DialogName} content anchored and refreshed", this.Name);
        }

        private void InitializeDialog()
        {
            Text = "Edit Customer";
            Size = new Size(760, 840);
            MinimumSize = new Size(720, 760);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            this.Style.Border = new Pen(SystemColors.WindowFrame, 1);
            this.Style.InactiveBorder = new Pen(SystemColors.GrayText, 1);

            // Apply theme
            WileyWidget.WinForms.Themes.ThemeColors.ApplyTheme(this);

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 20,
                Padding = new Padding(20),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            // Column styles
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Row styles
            for (int i = 0; i < 20; i++)
            {
                mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            }
            mainPanel.RowStyles[18] = new RowStyle(SizeType.Absolute, 96); // Notes

            int row = 0;

            // Account Number
            AddLabel(mainPanel, "Account #:", row, 0);
            _accountNumberTextBox = AddTextBox(mainPanel, row++, 1);
            _accountNumberTextBox.MaxLength = 20;
            _accountNumberTextBox.Width = 160;
            _accountNumberTextBox.MinimumSize = new Size(160, 32);

            // First Name
            AddLabel(mainPanel, "First Name:", row, 0);
            _firstNameTextBox = AddTextBox(mainPanel, row++, 1);
            _firstNameTextBox.MaxLength = 50;

            // Last Name
            AddLabel(mainPanel, "Last Name:", row, 0);
            _lastNameTextBox = AddTextBox(mainPanel, row++, 1);
            _lastNameTextBox.MaxLength = 50;

            // Company Name
            AddLabel(mainPanel, "Company:", row, 0);
            _companyNameTextBox = AddTextBox(mainPanel, row++, 1);
            _companyNameTextBox.MaxLength = 100;

            // Customer Type
            AddLabel(mainPanel, "Type:", row, 0);
            _customerTypeComboBox = AddComboBox(mainPanel, row++, 1);
            _customerTypeComboBox.Items.AddRange(Enum.GetNames(typeof(CustomerType)));

            // Service Address
            AddLabel(mainPanel, "Service Address:", row, 0);
            _serviceAddressTextBox = AddTextBox(mainPanel, row++, 1);
            _serviceAddressTextBox.MaxLength = 200;

            // Service City
            AddLabel(mainPanel, "Service City:", row, 0);
            _serviceCityTextBox = AddTextBox(mainPanel, row++, 1);
            _serviceCityTextBox.MaxLength = 50;

            // Service State
            AddLabel(mainPanel, "Service State:", row, 0);
            _serviceStateTextBox = AddTextBox(mainPanel, row++, 1);
            _serviceStateTextBox.MaxLength = 2;
            _serviceStateTextBox.Width = 80;
            _serviceStateTextBox.MinimumSize = new Size(80, 32);

            // Service ZIP
            AddLabel(mainPanel, "Service ZIP:", row, 0);
            _serviceZipCodeTextBox = AddTextBox(mainPanel, row++, 1);
            _serviceZipCodeTextBox.MaxLength = 10;
            _serviceZipCodeTextBox.Width = 165;
            _serviceZipCodeTextBox.MinimumSize = new Size(165, 32);

            // Mailing Address
            AddLabel(mainPanel, "Mailing Address:", row, 0);
            _mailingAddressTextBox = AddTextBox(mainPanel, row++, 1);
            _mailingAddressTextBox.MaxLength = 200;

            // Mailing City
            AddLabel(mainPanel, "Mailing City:", row, 0);
            _mailingCityTextBox = AddTextBox(mainPanel, row++, 1);
            _mailingCityTextBox.MaxLength = 50;

            // Mailing State
            AddLabel(mainPanel, "Mailing State:", row, 0);
            _mailingStateTextBox = AddTextBox(mainPanel, row++, 1);
            _mailingStateTextBox.MaxLength = 2;
            _mailingStateTextBox.Width = 80;
            _mailingStateTextBox.MinimumSize = new Size(80, 32);

            // Mailing ZIP
            AddLabel(mainPanel, "Mailing ZIP:", row, 0);
            _mailingZipCodeTextBox = AddTextBox(mainPanel, row++, 1);
            _mailingZipCodeTextBox.MaxLength = 10;
            _mailingZipCodeTextBox.Width = 165;
            _mailingZipCodeTextBox.MinimumSize = new Size(165, 32);

            // Phone
            AddLabel(mainPanel, "Phone:", row, 0);
            _phoneNumberTextBox = AddTextBox(mainPanel, row++, 1);
            _phoneNumberTextBox.MaxLength = 15;
            _phoneNumberTextBox.Width = 165;
            _phoneNumberTextBox.MinimumSize = new Size(165, 32);

            // Email
            AddLabel(mainPanel, "Email:", row, 0);
            _emailAddressTextBox = AddTextBox(mainPanel, row++, 1);
            _emailAddressTextBox.MaxLength = 100;

            // Meter Number
            AddLabel(mainPanel, "Meter #:", row, 0);
            _meterNumberTextBox = AddTextBox(mainPanel, row++, 1);
            _meterNumberTextBox.MaxLength = 20;
            _meterNumberTextBox.Width = 165;
            _meterNumberTextBox.MinimumSize = new Size(165, 32);

            // Service Location
            AddLabel(mainPanel, "Location:", row, 0);
            _serviceLocationComboBox = AddComboBox(mainPanel, row++, 1);
            _serviceLocationComboBox.Items.AddRange(Enum.GetNames(typeof(ServiceLocation)));

            // Status
            AddLabel(mainPanel, "Status:", row, 0);
            _statusComboBox = AddComboBox(mainPanel, row++, 1);
            _statusComboBox.Items.AddRange(Enum.GetNames(typeof(CustomerStatus)));

            // Notes
            AddLabel(mainPanel, "Notes:", row, 0);
            _notesTextBox = AddTextBox(mainPanel, row++, 1);
            _notesTextBox.Multiline = true;
            _notesTextBox.Dock = DockStyle.Fill;
            _notesTextBox.MinimumSize = new Size(320, 88);
            _notesTextBox.Height = 88;

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 56,
                Padding = new Padding(12)
            };

            _saveButton = new SfButton
            {
                Text = "Save",
                Width = 112,
                Height = 34
            };
            _saveButton.Click += SaveButton_Click;

            _cancelButton = new SfButton
            {
                Text = "Cancel",
                Width = 112,
                Height = 34
            };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_saveButton);

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            contentHost.Controls.Add(mainPanel);

            Controls.Add(contentHost);
            Controls.Add(buttonPanel);

            AcceptButton = _saveButton;
            CancelButton = _cancelButton;
        }

        private void AddLabel(TableLayoutPanel panel, string text, int row, int col)
        {
            var label = new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(label, col, row);
        }

        private TextBox AddTextBox(TableLayoutPanel panel, int row, int col)
        {
            var textBox = new TextBox
            {
                AutoSize = false,
                Anchor = AnchorStyles.Left,
                Width = 320,
                Height = 32,
                MinimumSize = new Size(320, 32)
            };
            panel.Controls.Add(textBox, col, row);
            return textBox;
        }

        private ComboBox AddComboBox(TableLayoutPanel panel, int row, int col)
        {
            var comboBox = new ComboBox
            {
                Anchor = AnchorStyles.Left,
                Width = 320,
                Height = 32,
                MinimumSize = new Size(320, 32),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            panel.Controls.Add(comboBox, col, row);
            return comboBox;
        }

        private void LoadCustomerData()
        {
            _accountNumberTextBox!.Text = _customer.AccountNumber;
            _firstNameTextBox!.Text = _customer.FirstName;
            _lastNameTextBox!.Text = _customer.LastName;
            _companyNameTextBox!.Text = _customer.CompanyName ?? string.Empty;
            _customerTypeComboBox!.SelectedItem = _customer.CustomerType.ToString();
            _serviceAddressTextBox!.Text = _customer.ServiceAddress;
            _serviceCityTextBox!.Text = _customer.ServiceCity;
            _serviceStateTextBox!.Text = _customer.ServiceState;
            _serviceZipCodeTextBox!.Text = _customer.ServiceZipCode;
            _mailingAddressTextBox!.Text = _customer.MailingAddress ?? string.Empty;
            _mailingCityTextBox!.Text = _customer.MailingCity ?? string.Empty;
            _mailingStateTextBox!.Text = _customer.MailingState ?? string.Empty;
            _mailingZipCodeTextBox!.Text = _customer.MailingZipCode ?? string.Empty;
            _phoneNumberTextBox!.Text = _customer.PhoneNumber ?? string.Empty;
            _emailAddressTextBox!.Text = _customer.EmailAddress ?? string.Empty;
            _meterNumberTextBox!.Text = _customer.MeterNumber ?? string.Empty;
            _serviceLocationComboBox!.SelectedItem = _customer.ServiceLocation.ToString();
            _statusComboBox!.SelectedItem = _customer.Status.ToString();
            _notesTextBox!.Text = _customer.Notes ?? string.Empty;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Update customer properties
                _customer.AccountNumber = _accountNumberTextBox!.Text.Trim();
                _customer.FirstName = _firstNameTextBox!.Text.Trim();
                _customer.LastName = _lastNameTextBox!.Text.Trim();
                _customer.CompanyName = string.IsNullOrWhiteSpace(_companyNameTextBox!.Text) ? null : _companyNameTextBox.Text.Trim();
                _customer.CustomerType = (CustomerType)Enum.Parse(typeof(CustomerType), _customerTypeComboBox!.SelectedItem!.ToString()!);
                _customer.ServiceAddress = _serviceAddressTextBox!.Text.Trim();
                _customer.ServiceCity = _serviceCityTextBox!.Text.Trim();
                _customer.ServiceState = _serviceStateTextBox!.Text.Trim();
                _customer.ServiceZipCode = _serviceZipCodeTextBox!.Text.Trim();
                _customer.MailingAddress = string.IsNullOrWhiteSpace(_mailingAddressTextBox!.Text) ? null : _mailingAddressTextBox.Text.Trim();
                _customer.MailingCity = string.IsNullOrWhiteSpace(_mailingCityTextBox!.Text) ? null : _mailingCityTextBox.Text.Trim();
                _customer.MailingState = string.IsNullOrWhiteSpace(_mailingStateTextBox!.Text) ? null : _mailingStateTextBox.Text.Trim();
                _customer.MailingZipCode = string.IsNullOrWhiteSpace(_mailingZipCodeTextBox!.Text) ? null : _mailingZipCodeTextBox.Text.Trim();
                _customer.PhoneNumber = string.IsNullOrWhiteSpace(_phoneNumberTextBox!.Text) ? null : _phoneNumberTextBox.Text.Trim();
                _customer.EmailAddress = string.IsNullOrWhiteSpace(_emailAddressTextBox!.Text) ? null : _emailAddressTextBox.Text.Trim();
                _customer.MeterNumber = string.IsNullOrWhiteSpace(_meterNumberTextBox!.Text) ? null : _meterNumberTextBox.Text.Trim();
                _customer.ServiceLocation = (ServiceLocation)Enum.Parse(typeof(ServiceLocation), _serviceLocationComboBox!.SelectedItem!.ToString()!);
                _customer.Status = (CustomerStatus)Enum.Parse(typeof(CustomerStatus), _statusComboBox!.SelectedItem!.ToString()!);
                _customer.Notes = string.IsNullOrWhiteSpace(_notesTextBox!.Text) ? null : _notesTextBox.Text.Trim();

                // Validate
                var validationResults = _customer.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(_customer));
                if (validationResults.Any())
                {
                    var errors = validationResults.Select(vr => vr.ErrorMessage ?? "Unknown error").ToList();
                    ValidationDialog.Show(this, "Validation Error", "Customer data validation failed:", errors, _logger);
                    return;
                }

                IsSaved = true;
                DialogResult = DialogResult.OK;
                Close();

                _logger?.LogInformation("Customer {Account} updated successfully", _customer.AccountNumber);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save customer changes");
                MessageBox.Show($"Failed to save customer: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
