using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Represents a interface for ibudgetcategoryservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ibudgetcategoryservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ibudgetcategoryservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ibudgetcategoryservice.
    /// </summary>
    /// <summary>
    /// Represents a interface for ibudgetcategoryservice.
    /// </summary>
    public interface IBudgetCategoryService
    {
        Task<List<BudgetCategoryDto>> GetAllCategoriesAsync(int fiscalYear, CancellationToken cancellationToken = default);

        Task<BudgetCategoryDto?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<BudgetCategoryDto> CreateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default);

        Task<BudgetCategoryDto> UpdateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default);

        Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);

        Task<(decimal TotalBudget, decimal TotalActual, decimal TotalEncumbrance)> GetTotalsAsync(int fiscalYear, CancellationToken cancellationToken = default);
    }
}
