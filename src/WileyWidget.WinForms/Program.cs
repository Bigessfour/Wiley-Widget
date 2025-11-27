using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;
using Syncfusion.Licensing;
using System;
using System.Windows.Forms;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var builder = Host.CreateApplicationBuilder(args);  // ✅ MCP-validated for .NET 9

                // 🔑 Syncfusion License (check UserSecrets, then env var)
                builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());
                // Attempt to register license from configuration or environment variable. Do not fail startup when license is absent — allow trial mode.
                WileyWidget.WinForms.Services.LicenseHelper.TryRegisterSyncfusionLicense(builder.Configuration, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

                // Serilog configuration - read settings from appsettings.json and configuration
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .Enrich.FromLogContext()
                    .CreateLogger();

                // Attach Serilog to DI so DI-resolved loggers are wired up
                // HostApplicationBuilder doesn't expose .Host in this runtime; add Serilog through AddLogging
                // Attach Serilog to the host so DI-resolved loggers are wired up and Serilog is the primary logger
                // NOTE: intentionally not attaching Serilog as a DI logging provider here to avoid SDK/API conflicts
                // If you want DI-resolved Microsoft.Extensions logging backed by Serilog, enable UseSerilog / AddSerilog after checking runtime SDK compatibility.
                // For CI and tests we rely on `Log.Logger` for structured logging instead of wiring the provider here.

                // 💉 DI - ✅ MCP-validated patterns
                // Use existing DependencyInjection configuration
                var diServices = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();
                foreach (var descriptor in diServices)
                {
                    builder.Services.Add(descriptor);
                }

                // Configure and register HealthCheckConfiguration from appsettings if present
                var healthChecksSection = builder.Configuration.GetSection("HealthChecks");
                var healthConfig = healthChecksSection.Get<HealthCheckConfiguration>() ?? new HealthCheckConfiguration();
                builder.Services.AddSingleton(healthConfig);

                var host = builder.Build();

                Services = host.Services;  // Make services available statically for forms

                // If an ErrorReportingService exists, suppress user dialogs on startup (we prefer logging for automated runs)
                try
                {
                    var errorReporting = Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService;
                    if (errorReporting != null)
                    {
                        errorReporting.SuppressUserDialogs = true;
                    }
                }
                catch
                {
                    // ignore - best effort
                }

                // Support a lightweight verify mode used by CI / automated smoke tests. When the app is launched with --verify-startup
                // we start the host to run hosted startup services (StartupOrchestrator) and then exit without starting the UI.
                if (args != null && Array.Exists(args, a => string.Equals(a, "--verify-startup", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        Log.Information("Running in --verify-startup mode: starting host and verifying startup orchestration.");
                        host.StartAsync().GetAwaiter().GetResult();
                        Log.Information("Host started successfully (verify mode). Stopping host and exiting.");
                        host.StopAsync().GetAwaiter().GetResult();
                        Log.CloseAndFlush();
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex, "Verify-startup run failed");
                        Log.CloseAndFlush();
                        throw;
                    }
                }

                using var scope = host.Services.CreateScope();
                var dashboardVM = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DashboardViewModel>(scope.ServiceProvider);

                // Global error handling — log everything to Serilog / ErrorReportingService and do NOT show popup dialogs
                Application.ThreadException += (sender, e) => {
                    Log.Fatal(e.Exception, "Unhandled UI thread exception");
                    try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(e.Exception, "UI Thread Exception", showToUser: false); } catch { }
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                    var ex = e.ExceptionObject as Exception;
                    Log.Fatal(ex, "Unhandled AppDomain exception");
                    try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex ?? new InvalidOperationException("Unhandled domain exception"), "Domain exception", showToUser: false); } catch { }
                };

                // Start UI
                Log.Information("🚀 WileyWidget Dashboard starting...");
                ApplicationConfiguration.Initialize();
                using var dashboardForm = new DashboardForm(dashboardVM);
                Application.Run(dashboardForm);

                // Normal shutdown - flush logs
                Log.Information("Application exited normally.");
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                // Ensure startup failures are recorded
                try
                {
                    Log.Fatal(ex, "Application failed to start");
                }
                catch { /* swallow logging errors - avoid hiding original exception */ }
                finally
                {
                    Log.CloseAndFlush();
                }

                // Prefer structured logging and error reporting (no startup popups in automated/dev runs)
                try { (Services?.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex, "Startup Failure", showToUser: false); } catch { }
                Log.Fatal(ex, "Application failed to start (startup failure)");
                throw;
            }
        }
    }
}