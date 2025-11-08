using System;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Serilog;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.Services
{
    /// <summary>
    /// Centralized error handler for Prism applications.
    /// Provides consistent error handling, logging, and event publishing for navigation and general errors.
    /// </summary>
    public class PrismErrorHandler : IPrismErrorHandler, IExceptionHandler
    {
        private readonly ILogger<PrismErrorHandler> _logger;
        private readonly IEventAggregator _eventAggregator;

        public PrismErrorHandler(ILogger<PrismErrorHandler> logger, IEventAggregator eventAggregator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        /// <inheritdoc/>
        public void HandleNavigationError(string regionName, string targetUri, Exception? error, string errorMessage)
        {
            // Log the navigation error
            _logger.LogError(error, "Navigation failed in region '{RegionName}' to '{TargetUri}': {ErrorMessage}",
                regionName, targetUri, errorMessage);

            // Also log to Serilog for consistency
            Log.Error(error, "Navigation failed in region '{RegionName}' to '{TargetUri}': {ErrorMessage}",
                regionName, targetUri, errorMessage);

            // Publish navigation error event for subscribers
            _eventAggregator.GetEvent<NavigationErrorEvent>().Publish(new NavigationErrorEvent
            {
                RegionName = regionName,
                TargetView = targetUri,
                Error = error,
                ErrorMessage = errorMessage
            });
        }

        /// <inheritdoc/>
        public void RegisterGlobalNavigationHandlers()
        {
            // This method is called during application initialization to ensure
            // global navigation handlers are registered. The actual registration
            // is done in App.xaml.cs using the EventAggregator.
            _logger.LogDebug("Global navigation handlers registration requested");
        }

        /// <inheritdoc/>
        public void HandleGeneralError(string source, string operation, Exception? error, string errorMessage, bool isHandled = false)
        {
            // Determine log level based on whether error is handled
            var logLevel = isHandled ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Error;

            // Log the general error
            if (isHandled)
            {
                _logger.LogWarning(error, "Error in {Source}.{Operation}: {ErrorMessage}", source, operation, errorMessage);
            }
            else
            {
                _logger.LogError(error, "Error in {Source}.{Operation}: {ErrorMessage}", source, operation, errorMessage);
            }

            // Also log to Serilog
            Log.Write(logLevel, "Error in {Source}.{Operation}: {ErrorMessage}", source, operation, errorMessage);

            // Publish general error event for subscribers
            _eventAggregator.GetEvent<GeneralErrorEvent>().Publish(new GeneralErrorEvent
            {
                Source = source,
                Operation = operation,
                Error = error,
                ErrorMessage = errorMessage,
                IsHandled = isHandled
            });
        }
    }
}
