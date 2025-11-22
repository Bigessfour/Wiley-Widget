using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class EnterpriseViewModel : ObservableRecipient
    {
        private readonly ILogger<EnterpriseViewModel> _logger;

        [ObservableProperty]
        private string title = "Enterprise Management";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<EnterpriseItem> enterpriseItems = new();

        public EnterpriseViewModel(ILogger<EnterpriseViewModel> logger)
        {
            _logger = logger;
            LoadEnterpriseDataCommand = new AsyncRelayCommand(LoadEnterpriseDataAsync);
        }

        public IAsyncRelayCommand LoadEnterpriseDataCommand { get; }

        private async Task LoadEnterpriseDataAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading enterprise data");

                // Placeholder for enterprise data loading
                EnterpriseItems.Clear();

                _logger.LogInformation("Enterprise data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load enterprise data");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class EnterpriseItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }
}