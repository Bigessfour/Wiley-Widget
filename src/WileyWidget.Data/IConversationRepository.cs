using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for persisting and retrieving conversation history.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Save a new conversation or update an existing one.
    /// </summary>
    Task<ConversationHistory> SaveConversationAsync(ConversationHistory conversation, CancellationToken ct = default);

    /// <summary>
    /// Retrieve a conversation by its ID.
    /// </summary>
    Task<ConversationHistory?> GetConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Get all conversations for the current user (with pagination).
    /// </summary>
    Task<List<ConversationHistory>> GetConversationsAsync(int skip = 0, int take = 20, CancellationToken ct = default);

    /// <summary>
    /// Delete a conversation (soft delete).
    /// </summary>
    Task DeleteConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Mark a conversation as a favorite.
    /// </summary>
    Task SetFavoriteAsync(string conversationId, bool isFavorite, CancellationToken ct = default);

    /// <summary>
    /// Get favorite conversations.
    /// </summary>
    Task<List<ConversationHistory>> GetFavoritesAsync(CancellationToken ct = default);

    /// <summary>
    /// Search conversations by title or description.
    /// </summary>
    Task<List<ConversationHistory>> SearchAsync(string query, CancellationToken ct = default);
}
