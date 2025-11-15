using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class ReportsViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<ReportsViewModel> _logger;

        private string _title = "Reports";
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

        private ObservableCollection<Report> _reports = new();
        public ObservableCollection<Report> Reports
        {
            get => _reports;
            set => SetProperty(ref _reports, value);
        }

        public ReportsViewModel(ILogger<ReportsViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to Reports View");
            LoadReportsAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from Reports View");
        }

        private async Task LoadReportsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading reports");
                // Load reports here
                _logger.LogInformation("Reports loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load reports");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class Report
    {
        public string ReportId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; }
    }
}