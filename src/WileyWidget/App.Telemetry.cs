// App.Telemetry.cs - Application Telemetry Partial Class
// Contains: SigNoz telemetry initialization and integration methods
// Part of App.xaml.cs partial class split for maintainability

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;

namespace WileyWidget
{
    public partial class App
    {
        #region Telemetry

        /// <summary>
        /// Initializes SigNoz telemetry for unified observability (logs, traces, metrics).
        /// Called early in startup to enable telemetry tracking of startup performance.
        /// </summary>
        private void InitializeSigNozTelemetry()
        {
            try
            {
                Log.Information("Initializing SigNoz telemetry service");

                // Build configuration early for telemetry setup
                var config = BuildConfiguration();

                // Create a temporary logger for telemetry service initialization
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
                var logger = loggerFactory.CreateLogger<SigNozTelemetryService>();

                // Initialize telemetry service
                var telemetryService = new SigNozTelemetryService(logger, config);
                telemetryService.Initialize();

                // Store for later registration in DI container
                _earlyTelemetryService = telemetryService;

                Log.Information("✅ SigNoz telemetry initialized - tracking startup performance");

                // Start tracking the overall application startup
                _startupActivity = SigNozTelemetryService.ActivitySource.StartActivity("application.startup");
                _startupActivity?.SetTag("app.version", SigNozTelemetryService.ServiceVersion);
                _startupActivity?.SetTag("startup.phase", "early_initialization");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Failed to initialize SigNoz telemetry - continuing without distributed tracing");
                // Don't fail startup if telemetry fails
            }
        }

        /// <summary>
        /// Integrates SigNoz telemetry service with ErrorReportingService for unified observability.
        /// Called after DI container is fully configured.
        /// </summary>
        private void IntegrateTelemetryServices()
        {
            try
            {
                var errorReportingService = this.Container.Resolve<ErrorReportingService>();
                var telemetryService = this.Container.Resolve<SigNozTelemetryService>();

                // Connect telemetry service to error reporting
                errorReportingService.SetTelemetryService(telemetryService);

                Log.Information("✅ SigNoz telemetry integrated with ErrorReportingService");

                // Validate telemetry connectivity
                var isConnected = telemetryService.ValidateConnectivity();
                if (!isConnected)
                {
                    Log.Warning("⚠️ SigNoz connectivity validation failed - telemetry may be degraded");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to integrate telemetry services - continuing without enhanced observability");
            }
        }

        #endregion
    }
}
