using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace WileyWidget.ViewModels
{
    public class DashboardMetric
    {
        public string Name { get; set; }
        public double Value { get; set; }
    }

    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<DashboardMetric> metrics;

        public DashboardViewModel()
        {
            Metrics = new ObservableCollection<DashboardMetric>
            {
                new DashboardMetric { Name = "Total Sales", Value = 150000.50 },
                new DashboardMetric { Name = "Growth Rate", Value = 12.34 },
                new DashboardMetric { Name = "Customer Count", Value = 1234 }
            };
        }
    }
}