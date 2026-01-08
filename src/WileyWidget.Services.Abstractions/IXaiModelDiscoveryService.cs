using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Service that discovers available xAI models for the authenticated API key and selects an appropriate model.
    /// </summary>
    public interface IXaiModelDiscoveryService
    {
        /// <summary>
        /// Returns available models (cached according to configuration).
        /// </summary>
        Task<IEnumerable<XaiModelDescriptor>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Choose the best model id to use for a given configured model or family.
        /// </summary>
        Task<XaiModelDescriptor?> ChooseBestModelAsync(string? configuredModelOrAlias = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forces a refresh of the discovery cache (useful for admin/diagnostics or tests).
        /// </summary>
        Task RefreshCacheAsync(CancellationToken cancellationToken = default);
    }
}
