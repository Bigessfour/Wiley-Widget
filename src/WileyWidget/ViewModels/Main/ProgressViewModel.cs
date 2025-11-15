using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Progress View - handles progress display and status updates
    /// </summary>
    public class ProgressViewModel : BindableBase, INavigationAware
    {
        private string _currentOperation = "Initializing...";
        private int _progressPercentage;
        private bool _isIndeterminate;
        private string _statusMessage = "Ready";

        public string CurrentOperation
        {
            get => _currentOperation;
            set => SetProperty(ref _currentOperation, value);
        }

        public int ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("ProgressViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("ProgressViewModel navigated from");
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