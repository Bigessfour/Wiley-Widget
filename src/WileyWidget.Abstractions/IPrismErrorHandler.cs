using System;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Interface for centralized error handling in Prism applications.
    /// Provides methods for handling navigation errors and general application errors
    /// with consistent logging and event publishing.
    /// </summary>
    public interface IPrismErrorHandler
    {
        void HandleNavigationError(string regionName, string targetUri, Exception? error, string errorMessage);
        void HandleGeneralError(string source, string operation, Exception? error, string errorMessage, bool isHandled = false);
        void RegisterGlobalNavigationHandlers();
    }
}
