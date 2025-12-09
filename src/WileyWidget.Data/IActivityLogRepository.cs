using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for activity log persistence and retrieval.
/// </summary>
public interface IActivityLogRepository
{
    /// <summary>
    /// Log a new activity.
    /// </summary>
    Task<ActivityLog> LogActivityAsync(ActivityLog activity, CancellationToken ct = default);

    /// <summary>
    /// Get recent activities with pagination.
    /// </summary>
    Task<List<ActivityLog>> GetRecentActivitiesAsync(int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>
    /// Get activities by user.
    /// </summary>
    Task<List<ActivityLog>> GetActivitiesByUserAsync(string user, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Get activities by type.
    /// </summary>
    Task<List<ActivityLog>> GetActivitiesByTypeAsync(string activityType, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Get activities for a specific entity.
    /// </summary>
    Task<List<ActivityLog>> GetActivitiesByEntityAsync(string entityType, string entityId, CancellationToken ct = default);

    /// <summary>
    /// Search activities by keyword.
    /// </summary>
    Task<List<ActivityLog>> SearchActivitiesAsync(string keyword, CancellationToken ct = default);

    /// <summary>
    /// Archive old activities (soft delete).
    /// </summary>
    Task ArchiveOldActivitiesAsync(DateTime cutoffDate, CancellationToken ct = default);

    /// <summary>
    /// Get activity statistics (counts by type, user, etc.).
    /// </summary>
    Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTime? startDate = null, CancellationToken ct = default);
}
