using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Stub implementation of IConversationRepository for AI chat persistence.
/// TODO: Replace with real implementation using Entity Framework or SQLite.
/// </summary>
public sealed class StubConversationRepository : IConversationRepository
{
    private readonly ILogger<StubConversationRepository> _logger;

    public StubConversationRepository(ILogger<StubConversationRepository> logger)
    {
        _logger = logger;
    }

    public Task SaveConversationAsync(object conversation)
    {
        _logger.LogInformation("Stub: SaveConversationAsync called");
        return Task.CompletedTask;
    }

    public Task<object?> GetConversationAsync(string id)
    {
        _logger.LogInformation("Stub: GetConversationAsync called for {Id}", id);
        return Task.FromResult<object?>(null);
    }

    public Task<List<object>> GetConversationsAsync(int skip, int limit)
    {
        _logger.LogInformation("Stub: GetConversationsAsync called (skip={Skip}, limit={Limit})", skip, limit);
        return Task.FromResult(new List<object>());
    }

    public Task DeleteConversationAsync(string conversationId)
    {
        _logger.LogInformation("Stub: DeleteConversationAsync called for {ConversationId}", conversationId);
        return Task.CompletedTask;
    }
}
