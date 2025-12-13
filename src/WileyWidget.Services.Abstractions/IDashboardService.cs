using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service interface for dashboard operations
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Gets dashboard data
        /// </summary>
        Task<IEnumerable<DashboardItem>> GetDashboardDataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets dashboard items for display
        /// </summary>
        Task<IEnumerable<DashboardItem>> GetDashboardItemsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes dashboard data
        /// </summary>
        Task RefreshDashboardAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a dashboard item
    /// </summary>
    public class DashboardItem
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}