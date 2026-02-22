using System;
using System.Collections.Generic;
using System.Threading;
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

        /// <summary>
        /// Populates dashboard collections from the full Town of Wiley 2026 budget dataset
        /// </summary>
        Task PopulateDashboardMetricsFromWileyDataAsync(CancellationToken ct = default);

        /// <summary>
        /// Populates department summaries from Town of Wiley 2026 budget data using mapped departments
        /// </summary>
        Task PopulateDepartmentSummariesFromSanitationAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets enterprise snapshots for vital signs display
        /// </summary>
        Task<List<EnterpriseSnapshot>> GetEnterpriseSnapshotsAsync(CancellationToken ct = default);
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
