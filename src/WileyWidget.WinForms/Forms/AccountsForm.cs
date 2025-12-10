using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Controls;
using Syncfusion.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

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
        private SfDataGrid? _dataGrid;
        private ToolStrip? _toolStrip;
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _statusLabel;
        private SfButton? _loadButton;
        private SfButton? _filterButton;

        public AccountsForm(AccountsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            ThemeColors.ApplyTheme(this);
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
            MinimumSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Create toolbar with Syncfusion buttons
            _toolStrip = new ToolStrip
            {
                Name = "toolStripMain",
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(8, 4, 8, 4)
            };

            // Create button container panel for Syncfusion buttons
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8)
            };

            _loadButton = new SfButton
            {
                Name = "btnLoad",
                Text = Resources.LoadAccountsButton,
                Size = new Size(120, 32),
                Location = new Point(8, 8),
                AccessibleName = "Load accounts button",
                AccessibleDescription = "Load municipal accounts from database",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _loadButton.Click += async (s, e) => await LoadData();

            _filterButton = new SfButton
            {
                Name = "btnFilter",
                Text = Resources.ApplyFiltersButton,
                Size = new Size(120, 32),
                Location = new Point(136, 8),
                AccessibleName = "Apply filters button",
                AccessibleDescription = "Apply filters to account list",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _filterButton.Click += async (s, e) => await _viewModel.FilterAccountsCommand.ExecuteAsync(null);

            buttonPanel.Controls.Add(_loadButton);
            buttonPanel.Controls.Add(_filterButton);

            // Create status strip
            _statusStrip = new StatusStrip
            {
                Name = "statusStripMain",
                Dock = DockStyle.Bottom
            };
            _statusLabel = new ToolStripStatusLabel
            {
                Name = "statusLabel",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _statusStrip.Items.Add(_statusLabel);

            // Bind status
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.IsLoading) && _statusLabel != null)
                {
                    _statusLabel.Text = _viewModel.IsLoading ? Resources.LoadingText : $"{_viewModel.ActiveAccountCount} accounts loaded. Total Balance: {_viewModel.TotalBalance:C}";
                }
            };

            Controls.Add(buttonPanel);
            Controls.Add(_statusStrip);
        }

        private void SetupDataGrid()
        {
            _dataGrid = new SfDataGrid
            {
                Name = "dataGridAccounts",
                Dock = DockStyle.Fill,
                AccessibleName = "Municipal accounts data grid",
                AccessibleDescription = "Data grid displaying municipal accounts with sorting, filtering, and selection capabilities",
                AutoGenerateColumns = false,
                AllowResizingColumns = true,
                AllowSorting = true,
                AllowFiltering = true,
                SelectionMode = GridSelectionMode.Single,
                RowHeight = 32,
                HeaderRowHeight = 36
            };

            // Configure columns with proper Syncfusion types and formatting
            _dataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "AccountNumber",
                HeaderText = Resources.AccountNumberHeader,
                Width = 120,
                AllowSorting = true,
                AllowFiltering = true
            });

            _dataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Name",
                HeaderText = Resources.AccountNameHeader,
                Width = 200,
                AllowSorting = true,
                AllowFiltering = true
            });

            _dataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Description",
                HeaderText = Resources.DescriptionHeader,
                Width = 250,
                AllowSorting = true,
                AllowFiltering = true
            });

            _dataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Type",
                HeaderText = Resources.TypeHeader,
                Width = 100,
                AllowSorting = true,
                AllowFiltering = true
            });

            _dataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Fund",
                HeaderText = Resources.FundHeader,
                Width = 100,
                AllowSorting = true,
                AllowFiltering = true
            });

            _dataGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "Balance",
                HeaderText = Resources.BalanceHeader,
                Width = 140,
                AllowSorting = true,
                AllowFiltering = true,
                Format = "C2",
                NumberFormatInfo = System.Globalization.CultureInfo.GetCultureInfo("en-US").NumberFormat
            });

            _dataGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "BudgetAmount",
                HeaderText = Resources.BudgetAmountHeader,
                Width = 140,
                AllowSorting = true,
                AllowFiltering = true,
                Format = "C2",
                NumberFormatInfo = System.Globalization.CultureInfo.GetCultureInfo("en-US").NumberFormat
            });

            _dataGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Department",
                HeaderText = Resources.DepartmentHeader,
                Width = 150,
                AllowSorting = true,
                AllowFiltering = true
            });

            _dataGrid.Columns.Add(new GridCheckBoxColumn
            {
                MappingName = "IsActive",
                HeaderText = Resources.ActiveHeader,
                Width = 80,
                AllowSorting = true,
                AllowFiltering = true
            });

            _dataGrid.Columns.Add(new GridCheckBoxColumn
            {
                MappingName = "HasParent",
                HeaderText = Resources.HasParentHeader,
                Width = 100,
                AllowSorting = true,
                AllowFiltering = true
            });
            
            // Add summary row for balance totals
            var summaryRow = new GridTableSummaryRow
            {
                Name = "TotalSummary",
                ShowSummaryInRow = false,
                Position = VerticalPosition.Bottom
            };
            summaryRow.SummaryColumns.Add(new GridSummaryColumn
            {
                Name = "BalanceTotal",
                MappingName = "Balance",
                SummaryType = SummaryType.DoubleAggregate,
                Format = "Total: {Sum:C2}"
            });
            summaryRow.SummaryColumns.Add(new GridSummaryColumn
            {
                Name = "BudgetTotal",
                MappingName = "BudgetAmount",
                SummaryType = SummaryType.DoubleAggregate,
                Format = "Total: {Sum:C2}"
            });
            _dataGrid.TableSummaryRows.Add(summaryRow);

            // Configure alternating row style via event handler
            _dataGrid.QueryRowStyle += DataGrid_QueryRowStyle;

            Controls.Add(_dataGrid);
        }

        private void DataGrid_QueryRowStyle(object? sender, Syncfusion.WinForms.DataGrid.Events.QueryRowStyleEventArgs e)
        {
            if (e.RowType == Syncfusion.WinForms.DataGrid.Enums.RowType.DefaultRow)
            {
                // Alternate row colors for better readability
                if (e.RowIndex % 2 == 0)
                {
                    e.Style.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
                }
            }
        }

        private async Task LoadData()
        {
            if (_dataGrid == null || _statusLabel == null)
                return;

            try
            {
                await _viewModel.LoadAccountsCommand.ExecuteAsync(null);
                var bindingSource = new BindingSource { DataSource = _viewModel.Accounts };
                _dataGrid.DataSource = bindingSource;
                _statusLabel.Text = $"{_viewModel.ActiveAccountCount} accounts loaded. Total Balance: {_viewModel.TotalBalance:C}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, Resources.LoadErrorMessage, ex.Message),
                    Resources.ErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dataGrid?.Dispose();
                _loadButton?.Dispose();
                _filterButton?.Dispose();
                _toolStrip?.Dispose();
                _statusStrip?.Dispose();
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
