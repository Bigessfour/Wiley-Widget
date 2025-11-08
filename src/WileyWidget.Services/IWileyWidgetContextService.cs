using System;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for building dynamic context in municipal finance operations.
    /// Provides methods to construct contextual information for system state, enterprises, budgets, and operations.
    /// </summary>
    public interface IWileyWidgetContextService
    {
        /// <summary>
        /// Builds the current system context asynchronously for municipal finance systems.
        /// Includes current system status, configuration, and operational parameters.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A string representing the current system context for municipal finance operations.</returns>
        Task<string> BuildCurrentSystemContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the enterprise context for a specific enterprise ID in municipal finance.
        /// Includes enterprise-specific data such as financial entities, departments, and organizational structure.
        /// </summary>
        /// <param name="enterpriseId">The ID of the enterprise within the municipal finance system.</param>
        /// <returns>A string representing the enterprise context for the specified ID.</returns>
        Task<string> GetEnterpriseContextAsync(int enterpriseId);

        /// <summary>
        /// Gets the budget context for a specified date range in municipal finance.
        /// Includes budget allocations, expenditures, and financial planning data for the given period.
        /// </summary>
        /// <param name="startDate">The start date of the budget period (optional).</param>
        /// <param name="endDate">The end date of the budget period (optional).</param>
        /// <returns>A string representing the budget context for the specified date range.</returns>
        Task<string> GetBudgetContextAsync(DateTime? startDate, DateTime? endDate);

        /// <summary>
        /// Gets the operational context asynchronously for municipal finance operations.
        /// Includes current operational status, active processes, and system performance metrics.
        /// </summary>
        /// <returns>A string representing the operational context for municipal finance systems.</returns>
        Task<string> GetOperationalContextAsync();
    }
}
