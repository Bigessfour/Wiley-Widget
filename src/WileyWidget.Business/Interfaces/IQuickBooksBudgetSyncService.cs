using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Business.Interfaces
{
    /// <summary>
    /// Orchestrates QuickBooks -> Budget actuals synchronization for a fiscal year.
    /// </summary>
    public interface IQuickBooksBudgetSyncService
    {
        /// <summary>
        /// Aggregates QuickBooks journal entries for the fiscal year-to-date and updates BudgetEntry.ActualAmount accordingly.
        /// Returns the number of budget rows updated.
        /// </summary>
        Task<int> SyncFiscalYearActualsAsync(int fiscalYear, CancellationToken cancellationToken = default);
    }
}
