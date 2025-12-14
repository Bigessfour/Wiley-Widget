using System;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Lightweight exception handler contract used for DI registration and centralized handling.
    /// Implementations should provide application-wide handling for navigation and general errors.
    /// </summary>
    public interface IExceptionHandler
    {
        void HandleNavigationError(string regionName, string targetUri, Exception? error, string errorMessage);
        void HandleGeneralError(string source, string operation, Exception? error, string errorMessage, bool isHandled = false);
        void RegisterGlobalNavigationHandlers();
    }
}
