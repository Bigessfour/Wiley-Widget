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
    public partial class MainViewModel : ObservableRecipient
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IRegionManager _regionManager;

        [ObservableProperty]
        private string title = "Wiley Widget";

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private ObservableCollection<MenuItem> menuItems = new();

        public MainViewModel(
            ILogger<MainViewModel> logger,
            IRegionManager regionManager)
        {
            _logger = logger;
            _regionManager = regionManager;

            InitializeMenuItems();
        }

        private void InitializeMenuItems()
        {
            MenuItems.Add(new MenuItem { Name = "Dashboard", Icon = "Home", IsSelected = true });
            MenuItems.Add(new MenuItem { Name = "QuickBooks", Icon = "Calculator" });
            MenuItems.Add(new MenuItem { Name = "Budget", Icon = "Money" });
            MenuItems.Add(new MenuItem { Name = "Analytics", Icon = "Data" });
            MenuItems.Add(new MenuItem { Name = "Reports", Icon = "Document" });
            MenuItems.Add(new MenuItem { Name = "Tools", Icon = "Settings" });
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Main View");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Main View");
        }
    }

    public partial class MenuItem : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string icon = string.Empty;

        [ObservableProperty]
        private bool isSelected;
    }
}