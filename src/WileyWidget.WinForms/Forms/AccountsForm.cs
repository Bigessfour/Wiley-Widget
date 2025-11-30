using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.ListView;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Extensions;

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

    public partial class AccountsForm : Form
    {
        private readonly AccountsViewModel _viewModel;
        private SfDataGrid _dataGrid;

        public AccountsForm(AccountsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            SetupDataGrid();
#pragma warning disable CS4014
            LoadData();
#pragma warning restore CS4014
        }

        private void InitializeComponent()
        {
            Text = Resources.FormTitle;
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;

            var toolStrip = new ToolStrip();
            var loadButton = new ToolStripButton(Resources.LoadAccountsButton, null, async (s, e) => await LoadData());
            var filterButton = new ToolStripButton(Resources.ApplyFiltersButton, null, async (s, e) => await _viewModel.FilterAccountsCommand.ExecuteAsync(null));
            toolStrip.Items.AddRange(new ToolStripItem[] { loadButton, filterButton });

            var statusStrip = new StatusStrip();
            var statusLabel = new ToolStripStatusLabel();
            statusStrip.Items.Add(statusLabel);

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
            _dataGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, 50),
                AutoGenerateColumns = false,
                AllowEditing = false,
                SelectionMode = Syncfusion.WinForms.DataGrid.Enums.GridSelectionMode.Single
            };

            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
            {
                MappingName = "AccountNumber",
                HeaderText = Resources.AccountNumberHeader,
                Width = 120
            });

            _dataGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn
            {
                MappingName = "Name",
                HeaderText = Resources.AccountNameHeader,
                Width = 200
            });

            Controls.Add(_dataGrid);
        }

        private async Task LoadData()
        {
            try
            {
                await _viewModel.LoadAccountsCommand.ExecuteAsync(null);
                _dataGrid.DataSource = _viewModel.Accounts;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.LoadErrorMessage, ex.Message), Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Helper: enumerate all child controls recursively
        private static IEnumerable<Control> GetAllControls(Control root)
        {
            if (root == null) yield break;
            var stack = new Stack<Control>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                yield return c;
                foreach (Control child in c.Controls)
                {
                    stack.Push(child);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Defensive cleanup for Syncfusion controls using centralized helpers
                try
                {
                    foreach (var ctrl in GetAllControls(this))
                    {
                        // Try to clear any DataSource before disposing (defensive)
                        try { ctrl.SafeClearDataSource(); } catch { }

                        // Try to dispose controls safely — SafeDispose will handle known NRE/ObjectDisposed scenarios
                        try { ctrl.SafeDispose(); } catch { }
                    }

                    // Ensure the main grid is cleared and disposed via the helpers
                    try { _dataGrid.SafeClearDataSource(); } catch { }
                    try { _dataGrid.SafeDispose(); } catch { }
                }
                catch (Exception ex)
                {
                    try { Serilog.Log.Warning(ex, "AccountsForm.Dispose: suppressed exception during cleanup"); } catch { }
                }
            }

            base.Dispose(disposing);
        }
    }
}
