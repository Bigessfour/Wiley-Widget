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

            // Initialize Serilog early so any startup logs have a sink.
            // Use explicit programmatic configuration rather than ReadFrom.Configuration
            // to avoid Serilog.Settings.Configuration scanning all loaded assemblies
            // (which can fail when transitive native/runtime assets are excluded).
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console()
                .WriteTo.File("logs/wileywidget-.log", rollingInterval: Serilog.RollingInterval.Day, retainedFileCountLimit: 7, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("=== Wiley Widget WinForms Starting ===");
                Log.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production");

                ApplicationConfiguration.Initialize();

                // Configure DI with configuration
                Services = DependencyInjection.ConfigureServices(configuration);

                // Initialize telemetry & diagnostics services (if available)
                try
                {
                    var telemetry = Services.GetService(typeof(WileyWidget.Services.Abstractions.ITelemetryService)) as WileyWidget.Services.Abstractions.ITelemetryService;
                    // Concrete SigNoz fallback has Initialize() — call only if present
                    var sig = Services.GetService(typeof(WileyWidget.Services.Telemetry.SigNozTelemetryService)) as WileyWidget.Services.Telemetry.SigNozTelemetryService;
                    sig?.Initialize();

                    var errorReporter = Services.GetService(typeof(WileyWidget.Services.ErrorReportingService)) as WileyWidget.Services.ErrorReportingService;

                    // Wire global exception handlers so unhandled errors reach our central reporter + logger
                    Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);

                    Application.ThreadException += (sender, e) =>
                    {
                        try
                        {
                            Log.Fatal(e.Exception, "Unhandled UI thread exception");
                            errorReporter?.ReportError(e.Exception, "Unhandled UI thread exception", showToUser: true);
                        }
                        catch (Exception ex)
                        {
                            Log.Fatal(ex, "Error reporting UI thread exception");
                        }
                    };

                    AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    {
                        try
                        {
                            var ex = e.ExceptionObject as Exception ?? new InvalidOperationException("Unknown domain unhandled exception");
                            Log.Fatal(ex, "Unhandled application domain exception");
                            errorReporter?.ReportError(ex, "Unhandled domain exception", showToUser: true);
                        }
                        catch (Exception ex2)
                        {
                            Log.Fatal(ex2, "Error reporting domain unhandled exception");
                        }
                    };

                    TaskScheduler.UnobservedTaskException += (sender, e) =>
                    {
                        try
                        {
                            Log.Error(e.Exception, "Unobserved task exception");
                            errorReporter?.ReportError(e.Exception, "UnobservedTaskException", showToUser: false);
                            e.SetObserved();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error reporting unobserved task exception");
                        }
                    };
                }
                catch (Exception ex)
                {
                    // Create a best-effort log when diagnostics cannot be initialized
                    Log.Warning(ex, "Failed to initialize telemetry or global exception handlers (non-fatal)");
                }

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
                    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

                    if (db != null)
                    {
                        Log.Information("Checking database state and applying migrations (if any)...");

                        // Do a pre-check for connectivity — Common failure mode in developer machines is a missing/locked DB
                        var canConnect = false;
                        try
                        {
                            canConnect = db.Database.CanConnect();
                        }
                        catch (Exception connEx)
                        {
                            // Connection check failed (login/database missing, etc.) — will handle below
                            Log.Debug(connEx, "Database connectivity check failed while determining migration path.");
                        }

                        if (!canConnect)
                        {
                            Log.Warning("Cannot connect to the configured DB for '{Env}'. Will skip automatic migrations.", environment);

                            // In Development allow a deterministic fallback to an in-memory DB so the UI can still start
                            if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
                            {
                                Log.Information("Development environment detected — switching AppDbContext to in-memory fallback to allow UI startup.");

                                // Dispose the current provider and rebuild services with in-memory DB
                                if (Services is IDisposable d) d.Dispose();
                                Services = DependencyInjection.ConfigureServices(configuration, forceInMemory: true);

                                using var fallbackScope = Services.CreateScope();
                                var fallbackDb = fallbackScope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
                                if (fallbackDb != null)
                                {
                                    if (!fallbackDb.MunicipalAccounts.Any())
                                    {
                                        Log.Information("Seeding demo data into in-memory fallback DB.");
                                        DemoDataSeeder.SeedDemoData(fallbackDb);
                                    }
                                }

                                Log.Information("Using in-memory DB fallback; continuing startup.");
                            }
                            else
                            {
                                Log.Error("Unable to connect to DB in non-development environment. Please verify connectivity / credentials.");
                            }
                        }
                        else
                        {
                            // We are able to reach the DB — attempt to apply migrations. If there's a model/migration mismatch
                            // EF will throw an InvalidOperationException with PendingModelChangesWarning.
                            try
                            {
                                db.Database.Migrate();

                                if (!db.MunicipalAccounts.Any())
                                {
                                    Log.Warning("Database appears empty — seeding demo data for development/testing.");
                                    DemoDataSeeder.SeedDemoData(db);
                                }

                                Log.Information("Database ready — accounts: {count}", db.MunicipalAccounts.Count());
                            }
                            catch (InvalidOperationException invEx) when (invEx.Message?.Contains("PendingModelChangesWarning") == true || invEx.Message?.IndexOf("pending changes", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // Clear guidance for contributors and allow dev fallback.
                                Log.Error(invEx, "Detected model snapshot mismatch (pending EF model changes) — automatic migration cannot be applied at runtime.");
                                Log.Information("If you changed the EF model, create a migration and apply it instead of modifying the database at runtime. Example (from repo root):\n  dotnet ef migrations add <Name> -p src/WileyWidget.Data -s WileyWidget.WinForms\n  dotnet ef database update -p src/WileyWidget.Data -s WileyWidget.WinForms");

                                if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Fall back to in-memory so UI still works for dev/debug
                                    Log.Information("Falling back to in-memory DB for Development environment so the app can continue.");
                                    if (Services is IDisposable d2) d2.Dispose();
                                    Services = DependencyInjection.ConfigureServices(configuration, forceInMemory: true);

                                    using var fallbackScope = Services.CreateScope();
                                    var fallbackDb = fallbackScope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
                                    if (fallbackDb != null && !fallbackDb.MunicipalAccounts.Any())
                                    {
                                        DemoDataSeeder.SeedDemoData(fallbackDb);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Generic migration / DB issue
                                Log.Error(ex, "Database initialization failed. Application will continue if possible.");
                            }
                        }
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
