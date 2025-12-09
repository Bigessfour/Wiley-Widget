using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using WileyWidget.Models.Entities;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Simple Add/Edit dialog for BudgetEntry. Designed to be created manually from parent forms.
    /// Follows existing project pattern for small dialogs (constructed on demand, not DI-registered).
    /// </summary>
    public class BudgetEntryDialog : Form
    {
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BudgetEntryDialog> _logger;

        private TextBox txtAccountNumber = null!;
        private NumericUpDown nudBudgetedAmount = null!;
        private NumericUpDown nudActualAmount = null!;
        private NumericUpDown nudFiscalYear = null!;
        private ComboBox cbDepartment = null!;
        private ComboBox cbFund = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        private CancellationTokenSource? _cts;

        private List<Department> _departments = new();
        private List<Fund> _funds = new();

        public BudgetEntry Entry { get; private set; }

        public BudgetEntryDialog(IDepartmentRepository departmentRepository, IServiceScopeFactory scopeFactory, ILogger<BudgetEntryDialog> logger, BudgetEntry? entry = null)
        {
            InitializeComponent();

            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Entry = entry != null ? Clone(entry) : new BudgetEntry { CreatedAt = DateTime.UtcNow };
            _cts = new CancellationTokenSource();

            Load += async (s, e) =>
            {
                try
                {
                    await LoadReferenceDataAsync(_cts.Token);
                    PopulateFromEntry();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load reference data for BudgetEntryDialog");
                    MessageBox.Show(this, "Failed to load departments or funds: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            FormClosing += (s, e) => { AsyncEventHelperCancel(); };
        }

        private static BudgetEntry Clone(BudgetEntry src)
        {
            return new BudgetEntry
            {
                Id = src.Id,
                AccountNumber = src.AccountNumber,
                BudgetedAmount = src.BudgetedAmount,
                ActualAmount = src.ActualAmount,
                FiscalYear = src.FiscalYear,
                EncumbranceAmount = src.EncumbranceAmount,
                DepartmentId = src.DepartmentId,
                FundId = src.FundId,
                CreatedAt = src.CreatedAt
            };
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = "Budget Entry";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(480, 340);

            var lblAccount = new Label { Text = "Account number", Left = 12, Top = 15, Width = 120 };
            txtAccountNumber = new TextBox { Left = 140, Top = 12, Width = 320 };

            var lblBudgeted = new Label { Text = "Budgeted amount", Left = 12, Top = 55, Width = 120 };
            nudBudgetedAmount = new NumericUpDown { Left = 140, Top = 52, Width = 140, DecimalPlaces = 2, Maximum = 1000000000, Minimum = 0 };

            var lblActual = new Label { Text = "Actual amount", Left = 12, Top = 95, Width = 120 };
            nudActualAmount = new NumericUpDown { Left = 140, Top = 92, Width = 140, DecimalPlaces = 2, Maximum = 1000000000, Minimum = 0 };

            var lblYear = new Label { Text = "Fiscal year", Left = 12, Top = 135, Width = 120 };
            nudFiscalYear = new NumericUpDown { Left = 140, Top = 132, Width = 100, Minimum = 2000, Maximum = 3000, Value = DateTime.Now.Year };

            var lblDepartment = new Label { Text = "Department", Left = 12, Top = 175, Width = 120 };
            cbDepartment = new ComboBox { Left = 140, Top = 172, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };

            var lblFund = new Label { Text = "Fund", Left = 12, Top = 215, Width = 120 };
            cbFund = new ComboBox { Left = 140, Top = 212, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };

            btnOk = new Button { Text = "OK", Left = 300, Width = 80, Top = 270, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 390, Width = 80, Top = 270, DialogResult = DialogResult.Cancel };

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (s, e) => Close();

            Controls.Add(lblAccount);
            Controls.Add(txtAccountNumber);
            Controls.Add(lblBudgeted);
            Controls.Add(nudBudgetedAmount);
            Controls.Add(lblActual);
            Controls.Add(nudActualAmount);
            Controls.Add(lblYear);
            Controls.Add(nudFiscalYear);
            Controls.Add(lblDepartment);
            Controls.Add(cbDepartment);
            Controls.Add(lblFund);
            Controls.Add(cbFund);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            ResumeLayout(false);
            PerformLayout();
        }

        private void PopulateFromEntry()
        {
            if (Entry == null) return;
            txtAccountNumber.Text = Entry.AccountNumber ?? string.Empty;
            nudBudgetedAmount.Value = CoerceDecimal(Entry.BudgetedAmount);
            nudActualAmount.Value = CoerceDecimal(Entry.ActualAmount);
            nudFiscalYear.Value = Entry.FiscalYear != 0 ? Entry.FiscalYear : DateTime.Now.Year;

            // Select department and fund if loaded
            try
            {
                if (cbDepartment != null && Entry.DepartmentId > 0)
                {
                    cbDepartment.SelectedValue = Entry.DepartmentId;
                }

                if (cbFund != null && Entry.FundId.HasValue)
                {
                    cbFund.SelectedValue = Entry.FundId.Value;
                }
            }
            catch { }
        }

        private async Task LoadReferenceDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Load departments
                _departments = (await _departmentRepository.GetAllAsync()).ToList();

                // Load funds from DbContext
                using var scope = _scopeFactory.CreateScope();
                var context = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scope.ServiceProvider);
                _funds = await context.Funds.OrderBy(f => f.Name).AsNoTracking().ToListAsync(cancellationToken);

                // Update UI on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        cbDepartment.DataSource = _departments;
                        cbFund.DataSource = _funds;
                    }));
                }
                else
                {
                    cbDepartment.DataSource = _departments;
                    cbFund.DataSource = _funds;
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected, don't log as error
                _logger.LogDebug("LoadReferenceDataAsync was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load reference data for BudgetEntryDialog");
                if (InvokeRequired)
                {
                    Invoke(new Action(() => MessageBox.Show(this, $"Failed to load reference data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
                else
                {
                    MessageBox.Show(this, $"Failed to load reference data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void AsyncEventHelperCancel()
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }
            }
            catch { }
        }

        private decimal CoerceDecimal(decimal value)
        {
            if (value < nudBudgetedAmount.Minimum) return nudBudgetedAmount.Minimum;
            if (value > nudBudgetedAmount.Maximum) return nudBudgetedAmount.Maximum;
            return value;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(txtAccountNumber.Text))
            {
                MessageBox.Show(this, "Account number is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Entry.AccountNumber = txtAccountNumber.Text.Trim();
            Entry.BudgetedAmount = nudBudgetedAmount.Value;
            Entry.ActualAmount = nudActualAmount.Value;
            Entry.FiscalYear = (int)nudFiscalYear.Value;
            try
            {
                if (cbDepartment != null && cbDepartment.SelectedValue is int deptId)
                {
                    Entry.DepartmentId = deptId;
                }

                if (cbFund != null && cbFund.SelectedValue is int fundId)
                {
                    Entry.FundId = fundId;
                }
            }
            catch { }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
