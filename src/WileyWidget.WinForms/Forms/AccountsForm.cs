using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Data;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
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
        private TableLayoutPanel? _mainLayout;
        private SfDataGrid? _dataGrid;
        private ToolStrip? _toolbar;
        private StatusStrip? _statusBar;
        private ToolStripStatusLabel? _statusPanel;
        private ToolStripStatusLabel? _totalsPanel;
        private ToolStripStatusLabel? _selectionPanel;
        private ToolStripButton? _editToggleButton;
        private BindingSource? _accountsBinding;

        public AccountsForm(AccountsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            ThemeColors.ApplyTheme(this);
            BindViewModel();

#pragma warning disable CS4014
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

            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            BuildToolbar();
            SetupDataGrid();
            BuildStatusBar();

            if (_toolbar != null)
            {
                _mainLayout.Controls.Add(_toolbar, 0, 0);
            }

            if (_dataGrid != null)
            {
                _mainLayout.Controls.Add(_dataGrid, 0, 1);
            }

            if (_statusBar != null)
            {
                _mainLayout.Controls.Add(_statusBar, 0, 2);
            }

            Controls.Add(_mainLayout);
        }

        private void BuildToolbar()
        {
            _toolbar = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(20, 20),
                Padding = new Padding(8, 4, 8, 4)
            };

            var loadButton = new ToolStripButton
            {
                Text = Resources.LoadAccountsButton,
                AutoSize = false,
                Width = 130,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            loadButton.Click += async (s, e) => await LoadData();

            var filterButton = new ToolStripButton
            {
                Text = Resources.ApplyFiltersButton,
                AutoSize = false,
                Width = 120,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            filterButton.Click += async (s, e) => await _viewModel.FilterAccountsCommand.ExecuteAsync(null);

            _editToggleButton = new ToolStripButton
            {
                Text = "Allow Editing",
                CheckOnClick = true,
                Checked = true,
                AutoSize = false,
                Width = 120,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _editToggleButton.Click += (s, e) =>
            {
                if (_dataGrid != null)
                {
                    _dataGrid.AllowEditing = _editToggleButton.Checked;
                }
            };

            _toolbar.Items.Add(loadButton);
            _toolbar.Items.Add(filterButton);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(_editToggleButton);
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
                AllowEditing = true,
                SelectionMode = GridSelectionMode.Single,
                NavigationMode = NavigationMode.Row,
                RowHeight = 32,
                HeaderRowHeight = 36
            };

            ThemeColors.ApplySfDataGridTheme(_dataGrid);

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
                NumberFormatInfo = CultureInfo.GetCultureInfo("en-US").NumberFormat
            });

            _dataGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "BudgetAmount",
                HeaderText = Resources.BudgetAmountHeader,
                Width = 140,
                AllowSorting = true,
                AllowFiltering = true,
                Format = "C2",
                NumberFormatInfo = CultureInfo.GetCultureInfo("en-US").NumberFormat
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

            _dataGrid.QueryRowStyle += DataGrid_QueryRowStyle;
            _dataGrid.QueryCellStyle += DataGrid_QueryCellStyle;
            _dataGrid.SelectionChanged += DataGrid_SelectionChanged;
            _dataGrid.CurrentCellEndEdit += (s, e) => UpdateStatusTotals();
            _dataGrid.CellDoubleClick += DataGrid_CellDoubleClick;
        }

        private void DataGrid_QueryRowStyle(object? sender, QueryRowStyleEventArgs e)
        {
            if (e.RowType == RowType.DefaultRow && e.RowIndex % 2 == 0)
            {
                e.Style.BackColor = ThemeColors.AlternatingRowBackground;
            }
        }

        private void DataGrid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
        {
            if (e.DataRow.RowData is MunicipalAccountDisplay account)
            {
                if (e.Column.MappingName == "Balance" && account.Balance < 0)
                {
                    e.Style.TextColor = ThemeColors.Error;
                }

                if (e.Column.MappingName == "BudgetAmount" && account.BudgetAmount == 0)
                {
                    e.Style.TextColor = SystemColors.ControlText;
                }
            }
        }

        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_selectionPanel == null || _dataGrid == null)
            {
                return;
            }

            var selectedAccount = _dataGrid.SelectedItem as MunicipalAccountDisplay;
            _selectionPanel.Text = selectedAccount == null
                ? "No selection"
                : $"Selected: {selectedAccount.AccountNumber}";
        }

        private void DataGrid_CellDoubleClick(object? sender, CellClickEventArgs e)
        {
            if (_dataGrid?.SelectedItem is MunicipalAccountDisplay account)
            {
                ShowAccountDetails(account);
            }
        }

        private async Task LoadData()
        {
            if (_dataGrid == null)
            {
                return;
            }

            try
            {
                await _viewModel.LoadAccountsCommand.ExecuteAsync(null);
                BindGridSource();
                UpdateStatusTotals();

                if (_statusPanel != null)
                {
                    _statusPanel.Text = $"{_viewModel.ActiveAccountCount} accounts loaded";
                }
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

        private void BuildStatusBar()
        {
            try
            {
                _statusBar = new StatusStrip
                {
                    Dock = DockStyle.Fill,
                    SizingGrip = false
                };

                _statusPanel = new ToolStripStatusLabel
                {
                    Text = Resources.LoadingText,
                    BorderSides = ToolStripStatusLabelBorderSides.None,
                    Size = new Size(420, 22)
                };

                _totalsPanel = new ToolStripStatusLabel
                {
                    Text = "Totals: --",
                    BorderSides = ToolStripStatusLabelBorderSides.None,
                    Size = new Size(320, 22)
                };

                _selectionPanel = new ToolStripStatusLabel
                {
                    Text = "No selection",
                    BorderSides = ToolStripStatusLabelBorderSides.None,
                    Size = new Size(220, 22)
                };

                _statusBar.Items.AddRange(new ToolStripItem[] { _statusPanel, _totalsPanel, _selectionPanel });
            }
            catch (Exception ex)
            {
                var logger = Program.Services.GetService<ILogger<AccountsForm>>();
                logger?.LogError(ex, "Failed to build accounts status bar");

                try
                {
                    _statusBar = new StatusStrip { Dock = DockStyle.Fill };
                    _statusPanel = new ToolStripStatusLabel { Text = "Status bar initialization failed" };
                    _statusBar.Items.Add(_statusPanel);
                }
                catch
                {
                }
            }
        }

        private void BindViewModel()
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.Accounts.CollectionChanged += Accounts_CollectionChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(AccountsViewModel.IsLoading):
                    if (_statusPanel != null)
                    {
                        _statusPanel.Text = _viewModel.IsLoading ? Resources.LoadingText : Resources.FormTitle;
                    }
                    break;
                case nameof(AccountsViewModel.ActiveAccountCount):
                case nameof(AccountsViewModel.TotalBalance):
                    UpdateStatusTotals();
                    break;
            }
        }

        private void Accounts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateStatusTotals();
            if (_dataGrid?.View != null)
            {
                _dataGrid.View.Refresh();
            }
        }

        private void UpdateStatusTotals()
        {
            if (_totalsPanel == null)
            {
                return;
            }

            _totalsPanel.Text = $"{_viewModel.ActiveAccountCount} accounts | Total: {_viewModel.TotalBalance:C}";
            if (_statusPanel != null && !_viewModel.IsLoading)
            {
                _statusPanel.Text = $"{_viewModel.ActiveAccountCount} accounts loaded";
            }
        }

        private void BindGridSource()
        {
            if (_dataGrid == null)
            {
                return;
            }

            _accountsBinding ??= new BindingSource();
            _accountsBinding.DataSource = _viewModel.Accounts;
            _dataGrid.DataSource = _accountsBinding;
            _accountsBinding.ResetBindings(false);
        }

        private void ShowAccountDetails(MunicipalAccountDisplay account)
        {
            var details = $"Account: {account.AccountNumber}\n" +
                          $"Name: {account.Name}\n" +
                          $"Type: {account.Type}\n" +
                          $"Fund: {account.Fund}\n" +
                          $"Department: {account.Department}\n" +
                          $"Balance: {account.Balance:C}\n" +
                          $"Budget: {account.BudgetAmount:C}\n" +
                          $"Has Parent: {(account.HasParent ? "Yes" : "No")}";

            MessageBox.Show(details, Resources.FormTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.Accounts.CollectionChanged -= Accounts_CollectionChanged;
                _dataGrid?.Dispose();
                _toolbar?.Dispose();
                _statusBar?.Dispose();
                _editToggleButton?.Dispose();
                _mainLayout?.Dispose();
                _statusPanel?.Dispose();
                _totalsPanel?.Dispose();
                _selectionPanel?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
        }
    }
}
