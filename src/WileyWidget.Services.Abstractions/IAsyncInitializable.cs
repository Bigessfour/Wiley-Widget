using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Asynchronous initialization hook for services that perform deferred startup work.
    /// </summary>
    public interface IAsyncInitializable
    {
        /// <summary>
        /// Perform asynchronous initialization work. Implementations should honor the cancellation token.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}