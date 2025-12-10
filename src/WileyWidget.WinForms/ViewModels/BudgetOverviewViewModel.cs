using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Models;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// ViewModel for budget overview with full CRUD operations.
    /// Delegates business logic to IBudgetCategoryService.
    /// </summary>
    public partial class BudgetOverviewViewModel : ObservableObject
    {
        private readonly ILogger<BudgetOverviewViewModel> _logger;
        private readonly IBudgetCategoryService _budgetCategoryService;

        [ObservableProperty]
        private ObservableCollection<BudgetCategoryDto> categories = new();

        [ObservableProperty]
        private BudgetCategoryDto? selectedCategory;

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal totalEncumbrance;

        [ObservableProperty]
        private decimal variance;

        [ObservableProperty]
        private int fiscalYear = DateTime.Now.Year;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string? errorMessage;

        /// <summary>Gets the command to load budget overview data.</summary>
        public IAsyncRelayCommand LoadDataCommand { get; }

        /// <summary>Gets the command to add a budget category.</summary>
        public IAsyncRelayCommand<BudgetCategoryDto> AddCategoryCommand { get; }

        /// <summary>Gets the command to update a budget category.</summary>
        public IAsyncRelayCommand<BudgetCategoryDto> UpdateCategoryCommand { get; }

        /// <summary>Gets the command to delete a budget category.</summary>
        public IAsyncRelayCommand<int> DeleteCategoryCommand { get; }

        /// <summary>Gets the command to refresh budget data.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        public BudgetOverviewViewModel(
            ILogger<BudgetOverviewViewModel> logger,
            IBudgetCategoryService budgetCategoryService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _budgetCategoryService = budgetCategoryService ?? throw new ArgumentNullException(nameof(budgetCategoryService));

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            AddCategoryCommand = new AsyncRelayCommand<BudgetCategoryDto>(AddCategoryAsync!);
            UpdateCategoryCommand = new AsyncRelayCommand<BudgetCategoryDto>(UpdateCategoryAsync!);
            DeleteCategoryCommand = new AsyncRelayCommand<int>(DeleteCategoryAsync);
            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

            _logger.LogInformation("BudgetOverviewViewModel constructed with IBudgetCategoryService");
        }

        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                _logger.LogInformation("Loading budget overview data for fiscal year {FiscalYear}", FiscalYear);

                // Load categories
                var categoryList = await _budgetCategoryService.GetAllCategoriesAsync(FiscalYear, cancellationToken);

                Categories.Clear();
                foreach (var category in categoryList)
                {
                    Categories.Add(category);
                }

                // Load totals
                var (budget, actual, encumbrance) = await _budgetCategoryService.GetTotalsAsync(FiscalYear, cancellationToken);
                TotalBudget = budget;
                TotalActual = actual;
                TotalEncumbrance = encumbrance;
                Variance = TotalBudget - TotalActual - TotalEncumbrance;

                _logger.LogInformation("Budget overview loaded: {CategoryCount} categories, Budget={Budget:C}, Actual={Actual:C}, Variance={Variance:C}",
                    Categories.Count, TotalBudget, TotalActual, Variance);
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogDebug(oce, "Budget overview loading canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load budget overview");
                ErrorMessage = $"Failed to load budget overview: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                category.FiscalYear = FiscalYear;
                var created = await _budgetCategoryService.CreateCategoryAsync(category, cancellationToken);

                Categories.Add(created);
                await RefreshTotalsAsync(cancellationToken);

                _logger.LogInformation("Added budget category: {Category}", created.Category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add budget category");
                ErrorMessage = $"Failed to add category: {ex.Message}";
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UpdateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var updated = await _budgetCategoryService.UpdateCategoryAsync(category, cancellationToken);

                // Update in collection (match by Id rather than reference equality)
                var existing = Categories.FirstOrDefault(c => c.Id == category.Id);
                if (existing != null)
                {
                    var index = Categories.IndexOf(existing);
                    if (index >= 0)
                    {
                        Categories[index] = updated;
                    }
                }

                await RefreshTotalsAsync(cancellationToken);

                _logger.LogInformation("Updated budget category: {Category}", updated.Category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update budget category");
                ErrorMessage = $"Failed to update category: {ex.Message}";
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var success = await _budgetCategoryService.DeleteCategoryAsync(id, cancellationToken);

                if (success)
                {
                    var category = Categories.FirstOrDefault(c => c.Id == id);
                    if (category != null)
                    {
                        Categories.Remove(category);
                    }

                    await RefreshTotalsAsync(cancellationToken);

                    _logger.LogInformation("Deleted budget category: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete budget category");
                ErrorMessage = $"Failed to delete category: {ex.Message}";
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshTotalsAsync(CancellationToken cancellationToken = default)
        {
            var (budget, actual, encumbrance) = await _budgetCategoryService.GetTotalsAsync(FiscalYear, cancellationToken);
            TotalBudget = budget;
            TotalActual = actual;
            TotalEncumbrance = encumbrance;
            Variance = TotalBudget - TotalActual - TotalEncumbrance;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await LoadDataAsync(cancellationToken);
        }
    }
}
