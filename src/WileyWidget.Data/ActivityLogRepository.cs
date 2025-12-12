#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using IActivityLogRepository = WileyWidget.Business.Interfaces.IActivityLogRepository;

namespace WileyWidget.Data;

/// <summary>
/// Provides activity log data for dashboards and docking panels.
/// Persists to the ActivityLog table.
/// </summary>
public sealed class ActivityLogRepository : IActivityLogRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ActivityLogRepository> _logger;

    public ActivityLogRepository(IDbContextFactory<AppDbContext> contextFactory, ILogger<ActivityLogRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ActivityItem>> GetRecentActivitiesAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var safeSkip = Math.Max(0, skip);
        var safeTake = take <= 0 ? 50 : take;

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var activities = await db.ActivityLogs
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Skip(safeSkip)
            .Take(safeTake)
            .Select(a => new ActivityItem
            {
                Timestamp = a.Timestamp,
                Activity = a.Activity,
                Details = a.Details ?? string.Empty,
                User = a.User ?? "System",
                Category = a.Category ?? string.Empty,
                Icon = a.Icon ?? string.Empty,
                ActivityType = a.ActivityType ?? string.Empty
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Returning {Count} activity items (skip {Skip}, take {Take})", activities.Count, safeSkip, safeTake);
        return activities;
    }

    public async Task LogActivityAsync(ActivityLog activityLog, CancellationToken cancellationToken = default)
    {
        if (activityLog == null)
        {
            throw new ArgumentNullException(nameof(activityLog));
        }

        // Set timestamp if not already set
        if (activityLog.Timestamp == default)
        {
            activityLog.Timestamp = DateTime.UtcNow;
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await db.ActivityLogs.AddAsync(activityLog, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Logged activity: {ActivityType} - {Activity}", activityLog.ActivityType, activityLog.Activity);
    }
}
