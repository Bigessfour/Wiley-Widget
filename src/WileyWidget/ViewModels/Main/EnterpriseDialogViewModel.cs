using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Enterprise Dialog - manages enterprise settings
    /// </summary>
    public class EnterpriseDialogViewModel : BindableBase, INavigationAware
    {
        private string _enterpriseName;
        private int _userLimit;
        private bool _advancedFeaturesEnabled;

        public string EnterpriseName
        {
            get => _enterpriseName;
            set => SetProperty(ref _enterpriseName, value);
        }

        public int UserLimit
        {
            get => _userLimit;
            set => SetProperty(ref _userLimit, value);
        }

        public bool AdvancedFeaturesEnabled
        {
            get => _advancedFeaturesEnabled;
            set => SetProperty(ref _advancedFeaturesEnabled, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("EnterpriseDialogViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("EnterpriseDialogViewModel navigated from");
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