using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace WileyWidget.WinUI.ViewModels
{
    /// <summary>
    /// ViewModel for DataView - handles data grid display with filtering and async loading.
    /// Pure WinUI 3 implementation using CommunityToolkit.Mvvm.
    /// </summary>
    public partial class DataViewModel : ObservableObject
    {
        private readonly ILogger<DataViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<DataItemModel> _dataItems = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        public DataViewModel(ILogger<DataViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("DataViewModel initialized");
        }

        partial void OnSearchQueryChanged(string value)
        {
            _logger.LogDebug("Search query changed: {Query}", value);
            FilterData(value);
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading data");

                // Simulate async data loading
                await Task.Delay(500);

                // TODO: Replace with actual data service call
                var sampleData = GenerateSampleData();
                DataItems = new ObservableCollection<DataItemModel>(sampleData);

                _logger.LogInformation("Loaded {Count} data items", DataItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            _logger.LogInformation("Refreshing data");
            await LoadDataAsync();
        }

        private void FilterData(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            // TODO: Implement actual filtering logic
            _logger.LogDebug("Filtering data with query: {Query}", query);
        }

        private DataItemModel[] GenerateSampleData()
        {
            var random = new Random();
            var categories = new[] { "Revenue", "Expenses", "Assets", "Liabilities" };

            return Enumerable.Range(1, 50)
                .Select(i => new DataItemModel
                {
                    Id = i,
                    Name = $"Item {i}",
                    Category = categories[random.Next(categories.Length)],
                    Amount = random.Next(100, 10000) + random.NextDouble(),
                    Date = DateTime.Now.AddDays(-random.Next(0, 365))
                })
                .ToArray();
        }
    }

    /// <summary>
    /// Data model for DataView grid items.
    /// </summary>
    public class DataItemModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Amount { get; set; }
        public DateTime Date { get; set; }
    }
}
