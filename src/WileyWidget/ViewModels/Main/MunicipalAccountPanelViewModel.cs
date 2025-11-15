using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Municipal Account Panel - manages municipal accounts
    /// </summary>
    public class MunicipalAccountPanelViewModel : BindableBase, INavigationAware
    {
        private string _selectedMunicipality;
        private int _accountCount;
        private decimal _totalBalance;

        public string SelectedMunicipality
        {
            get => _selectedMunicipality;
            set => SetProperty(ref _selectedMunicipality, value);
        }

        public int AccountCount
        {
            get => _accountCount;
            set => SetProperty(ref _accountCount, value);
        }

        public decimal TotalBalance
        {
            get => _totalBalance;
            set => SetProperty(ref _totalBalance, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("MunicipalAccountPanelViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("MunicipalAccountPanelViewModel navigated from");
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