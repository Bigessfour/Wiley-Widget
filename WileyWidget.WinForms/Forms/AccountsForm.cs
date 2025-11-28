using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WileyWidget.WinForms.ViewModels;
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
        private Panel? topPanel;
        // Summary UI (bottom): displays total balance and active account count
        private Panel? summaryPanel;
        private Label? lblTotalBalance;
        private Label? lblAccountCount;

        public AccountsForm(AccountsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // Keep a DataContext reference (DI / service-locator style)
            DataContext = viewModel; // or: DataContext = Program.Services.GetRequiredService<AccountsViewModel>();

            InitializeComponent();
            SetupUI();
            BindViewModel();

            // Explicitly bind grid data source to the view model's Accounts collection
            try
            {
                if (gridAccounts != null)
                {
                    gridAccounts.DataSource = viewModel.Accounts;
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            Text = Resources.FormTitle;
            Size = new Size(1200, 800);
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
            btnRefresh.Click += async (s, e) =>
            {
                try
                {
                    if (_viewModel.FilterAccountsCommand != null)
                    {
#pragma warning disable CS4014
                        _viewModel.FilterAccountsCommand.ExecuteAsync(null);
#pragma warning restore CS4014
                    }
                }
                catch (Exception ex)
                {
                    try { var reporting = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Services.ErrorReportingService>(Program.Services); reporting?.ReportError(ex, "Error running FilterAccountsCommand", showToUser: false); } catch { }
                }
            };

            // Add controls to top panel
            topPanel.Controls.Add(fundLabel);
            topPanel.Controls.Add(comboFund);
            topPanel.Controls.Add(acctTypeLabel);
            topPanel.Controls.Add(comboAccountType);
            topPanel.Controls.Add(btnRefresh);

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

            // Columns setup
            gridAccounts.Columns.Add(new GridTextColumn { MappingName = "AccountNumber", HeaderText = Resources.AccountNumberHeader, Width = 160 });
            gridAccounts.Columns.Add(new GridTextColumn { MappingName = "Name", HeaderText = Resources.AccountNameHeader, Width = 360 });
            gridAccounts.Columns.Add(new GridTextColumn { MappingName = "Type", HeaderText = Resources.TypeHeader, Width = 160 });
            gridAccounts.Columns.Add(new GridNumericColumn { MappingName = "CurrentBalance", HeaderText = Resources.BalanceHeader, Format = "C2", Width = 160 });

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
                comboFund.DataSource = _viewModel.AvailableFunds;
                comboFund.DisplayMember = "DisplayName";
                comboFund.ValueMember = "DisplayName"; // Explicit ValueMember
                if (_viewModel.SelectedFund != null)
                {
                    comboFund.SelectedItem = _viewModel.SelectedFund;
                }
            }

            if (comboAccountType != null && _viewModel.AvailableAccountTypes != null)
            {
                comboAccountType.DataSource = null; // Clear first to avoid binding issues
                comboAccountType.DataSource = _viewModel.AvailableAccountTypes;
                comboAccountType.DisplayMember = "DisplayName";
                comboAccountType.ValueMember = "DisplayName"; // Explicit ValueMember
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gridAccounts?.Dispose();
                comboFund?.Dispose();
                comboAccountType?.Dispose();
                btnRefresh?.Dispose();
                topPanel?.Dispose();
                // Dispose summary UI elements
                lblTotalBalance?.Dispose();
                lblAccountCount?.Dispose();
                summaryPanel?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
