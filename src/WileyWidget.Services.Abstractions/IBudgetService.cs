using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    public interface IBudgetService
    {
        Task<BudgetData> CreateBudgetAsync(BudgetData budgetData);
        Task<BudgetData?> GetBudgetDataAsync(int enterpriseId, int fiscalYear);
        Task UpdateBudgetAsync(BudgetData budgetData);
    }
}
