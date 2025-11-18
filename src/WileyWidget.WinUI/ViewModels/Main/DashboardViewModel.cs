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
    public partial class DashboardViewModel : ObservableRecipient
    {
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IRegionManager _regionManager;

        [ObservableProperty]
        private string title = "Dashboard";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<DashboardItem> dashboardItems = new();

        public DashboardViewModel(
            ILogger<DashboardViewModel> logger,
            IRegionManager regionManager)
        {
            _logger = logger;
            _regionManager = regionManager;

            LoadDashboardCommand = new AsyncRelayCommand(LoadDashboardAsync);
        }

        public IAsyncRelayCommand LoadDashboardCommand { get; }

        private async Task LoadDashboardAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading dashboard data");

                // Placeholder for dashboard data loading
                DashboardItems.Clear();
                DashboardItems.Add(new DashboardItem
                {
                    Title = "Total Budget",
                    Description = "Annual budget allocation",
                    Icon = "Money",
                    Count = 0,
                    Status = "Active"
                });

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