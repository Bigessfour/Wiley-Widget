#nullable enable

using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Provides access to recent activity log entries for dashboard/docking views.
/// </summary>
public interface IActivityLogRepository
{
    /// <summary>
    /// Returns recent activities ordered from newest to oldest.
    /// </summary>
    /// <param name="skip">Items to skip for paging.</param>
    /// <param name="take">Maximum items to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent activities.</returns>
    Task<List<ActivityItem>> GetRecentActivitiesAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a new activity entry.
    /// </summary>
    /// <param name="activityLog">The activity log entry to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogActivityAsync(ActivityLog activityLog, CancellationToken cancellationToken = default);
}
