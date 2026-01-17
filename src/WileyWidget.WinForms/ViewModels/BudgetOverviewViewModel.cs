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
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// Budget metric for display in grids and charts showing department-level budget vs actual variance.
    /// </summary>
    public readonly record struct BudgetMetric(
        string Name,
        decimal Value,
        string DepartmentName = "",
        decimal BudgetedAmount = 0m,
        decimal Amount = 0m,
        decimal Variance = 0m,
        decimal VariancePercent = 0m,
        bool IsOverBudget = false);

    /// <summary>
    /// ViewModel for Budget Overview panel displaying fiscal year budget vs actual analysis,
    /// variance tracking, and summary metrics with CSV export capabilities.
    /// </summary>
    /// <summary>
    /// ViewModel for Budget Overview panel displaying fiscal year budget vs actual analysis,
    /// variance tracking, and summary metrics with CSV export capabilities.
    /// </summary>
    public partial class BudgetOverviewViewModel : ObservableObject, ILazyLoadViewModel
    {
        private bool _isDataLoaded;
        public bool IsDataLoaded
        {
            get => _isDataLoaded;
            private set => SetProperty(ref _isDataLoaded, value);
        }

        public async Task OnVisibilityChangedAsync(bool isVisible)
        {
            if (isVisible && !IsDataLoaded && !IsLoading)
            {
                await LoadDataCommand.ExecuteAsync(null);
                IsDataLoaded = true;
            }
        }

        private readonly ILogger<BudgetOverviewViewModel> _logger;
        private readonly IBudgetCategoryService _budgetCategoryService;

        /// <summary>Gets the collection of budget categories for the selected fiscal year.</summary>
        public ObservableCollection<BudgetCategoryDto> Categories { get; } = new();

        /// <summary>Gets the collection of available fiscal years for filtering.</summary>
        public ObservableCollection<int> AvailableFiscalYears { get; } = new();

        /// <summary>Gets the collection of budget metrics for grid/chart display.</summary>
        public ObservableCollection<BudgetMetric> Metrics { get; } = new();

        /// <summary>Gets the command to load/refresh budget data for the selected fiscal year.</summary>
        public IAsyncRelayCommand LoadDataCommand { get; }

        /// <summary>Gets the command to refresh budget data.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>Gets the command to add a new budget category.</summary>
        public IAsyncRelayCommand<BudgetCategoryDto> AddCategoryCommand { get; }

        /// <summary>Gets the command to update an existing budget category.</summary>
        public IAsyncRelayCommand<BudgetCategoryDto> UpdateCategoryCommand { get; }

        /// <summary>Gets the command to delete a budget category.</summary>
        /// <summary>Gets the command to delete a budget category.</summary>
        public IAsyncRelayCommand<int> DeleteCategoryCommand { get; }

        /// <summary>Gets or sets the currently selected fiscal year for budget analysis.</summary>
        [ObservableProperty]
        private int fiscalYear = DateTime.Now.Year;

        /// <summary>Gets or sets the currently selected budget category.</summary>
        [ObservableProperty]
        private BudgetCategoryDto? selectedCategory;

        /// <summary>Gets or sets the total budgeted amount across all categories.</summary>
        [ObservableProperty]
        private decimal totalBudget;

        /// <summary>Gets or sets the total actual spent amount across all categories.</summary>
        [ObservableProperty]
        private decimal totalActual;

        /// <summary>Gets or sets the total encumbrance amount across all categories.</summary>
        [ObservableProperty]
        private decimal totalEncumbrance;

        /// <summary>Gets or sets the total variance (Budget - Actual - Encumbrance).</summary>
        [ObservableProperty]
        private decimal totalVariance;

        /// <summary>Gets or sets the overall variance percentage.</summary>
        [ObservableProperty]
        private decimal overallVariancePercent;

        /// <summary>Gets or sets the count of categories that are over budget.</summary>
        [ObservableProperty]
        private int overBudgetCount;

        /// <summary>Gets or sets the count of categories that are under budget.</summary>
        [ObservableProperty]
        private int underBudgetCount;

        /// <summary>Gets or sets the timestamp of the last data refresh.</summary>
        [ObservableProperty]
        private DateTime lastUpdated = DateTime.Now;

        /// <summary>Gets or sets a value indicating whether data is currently loading.</summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>Gets or sets the current error message, if any.</summary>
        /// <summary>Gets or sets the current error message, if any.</summary>
        [ObservableProperty]
        private string? errorMessage;

        /// <summary>Gets the computed variance amount (Budget - Actual - Encumbrance).</summary>
        public decimal Variance => TotalBudget - TotalActual - TotalEncumbrance;

        /// <summary>Gets the total budgeted amount (alias for compatibility).</summary>
        public decimal TotalBudgeted => TotalBudget;

        /// <summary>Gets or sets the selected fiscal year (alias for binding compatibility).</summary>
        public int SelectedFiscalYear
        {
            get => FiscalYear;
            set => FiscalYear = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BudgetOverviewViewModel"/> class with default (fallback) services.
        /// Used for design-time or when DI container is not available.
        /// </summary>
        public BudgetOverviewViewModel()
            : this(NullLogger<BudgetOverviewViewModel>.Instance, new FallbackBudgetCategoryService(), null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BudgetOverviewViewModel"/> class with full service injection.
        /// </summary>
        /// <param name="logger">Logger for diagnostic and error logging.</param>
        /// <param name="budgetCategoryService">Service for budget category CRUD operations.</param>
        /// <param name="uiConfig">Optional UI configuration for default fiscal year settings.</param>
        /// <summary>
        /// Initializes a new instance of the <see cref="BudgetOverviewViewModel"/> class with full service injection.
        /// </summary>
        /// <param name="logger">Logger for diagnostic and error logging.</param>
        /// <param name="budgetCategoryService">Service for budget category CRUD operations.</param>
        /// <param name="uiConfig">Optional UI configuration for default fiscal year settings.</param>
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

            _logger.LogDebug("BudgetOverviewViewModel initialized for fiscal year {FiscalYear}", FiscalYear);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BudgetOverviewViewModel"/> class with service only.
        /// </summary>
        /// <param name="budgetCategoryService">Service for budget category operations.</param>
        public BudgetOverviewViewModel(IBudgetCategoryService budgetCategoryService)
            : this(NullLogger<BudgetOverviewViewModel>.Instance, budgetCategoryService, null)
        {
        }

        /// <summary>
        /// Initializes the ViewModel and loads initial data asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>A task representing the async initialization.</returns>
        /// <summary>
        /// Initializes the ViewModel and loads initial data asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>A task representing the async initialization.</returns>
        public Task InitializeAsync(CancellationToken cancellationToken = default) => LoadDataAsync(cancellationToken);

        /// <summary>
        /// Loads budget data for the selected fiscal year from the service.
        /// Refreshes categories, metrics, and summary totals.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the async load operation.</returns>
        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsLoading = true;
                ErrorMessage = null;
                Categories.Clear();
                Metrics.Clear();

                _logger.LogInformation("Loading budget overview data for fiscal year {FiscalYear}", FiscalYear);

                var categories = await _budgetCategoryService.GetAllCategoriesAsync(FiscalYear, cancellationToken);
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }

                _logger.LogDebug("Loaded {Count} budget categories", Categories.Count);

                UpdateFiscalYearOptions(categories);
                await RefreshTotalsAsync(cancellationToken);
                UpdateMetricsFromCategories();
                LastUpdated = DateTime.Now;

                _logger.LogInformation("Budget overview loaded successfully: {CategoryCount} categories, Total Budget: {TotalBudget:C}",
                    Categories.Count, TotalBudget);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Load operation cancelled for fiscal year {FiscalYear}", FiscalYear);
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load budget overview for fiscal year {FiscalYear}", FiscalYear);
                ErrorMessage = $"Failed to load budget overview: {ex.Message}";

                // Provide sample data on failure for graceful degradation
                LoadSampleDataOnFailure();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes budget totals from the service for the selected fiscal year.
        /// Updates TotalBudget, TotalActual, TotalEncumbrance, TotalVariance, and OverallVariancePercent.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the async refresh operation.</returns>
        private async Task RefreshTotalsAsync(CancellationToken cancellationToken)
        {
            var (totalBudget, totalActual, totalEncumbrance) = await _budgetCategoryService.GetTotalsAsync(FiscalYear, cancellationToken);
            TotalBudget = totalBudget;
            TotalActual = totalActual;
            TotalEncumbrance = totalEncumbrance;
            TotalVariance = Variance;
            OverallVariancePercent = TotalBudget == 0 ? 0 : (Variance / TotalBudget) * 100m;

            _logger.LogDebug("Refreshed totals: Budget={Budget:C}, Actual={Actual:C}, Encumbrance={Encumbrance:C}, Variance={Variance:C}",
                totalBudget, totalActual, totalEncumbrance, Variance);
        }

        /// <summary>
        /// Adds a new budget category and refreshes data.
        /// </summary>
        /// <param name="newCategory">The new category to add.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the async add operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when newCategory is null.</exception>
        private async Task AddCategoryAsync(BudgetCategoryDto? newCategory, CancellationToken cancellationToken)
        {
            if (newCategory is null)
            {
                throw new ArgumentNullException(nameof(newCategory));
            }

            try
            {
                _logger.LogInformation("Adding new budget category: {Category}", newCategory.Category);

                newCategory.FiscalYear = newCategory.FiscalYear == 0 ? FiscalYear : newCategory.FiscalYear;
                var created = await _budgetCategoryService.CreateCategoryAsync(newCategory, cancellationToken);
                Categories.Add(created);
                await RefreshTotalsAsync(cancellationToken);
                UpdateMetricsFromCategories();
                LastUpdated = DateTime.Now;

                _logger.LogInformation("Added budget category {Id}: {Category}", created.Id, created.Category);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Add category operation cancelled");
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

        /// <summary>
        /// Updates an existing budget category and refreshes data.
        /// </summary>
        /// <param name="category">The category to update.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the async update operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when category is null.</exception>
        private async Task UpdateCategoryAsync(BudgetCategoryDto? category, CancellationToken cancellationToken)
        {
            if (category is null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            try
            {
                _logger.LogInformation("Updating budget category {Id}: {Category}", category.Id, category.Category);

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

                _logger.LogInformation("Updated budget category {Id} successfully", category.Id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Update category operation cancelled");
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

        /// <summary>
        /// Deletes a budget category by ID and refreshes data.
        /// </summary>
        /// <param name="categoryId">The ID of the category to delete.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the async delete operation.</returns>
        private async Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Deleting budget category {CategoryId}", categoryId);

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

                _logger.LogInformation("Deleted budget category {CategoryId} successfully", categoryId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Delete category operation cancelled");
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

        /// <summary>
        /// Partial method called when TotalBudget changes to update derived Variance property.
        /// </summary>
        partial void OnTotalBudgetChanged(decimal value) => OnPropertyChanged(nameof(Variance));

        /// <summary>
        /// Partial method called when TotalActual changes to update derived Variance property.
        /// </summary>
        partial void OnTotalActualChanged(decimal value) => OnPropertyChanged(nameof(Variance));

        /// <summary>
        /// Partial method called when TotalEncumbrance changes to update derived Variance property.
        /// </summary>
        partial void OnTotalEncumbranceChanged(decimal value) => OnPropertyChanged(nameof(Variance));

        /// <summary>
        /// Initializes the fiscal year dropdown options with current year ± 1.
        /// </summary>
        private void InitializeFiscalYearOptions()
        {
            if (AvailableFiscalYears.Count == 0)
            {
                var currentYear = DateTime.Now.Year;
                AvailableFiscalYears.Add(currentYear - 1);
                AvailableFiscalYears.Add(currentYear);
                AvailableFiscalYears.Add(currentYear + 1);

                _logger.LogDebug("Initialized fiscal year options: {Years}", string.Join(", ", AvailableFiscalYears));
            }
        }

        /// <summary>
        /// Updates the fiscal year options based on categories loaded from the service.
        /// Adds any new fiscal years found in the data.
        /// </summary>
        /// <param name="categories">The categories to extract fiscal years from.</param>
        private void UpdateFiscalYearOptions(IEnumerable<BudgetCategoryDto> categories)
        {
            var newYearsAdded = false;
            foreach (var year in categories.Select(c => c.FiscalYear).Where(y => y > 0).Distinct().OrderBy(y => y))
            {
                if (!AvailableFiscalYears.Contains(year))
                {
                    AvailableFiscalYears.Add(year);
                    newYearsAdded = true;
                }
            }

            if (newYearsAdded)
            {
                _logger.LogDebug("Updated fiscal year options: {Years}", string.Join(", ", AvailableFiscalYears.OrderBy(y => y)));
            }
        }

        /// <summary>
        /// Updates the Metrics collection and budget counts based on current Categories.
        /// Recalculates OverBudgetCount, UnderBudgetCount, and populates Metrics for display.
        /// </summary>
        private void UpdateMetricsFromCategories()
        {
            OverBudgetCount = Categories.Count(c => c.ActualAmount + c.EncumbranceAmount > c.BudgetedAmount);
            UnderBudgetCount = Categories.Count(c => c.ActualAmount + c.EncumbranceAmount <= c.BudgetedAmount);

            Metrics.Clear();

            // Add summary metrics
            Metrics.Add(new BudgetMetric("Total Budget", TotalBudget));
            Metrics.Add(new BudgetMetric("Total Actual", TotalActual));
            Metrics.Add(new BudgetMetric("Encumbrance", TotalEncumbrance));
            Metrics.Add(new BudgetMetric("Variance", Variance));

            // Add department-level metrics from categories
            foreach (var category in Categories.Take(20)) // Limit to top 20 for performance
            {
                var variance = category.BudgetedAmount - category.ActualAmount - category.EncumbranceAmount;
                var variancePercent = category.BudgetedAmount == 0 ? 0m : (variance / category.BudgetedAmount) * 100m;
                var isOverBudget = category.ActualAmount + category.EncumbranceAmount > category.BudgetedAmount;

                Metrics.Add(new BudgetMetric(
                    Name: category.Category,
                    Value: category.BudgetedAmount,
                    DepartmentName: category.DepartmentName,
                    BudgetedAmount: category.BudgetedAmount,
                    Amount: category.ActualAmount,
                    Variance: variance,
                    VariancePercent: variancePercent,
                    IsOverBudget: isOverBudget
                ));
            }

            _logger.LogDebug("Updated metrics: {MetricCount} total, {OverBudget} over budget, {UnderBudget} under budget",
                Metrics.Count, OverBudgetCount, UnderBudgetCount);
        }

        /// <summary>
        /// Loads sample/fallback data when service fails, to maintain UI functionality.
        /// </summary>
        private void LoadSampleDataOnFailure()
        {
            try
            {
                _logger.LogWarning("Loading sample budget data as fallback");

                // Create sample categories
                var sampleCategories = new[]
                {
                    new BudgetCategoryDto { Id = 1, Category = "Personnel", DepartmentName = "General", BudgetedAmount = 500000m, ActualAmount = 475000m, EncumbranceAmount = 10000m, FiscalYear = FiscalYear },
                    new BudgetCategoryDto { Id = 2, Category = "Operations", DepartmentName = "General", BudgetedAmount = 250000m, ActualAmount = 260000m, EncumbranceAmount = 5000m, FiscalYear = FiscalYear },
                    new BudgetCategoryDto { Id = 3, Category = "Utilities", DepartmentName = "Sewer", BudgetedAmount = 150000m, ActualAmount = 145000m, EncumbranceAmount = 2000m, FiscalYear = FiscalYear },
                    new BudgetCategoryDto { Id = 4, Category = "Maintenance", DepartmentName = "Sewer", BudgetedAmount = 100000m, ActualAmount = 95000m, EncumbranceAmount = 3000m, FiscalYear = FiscalYear },
                    new BudgetCategoryDto { Id = 5, Category = "Capital Projects", DepartmentName = "General", BudgetedAmount = 300000m, ActualAmount = 280000m, EncumbranceAmount = 15000m, FiscalYear = FiscalYear }
                };

                Categories.Clear();
                foreach (var category in sampleCategories)
                {
                    Categories.Add(category);
                }

                // Calculate totals from sample data
                TotalBudget = sampleCategories.Sum(c => c.BudgetedAmount);
                TotalActual = sampleCategories.Sum(c => c.ActualAmount);
                TotalEncumbrance = sampleCategories.Sum(c => c.EncumbranceAmount);
                TotalVariance = Variance;
                OverallVariancePercent = TotalBudget == 0 ? 0 : (Variance / TotalBudget) * 100m;

                UpdateMetricsFromCategories();
                LastUpdated = DateTime.Now;

                _logger.LogInformation("Loaded {Count} sample budget categories", sampleCategories.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load sample data");
            }
        }
    }
}
