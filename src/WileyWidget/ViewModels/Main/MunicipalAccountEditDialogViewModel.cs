using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Municipal Account Edit Dialog - edits municipal account information
    /// </summary>
    public class MunicipalAccountEditDialogViewModel : BindableBase, INavigationAware
    {
        private string _accountName;
        private string _municipality;
        private decimal _balance;

        public string AccountName
        {
            get => _accountName;
            set => SetProperty(ref _accountName, value);
        }

        public string Municipality
        {
            get => _municipality;
            set => SetProperty(ref _municipality, value);
        }

        public decimal Balance
        {
            get => _balance;
            set => SetProperty(ref _balance, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("MunicipalAccountEditDialogViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("MunicipalAccountEditDialogViewModel navigated from");
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