using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class ToolsViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<ToolsViewModel> _logger;

        private string _title = "Tools";
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

        private ObservableCollection<Tool> _tools = new();
        public ObservableCollection<Tool> Tools
        {
            get => _tools;
            set => SetProperty(ref _tools, value);
        }

        public ToolsViewModel(ILogger<ToolsViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Tools View");
            LoadToolsAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Tools View");
        }

        private async Task LoadToolsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading tools");
                // Load tools here
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
    }

    public class Tool
    {
        public string ToolId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}