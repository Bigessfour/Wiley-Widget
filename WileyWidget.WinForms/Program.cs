using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WileyWidget.WinForms.Configuration;  // DI + DemoDataSeeder
using WileyWidget.WinForms.Forms;
using WileyWidget.Data;

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;

        [STAThread]
        static void Main()
        {
            // Build configuration — read appsettings + environment
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Initialize Serilog early so any startup logs have a sink
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            try
            {
                Log.Information("=== Wiley Widget WinForms Starting ===");
                Log.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production");

                ApplicationConfiguration.Initialize();

                // Configure DI with configuration
                Services = DependencyInjection.ConfigureServices(configuration);

                // Register Syncfusion license KEY per official guidance BEFORE any Syncfusion control is created
                var loggerFactory = Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                var logger = loggerFactory?.CreateLogger("Program");

                string? licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    try
                    {
                        var vault = Services.GetService(typeof(WileyWidget.Services.ISecretVaultService)) as WileyWidget.Services.ISecretVaultService;
                        licenseKey = vault?.GetSecret("SyncfusionLicenseKey");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogDebug(ex, "Secret vault lookup for Syncfusion license key failed (non-fatal).");
                    }
                }

                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    try
                    {
                        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                        logger?.LogInformation("Syncfusion license registered successfully.");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to register Syncfusion license key (controls may throw licensing warnings).");
                    }
                }
                else
                {
                    logger?.LogInformation("No Syncfusion license key found (env: SYNCFUSION_LICENSE_KEY or secret 'SyncfusionLicenseKey'). Running without registration.");
                }

                // Ensure DB is migrated and seeded
                try
                {
                    using var scope = Services.CreateScope();
                    var db = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
                    if (db != null)
                    {
                        Log.Information("Checking database state and applying migrations (if any)...");
                        db.Database.Migrate();

                        if (!db.MunicipalAccounts.Any())
                        {
                            Log.Warning("Database appears empty — seeding demo data for development/testing.");
                            DemoDataSeeder.SeedDemoData(db);
                        }

                        Log.Information("Database ready — accounts: {count}", db.MunicipalAccounts.Count());
                    }
                    else
                    {
                        Log.Warning("AppDbContext not registered; skipping DB migration/seed.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Database initialization failed. Application will continue if possible.");
                }

                // Launch main form
                var mainForm = Services.GetService(typeof(MainForm)) as MainForm;
                if (mainForm == null) throw new InvalidOperationException("MainForm not found in IServiceProvider");
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                System.Windows.Forms.MessageBox.Show($"Fatal error: {ex.Message}", "Startup Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
