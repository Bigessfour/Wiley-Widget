using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Utility Customer Panel - manages utility customers
    /// </summary>
    public class UtilityCustomerPanelViewModel : BindableBase, INavigationAware
    {
        private string _selectedUtility;
        private int _customerCount;
        private decimal _totalRevenue;

        public string SelectedUtility
        {
            get => _selectedUtility;
            set => SetProperty(ref _selectedUtility, value);
        }

        public int CustomerCount
        {
            get => _customerCount;
            set => SetProperty(ref _customerCount, value);
        }

        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => SetProperty(ref _totalRevenue, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("UtilityCustomerPanelViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("UtilityCustomerPanelViewModel navigated from");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // Synchronous navigation handler
        }
    }
}