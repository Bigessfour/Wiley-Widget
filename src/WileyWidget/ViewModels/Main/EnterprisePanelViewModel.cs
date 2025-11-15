using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Enterprise Panel - handles enterprise-level features
    /// </summary>
    public class EnterprisePanelViewModel : BindableBase, INavigationAware
    {
        private string _enterpriseName;
        private int _userCount;
        private bool _isEnterpriseMode;

        public string EnterpriseName
        {
            get => _enterpriseName;
            set => SetProperty(ref _enterpriseName, value);
        }

        public int UserCount
        {
            get => _userCount;
            set => SetProperty(ref _userCount, value);
        }

        public bool IsEnterpriseMode
        {
            get => _isEnterpriseMode;
            set => SetProperty(ref _isEnterpriseMode, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("EnterprisePanelViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("EnterprisePanelViewModel navigated from");
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