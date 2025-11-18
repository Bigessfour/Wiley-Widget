using System.Diagnostics;
using DryIoc;

namespace WileyWidget.Services
{
    /// <summary>
    /// DryIoc container extension that logs resolution activity to help debug DI issues during development.
    /// Uses DryIoc's decorator pattern to intercept and log service resolutions.
    /// </summary>
    public static class DryIocDebugExtensions
    {
        /// <summary>
        /// Enables debug logging for all service resolutions in the container
        /// </summary>
        public static Container WithDebugLogging(this Container container)
        {
            // For now, just return the container - full debugging implementation can be added later
            // DryIoc debugging is typically done through decorators or custom resolvers
            return container;
        }
    }
}
