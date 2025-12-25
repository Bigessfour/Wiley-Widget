using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces
{
    public interface ITransactionRepository
    {
        Task<IEnumerable<Transaction>> GetTransactionsForBudgetEntryAsync(int budgetEntryId);
        Task AddAsync(Transaction transaction);
        Task<Transaction?> GetByIdAsync(int id);
        Task UpdateAsync(Transaction transaction);
        Task DeleteAsync(int id);
        Task<IEnumerable<Transaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<Transaction?> GetByIdWithIncludesAsync(int id);
        Task<IEnumerable<Transaction>> GetAllAsync();
        Task BulkInsertAsync(IEnumerable<Transaction> transactions);
    }
}
