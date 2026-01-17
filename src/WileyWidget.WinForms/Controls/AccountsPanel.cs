using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// Lightweight Accounts panel that hosts the AccountsViewModel in a dockable UserControl.
/// Provides a minimal grid so navigation can dock the control without runtime errors.
/// </summary>
public partial class AccountsPanel : UserControl
{
    private readonly AccountsViewModel _viewModel;
    private readonly ILogger<AccountsPanel> _logger;

    private PanelHeader? _header;
    private Syncfusion.WinForms.DataGrid.SfDataGrid? _accountsGrid;
    private TableLayoutPanel? _layout;

    public AccountsPanel(AccountsViewModel viewModel, ILogger<AccountsPanel> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeControls();
        BindViewModel();

        Load += AccountsPanel_Load;
    }

    private void AccountsPanel_Load(object? sender, EventArgs e)
    {
        // Note: Data loading is now handled by ILazyLoadViewModel via DockingManager events
    }

    private void InitializeControls()
    {
        SuspendLayout();

        Name = "AccountsPanel";
        Dock = DockStyle.Fill;

        _header = new PanelHeader
        {
            Title = "Municipal Accounts",
            Dock = DockStyle.Top,
            Height = 42
        };

        _accountsGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowSorting = true,
            AllowFiltering = true,
            RowHeight = 36
        };

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
    }

    private void BindViewModel()
    {
        if (_accountsGrid != null)
        {
            _accountsGrid.DataSource = _viewModel.Accounts;
        }

        if (_header != null)
        {
            _header.Title = _viewModel.Title;
        }
    }
}
