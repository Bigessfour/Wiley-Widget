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
    Task SaveStateAsync(object state);

    /// <summary>
    /// Restores the previously saved UI state
    /// </summary>
    Task<object?> RestoreStateAsync();

    /// <summary>
    /// Clears the saved state
    /// </summary>
    Task ClearStateAsync();
}
