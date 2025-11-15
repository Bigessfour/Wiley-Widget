using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.ViewModels.Main
{
    /// <summary>
    /// ViewModel for Dashboard Panel - displays dashboard items and metrics
    /// </summary>
    public class DashboardPanelViewModel : BindableBase, INavigationAware
    {
        private readonly IDashboardService _dashboardService;
        private ObservableCollection<DashboardItem> _dashboardItems = new();

        public ObservableCollection<DashboardItem> DashboardItems
        {
            get => _dashboardItems;
            set => SetProperty(ref _dashboardItems, value);
        }

        public DashboardPanelViewModel(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Log.Debug("DashboardPanelViewModel navigated to");
            await LoadDashboardItemsAsync();
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Log.Debug("DashboardPanelViewModel navigated from");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // Synchronous navigation handler
        }

        private async Task LoadDashboardItemsAsync()
        {
            try
            {
                var items = await _dashboardService.GetDashboardItemsAsync();
                DashboardItems = new ObservableCollection<DashboardItem>(items);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load dashboard items");
            }
        }
    }
}