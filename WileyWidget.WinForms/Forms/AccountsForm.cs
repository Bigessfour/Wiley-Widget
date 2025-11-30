using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.ListView.Enums;

namespace WileyWidget.WinForms.Forms
{
    internal static class Resources
    {
        public const string FormTitle = "Municipal Accounts";
        public const string RefreshButton = "Refresh";
        public const string LoadingText = "Loading...";
        public const string AccountNumberHeader = "Account Number";
        public const string AccountNameHeader = "Account Name";
        public const string TypeHeader = "Type";
        public const string FundHeader = "Fund";
        public const string BalanceHeader = "Current Balance";
        public const string ErrorTitle = "Error";
        public const string LoadErrorMessage = "Error loading accounts: {0}";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class AccountsForm : Form
    {
        private readonly AccountsViewModel _viewModel;

        /// <summary>
        /// A simple DataContext property (WinForms does not have DataContext like WPF)
        /// We store the view model here for possible binding/consumption by other helpers.
        /// </summary>
        public new object? DataContext { get; private set; }

        private SfDataGrid? gridAccounts;
        private SfComboBox? comboFund;
        private SfComboBox? comboAccountType;
        private Button? btnRefresh;
        private Button? btnAdd;
        private Button? btnEdit;
        private Button? btnDelete;
        private Panel? topPanel;
        // Summary UI (bottom): displays total balance and active account count
        private Panel? summaryPanel;
        private Label? lblTotalBalance;
        private Label? lblAccountCount;

        public AccountsForm(AccountsViewModel viewModel)
        {
            try
            {
                _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

                // Keep a DataContext reference (DI / service-locator style)
                DataContext = viewModel; // or: DataContext = Program.Services.GetRequiredService<AccountsViewModel>();

                InitializeComponent();
                SetupUI();
                BindViewModel();

                // Explicitly bind grid data source to the view model's Accounts collection
                if (gridAccounts != null)
                {
                    gridAccounts.DataSource = viewModel.Accounts;
                }

                Serilog.Log.Information("AccountsForm initialized with {Count} accounts", viewModel.Accounts?.Count ?? 0);
            }
            catch (Exception ex)
            {
                // Log and show an actionable message — fail fast to surface the issue to the caller
                Serilog.Log.Error(ex, "Failed to initialize AccountsForm");

                System.Windows.Forms.MessageBox.Show(
                    $"Error loading accounts form:\n\n{ex.Message}\n\nCheck logs at: logs/wileywidget-{DateTime.UtcNow:yyyyMMdd}.log",
                    Resources.ErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                throw;
            }
        }

        private void InitializeComponent()
        {
            Text = Resources.FormTitle;
            Size = new Size(1200, 800);
            // Prefer DPI scaling for modern displays
            try
            {
                this.AutoScaleMode = AutoScaleMode.Dpi;
            }
            catch { }
            StartPosition = FormStartPosition.CenterParent;
        }

        private void SetupUI()
        {
            // Top filter panel
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(250, 250, 250)
            };

            // Fund label + combo
            var fundLabel = new Label
            {
                Text = "Fund:",
                AutoSize = true,
                Margin = new Padding(6, 12, 6, 6),
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            comboFund = new SfComboBox
            {
                Name = "comboFund",
                Width = 260,
                DropDownStyle = DropDownStyle.DropDownList,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                Watermark = "Select a fund...",
                Margin = new Padding(6),
                AccessibleName = "Fund",
                ThemeName = "Office2016Colorful"
            };

            // Account Type label + combo
            var acctTypeLabel = new Label
            {
                Text = "Account Type:",
                AutoSize = true,
                Margin = new Padding(12, 12, 6, 6),
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            comboAccountType = new SfComboBox
            {
                Name = "comboAccountType",
                Width = 260,
                DropDownStyle = DropDownStyle.DropDownList,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                Watermark = "Select account type...",
                Margin = new Padding(6),
                AccessibleName = "Account Type",
                ThemeName = "Office2016Colorful"
            };

            // Refresh button
            btnRefresh = new Button
            {
                Text = Resources.RefreshButton,
                Name = "btnRefresh",
                Width = 100,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6)
            };
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnRefresh.Image = iconService?.GetIcon("refresh", theme, 16);
                btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;

                // Update on theme change
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) =>
                {
                    try
                    {
                        btnRefresh.Image = iconService?.GetIcon("refresh", t, 16);
                    }
                    catch { }
                };
            }
            catch { }
            btnRefresh.Click += async (s, e) =>
            {
                try
                {
                    if (_viewModel.FilterAccountsCommand != null)
                    {
                        await _viewModel.FilterAccountsCommand.ExecuteAsync(null);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        var reporting = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.ErrorReportingService>(Program.Services);
                        reporting?.ReportError(ex, "Error running FilterAccountsCommand", showToUser: false);
                    }
                    catch { }
                }
            };

            // Add button
            btnAdd = new Button
            {
                Text = "Add",
                Name = "btnAdd",
                Width = 80,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnAdd.Image = iconService?.GetIcon("add", theme, 14);
                btnAdd.ImageAlign = ContentAlignment.MiddleLeft;
                btnAdd.TextImageRelation = TextImageRelation.ImageBeforeText;
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) =>
                {
                    try
                    {
                        btnAdd.Image = iconService?.GetIcon("add", t, 14);
                    }
                    catch { }
                };
            }
            catch { }
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += BtnAdd_Click;

            // Edit button
            btnEdit = new Button
            {
                Text = "Edit",
                Name = "btnEdit",
                Width = 80,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6)
            };
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnEdit.Image = iconService?.GetIcon("edit", theme, 14);
                btnEdit.ImageAlign = ContentAlignment.MiddleLeft;
                btnEdit.TextImageRelation = TextImageRelation.ImageBeforeText;
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) =>
                {
                    try
                    {
                        btnEdit.Image = iconService?.GetIcon("edit", t, 14);
                    }
                    catch { }
                };
            }
            catch { }
            btnEdit.Click += BtnEdit_Click;

            // Delete button
            btnDelete = new Button
            {
                Text = "Delete",
                Name = "btnDelete",
                Width = 80,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                BackColor = Color.FromArgb(209, 52, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnDelete.Image = iconService?.GetIcon("delete", theme, 14);
                btnDelete.ImageAlign = ContentAlignment.MiddleLeft;
                btnDelete.TextImageRelation = TextImageRelation.ImageBeforeText;
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) =>
                {
                    try
                    {
                        btnDelete.Image = iconService?.GetIcon("delete", t, 14);
                    }
                    catch { }
                };
            }
            catch { }
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += BtnDelete_Click;

            // Add controls to top panel
            topPanel.Controls.Add(fundLabel);
            topPanel.Controls.Add(comboFund);
            topPanel.Controls.Add(acctTypeLabel);
            topPanel.Controls.Add(comboAccountType);
            topPanel.Controls.Add(btnRefresh);
            topPanel.Controls.Add(btnAdd);
            topPanel.Controls.Add(btnEdit);
            topPanel.Controls.Add(btnDelete);

            // Main grid
            gridAccounts = new SfDataGrid
            {
                Name = "gridAccounts",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowFiltering = true,
                AllowSorting = true,
                AllowResizingColumns = true,
                SelectionMode = GridSelectionMode.Single,
                NavigationMode = NavigationMode.Row
            };

            // Try to enable any Syncfusion DPI-aware flags if available (best-effort)
            try
            {
                var dpiProp = gridAccounts.GetType().GetProperty("DpiAware");
                if (dpiProp != null && dpiProp.CanWrite)
                {
                    dpiProp.SetValue(gridAccounts, true);
                }
            }
            catch { /* not all versions expose DpiAware */ }

            // Columns setup
            gridAccounts.Columns.Add(new GridTextColumn { MappingName = "AccountNumber", HeaderText = Resources.AccountNumberHeader, Width = 160 });
            gridAccounts.Columns.Add(new GridTextColumn { MappingName = "AccountName", HeaderText = Resources.AccountNameHeader, Width = 360 });
            gridAccounts.Columns.Add(new GridTextColumn { MappingName = "AccountType", HeaderText = Resources.TypeHeader, Width = 160 });
            gridAccounts.Columns.Add(new GridNumericColumn { MappingName = "CurrentBalance", HeaderText = Resources.BalanceHeader, Format = "C2", Width = 160 });

            // Events: selection, double-click and keyboard shortcuts for edit/delete
            gridAccounts.DoubleClick += (s, e) => BtnEdit_Click(s, EventArgs.Empty);
            gridAccounts.KeyDown += (s, e) =>
            {
                try
                {
                    if (e.KeyCode == Keys.Delete)
                    {
                        BtnDelete_Click(s, EventArgs.Empty);
                    }
                    else if (e.KeyCode == Keys.Enter)
                    {
                        BtnEdit_Click(s, EventArgs.Empty);
                        e.Handled = true;
                    }
                }
                catch { }
            };
            gridAccounts.SelectionChanged += (s, e) => UpdateButtonsState();

            // Table summary (bottom): Show total balance and active count
            // TODO: Re-enable after verifying correct Syncfusion API
            /*
            try
            {
                var summary = new GridTableSummaryRow
                {
                    Name = "AccountsSummary",
                    Title = string.Empty,
                    ShowSummaryInRow = false,
                    Position = VerticalPosition.Bottom
                };

                // Total balance summary (sum of CurrentBalance)
                var totalBalanceColumn = new GridSummaryColumn
                {
                    Name = "TotalBalance",
                    MappingName = "CurrentBalance",
                    Format = "C2",
                    SummaryType = SummaryType.DoubleAggregate
                };

                // Count of rows (by AccountNumber)
                var accountCountColumn = new GridSummaryColumn
                {
                    Name = "AccountCount",
                    MappingName = "AccountNumber",
                    Format = "{Count}",
                    SummaryType = SummaryType.Count
                };

                summary.SummaryRowColumns.Add(totalBalanceColumn);
                summary.SummaryRowColumns.Add(accountCountColumn);

                gridAccounts.TableSummaryRows.Add(summary);
            }
            catch
            {
                // If GridTableSummaryRow or GridSummaryColumn types differ in this Syncfusion version, this is a best-effort addition.
            }
            */

            // Theme
            try
            {
                gridAccounts.ThemeName = "Office2016Colorful";
            }
            catch { }

            // Bottom summary panel (total balance + active count) — lightweight and independent of Syncfusion summary API
            summaryPanel = new Panel
            {
                Name = "summaryPanel",
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 245, 245)
            };

            lblTotalBalance = new Label
            {
                Name = "lblTotalBalance",
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Text = "Total: " + (_viewModel != null ? _viewModel.TotalBalance.ToString("C2", CultureInfo.CurrentCulture) : "—")
            };

            lblAccountCount = new Label
            {
                Name = "lblAccountCount",
                AutoSize = true,
                Margin = new Padding(24, 0, 0, 0),
                Font = new Font("Segoe UI", 9),
                Text = "Active: " + (_viewModel != null ? _viewModel.ActiveAccountCount.ToString(CultureInfo.CurrentCulture) : "0")
            };

            summaryPanel.Controls.Add(lblTotalBalance);
            summaryPanel.Controls.Add(lblAccountCount);

            // Add main controls: grid (fill), summary (bottom) and top panel (top)
            Controls.Add(gridAccounts);
            Controls.Add(summaryPanel);
            Controls.Add(topPanel);

            // Apply initial bindings
            TryApplyViewModelBindings();

            // Ensure buttons reflect selection state
            UpdateButtonsState();

            // Apply theming and subscribe for live updates
            try
            {
                ThemeManager.ApplyTheme(this);
                ThemeManager.ThemeChanged += (s, t) => ThemeManager.ApplyTheme(this);
            }
            catch { }
        }

        private void BindViewModel()
        {
            if (_viewModel is System.ComponentModel.INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    try
                    {
                        switch (e.PropertyName)
                        {
                            case nameof(_viewModel.AvailableFunds):
                                if (comboFund != null)
                                {
                                    comboFund.DataSource = _viewModel.AvailableFunds;
                                    comboFund.DisplayMember = "DisplayName";
                                    comboFund.SelectedItem = _viewModel.SelectedFund;
                                }
                                break;
                            case nameof(_viewModel.SelectedFund):
                                if (comboFund != null) comboFund.SelectedItem = _viewModel.SelectedFund;
                                break;
                            case nameof(_viewModel.AvailableAccountTypes):
                                if (comboAccountType != null)
                                {
                                    comboAccountType.DataSource = _viewModel.AvailableAccountTypes;
                                    comboAccountType.DisplayMember = "DisplayName";
                                    comboAccountType.SelectedItem = _viewModel.SelectedAccountType;
                                }
                                break;
                            case nameof(_viewModel.SelectedAccountType):
                                if (comboAccountType != null) comboAccountType.SelectedItem = _viewModel.SelectedAccountType;
                                break;
                            case nameof(_viewModel.Accounts):
                                if (gridAccounts != null)
                                {
                                    gridAccounts.DataSource = _viewModel.Accounts;
                                    // Ensure buttons reflect selection after new data loads
                                    UpdateButtonsState();
                                }
                                break;
                            case nameof(_viewModel.TotalBalance):
                                if (lblTotalBalance != null)
                                {
                                    lblTotalBalance.Text = "Total: " + _viewModel.TotalBalance.ToString("C2", CultureInfo.CurrentCulture);
                                }
                                break;
                            case nameof(_viewModel.ActiveAccountCount):
                                if (lblAccountCount != null)
                                {
                                    lblAccountCount.Text = "Active: " + _viewModel.ActiveAccountCount.ToString(CultureInfo.CurrentCulture);
                                }
                                break;
                        }
                    }
                    catch
                    {
                        // swallow - UI binding best-effort
                    }
                };
            }

            if (comboFund != null)
            {
                comboFund.SelectedIndexChanged += (s, e) =>
                {
                    try
                    {
                        // SelectedItem can be enum value or object; handle both safely
                        if (comboFund.SelectedItem is WileyWidget.Models.MunicipalFundType mf)
                        {
                            _viewModel.SelectedFund = mf;
                        }
                        else if (comboFund.SelectedItem != null && Enum.TryParse<WileyWidget.Models.MunicipalFundType>(comboFund.SelectedItem.ToString(), out var parsedFund))
                        {
                            _viewModel.SelectedFund = parsedFund;
                        }
                        else
                        {
                            _viewModel.SelectedFund = null;
                        }
                    }
                    catch { }
                };
            }

            if (comboAccountType != null)
            {
                comboAccountType.SelectedIndexChanged += (s, e) =>
                {
                    try
                    {
                        if (comboAccountType.SelectedItem is WileyWidget.Models.AccountType at)
                        {
                            _viewModel.SelectedAccountType = at;
                        }
                        else if (comboAccountType.SelectedItem != null && Enum.TryParse<WileyWidget.Models.AccountType>(comboAccountType.SelectedItem.ToString(), out var parsedType))
                        {
                            _viewModel.SelectedAccountType = parsedType;
                        }
                        else
                        {
                            _viewModel.SelectedAccountType = null;
                        }
                    }
                    catch { }
                };
            }

            TryApplyViewModelBindings();
        }

        private void TryApplyViewModelBindings()
        {
            if (comboFund != null && _viewModel.AvailableFunds != null)
            {
                comboFund.DataSource = null; // Clear first to avoid binding issues
                // If the items are simple enums (MunicipalFundType), do not set DisplayMember/ValueMember which expect property names on reference types.
                // FirstOrDefault will return a default value for value types (enums), so checking
                // for null is incorrect and triggers CS0472. Use Any() to determine if there are
                // items and then inspect the runtime type of the first item.
                if (_viewModel.AvailableFunds != null && _viewModel.AvailableFunds.Any() && _viewModel.AvailableFunds.First().GetType().IsEnum)
                {
                    comboFund.DataSource = _viewModel.AvailableFunds;
                }
                else
                {
                    comboFund.DataSource = _viewModel.AvailableFunds;
                    comboFund.DisplayMember = "DisplayName";
                    try
                    {
                        comboFund.ValueMember = "DisplayName"; // Explicit ValueMember when items are object-like
                    }
                    catch (ArgumentException) { /* ValueMember can't be set when items lack property - ignore to remain resilient */ }
                }

                if (_viewModel.SelectedFund != null)
                {
                    comboFund.SelectedItem = _viewModel.SelectedFund;
                }
            }

            if (comboAccountType != null && _viewModel.AvailableAccountTypes != null)
            {
                comboAccountType.DataSource = null; // Clear first to avoid binding issues
                if (_viewModel.AvailableAccountTypes != null && _viewModel.AvailableAccountTypes.Any() && _viewModel.AvailableAccountTypes.First().GetType().IsEnum)
                {
                    comboAccountType.DataSource = _viewModel.AvailableAccountTypes;
                }
                else
                {
                    comboAccountType.DataSource = _viewModel.AvailableAccountTypes;
                    comboAccountType.DisplayMember = "DisplayName";
                    try
                    {
                        comboAccountType.ValueMember = "DisplayName"; // Explicit ValueMember when items are object-like
                    }
                    catch (ArgumentException) { /* ignore */ }
                }

                if (_viewModel.SelectedAccountType != null)
                {
                    comboAccountType.SelectedItem = _viewModel.SelectedAccountType;
                }
            }

            if (gridAccounts != null && _viewModel.Accounts != null)
            {
                gridAccounts.DataSource = _viewModel.Accounts;
            }

            // Update summary labels from the view model (safe-null checks)
            if (lblTotalBalance != null)
            {
                lblTotalBalance.Text = "Total: " + _viewModel.TotalBalance.ToString("C2", CultureInfo.CurrentCulture);
            }

            if (lblAccountCount != null)
            {
                lblAccountCount.Text = "Active: " + _viewModel.ActiveAccountCount.ToString(CultureInfo.CurrentCulture);
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            try
            {
                using var editForm = new AccountEditForm(_viewModel);
                if (editForm.ShowDialog(this) == DialogResult.OK)
                {
                    // Accounts are refreshed by the ViewModel after save
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error opening add account form");
                MessageBox.Show($"Error: {ex.Message}", Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnEdit_Click(object? sender, EventArgs e)
        {
            try
            {
                var selectedAccount = GetSelectedAccount();
                if (selectedAccount == null)
                {
                    MessageBox.Show("Please select an account to edit.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Get the full entity from DB
                var account = await _viewModel.GetAccountByIdAsync(selectedAccount.Id);
                if (account == null)
                {
                    MessageBox.Show("Could not load account details.", Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using var editForm = new AccountEditForm(_viewModel, account);
                if (editForm.ShowDialog(this) == DialogResult.OK)
                {
                    // Accounts are refreshed by the ViewModel after save
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error opening edit account form");
                MessageBox.Show($"Error: {ex.Message}", Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            try
            {
                var selectedAccount = GetSelectedAccount();
                if (selectedAccount == null)
                {
                    MessageBox.Show("Please select an account to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete account '{selectedAccount.AccountNumber} - {selectedAccount.AccountName}'?\n\nThis will deactivate the account.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    var success = await _viewModel.DeleteAccountAsync(selectedAccount.Id);
                    if (!success)
                    {
                        MessageBox.Show("Failed to delete account. Check logs for details.", Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error deleting account");
                MessageBox.Show($"Error: {ex.Message}", Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Models.MunicipalAccountDisplay? GetSelectedAccount()
        {
            if (gridAccounts?.SelectedItem is Models.MunicipalAccountDisplay account)
            {
                return account;
            }
            return null;
        }

        private void UpdateButtonsState()
        {
            var selected = GetSelectedAccount();
            try
            {
                if (btnEdit != null) btnEdit.Enabled = selected != null;
                if (btnDelete != null) btnDelete.Enabled = selected != null;
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gridAccounts?.Dispose();
                comboFund?.Dispose();
                comboAccountType?.Dispose();
                btnRefresh?.Dispose();
                btnAdd?.Dispose();
                btnEdit?.Dispose();
                btnDelete?.Dispose();
                topPanel?.Dispose();
                // Dispose summary UI elements
                lblTotalBalance?.Dispose();
                lblAccountCount?.Dispose();
                summaryPanel?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Prepare this form to be docked/embedded inside a host control (DockingManager etc).
        /// Calling code should keep a reference to the original instance (DI-scoped) when used.
        /// </summary>
        public void PrepareForDocking()
        {
            try
            {
                // Make it a child control instead of a top-level window
                TopLevel = false;
                FormBorderStyle = FormBorderStyle.None;
                Dock = DockStyle.Fill;
                // Some dialogs expect CenterParent; in docking hosts we want fill behaviour
                StartPosition = FormStartPosition.Manual;
            }
            catch { }
        }
    }
}
