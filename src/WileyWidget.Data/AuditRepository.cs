#nullable enable

using System.Threading;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for audit trail data operations
/// </summary>
public class AuditRepository : IAuditRepository
{
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.AuditRepository");

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuditRepository> _logger;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public AuditRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        IMemoryCache cache,
        ILogger<AuditRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets audit trail entries within a date range
    /// </summary>
    public async Task<IEnumerable<AuditEntry>> GetAuditTrailAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuditRepository.GetAuditTrail");
        activity?.SetTag("operation.type", "query");
        activity?.SetTag("start_date", startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        activity?.SetTag("end_date", endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        try
        {
            _logger.LogDebug("Retrieving audit trail from {StartDate} to {EndDate}", startDate, endDate);

            await using var context = await _contextFactory.CreateDbContextAsync();
            var result = await context.AuditEntries
                .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate)
                .OrderByDescending(a => a.Timestamp)
                .AsNoTracking()
                .ToListAsync();

            activity?.SetTag("result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Retrieved {Count} audit entries from {StartDate} to {EndDate}", result.Count, startDate, endDate);

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error retrieving audit trail from {StartDate} to {EndDate}", startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Gets audit trail entries for a specific entity type
    /// </summary>
    public async Task<IEnumerable<AuditEntry>> GetAuditTrailForEntityAsync(string entityType, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AuditEntries
            .Where(a => a.EntityType == entityType && a.Timestamp >= startDate && a.Timestamp <= endDate)
            .OrderByDescending(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets audit trail entries for a specific entity
    /// </summary>
    public async Task<IEnumerable<AuditEntry>> GetAuditTrailForEntityAsync(string entityType, int entityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AuditEntries
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && a.Timestamp >= startDate && a.Timestamp <= endDate)
            .OrderByDescending(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new audit entry
    /// </summary>
    public async Task AddAuditEntryAsync(AuditEntry auditEntry, CancellationToken cancellationToken = default)
    {
        if (auditEntry == null) throw new ArgumentNullException(nameof(auditEntry));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.AuditEntries.Add(auditEntry);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Gets paged audit entries with sorting support
    /// </summary>
    public async Task<(IEnumerable<AuditEntry> Items, int TotalCount)> GetPagedAsync(int pageNumber = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = false,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? entityType = null, CancellationToken cancellationToken = default)
    {
        // Validate paging parameters
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize < 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.AuditEntries.AsQueryable();

        // Apply filters
        if (startDate.HasValue)
            query = query.Where(a => a.Timestamp >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(a => a.Timestamp <= endDate.Value);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);

        // Apply sorting
        query = ApplySorting(query, sortBy, sortDescending);

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply paging
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Gets an IQueryable for flexible querying and paging
    /// </summary>
    public async Task<IQueryable<AuditEntry>> GetQueryableAsync(CancellationToken cancellationToken = default)
    {
        var context = await _contextFactory.CreateDbContextAsync();
        return context.AuditEntries.AsQueryable();
    }

    private IQueryable<AuditEntry> ApplySorting(IQueryable<AuditEntry> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            return sortDescending
                ? query.OrderByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.Timestamp);
        }

        return sortBy.ToLowerInvariant() switch
        {
            "timestamp" => sortDescending
                ? query.OrderByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.Timestamp),
            "entitytype" => sortDescending
                ? query.OrderByDescending(a => a.EntityType)
                : query.OrderBy(a => a.EntityType),
            "action" => sortDescending
                ? query.OrderByDescending(a => a.Action)
                : query.OrderBy(a => a.Action),
            "username" => sortDescending
                ? query.OrderByDescending(a => a.User)
                : query.OrderBy(a => a.User),
            _ => sortDescending
                ? query.OrderByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.Timestamp)
        };
    }
}
