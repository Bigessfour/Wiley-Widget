using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class MainViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<MainViewModel> _logger;

        private string _title = "Wiley Widget";
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

        private ObservableCollection<MenuItem> _menuItems = new();
        public ObservableCollection<MenuItem> MenuItems
        {
            get => _menuItems;
            set => SetProperty(ref _menuItems, value);
        }

        public MainViewModel(ILogger<MainViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Main View");
            LoadMenuItems();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Main View");
        }

        private void LoadMenuItems()
        {
            MenuItems.Clear();
            MenuItems.Add(new MenuItem { Name = "Dashboard", ViewName = "DashboardView" });
            MenuItems.Add(new MenuItem { Name = "Budget", ViewName = "BudgetView" });
            MenuItems.Add(new MenuItem { Name = "QuickBooks", ViewName = "QuickBooksView" });
            MenuItems.Add(new MenuItem { Name = "Settings", ViewName = "SettingsView" });
        }
    }

    public class MenuItem
    {
        public string Name { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
    }
}