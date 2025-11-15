using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;
using WileyWidget.ViewModels.Messages;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class DashboardViewModel : ObservableRecipient, INavigationAware
    {
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IRegionManager _regionManager;
        private readonly IDashboardService _dashboardService;

        [ObservableProperty]
        private string title = "Dashboard";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<DashboardItem> dashboardItems = new();

        public DashboardViewModel(
            ILogger<DashboardViewModel> logger,
            IRegionManager regionManager,
            IDashboardService dashboardService)
        {
            _logger = logger;
            _regionManager = regionManager;
            _dashboardService = dashboardService;

            LoadDashboardCommand = new AsyncRelayCommand(LoadDashboardAsync);
        }

        public IAsyncRelayCommand LoadDashboardCommand { get; }

        private async Task LoadDashboardAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading dashboard data");

                var items = await _dashboardService.GetDashboardItemsAsync();
                DashboardItems.Clear();

                foreach (var item in items)
                {
                    DashboardItems.Add(item);
                }

                _logger.LogInformation("Dashboard loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Dashboard");
            LoadDashboardCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Dashboard");
        }
    }

    public class DashboardItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}