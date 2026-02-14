using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class EfConversationRepository : IConversationRepository
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EfConversationRepository> _logger;

    public EfConversationRepository(
        IServiceProvider serviceProvider,
        ILogger<EfConversationRepository> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveConversationAsync(object conversation, CancellationToken cancellationToken = default)
    {
        if (conversation is not ConversationHistory history)
        {
            throw new ArgumentException(
                $"Expected {nameof(ConversationHistory)} but got {conversation?.GetType().FullName ?? "<null>"}",
                nameof(conversation));
        }

        if (string.IsNullOrWhiteSpace(history.ConversationId))
        {
            throw new ArgumentException("ConversationId is required.", nameof(conversation));
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var existing = await context.ConversationHistories
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(c => c.ConversationId == history.ConversationId)
            .ConfigureAwait(false);

        if (existing == null)
        {
            context.ConversationHistories.Add(history);
        }
        else
        {
            existing.Title = history.Title;
            existing.Content = history.Content;
            existing.MessagesJson = history.MessagesJson;
            existing.MessageCount = history.MessageCount;
            existing.UpdatedAt = history.UpdatedAt;

            // Preserve original CreatedAt if already set
            if (existing.CreatedAt == default)
            {
                existing.CreatedAt = history.CreatedAt;
            }
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<object?> GetConversationAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var conversation = await context.ConversationHistories
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(c => c.ConversationId == id)
            .ConfigureAwait(false);

        return conversation;
    }

    public async Task<List<object>> GetConversationsAsync(int skip, int limit, CancellationToken cancellationToken = default)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;

        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var conversations = await context.ConversationHistories
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .Skip(skip)
            .Take(limit)
            .ToListAsync()
            .ConfigureAwait(false);

        return conversations.Cast<object>().ToList();
    }

    public async Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var existing = await context.ConversationHistories
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId)
            .ConfigureAwait(false);

        if (existing == null)
        {
            _logger.LogDebug("Conversation not found for delete: {ConversationId}", conversationId);
            return;
        }

        context.ConversationHistories.Remove(existing);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
