using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;

namespace WileyWidget.ViewModels
{
    public readonly record struct BudgetMetric(
        string Name,
        decimal Value,
        string DepartmentName = "",
        decimal BudgetedAmount = 0m,
        decimal Amount = 0m,
        decimal Variance = 0m,
        decimal VariancePercent = 0m,
        bool IsOverBudget = false);

    public partial class BudgetOverviewViewModel : ObservableObject
    {
        private readonly ILogger<BudgetOverviewViewModel> _logger;
        private readonly IBudgetCategoryService _budgetCategoryService;

        public ObservableCollection<BudgetCategoryDto> Categories { get; } = new();

        public ObservableCollection<int> AvailableFiscalYears { get; } = new();

        public ObservableCollection<BudgetMetric> Metrics { get; } = new();

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand<BudgetCategoryDto> AddCategoryCommand { get; }
        public IAsyncRelayCommand<BudgetCategoryDto> UpdateCategoryCommand { get; }
        public IAsyncRelayCommand<int> DeleteCategoryCommand { get; }

        [ObservableProperty]
        private int fiscalYear = DateTime.Now.Year;

        [ObservableProperty]
        private BudgetCategoryDto? selectedCategory;

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal totalEncumbrance;

        [ObservableProperty]
        private decimal totalVariance;

        [ObservableProperty]
        private decimal overallVariancePercent;

        [ObservableProperty]
        private int overBudgetCount;

        [ObservableProperty]
        private int underBudgetCount;

        [ObservableProperty]
        private DateTime lastUpdated = DateTime.Now;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        public decimal Variance => TotalBudget - TotalActual - TotalEncumbrance;

        public decimal TotalBudgeted => TotalBudget;

        public int SelectedFiscalYear
        {
            get => FiscalYear;
            set => FiscalYear = value;
        }

        public BudgetOverviewViewModel()
            : this(NullLogger<BudgetOverviewViewModel>.Instance, new FallbackBudgetCategoryService(), null)
        {
        }

        public BudgetOverviewViewModel(ILogger<BudgetOverviewViewModel> logger, IBudgetCategoryService budgetCategoryService, UIConfiguration? uiConfig = null)
        {
            _logger = logger ?? NullLogger<BudgetOverviewViewModel>.Instance;
            _budgetCategoryService = budgetCategoryService ?? new FallbackBudgetCategoryService();

            // Initialize fiscal year options and set default from UI configuration when present
            InitializeFiscalYearOptions();
            FiscalYear = uiConfig?.DefaultFiscalYear ?? FiscalYear;

            LoadDataCommand = new AsyncRelayCommand(async parameter =>
                await LoadDataAsync(parameter is CancellationToken ct ? ct : CancellationToken.None));

            RefreshCommand = new AsyncRelayCommand(async parameter =>
                await LoadDataAsync(parameter is CancellationToken ct ? ct : CancellationToken.None));

            AddCategoryCommand = new AsyncRelayCommand<BudgetCategoryDto>(category =>
                AddCategoryAsync(category, CancellationToken.None));

            UpdateCategoryCommand = new AsyncRelayCommand<BudgetCategoryDto>(category =>
                UpdateCategoryAsync(category, CancellationToken.None));

            DeleteCategoryCommand = new AsyncRelayCommand<int>(id =>
                DeleteCategoryAsync(id, CancellationToken.None));
        }

        public BudgetOverviewViewModel(IBudgetCategoryService budgetCategoryService)
            : this(NullLogger<BudgetOverviewViewModel>.Instance, budgetCategoryService, null)
        {
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => LoadDataAsync(cancellationToken);

        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsLoading = true;
                ErrorMessage = null;
                Categories.Clear();
                Metrics.Clear();

                var categories = await _budgetCategoryService.GetAllCategoriesAsync(FiscalYear, cancellationToken);
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }

                UpdateFiscalYearOptions(categories);
                await RefreshTotalsAsync(cancellationToken);
                UpdateMetricsFromCategories();
                LastUpdated = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load budget overview for fiscal year {FiscalYear}", FiscalYear);
                ErrorMessage = $"Failed to load budget overview: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshTotalsAsync(CancellationToken cancellationToken)
        {
            var (totalBudget, totalActual, totalEncumbrance) = await _budgetCategoryService.GetTotalsAsync(FiscalYear, cancellationToken);
            TotalBudget = totalBudget;
            TotalActual = totalActual;
            TotalEncumbrance = totalEncumbrance;
            TotalVariance = Variance;
            OverallVariancePercent = TotalBudget == 0 ? 0 : (Variance / TotalBudget) * 100m;
        }

        private async Task AddCategoryAsync(BudgetCategoryDto? newCategory, CancellationToken cancellationToken)
        {
            if (newCategory is null)
            {
                throw new ArgumentNullException(nameof(newCategory));
            }

            try
            {
                newCategory.FiscalYear = newCategory.FiscalYear == 0 ? FiscalYear : newCategory.FiscalYear;
                var created = await _budgetCategoryService.CreateCategoryAsync(newCategory, cancellationToken);
                Categories.Add(created);
                await RefreshTotalsAsync(cancellationToken);
                UpdateMetricsFromCategories();
                LastUpdated = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = null;
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to add category: {ex.Message}";
                _logger.LogError(ex, "Failed to add category");
                throw;
            }
        }

        private async Task UpdateCategoryAsync(BudgetCategoryDto? category, CancellationToken cancellationToken)
        {
            if (category is null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            try
            {
                var updated = await _budgetCategoryService.UpdateCategoryAsync(category, cancellationToken);
                var existingIndex = Categories.ToList().FindIndex(c => c.Id == updated.Id);
                if (existingIndex >= 0)
                {
                    Categories[existingIndex] = updated;
                }
                else
                {
                    Categories.Add(updated);
                }

                await RefreshTotalsAsync(cancellationToken);
                UpdateMetricsFromCategories();
                LastUpdated = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = null;
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to update category: {ex.Message}";
                _logger.LogError(ex, "Failed to update category {CategoryId}", category.Id);
                throw;
            }
        }

        private async Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _budgetCategoryService.DeleteCategoryAsync(categoryId, cancellationToken);
                if (deleted)
                {
                    var existing = Categories.FirstOrDefault(c => c.Id == categoryId);
                    if (existing != null)
                    {
                        Categories.Remove(existing);
                    }
                }

                await RefreshTotalsAsync(cancellationToken);
                UpdateMetricsFromCategories();
                LastUpdated = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = null;
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete category: {ex.Message}";
                _logger.LogError(ex, "Failed to delete category {CategoryId}", categoryId);
                throw;
            }
        }

        partial void OnTotalBudgetChanged(decimal value) => OnPropertyChanged(nameof(Variance));

        partial void OnTotalActualChanged(decimal value) => OnPropertyChanged(nameof(Variance));

        partial void OnTotalEncumbranceChanged(decimal value) => OnPropertyChanged(nameof(Variance));

        private void InitializeFiscalYearOptions()
        {
            if (AvailableFiscalYears.Count == 0)
            {
                var currentYear = DateTime.Now.Year;
                AvailableFiscalYears.Add(currentYear - 1);
                AvailableFiscalYears.Add(currentYear);
                AvailableFiscalYears.Add(currentYear + 1);
            }
        }

        private void UpdateFiscalYearOptions(IEnumerable<BudgetCategoryDto> categories)
        {
            foreach (var year in categories.Select(c => c.FiscalYear).Where(y => y > 0).Distinct().OrderBy(y => y))
            {
                if (!AvailableFiscalYears.Contains(year))
                {
                    AvailableFiscalYears.Add(year);
                }
            }
        }

        private void UpdateMetricsFromCategories()
        {
            OverBudgetCount = Categories.Count(c => c.ActualAmount + c.EncumbranceAmount > c.BudgetedAmount);
            UnderBudgetCount = Categories.Count(c => c.ActualAmount + c.EncumbranceAmount <= c.BudgetedAmount);

            Metrics.Clear();
            Metrics.Add(new BudgetMetric("Total Budget", TotalBudget));
            Metrics.Add(new BudgetMetric("Total Actual", TotalActual));
            Metrics.Add(new BudgetMetric("Encumbrance", TotalEncumbrance));
            Metrics.Add(new BudgetMetric("Variance", Variance));
        }
    }
}
