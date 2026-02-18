using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Repository contract for persisting and retrieving scenario snapshots.
/// </summary>
public interface IScenarioSnapshotRepository
{
    /// <summary>
    /// Saves a scenario snapshot.
    /// </summary>
    Task<SavedScenarioSnapshot> SaveAsync(SavedScenarioSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent scenario snapshots.
    /// </summary>
    Task<IReadOnlyList<SavedScenarioSnapshot>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default);
}
