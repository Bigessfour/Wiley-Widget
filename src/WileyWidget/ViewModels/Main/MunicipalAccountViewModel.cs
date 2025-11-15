using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class MunicipalAccountViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<MunicipalAccountViewModel> _logger;

        private string _title = "Municipal Accounts";
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

        private ObservableCollection<MunicipalAccount> _accounts = new();
        public ObservableCollection<MunicipalAccount> Accounts
        {
            get => _accounts;
            set => SetProperty(ref _accounts, value);
        }

        public MunicipalAccountViewModel(ILogger<MunicipalAccountViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Municipal Accounts View");
            LoadAccountsAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Municipal Accounts View");
        }

        private async Task LoadAccountsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading municipal accounts");
                // Load municipal accounts here
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
    }

    public class MunicipalAccount
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }
}