using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Input;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Abstractions.Models;

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
        public const string NewAccountMenu = "New Account";
        public const string EditAccountMenu = "Edit Account";
        public const string DeleteAccountMenu = "Delete Account";
        public const string ViewDetailsMenu = "View Details";
        public const string ExportMenu = "Export Selected";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class AccountsForm : Form
    {
        private readonly AccountsViewModel _viewModel;
        private readonly ILogger<AccountsForm> _logger;
        private SfDataGrid? _dataGrid;  // Use Syncfusion SfDataGrid for high-performance grid
        private Panel? _detailPanel;
        private Label? _detailAccountNumber;
        private Label? _detailAccountName;
        private Label? _detailBalance;
        private Label? _detailBudget;
        private Label? _detailVariance;
        private ComboBox? _fundCombo;
        private ComboBox? _typeCombo;
        private TextBox? _searchBox;

        // Cancellation token source for async operations
        private CancellationTokenSource? _cts;

        public AccountsForm(AccountsViewModel viewModel, ILogger<AccountsForm> logger)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                InitializeComponent();
                SetupDataGrid();
                _logger.LogInformation("AccountsForm initialized successfully");

                // Initialize cancellation token source
                _cts = new CancellationTokenSource();

                FormClosing += (s, e) =>
                {
                    Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
                };

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                LoadData();
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AccountsForm");
                if (Application.MessageLoop)
                {
                    MessageBox.Show($"Unable to open accounts view: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                throw;
            }
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Forms.Form.set_Text")]
        private void InitializeComponent()
        {
            SuspendLayout();

            Text = Resources.FormTitle;
            Size = new Size(1400, 900);
            MinimumSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterScreen;

            // === Enhanced Toolbar ===
            var toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(5, 0, 5, 0)
            };

            var loadButton = new ToolStripButton(Resources.LoadAccountsButton, null, async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct => await LoadData(),
                    _cts,
                    this,
                    _logger,
                    "Loading accounts");
            })
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                BackColor = Color.FromArgb(66, 133, 244),
                ForeColor = Color.White
            };

            var filterButton = new ToolStripButton(Resources.ApplyFiltersButton, null, async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct => await _viewModel.FilterAccountsCommand.ExecuteAsync(ct),
                    _cts,
                    this,
                    _logger,
                    "Applying account filters");
            })
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };

            // Fund filter
            var fundLabel = new ToolStripLabel("  Fund: ");
            _fundCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            _fundCombo.Items.AddRange(new object[] { "(all)", "General Fund", "Water Fund", "Sewer Fund", "Capital Projects", "Debt Service" });
            _fundCombo.SelectedIndex = 0;
            _fundCombo.AccessibleName = "Fund Filter";
            _fundCombo.AccessibleDescription = "Filter accounts by fund type";
            var fundHost = new ToolStripControlHost(_fundCombo);

            // Account Type filter
            var typeLabel = new ToolStripLabel("  Type: ");
            _typeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            _typeCombo.Items.AddRange(new object[] { "(all)", "Asset", "Liability", "Revenue", "Expense", "Equity" });
            _typeCombo.SelectedIndex = 0;
            _typeCombo.AccessibleName = "Account Type Filter";
            _typeCombo.AccessibleDescription = "Filter accounts by type (Asset, Liability, Revenue, Expense, Equity)";
            var typeHost = new ToolStripControlHost(_typeCombo);

            // Search box
            var searchLabel = new ToolStripLabel("  Search: ");
            _searchBox = new TextBox { Width = 180 };
            _searchBox.PlaceholderText = "Account name or number...";
            _searchBox.AccessibleName = "Account Search";
            _searchBox.AccessibleDescription = "Search accounts by name or number";
            var searchHost = new ToolStripControlHost(_searchBox);

            // Export button
            var exportButton = new ToolStripButton("Export to Excel", null, (s, e) =>
            {
                _logger.LogInformation("Export to Excel button clicked");
                MessageBox.Show("Export feature available in full version.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                loadButton,
                new ToolStripSeparator(),
                fundLabel, fundHost,
                typeLabel, typeHost,
                searchLabel, searchHost,
                new ToolStripSeparator(),
                filterButton,
                new ToolStripSeparator(),
                exportButton
            });

            // === Status Strip ===
            var statusStrip = new StatusStrip { BackColor = Color.FromArgb(248, 249, 250) };
            var statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var recordCountLabel = new ToolStripStatusLabel { Alignment = ToolStripItemAlignment.Right };
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, recordCountLabel });

            // Bind status
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.IsLoading))
                {
                    statusLabel.Text = _viewModel.IsLoading ? Resources.LoadingText : "Ready";
                    recordCountLabel.Text = _viewModel.IsLoading ? "" : $"{_viewModel.ActiveAccountCount} accounts | Total Balance: {_viewModel.TotalBalance:C}";
                }
            };

            Controls.AddRange(new Control[] { toolStrip, statusStrip });

            ResumeLayout(false);
            PerformLayout();
        }

        private void SetupDataGrid()
        {
            // === Main Split Container ===
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 1000,
                BackColor = Color.FromArgb(245, 245, 250)
            };

            _dataGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AllowEditing = false,
                SelectionUnit = SelectionUnit.Row,
                AutoGenerateColumns = false,
                ShowGroupDropArea = false,
                BackColor = Color.White,
                RowHeight = 30,
                AccessibleName = "Municipal Accounts Grid",
                AccessibleDescription = "Data grid displaying all municipal accounts with balance and budget information. Use arrow keys to navigate, Enter to view details.",
                AccessibleRole = AccessibleRole.Table
            };

            // Selection changed event for detail panel
            _dataGrid.SelectionChanged += DataGrid_SelectionChanged;

            // === Context Menu ===
            var contextMenu = new ContextMenuStrip();
            var viewDetailsItem = new ToolStripMenuItem(Resources.ViewDetailsMenu, null, (s, e) =>
            {
                _logger.LogInformation("View account details menu item clicked");
                ShowAccountDetails();
            });
            var editItem = new ToolStripMenuItem(Resources.EditAccountMenu, null, (s, e) =>
            {
                _logger.LogInformation("Edit account menu item clicked");
                EditSelectedAccount();
            });
            var newItem = new ToolStripMenuItem(Resources.NewAccountMenu, null, (s, e) =>
            {
                _logger.LogInformation("Create new account menu item clicked");
                CreateNewAccount();
            });
            var deleteItem = new ToolStripMenuItem(Resources.DeleteAccountMenu, null, (s, e) =>
            {
                _logger.LogInformation("Delete account menu item clicked");
                DeleteSelectedAccount();
            });
            var exportItem = new ToolStripMenuItem(Resources.ExportMenu, null, (s, e) =>
            {
                _logger.LogInformation("Export accounts menu item clicked");
                ExportSelectedAccounts();
            });

            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                viewDetailsItem,
                editItem,
                new ToolStripSeparator(),
                newItem,
                deleteItem,
                new ToolStripSeparator(),
                exportItem
            });
            _dataGrid.ContextMenuStrip = contextMenu;

            // Double-click to view details
            _dataGrid.CellDoubleClick += (s, e) => ShowAccountDetails();

            // Configure SfDataGrid columns (bind to AccountsViewModel.MunicipalAccountDisplay)
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "AccountNumber", HeaderText = Resources.AccountNumberHeader, Width = 120 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = Resources.AccountNameHeader, Width = 200 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Description", HeaderText = Resources.DescriptionHeader, Width = 250 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Type", HeaderText = Resources.TypeHeader, Width = 100 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Fund", HeaderText = Resources.FundHeader, Width = 100 });
            _dataGrid.Columns.Add(new GridNumericColumn { MappingName = "Balance", HeaderText = Resources.BalanceHeader, Format = "C2", Width = 120 });
            _dataGrid.Columns.Add(new GridNumericColumn { MappingName = "BudgetAmount", HeaderText = Resources.BudgetAmountHeader, Format = "C2", Width = 120 });
            _dataGrid.Columns.Add(new GridTextColumn { MappingName = "Department", HeaderText = Resources.DepartmentHeader, Width = 150 });
            _dataGrid.Columns.Add(new GridCheckBoxColumn { MappingName = "IsActive", HeaderText = Resources.ActiveHeader, Width = 80 });
            _dataGrid.Columns.Add(new GridCheckBoxColumn { MappingName = "HasParent", HeaderText = Resources.HasParentHeader, Width = 100 });

            // === Account Detail Panel ===
            _detailPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 320,
                BackColor = Color.White,
                Padding = new Padding(15),
                BorderStyle = BorderStyle.None
            };

            // Add left border effect
            var borderPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 1,
                BackColor = Color.FromArgb(220, 220, 225)
            };
            _detailPanel.Controls.Add(borderPanel);

            // Detail header
            var detailHeader = new Label
            {
                Text = "Account Details",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(10, 10, 0, 0)
            };
            _detailPanel.Controls.Add(detailHeader);

            // Detail content panel with TableLayout
            var detailContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(10, 50, 10, 10),
                AutoScroll = true
            };
            detailContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            detailContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            // Account Number
            detailContent.Controls.Add(CreateDetailLabel("Account #:"), 0, 0);
            _detailAccountNumber = CreateDetailValue("-");
            detailContent.Controls.Add(_detailAccountNumber, 1, 0);

            // Account Name
            detailContent.Controls.Add(CreateDetailLabel("Name:"), 0, 1);
            _detailAccountName = CreateDetailValue("-");
            detailContent.Controls.Add(_detailAccountName, 1, 1);

            // Balance
            detailContent.Controls.Add(CreateDetailLabel("Balance:"), 0, 2);
            _detailBalance = CreateDetailValue("-");
            _detailBalance.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            detailContent.Controls.Add(_detailBalance, 1, 2);

            // Budget
            detailContent.Controls.Add(CreateDetailLabel("Budget:"), 0, 3);
            _detailBudget = CreateDetailValue("-");
            detailContent.Controls.Add(_detailBudget, 1, 3);

            // Variance
            detailContent.Controls.Add(CreateDetailLabel("Variance:"), 0, 4);
            _detailVariance = CreateDetailValue("-");
            detailContent.Controls.Add(_detailVariance, 1, 4);

            // Fund
            var detailFundLabel = CreateDetailLabel("Fund:");
            detailContent.Controls.Add(detailFundLabel, 0, 5);
            var detailFundValue = CreateDetailValue("-");
            detailFundValue.Name = "detailFund";
            detailContent.Controls.Add(detailFundValue, 1, 5);

            // Department
            var detailDeptLabel = CreateDetailLabel("Department:");
            detailContent.Controls.Add(detailDeptLabel, 0, 6);
            var detailDeptValue = CreateDetailValue("-");
            detailDeptValue.Name = "detailDept";
            detailContent.Controls.Add(detailDeptValue, 1, 6);

            // Status
            var detailStatusLabel = CreateDetailLabel("Status:");
            detailContent.Controls.Add(detailStatusLabel, 0, 7);
            var detailStatusValue = CreateDetailValue("-");
            detailStatusValue.Name = "detailStatus";
            detailContent.Controls.Add(detailStatusValue, 1, 7);

            _detailPanel.Controls.Add(detailContent);

            // Action buttons panel at bottom of detail panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            var editButton = new Button
            {
                Text = "Edit",
                Width = 80,
                Height = 32,
                BackColor = Color.FromArgb(66, 133, 244),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            editButton.FlatAppearance.BorderSize = 0;
            editButton.Click += (s, e) => EditSelectedAccount();

            var viewButton = new Button
            {
                Text = "View",
                Width = 80,
                Height = 32,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            viewButton.FlatAppearance.BorderSize = 0;
            viewButton.Click += (s, e) => ShowAccountDetails();

            buttonPanel.Controls.AddRange(new Control[] { editButton, viewButton });
            _detailPanel.Controls.Add(buttonPanel);

            // Add controls to form
            mainSplit.Panel1.Controls.Add(_dataGrid);
            mainSplit.Panel2.Controls.Add(_detailPanel);
            Controls.Add(mainSplit);
        }

        private static Label CreateDetailLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = true,
                Padding = new Padding(0, 5, 0, 5)
            };
        }

        private static Label CreateDetailValue(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Padding = new Padding(0, 5, 0, 5)
            };
        }

        private void DataGrid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dataGrid == null || _detailPanel == null) return;

            // SfDataGrid provides SelectedItems - first item is our bound object
            try
            {
                var sel = _dataGrid.SelectedItems;
                if (sel != null && sel.Count > 0)
                {
                    UpdateDetailPanelFromObject(sel[0]);
                }
            }
            catch
            {
                // Fallback for other grid types (not expected here)
            }
        }

        private void UpdateDetailPanelFromObject(object rowOrItem)
        {
            // Handle MunicipalAccountDisplay objects (the ViewModel's display models)
            if (rowOrItem is MunicipalAccountDisplay disp)
            {
                _detailAccountNumber!.Text = disp.AccountNumber ?? "-";
                _detailAccountName!.Text = disp.Name ?? "-";
                if (_detailBalance != null)
                {
                    _detailBalance.Text = disp.Balance.ToString("C2", CultureInfo.CurrentCulture);
                    _detailBalance.ForeColor = disp.Balance >= 0 ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                }

                if (_detailBudget != null) _detailBudget.Text = disp.BudgetAmount.ToString("C2", CultureInfo.CurrentCulture);

                if (_detailVariance != null)
                {
                    var variance = disp.Balance - disp.BudgetAmount;
                    _detailVariance.Text = variance.ToString("C2", CultureInfo.CurrentCulture);
                    _detailVariance.ForeColor = variance >= 0 ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                }

                var detailContent = _detailPanel?.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
                if (detailContent != null)
                {
                    var fundLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailFund");
                    if (fundLabel != null) fundLabel.Text = disp.Fund ?? "-";

                    var deptLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailDept");
                    if (deptLabel != null) deptLabel.Text = disp.Department ?? "-";

                    var statusLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailStatus");
                    if (statusLabel != null)
                    {
                        statusLabel.Text = disp.IsActive ? "Active" : "Inactive";
                        statusLabel.ForeColor = disp.IsActive ? Color.FromArgb(40, 167, 69) : Color.FromArgb(108, 117, 125);
                    }
                }
                return;
            }

            // DataGridViewRow fallback (in case of older implementations)
            if (rowOrItem is DataGridViewRow row)
            {
                if (_detailAccountNumber != null)
                    _detailAccountNumber.Text = row.Cells["AccountNumber"]?.Value?.ToString() ?? "-";

                if (_detailAccountName != null)
                    _detailAccountName.Text = row.Cells["Name"]?.Value?.ToString() ?? "-";

                if (_detailBalance != null)
                {
                    var balanceValue = row.Cells["Balance"]?.Value;
                    if (balanceValue is decimal balance)
                    {
                        _detailBalance.Text = balance.ToString("C2", CultureInfo.CurrentCulture);
                        _detailBalance.ForeColor = balance >= 0 ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                    }
                    else
                    {
                        _detailBalance.Text = "-";
                    }
                }

                if (_detailBudget != null)
                {
                    var budgetValue = row.Cells["BudgetAmount"]?.Value;
                    _detailBudget.Text = budgetValue is decimal budget ? budget.ToString("C2", CultureInfo.CurrentCulture) : "-";
                }

                if (_detailVariance != null)
                {
                    var balanceValue = row.Cells["Balance"]?.Value;
                    var budgetValue = row.Cells["BudgetAmount"]?.Value;
                    if (balanceValue is decimal balance && budgetValue is decimal budget)
                    {
                        var variance = balance - budget;
                        _detailVariance.Text = variance.ToString("C2", CultureInfo.CurrentCulture);
                        _detailVariance.ForeColor = variance >= 0 ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
                    }
                    else
                    {
                        _detailVariance.Text = "-";
                    }
                }

                // Update other detail fields
                var detailContent = _detailPanel?.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
                if (detailContent != null)
                {
                    var fundLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailFund");
                    if (fundLabel != null)
                        fundLabel.Text = row.Cells["Fund"]?.Value?.ToString() ?? "-";

                    var deptLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailDept");
                    if (deptLabel != null)
                        deptLabel.Text = row.Cells["Department"]?.Value?.ToString() ?? "-";

                    var statusLabel = detailContent.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "detailStatus");
                    if (statusLabel != null)
                    {
                        var isActive = row.Cells["IsActive"]?.Value;
                        statusLabel.Text = isActive is true ? "Active" : "Inactive";
                        statusLabel.ForeColor = isActive is true ? Color.FromArgb(40, 167, 69) : Color.FromArgb(108, 117, 125);
                    }
                }
            }
        }

        private void ShowAccountDetails()
        {
            if (_dataGrid?.SelectedItems != null && _dataGrid.SelectedItems.Count > 0)
            {
                var item = _dataGrid.SelectedItems[0];
                if (item is MunicipalAccountDisplay disp)
                {
                    _logger.LogWarning("Full account details view coming soon for account {AccountNumber} ({Name})", disp.AccountNumber, disp.Name);
                }
                else if (item is DataGridViewRow row)
                {
                    var accountNumber = row.Cells["AccountNumber"]?.Value?.ToString() ?? "Unknown";
                    var accountName = row.Cells["Name"]?.Value?.ToString() ?? "Unknown";
                    _logger.LogWarning("Full account details view coming soon for account {AccountNumber} ({Name})", accountNumber, accountName);
                }
            }
        }

        private void EditSelectedAccount()
        {
            if (_dataGrid?.SelectedItems != null && _dataGrid.SelectedItems.Count > 0)
            {
                var item = _dataGrid.SelectedItems[0];
                string accountNumber = "Unknown";
                if (item is MunicipalAccountDisplay disp)
                    accountNumber = disp.AccountNumber ?? accountNumber;
                else if (item is DataGridViewRow row)
                    accountNumber = row.Cells["AccountNumber"]?.Value?.ToString() ?? accountNumber;

                _logger.LogWarning("Account editing feature coming soon for account {AccountNumber}", accountNumber);
            }
        }

        private void CreateNewAccount()
        {
            _logger.LogWarning("Create new account feature coming soon");
        }

        private void DeleteSelectedAccount()
        {
            if (_dataGrid?.SelectedItems != null && _dataGrid.SelectedItems.Count > 0)
            {
                var item = _dataGrid.SelectedItems[0];
                string accountNumber = "Unknown";
                if (item is MunicipalAccountDisplay disp)
                    accountNumber = disp.AccountNumber ?? accountNumber;
                else if (item is DataGridViewRow row)
                    accountNumber = row.Cells["AccountNumber"]?.Value?.ToString() ?? accountNumber;

                _logger.LogWarning("Delete confirmation requested for account {AccountNumber}", accountNumber);

                _logger.LogWarning("Delete functionality coming soon for account {AccountNumber}", accountNumber);
            }
        }

        private void ExportSelectedAccounts()
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv",
                Title = "Export Accounts",
                FileName = $"Accounts_Export_{DateTime.Now:yyyyMMdd}"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                _logger.LogWarning("Export functionality coming soon for file: {FileName}", saveDialog.FileName);
            }
        }

        private async Task LoadData()
        {
            try
            {
                await _viewModel.LoadAccountsCommand.ExecuteAsync(CancellationToken.None);
                _dataGrid!.DataSource = new BindingSource { DataSource = _viewModel.Accounts };
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogWarning(oce, "Account loading was canceled (likely due to form close or app shutdown)");
                // Don’t show a dialog on cancellation — this is expected behavior during shutdown or quick navigation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading accounts in AccountsForm");
                if (Application.MessageLoop)
                {
                    MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.LoadErrorMessage, ex.Message), Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dataGrid?.Dispose();
                _detailPanel?.Dispose();
                _detailAccountNumber?.Dispose();
                _detailAccountName?.Dispose();
                _detailBalance?.Dispose();
                _detailBudget?.Dispose();
                _detailVariance?.Dispose();
                _fundCombo?.Dispose();
                _typeCombo?.Dispose();
                _searchBox?.Dispose();

                // Cancel and dispose async operations
                Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
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
