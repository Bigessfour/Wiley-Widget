// App.ExceptionHandling.cs - Application Exception Handling Partial Class
// Contains: Global exception handling setup and error management methods
// Part of App.xaml.cs partial class split for maintainability

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using Prism.Events;
using Serilog.Events;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;

namespace WileyWidget
{
    public partial class App
    {
        #region Exception Handling

        /// <summary>
        /// Sets up comprehensive global exception handling for the application.
        /// Configures DispatcherUnhandledException, EventAggregator error subscriptions,
        /// and integrates with error reporting and telemetry services.
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            var errorReportingService = ResolveWithRetry<ErrorReportingService>();
            var telemetryService = ResolveWithRetry<TelemetryStartupService>();
            var eventAggregator = this.Container.Resolve<IEventAggregator>();

            // DispatcherUnhandledException
            Application.Current.DispatcherUnhandledException += (sender, e) =>
            {
                var processedEx = TryUnwrapTargetInvocationException(e.Exception);
                if (TryHandleDryIocContainerException(processedEx) || TryHandleXamlException(processedEx))
                {
                    e.Handled = true;
                    // ... (your existing handling)
                    return;
                }
                // Fallback logging/reporting
                Log.Fatal(processedEx, "Unhandled Dispatcher exception");
                errorReportingService?.TrackEvent("Exception_Unhandled", new Dictionary<string, object> { ["Type"] = processedEx.GetType().Name });  // If no TrackException, use TrackEvent
            };

            // AppDomain Unhandled (already in constructor)
            // EventAggregator subscriptions for nav/errors (integrated from unused method)
            eventAggregator.GetEvent<NavigationErrorEvent>().Subscribe(errorEvent =>
            {
                Log.Error("Global nav error: {Region} -> {View}: {Msg}", errorEvent.RegionName, errorEvent.TargetView, errorEvent.ErrorMessage);
            }, ThreadOption.UIThread);

            eventAggregator.GetEvent<GeneralErrorEvent>().Subscribe(errorEvent =>
            {
                Log.Write(errorEvent.IsHandled ? LogEventLevel.Warning : LogEventLevel.Error, errorEvent.Error,
                    "Global error: {Source}.{Op} - {Msg}", errorEvent.Source, errorEvent.Operation, errorEvent.ErrorMessage);
            }, ThreadOption.UIThread);

            Log.Information("✓ Global exception handling configured with EventAggregator");
        }

        /// <summary>
        /// Unwraps TargetInvocationException to get the actual inner exception.
        /// </summary>
        private Exception TryUnwrapTargetInvocationException(Exception ex)
        {
            return ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
        }

        /// <summary>
        /// Attempts to handle DryIoc container exceptions gracefully.
        /// </summary>
        private bool TryHandleDryIocContainerException(Exception ex)
        {
            if (ex is ContainerException)  // DryIoc-specific
            {
                Log.Warning(ex, "Handled DryIoc container exception");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to handle WPF XAML parse exceptions gracefully.
        /// </summary>
        private bool TryHandleXamlException(Exception ex)
        {
            if (ex is XamlParseException)  // WPF XAML errors
            {
                Log.Warning(ex, "Handled XAML parse exception");
                return true;
            }
            return false;
        }

        #endregion
    }
}
