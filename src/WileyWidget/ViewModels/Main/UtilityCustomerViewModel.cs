using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class UtilityCustomerViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<UtilityCustomerViewModel> _logger;

        private string _title = "Utility Customers";
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

        private ObservableCollection<UtilityCustomer> _customers = new();
        public ObservableCollection<UtilityCustomer> Customers
        {
            get => _customers;
            set => SetProperty(ref _customers, value);
        }

        public UtilityCustomerViewModel(ILogger<UtilityCustomerViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Utility Customers View");
            LoadCustomersAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Utility Customers View");
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading utility customers");
                // Load utility customers here
                _logger.LogInformation("Utility customers loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load utility customers");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class UtilityCustomer
    {
        public string CustomerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
    }
}