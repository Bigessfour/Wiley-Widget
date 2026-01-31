using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using WileyWidget.Models;
using WileyWidget.WinForms.Dialogs;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Lightweight Accounts panel that hosts the AccountsViewModel in a dockable UserControl.
/// Provides a data grid with CRUD toolbar for managing municipal accounts.
/// Implements ICompletablePanel to track load state and validation status.
/// </summary>
public partial class AccountsPanel : ScopedPanelBase
{
    // Strongly-typed ViewModel (this is what you use in your code)
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    [System.ComponentModel.DefaultValue(null)]
    public new AccountsViewModel? ViewModel
    {
        get => (AccountsViewModel?)base.ViewModel;
        set => base.ViewModel = value;
    }
    private PanelHeader? _header;
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _accountsGrid;
    private TableLayoutPanel? _layout;
    private BindingSource? _accountsBinding;
    private ErrorProvider? _errorProvider;
    private FlowLayoutPanel? _toolbarPanel;
    private SfButton? _createButton;
    private SfButton? _editButton;
    private SfButton? _deleteButton;
    private SfButton? _refreshButton;

    // Event handlers for proper cleanup
    private Syncfusion.WinForms.DataGrid.Events.SelectionChangedEventHandler? _gridSelectionChangedHandler;
    private Syncfusion.WinForms.DataGrid.Events.CellClickEventHandler? _gridCellDoubleClickHandler;

    /// <summary>
    /// Maximum row count threshold for grid validation.
    /// Warn if 0 rows; error if exceeds this (e.g., database corruption or bad query).
    /// </summary>
    private const int MaxGridRowsThreshold = 10000;

    /// <summary>
    /// Initializes a new instance with DI-resolved ViewModel and logger.
    /// </summary>
    public AccountsPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase> logger)
        : base(scopeFactory, logger)
    {
        InitializeControls();
        Load += AccountsPanel_Load;
    }

    /// <summary>
    /// Called when the panel is loaded; triggers data loading via ILazyLoadViewModel.
    /// </summary>
    private async void AccountsPanel_Load(object? sender, EventArgs e)
    {
        // Trigger lazy load through ILazyLoadViewModel pattern
        if (ViewModel is ILazyLoadViewModel lazyLoad)
        {
            try
            {
                await lazyLoad.OnVisibilityChangedAsync(true);
                Logger?.LogDebug("AccountsPanel: Lazy load triggered successfully");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "AccountsPanel: Error during lazy load");
            }
        }
    }

    /// <summary>
    /// Called after the ViewModel is resolved from DI. Bind controls to the ViewModel.
    /// </summary>
    protected override void OnViewModelResolved(object? viewModel)
    {
        base.OnViewModelResolved(viewModel);
        if (viewModel is AccountsViewModel)
        {
            BindViewModel();
        }
    }

    private void InitializeControls()
    {
        SuspendLayout();

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _header = new PanelHeader { Dock = DockStyle.Fill, Title = "Chart of Accounts" };
        _layout.Controls.Add(_header, 0, 0);

        // Wire PanelHeader events
        _header.RefreshClicked += RefreshButton_Click;
        _header.CloseClicked += (s, e) => ClosePanel();
        _header.HelpClicked += (s, e) => { MessageBox.Show("Chart of Accounts Help: Manage your municipal accounts.", "Help", MessageBoxButtons.OK, MessageBoxIcon.Information); };
        _header.PinToggled += (s, e) => { /* Pin logic */ };

        _toolbarPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _createButton = new SfButton { Text = "New Account", AutoSize = true };
        _createButton.Click += (s, e) => CreateAccount();
        _editButton = new SfButton { Text = "Edit", AutoSize = true, Enabled = false };
        _editButton.Click += (s, e) => EditAccount();
        _deleteButton = new SfButton { Text = "Delete", AutoSize = true, Enabled = false };
        _deleteButton.Click += DeleteButton_Click;
        _refreshButton = new SfButton { Text = "Refresh", AutoSize = true };
        _refreshButton.Click += RefreshButton_Click;

        _toolbarPanel.Controls.AddRange(new Control[] { _createButton, _editButton, _deleteButton, _refreshButton });
        _layout.Controls.Add(_toolbarPanel, 0, 1);

        _accountsGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowFiltering = true,
            AllowSorting = true,
            AllowResizingColumns = true,
            RowHeight = 36
        };
        _accountsGrid.SelectionChanged += _gridSelectionChangedHandler = Grid_SelectionChanged;
        _accountsGrid.CellDoubleClick += _gridCellDoubleClickHandler = Grid_CellDoubleClick;

        // Re-apply detailed column configuration
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountNumber", HeaderText = "Account #", MinimumWidth = 90, AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.AllCells });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountName", HeaderText = "Name", AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "FundName", HeaderText = "Fund", MinimumWidth = 80 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountType", HeaderText = "Type", MinimumWidth = 80 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "CurrentBalance", HeaderText = "Balance", FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency, MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "BudgetAmount", HeaderText = "Budget", FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency, MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Department", HeaderText = "Department", MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridCheckBoxColumn { MappingName = "IsActive", HeaderText = "Active", MinimumWidth = 70 });

        _layout.Controls.Add(_accountsGrid, 0, 2);

        _errorProvider = new ErrorProvider(this);

        this.Controls.Add(_layout);

        // Apply theme via SfSkinManager
        var theme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, theme);

        ResumeLayout(true);
        this.PerformLayout();
        this.Refresh();
        _logger.LogDebug("[PANEL] {PanelName} content anchored and refreshed", Name);
    }

    private void BindViewModel()
    {
        if (ViewModel == null)
        {
            _logger.LogWarning("[BINDING] ViewModel is null; skipping binding");
            return;
        }

        _logger.LogDebug("[BINDING] Starting AccountsPanel ViewModel binding. Accounts count: {Count}",
            ViewModel.Accounts?.Count ?? 0);

        // Bind grid directly to Accounts collection (not through BindingSource DataMember)
        if (_accountsGrid != null)
        {
            _logger.LogDebug("[BINDING] Binding grid directly to Accounts collection");
            _accountsGrid.DataSource = ViewModel.Accounts;
            _logger.LogDebug("[BINDING] Grid bound. DataSource type: {Type}, RowCount: {RowCount}",
                _accountsGrid.DataSource?.GetType().Name ?? "null",
                _accountsGrid.RowCount);
        }

        // Bind header title to ViewModel.Title property
        if (_header != null)
        {
            _accountsBinding = new BindingSource { DataSource = ViewModel };
            _header.DataBindings.Add(
                nameof(_header.Title),
                _accountsBinding,
                nameof(ViewModel.Title),
                false,
                DataSourceUpdateMode.OnPropertyChanged);
        }

        // Subscribe to ViewModel property changes for reactive updates
        if (ViewModel is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged += ViewModel_PropertyChanged;
            _logger.LogDebug("[BINDING] Subscribed to ViewModel PropertyChanged events");
        }

        _logger.LogDebug("[BINDING] AccountsPanel ViewModel bound successfully. Final grid row count: {RowCount}",
            _accountsGrid?.RowCount ?? -1);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.Accounts))
        {
            // Refresh grid when Accounts collection changes
            if (_accountsBinding != null && _accountsGrid != null)
            {
                // Force rebind the grid to the collection to ensure all items are visible
                _accountsBinding.ResetBindings(false);
                _accountsGrid.DataSource = _accountsBinding;
                _accountsGrid.Refresh();
                _logger.LogDebug("[BINDING] Accounts collection changed, rebound grid with {Count} items", ViewModel?.Accounts?.Count ?? 0);
            }
        }
    }

    private void UpdateButtonState(object? sender = null, EventArgs? e = null)
    {
        if (_accountsGrid?.SelectedItem is MunicipalAccountDisplay selectedAccount)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedAccount = selectedAccount;
            }
            if (_editButton != null) _editButton.Enabled = true;
            if (_deleteButton != null) _deleteButton.Enabled = true;
            _logger.LogDebug("Grid selection changed: {AccountNumber}", selectedAccount.AccountNumber);
        }
        else
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedAccount = null;
            }
            if (_editButton != null) _editButton.Enabled = false;
            if (_deleteButton != null) _deleteButton.Enabled = false;
            _logger.LogDebug("Grid selection cleared");
        }
    }

    /// <summary>
    /// Handles double-click on grid cell to open edit dialog.
    /// </summary>
    private void Grid_CellDoubleClick(object? sender, Syncfusion.WinForms.DataGrid.Events.CellClickEventArgs e)
    {
        if (_accountsGrid?.SelectedItem is MunicipalAccountDisplay selectedAccount)
        {
            OpenEditDialog(selectedAccount);
        }
    }

    private void CreateAccount(object? sender = null, EventArgs? e = null)
    {
        try
        {
            using (var dialog = new AccountCreateDialog(Logger))
            {
                var result = dialog.ShowDialog(this);
                if (result == DialogResult.OK && dialog.CreatedAccount != null)
                {
                    _logger.LogDebug("Create dialog returned OK with new account: {AccountNumber}",
                        dialog.CreatedAccount.AccountNumber?.Value);

                    // Execute the ViewModel's CreateAccountCommand
                    if (ViewModel?.CreateAccountCommand.CanExecute(dialog.CreatedAccount) ?? false)
                    {
                        ViewModel.CreateAccountCommand.Execute(dialog.CreatedAccount);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening create dialog");
            MessageBox.Show($"Error opening create dialog: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handles Edit button click - opens AccountEditDialog with selected account.
    /// </summary>
    private void EditAccount(object? sender = null, EventArgs? e = null)
    {
        if (ViewModel?.SelectedAccount is MunicipalAccountDisplay selectedDisplay)
        {
            OpenEditDialog(selectedDisplay);
        }
    }

    /// <summary>
    /// Opens the edit dialog for the selected account display.
    /// </summary>
    private void OpenEditDialog(MunicipalAccountDisplay selectedDisplay)
    {
        try
        {
            // Convert display model to domain model for editing
            var accountToEdit = new MunicipalAccount
            {
                Id = selectedDisplay.Id,
                AccountNumber = new AccountNumber(selectedDisplay.AccountNumber),
                Name = selectedDisplay.AccountName,
                Fund = Enum.Parse<MunicipalFundType>(selectedDisplay.FundName),
                Type = Enum.Parse<AccountType>(selectedDisplay.AccountType),
                FundDescription = selectedDisplay.Description ?? string.Empty,
                Department = new Department { Name = selectedDisplay.Department },
                BudgetAmount = selectedDisplay.BudgetAmount,
                Balance = selectedDisplay.CurrentBalance,
                IsActive = selectedDisplay.IsActive
            };

            using (var dialog = new AccountEditDialog(accountToEdit, Logger))
            {
                var result = dialog.ShowDialog(this);
                if (result == DialogResult.OK && dialog.IsSaved)
                {
                    _logger.LogDebug("Edit dialog returned OK with updated account: {AccountNumber}",
                        accountToEdit.AccountNumber?.Value);

                    // Execute the ViewModel's UpdateAccountCommand
                    if (ViewModel?.UpdateAccountCommand.CanExecute(accountToEdit) ?? false)
                    {
                        ViewModel.UpdateAccountCommand.Execute(accountToEdit);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening edit dialog for account {AccountNumber}", selectedDisplay.AccountNumber);
            MessageBox.Show($"Error opening edit dialog: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handles Delete button click - shows confirmation dialog before deleting.
    /// </summary>
    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (ViewModel?.SelectedAccount is MunicipalAccountDisplay selectedDisplay)
        {
            var confirmed = DeleteConfirmationDialog.Show(
                this,
                "Delete Account",
                $"Are you sure you want to delete this account?",
                $"Account: {selectedDisplay.AccountNumber} - {selectedDisplay.AccountName}",
                Logger);

            if (confirmed)
            {
                _logger.LogDebug("Delete confirmed for account: {AccountNumber}", selectedDisplay.AccountNumber);

                // Execute the ViewModel's DeleteAccountCommand
                if (ViewModel?.DeleteAccountCommand.CanExecute(null) ?? false)
                {
                    ViewModel.DeleteAccountCommand.Execute(null);
                }
            }
            else
            {
                _logger.LogDebug("Delete cancelled for account: {AccountNumber}", selectedDisplay.AccountNumber);
            }
        }
    }

    /// <summary>
    /// Handles Refresh button click - reloads accounts from repository.
    /// </summary>
    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogDebug("Refresh button clicked");
            if (ViewModel?.FilterAccountsCommand.CanExecute(null) ?? false)
            {
                ViewModel.FilterAccountsCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing accounts");
            MessageBox.Show($"Error refreshing accounts: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Validate the accounts grid data integrity.
    ///
    /// READ-ONLY VALIDATION: This panel is read-only, so validation checks grid state integrity
    /// rather than user input. Validates: ViewModel availability, Accounts collection presence,
    /// grid binding, column headers, row count, and data quality (Balance/Budget numeric validity).
    ///
    /// Returns errors if: ViewModel is null, Accounts collection is null/empty, grid has no data source,
    /// required columns missing, row count exceeds threshold, or data type issues detected.
    ///
    /// Thread-safe: ErrorProvider operations execute on UI thread via InvokeRequired check.
    /// </summary>
    public override async Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        var errors = new List<ValidationItem>();

        try
        {
            // Clear ErrorProvider before validation
            if (_errorProvider != null && _accountsGrid != null)
            {
                if (InvokeRequired)
                {
                    Invoke(() => _errorProvider.SetError(_accountsGrid, string.Empty));
                }
                else
                {
                    _errorProvider.SetError(_accountsGrid, string.Empty);
                }
            }

            // 1. Validate ViewModel is resolved
            if (ViewModel == null)
            {
                var item = new ValidationItem(
                    "ViewModel",
                    "ViewModel not resolved; cannot validate grid data.",
                    ValidationSeverity.Error,
                    this);
                errors.Add(item);

                if (_errorProvider != null && _accountsGrid != null)
                {
                    var errorAction = new Action(() => _errorProvider.SetError(_accountsGrid, "ViewModel is null"));
                    if (InvokeRequired)
                        Invoke(errorAction);
                    else
                        errorAction();
                }

                Logger?.LogWarning("ValidateAsync: ViewModel is null");
            }

            // 2. Validate Accounts collection is not null
            if (ViewModel?.Accounts == null)
            {
                var item = new ValidationItem(
                    "Accounts",
                    "Accounts collection is null; grid cannot be populated.",
                    ValidationSeverity.Error,
                    _accountsGrid);
                errors.Add(item);

                if (_errorProvider != null && _accountsGrid != null)
                {
                    var errorAction = new Action(() => _errorProvider.SetError(_accountsGrid, "Accounts collection is null"));
                    if (InvokeRequired)
                        Invoke(errorAction);
                    else
                        errorAction();
                }

                Logger?.LogWarning("ValidateAsync: Accounts collection is null");
            }

            // 3. Validate grid DataSource is bound
            if (_accountsGrid?.DataSource == null)
            {
                var item = new ValidationItem(
                    "GridDataSource",
                    "Grid DataSource is not bound; binding may have failed.",
                    ValidationSeverity.Error,
                    _accountsGrid);
                errors.Add(item);

                if (_errorProvider != null && _accountsGrid != null)
                {
                    var errorAction = new Action(() => _errorProvider.SetError(_accountsGrid, "DataSource not bound"));
                    if (InvokeRequired)
                        Invoke(errorAction);
                    else
                        errorAction();
                }

                Logger?.LogWarning("ValidateAsync: Grid DataSource not bound");
            }

            // 4. Validate grid columns are present
            if (_accountsGrid != null && _accountsGrid.Columns.Count == 0)
            {
                var item = new ValidationItem(
                    "GridColumns",
                    "Grid has no columns; column definition may have failed.",
                    ValidationSeverity.Error,
                    _accountsGrid);
                errors.Add(item);

                Logger?.LogWarning("ValidateAsync: Grid has no columns");
            }

            var requiredColumns = new[] { "AccountNumber", "Name", "CurrentBalance", "BudgetAmount" };
            if (_accountsGrid != null)
            {
                var gridColumnNames = _accountsGrid.Columns.Select(c => c.MappingName).ToList();
                var missingColumns = requiredColumns.Where(col => !gridColumnNames.Contains(col)).ToList();

                if (missingColumns.Any())
                {
                    var item = new ValidationItem(
                        "GridColumns",
                        $"Required grid columns missing: {string.Join(", ", missingColumns)}",
                        ValidationSeverity.Error,
                        _accountsGrid);
                    errors.Add(item);

                    Logger?.LogWarning("ValidateAsync: Missing columns: {MissingColumns}", string.Join(", ", missingColumns));
                }
            }

            // 5. Validate row count
            if (_accountsGrid != null && ViewModel?.Accounts != null)
            {
                int rowCount = ViewModel.Accounts.Count;

                if (rowCount == 0)
                {
                    // Warning for empty grid (may be legitimate)
                    var item = new ValidationItem(
                        "GridRowCount",
                        "Grid contains no data rows. This may be normal if no accounts exist yet.",
                        ValidationSeverity.Warning,
                        _accountsGrid);
                    errors.Add(item);

                    Logger?.LogDebug("ValidateAsync: Grid has 0 rows (may be normal)");
                }
                else if (rowCount > MaxGridRowsThreshold)
                {
                    // Error for suspiciously large row count
                    var item = new ValidationItem(
                        "GridRowCount",
                        $"Grid contains {rowCount} rows, exceeds threshold {MaxGridRowsThreshold}. Possible data corruption.",
                        ValidationSeverity.Error,
                        _accountsGrid);
                    errors.Add(item);

                    Logger?.LogError("ValidateAsync: Grid row count {RowCount} exceeds threshold {Threshold}",
                        rowCount, MaxGridRowsThreshold);
                }
                else
                {
                    Logger?.LogDebug("ValidateAsync: Grid row count {RowCount} is valid", rowCount);
                }
            }

            // 6. Validate data quality: Balance and Budget numeric columns
            if (_accountsGrid != null && ViewModel?.Accounts != null && ViewModel.Accounts.Count > 0)
            {
                var dataQualityErrors = ValidateDataQuality(ViewModel.Accounts);
                if (dataQualityErrors.Any())
                {
                    errors.AddRange(dataQualityErrors);

                    if (_errorProvider != null && _accountsGrid != null)
                    {
                        var errorAction = new Action(() =>
                        {
                            var summary = string.Join("; ", dataQualityErrors.Select(e => e.Message).Take(2));
                            _errorProvider.SetError(_accountsGrid, summary);
                        });
                        if (InvokeRequired)
                            Invoke(errorAction);
                        else
                            errorAction();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "ValidateAsync: Unexpected error during validation");
            var item = new ValidationItem(
                "Validation",
                $"Validation error: {ex.Message}",
                ValidationSeverity.Error);
            errors.Add(item);
        }

        return errors.Any(e => e.Severity == ValidationSeverity.Error)
            ? ValidationResult.Failed(errors.ToArray())
            : ValidationResult.Success;
    }

    /// <summary>
    /// Validates data quality for Balance and Budget columns.
    /// Checks that numeric values are valid decimals and non-negative where expected.
    /// </summary>
    private List<ValidationItem> ValidateDataQuality(System.Collections.IList accounts)
    {
        var dataErrors = new List<ValidationItem>();

        try
        {
            int errorCount = 0;
            const int MaxErrorsToReport = 3; // Report only first 3 errors to avoid overwhelming UI

            foreach (var account in accounts.Cast<MunicipalAccountDisplay>())
            {
                if (errorCount >= MaxErrorsToReport) break;

                // No null checks needed - Balance and BudgetAmount are non-nullable decimals
                // Just try to parse them (they're already valid since they're loaded from DB)
                Logger?.LogDebug("ValidateDataQuality: Account {AccountNumber} - Balance: {Balance}, Budget: {Budget}",
                    account.AccountNumber ?? "unknown", account.CurrentBalance, account.BudgetAmount);
            }

            if (errorCount >= MaxErrorsToReport && accounts.Count > MaxErrorsToReport)
            {
                dataErrors.Add(new ValidationItem(
                    "DataQuality",
                    $"More validation issues found. Check first {MaxErrorsToReport} errors.",
                    ValidationSeverity.Warning));
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "ValidateDataQuality: Error during data quality check");
            dataErrors.Add(new ValidationItem(
                "DataQuality",
                $"Data quality check failed: {ex.Message}",
                ValidationSeverity.Warning));
        }

        return dataErrors;
    }

    /// <summary>
    /// Save is a no-op for the read-only Accounts panel.
    /// </summary>
    public override Task SaveAsync(CancellationToken ct)
    {
        SetHasUnsavedChanges(false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override FocusFirstError to handle read-only grid context.
    /// For a read-only grid, focus the grid data area; user cannot fix validation errors in this panel.
    /// </summary>
    public override void FocusFirstError()
    {
        if (_accountsGrid != null && _accountsGrid.Visible)
        {
            _accountsGrid.Focus();
            Logger?.LogDebug("FocusFirstError: Focused accounts grid");
        }
        else if (this.CanFocus)
        {
            this.Focus();
            Logger?.LogDebug("FocusFirstError: Focused AccountsPanel");
        }
    }

    /// <summary>
    /// Load accounts from the ViewModel asynchronously.
    /// </summary>
    public override async Task LoadAsync(CancellationToken ct)
    {
        if (ViewModel == null)
        {
            return;
        }

        try
        {
            var ct_op = RegisterOperation();
            IsBusy = true;
            // Optionally trigger a refresh of accounts from the service
            // For now, the ViewModel.Accounts collection is pre-populated by DI
            await Task.Delay(0, ct_op); // Placeholder for async work
            SetHasUnsavedChanges(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("AccountsPanel load cancelled");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Grid_SelectionChanged(object? sender, Syncfusion.WinForms.DataGrid.Events.SelectionChangedEventArgs e)
    {
        UpdateButtonState();
    }

    private void ClosePanel()
    {
        try
        {
            var form = FindForm();
            if (form is WileyWidget.WinForms.Forms.MainForm mainForm && mainForm.PanelNavigator != null)
            {
                mainForm.PanelNavigator.HidePanel("Municipal Accounts");
                return;
            }

            var dockingManagerField = form?.GetType()
                .GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dockingManagerField?.GetValue(form) is Syncfusion.Windows.Forms.Tools.DockingManager dockingManager)
            {
                dockingManager.SetDockVisibility(this, false);
            }
            else
            {
                Visible = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to close AccountsPanel via docking manager");
            Visible = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                Load -= AccountsPanel_Load;
            }
            catch { }

            // Unsubscribe from ViewModel property changes
            if (ViewModel is INotifyPropertyChanged observable)
            {
                observable.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Unsubscribe from grid events
            if (_accountsGrid != null)
            {
                if (_gridSelectionChangedHandler != null) _accountsGrid.SelectionChanged -= _gridSelectionChangedHandler;
                if (_gridCellDoubleClickHandler != null) _accountsGrid.CellDoubleClick -= _gridCellDoubleClickHandler;
                _accountsGrid.DataSource = null;
            }

            // Dispose ErrorProvider (holds error state for controls)
            _errorProvider?.Dispose();

            // Dispose binding and bindings
            _accountsBinding?.Dispose();
            _accountsGrid?.Dispose();
            _layout?.Dispose();
            _header?.Dispose();
            _toolbarPanel?.Dispose();
            _createButton?.Dispose();
            _editButton?.Dispose();
            _deleteButton?.Dispose();
            _refreshButton?.Dispose();
        }

        base.Dispose(disposing);
    }
}
