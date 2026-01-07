using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Abstractions;

/// <summary>
/// Interface for services that require asynchronous initialization after the main form is shown.
/// This prevents blocking the UI thread during startup with heavy I/O operations.
/// </summary>
public interface IAsyncInitializable
{
    /// <summary>
    /// Performs asynchronous initialization. Called after MainForm.Shown to avoid blocking UI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
