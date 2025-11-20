using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WileyWidget.ViewModels;

namespace WileyWidget.Views
{
    public sealed partial class BudgetOverviewPage : Page
    {
        public BudgetOverviewViewModel ViewModel { get; }

        public BudgetOverviewPage()
        {
            this.InitializeComponent();

            // Get ViewModel from DI
            ViewModel = App.Services?.GetService(typeof(BudgetOverviewViewModel)) as BudgetOverviewViewModel ?? new BudgetOverviewViewModel();

            this.DataContext = ViewModel;
        }
    }
}
