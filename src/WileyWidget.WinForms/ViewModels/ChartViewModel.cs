using System.Collections.ObjectModel;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels
{
    public class ChartViewModel
    {
        public ObservableCollection<MonthlyRevenue> MonthlyRevenueData { get; set; } = new();
        public ObservableCollection<(string Category, decimal Value)> PieChartData { get; set; } = new();

        public async Task LoadChartDataAsync()
        {
            // Populate MonthlyRevenueData and pie chart data for the view
            await Task.Delay(100);

            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul" };
            for (int i = 0; i < monthNames.Length; i++)
            {
                MonthlyRevenueData.Add(new MonthlyRevenue
                {
                    Month = monthNames[i],
                    MonthNumber = i + 1,
                    Amount = (decimal)(new Random().NextDouble() * 1000 + 1000)
                });
            }

            PieChartData.Add(("Category 1", 2m));
            PieChartData.Add(("Category 2", 4m));
            PieChartData.Add(("Category 3", 1m));
        }
    }
}
