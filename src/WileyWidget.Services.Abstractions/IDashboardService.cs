using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service interface for dashboard operations
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Gets dashboard metrics
        /// </summary>
        Task<IEnumerable<DashboardMetric>> GetDashboardDataAsync();

        /// <summary>
        /// Gets dashboard summary with all data
        /// </summary>
        Task<DashboardSummary> GetDashboardSummaryAsync();

        /// <summary>
        /// Refreshes dashboard data
        /// </summary>
        Task RefreshDashboardAsync();
    }
}