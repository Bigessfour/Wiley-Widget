#nullable enable

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using IActivityLogRepository = WileyWidget.Business.Interfaces.IActivityLogRepository;

namespace WileyWidget.Data;

/// <summary>
/// Provides activity log data for dashboards and docking panels.
/// Persists to the ActivityLog table.
/// CRITICAL: All read-only queries use .AsNoTracking() to prevent ObjectDisposedException
/// when binding entities to UI controls after the scoped DbContext is disposed.
/// This is a mandatory pattern for disconnected/UI scenarios per EF Core best practices.
/// </summary>
public sealed class ActivityLogRepository : IActivityLogRepository
{
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.ActivityLogRepository");

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ActivityLogRepository> _logger;

    public ActivityLogRepository(IDbContextFactory<AppDbContext> contextFactory, ILogger<ActivityLogRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves recent activity log entries for UI display (dashboard panels, grids, etc.)
    /// PERFORMANCE & SAFETY:
    /// - Uses .AsNoTracking() for read-only queries - prevents change tracking overhead.
    /// - Projects to ActivityItem DTO - decouples UI from EF entity, prevents ObjectDisposedException.
    /// - ActivityItem DTOs are detached from DbContext and safe for binding after scope disposal.
    /// This pattern is MANDATORY for all UI-bound repository methods to prevent:
    /// - ObjectDisposedException when grid operations (sort, filter, render) access disposed DbContext.
    /// - Memory overhead from tracking entities that will never be modified.
    /// References:
    /// - EF Core docs: https://learn.microsoft.com/en-us/ef/core/querying/tracking#no-tracking-queries
    /// - Syncfusion binding: https://help.syncfusion.com/windowsforms/datagrid/data-binding
    /// </summary>
    /// <param name="skip">Number of records to skip for pagination.</param>
    /// <param name="take">Maximum number of records to retrieve (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>List of ActivityItem DTOs safe for UI binding.</returns>
    public async Task<List<ActivityItem>> GetRecentActivitiesAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ActivityLogRepository.GetRecentActivities");
        activity?.SetTag("operation.type", "query");
        activity?.SetTag("skip", skip);
        activity?.SetTag("take", take);

        var safeSkip = Math.Max(0, skip);
        var safeTake = take <= 0 ? 50 : take;

        try
        {
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

            activity?.SetTag("result.count", activities.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogDebug("Returning {Count} activity items (skip {Skip}, take {Take})", activities.Count, safeSkip, safeTake);

            return activities;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error retrieving recent activities (skip {Skip}, take {Take})", safeSkip, safeTake);
            throw;
        }
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
