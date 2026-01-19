using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    /// Called when the panel is loaded; delegates to async initialization if needed.
    /// </summary>
    private async void AccountsPanel_Load(object? sender, EventArgs e)
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
        // Try to apply the application's current Syncfusion visual theme to runtime-created controls.
        try
        {
            var theme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme;
            if (!string.IsNullOrEmpty(theme))
            {
                Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, theme);
            }
        }
        catch
        {
            // best-effort; never throw from UI init
        }
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
            RowHeight = 36
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
            _logger.LogWarning("ViewModel is null; skipping binding");
            return;
        }

        // Use a BindingSource so the grid can react to collection changes and null lists safely.
        _accountsBinding = new BindingSource
        {
            // Avoid referencing concrete view model types here; fall back to an empty enumerable if null.
            DataSource = (object)ViewModel.Accounts ?? Array.Empty<object>()
        };

        if (_accountsGrid != null)
        {
            _accountsGrid.DataSource = _accountsBinding;
        }

        if (_header != null)
        {
            _header.Title = ViewModel.Title ?? string.Empty;
        }
    }

    /// <summary>
    /// Validate the accounts grid. Accounts panel is read-only, so always valid if data loads.
    /// </summary>
    public override Task<ValidationResult> ValidateAsync(CancellationToken ct)
    {
        // Accounts are loaded from the database and read-only in this panel.
        // If we reach this, the ViewModel resolved and data loaded successfully.
        return Task.FromResult(ValidationResult.Success);
    }

    /// <summary>
    /// Save is a no-op for the read-only Accounts panel.
    /// </summary>
    public override Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

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

            _accountsBinding?.Dispose();
            _accountsGrid?.Dispose();
            _layout?.Dispose();
            _header?.Dispose();
        }

        base.Dispose(disposing);
    }
}
