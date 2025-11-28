using System.Collections.Generic;
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
        Task<IEnumerable<DashboardItem>> GetDashboardDataAsync();

        /// <summary>
        /// Gets dashboard items for display
        /// </summary>
        Task<IEnumerable<DashboardItem>> GetDashboardItemsAsync();

        /// <summary>
        /// Refreshes dashboard data
        /// </summary>
        Task RefreshDashboardAsync();
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
