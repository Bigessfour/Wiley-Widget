using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.ViewModels
{
    public partial class BudgetOverviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ObservableCollection<BudgetItem> _budgetItems = new();

        [ObservableProperty]
        private decimal _totalBudget;

        [ObservableProperty]
        private decimal _totalActual;

        [ObservableProperty]
        private decimal _totalVariance;

        [ObservableProperty]
        private double _budgetHealthPercentage;

        public IAsyncRelayCommand RefreshDataAsyncCommand => RefreshDataAsyncCommandPrivate ??= new AsyncRelayCommand(RefreshDataAsync);
        private IAsyncRelayCommand? RefreshDataAsyncCommandPrivate;

        public BudgetOverviewViewModel()
        {
            // Initialize with sample data
            _ = LoadBudgetDataAsync();
        }

        [RelayCommand]
        private async Task LoadBudgetDataAsync()
        {
            IsLoading = true;

            // Simulate loading data
            await Task.Delay(1000);

            BudgetItems.Clear();
            BudgetItems.Add(new BudgetItem
            {
                Department = "Administration",
                BudgetAmount = 50000,
                ActualAmount = 45000,
                Variance = -5000,
                VariancePercentage = -10.0
            });
            BudgetItems.Add(new BudgetItem
            {
                Department = "Operations",
                BudgetAmount = 150000,
                ActualAmount = 165000,
                Variance = 15000,
                VariancePercentage = 10.0
            });
            BudgetItems.Add(new BudgetItem
            {
                Department = "Marketing",
                BudgetAmount = 75000,
                ActualAmount = 72000,
                Variance = -3000,
                VariancePercentage = -4.0
            });
            BudgetItems.Add(new BudgetItem
            {
                Department = "IT",
                BudgetAmount = 100000,
                ActualAmount = 95000,
                Variance = -5000,
                VariancePercentage = -5.0
            });

            // Calculate totals
            TotalBudget = BudgetItems.Sum(x => x.BudgetAmount);
            TotalActual = BudgetItems.Sum(x => x.ActualAmount);
            TotalVariance = TotalActual - TotalBudget;

            // Calculate health percentage (inverse of variance percentage)
            var avgVariancePercent = BudgetItems.Average(x => x.VariancePercentage);
            BudgetHealthPercentage = Math.Max(0, Math.Min(100, 100 - Math.Abs(avgVariancePercent)));

            IsLoading = false;
        }

        [RelayCommand]
        public async Task RefreshDataAsync()
        {
            await LoadBudgetDataAsync();
        }
    }

    public class BudgetItem
    {
        public string Department { get; set; } = string.Empty;
        public decimal BudgetAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance { get; set; }
        public double VariancePercentage { get; set; }
    }
}
