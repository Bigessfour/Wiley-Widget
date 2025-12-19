using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Fallback implementation of IBudgetCategoryService for use when DI container is not available.
    /// Returns empty collections and does not perform actual operations.
    /// </summary>
    internal class FallbackBudgetCategoryService : IBudgetCategoryService
    {
        public Task<List<BudgetCategoryDto>> GetAllCategoriesAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<BudgetCategoryDto>());
        }

        public Task<BudgetCategoryDto?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BudgetCategoryDto?>(null);
        }

        public Task<BudgetCategoryDto> CreateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(category);
        }

        public Task<BudgetCategoryDto> UpdateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(category);
        }

        public Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<(decimal TotalBudget, decimal TotalActual, decimal TotalEncumbrance)> GetTotalsAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((0m, 0m, 0m));
        }
    }
}
