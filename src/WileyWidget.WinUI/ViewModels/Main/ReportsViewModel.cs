using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class ReportsViewModel : ObservableRecipient
    {
        private readonly ILogger<ReportsViewModel> _logger;

        [ObservableProperty]
        private string title = "Reports";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<Report> reports = new();

        public ReportsViewModel(ILogger<ReportsViewModel> logger)
        {
            _logger = logger;
            LoadReportsCommand = new AsyncRelayCommand(LoadReportsAsync);
        }

        public IAsyncRelayCommand LoadReportsCommand { get; }

        private async Task LoadReportsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading reports");
                Reports.Clear();
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