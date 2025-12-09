using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// EF Core implementation of AI context entity persistence.
/// </summary>
public class AIContextRepository : IAIContextRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public AIContextRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<AIContextEntity> SaveEntityAsync(AIContextEntity entity, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var existing = await dbContext.AIContextEntities
            .FirstOrDefaultAsync(e => 
                e.ConversationId == entity.ConversationId &&
                e.EntityType == entity.EntityType &&
                e.NormalizedValue == entity.NormalizedValue, ct);

        if (existing != null)
        {
            // Update existing entity
            existing.LastMentionedAt = DateTime.UtcNow;
            existing.MentionCount++;
            existing.Context = entity.Context; // Update with latest context
            existing.ImportanceScore = Math.Max(existing.ImportanceScore, entity.ImportanceScore);
        }
        else
        {
            // Insert new entity
            entity.FirstMentionedAt = DateTime.UtcNow;
            entity.LastMentionedAt = DateTime.UtcNow;
            dbContext.AIContextEntities.Add(entity);
        }

        await dbContext.SaveChangesAsync(ct);
        return existing ?? entity;
    }

    public async Task<List<AIContextEntity>> GetEntitiesByConversationAsync(string conversationId, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.AIContextEntities
            .Where(e => e.ConversationId == conversationId && e.IsActive)
            .OrderByDescending(e => e.LastMentionedAt)
            .ToListAsync(ct);
    }

    public async Task<List<AIContextEntity>> SearchEntitiesAsync(string entityType, string searchValue, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var normalized = searchValue.ToLower().Trim();
        return await dbContext.AIContextEntities
            .Where(e => e.EntityType == entityType && 
                        e.NormalizedValue.Contains(normalized) && 
                        e.IsActive)
            .OrderByDescending(e => e.MentionCount)
            .ThenByDescending(e => e.LastMentionedAt)
            .ToListAsync(ct);
    }

    public async Task<List<AIContextEntity>> GetRecentEntitiesAsync(int limit = 50, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.AIContextEntities
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.LastMentionedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<AIContextEntity>> GetEntitiesByTypeAsync(string entityType, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.AIContextEntities
            .Where(e => e.EntityType == entityType && e.IsActive)
            .OrderByDescending(e => e.ImportanceScore)
            .ThenByDescending(e => e.MentionCount)
            .ToListAsync(ct);
    }

    public async Task IncrementMentionCountAsync(int entityId, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var entity = await dbContext.AIContextEntities.FindAsync(new object[] { entityId }, ct);
        if (entity != null)
        {
            entity.MentionCount++;
            entity.LastMentionedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task ArchiveInactiveEntitiesAsync(DateTime cutoffDate, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var entitiesToArchive = await dbContext.AIContextEntities
            .Where(e => e.IsActive && e.LastMentionedAt < cutoffDate)
            .ToListAsync(ct);

        foreach (var entity in entitiesToArchive)
        {
            entity.IsActive = false;
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
