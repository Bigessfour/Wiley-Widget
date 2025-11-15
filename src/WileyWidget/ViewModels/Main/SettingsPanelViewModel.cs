using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Settings Panel - manages application settings
    /// </summary>
    public class SettingsPanelViewModel : BindableBase, INavigationAware
    {
        private bool _autoSaveEnabled = true;
        private string _theme = "Light";
        private bool _notificationsEnabled = true;

        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set => SetProperty(ref _autoSaveEnabled, value);
        }

        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        public bool NotificationsEnabled
        {
            get => _notificationsEnabled;
            set => SetProperty(ref _notificationsEnabled, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("SettingsPanelViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("SettingsPanelViewModel navigated from");
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