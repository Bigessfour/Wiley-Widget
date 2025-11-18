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
    public partial class ToolsViewModel : ObservableRecipient
    {
        private readonly ILogger<ToolsViewModel> _logger;

        [ObservableProperty]
        private string title = "Tools";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<Tool> tools = new();

        public ToolsViewModel(ILogger<ToolsViewModel> logger)
        {
            _logger = logger;
            LoadToolsCommand = new AsyncRelayCommand(LoadToolsAsync);
        }

        public IAsyncRelayCommand LoadToolsCommand { get; }

        private async Task LoadToolsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading tools");
                Tools.Clear();
                _logger.LogInformation("Tools loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tools");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Tools View");
            LoadToolsCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Tools View");
        }
    }

    public class Tool
    {
        public string ToolId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}