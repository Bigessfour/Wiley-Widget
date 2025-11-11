// App.ExceptionHandling.cs - Application Exception Handling Partial Class
// Contains: Global exception handling setup and error management methods
// Part of App.xaml.cs partial class split for maintainability

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using DryIoc;
using Prism.Events;
using Serilog;
using Serilog.Events;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;
using WileyWidget.ViewModels.Messages;

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
                if (TryHandleDryIocContainerException(processedEx) || TryHandleXamlException(processedEx) || TryHandlePresentationFrameworkException(processedEx))
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

        /// <summary>
        /// Attempts to handle PresentationFramework ArgumentException gracefully.
        /// These often occur due to XAML resource issues, invalid pack URIs, or duplicate keys.
        /// </summary>
        private bool TryHandlePresentationFrameworkException(Exception ex)
        {
            if (ex is ArgumentException argEx)
            {
                // Check if it's coming from PresentationFramework
                var stackTrace = new StackTrace(ex, true);
                var frames = stackTrace.GetFrames();
                bool isPresentationFramework = false;

                if (frames != null)
                {
                    foreach (var frame in frames)
                    {
                        var method = frame.GetMethod();
                        if (method?.DeclaringType?.Assembly.GetName().Name == "PresentationFramework")
                        {
                            isPresentationFramework = true;
                            break;
                        }
                    }
                }

                if (isPresentationFramework)
                {
                    Log.Error(ex, "[PRESENTATION_FRAMEWORK] ArgumentException in PresentationFramework.dll - Message: {Message}, ParamName: {ParamName}",
                        argEx.Message, argEx.ParamName ?? "(null)");

                    // Log detailed diagnostic information
                    Log.Error("[PRESENTATION_FRAMEWORK] This typically indicates:");
                    Log.Error("  - Invalid XAML resource key (StaticResource/DynamicResource not found)");
                    Log.Error("  - Duplicate resource keys in merged dictionaries");
                    Log.Error("  - Invalid pack:// URI in ResourceDictionary.Source");
                    Log.Error("  - Type mismatch in XAML property binding");
                    Log.Error("[PRESENTATION_FRAMEWORK] Check App.xaml merged dictionaries and resource files");

                    // Log current merged dictionaries for diagnostics
                    try
                    {
                        if (Application.Current?.Resources?.MergedDictionaries != null)
                        {
                            Log.Debug("[PRESENTATION_FRAMEWORK] Current merged dictionaries ({Count}):",
                                Application.Current.Resources.MergedDictionaries.Count);
                            for (int i = 0; i < Application.Current.Resources.MergedDictionaries.Count; i++)
                            {
                                var dict = Application.Current.Resources.MergedDictionaries[i];
                                Log.Debug("  [{Index}] Source: {Source}, Keys: {KeyCount}",
                                    i, dict.Source?.ToString() ?? "(inline)", dict.Keys.Count);
                            }
                        }
                    }
                    catch (Exception diagEx)
                    {
                        Log.Warning(diagEx, "[PRESENTATION_FRAMEWORK] Could not log merged dictionaries diagnostic info");
                    }

                    return true; // Mark as handled to prevent crash
                }
            }
            return false;
        }

        #endregion
    }
}
