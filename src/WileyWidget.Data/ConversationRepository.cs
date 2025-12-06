using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using WileyWidget.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// EF Core implementation of conversation persistence.
/// </summary>
public class ConversationRepository : IConversationRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConversationRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<ConversationHistory> SaveConversationAsync(ConversationHistory conversation, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        
        var existing = await dbContext.ConversationHistories
            .FirstOrDefaultAsync(c => c.ConversationId == conversation.ConversationId, ct);

        conversation.UpdatedAt = DateTime.UtcNow;

        if (existing != null)
        {
            // Update existing
            dbContext.Entry(existing).CurrentValues.SetValues(conversation);
        }
        else
        {
            // Insert new
            dbContext.ConversationHistories.Add(conversation);
        }

        await dbContext.SaveChangesAsync(ct);
        return conversation;
    }

    public async Task<ConversationHistory?> GetConversationAsync(string conversationId, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var conversation = await dbContext.ConversationHistories
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId && !c.IsArchived, ct);

        if (conversation != null)
        {
            conversation.LastAccessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }

        return conversation;
    }

    public async Task<List<ConversationHistory>> GetConversationsAsync(int skip = 0, int take = 20, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.ConversationHistories
            .Where(c => !c.IsArchived)
            .OrderByDescending(c => c.LastAccessedAt ?? c.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task DeleteConversationAsync(string conversationId, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var conversation = await dbContext.ConversationHistories
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId, ct);

        if (conversation != null)
        {
            conversation.IsArchived = true;
            conversation.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task SetFavoriteAsync(string conversationId, bool isFavorite, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var conversation = await dbContext.ConversationHistories
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId, ct);

        if (conversation != null)
        {
            conversation.IsFavorite = isFavorite;
            conversation.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<List<ConversationHistory>> GetFavoritesAsync(CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return await dbContext.ConversationHistories
            .Where(c => c.IsFavorite && !c.IsArchived)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<ConversationHistory>> SearchAsync(string query, CancellationToken ct = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var lowerQuery = query.ToLower();
        return await dbContext.ConversationHistories
            .Where(c => !c.IsArchived && 
                   (c.Title.ToLower().Contains(lowerQuery) ||
                    (c.Description != null && c.Description.ToLower().Contains(lowerQuery))))
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);
    }
}
