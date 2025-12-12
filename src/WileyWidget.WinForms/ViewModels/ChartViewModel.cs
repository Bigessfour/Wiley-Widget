using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class ChartViewModel : ObservableObject
    {
        public ChartViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(LoadChartDataAsync);
        }

        public ObservableCollection<MonthlyRevenue> MonthlyRevenueData { get; } = new();
        public ObservableCollection<(string Category, decimal Value)> PieChartData { get; } = new();
        public ObservableCollection<KeyValuePair<string, decimal>> ChartData { get; } = new();

        [ObservableProperty]
        private string? errorMessage;

        public IAsyncRelayCommand RefreshCommand { get; }

        public async Task LoadChartDataAsync()
        {
            ErrorMessage = null;
            ChartData.Clear();
            MonthlyRevenueData.Clear();
            PieChartData.Clear();

            await Task.Delay(100);

            var random = new Random();
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul" };
            for (int i = 0; i < monthNames.Length; i++)
            {
                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = monthNames[i],
                    MonthNumber = i + 1,
                    Amount = (decimal)(random.NextDouble() * 1000 + 1000)
                });
            }

            // Sample data for charts
            PieChartData.Add(("Category 1", 2m));
            PieChartData.Add(("Category 2", 4m));
            PieChartData.Add(("Category 3", 1m));

            ChartData.Add(new KeyValuePair<string, decimal>("Admin", 120_000m));
            ChartData.Add(new KeyValuePair<string, decimal>("Public Works", 95_500m));
            ChartData.Add(new KeyValuePair<string, decimal>("Public Safety", 150_250m));
            ChartData.Add(new KeyValuePair<string, decimal>("Parks", 62_700m));
        }
    }
}
