using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class EfUserMemoryRepository : IUserMemoryRepository
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EfUserMemoryRepository> _logger;

    public EfUserMemoryRepository(
        IServiceProvider serviceProvider,
        ILogger<EfUserMemoryRepository> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task UpsertFactAsync(UserMemoryFact fact, CancellationToken cancellationToken = default)
    {
        if (fact == null)
        {
            throw new ArgumentNullException(nameof(fact));
        }

        if (string.IsNullOrWhiteSpace(fact.UserId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(fact.FactKey) || string.IsNullOrWhiteSpace(fact.FactValue))
        {
            return;
        }

        fact.UserId = fact.UserId.Trim();
        fact.FactKey = fact.FactKey.Trim();
        fact.FactValue = fact.FactValue.Trim();

        if (fact.UserId.Length > 128)
        {
            fact.UserId = fact.UserId[..128];
        }

        if (fact.FactKey.Length > 64)
        {
            fact.FactKey = fact.FactKey[..64];
        }

        if (fact.FactValue.Length > 512)
        {
            fact.FactValue = fact.FactValue[..512];
        }

        fact.Confidence = Math.Clamp(fact.Confidence, 0.0, 1.0);
        fact.ObservationCount = Math.Max(1, fact.ObservationCount);

        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await UpsertInternalAsync(context, fact, cancellationToken).ConfigureAwait(false);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueUserFactConflict(ex))
        {
            _logger.LogDebug(
                ex,
                "Detected concurrent UserMemoryFact upsert for UserId={UserId}, FactKey={FactKey}; retrying update path.",
                fact.UserId,
                fact.FactKey);

            context.ChangeTracker.Clear();
            await UpsertInternalAsync(context, fact, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<UserMemoryFact>> GetFactsForUserAsync(string userId, int take = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<UserMemoryFact>();
        }

        if (take <= 0)
        {
            take = 10;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var facts = await context.UserMemoryFacts
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Confidence)
            .ThenByDescending(item => item.LastObservedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return facts;
    }

    private static async Task UpsertInternalAsync(AppDbContext context, UserMemoryFact fact, CancellationToken cancellationToken)
    {
        var existing = await context.UserMemoryFacts
            .FirstOrDefaultAsync(
                item => item.UserId == fact.UserId && item.FactKey == fact.FactKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing == null)
        {
            var utcNow = DateTime.UtcNow;
            fact.CreatedAtUtc = utcNow;
            fact.UpdatedAtUtc = utcNow;
            fact.LastObservedAtUtc = utcNow;
            context.UserMemoryFacts.Add(fact);
            return;
        }

        existing.FactValue = fact.FactValue;
        existing.Confidence = Math.Max(existing.Confidence, fact.Confidence);
        existing.ObservationCount = Math.Max(1, existing.ObservationCount) + 1;
        existing.SourceConversationId = string.IsNullOrWhiteSpace(fact.SourceConversationId)
            ? existing.SourceConversationId
            : fact.SourceConversationId;
        existing.LastObservedAtUtc = DateTime.UtcNow;
        existing.UpdatedAtUtc = existing.LastObservedAtUtc;
    }

    private static bool IsUniqueUserFactConflict(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UserMemoryFacts", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
