using System;
using System.Windows.Forms;
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
            // Build configuration — use single production-style appsettings.json (no env-specific files)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                // NOTE: Environment-specific appsettings are intentionally disabled — app runs in production-like mode only
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
                Log.Information("Enforcing production-only configuration (environment detection disabled).");

                // Ensure process uses PerMonitorV2 DPI mode (explicit runtime call in addition to manifest)
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                }
                catch
                {
                    // OS/.NET version might not support PerMonitorV2 - swallow and continue
                }

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
                    // Production-only behavior: require DB connectivity and migrations to succeed.

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
                            // Fail-fast in all modes — DB connectivity is required for production-like behavior
                            Log.Error("Unable to connect to the configured database. Application will stop — verify connectivity and credentials.");
                            throw new InvalidOperationException("Cannot connect to configured database — aborting startup.");
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
                                    // Seed demo data only when the real DB is empty (useful for new deployments/tests)
                                    Log.Information("Database is empty — seeding initial demo data.");
                                    DemoDataSeeder.SeedDemoData(db);
                                }

                                Log.Information("Database ready — accounts: {count}", db.MunicipalAccounts.Count());
                            }
                            catch (InvalidOperationException invEx) when ((invEx.Message != null && invEx.Message.IndexOf("PendingModelChangesWarning", StringComparison.OrdinalIgnoreCase) >= 0) || invEx.Message?.IndexOf("pending changes", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // Detected a model/snapshot mismatch. By default this is fatal so production fails fast and we don't
                                // attempt dangerous automatic schema changes at runtime. For development machines we provide an
                                // opt-in escape hatch so contributors can run the UI even when new migrations haven't been applied.
                                Log.Error(invEx, "Detected model snapshot mismatch (pending EF model changes) — automatic migration cannot be applied at runtime.");
                                Log.Information("If you changed the EF model, create a migration and apply it instead of modifying the database at runtime. Example (from repo root):\n  dotnet ef migrations add <Name> -p src/WileyWidget.Data -s WileyWidget.WinForms\n  dotnet ef database update -p src/WileyWidget.Data -s WileyWidget.WinForms");

                                // Allow non-fatal behaviour when a developer intentionally opts-in via environment variable
                                // or when DOTNET_ENVIRONMENT=Development. This keeps production safe while improving local DX.
                                var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                                var allowDevFallback = false;
                                if (!string.IsNullOrWhiteSpace(env) && env.Equals("Development", StringComparison.OrdinalIgnoreCase)) allowDevFallback = true;
                                // Explicit override env var — safe to use on developer machines or CI test nodes.
                                if (Environment.GetEnvironmentVariable("WW_IGNORE_PENDING_MODEL_CHANGES")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true) allowDevFallback = true;

                                if (allowDevFallback)
                                {
                                    Log.Warning("Running in Development/override mode — skipping runtime migration error and continuing startup. THIS IS DEVELOPMENT-ONLY behaviour.");
                                }
                                else
                                {
                                    // Production-only: do not fall back to in-memory DB. Let the exception bubble so startup fails predictably.
                                    throw;
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

                // Initialize theme system earlier so Syncfusion skins and icon preloads can run before UI creation
                try
                {
                    WileyWidget.WinForms.Theming.ThemeManager.Initialize();

                    // Attempt to preload a small set of frequently used icons so theme-aware caching is warmed.
                    var iconService = Services.GetService(typeof(WileyWidget.WinForms.Services.IThemeIconService)) as WileyWidget.WinForms.Services.IThemeIconService;
                    iconService?.Preload(new string[] { "add", "edit", "delete", "save", "dismiss", "home", "settings", "search", "refresh", "play", "share", "question" }, WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme, 24);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Non-fatal: theme initialization / icon preloading failed at startup.");
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
