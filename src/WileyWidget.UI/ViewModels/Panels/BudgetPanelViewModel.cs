using System.Collections.ObjectModel;
using System.ComponentModel;
using Prism.Mvvm;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.ViewModels.Panels {
    /// <summary>
    /// ViewModel for the Budget Panel View
    /// </summary>
    public class BudgetPanelViewModel : BindableBase, IDataErrorInfo
    {
        private readonly IBudgetRepository _budgetRepository;

        /// <summary>
        /// Collection of budgets
        /// </summary>
        public ObservableCollection<OverallBudget> Budgets { get; } = new();

        /// <summary>
        /// Selected budget
        /// </summary>
        private OverallBudget? _selectedBudget;
        public OverallBudget? SelectedBudget
        {
            get => _selectedBudget;
            set => SetProperty(ref _selectedBudget, value);
        }

        /// <summary>
        /// Collection of budget periods
        /// </summary>
        public ObservableCollection<BudgetPeriod> BudgetPeriods { get; } = new();

        /// <summary>
        /// Collection of budget entries
        /// </summary>
        public ObservableCollection<BudgetEntry> BudgetEntries { get; } = new();

        /// <summary>
        /// Budget analysis data
        /// </summary>
        private BudgetAnalysisResult? _analysisData;
        public BudgetAnalysisResult? AnalysisData
        {
            get => _analysisData;
            set => SetProperty(ref _analysisData, value);
        }

    /// <summary>
    /// Trend data for budget analysis
    /// </summary>
    public ObservableCollection<WileyWidget.Models.BudgetTrendItem> TrendData { get; } = new();

    /// <summary>
    /// Rate trend data for charts
    /// </summary>
    public ObservableCollection<RateTrendData> RateTrendData { get; } = new();

    /// <summary>
    /// Projected rate data for charts
    /// </summary>
    public ObservableCollection<ProjectedRateData> ProjectedRateData { get; } = new();

    /// <summary>
    /// Budget performance data for charts
    /// </summary>
    public ObservableCollection<BudgetPerformanceData> BudgetPerformanceData { get; } = new();

    /// <summary>
    /// Loading state
    /// </summary>
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Total revenue
        /// </summary>
        private decimal _totalRevenue;
        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => SetProperty(ref _totalRevenue, value);
        }

        /// <summary>
        /// Total expenses
        /// </summary>
        private decimal _totalExpenses;
        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set => SetProperty(ref _totalExpenses, value);
        }

        /// <summary>
        /// Net income
        /// </summary>
        private decimal _netIncome;
        public decimal NetIncome
        {
            get => _netIncome;
            set => SetProperty(ref _netIncome, value);
        }

        /// <summary>
        /// Budget variance
        /// </summary>
        private decimal _budgetVariance;
        public decimal BudgetVariance
        {
            get => _budgetVariance;
            set => SetProperty(ref _budgetVariance, value);
        }

        public BudgetPanelViewModel(IBudgetRepository budgetRepository)
        {
            _budgetRepository = budgetRepository;
            LoadDataAsync();
        }

        /// <summary>
        /// Loads budget data asynchronously
        /// </summary>
        private async void LoadDataAsync()
        {
            try
            {
                IsLoading = true;

                // Load budget entries for current fiscal year
                var currentYear = DateTime.Now.Year;
                var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(currentYear);

                // Populate collections
                BudgetEntries.Clear();
                foreach (var entry in budgetEntries)
                {
                    BudgetEntries.Add(entry);
                }

                // Calculate totals
                TotalRevenue = budgetEntries.Where(b => b.BudgetedAmount > 0).Sum(b => b.BudgetedAmount);
                TotalExpenses = budgetEntries.Where(b => b.ActualAmount > 0).Sum(b => b.ActualAmount);
                NetIncome = TotalRevenue - TotalExpenses;
                BudgetVariance = budgetEntries.Sum(b => b.Variance);

                // Generate trend data
                TrendData.Clear();
                var trendItem = new WileyWidget.Models.BudgetTrendItem
                {
                    Period = $"FY {currentYear}",
                    Amount = TotalRevenue,
                    ProjectedAmount = TotalExpenses,
                    Category = "Revenue vs Expenses"
                };
                TrendData.Add(trendItem);
            }
            catch (Exception ex)
            {
                // Handle error - could add error message property
                System.Diagnostics.Debug.WriteLine($"Error loading budget data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // IDataErrorInfo implementation
        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                return columnName switch
                {
                    nameof(TotalRevenue) when TotalRevenue < 0 => "Total revenue cannot be negative",
                    nameof(TotalExpenses) when TotalExpenses < 0 => "Total expenses cannot be negative",
                    _ => string.Empty
                };
            }
        }
    }
}
