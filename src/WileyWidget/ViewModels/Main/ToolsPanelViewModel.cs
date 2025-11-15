using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Tools Panel - provides access to various tools
    /// </summary>
    public class ToolsPanelViewModel : BindableBase, INavigationAware
    {
        private bool _isToolRunning;
        private string _currentTool;

        public bool IsToolRunning
        {
            get => _isToolRunning;
            set => SetProperty(ref _isToolRunning, value);
        }

        public string CurrentTool
        {
            get => _currentTool;
            set => SetProperty(ref _currentTool, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("ToolsPanelViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("ToolsPanelViewModel navigated from");
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