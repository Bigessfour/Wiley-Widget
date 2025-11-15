using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.ViewModels.Main
{
    public class QuickBooksViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<QuickBooksViewModel> _logger;
        private readonly IQuickBooksService _quickBooksService;

        private string _title = "QuickBooks Integration";
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

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        private ObservableCollection<QuickBooksItem> _items = new();
        public ObservableCollection<QuickBooksItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public QuickBooksViewModel(ILogger<QuickBooksViewModel> logger, IQuickBooksService quickBooksService)
        {
            _logger = logger;
            _quickBooksService = quickBooksService;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to QuickBooks View");
            CheckConnectionAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from QuickBooks View");
        }

        private async Task CheckConnectionAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Checking QuickBooks connection");
                IsConnected = await _quickBooksService.IsConnectedAsync();
                _logger.LogInformation("QuickBooks connection status: {Status}", IsConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check QuickBooks connection");
                IsConnected = false;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class QuickBooksItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}