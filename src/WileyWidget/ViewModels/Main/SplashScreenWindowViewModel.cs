using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Splash Screen Window - handles application startup display
    /// </summary>
    public class SplashScreenWindowViewModel : BindableBase, INavigationAware
    {
        private string _statusMessage = "Initializing...";
        private int _progressValue;
        private bool _isComplete;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public bool IsComplete
        {
            get => _isComplete;
            set => SetProperty(ref _isComplete, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("SplashScreenWindowViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("SplashScreenWindowViewModel navigated from");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // Synchronous navigation handler
        }

        public void UpdateStatus(string message, int progress)
        {
            StatusMessage = message;
            ProgressValue = progress;
        }

        public void MarkComplete()
        {
            IsComplete = true;
            StatusMessage = "Ready";
            ProgressValue = 100;
        }
    }
}