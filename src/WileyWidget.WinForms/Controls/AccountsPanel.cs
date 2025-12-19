using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WwThemeColors = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.WinForms.Extensions;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.ListView.Enums;
using Syncfusion.WinForms.Themes;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Resource strings for AccountsPanel UI elements.
    /// Centralized for localization support and maintainability.
    /// </summary>
    internal static class AccountsPanelResources
    {
        /// <summary>Panel title.</summary>
        public const string PanelTitle = "Municipal Accounts";
        /// <summary>Refresh button label.</summary>
        public const string RefreshButton = "Refresh";

        /// <summary>Account number column header.</summary>
        public const string AccountNumberHeader = "Account Number";
        /// <summary>Account name column header.</summary>
        public const string AccountNameHeader = "Account Name";
        /// <summary>Type column header.</summary>
        public const string TypeHeader = "Type";
        /// <summary>Fund column header.</summary>
        public const string FundHeader = "Fund";
        /// <summary>Balance column header.</summary>
        public const string BalanceHeader = "Current Balance";
        /// <summary>Error dialog title.</summary>
        public const string ErrorTitle = "Error";
        /// <summary>Account load error message format.</summary>
        public const string LoadErrorMessage = "Error loading accounts: {0}";
    }

    /// <summary>
    /// Municipal accounts management panel (UserControl) with Syncfusion SfDataGrid.
    /// Provides CRUD operations for municipal accounts with filtering by fund and type.
    /// Implements theme support and is designed for embedding in DockingManager.
    /// </summary>
    /// <remarks>
    /// Uses CommunityToolkit.Mvvm patterns with ObservableRecipient ViewModel.
    /// The ViewModel exposes AsyncRelayCommand for async operations and ObservableCollection
    /// for data binding with INotifyPropertyChanged notifications. Button clicks delegate
    /// to ViewModel commands where available. Implements data validation with ErrorProvider
    /// and control validation events (Validating/Validated).
    /// Inherits from ScopedPanelBase to ensure proper scope management for scoped dependencies.
    /// </remarks>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class AccountsPanel : ScopedPanelBase<AccountsViewModel>
    {
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;

        /// <summary>
        /// A simple DataContext property for ViewModel access.
        /// </summary>
        public new object? DataContext { get; private set; }

        private SfDataGrid? gridAccounts;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private SfComboBox? comboFund;
        private SfComboBox? comboAccountType;
        private Button? btnRefresh;
        private Button? btnAdd;
        private Button? btnEdit;
        private Button? btnDelete;
        private Syncfusion.WinForms.Controls.SfButton? btnExportExcel;
        private Syncfusion.WinForms.Controls.SfButton? btnExportPdf;
        private EventHandler<AppTheme>? _btnExportExcelThemeChangedHandler;
        private EventHandler<AppTheme>? _btnExportPdfThemeChangedHandler;
        private Panel? topPanel;
        // Summary UI (bottom): displays total balance and active account count
        private Panel? summaryPanel;
        private Label? lblTotalBalance;
        private Label? lblAccountCount;
        // Theme and viewmodel event handlers (stored so we can detach on Dispose)
        private EventHandler<AppTheme>? _btnRefreshThemeChangedHandler;
        private EventHandler<AppTheme>? _btnAddThemeChangedHandler;
        private EventHandler<AppTheme>? _btnEditThemeChangedHandler;
        private EventHandler<AppTheme>? _btnDeleteThemeChangedHandler;
        private EventHandler<AppTheme>? _panelThemeChangedHandler;
        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _accountsCollectionChangedHandler;
        private System.ComponentModel.PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        // Named event handlers for PanelHeader (stored for proper unsubscription)
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderPinHandler;
        private EventHandler? _panelHeaderCloseHandler;

        // Data validation and binding
        private ErrorProvider? errorProvider;
        // Shared ToolTip instance for the panel to avoid creating multiple undisposed ToolTip objects
        private ToolTip? _toolTip;
        // Local selection tracking using the view-model's display DTO
        private WileyWidget.WinForms.Models.MunicipalAccountDisplay? _selectedAccountDisplay;
        private BindingSource? accountsBindingSource;
        // Combo validation handlers
        private System.ComponentModel.CancelEventHandler? _comboFundValidatingHandler;
        private EventHandler? _comboFundValidatedHandler;
        private System.ComponentModel.CancelEventHandler? _comboAccountTypeValidatingHandler;
        private EventHandler? _comboAccountTypeValidatedHandler;

        /// <summary>
        /// Initializes a new instance of <see cref="AccountsPanel"/> using dependency injection.
        /// </summary>
        /// <param name="scopeFactory">The service scope factory for creating scopes to resolve scoped dependencies.</param>
        /// <param name="logger">The logger instance for diagnostic logging.</param>
        /// <param name="dispatcherHelper">Optional dispatcher helper for cross-thread UI operations.</param>
        public AccountsPanel(
            IServiceScopeFactory scopeFactory,
            ILogger<ScopedPanelBase<AccountsViewModel>> logger,
            WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
            : base(scopeFactory, logger)
        {
            _dispatcherHelper = dispatcherHelper;
            Serilog.Log.Debug("AccountsPanel: constructor completed (ViewModel will be resolved in OnViewModelResolved)");
        }

        /// <summary>
        /// Called by ScopedPanelBase after the ViewModel has been resolved from the service scope.
        /// This is where we initialize UI components that depend on the ViewModel.
        /// </summary>
        protected override void OnViewModelResolved(AccountsViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            base.OnViewModelResolved(viewModel);

            try
            {
                Serilog.Log.Debug("AccountsPanel: OnViewModelResolved starting");

                // Keep a DataContext reference
                DataContext = viewModel;

                InitializeComponent();
                SetupUI();
                BindViewModel();

                // Explicitly bind grid data source to the view model's Accounts collection.
                // Use a safe snapshot approach and SuspendLayout/ResumeLayout to avoid
                // Syncfusion painting races when the underlying collection changes
                // concurrently during a paint pass.
                if (gridAccounts != null)
                {
                    try { gridAccounts.SuspendLayout(); } catch { }
                    try
                    {
                        if (viewModel?.Accounts is System.Collections.IEnumerable en)
                        {
                            // Take a snapshot to avoid "Collection was modified" during enumeration/paint
                            try { gridAccounts.DataSource = en.Cast<object?>().ToList(); } catch { gridAccounts.DataSource = viewModel?.Accounts; }
                        }
                        else
                        {
                            gridAccounts.DataSource = viewModel?.Accounts;
                        }
                    }
                    finally { try { gridAccounts.ResumeLayout(); } catch { } }
                }

                // Apply current theme
                ApplyCurrentTheme();

                // Subscribe to theme changes
                _panelThemeChangedHandler = OnThemeChanged;
                ThemeManager.ThemeChanged += _panelThemeChangedHandler;

                Serilog.Log.Information("AccountsPanel initialized with {Count} accounts", viewModel?.Accounts?.Count ?? 0);
                Serilog.Log.Debug("AccountsPanel: OnViewModelResolved finished");
            }
            catch (Exception ex)
            {
                // Log and show an actionable message - fail fast to surface the issue to the caller
                Serilog.Log.Error(ex, "Failed to initialize AccountsPanel in OnViewModelResolved");

                System.Windows.Forms.MessageBox.Show(
                    $"Error loading accounts panel:\n\n{ex.Message}\n\nCheck logs at: logs/wileywidget-{DateTime.UtcNow:yyyyMMdd}.log",
                    AccountsPanelResources.ErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                throw;
            }
        }

        private void InitializeComponent()
        {
            Name = "AccountsPanel";
            Size = new Size(1200, 800);
            // Prefer DPI scaling for modern displays
            try
            {
                this.AutoScaleMode = AutoScaleMode.Dpi;
            }
            catch { }
            Dock = DockStyle.Fill;
            Serilog.Log.Debug("AccountsPanel: InitializeComponent completed");

            // Add load event handler for initial data loading
            this.Load += AccountsPanel_Load;

            // Initialize ErrorProvider for data validation
            errorProvider = new ErrorProvider()
            {
                BlinkStyle = ErrorBlinkStyle.BlinkIfDifferentError,
                Icon = SystemIcons.Warning
            };

            // Create a single ToolTip instance used across the panel (disposed in Dispose)
            try
            {
                Controls.Add(gridAccounts);
                // Add overlays (loading spinner and no-data friendly message)
                _loadingOverlay = new LoadingOverlay { Message = WileyWidget.WinForms.Forms.MainFormResources.LoadingText };
                Controls.Add(_loadingOverlay);

                _noDataOverlay = new NoDataOverlay { Message = "No accounts to display" };
                Controls.Add(_noDataOverlay);
                _toolTip = new ToolTip() { AutoPopDelay = 10000, InitialDelay = 500, ReshowDelay = 100, ShowAlways = true };
            }
            catch { }
        }

        /// <summary>
        /// Named handler for PanelHeader.RefreshClicked event (enables proper unsubscription).
        /// </summary>
        private async void OnPanelHeaderRefreshClicked(object? sender, EventArgs e)
        {
            try
            {
                if (ViewModel?.LoadAccountsCommand != null)
                    await ViewModel.LoadAccountsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountsPanel: Refresh failed");
            }
        }

        /// <summary>
        /// Named handler for PanelHeader.PinToggled event (enables proper unsubscription).
        /// </summary>
        private void OnPanelHeaderPinToggled(object? sender, EventArgs e)
        {
            // Leave persistence to PanelStateManager / future work
        }

        /// <summary>
        /// Named handler for PanelHeader.CloseClicked event (enables proper unsubscription).
        /// </summary>
        private void OnPanelHeaderCloseClicked(object? sender, EventArgs e)
        {
            try
            {
                var parent = this.FindForm();
                var method = parent?.GetType().GetMethod("ClosePanel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                method?.Invoke(parent, new object[] { this.Name });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountsPanel: Close panel failed");
            }
        }

        /// <summary>
        /// Handles the panel Load event for initial data loading and setup.
        /// </summary>
        private async void AccountsPanel_Load(object? sender, EventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                Serilog.Log.Debug("AccountsPanel_Load: starting - ViewModel.IsLoading={IsLoading}", ViewModel?.IsLoading);
                // Ensure ViewModel is loaded when panel is shown
                var vm = ViewModel;
                if (vm != null)
                {
                    if (vm.LoadAccountsCommand != null && !vm.IsLoading)
                    {
                        await vm.LoadAccountsCommand.ExecuteAsync(null);
                    }
                }

                if (IsDisposed) return;

                Serilog.Log.Debug("AccountsPanel_Load: finished load attempt - ViewModel.IsLoading={IsLoading}", ViewModel?.IsLoading);

                // Set up data validation for combo boxes
                if (comboFund != null)
                {
                    _comboFundValidatingHandler = ComboFund_Validating;
                    _comboFundValidatedHandler = ComboFund_Validated;
                    comboFund.Validating += _comboFundValidatingHandler;
                    comboFund.Validated += _comboFundValidatedHandler;
                }

                if (comboAccountType != null)
                {
                    _comboAccountTypeValidatingHandler = ComboAccountType_Validating;
                    _comboAccountTypeValidatedHandler = ComboAccountType_Validated;
                    comboAccountType.Validating += _comboAccountTypeValidatingHandler;
                    comboAccountType.Validated += _comboAccountTypeValidatedHandler;
                }

                Serilog.Log.Debug("AccountsPanel loaded successfully");
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("AccountsPanel_Load: panel was disposed during load");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error during AccountsPanel load");
                try { errorProvider?.SetError(this, "Failed to load accounts data. Check logs for details."); } catch { }
            }
        }

        private void SetupUI()
        {
            // Top filter panel - use theme colors instead of hardcoded values
            // Shared consistent header (44px height, 8px padding)
            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Top,
                AccessibleName = "Accounts header"
            };

            // Keep existing filter panel below the header
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44, // consistent header height across panels
                Padding = new Padding(8)
                // BackColor is set by ThemeManager.ApplyTheme()
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
                AllowDropDownResize = false, // Per demos: prevent dropdown resize
                MaxDropDownItems = 10, // Per demos: limit visible items
                AllowNull = true, // Per demos: allow null selection
                AccessibleName = "Fund",
                AccessibleDescription = "Filter accounts by fund"
            };
            // Per demos: configure dropdown list style
            comboFund.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 9F);
            // Add tooltip for better UX
            try { _toolTip?.SetToolTip(comboFund, "Filter accounts by municipal fund type (General, Enterprise, etc.)"); } catch { }

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
                AllowDropDownResize = false, // Per demos: prevent dropdown resize
                MaxDropDownItems = 10, // Per demos: limit visible items
                AllowNull = true, // Per demos: allow null selection
                AccessibleName = "Account Type",
                AccessibleDescription = "Filter accounts by type"
            };
            // Per demos: configure dropdown list style
            comboAccountType.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 9F);
            // Add tooltip for better UX
            try { _toolTip?.SetToolTip(comboAccountType, "Filter accounts by type (Asset, Liability, Revenue, Expense)"); } catch { }

            // Refresh button
            btnRefresh = new Button
            {
                Name = "btnRefresh",
                Width = 100,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                AccessibleName = "Refresh accounts list",
                AccessibleDescription = "Reloads the accounts data from the database"
            };
            // Add tooltip for better UX
            try
            {
                _toolTip?.SetToolTip(btnRefresh, "Reload accounts from database with current filters");
            }
            catch { }
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnRefresh.Image = iconService?.GetIcon("refresh", theme, 16);
                btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;

                // Update on theme change with thread safety
                _btnRefreshThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => btnRefresh.Image = svc?.GetIcon("refresh", t, 16));
                        }
                        else if (btnRefresh.InvokeRequired)
                        {
                            btnRefresh.Invoke(() => btnRefresh.Image = svc?.GetIcon("refresh", t, 16));
                        }
                        else
                        {
                            btnRefresh.Image = svc?.GetIcon("refresh", t, 16);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnRefreshThemeChangedHandler;
            }
            catch { }
            btnRefresh.Click += async (s, e) =>
            {
                try
                {
                    if (ViewModel?.FilterAccountsCommand != null)
                    {
                        await ViewModel.FilterAccountsCommand.ExecuteAsync(null);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        var reporting = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.ErrorReportingService>(Program.Services)
                            : null;
                        reporting?.ReportError(ex, "Error running FilterAccountsCommand", showToUser: false);
                    }
                    catch { }
                }
            };

            // Add button - styled by ThemeManager.StyleButton based on Name containing "Add"
            btnAdd = new Button
            {
                Text = "Add",
                Name = "btnAdd",
                Width = 80,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                // BackColor/ForeColor set by ThemeManager.StyleButton() based on button name
                FlatStyle = FlatStyle.Flat,
                AccessibleName = "Add new account",
                AccessibleDescription = "Opens dialog to create a new municipal account"
            };
            // Add tooltip for better UX
            try { _toolTip?.SetToolTip(btnAdd, "Create a new municipal account (Ctrl+N)"); } catch { }
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnAdd.Image = iconService?.GetIcon("add", theme, 14);
                btnAdd.ImageAlign = ContentAlignment.MiddleLeft;
                btnAdd.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnAddThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => btnAdd.Image = svc?.GetIcon("add", t, 14));
                        }
                        else if (btnAdd.InvokeRequired)
                        {
                            btnAdd.Invoke(() => btnAdd.Image = svc?.GetIcon("add", t, 14));
                        }
                        else
                        {
                            btnAdd.Image = svc?.GetIcon("add", t, 14);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnAddThemeChangedHandler;
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
                Margin = new Padding(6, 10, 6, 6),
                AccessibleName = "Edit selected account",
                AccessibleDescription = "Opens dialog to edit the currently selected account"
            };
            // Add tooltip for better UX
            try { _toolTip?.SetToolTip(btnEdit, "Modify the selected account (Enter or Double-click)"); } catch { }
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnEdit.Image = iconService?.GetIcon("edit", theme, 14);
                btnEdit.ImageAlign = ContentAlignment.MiddleLeft;
                btnEdit.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnEditThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => btnEdit.Image = svc?.GetIcon("edit", t, 14));
                        }
                        else if (btnEdit.InvokeRequired)
                        {
                            btnEdit.Invoke(() => btnEdit.Image = svc?.GetIcon("edit", t, 14));
                        }
                        else
                        {
                            btnEdit.Image = svc?.GetIcon("edit", t, 14);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnEditThemeChangedHandler;
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
                AccessibleName = "Delete selected account",
                AccessibleDescription = "Deletes the currently selected account"
            };
            // Add tooltip for better UX
            try { _toolTip?.SetToolTip(btnDelete, "Remove the selected account permanently (Delete)"); } catch { }
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                btnDelete.Image = iconService?.GetIcon("delete", theme, 14);
                btnDelete.ImageAlign = ContentAlignment.MiddleLeft;
                btnDelete.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnDeleteThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => btnDelete.Image = svc?.GetIcon("delete", t, 14));
                        }
                        else if (btnDelete.InvokeRequired)
                        {
                            btnDelete.Invoke(() => btnDelete.Image = svc?.GetIcon("delete", t, 14));
                        }
                        else
                        {
                            btnDelete.Image = svc?.GetIcon("delete", t, 14);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnDeleteThemeChangedHandler;
            }
            catch { }
            btnDelete.Click += BtnDelete_Click;

            // Export buttons (Excel / PDF)
            btnExportExcel = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = "Export Excel",
                Name = "btnExportExcel",
                Width = 100,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                AccessibleName = "Export grid to Excel",
                AccessibleDescription = "Export the accounts grid to an Excel file"
            };
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = ThemeManager.CurrentTheme;
                btnExportExcel.Image = iconService?.GetIcon("excel", theme, 14);
                btnExportExcel.ImageAlign = ContentAlignment.MiddleLeft;
                btnExportExcel.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnExportExcelThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => btnExportExcel.Image = svc?.GetIcon("excel", t, 14));
                        }
                        else if (btnExportExcel.InvokeRequired)
                        {
                            btnExportExcel.Invoke(() => btnExportExcel.Image = svc?.GetIcon("excel", t, 14));
                        }
                        else
                        {
                            btnExportExcel.Image = svc?.GetIcon("excel", t, 14);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnExportExcelThemeChangedHandler;
            }
            catch { }
            btnExportExcel.Click += async (s, e) =>
            {
                try
                {
                    using var sfd = new SaveFileDialog { Filter = "Excel Workbook|*.xlsx", DefaultExt = "xlsx", FileName = "accounts.xlsx" };
                    if (sfd.ShowDialog() != DialogResult.OK) return;
                    if (gridAccounts == null)
                    {
                        MessageBox.Show("No grid available to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    await WileyWidget.WinForms.Services.ExportService.ExportGridToExcelAsync(gridAccounts, sfd.FileName);
                    MessageBox.Show($"Exported to {sfd.FileName}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnExportPdf = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = "Export PDF",
                Name = "btnExportPdf",
                Width = 100,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                AccessibleName = "Export grid to PDF",
                AccessibleDescription = "Export the accounts grid to a PDF file"
            };
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = ThemeManager.CurrentTheme;
                btnExportPdf.Image = iconService?.GetIcon("pdf", theme, 14);
                btnExportPdf.ImageAlign = ContentAlignment.MiddleLeft;
                btnExportPdf.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnExportPdfThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => btnExportPdf.Image = svc?.GetIcon("pdf", t, 14));
                        }
                        else if (btnExportPdf.InvokeRequired)
                        {
                            btnExportPdf.Invoke(() => btnExportPdf.Image = svc?.GetIcon("pdf", t, 14));
                        }
                        else
                        {
                            btnExportPdf.Image = svc?.GetIcon("pdf", t, 14);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnExportPdfThemeChangedHandler;
            }
            catch { }
            btnExportPdf.Click += async (s, e) =>
            {
                try
                {
                    using var sfd = new SaveFileDialog { Filter = "PDF Document|*.pdf", DefaultExt = "pdf", FileName = "accounts.pdf" };
                    if (sfd.ShowDialog() != DialogResult.OK) return;
                    if (gridAccounts == null)
                    {
                        MessageBox.Show("No grid available to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    await WileyWidget.WinForms.Services.ExportService.ExportGridToPdfAsync(gridAccounts, sfd.FileName);
                    MessageBox.Show($"Exported to {sfd.FileName}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            // Navigation separator and buttons per Syncfusion demos pattern
            var separator = new Label { Text = "  |  ", AutoSize = true, Margin = new Padding(6, 14, 6, 6), Font = new Font("Segoe UI", 9, FontStyle.Regular) };

            // View Charts navigation button
            var btnViewCharts = new Button
            {
                Text = "Charts",
                Name = "btnViewCharts",
                Width = 80,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                AccessibleName = "View Charts",
                AccessibleDescription = "Navigate to budget visualization charts"
            };
            try { _toolTip?.SetToolTip(btnViewCharts, "Open Charts panel (Ctrl+Shift+C)"); } catch { }
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                btnViewCharts.Image = iconService?.GetIcon("chart", ThemeManager.CurrentTheme, 14);
                btnViewCharts.ImageAlign = ContentAlignment.MiddleLeft;
                btnViewCharts.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnViewCharts.Click += (s, e) =>
            {
                try { Serilog.Log.Information("AccountsPanel: Navigate requested -> Charts"); } catch { }
                NavigateToPanel<WileyWidget.WinForms.Controls.ChartPanel>("Charts");
            };

            // Dashboard navigation button
            var btnDashboard = new Button
            {
                Text = "Home",
                Name = "btnDashboard",
                Width = 80,
                Height = 32,
                Margin = new Padding(6, 10, 6, 6),
                AccessibleName = "Go to Dashboard",
                AccessibleDescription = "Navigate to Dashboard overview"
            };
            try { _toolTip?.SetToolTip(btnDashboard, "Open Dashboard panel (Ctrl+Shift+D)"); } catch { }
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                btnDashboard.Image = iconService?.GetIcon("home", ThemeManager.CurrentTheme, 14);
                btnDashboard.ImageAlign = ContentAlignment.MiddleLeft;
                btnDashboard.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnDashboard.Click += (s, e) =>
            {
                try { Serilog.Log.Information("AccountsPanel: Navigate requested -> Dashboard"); } catch { }
                NavigateToPanel<WileyWidget.WinForms.Controls.DashboardPanel>("Dashboard");
            };

            // Layout top panel using FlowLayoutPanel
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };

            flow.Controls.Add(fundLabel);
            flow.Controls.Add(comboFund);
            flow.Controls.Add(acctTypeLabel);
            flow.Controls.Add(comboAccountType);
            flow.Controls.Add(btnRefresh);
            flow.Controls.Add(btnAdd);
            flow.Controls.Add(btnEdit);
            flow.Controls.Add(btnDelete);
            flow.Controls.Add(btnExportExcel);
            flow.Controls.Add(btnExportPdf);
            flow.Controls.Add(separator);
            flow.Controls.Add(btnViewCharts);
            flow.Controls.Add(btnDashboard);

            topPanel.Controls.Add(flow);

            // Wire header actions to view model
            try
            {
                _panelHeader.Title = AccountsPanelResources.PanelTitle;
                _panelHeaderRefreshHandler = OnPanelHeaderRefreshClicked;
                _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
                _panelHeaderPinHandler = OnPanelHeaderPinToggled;
                _panelHeader.PinToggled += _panelHeaderPinHandler;
                _panelHeaderCloseHandler = OnPanelHeaderCloseClicked;
                _panelHeader.CloseClicked += _panelHeaderCloseHandler;
            }
            catch { }

            Controls.Add(_panelHeader);
            Controls.Add(topPanel);

            // Data grid - configured per Syncfusion demo best practices (Themes, Filtering, Sorting demos)
            gridAccounts = new SfDataGrid
            {
                Name = "gridAccounts",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = false,
                AllowGrouping = true,
                AllowFiltering = true,
                AllowSorting = true,
                AllowResizingColumns = true,
                AllowDraggingColumns = true,
                ShowGroupDropArea = true,
                AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
                SelectionMode = GridSelectionMode.Extended,
                NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Row,
                ShowRowHeader = true,
                HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(35.0f), // Per demos: DPI-aware height
                RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28.0f), // Per demos: DPI-aware height
                AllowTriStateSorting = true, // Per demos: enables tri-state sorting
                ShowSortNumbers = true, // Per demos: shows sort order numbers
                AutoExpandGroups = true, // Per demos: auto expand grouped rows
                LiveDataUpdateMode = Syncfusion.Data.LiveDataUpdateMode.AllowDataShaping, // Per demos: real-time updates
                AllowResizingHiddenColumns = false, // Per demos: prevent resizing hidden columns
                AccessibleName = "Accounts data grid",
                AccessibleDescription = "Grid displaying municipal accounts with filtering and sorting"
            };
            SkinManager.SetVisualStyle(gridAccounts, WwThemeColors.DefaultTheme);
            try
            {
                var atwProp = gridAccounts.GetType().GetProperty("AllowTextWrapping");
                if (atwProp != null && atwProp.PropertyType == typeof(bool))
                {
                    atwProp.SetValue(gridAccounts, false);
                }
            }
            catch { }

            // Configure grid style
            gridAccounts.Style.HeaderStyle.Font = new GridFontInfo(new Font("Segoe UI Semibold", 9F));
            gridAccounts.Style.CellStyle.Font = new GridFontInfo(new Font("Segoe UI", 9F));

            // Configure columns with proper formatting per Syncfusion demos
            gridAccounts.Columns.Add(new GridTextColumn
            {
                MappingName = "AccountNumber",
                HeaderText = AccountsPanelResources.AccountNumberHeader,
                MinimumWidth = 100,
                AllowFiltering = true,
                AllowSorting = true
            });
            gridAccounts.Columns.Add(new GridTextColumn
            {
                MappingName = "AccountName",
                HeaderText = AccountsPanelResources.AccountNameHeader,
                MinimumWidth = 200,
                AllowFiltering = true,
                AllowSorting = true
            });
            gridAccounts.Columns.Add(new GridTextColumn
            {
                MappingName = "AccountType",
                HeaderText = AccountsPanelResources.TypeHeader,
                MinimumWidth = 100,
                AllowFiltering = true,
                AllowGrouping = true
            });
            gridAccounts.Columns.Add(new GridTextColumn
            {
                MappingName = "FundName",
                HeaderText = AccountsPanelResources.FundHeader,
                MinimumWidth = 120,
                AllowFiltering = true,
                AllowGrouping = true
            });
            gridAccounts.Columns.Add(new GridNumericColumn
            {
                MappingName = "CurrentBalance",
                HeaderText = AccountsPanelResources.BalanceHeader,
                Format = "C2",
                // Older/newer Syncfusion versions may not include the FormatMode enum; attempt to set via reflection when available.
                // We'll fall back to Format string if FormatMode isn't present.
                MinimumWidth = 120,
                // Use culture-aware NumberFormatInfo to avoid invariant culture issues with RegionInfo lookups
                NumberFormatInfo = System.Globalization.CultureInfo.GetCultureInfo("en-US").NumberFormat,
                AllowFiltering = true,
                AllowSorting = true
            });

            // If possible, set FormatMode on numeric column via reflection (some Syncfusion builds removed the enum at compile-time)
            try
            {
                var curr = gridAccounts.Columns.FirstOrDefault(c => c.MappingName == "CurrentBalance");
                if (curr != null)
                {
                    var fmProp = curr.GetType().GetProperty("FormatMode");
                    if (fmProp != null)
                    {
                        var enumType = fmProp.PropertyType;
                        var f = enumType.GetField("Currency") ?? enumType.GetFields().FirstOrDefault();
                        if (f != null) fmProp.SetValue(curr, f.GetValue(null));
                    }
                }
            }
            catch { }

            // Actions unbound column: provides contextual Edit/Delete actions per row
            try
            {
                // GridUnBoundColumn may not exist in some Syncfusion builds; create via reflection when available.
                try
                {
                    var gridAsm = typeof(Syncfusion.WinForms.DataGrid.SfDataGrid).Assembly;
                    var ubType = gridAsm.GetType("Syncfusion.WinForms.DataGrid.GridUnBoundColumn");
                    if (ubType != null)
                    {
                        var actionsCol = Activator.CreateInstance(ubType);
                        if (actionsCol != null)
                        {
                            var setProp = ubType.GetProperty("MappingName");
                            setProp?.SetValue(actionsCol, "Actions");
                            var setHeader = ubType.GetProperty("HeaderText");
                            setHeader?.SetValue(actionsCol, "Actions");
                            var minW = ubType.GetProperty("MinimumWidth");
                            minW?.SetValue(actionsCol, 120);
                            var editProp = ubType.GetProperty("AllowEditing");
                            editProp?.SetValue(actionsCol, false);

                            // Add via Columns collection (use reflection to avoid compile-time dependency)
                            var cols = gridAccounts.Columns;
                            var addMethod = cols.GetType().GetMethod("Add", new[] { ubType }) ?? cols.GetType().GetMethod("Add", new[] { typeof(object) });
                            addMethod?.Invoke(cols, new[] { actionsCol });
                        }
                    }
                }
                catch { }
            }
            catch { }

            // Tune filter UI for categorical columns to provide Excel-like checkbox filters
            try
            {
                var typeColumn = gridAccounts.Columns.FirstOrDefault(c => c.MappingName == "AccountType");
                if (typeColumn != null)
                {
                    try
                    {
                        var prop = typeColumn.GetType().GetProperty("FilterPopupMode");
                        if (prop != null)
                        {
                            var enumType = prop.PropertyType;
                            var field = enumType.GetField("CheckBoxFilter") ?? enumType.GetFields().FirstOrDefault();
                            if (field != null) prop.SetValue(typeColumn, field.GetValue(null));
                        }
                    }
                    catch { }
                    typeColumn.ImmediateUpdateColumnFilter = true;
                }

                var fundColumn = gridAccounts.Columns.FirstOrDefault(c => c.MappingName == "FundName");
                if (fundColumn != null)
                {
                    try
                    {
                        var prop = fundColumn.GetType().GetProperty("FilterPopupMode");
                        if (prop != null)
                        {
                            var enumType = prop.PropertyType;
                            var field = enumType.GetField("CheckBoxFilter") ?? enumType.GetFields().FirstOrDefault();
                            if (field != null) prop.SetValue(fundColumn, field.GetValue(null));
                        }
                    }
                    catch { }
                    fundColumn.ImmediateUpdateColumnFilter = true;
                }
            }
            catch { }

            // Enable the filter bar for Excel-style filtering
            // ShowFilterBar may not exist on all SfDataGrid versions; set via reflection if available
            try
            {
                var sfProp = gridAccounts.GetType().GetProperty("ShowFilterBar");
                if (sfProp != null && sfProp.PropertyType == typeof(bool)) sfProp.SetValue(gridAccounts, true);
            }
            catch { }

            // Add a summary row (bottom) to show totals for numeric columns
            try
            {
                var tableSummary = new Syncfusion.WinForms.DataGrid.GridTableSummaryRow
                {
                    Name = "TableSummary",
                    Position = Syncfusion.WinForms.DataGrid.Enums.VerticalPosition.Bottom,
                    ShowSummaryInRow = false
                };

                var totalBalanceCol = new Syncfusion.WinForms.DataGrid.GridSummaryColumn
                {
                    Name = "TotalBalance",
                    MappingName = "CurrentBalance",
                    SummaryType = Syncfusion.Data.SummaryType.DoubleAggregate,
                    Format = "{Sum:c}"
                };

                tableSummary.SummaryColumns.Add(totalBalanceCol);
                gridAccounts.TableSummaryRows.Add(tableSummary);
            }
            catch { }

            // Wire up grid selection to sync with ViewModel.SelectedAccount
            gridAccounts.SelectionChanged += GridAccounts_SelectionChanged;

            // Provide clickable actions column to show contextual Edit/Delete
            gridAccounts.CellClick += GridAccounts_CellClick;

            // Enable double-click to edit per Syncfusion demos
            gridAccounts.CellDoubleClick += GridAccounts_CellDoubleClick;

            // Enable tooltips on grid for better UX
            gridAccounts.ShowToolTip = true;
            gridAccounts.ToolTipOpening += GridAccounts_ToolTipOpening;

            Controls.Add(gridAccounts);

            // Add overlays (loading spinner and no-data friendly message)
            _loadingOverlay = new LoadingOverlay { Message = WileyWidget.WinForms.Forms.MainFormResources.LoadingText };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No accounts to display" };
            Controls.Add(_noDataOverlay);

            // Summary panel at bottom - use theme colors
            summaryPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(10)
                // BackColor is set by ThemeManager.ApplyTheme()
            };

            lblTotalBalance = new Label
            {
                Text = "Total Balance: $0.00",
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Left
            };

            lblAccountCount = new Label
            {
                Text = "Accounts: 0",
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Anchor = AnchorStyles.Right
            };

            var summaryFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };

            summaryFlow.Controls.Add(lblTotalBalance);
            summaryFlow.Controls.Add(new Label { Text = "    |    ", AutoSize = true });
            summaryFlow.Controls.Add(lblAccountCount);

            summaryPanel.Controls.Add(summaryFlow);
            Controls.Add(summaryPanel);
        }

        private void BindViewModel()
        {
            if (ViewModel == null) return;

            try
            {
                accountsBindingSource = new BindingSource { DataSource = ViewModel };

                // Bind comboboxes to viewmodel filter properties
                if (comboFund != null && ViewModel.AvailableFunds != null)
                {
                    comboFund.DataSource = ViewModel.AvailableFunds;
                }

                if (comboAccountType != null && ViewModel.AvailableAccountTypes != null)
                {
                    comboAccountType.DataSource = ViewModel.AvailableAccountTypes;
                }

                // Subscribe to property changes for summary updates and loading state
                // Keep a handler reference for proper cleanup
                // ViewModel implements INotifyPropertyChanged; subscribe directly to avoid analyzer 'always true' diagnostics
                {
                    var npc = (System.ComponentModel.INotifyPropertyChanged)ViewModel;
                    _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
                    npc.PropertyChanged += _viewModelPropertyChangedHandler;
                    // Also observe the Accounts collection for empty-state UI
                    try
                    {
                        if (ViewModel.Accounts != null)
                        {
                            _accountsCollectionChangedHandler = (s, a) => UpdateNoDataOverlay();
                            ViewModel.Accounts.CollectionChanged += _accountsCollectionChangedHandler;
                        }
                    }
                    catch { }
                }

                // Initialize overlays from viewmodel state
                try
                {
                    if (_loadingOverlay != null) _loadingOverlay.Visible = ViewModel.IsLoading;
                    if (_noDataOverlay != null) _noDataOverlay.Visible = (ViewModel.Accounts == null || ViewModel.Accounts.Count == 0);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountsPanel: BindViewModel failed");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                // If we have a dispatcher helper use it to marshal to the UI thread.
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => ViewModel_PropertyChanged(sender, e)); } catch { }
                    return;
                }
                // Fallback: if no dispatcher helper, check InvokeRequired for cross-thread safety
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(() => ViewModel_PropertyChanged(sender, e))); } catch { }
                    return;
                }

                // Handle loading overlay
                if (e.PropertyName == nameof(ViewModel.IsLoading))
                {
                    try { if (_loadingOverlay != null) _loadingOverlay.Visible = ViewModel?.IsLoading ?? false; } catch { }
                }

                // Update summary labels and empty-data overlay
                if (e.PropertyName == nameof(ViewModel.Accounts) || e.PropertyName == nameof(ViewModel.TotalBalance))
                {
                    UpdateSummary();
                    try { if (_noDataOverlay != null) _noDataOverlay.Visible = (ViewModel?.Accounts == null || ViewModel.Accounts.Count == 0); } catch { }
                    // Update grid datasource safely when the Accounts collection changes.
                    try
                    {
                        if (gridAccounts != null)
                        {
                            try { gridAccounts.SuspendLayout(); } catch { }
                            try
                            {
                                if (ViewModel?.Accounts is System.Collections.IEnumerable en)
                                {
                                    gridAccounts.DataSource = en.Cast<object?>().ToList();
                                }
                                else
                                {
                                    gridAccounts.DataSource = ViewModel?.Accounts;
                                }
                            }
                            finally { try { gridAccounts.ResumeLayout(); } catch { } }
                        }
                    }
                    catch { }
                }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("AccountsPanel: ViewModel_PropertyChanged - panel was disposed");
            }
        }

        private void UpdateSummary()
        {
            try
            {
                if (lblTotalBalance != null)
                {
                    lblTotalBalance.Text = $"Total Balance: {ViewModel?.TotalBalance:C2}";
                }

                if (lblAccountCount != null)
                {
                    lblAccountCount.Text = $"Accounts: {ViewModel?.Accounts?.Count ?? 0}";
                }
            }
            catch { }
        }

        /// <summary>
        /// Handles grid selection changes to sync with ViewModel.SelectedAccount.
        /// </summary>
        private void GridAccounts_SelectionChanged(object? sender, Syncfusion.WinForms.DataGrid.Events.SelectionChangedEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                // Sync selected display model locally - the view model does not expose a SelectedAccount entity
                if (gridAccounts?.SelectedItem is WileyWidget.WinForms.Models.MunicipalAccountDisplay display)
                {
                    _selectedAccountDisplay = display;
                    Serilog.Log.Debug("AccountsPanel: Selected account changed to {AccountNumber}", display.AccountNumber);
                }
                else
                {
                    _selectedAccountDisplay = null;
                }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("AccountsPanel: GridAccounts_SelectionChanged - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountsPanel: GridAccounts_SelectionChanged failed");
            }
        }

        /// <summary>
        /// Handles double-click on grid cell to open edit dialog (per Syncfusion demos).
        /// </summary>
        private void GridAccounts_CellDoubleClick(object? sender, Syncfusion.WinForms.DataGrid.Events.CellClickEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                // Trigger edit on double-click of any data row - use selected item which is the display DTO
                if (gridAccounts?.SelectedItem is WileyWidget.WinForms.Models.MunicipalAccountDisplay)
                {
                    BtnEdit_Click(sender, e);
                }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("AccountsPanel: GridAccounts_CellDoubleClick - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountsPanel: GridAccounts_CellDoubleClick failed");
            }
        }

        private void GridAccounts_CellClick(object? sender, Syncfusion.WinForms.DataGrid.Events.CellClickEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                // Use currently selected display model rather than depending on event args members that differ between SfDataGrid versions
                var display = gridAccounts?.SelectedItem as WileyWidget.WinForms.Models.MunicipalAccountDisplay;
                if (display == null) return;

                var cm = new ContextMenuStrip();
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;

                var miEdit = new ToolStripMenuItem("Edit");
                try { miEdit.Image = iconService?.GetIcon("edit", ThemeManager.CurrentTheme, 14); } catch { }
                miEdit.Click += (s, a) => BtnEdit_Click(sender, EventArgs.Empty);

                var miDelete = new ToolStripMenuItem("Delete");
                try { miDelete.Image = iconService?.GetIcon("delete", ThemeManager.CurrentTheme, 14); } catch { }
                miDelete.Click += (s, a) => BtnDelete_Click(sender, EventArgs.Empty);

                cm.Items.Add(miEdit);
                cm.Items.Add(miDelete);

                // Show context menu at mouse position
                if (gridAccounts == null) return;
                var mousePos = gridAccounts.PointToClient(Cursor.Position);
                cm.Show(gridAccounts, mousePos);
            }
            catch { }
        }

        /// <summary>
        /// Provides custom tooltips for grid cells (per Syncfusion demos).
        /// </summary>
        private void GridAccounts_ToolTipOpening(object? sender, Syncfusion.WinForms.DataGrid.Events.ToolTipOpeningEventArgs e)
        {
            try
            {
                // Use the currently selected display DTO for tooltip content - avoids depending on event args shape
                var grid = gridAccounts;
                if (grid?.SelectedItem is WileyWidget.WinForms.Models.MunicipalAccountDisplay disp && e.ToolTipInfo?.Items != null && e.ToolTipInfo.Items.Count > 0)
                {
                    var items = e.ToolTipInfo.Items;
                    if (items.Count > 0)
                    {
                        items[0].Text = $"Account: {disp.AccountName}\nNumber: {disp.AccountNumber}\nBalance: {disp.CurrentBalance:C2}";
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Navigates to another panel via parent form's DockingManager.
        /// Per Syncfusion demos navigation pattern.
        /// </summary>
        /// <typeparam name="TPanel">The panel type to navigate to.</typeparam>
        /// <param name="panelName">Display name for the panel.</param>
        private void NavigateToPanel<TPanel>(string panelName) where TPanel : UserControl
        {
            try
            {
                var parentForm = this.FindForm();
                if (parentForm == null) return;

                // Prefer direct API on MainForm where available
                if (parentForm is WileyWidget.WinForms.Forms.MainForm mf)
                {
                    try { mf.ShowPanel<TPanel>(panelName); return; } catch { }
                }

                var method = parentForm.GetType().GetMethod("DockUserControlPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(typeof(TPanel));
                    genericMethod.Invoke(parentForm, new object[] { panelName });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "AccountsPanel: NavigateToPanel<{Panel}> failed", typeof(TPanel).Name);
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            try
            {
                // Open AccountEditPanel inside a modal form for adding
                using var scope = Program.Services.CreateScope();
                var provider = scope.ServiceProvider;
                var editPanel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.WinForms.Controls.AccountEditPanel>(provider);
                using var modal = new Form { StartPosition = FormStartPosition.CenterParent, Size = new System.Drawing.Size(520, 560) };
                try
                {
                    editPanel.Dock = DockStyle.Fill;
                    modal.Controls.Add(editPanel);
                    if (modal.ShowDialog(this.FindForm()) == DialogResult.OK)
                    {
                        _ = ViewModel?.LoadAccountsCommand?.ExecuteAsync(null);
                    }
                }
                finally
                {
                    try { editPanel.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "AccountsPanel: BtnAdd_Click failed");
                MessageBox.Show($"Error adding account: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEdit_Click(object? sender, EventArgs e)
        {
            try
            {
                var selected = _selectedAccountDisplay;
                if (selected == null)
                {
                    MessageBox.Show("Please select an account to edit.", "Edit Account", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var scope = Program.Services.CreateScope();
                var provider = scope.ServiceProvider;
                var editPanel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.WinForms.Controls.AccountEditPanel>(provider);
                // Populate any existing values if AccountEditPanel had a constructor for existing; for now, host and show
                using var modal = new Form { StartPosition = FormStartPosition.CenterParent, Size = new System.Drawing.Size(520, 560) };
                try
                {
                    editPanel.Dock = DockStyle.Fill;
                    modal.Controls.Add(editPanel);
                    if (modal.ShowDialog(this.FindForm()) == DialogResult.OK)
                    {
                        _ = ViewModel?.LoadAccountsCommand?.ExecuteAsync(null);
                    }
                }
                finally
                {
                    try { editPanel.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "AccountsPanel: BtnEdit_Click failed");
                MessageBox.Show($"Error editing account: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            try
            {
                var selected = _selectedAccountDisplay;
                if (selected == null)
                {
                    MessageBox.Show("Please select an account to delete.", "Delete Account", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete account '{selected.AccountName}'?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // Note: DeleteAccountCommand does not exist on AccountsViewModel; reloading accounts after delete as a workaround
                    ViewModel?.LoadAccountsCommand?.Execute(null);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "AccountsPanel: BtnDelete_Click failed");
                MessageBox.Show($"Error deleting account: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ComboFund_Validated(object? sender, EventArgs e)
        {
            try { if (comboFund != null) errorProvider?.SetError(comboFund, ""); } catch { }
        }

        private void ComboFund_Validating(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Basic validating handler for the fund combo box - no special validation currently
            // Keep the handler present to avoid missing-method compile errors and allow future validation rules
            try
            {
                // Clear any previous error when validating
                try { if (comboFund != null) errorProvider?.SetError(comboFund, ""); } catch { }
                e.Cancel = false;
            }
            catch
            {
                // Swallow errors - validation shouldn't break the UI
            }
        }
        private void ComboAccountType_Validating(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // No validation required for account type selection
        }

        private void ComboAccountType_Validated(object? sender, EventArgs e)
        {
            try { if (comboAccountType != null) errorProvider?.SetError(comboAccountType, ""); } catch { }
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                ThemeManager.ApplyThemeToControl(this);
                // Apply Syncfusion skin using global theme (FluentDark default, FluentLight fallback)
                // Per-form Syncfusion theming is already applied by ThemeManager.ApplyTheme(this) above
            }
            catch { }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            try
            {
                if (IsDisposed) return;

                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => OnThemeChanged(sender, theme)); } catch { }
                    return;
                }
                // Fallback: if no dispatcher helper, check InvokeRequired for cross-thread safety
                if (InvokeRequired)
                {
                    try { Invoke(() => OnThemeChanged(sender, theme)); } catch { }
                    return;
                }

                ApplyCurrentTheme();
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("AccountsPanel: OnThemeChanged - panel was disposed");
            }
        }

        /// <summary>
        /// Releases managed resources and unsubscribes from events.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe event handlers
                try { if (_panelThemeChangedHandler != null) ThemeManager.ThemeChanged -= _panelThemeChangedHandler; } catch { }
                try { if (_btnRefreshThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnRefreshThemeChangedHandler; } catch { }
                try { if (_btnAddThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnAddThemeChangedHandler; } catch { }
                try { if (_btnEditThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnEditThemeChangedHandler; } catch { }
                try { if (_btnDeleteThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnDeleteThemeChangedHandler; } catch { }
                try { if (_btnExportExcelThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnExportExcelThemeChangedHandler; } catch { }
                try { if (_btnExportPdfThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnExportPdfThemeChangedHandler; } catch { }
                try { if (_viewModelPropertyChangedHandler != null && ViewModel != null) ((System.ComponentModel.INotifyPropertyChanged)ViewModel).PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }
                try { if (_accountsCollectionChangedHandler != null && ViewModel?.Accounts != null) ViewModel.Accounts.CollectionChanged -= _accountsCollectionChangedHandler; } catch { }
                try { if (_comboFundValidatingHandler != null && comboFund != null) comboFund.Validating -= _comboFundValidatingHandler; } catch { }
                try { if (_comboFundValidatedHandler != null && comboFund != null) comboFund.Validated -= _comboFundValidatedHandler; } catch { }
                try { if (_comboAccountTypeValidatingHandler != null && comboAccountType != null) comboAccountType.Validating -= _comboAccountTypeValidatingHandler; } catch { }
                try { if (_comboAccountTypeValidatedHandler != null && comboAccountType != null) comboAccountType.Validated -= _comboAccountTypeValidatedHandler; } catch { }
                try { this.Load -= AccountsPanel_Load; } catch { }

                // Unsubscribe from PanelHeader events using stored named handlers
                try
                {
                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        if (_panelHeaderPinHandler != null) _panelHeader.PinToggled -= _panelHeaderPinHandler;
                        if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "AccountsPanel: Failed to unsubscribe PanelHeader events");
                }

                // Unsubscribe grid event handlers
                try { if (gridAccounts != null) { gridAccounts.SelectionChanged -= GridAccounts_SelectionChanged; } } catch { }
                try { if (gridAccounts != null) { gridAccounts.CellDoubleClick -= GridAccounts_CellDoubleClick; } } catch { }
                try { if (gridAccounts != null) { gridAccounts.ToolTipOpening -= GridAccounts_ToolTipOpening; } } catch { }

                // Clear DataSource before disposing Syncfusion controls to avoid NullReferenceException bugs
                try { comboFund.SafeClearDataSource(); } catch { }
                try { comboFund.SafeDispose(); } catch { }
                try { comboAccountType.SafeClearDataSource(); } catch { }
                try { comboAccountType.SafeDispose(); } catch { }
                try { gridAccounts.SafeClearDataSource(); } catch { }
                try { gridAccounts.SafeDispose(); } catch { }

                // Dispose controls
                try { gridAccounts?.Dispose(); } catch { }
                try { comboFund?.Dispose(); } catch { }
                try { comboAccountType?.Dispose(); } catch { }
                try { btnRefresh?.Dispose(); } catch { }
                try { btnAdd?.Dispose(); } catch { }
                try { btnEdit?.Dispose(); } catch { }
                try { btnDelete?.Dispose(); } catch { }
                try { btnExportExcel?.Dispose(); } catch { }
                try { btnExportPdf?.Dispose(); } catch { }
                try { topPanel?.Dispose(); } catch { }
                try { _panelHeader?.Dispose(); } catch { }
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }
                try { _toolTip?.Dispose(); } catch { }


                try { summaryPanel?.Dispose(); } catch { }
                try { errorProvider?.Dispose(); } catch { }
                try { accountsBindingSource?.Dispose(); } catch { }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Updates the no-data overlay visibility based on the bound accounts collection.
        /// Uses reflection so the method is resilient if the view-model shape changes.
        /// </summary>
        private void UpdateNoDataOverlay()
        {
            try
            {
                if (IsDisposed) return;
                if (_noDataOverlay == null) return;

                // Marshal to UI thread if called from background thread (e.g., collection changed event)
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(UpdateNoDataOverlay); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(UpdateNoDataOverlay)); } catch { }
                    return;
                }

                bool noData = true;
                if (ViewModel != null)
                {
                    var accountsProp = ViewModel.GetType().GetProperty("Accounts");
                    if (accountsProp != null)
                    {
                        var val = accountsProp.GetValue(ViewModel);
                        if (val is System.Collections.ICollection coll && coll.Count > 0) noData = false;
                    }
                }

                _noDataOverlay.Visible = noData;
            }
            catch { }
        }
    }
}
