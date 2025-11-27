using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace WileyWidget.WinForms.ViewModels
{
    public class ChartDataPoint
    {
        public string Month { get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class PieDataPoint
    {
        public string Category { get; set; } = string.Empty;
        // Use Amount for consistency with ChartDataPoint and the rest of the codebase
        public double Amount { get; set; }
    }

    public partial class ChartViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ChartDataPoint> monthlyPoints = new();

        [ObservableProperty]
        private ObservableCollection<PieDataPoint> piePoints = new();

        public async Task LoadChartDataAsync()
        {
            // Load your chart data here
            await Task.Delay(100); // Simulate loading

            MonthlyPoints = new ObservableCollection<ChartDataPoint>
            {
                new() { Month = "Jan", Amount = 2 },
                new() { Month = "Feb", Amount = 1 },
                new() { Month = "Mar", Amount = 3 },
                new() { Month = "Apr", Amount = 5 },
                new() { Month = "May", Amount = 3 },
                new() { Month = "Jun", Amount = 4 },
                new() { Month = "Jul", Amount = 6 }
            };

            PiePoints = new ObservableCollection<PieDataPoint>
            {
                new() { Category = "Category 1", Amount = 2 },
                new() { Category = "Category 2", Amount = 4 },
                new() { Category = "Category 3", Amount = 1 }
            };
        }
    }
}
