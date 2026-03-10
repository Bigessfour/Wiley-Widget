using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

public sealed class UserMemoryFact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string UserId { get; set; } = string.Empty;

    public string FactKey { get; set; } = string.Empty;

    public string FactValue { get; set; } = string.Empty;

    public double Confidence { get; set; } = 0.5;

    public int ObservationCount { get; set; } = 1;

    public string? SourceConversationId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastObservedAtUtc { get; set; } = DateTime.UtcNow;
}

public interface IUserMemoryRepository
{
    Task UpsertFactAsync(UserMemoryFact fact, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMemoryFact>> GetFactsForUserAsync(string userId, int take = 10, CancellationToken cancellationToken = default);
}
