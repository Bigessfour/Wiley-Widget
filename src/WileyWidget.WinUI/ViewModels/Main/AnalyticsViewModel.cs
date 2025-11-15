using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class AnalyticsViewModel : ObservableRecipient, INavigationAware
    {
        private readonly ILogger<AnalyticsViewModel> _logger;

        [ObservableProperty]
        private string title = "Analytics";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<AnalyticsMetric> metrics = new();

        public AnalyticsViewModel(ILogger<AnalyticsViewModel> logger)
        {
            _logger = logger;
            LoadAnalyticsCommand = new AsyncRelayCommand(LoadAnalyticsAsync);
        }

        public IAsyncRelayCommand LoadAnalyticsCommand { get; }

        private async Task LoadAnalyticsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading analytics data");
                Metrics.Clear();
                _logger.LogInformation("Analytics data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load analytics data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Analytics View");
            LoadAnalyticsCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Analytics View");
        }
    }

    public class AnalyticsMetric
    {
        public string MetricName { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}