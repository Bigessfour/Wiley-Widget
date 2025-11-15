using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.ViewModels.Main
{
    public class DashboardViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IDashboardService _dashboardService;

        private string _title = "Dashboard";
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

        private ObservableCollection<DashboardItem> _dashboardItems = new();
        public ObservableCollection<DashboardItem> DashboardItems
        {
            get => _dashboardItems;
            set => SetProperty(ref _dashboardItems, value);
        }

        public DashboardViewModel(ILogger<DashboardViewModel> logger, IDashboardService dashboardService)
        {
            _logger = logger;
            _dashboardService = dashboardService;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Dashboard View");
            LoadDashboardAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Dashboard View");
        }

        private async Task LoadDashboardAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading dashboard data");
                // Load dashboard data here
                _logger.LogInformation("Dashboard data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard data");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class DashboardItem
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}