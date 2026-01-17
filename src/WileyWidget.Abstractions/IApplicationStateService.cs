using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Abstractions;

/// <summary>
/// Service for persisting and restoring application state across sessions
/// </summary>
public interface IApplicationStateService
{
    /// <summary>
    /// Saves the current UI state (filters, selections, etc.)
    /// </summary>
    Task SaveStateAsync(object state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the previously saved UI state
    /// </summary>
    Task<object?> RestoreStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the saved state
    /// </summary>
    Task ClearStateAsync(CancellationToken cancellationToken = default);
}
