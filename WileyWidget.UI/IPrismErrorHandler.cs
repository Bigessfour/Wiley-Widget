using System;
using Prism.Events;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for centralized error handling in Prism applications.
    /// Provides methods for handling navigation errors and general application errors
    /// with consistent logging and event publishing.
    /// </summary>
    public interface IPrismErrorHandler
    {
        /// <summary>
        /// Handles navigation errors that occur during region navigation.
        /// </summary>
        /// <param name="regionName">The name of the region where navigation failed</param>
        /// <param name="targetUri">The URI that was being navigated to</param>
        /// <param name="error">The exception that occurred (optional)</param>
        /// <param name="errorMessage">A descriptive error message</param>
        void HandleNavigationError(string regionName, string targetUri, Exception? error, string errorMessage);

        /// <summary>
        /// Handles general application errors.
        /// </summary>
        /// <param name="source">The source component where the error occurred</param>
        /// <param name="operation">The operation being performed when the error occurred</param>
        /// <param name="error">The exception that occurred (optional)</param>
        /// <param name="errorMessage">A descriptive error message</param>
        /// <param name="isHandled">Whether the error has been handled and doesn't need further processing</param>
        void HandleGeneralError(string source, string operation, Exception? error, string errorMessage, bool isHandled = false);

        /// <summary>
        /// Registers global navigation error handlers with the Prism EventAggregator.
        /// This method should be called during application initialization.
        /// </summary>
        void RegisterGlobalNavigationHandlers();
    }
}
