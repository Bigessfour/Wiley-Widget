using Microsoft.Extensions.DependencyInjection;
// using Syncfusion.WinForms.DataGrid;  // Uncomment when Syncfusion packages are installed
// using Syncfusion.WinForms.DataGrid.Enums;  // Uncomment when Syncfusion packages are installed
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    internal static class Resources
    {
        public const string FormTitle = "Municipal Accounts";
        public const string LoadAccountsButton = "Load Accounts";
        public const string ApplyFiltersButton = "Apply Filters";
        public const string LoadingText = "Loading...";
        public const string AccountNumberHeader = "Account Number";
        public const string AccountNameHeader = "Account Name";
        public const string DescriptionHeader = "Description";
        public const string TypeHeader = "Type";
        public const string FundHeader = "Fund";
        public const string BalanceHeader = "Balance";
        public const string BudgetAmountHeader = "Budget Amount";
        public const string DepartmentHeader = "Department";
        public const string ActiveHeader = "Active";
        public const string HasParentHeader = "Has Parent";
        public const string ErrorTitle = "Error";
        public const string LoadErrorMessage = "Error loading accounts: {0}";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class AccountsForm : Form
    {
        private readonly AccountsViewModel _viewModel;
        // private SfDataGrid _dataGrid;  // Uncomment when Syncfusion packages are installed
        private DataGridView? _dataGrid;  // Temporary replacement, nullable to fix CS8618

        public AccountsForm(AccountsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            SetupDataGrid();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            LoadData();
#pragma warning restore CS4014
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Forms.Form.set_Text")]
        private void InitializeComponent()
        {
            Text = Resources.FormTitle;
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;

            // Create toolbar
            var toolStrip = new ToolStrip();
            var loadButton = new ToolStripButton(Resources.LoadAccountsButton, null, async (s, e) => await LoadData());
            var filterButton = new ToolStripButton(Resources.ApplyFiltersButton, null, async (s, e) => await _viewModel.FilterAccountsCommand.ExecuteAsync(null));

            toolStrip.Items.AddRange(new ToolStripItem[] { loadButton, filterButton });

            // Create status strip
            var statusStrip = new StatusStrip();
            var statusLabel = new ToolStripStatusLabel();
            statusStrip.Items.Add(statusLabel);

            // Bind status
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.IsLoading))
                {
                    statusLabel.Text = _viewModel.IsLoading ? Resources.LoadingText : $"{_viewModel.ActiveAccountCount} accounts loaded. Total Balance: {_viewModel.TotalBalance:C}";
                }
            };

            Controls.AddRange(new Control[] { toolStrip, statusStrip });
        }

        private void SetupDataGrid()
        {
            _dataGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, 50), // Below toolbar
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            // Configure columns
            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "AccountNumber",
                HeaderText = Resources.AccountNumberHeader,
                DataPropertyName = "AccountNumber",
                Width = 120
            });

            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = Resources.AccountNameHeader,
                DataPropertyName = "Name",
                Width = 200
            });

            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Description",
                HeaderText = Resources.DescriptionHeader,
                DataPropertyName = "Description",
                Width = 250
            });

            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Type",
                HeaderText = Resources.TypeHeader,
                DataPropertyName = "Type",
                Width = 100
            });

            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Fund",
                HeaderText = Resources.FundHeader,
                DataPropertyName = "Fund",
                Width = 100
            });

            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Balance",
                HeaderText = Resources.BalanceHeader,
                DataPropertyName = "Balance",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }
            });

            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "BudgetAmount",
                HeaderText = Resources.BudgetAmountHeader,
                DataPropertyName = "BudgetAmount",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }
            });

            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Department",
                HeaderText = Resources.DepartmentHeader,
                DataPropertyName = "Department",
                Width = 150
            });

            _dataGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "IsActive",
                HeaderText = Resources.ActiveHeader,
                DataPropertyName = "IsActive",
                Width = 80
            });

            _dataGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "HasParent",
                HeaderText = Resources.HasParentHeader,
                DataPropertyName = "HasParent",
                Width = 100
            });

            Controls.Add(_dataGrid);
        }

        private async Task LoadData()
        {
            try
            {
                await _viewModel.LoadAccountsCommand.ExecuteAsync(null);
                _dataGrid!.DataSource = new BindingSource { DataSource = _viewModel.Accounts };
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.LoadErrorMessage, ex.Message), Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dataGrid?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // DataGridView handles docking automatically
        }
    }
}
