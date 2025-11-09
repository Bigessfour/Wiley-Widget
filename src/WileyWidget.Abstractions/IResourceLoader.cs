using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Defines the contract for enterprise-grade application resource loading.
    /// Implementations must provide retry logic, timeout enforcement, idempotency,
    /// and comprehensive telemetry integration.
    /// </summary>
    public interface IResourceLoader
    {
        /// <summary>
        /// Loads application resources with comprehensive error handling and resilience.
        /// This method is idempotent and safe to call multiple times.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token for coordinated shutdown</param>
        /// <returns>Detailed result indicating success/failure and performance metrics</returns>
        Task<ResourceLoadResult> LoadApplicationResourcesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if resources have already been loaded successfully.
        /// Used to implement idempotency and prevent duplicate loads.
        /// </summary>
        bool AreResourcesLoaded { get; }

        /// <summary>
        /// Gets the timestamp of the last successful resource load.
        /// </summary>
        DateTimeOffset? LastLoadTimestamp { get; }
    }

    /// <summary>
    /// Classification for resource criticality.
    /// Determines whether loading failure should halt application startup.
    /// </summary>
    public enum ResourceCriticality
    {
        /// <summary>Critical resource - application cannot start without it</summary>
        Critical,

        /// <summary>Optional resource - application can start with degraded functionality</summary>
        Optional
    }

    /// <summary>
    /// Result object for resource loading operations with detailed metrics and error information.
    /// Supports enterprise monitoring and observability requirements.
    /// </summary>
    public class ResourceLoadResult
    {
        /// <summary>
        /// Indicates whether the resource loading operation completed successfully.
        /// True only if all critical resources loaded and no unhandled errors occurred.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of resources successfully loaded.
        /// </summary>
        public int LoadedCount { get; set; }

        /// <summary>
        /// Number of errors encountered during loading.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Number of retry attempts executed across all resource loads.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Total time taken for the loading operation in milliseconds.
        /// </summary>
        public long LoadTimeMs { get; set; }

        /// <summary>
        /// List of successfully loaded resource paths.
        /// </summary>
        public List<string> LoadedPaths { get; set; } = new();

        /// <summary>
        /// List of resource paths that failed to load.
        /// </summary>
        public List<string> FailedPaths { get; set; } = new();

        /// <summary>
        /// Collection of all errors encountered during loading.
        /// </summary>
        public List<Exception> Errors { get; set; } = new();

        /// <summary>
        /// Indicates if any critical resources failed to load.
        /// </summary>
        public bool HasCriticalFailures { get; set; }

        /// <summary>
        /// Additional diagnostic information for troubleshooting.
        /// </summary>
        public Dictionary<string, object> Diagnostics { get; set; } = new();

        /// <summary>
        /// Returns a summary string of the loading results.
        /// </summary>
        public override string ToString()
        {
            return $"ResourceLoadResult: Success={Success}, Loaded={LoadedCount}, Errors={ErrorCount}, Retries={RetryCount}, Time={LoadTimeMs}ms, CriticalFailures={HasCriticalFailures}";
        }
    }

    /// <summary>
    /// Specialized exception for resource loading failures.
    /// Provides context about which resources failed and why.
    /// </summary>
    public class ResourceLoadException : Exception
    {
        public List<string> FailedResources { get; }
        public bool IsCritical { get; }

        public ResourceLoadException(string message, List<string> failedResources, bool isCritical = false)
            : base(message)
        {
            FailedResources = failedResources ?? new List<string>();
            IsCritical = isCritical;
        }

        public ResourceLoadException(string message, List<string> failedResources, Exception innerException, bool isCritical = false)
            : base(message, innerException)
        {
            FailedResources = failedResources ?? new List<string>();
            IsCritical = isCritical;
        }
    }
}
