using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// In-memory stub implementation of IConversationRepository for AI chat persistence.
/// Production version should use Entity Framework with SQLite or SQL Server for real persistence.
/// </summary>
public sealed class StubConversationRepository : IConversationRepository
{
    private readonly ILogger<StubConversationRepository> _logger;
    private readonly Dictionary<string, object> _conversations = new();

    public StubConversationRepository(ILogger<StubConversationRepository> logger)
    {
        _logger = logger;
    }

    public Task SaveConversationAsync(object conversation)
    {
        try
        {
            if (conversation == null) return Task.CompletedTask;

            var id = Guid.NewGuid().ToString();
            _conversations[id] = conversation;
            _logger.LogInformation("Conversation saved (in-memory): {ConversationId}", id);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving conversation");
            return Task.CompletedTask;
        }
    }

    public Task<object?> GetConversationAsync(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id)) return Task.FromResult<object?>(null);

            var result = _conversations.TryGetValue(id, out var conv) ? conv : null;
            _logger.LogInformation("Conversation retrieved (in-memory): {ConversationId}, Found: {Found}", id, result != null);
            return Task.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation: {ConversationId}", id);
            return Task.FromResult<object?>(null);
        }
    }

    public Task<List<object>> GetConversationsAsync(int skip, int limit)
    {
        try
        {
            var result = _conversations.Values
                .Skip(skip)
                .Take(limit > 0 ? limit : 50)
                .ToList();
            _logger.LogInformation("Conversations retrieved (in-memory): skip={Skip}, limit={Limit}, count={Count}", skip, limit, result.Count);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations");
            return Task.FromResult(new List<object>());
        }
    }

    public Task DeleteConversationAsync(string conversationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(conversationId)) return Task.CompletedTask;

            var deleted = _conversations.Remove(conversationId);
            _logger.LogInformation("Conversation deleted (in-memory): {ConversationId}, Deleted: {Deleted}", conversationId, deleted);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation: {ConversationId}", conversationId);
            return Task.CompletedTask;
        }
    }
}
