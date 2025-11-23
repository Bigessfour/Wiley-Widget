using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class UtilityCustomerViewModel : ObservableRecipient
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

        private Task LoadCustomersAsync()
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
            return Task.CompletedTask;
        }
    }

    public class UtilityCustomer
    {
        public string CustomerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
    }
}