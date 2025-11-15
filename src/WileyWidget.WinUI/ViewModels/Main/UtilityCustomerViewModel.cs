using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class UtilityCustomerViewModel : ObservableRecipient, INavigationAware
    {
        private readonly ILogger<UtilityCustomerViewModel> _logger;

        [ObservableProperty]
        private string title = "Utility Customers";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<UtilityCustomer> customers = new();

        public UtilityCustomerViewModel(ILogger<UtilityCustomerViewModel> logger)
        {
            _logger = logger;
            LoadCustomersCommand = new AsyncRelayCommand(LoadCustomersAsync);
        }

        public IAsyncRelayCommand LoadCustomersCommand { get; }

        private async Task LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading utility customers");
                Customers.Clear();
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

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Utility Customers View");
            LoadCustomersCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Utility Customers View");
        }
    }

    public class UtilityCustomer
    {
        public string CustomerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
    }
}