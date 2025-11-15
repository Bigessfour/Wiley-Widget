using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for About Window - displays application information
    /// </summary>
    public class AboutViewModel : BindableBase, INavigationAware
    {
        private string _applicationName = "Wiley Widget";
        private string _version = "1.0.0";
        private string _description = "Enterprise financial management application";

        public string ApplicationName
        {
            get => _applicationName;
            set => SetProperty(ref _applicationName, value);
        }

        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("AboutViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("AboutViewModel navigated from");
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