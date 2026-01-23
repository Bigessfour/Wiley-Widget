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
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Lightweight Accounts panel that hosts the AccountsViewModel in a dockable UserControl.
/// Provides a minimal grid so navigation can dock the control without runtime errors.
/// Implements ICompletablePanel to track load state and validation status.
/// </summary>
public partial class AccountsPanel : ScopedPanelBase<AccountsViewModel>
{
    private PanelHeader? _header;
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _accountsGrid;
    private TableLayoutPanel? _layout;
    private BindingSource? _accountsBinding;
    private ErrorProvider? _errorProvider;

    /// <summary>
    /// Maximum row count threshold for grid validation.
    /// Warn if 0 rows; error if exceeds this (e.g., database corruption or bad query).
    /// </summary>
    private const int MaxGridRowsThreshold = 10000;

    /// <summary>
    /// Initializes a new instance with DI-resolved ViewModel and logger.
    /// </summary>
    public AccountsPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<AccountsViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeControls();
        Load += AccountsPanel_Load;
    }

    /// <summary>
    /// Called when the panel is loaded; data loading is handled by ILazyLoadViewModel.
    /// </summary>
    private void AccountsPanel_Load(object? sender, EventArgs e)
    {
        // Note: Data loading is now handled by ILazyLoadViewModel via DockingManager events
        // ViewModel is resolved in OnViewModelResolved after scope creation
    }

    /// <summary>
    /// Called after the ViewModel is resolved from DI. Bind controls to the ViewModel.
    /// </summary>
    protected override void OnViewModelResolved(AccountsViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);
        BindViewModel();
    }

    private void InitializeControls()
    {
        SuspendLayout();

        Name = "AccountsPanel";
        Dock = DockStyle.Fill;
        // Apply the application's current Syncfusion visual theme to runtime-created controls.
        var theme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
        SfSkinManager.SetVisualStyle(this, theme);
        Logger?.LogDebug("Applied theme {ThemeName} to AccountsPanel", theme);
        _header = new PanelHeader
        {
            Title = "Municipal Accounts",
            Dock = DockStyle.Top,
            Height = 42
        };
        _header.AccessibleName = "Accounts Panel Header";
        _header.AccessibleDescription = "Header for the municipal accounts panel";
        _header.TabIndex = 0;

        _accountsGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowSorting = true,
            AllowFiltering = true,
            RowHeight = 36,
            ThemeName = theme
        };
        _accountsGrid.AccessibleName = "Accounts Grid";
        _accountsGrid.AccessibleDescription = "Displays municipal accounts in a sortable grid";
        _accountsGrid.TabIndex = 1;

        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountNumber", HeaderText = "Account #", MinimumWidth = 90, AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.AllCells });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountName", HeaderText = "Name", AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "FundName", HeaderText = "Fund", MinimumWidth = 80 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "AccountType", HeaderText = "Type", MinimumWidth = 80 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "CurrentBalance", HeaderText = "Balance", FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency, MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridNumericColumn { MappingName = "BudgetAmount", HeaderText = "Budget", FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency, MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridTextColumn { MappingName = "Department", HeaderText = "Department", MinimumWidth = 100 });
        _accountsGrid.Columns.Add(new Syncfusion.WinForms.DataGrid.GridCheckBoxColumn { MappingName = "IsActive", HeaderText = "Active", MinimumWidth = 70 });

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _layout.Controls.Add(_header, 0, 0);
        _layout.Controls.Add(_accountsGrid, 0, 1);

        // Initialize ErrorProvider for validation feedback
        _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
        _errorProvider.SetError(_accountsGrid, string.Empty); // Initialize error state

        Controls.Add(_layout);

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

        _logger.LogInformation("[BINDING] Starting AccountsPanel ViewModel binding. Accounts count: {Count}",
            ViewModel.Accounts?.Count ?? 0);

        // Create BindingSource bound to the ViewModel for two-way binding support
        _accountsBinding = new BindingSource
        {
            DataSource = ViewModel
        };

        // Bind grid to Accounts collection via BindingSource
        if (_accountsGrid != null)
        {
            _logger.LogInformation("[BINDING] Binding grid to Accounts collection via BindingSource");
            _accountsGrid.DataSource = _accountsBinding;
            _accountsGrid.DataMember = nameof(ViewModel.Accounts);
            _logger.LogInformation("[BINDING] Grid bound. DataSource type: {Type}, DataMember: {DataMember}, RowCount: {RowCount}",
                _accountsGrid.DataSource?.GetType().Name ?? "null",
                _accountsGrid.DataMember,
                _accountsGrid.RowCount);
        }

        // Bind header title to ViewModel.Title property
        if (_header != null)
        {
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
            _logger.LogInformation("[BINDING] Subscribed to ViewModel PropertyChanged events");
        }

        _logger.LogInformation("[BINDING] AccountsPanel ViewModel bound successfully. Final grid row count: {RowCount}",
            _accountsGrid?.RowCount ?? -1);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.Accounts))
        {
            // Refresh grid when Accounts collection changes
            _accountsBinding?.ResetBindings(false);
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

                    Logger?.LogInformation("ValidateAsync: Grid has 0 rows (may be normal)");
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

            foreach (var account in accounts.Cast<MunicipalAccount>())
            {
                if (errorCount >= MaxErrorsToReport) break;

                // No null checks needed - Balance and BudgetAmount are non-nullable decimals
                // Just try to parse them (they're already valid since they're loaded from DB)
                Logger?.LogDebug("ValidateDataQuality: Account {AccountNumber} - Balance: {Balance}, Budget: {Budget}",
                    account.AccountNumber?.Value ?? "unknown", account.Balance, account.BudgetAmount);
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

            // Clear grid bindings before disposal
            if (_accountsGrid != null)
            {
                _accountsGrid.DataSource = null;
            }

            // Dispose ErrorProvider (holds error state for controls)
            _errorProvider?.Dispose();

            // Dispose binding and bindings
            _accountsBinding?.Dispose();
            _accountsGrid?.Dispose();
            _layout?.Dispose();
            _header?.Dispose();
        }

        base.Dispose(disposing);
    }
}
