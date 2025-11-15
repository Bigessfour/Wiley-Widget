using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Customer Edit Dialog - edits customer information
    /// </summary>
    public class CustomerEditDialogViewModel : BindableBase, INavigationAware
    {
        private string _customerName;
        private string _customerId;
        private string _contactInfo;

        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        public string CustomerId
        {
            get => _customerId;
            set => SetProperty(ref _customerId, value);
        }

        public string ContactInfo
        {
            get => _contactInfo;
            set => SetProperty(ref _contactInfo, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("CustomerEditDialogViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("CustomerEditDialogViewModel navigated from");
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