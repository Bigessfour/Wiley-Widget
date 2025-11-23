using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace WileyWidget.ViewModels
{
    public class FinancialMetric
    {
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }

    public partial class BudgetOverviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<FinancialMetric> metrics;

        public BudgetOverviewViewModel()
        {
            Metrics = new ObservableCollection<FinancialMetric>
            {
                new FinancialMetric { Category = "Revenue", Amount = 100000 },
                new FinancialMetric { Category = "Expenses", Amount = 75000 },
                new FinancialMetric { Category = "Profit", Amount = 25000 }
            };
        }
    }
}