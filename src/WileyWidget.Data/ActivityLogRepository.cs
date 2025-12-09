using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// EF Core implementation of activity log persistence.
/// </summary>
public class ActivityLogRepository : IActivityLogRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public ActivityLogRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<ActivityLog> LogActivityAsync(ActivityLog activity, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        activity.Timestamp = DateTime.UtcNow;
        dbContext.ActivityLogs.Add(activity);
        await dbContext.SaveChangesAsync(ct);
        return activity;
    }

    public async Task<List<ActivityLog>> GetRecentActivitiesAsync(int skip = 0, int take = 50, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.ActivityLogs
            .Where(a => !a.IsArchived)
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<ActivityLog>> GetActivitiesByUserAsync(string user, int limit = 100, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.ActivityLogs
            .Where(a => a.User == user && !a.IsArchived)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<ActivityLog>> GetActivitiesByTypeAsync(string activityType, int limit = 100, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.ActivityLogs
            .Where(a => a.ActivityType == activityType && !a.IsArchived)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<ActivityLog>> GetActivitiesByEntityAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.ActivityLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && !a.IsArchived)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<ActivityLog>> SearchActivitiesAsync(string keyword, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var lowerKeyword = keyword.ToLower();
        return await dbContext.ActivityLogs
            .Where(a => !a.IsArchived &&
                   (a.Activity.ToLower().Contains(lowerKeyword) ||
                    (a.Details != null && a.Details.ToLower().Contains(lowerKeyword))))
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task ArchiveOldActivitiesAsync(DateTime cutoffDate, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var activitiesToArchive = await dbContext.ActivityLogs
            .Where(a => !a.IsArchived && a.Timestamp < cutoffDate)
            .ToListAsync(ct);

        foreach (var activity in activitiesToArchive)
        {
            activity.IsArchived = true;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTime? startDate = null, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var query = dbContext.ActivityLogs.Where(a => !a.IsArchived);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= startDate.Value);
        }

        var stats = await query
            .GroupBy(a => a.ActivityType)
            .Select(g => new { ActivityType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityType, x => x.Count, ct);

        return stats;
    }
}
