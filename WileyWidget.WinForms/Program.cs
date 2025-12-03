using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Forms;
// using Syncfusion.Licensing; // Uncomment when you have a license key

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();  // Required for WinForms + .NET 9

            // Build the host — this is safe, nothing gets constructed yet
            var host = CreateHostBuilder().Build();

            // === RUN STARTUP DIAGNOSTICS ===
            // This verifies all services can be resolved before the app tries to use them
            RunStartupDiagnosticsAsync(host).Wait();

            // === Register Syncfusion License ===
            // var syncfusionKey = host.Services.GetRequiredService<IConfiguration>()["Syncfusion:LicenseKey"];
            // if (!string.IsNullOrEmpty(syncfusionKey))
            // {
            //     Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
            // }

            // Now we have a running message pump — safe to resolve MainForm + its dependencies
            // IMPORTANT: MainForm is Scoped, so resolve it within a service scope
            using (var scope = host.Services.CreateScope())
            {
                Application.Run(scope.ServiceProvider.GetRequiredService<MainForm>());
            }
        }

        /// <summary>
        /// Run startup diagnostics to catch DI configuration errors early
        /// </summary>
        private static async Task RunStartupDiagnosticsAsync(IHost host)
        {
            try
            {
                var diagnostics = host.Services.GetRequiredService<IStartupDiagnostics>();
                var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Startup");

                logger.LogInformation("═══════════════════════════════════════════════════════════");
                logger.LogInformation("Starting Wiley Widget Startup Diagnostics");
                logger.LogInformation("═══════════════════════════════════════════════════════════");

                var report = await diagnostics.RunDiagnosticsAsync();

                // Log the full report
                logger.LogInformation(report.ToString());

                if (!report.AllChecksPassed)
                {
                    logger.LogWarning("⚠ Some diagnostics checks failed. The application may not function correctly.");
                    foreach (var failure in report.Results.Where(r => !r.IsSuccess))
                    {
                        logger.LogError("  FAILED: {Service} - {Message}", failure.ServiceName, failure.Message);
                        if (failure.Exception != null)
                        {
                            logger.LogError(failure.Exception, "Exception details for {Service}", failure.ServiceName);
                        }
                    }
                    // Optionally, you could throw here to prevent startup if critical services fail
                    // throw new InvalidOperationException("Startup diagnostics detected critical issues");
                }
                else
                {
                    logger.LogInformation("✓ All startup diagnostics checks passed!");
                }

                logger.LogInformation("═══════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Startup");
                logger.LogError(ex, "Failed to run startup diagnostics");
                throw;
            }
        }

        private static IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext();
                })
                .ConfigureServices((context, services) =>
                {
                    // Register all WileyWidget services
                    DependencyInjection.ConfigureServices(services, context.Configuration);

                    // === REGISTER STARTUP DIAGNOSTICS ===
                    services.AddSingleton<IStartupDiagnostics, StartupDiagnostics>();
                });
        }
    }
}
