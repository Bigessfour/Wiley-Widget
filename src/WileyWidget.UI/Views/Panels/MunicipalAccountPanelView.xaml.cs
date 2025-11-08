using System.Windows.Controls;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.Views.Panels;

/// <summary>
/// Docked panel view for municipal chart of accounts and QuickBooks synchronization.
/// </summary>
public partial class MunicipalAccountPanelView : UserControl
{
    public MunicipalAccountPanelView()
    {
        InitializeComponent();
        Loaded += MunicipalAccountPanelView_Loaded;
    }

    private void MunicipalAccountPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MunicipalAccountViewModel viewModel)
        {
            viewModel.AccountsDataGrid = AccountsDataGrid;
        }
    }
}
