using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service interface for dashboard operations
    /// </summary>
    /// <summary>
    /// Represents a interface for idashboardservice.
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

        /// <summary>
        /// Gets data statistics for diagnostic purposes
        /// </summary>
        Task<(int TotalRecords, DateTime? OldestRecord, DateTime? NewestRecord)> GetDataStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a dashboard item
    /// </summary>
    /// <summary>
    /// Represents a class for dashboarditem.
    /// </summary>
    public class DashboardItem
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        public string Category { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the fiscal year.
        /// </summary>
        public int FiscalYear { get; set; }
    }
}
