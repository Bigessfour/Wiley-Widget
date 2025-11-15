using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class EnterpriseViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<EnterpriseViewModel> _logger;

        private string _title = "Enterprise Management";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private ObservableCollection<EnterpriseItem> _enterprises = new();
        public ObservableCollection<EnterpriseItem> Enterprises
        {
            get => _enterprises;
            set => SetProperty(ref _enterprises, value);
        }

        public EnterpriseViewModel(ILogger<EnterpriseViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Enterprise View");
            LoadEnterprisesAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Enterprise View");
        }

        private async Task LoadEnterprisesAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading enterprise data");
                // Load enterprise data here
                _logger.LogInformation("Enterprise data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load enterprise data");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class EnterpriseItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}