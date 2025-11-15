using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class AnalyticsViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<AnalyticsViewModel> _logger;

        private string _title = "Analytics";
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

        private ObservableCollection<AnalyticsMetric> _metrics = new();
        public ObservableCollection<AnalyticsMetric> Metrics
        {
            get => _metrics;
            set => SetProperty(ref _metrics, value);
        }

        public AnalyticsViewModel(ILogger<AnalyticsViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Analytics View");
            LoadAnalyticsAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Analytics View");
        }

        private async Task LoadAnalyticsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading analytics data");
                // Load analytics data here
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
    }

    public class AnalyticsMetric
    {
        public string MetricName { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}