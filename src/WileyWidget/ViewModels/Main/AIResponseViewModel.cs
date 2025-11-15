using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for AI Response display and interaction
    /// </summary>
    public class AIResponseViewModel : BindableBase, INavigationAware
    {
        private string _responseText;
        private bool _isLoading;
        private string _currentQuery;

        public string ResponseText
        {
            get => _responseText;
            set => SetProperty(ref _responseText, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string CurrentQuery
        {
            get => _currentQuery;
            set => SetProperty(ref _currentQuery, value);
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("AIResponseViewModel navigated to");
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("AIResponseViewModel navigated from");
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