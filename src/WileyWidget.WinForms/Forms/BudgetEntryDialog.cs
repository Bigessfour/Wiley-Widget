using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Dialog for creating or editing a <see cref="BudgetEntry"/>. Persists changes through <see cref="BudgetViewModel"/>.
    /// </summary>
    public sealed class BudgetEntryDialog : Form
    {
        private readonly BudgetViewModel _viewModel;
        private readonly BudgetEntry _entry;
        private readonly ILogger<BudgetEntryDialog>? _logger;
        private readonly CancellationTokenSource _cts = new();

        private readonly TextBox _accountNumber = new() { Width = 180 };
        private readonly TextBox _description = new() { Width = 260 };
        private readonly NumericUpDown _budgeted = new() { DecimalPlaces = 2, Maximum = 1000000000, Width = 120, ThousandsSeparator = true };
        private readonly NumericUpDown _actual = new() { DecimalPlaces = 2, Maximum = 1000000000, Width = 120, ThousandsSeparator = true };
        private readonly NumericUpDown _encumbrance = new() { DecimalPlaces = 2, Maximum = 1000000000, Width = 120, ThousandsSeparator = true };
        private readonly NumericUpDown _fiscalYear = new() { Minimum = 2000, Maximum = 2100, Width = 80, Increment = 1, Value = (decimal)DateTime.Now.Year };
        private readonly NumericUpDown _departmentId = new() { Minimum = 1, Maximum = 9999, Width = 80, Increment = 1 };
        private readonly NumericUpDown _fundId = new() { Minimum = 0, Maximum = 9999, Width = 80, Increment = 1 }; // 0 treated as null
        private readonly ComboBox _fundType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private readonly DateTimePicker _startDate = new() { Format = DateTimePickerFormat.Short, Width = 140 };
        private readonly DateTimePicker _endDate = new() { Format = DateTimePickerFormat.Short, Width = 140 };
        private readonly Button _saveButton = new() { Text = "Save", AutoSize = true, BackColor = ThemeColors.PrimaryAccent, ForeColor = Color.White };
        private readonly Button _cancelButton = new() { Text = "Cancel", AutoSize = true };
        private readonly ErrorProvider _errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

        public BudgetEntry ResultEntry { get; private set; }

        public BudgetEntryDialog(BudgetViewModel viewModel, BudgetEntry? entry = null, ILogger<BudgetEntryDialog>? logger = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _entry = entry != null ? Clone(entry) : CreateDefaultEntry();
            _logger = logger;
            ResultEntry = _entry;

            InitializeComponent();
            PopulateFields();
        }

        private void InitializeComponent()
        {
            ThemeColors.ApplyTheme(this);

            Text = _entry.Id == 0 ? "Add Budget Entry" : "Edit Budget Entry";
            Size = new Size(640, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            _fundType.Items.AddRange(Enum.GetNames<FundType>());
            if (_fundType.Items.Count > 0)
            {
                _fundType.SelectedIndex = 0;
            }

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 0,
                Padding = new Padding(12),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            AddRow(layout, "Account Number", _accountNumber);
            AddRow(layout, "Description", _description);
            AddRow(layout, "Budgeted", _budgeted);
            AddRow(layout, "Actual", _actual);
            AddRow(layout, "Encumbrance", _encumbrance);
            AddRow(layout, "Fiscal Year", _fiscalYear);
            AddRow(layout, "Department Id", _departmentId);
            AddRow(layout, "Fund Id", _fundId);
            AddRow(layout, "Fund Type", _fundType);

            var datesPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            datesPanel.Controls.Add(new Label { Text = "Start", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
            datesPanel.Controls.Add(_startDate);
            datesPanel.Controls.Add(new Label { Text = "End", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
            datesPanel.Controls.Add(_endDate);
            AddRow(layout, "Period", datesPanel);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            _saveButton.Click += async (_, _) => await SaveAsync();
            _cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(_cancelButton);
            buttons.Controls.Add(_saveButton);
            layout.Controls.Add(buttons, 0, layout.RowCount);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);

            FormClosing += (_, _) => _cts.Cancel();
        }

        private void AddRow(TableLayoutPanel layout, string label, Control control)
        {
            var row = layout.RowCount;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 8, 6) }, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        private void PopulateFields()
        {
            _accountNumber.Text = _entry.AccountNumber;
            _description.Text = _entry.Description;
            _budgeted.Value = ClampNumeric(_budgeted, _entry.BudgetedAmount);
            _actual.Value = ClampNumeric(_actual, _entry.ActualAmount);
            _encumbrance.Value = ClampNumeric(_encumbrance, _entry.EncumbranceAmount);
            _fiscalYear.Value = Math.Clamp(_entry.FiscalYear == 0 ? DateTime.Now.Year : _entry.FiscalYear, (int)_fiscalYear.Minimum, (int)_fiscalYear.Maximum);
            _departmentId.Value = Math.Clamp(_entry.DepartmentId, (int)_departmentId.Minimum, (int)_departmentId.Maximum);
            _fundId.Value = _entry.FundId.HasValue ? _entry.FundId.Value : 0;
            _fundType.SelectedItem = _entry.FundType.ToString();
            _startDate.Value = _entry.StartPeriod == default ? DateTime.Today : _entry.StartPeriod;
            _endDate.Value = _entry.EndPeriod == default ? DateTime.Today.AddMonths(1) : _entry.EndPeriod;
        }

        private static decimal ClampNumeric(NumericUpDown control, decimal value)
        {
            return Math.Min(control.Maximum, Math.Max(control.Minimum, value));
        }

        private async Task SaveAsync()
        {
            if (!ValidateInputs())
            {
                return;
            }

            ApplyInputToEntry(_entry);

            try
            {
                ToggleInputs(enabled: false);

                if (_entry.Id == 0)
                {
                    await _viewModel.AddEntryAsync(_entry, _cts.Token);
                }
                else
                {
                    await _viewModel.UpdateEntryAsync(_entry, _cts.Token);
                }

                await _viewModel.RefreshAnalysisCommand.ExecuteAsync(null);
                ResultEntry = _entry;
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException)
            {
                DialogResult = DialogResult.Cancel;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist budget entry");
                MessageBox.Show(this, $"Unable to save entry: {ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ToggleInputs(enabled: true);
            }
        }

        private void ApplyInputToEntry(BudgetEntry target)
        {
            target.AccountNumber = _accountNumber.Text.Trim();
            target.Description = _description.Text.Trim();
            target.BudgetedAmount = _budgeted.Value;
            target.ActualAmount = _actual.Value;
            target.EncumbranceAmount = _encumbrance.Value;
            target.Variance = target.BudgetedAmount - target.ActualAmount;
            target.FiscalYear = (int)_fiscalYear.Value;
            target.DepartmentId = (int)_departmentId.Value;
            target.FundId = _fundId.Value == 0 ? null : (int)_fundId.Value;
            target.FundType = Enum.TryParse<FundType>(_fundType.SelectedItem?.ToString(), ignoreCase: true, out var fundType)
                ? fundType
                : target.FundType;
            target.StartPeriod = _startDate.Value.Date;
            target.EndPeriod = _endDate.Value.Date;
        }

        private bool ValidateInputs()
        {
            _errors.Clear();
            var valid = true;

            if (string.IsNullOrWhiteSpace(_accountNumber.Text))
            {
                _errors.SetError(_accountNumber, "Account number is required");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(_description.Text))
            {
                _errors.SetError(_description, "Description is required");
                valid = false;
            }

            if (_endDate.Value.Date < _startDate.Value.Date)
            {
                _errors.SetError(_endDate, "End date must be on or after start date");
                valid = false;
            }

            return valid;
        }

        private void ToggleInputs(bool enabled)
        {
            _saveButton.Enabled = enabled;
            _cancelButton.Enabled = enabled;
            _accountNumber.Enabled = enabled;
            _description.Enabled = enabled;
            _budgeted.Enabled = enabled;
            _actual.Enabled = enabled;
            _encumbrance.Enabled = enabled;
            _fiscalYear.Enabled = enabled;
            _departmentId.Enabled = enabled;
            _fundId.Enabled = enabled;
            _fundType.Enabled = enabled;
            _startDate.Enabled = enabled;
            _endDate.Enabled = enabled;
        }

        private static BudgetEntry Clone(BudgetEntry entry)
        {
            return new BudgetEntry
            {
                Id = entry.Id,
                AccountNumber = entry.AccountNumber,
                Description = entry.Description,
                BudgetedAmount = entry.BudgetedAmount,
                ActualAmount = entry.ActualAmount,
                Variance = entry.Variance,
                ParentId = entry.ParentId,
                FiscalYear = entry.FiscalYear,
                StartPeriod = entry.StartPeriod,
                EndPeriod = entry.EndPeriod,
                FundType = entry.FundType,
                EncumbranceAmount = entry.EncumbranceAmount,
                IsGASBCompliant = entry.IsGASBCompliant,
                DepartmentId = entry.DepartmentId,
                FundId = entry.FundId,
                MunicipalAccountId = entry.MunicipalAccountId,
                SourceFilePath = entry.SourceFilePath,
                SourceRowNumber = entry.SourceRowNumber,
                ActivityCode = entry.ActivityCode,
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt
            };
        }

        private static BudgetEntry CreateDefaultEntry()
        {
            var today = DateTime.Today;
            return new BudgetEntry
            {
                FiscalYear = today.Year,
                StartPeriod = new DateTime(today.Year, 1, 1),
                EndPeriod = new DateTime(today.Year, 12, 31),
                FundType = FundType.GeneralFund,
                MunicipalAccountId = 0,
                DepartmentId = 1
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                _errors.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
