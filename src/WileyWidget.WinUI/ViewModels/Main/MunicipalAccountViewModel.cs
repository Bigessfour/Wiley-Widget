using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class MunicipalAccountViewModel : ObservableRecipient
    {
        private readonly ILogger<MunicipalAccountViewModel> _logger;

        [ObservableProperty]
        private string title = "Municipal Accounts";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<MunicipalAccount> accounts = new();

        public MunicipalAccountViewModel(ILogger<MunicipalAccountViewModel> logger)
        {
            _logger = logger;
            LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
        }

        public IAsyncRelayCommand LoadAccountsCommand { get; }

        private async Task LoadAccountsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading municipal accounts");
                // Placeholder implementation
                Accounts.Clear();
                _logger.LogInformation("Municipal accounts loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load municipal accounts");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Municipal Accounts View");
            LoadAccountsCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Municipal Accounts View");
        }
    }

    public class MunicipalAccount
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }
}