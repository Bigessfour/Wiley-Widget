using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation;
using Prism.Navigation.Regions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class QuickBooksViewModel : ObservableRecipient
    {
        private readonly ILogger<QuickBooksViewModel> _logger;
        private readonly IRegionManager _regionManager;
        private readonly IQuickBooksService _quickBooksService;

        [ObservableProperty]
        private string title = "QuickBooks Integration";

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string connectionStatus = "Not Connected";

        [ObservableProperty]
        private ObservableCollection<QuickBooksItem> quickBooksItems = new();

        public QuickBooksViewModel(
            ILogger<QuickBooksViewModel> logger,
            IRegionManager regionManager,
            IQuickBooksService quickBooksService)
        {
            _logger = logger;
            _regionManager = regionManager;
            _quickBooksService = quickBooksService;

            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
            SyncCommand = new AsyncRelayCommand(SyncAsync);
        }

        public IAsyncRelayCommand ConnectCommand { get; }
        public IAsyncRelayCommand DisconnectCommand { get; }
        public IAsyncRelayCommand SyncCommand { get; }

        private async Task ConnectAsync()
        {
            try
            {
                IsLoading = true;
                ConnectionStatus = "Connecting...";

                var result = await _quickBooksService.ConnectAsync();
                IsConnected = result;
                ConnectionStatus = result ? "Connected" : "Connection Failed";

                if (result)
                {
                    await LoadQuickBooksDataAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to QuickBooks");
                ConnectionStatus = "Connection Error";
                IsConnected = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                await _quickBooksService.DisconnectAsync();
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                QuickBooksItems.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disconnect from QuickBooks");
            }
        }

        private async Task SyncAsync()
        {
            if (!IsConnected) return;

            try
            {
                IsLoading = true;
                ConnectionStatus = "Syncing...";

                var result = await _quickBooksService.SyncDataAsync();
                if (result.Success)
                {
                    await LoadQuickBooksDataAsync();
                    ConnectionStatus = "Synced";
                }
                else
                {
                    ConnectionStatus = $"Sync failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync with QuickBooks");
                ConnectionStatus = "Sync Error";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadQuickBooksDataAsync()
        {
            try
            {
                // Load various QuickBooks data
                var customers = await _quickBooksService.GetCustomersAsync();
                QuickBooksItems.Clear();

                foreach (var customer in customers)
                {
                    QuickBooksItems.Add(new QuickBooksItem
                    {
                        Name = customer.DisplayName ?? "Unknown",
                        Type = "Customer",
                        Amount = 0,
                        LastModified = DateTime.Now,
                        IsActive = customer.Active
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load QuickBooks data");
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to QuickBooks View");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from QuickBooks View");
        }
    }

    public class QuickBooksItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsActive { get; set; }
    }
}