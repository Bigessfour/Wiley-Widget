using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Error Dialog - displays error information
    /// </summary>
    public class ErrorDialogViewModel : BindableBase, INavigationAware
    {
        private string _errorMessage;
        private string _errorDetails;
        private Exception _exception;

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string ErrorDetails
        {
            get => _errorDetails;
            set => SetProperty(ref _errorDetails, value);
        }

        public Exception Exception
        {
            get => _exception;
            set => SetProperty(ref _exception, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("ErrorDialogViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("ErrorDialogViewModel navigated from");
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