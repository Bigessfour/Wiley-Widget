using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Confirmation Dialog - handles user confirmations
    /// </summary>
    public class ConfirmationDialogViewModel : BindableBase, INavigationAware
    {
        private string _message = "Are you sure?";
        private string _title = "Confirm";
        private bool _result;

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("ConfirmationDialogViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("ConfirmationDialogViewModel navigated from");
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