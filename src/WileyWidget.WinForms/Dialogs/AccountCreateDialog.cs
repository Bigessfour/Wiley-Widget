using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Models;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Dialog for creating a new municipal account.
    /// Captures essential account properties and validates before saving.
    /// </summary>
    public sealed class AccountCreateDialog : Form
    {
        private readonly ILogger? _logger;
        private TextBox? _accountNumberTextBox;
        private TextBox? _accountNameTextBox;
        private ComboBox? _fundTypeComboBox;
        private ComboBox? _accountTypeComboBox;
        private TextBox? _descriptionTextBox;
        private TextBox? _departmentTextBox;
        private NumericUpDown? _budgetAmountNumeric;
        private SfButton? _createButton;
        private SfButton? _cancelButton;

        /// <summary>
        /// Gets the created account if successfully saved, otherwise null.
        /// </summary>
        public MunicipalAccount? CreatedAccount { get; private set; }

        /// <summary>
        /// Gets whether the dialog was saved successfully.
        /// </summary>
        public bool IsSaved { get; private set; }

        /// <summary>
        /// Creates a new account creation dialog.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics</param>
        public AccountCreateDialog(ILogger? logger = null)
        {
            _logger = logger;
            InitializeDialog();
            ThemeColors.ApplyTheme(this);

            this.PerformLayout();
            this.Refresh();
            _logger?.LogDebug("[DIALOG] {DialogName} content anchored and refreshed", this.Name);
        }

        private void InitializeDialog()
        {
            Text = "Create New Municipal Account";
            Size = new Size(500, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(10)
            };

            // Column styles
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Row styles
            for (int i = 0; i < 8; i++)
            {
                mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            }

            int row = 0;

            // Account Number
            AddLabel(mainPanel, "Account Number *:", row, 0);
            _accountNumberTextBox = AddTextBox(mainPanel, row++, 1);
            _accountNumberTextBox.MaxLength = 20;

            // Account Name
            AddLabel(mainPanel, "Account Name *:", row, 0);
            _accountNameTextBox = AddTextBox(mainPanel, row++, 1);
            _accountNameTextBox.MaxLength = 100;

            // Fund Type
            AddLabel(mainPanel, "Fund Type *:", row, 0);
            _fundTypeComboBox = AddComboBox(mainPanel, row++, 1);
            PopulateFundTypes(_fundTypeComboBox);

            // Account Type
            AddLabel(mainPanel, "Account Type *:", row, 0);
            _accountTypeComboBox = AddComboBox(mainPanel, row++, 1);
            PopulateAccountTypes(_accountTypeComboBox);

            // Description
            AddLabel(mainPanel, "Description:", row, 0);
            _descriptionTextBox = AddTextBox(mainPanel, row++, 1);
            _descriptionTextBox.MaxLength = 500;

            // Department
            AddLabel(mainPanel, "Department:", row, 0);
            _departmentTextBox = AddTextBox(mainPanel, row++, 1);
            _departmentTextBox.MaxLength = 100;

            // Budget Amount
            AddLabel(mainPanel, "Budget Amount:", row, 0);
            _budgetAmountNumeric = AddNumericUpDown(mainPanel, row++, 1);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            _createButton = new SfButton
            {
                Text = "Create",
                Width = 80,
                Height = 30
            };
            _createButton.Click += CreateButton_Click;

            _cancelButton = new SfButton
            {
                Text = "Cancel",
                Width = 80,
                Height = 30
            };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_createButton);

            Controls.Add(mainPanel);
            Controls.Add(buttonPanel);

            AcceptButton = _createButton;
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
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(textBox, col, row);
            return textBox;
        }

        private ComboBox AddComboBox(TableLayoutPanel panel, int row, int col)
        {
            var comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            panel.Controls.Add(comboBox, col, row);
            return comboBox;
        }

        private NumericUpDown AddNumericUpDown(TableLayoutPanel panel, int row, int col)
        {
            var numeric = new NumericUpDownExt
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = decimal.MaxValue,
                DecimalPlaces = 2
            };
            panel.Controls.Add(numeric, col, row);
            return numeric;
        }

        private void PopulateFundTypes(ComboBox comboBox)
        {
            var funds = Enum.GetNames(typeof(MunicipalFundType));
            comboBox.Items.AddRange(funds);
            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private void PopulateAccountTypes(ComboBox comboBox)
        {
            var types = Enum.GetNames(typeof(AccountType));
            comboBox.Items.AddRange(types);
            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private void CreateButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Validate required fields
                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(_accountNumberTextBox?.Text))
                    errors.Add("Account Number is required");

                if (string.IsNullOrWhiteSpace(_accountNameTextBox?.Text))
                    errors.Add("Account Name is required");

                if (_fundTypeComboBox?.SelectedIndex == -1)
                    errors.Add("Fund Type must be selected");

                if (_accountTypeComboBox?.SelectedIndex == -1)
                    errors.Add("Account Type must be selected");

                if (errors.Any())
                {
                    ValidationDialog.Show(this, "Validation Error", "Please fix the following issues:", errors, _logger);
                    return;
                }

                // Create new account with validated values
                var fundType = (MunicipalFundType)Enum.Parse(typeof(MunicipalFundType), _fundTypeComboBox!.SelectedItem!.ToString()!);
                var accountType = (AccountType)Enum.Parse(typeof(AccountType), _accountTypeComboBox!.SelectedItem!.ToString()!);

                CreatedAccount = new MunicipalAccount
                {
                    Id = 0, // Will be assigned by DB
                    AccountNumber = new AccountNumber(_accountNumberTextBox!.Text.Trim()),
                    Name = _accountNameTextBox!.Text.Trim(),
                    Fund = fundType,
                    Type = accountType,
                    FundDescription = string.IsNullOrWhiteSpace(_descriptionTextBox?.Text) ? string.Empty : _descriptionTextBox.Text.Trim(),
                    Department = new Department { Name = _departmentTextBox?.Text?.Trim() ?? string.Empty },
                    BudgetAmount = _budgetAmountNumeric?.Value ?? 0m,
                    Balance = 0m,
                    IsActive = true
                };

                IsSaved = true;
                DialogResult = DialogResult.OK;
                Close();

                _logger?.LogInformation("Account {AccountNumber} created successfully", CreatedAccount.AccountNumber?.Value);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create account");
                MessageBox.Show($"Failed to create account: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _accountNumberTextBox?.Dispose();
                _accountNameTextBox?.Dispose();
                _fundTypeComboBox?.Dispose();
                _accountTypeComboBox?.Dispose();
                _descriptionTextBox?.Dispose();
                _departmentTextBox?.Dispose();
                _budgetAmountNumeric?.Dispose();
                _createButton?.Dispose();
                _cancelButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
