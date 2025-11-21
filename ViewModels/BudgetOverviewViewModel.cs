using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.ViewModels
{
    public partial class BudgetOverviewViewModel : ObservableObject
    {
        private readonly IBudgetRepository? _budgetRepository;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private ObservableCollection<BudgetItem> _budgetItems = new();
        public ObservableCollection<BudgetItem> BudgetItems
        {
            get => _budgetItems;
            set => SetProperty(ref _budgetItems, value);
        }

        private decimal _totalBudget;
        public decimal TotalBudget
        {
            get => _totalBudget;
            set => SetProperty(ref _totalBudget, value);
        }

        private decimal _totalActual;
        public decimal TotalActual
        {
            get => _totalActual;
            set => SetProperty(ref _totalActual, value);
        }

        private decimal _totalVariance;
        public decimal TotalVariance
        {
            get => _totalVariance;
            set => SetProperty(ref _totalVariance, value);
        }

        private double _budgetHealthPercentage;
        public double BudgetHealthPercentage
        {
            get => _budgetHealthPercentage;
            set => SetProperty(ref _budgetHealthPercentage, value);
        }

        private ObservableCollection<MonthlyPoint> _monthlyTrend = new();
        public ObservableCollection<MonthlyPoint> MonthlyTrend
        {
            get => _monthlyTrend;
            set => SetProperty(ref _monthlyTrend, value);
        }

        private int _healthScore;
        public int HealthScore
        {
            get => _healthScore;
            set => SetProperty(ref _healthScore, value);
        }

        public IAsyncRelayCommand LoadBudgetDataAsyncCommand => LoadBudgetDataAsyncCommandPrivate ??= new AsyncRelayCommand(LoadBudgetDataAsync);
        private IAsyncRelayCommand? LoadBudgetDataAsyncCommandPrivate;

        public BudgetOverviewViewModel()
        {
            // Attempt to resolve repository from global App services. If registered, use real backend data.
            _budgetRepository = App.Services?.GetService(typeof(IBudgetRepository)) as IBudgetRepository;

            // Trigger initial load
            _ = LoadBudgetDataAsyncCommand?.ExecuteAsync(null);
        }

        private async Task LoadBudgetDataAsync()
        {
            IsLoading = true;

            try
            {
                var year = DateTime.Now.Year;

                if (_budgetRepository is not null)
                {
                    var entries = (await _budgetRepository.GetByFiscalYearAsync(year))?.ToList() ?? new();

                    BudgetItems.Clear();

                    foreach (var be in entries)
                    {
                        BudgetItems.Add(new BudgetItem
                        {
                            Department = be.Department?.Name ?? "(Unknown)",
                            BudgetAmount = be.BudgetedAmount,
                            ActualAmount = be.ActualAmount,
                            Variance = be.ActualAmount - be.BudgetedAmount,
                            VariancePercentage = be.BudgetedAmount != 0 ? (double)((be.ActualAmount - be.BudgetedAmount) / be.BudgetedAmount * 100.0m) : 0.0
                        });
                    }

                    // Build monthly trend by distributing annual values evenly across months
                    BuildMonthlyTrendFromEntries(entries, year);
                }
                else
                {
                    // Fallback sample data for environments without backend configured
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

                    // Build simple monthly trend from sample data
                    var sampleEntries = BudgetItems.Select(b => new BudgetEntry
                    {
                        BudgetedAmount = b.BudgetAmount,
                        ActualAmount = b.ActualAmount,
                        StartPeriod = new DateTime(year, 1, 1),
                        EndPeriod = new DateTime(year, 12, 31)
                    }).ToList();

                    BuildMonthlyTrendFromEntries(sampleEntries, year);
                }

                // Calculate totals
                TotalBudget = BudgetItems.Sum(x => x.BudgetAmount);
                TotalActual = BudgetItems.Sum(x => x.ActualAmount);
                TotalVariance = TotalActual - TotalBudget;

                // Calculate health percentage (inverse of average absolute variance percent)
                var avgVariancePercent = BudgetItems.Any() ? BudgetItems.Average(x => Math.Abs(x.VariancePercentage)) : 0.0;
                BudgetHealthPercentage = Math.Max(0, Math.Min(100, 100 - avgVariancePercent));
                HealthScore = (int)Math.Round(BudgetHealthPercentage);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BuildMonthlyTrendFromEntries(System.Collections.Generic.IEnumerable<BudgetEntry> entries, int year)
        {
            MonthlyTrend.Clear();

            // Initialize months
            var months = Enumerable.Range(1, 12)
                .Select(m => new MonthlyPoint { MonthLabel = new DateTime(year, m, 1).ToString("MMM", CultureInfo.CurrentCulture), Budget = 0.0, Actual = 0.0 })
                .ToList();

            foreach (var be in entries)
            {
                // Distribute annual amounts evenly across months for simple trend
                var monthlyBudget = be.BudgetedAmount / 12.0m;
                var monthlyActual = be.ActualAmount / 12.0m;

                for (int i = 0; i < 12; i++)
                {
                    months[i].Budget += (double)monthlyBudget;
                    months[i].Actual += (double)monthlyActual;
                }
            }

            foreach (var m in months)
            {
                MonthlyTrend.Add(m);
            }
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

    public class MonthlyPoint
    {
        public string MonthLabel { get; set; } = string.Empty;
        public double Budget { get; set; }
        public double Actual { get; set; }
    }
}
