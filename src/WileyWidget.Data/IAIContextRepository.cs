using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for AI context entity persistence and retrieval.
/// </summary>
public interface IAIContextRepository
{
    /// <summary>
    /// Save or update a context entity.
    /// </summary>
    Task<AIContextEntity> SaveEntityAsync(AIContextEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Get all entities for a specific conversation.
    /// </summary>
    Task<List<AIContextEntity>> GetEntitiesByConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Search entities by type and value.
    /// </summary>
    Task<List<AIContextEntity>> SearchEntitiesAsync(string entityType, string searchValue, CancellationToken ct = default);

    /// <summary>
    /// Get recently mentioned entities across all conversations.
    /// </summary>
    Task<List<AIContextEntity>> GetRecentEntitiesAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Get entities by type (e.g., all "Person" entities).
    /// </summary>
    Task<List<AIContextEntity>> GetEntitiesByTypeAsync(string entityType, CancellationToken ct = default);

    /// <summary>
    /// Update mention count and last mentioned timestamp.
    /// </summary>
    Task IncrementMentionCountAsync(int entityId, CancellationToken ct = default);

    /// <summary>
    /// Archive old or inactive entities.
    /// </summary>
    Task ArchiveInactiveEntitiesAsync(DateTime cutoffDate, CancellationToken ct = default);
}
