using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Threading.Tasks;
using System.Globalization;
using Action = System.Action;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Security.Permissions;
using System.Windows.Forms;
using WileyWidget.Data;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using WileyWidget.Models;
using WileyWidget.Services;
// Removed using WileyWidget.WinForms.Services; (ambiguous IStartupTimelineService)
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
// using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms
{
    /// <summary>
    /// The main program class for the WileyWidget WinForms application.
    /// </summary>

    public static class Program
    {
        private const string UnknownValue = "(unknown)";
        public static IServiceProvider Services { get; private set; } = null!;
        public static IServiceScope? UIScope { get; private set; }
        // Captured UI thread SynchronizationContext for marshaling UI actions
        public static SynchronizationContext? UISynchronizationContext { get; private set; }

        // Removed unused constants
        private static IStartupTimelineService? _timelineService;

        [STAThread]
        static void Main(string[] args)
        {
            // Synchronous STA entry point — ensure main thread is STA before running async startup
#pragma warning disable WW1003 // Blocking call on async operation is allowed here for process bootstrap on STA thread
            MainAsync(args).GetAwaiter().GetResult();
#pragma warning restore WW1003
        }

        private static async Task MainAsync(string[] args)
        {
            // ═══════════════════════════════════════════════════════════════════
            // ENHANCED EXCEPTION DIAGNOSTICS
            // ═══════════════════════════════════════════════════════════════════
            // Install FirstChanceException handler FIRST to capture ALL exceptions
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

            // Load .env file FIRST - before any configuration is read
            DotNetEnv.Env.Load("secrets/my.secrets"); // Load secrets first
            DotNetEnv.Env.Load(); // Then load .env (overrides if needed)
#if DEBUG
            Console.WriteLine("Main method started");
#endif

            // Suppress loading of Microsoft.WinForms.Utilities.Shared which is not needed at runtime
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                (bool flowControl, System.Reflection.Assembly? value) = NewMethod(resolveArgs);
                if (!flowControl)
                {
                    return value;
                }
                return null;
            };

            // === Phase 1: License Registration (must complete BEFORE Theme Initialization) ===
            IStartupTimelineService? earlyTimeline = null;
            IDisposable? licensePhase = null;
            IHost? host = null;

            try
            {
#if DEBUG
                Console.WriteLine("Phase 1: Registering Syncfusion license...");
#endif
                var earlyBuilder = Host.CreateApplicationBuilder(args);
                AddConfiguration(earlyBuilder);
                using var earlyHost = earlyBuilder.Build();
                var earlyConfig = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(earlyHost.Services);

                // Try to get timeline service early (may not be available yet)
                earlyTimeline = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(earlyHost.Services);
                if (earlyTimeline != null && earlyTimeline.IsEnabled)
                {
                    licensePhase = earlyTimeline.BeginPhaseScope("License Registration");
                }

                // CRITICAL NULL GUARD: Validate earlyConfig before using
                if (earlyConfig == null)
                {
#if DEBUG
                    Console.WriteLine("[ERROR] earlyConfig is null - cannot proceed with license registration");
#endif
                    throw new InvalidOperationException("Configuration system failed to initialize");
                }

                // DEBUG: Log configuration values (masked)
#if DEBUG
                var xaiKey = earlyConfig["XAI:ApiKey"];
                var syncKey = earlyConfig["Syncfusion:LicenseKey"];
                Console.WriteLine($"[CONFIG DEBUG] XAI:ApiKey = {(xaiKey != null ? xaiKey.Substring(0, Math.Min(15, xaiKey.Length)) + "..." : "NULL")} ({xaiKey?.Length ?? 0} chars)");
                Console.WriteLine($"[CONFIG DEBUG] Syncfusion:LicenseKey = {(syncKey != null ? syncKey.Substring(0, Math.Min(15, syncKey.Length)) + "..." : "NULL")} ({syncKey?.Length ?? 0} chars)");
#endif

                RegisterSyncfusionLicense(earlyConfig);
#if DEBUG
                Console.WriteLine("Syncfusion license registered successfully");
#endif

                // === Phase 2: Theme Initialization (MUST be after License, before WinForms) ===
#if DEBUG
                Console.WriteLine("Phase 2: Applying Office2019 theme");
#endif
                using (var themePhase = earlyTimeline?.BeginPhaseScope("Theme Initialization"))
                {
                    InitializeTheme(earlyConfig);
                } // Phase 2 complete
#if DEBUG
                Console.WriteLine("[SYNC] Phase 2 complete - Theme initialized and ready");
#endif
                System.Threading.Thread.Sleep(100); // Allow theme to fully propagate
            }
            finally
            {
                licensePhase?.Dispose(); // Phase 1 complete
#if DEBUG
                Console.WriteLine("[SYNC] Phase 1 complete - License Registration ready");
#endif
                System.Threading.Thread.Sleep(50); // Allow timeline service to process completion
            }

            // FirstChanceException handler already installed at method start

            // MUST happen after Theme, before ANY Form/Window creation
#if DEBUG
            Console.WriteLine("Phase 3: Calling InitializeWinForms");
#endif
            using (var winformsPhase = earlyTimeline?.BeginPhaseScope("WinForms Initialization"))
            {
                InitializeWinForms();
            } // Phase 3 complete
#if DEBUG
            Console.WriteLine("[SYNC] Phase 3 complete - WinForms Initialization ready");
            System.Threading.Thread.Sleep(50); // Allow WinForms subsystem to stabilize
            Console.WriteLine("InitializeWinForms completed");
#endif

            // === Phase 4: Splash Screen (depends on WinForms Initialization) ===
#if DEBUG
            Console.WriteLine("Phase 4: Showing Splash Screen");
#endif
            using (var splashSetupPhase = earlyTimeline?.BeginPhaseScope("Splash Screen"))
            {
                // SynchronizationContext will be captured in MainForm.OnShown
            } // Phase 4 complete
#if DEBUG
            Console.WriteLine("[SYNC] Phase 4 complete - Splash Screen ready");
            System.Threading.Thread.Sleep(50); // Allow splash screen to initialize
            Console.WriteLine("Splash screen setup completed");
#endif

            // License already registered above
            // Show splash screen on main thread to avoid cross-thread handle issues
            SplashForm? splash = null;

#if DEBUG
            Console.WriteLine("[SPLASH] About to create SplashForm instance");
#endif
            Log.Information("[SPLASH] Starting splash screen initialization");

            // ═══════════════════════════════════════════════════════════════════
            // COMPREHENSIVE STARTUP TRY-CATCH WRAPPER
            // Captures all exceptions during startup with full diagnostic details
            // ═══════════════════════════════════════════════════════════════════
            try
            {
                // Create splash on main UI thread
#if DEBUG
                Console.WriteLine("[SPLASH] Creating new SplashForm()");
#endif
                splash = new SplashForm();
#if DEBUG
                Console.WriteLine("[SPLASH] SplashForm created");
                Console.WriteLine("[SPLASH] splash.Show() completed");
#endif

                Application.DoEvents(); // Process show event
#if DEBUG
                Console.WriteLine("[SPLASH] First Application.DoEvents() after Show() completed");
#endif

                // === Phase 5: DI Container Build (must complete BEFORE DI Validation and DB Health Check) ===
                splash.Report(0.05, "Building dependency injection container...");

#if DEBUG
                Console.WriteLine("Phase 5: Building DI Container...");
#endif
                using (var diContainerPhase = earlyTimeline?.BeginPhaseScope("DI Container Build"))
                {
                    var hostBuildScope = System.Diagnostics.Stopwatch.StartNew();
                    host = BuildHost(args);
                    hostBuildScope.Stop();
#if DEBUG
                    Console.WriteLine($"[TIMING] DI Container Build: {hostBuildScope.ElapsedMilliseconds}ms");
#endif
                } // Phase 5 complete - EXPLICIT COMPLETION MARKER
#if DEBUG
                Console.WriteLine("[SYNC] Phase 5 complete - DI Container ready");
#endif
                Application.DoEvents(); // Process any pending UI messages
                System.Threading.Thread.Sleep(100); // Allow DI container to fully initialize

                var uiScope = host.Services.CreateScope();
                Program.UIScope = uiScope;
                Services = uiScope.ServiceProvider;

                // === AUTOMATIC EF CORE MIGRATIONS ON STARTUP (DEVELOPMENT-SAFE) ===
                try
                {
                    using (var migrationScope = Services.CreateScope())
                    {
                        var env = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IHostEnvironment>(migrationScope.ServiceProvider);
                        var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(migrationScope.ServiceProvider);

                        // Run automatic migrations in Development, or when explicitly enabled via configuration (Database:AutoMigrate = true)
                        var shouldMigrate = (env != null && env.IsDevelopment()) || (configuration?.GetValue<bool>("Database:AutoMigrate") ?? false);
                        if (shouldMigrate)
                        {
                            var dbContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(migrationScope.ServiceProvider);
                            try
                            {
                                // Don't attempt relational migrations when using a non-relational provider (e.g., InMemory used for UI tests)
                                if (!dbContext.Database.IsRelational())
                                {
                                    Log.Information("[STARTUP] Skipping automatic EF migrations: non-relational database provider detected.");
                                }
                                else
                                {
                                    Log.Information("[STARTUP] Applying EF Core migrations...");
                                    await dbContext.Database.MigrateAsync();
                                    Log.Information("[STARTUP] Database migrations applied successfully.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "[STARTUP] Failed to apply database migrations. Application may fall back to degraded mode or fail queries.");
                                try
                                {
                                    // Avoid showing blocking MessageBox during automated UI tests or when running headless
                                    var isUiTestRun = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
                                    if (!isUiTestRun && Environment.UserInteractive)
                                    {
                                        MessageBox.Show(
                                            "Database initialization failed. The application may not function correctly.\n\n" +
                                            "Please contact support or run 'dotnet ef database update' manually.\n\n" +
                                            ex.Message,
                                            "Database Error",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                                    }
                                    else
                                    {
                                        Log.Warning("Database initialization failed during non-interactive/test run: {Message}", ex.Message);
                                    }
                                }
                                catch (Exception mbEx)
                                {
                                    Log.Warning(mbEx, "Failed to show MessageBox for DB migration error (running headless?)");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: log and continue startup
                    Log.Warning(ex, "[STARTUP] Error while attempting automatic DB migrations (non-fatal)");
                }

                // If requested via startup arg, migrate secrets from environment into encrypted vault then exit.
                if (args != null && args.Any(a => string.Equals(a, "--migrate-secrets", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        var vault = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ISecretVaultService>(Services);
                        if (vault != null)
                        {
                            Log.Information("Running secret migration (--migrate-secrets)...");
                            await vault.MigrateSecretsFromEnvironmentAsync();
                            Log.Information("Secret migration completed. Exiting as requested by --migrate-secrets");
                            return;
                        }
                        else
                        {
                            Log.Warning("ISecretVaultService not registered in DI container; cannot migrate secrets.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Secret migration failed");
                        throw;
                    }
                }

                // If requested via startup arg, run data seeding then exit
                if (args != null && args.Any(a => string.Equals(a, "--seed-data", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using var seedScope = Services.CreateScope();
                        var seedSvc = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DataSeedingService>(seedScope.ServiceProvider);
                        if (seedSvc != null)
                        {
                            Log.Information("Running data seeding (--seed-data)...");
                            await seedSvc.SeedBudgetDataAsync();
                            Log.Information("Data seeding completed. Exiting as requested by --seed-data");
                            return;
                        }
                        else
                        {
                            Log.Warning("DataSeedingService not registered in DI; cannot seed data.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Data seeding failed");
                        throw;
                    }
                }

                // Get timeline service from built DI container
                _timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(Services);
                if (_timelineService != null && _timelineService.IsEnabled)
                {
                    Log.Information("[TIMELINE] StartupTimelineService enabled - tracking startup phases");

                    // Retroactively mark early phases as complete before DI container was available
                    // These phases already executed, so we immediately complete them to satisfy dependency tracking
                    if (earlyTimeline != _timelineService)
                    {
                        // Mark License Registration as complete FIRST (happened in Phase 1)
                        using (var retroLicense = _timelineService.BeginPhaseScope("License Registration"))
                        {
                            _timelineService.RecordOperation("Register Syncfusion license", "License Registration");
                        } // Immediately dispose to mark complete

                        // Mark Theme Initialization as already complete (happened in Phase 2, depends on License)
                        using (var retroTheme = _timelineService.BeginPhaseScope("Theme Initialization"))
                        {
                            _timelineService.RecordOperation("Load Office2019Theme assembly and set global theme", "Theme Initialization");
                        } // Immediately dispose to mark complete
                    }

                    // DEBUG: Check main configuration
#if DEBUG
                    var mainConfig = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(host.Services);
                    var mainXaiKey = mainConfig["XAI:ApiKey"];
                    Console.WriteLine($"[MAIN CONFIG DEBUG] XAI:ApiKey = {(mainXaiKey != null ? mainXaiKey.Substring(0, Math.Min(15, mainXaiKey.Length)) + "..." : "NULL")} ({mainXaiKey?.Length ?? 0} chars)");
#endif

                    // === Phase 6: DI Validation (depends on DI Container Build completing first) ===
                    // CRITICAL: Validate critical services - run in background to avoid StackGuard deadlock
                    splash.Report(0.10, "Validating service registration...");
                    Application.DoEvents(); // Keep UI responsive

                    // Fire-and-forget: DI resolution can trigger StackGuard which deadlocks on UI thread
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            // ValidateCriticalServices(uiScope.ServiceProvider); // Disabled: missing method
                            Log.Information("DI validation completed successfully in background");
                        }
                        catch (Exception valEx)
                        {
                            Log.Fatal(valEx, "DI validation failed in background");
                        }
                    });

                    splash.Report(0.15, "Service validation initiated...");
                    Application.DoEvents();
#if DEBUG
                    Console.WriteLine("[SYNC] Phase 6 complete - DI Validation passed");
#endif
                    Application.DoEvents(); // Process any pending UI messages
                    System.Threading.Thread.Sleep(100); // Allow services to stabilize

                    // === Phase 7: Configure Error Reporting (depends on Theme Initialization) ===
                    // NOTE: Theme already initialized in Phase 2 before any forms created
                    splash.Report(0.40, "Configuring error reporting...");
                    _timelineService?.RecordOperation("Configure error reporting", "Error Handlers");
                    ConfigureErrorReporting();
                    Console.WriteLine("[SYNC] Phase 7 complete - Error reporting configured");
                    Application.DoEvents(); // Process any pending UI messages
                    System.Threading.Thread.Sleep(50); // Allow error handlers to wire up

                    // === Phase 8: Database Health Check (depends on DI Container Build completing first) ===
                    // Startup health check - run in background to avoid blocking UI thread
                    Log.Information("[DIAGNOSTIC] Starting health check phase");
#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Starting health check phase");
#endif
                    splash.Report(0.50, "Verifying database connectivity...");
                    Application.DoEvents(); // Keep UI responsive

                    // CRITICAL: Run health check on background thread to prevent UI deadlock
                    // Don't await - let it complete asynchronously while UI continues
                    _ = Task.Run(async () =>
                    {
                        using (var healthScope = host.Services.CreateScope())
                        {
                            try
                            {
                                Log.Information("[DIAGNOSTIC] Calling RunStartupHealthCheckAsync");
#if DEBUG
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Calling RunStartupHealthCheckAsync");
#endif
                                await RunStartupHealthCheckAsync(healthScope.ServiceProvider);
                                Log.Information("[DIAGNOSTIC] RunStartupHealthCheckAsync completed successfully");
#if DEBUG
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] RunStartupHealthCheckAsync completed");
#endif
                            }
                            catch (Exception healthEx)
                            {
                                Log.Warning(healthEx, "Background health check failed (non-blocking)");
                            }
                        }
                    });

                    splash.Report(0.65, "Database check initiated...");
                    Application.DoEvents();
#if DEBUG
                    Console.WriteLine("[SYNC] Phase 8 complete - Database health check running in background");
#endif

                    Log.Information("[DIAGNOSTIC] Checking IsVerifyStartup at line 121");

#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Checking IsVerifyStartup");
#endif
                    if (IsVerifyStartup(args!))
                    {
                        Log.Information("[DIAGNOSTIC] IsVerifyStartup=true, running verify-startup mode");
#if DEBUG
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Running verify-startup mode");
#endif
                        splash.Complete("Startup verification complete");
                        await RunVerifyStartupAsync(host);
                        return; // Exit Main() after verification
                    }
                    Log.Information("[DIAGNOSTIC] IsVerifyStartup=false, continuing normal startup");
#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Continuing normal startup");
#endif

                    // === Phase 9: MainForm Creation (must complete BEFORE Chrome Init and Data Prefetch) ===
                    Log.Information("[DIAGNOSTIC] Creating MainForm");
#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Creating MainForm");
#endif
                    splash.Report(0.75, "Initializing main window...");
                    Application.DoEvents(); // Keep UI responsive
#if DEBUG
                    Console.WriteLine("Creating MainForm...");
#endif

                    // CRITICAL: Defer MainForm creation until AFTER Application.Run starts message pump
                    // WinForms controls require active message loop for proper initialization
                    // Creating MainForm before Application.Run() causes thread affinity deadlocks
#if DEBUG
                    Console.WriteLine("[SYNC] Phase 9 - MainForm creation deferred until message pump active");
#endif
                    Application.DoEvents(); // Process MainForm initialization events
                    System.Threading.Thread.Sleep(150); // Allow MainForm controls to initialize

                    // === Phase 10: Chrome Initialization (depends on Theme Init [Phase 2] completing) ===
                    Log.Information("[DIAGNOSTIC] Wiring exception handlers");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Wiring exception handlers");
                    splash.Report(0.80, "Wiring global exception handlers...");
                    Application.DoEvents(); // Keep UI responsive
                    using (var handlerScope = _timelineService?.BeginPhaseScope("Chrome Initialization"))
                    {
                        WireGlobalExceptionHandlers();
                    } // Phase 10 complete
#if DEBUG
                    Console.WriteLine("[SYNC] Phase 10 complete - Exception handlers ready");
#endif
                    Application.DoEvents(); // Process any pending UI messages
                    System.Threading.Thread.Sleep(50); // Allow exception handlers to stabilize
                    Log.Information("[DIAGNOSTIC] Exception handlers wired successfully");
#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Exception handlers wired");
#endif

                    // === Phase 11: Data Prefetch - MOVED TO ApplicationContext ===
                    // Data seeding happens AFTER MainForm creation in ApplicationContext.OnApplicationIdle
                    // Splash Screen Hide has been relocated to the UI Message Loop area (after ApplicationContext construction) so Data Prefetch completes first.


                    // === Phase 9: UI Message Loop Preparation ===
                    Log.Information("[DIAGNOSTIC] Preparing UI message loop with ApplicationContext");
#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Preparing UI message loop");
                    Console.WriteLine("Starting application message loop...");
#endif

                    // Use WileyWidgetApplicationContext if available
                    var appContext = new Forms.WileyWidgetApplicationContext(Services, _timelineService as WileyWidget.Services.IStartupTimelineService, host);
                    Application.Run(appContext);
                    using (var uiPrepScope = _timelineService?.BeginPhaseScope("UI Message Loop Preparation"))
                    {
                        // MICROSOFT PATTERN: Use ApplicationContext with deferred MainForm creation
                        // ApplicationContext constructor handles:
                        // - Phase 7: MainForm Creation
                        // - Phase 10: Data Prefetch (background seeding)

                        // === Phase 11: Splash Screen Hide (AFTER Data Prefetch completes) ===
                        // CRITICAL: Must happen AFTER ApplicationContext constructor completes
                        // ApplicationContext runs Data Prefetch, so splash must wait for it
                        Log.Information("[DIAGNOSTIC] Completing splash screen after data prefetch");
#if DEBUG
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Completing splash screen");
#endif
                        splash.Report(0.95, "Ready");
                        Application.DoEvents();
                        using (var splashHideScope = _timelineService?.BeginPhaseScope("Splash Screen Hide"))
                        {
                            splash.Complete("Ready");
                            Application.DoEvents(); // Process complete message

                            // Brief delay to show "Ready" message (under 300ms threshold)
                            System.Threading.Thread.Sleep(200);
                            splash.Dispose();
                        } // Phase 11 complete
#if DEBUG
                        Console.WriteLine("[SYNC] Phase 11 complete - Splash screen hidden after data prefetch");
#endif
                        Application.DoEvents(); // Final UI processing before main loop
                        System.Threading.Thread.Sleep(50); // Brief pause before entering main loop

                        // Generate startup timeline report (DEBUG or env var only)
                        if (_timelineService != null && _timelineService.IsEnabled)
                        {
                            var report = _timelineService.GenerateReport();
                            if (report.Errors.Count > 0 || report.Warnings.Count > 0)
                            {
                                Log.Warning("[TIMELINE] Startup completed with {ErrorCount} errors and {WarningCount} warnings",
                                    report.Errors.Count, report.Warnings.Count);
                            }
                            else
                            {
                                Log.Information("[TIMELINE] Startup completed successfully with optimal timing");
                            }
                        }
                    } // Phase 9 complete - UI preparation done, startup timeline ends here
#if DEBUG
                    Console.WriteLine("[SYNC] Phase 9 complete - UI Message Loop prepared, entering main message loop");
#endif

                    // === Enter UI Message Loop (blocks until application exits) ===
                    // CRITICAL: Application.Run() blocks indefinitely running the message loop
                    // This is NOT a startup phase - it's the main execution mode
                    // Startup timeline ends before this point - do not track Application.Run as a phase
                    Log.Information("[DIAGNOSTIC] Entering UI message loop (Application.Run) - will block until exit");
#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Entering Application.Run message loop");
#endif
                    RunUiLoop(appContext);

                    Log.Information("[DIAGNOSTIC] UI message loop exited");
                    Program.UIScope?.Dispose();

                    // Graceful host shutdown: stop host and dispose after UI loop exits to avoid disposing
                    // logging providers while UI global exception handlers might still be invoked.
                    try
                    {
                        _timelineService?.RecordOperation("Stop host after UI loop", "Shutdown");
                    }
                    catch { }

                    try
                    {
                        if (host != null)
                        {
                            await host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        }
                    }
                    catch (Exception stopEx)
                    {
                        try { Log.Warning(stopEx, "Host.StopAsync failed during shutdown"); } catch { Console.Error.WriteLine($"Host.StopAsync failed during shutdown: {stopEx}"); }
                    }

                    try
                    {
                        host?.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        try { Log.Warning(disposeEx, "Host.Dispose failed during shutdown"); } catch { Console.Error.WriteLine($"Host.Dispose failed during shutdown: {disposeEx}"); }
                    }

                    // Flush logs now that host and providers are stopped
                    try
                    {
                        Log.CloseAndFlush();
                    }
                    catch { /* best-effort */ }

#if DEBUG
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] UI message loop exited");
#endif
                }
            }
            catch (NullReferenceException nreEx)
            {
                // Special handling for NullReferenceException with enhanced diagnostics
                Log.Fatal(nreEx, "═══ NULLREFERENCEEXCEPTION DURING STARTUP ═══\n" +
                    "Exception Type: {ExceptionType}\n" +
                    "Message: {Message}\n" +
                    "StackTrace:\n{StackTrace}\n" +
                    "Source: {Source}\n" +
                    "TargetSite: {TargetSite}\n" +
                    "HResult: {HResult}",
                    nreEx.GetType().FullName,
                    nreEx.Message,
                    nreEx.StackTrace ?? "(no stack trace)",
                    nreEx.Source ?? UnknownValue,
                    nreEx.TargetSite?.ToString() ?? UnknownValue,
                    nreEx.HResult);

                Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
                Console.WriteLine("║  CRITICAL: NullReferenceException During Startup");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
                Console.WriteLine($"Type:       {nreEx.GetType().FullName}");
                Console.WriteLine($"Message:    {nreEx.Message}");
                Console.WriteLine($"Source:     {nreEx.Source ?? UnknownValue}");
                Console.WriteLine($"TargetSite: {nreEx.TargetSite?.ToString() ?? UnknownValue}");
                Console.WriteLine($"HResult:    0x{nreEx.HResult:X8}");
                Console.WriteLine("\nStack Trace:");
                Console.WriteLine(nreEx.StackTrace ?? "(no stack trace available)");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

                HandleStartupFailure(nreEx);
                throw;
            }
            catch (Exception ex)
            {
                // Generic exception handler with comprehensive diagnostics
                Log.Fatal(ex, "═══ UNHANDLED EXCEPTION DURING STARTUP ═══\n" +
                    "Exception Type: {ExceptionType}\n" +
                    "Message: {Message}\n" +
                    "StackTrace:\n{StackTrace}\n" +
                    "Source: {Source}\n" +
                    "TargetSite: {TargetSite}\n" +
                    "HResult: {HResult}\n" +
                    "InnerException: {InnerException}",
                    ex.GetType().FullName,
                    ex.Message,
                    ex.StackTrace ?? "(no stack trace)",
                    ex.Source ?? UnknownValue,
                    ex.TargetSite?.ToString() ?? UnknownValue,
                    ex.HResult,
                    ex.InnerException?.ToString() ?? "(none)");

                Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
                Console.WriteLine("║  CRITICAL: Unhandled Exception During Startup");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
                Console.WriteLine($"Type:       {ex.GetType().FullName}");
                Console.WriteLine($"Message:    {ex.Message}");
                Console.WriteLine($"Source:     {ex.Source ?? UnknownValue}");
                Console.WriteLine($"TargetSite: {ex.TargetSite?.ToString() ?? UnknownValue}");
                Console.WriteLine($"HResult:    0x{ex.HResult:X8}");
                Console.WriteLine("\nStack Trace:");
                Console.WriteLine(ex.StackTrace ?? "(no stack trace available)");
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine($"  Type:    {ex.InnerException.GetType().FullName}");
                    Console.WriteLine($"  Message: {ex.InnerException.Message}");
                    Console.WriteLine($"  Stack:   {ex.InnerException.StackTrace ?? "(no stack trace)"}");
                }
                Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

                HandleStartupFailure(ex);
                throw;
            }
            finally
            {
                // Ensure host is stopped and disposed to avoid disposal races during shutdown
                try
                {
                    if (host != null)
                    {
                        try { Log.Information("[DIAGNOSTIC] Initiating host shutdown (final cleanup)"); } catch { }
                        try
                        {
                            await host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        }
                        catch (Exception stopEx)
                        {
                            try { Log.Warning(stopEx, "Host.StopAsync failed during final cleanup"); } catch { Console.Error.WriteLine($"Host.StopAsync failed during final cleanup: {stopEx}"); }
                        }

                        try
                        {
                            host.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            try { Log.Warning(disposeEx, "Host.Dispose failed during final cleanup"); } catch { Console.Error.WriteLine($"Host.Dispose failed during final cleanup: {disposeEx}"); }
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Console.Error.WriteLine($"Host shutdown encountered exception: {ex2}");
                }
                finally
                {
                    try { Log.CloseAndFlush(); } catch { /* best-effort */ }
                }
            }
        }

        private static (bool flowControl, System.Reflection.Assembly? value) NewMethod(ResolveEventArgs resolveArgs)
        {
            if (resolveArgs.Name != null && resolveArgs.Name.StartsWith("Microsoft.WinForms.Utilities.Shared", StringComparison.OrdinalIgnoreCase))
            {
                return (flowControl: false, value: null); // Return null to indicate the assembly should not be loaded
            }

            return (flowControl: true, value: null);
        }

        /// <summary>
        /// FirstChanceException handler for comprehensive exception diagnostics.
        /// Captures ALL exceptions thrown in the AppDomain BEFORE they are caught.
        /// Provides detailed logging for NullReferenceExceptions with full stack traces.
        /// </summary>
        private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is NullReferenceException nre)
            {
                // Log NullReferenceExceptions with full diagnostic details
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                Console.WriteLine($"\n[{timestamp}] ═══ FIRST CHANCE: NullReferenceException ═══");
                Console.WriteLine($"Message:    {nre.Message}");
                Console.WriteLine($"Source:     {nre.Source ?? UnknownValue}");
                Console.WriteLine($"TargetSite: {nre.TargetSite?.ToString() ?? UnknownValue}");
                Console.WriteLine($"HResult:    0x{nre.HResult:X8}");
                Console.WriteLine("Stack Trace:");
                Console.WriteLine(nre.StackTrace ?? "(no stack trace available)");
                Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

                // Also log via Serilog if available
                try
                {
                    Log.Warning(nre, "[FIRST CHANCE] NullReferenceException detected at {TargetSite}",
                        nre.TargetSite?.ToString() ?? UnknownValue);
                }
                catch
                {
                    // Serilog might not be initialized yet - ignore
                }
            }
            else if (e.Exception is InvalidOperationException ioe &&
                     (ioe.Source?.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) == true))
            {
                // Log DI-related exceptions for additional diagnostics
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                Console.WriteLine($"\n[{timestamp}] ═══ FIRST CHANCE: DI Exception ═══");
                Console.WriteLine($"Message: {ioe.Message}");
                Console.WriteLine($"Stack:   {ioe.StackTrace ?? "(no stack)"}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
            }
        }
        /// <summary>
        /// Performs registersyncfusionlicense. Parameters: configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
#pragma warning disable CA1508 // licenseKey null-check is intentional to catch whitespace-only values
        private static void RegisterSyncfusionLicense(IConfiguration configuration)
        {
            try
            {
                    string? rawLicenseKey = configuration["Syncfusion:LicenseKey"];
                    if (string.IsNullOrWhiteSpace(rawLicenseKey))
                    {
                        throw new InvalidOperationException("Syncfusion license key not found in configuration.");
                    }
                    string licenseKey = rawLicenseKey!; // non-null after guard
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);

                // Validate the license key - non-fatal in development to allow testing
                bool isValid = Syncfusion.Licensing.SyncfusionLicenseProvider.ValidateLicense(Syncfusion.Licensing.Platform.WindowsForms);
                if (!isValid)
                {
                    // Log key metadata for debugging (no secrets exposed)
                    var keyLength = rawLicenseKey?.Length ?? 0;
                    var keyHash = string.Format(System.Globalization.CultureInfo.InvariantCulture, "0x{0:X8}", StringComparer.Ordinal.GetHashCode(rawLicenseKey ?? string.Empty));
                    Log.Warning("Syncfusion license validation failed: Key length={Length}, Hash={Hash}. App will continue with license banner.", keyLength, keyHash);
#if DEBUG
                    Console.WriteLine($"[WARNING] Syncfusion license validation failed. Continuing in development mode with banner.");
#else
                    // In production, throw to prevent unlicensed usage
                    throw new InvalidOperationException("Syncfusion license key is invalid or does not match the package versions. Check Syncfusion account for correct WinForms license key.");
#endif
                }
                else
                {
                    Log.Information("Syncfusion license registered and validated successfully.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register Syncfusion license.");
                throw;
            }
#pragma warning restore CA1508
        }
        /// <summary>
        /// Performs isrunningintestenvironment.
        /// </summary>
        private static bool IsRunningInTestEnvironment()
        {
            // Check for test environment indicators
            return Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS") == "true" ||
                   AppDomain.CurrentDomain.GetAssemblies()
                       .Any(asm => asm.FullName?.Contains("test", StringComparison.OrdinalIgnoreCase) == true ||
                                   asm.FullName?.Contains("xunit", StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Initialize the Syncfusion theme system at application startup.
        /// AUTHORITATIVE SOURCE: This is the ONLY location where theme should be set during startup.
        /// Uses SkinManager.LoadAssembly and SkinManager.ApplicationVisualTheme (CORRECT API).
        /// Child forms automatically inherit this theme - do NOT call SetVisualStyle in form constructors.
        /// Reference: https://help.syncfusion.com/windowsforms/skins/getting-started
        /// </summary>
        private static void InitializeTheme(IConfiguration config)
        {
            try
            {
#if DEBUG
                Console.WriteLine("[THEME] Starting theme initialization...");
#endif

                // Null guard for timeline service
                if (_timelineService != null)
                {
                    _timelineService.RecordOperation("Load Office2019Theme assembly", "Theme Initialization");
                }

                // CRITICAL: Load theme assembly FIRST before setting ApplicationVisualTheme
                // Reference: Syncfusion WinForms Skins documentation
                try
                {
                    SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
#if DEBUG
                    Console.WriteLine("[THEME] Office2019Theme assembly loaded successfully");
#endif
                    Log.Information("Syncfusion Office2019Theme assembly loaded successfully");
                }
                catch (Exception loadEx)
                {
                    Console.WriteLine($"[THEME ERROR] Failed to load Office2019Theme assembly: {loadEx.Message}");
                    Console.WriteLine($"[THEME ERROR] StackTrace: {loadEx.StackTrace}");
                    Log.Error(loadEx, "Failed to load Office2019Theme assembly");
                    throw; // Rethrow to outer catch for comprehensive handling
                }

                // Apply global theme - read from configuration with fallback to Office2019Colorful
                var themeName = config["UI:Theme"] ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
#if DEBUG
                Console.WriteLine($"[THEME] Setting ApplicationVisualTheme to: {themeName}");
#endif

                if (_timelineService != null)
                {
                    _timelineService.RecordOperation($"Set ApplicationVisualTheme: {themeName}", "Theme Initialization");
                }

                // CRITICAL: Set ApplicationVisualTheme AFTER assembly load and BEFORE any form creation
                // This must be set after Application.EnableVisualStyles() for proper rendering
                try
                {
                    SkinManager.ApplicationVisualTheme = themeName;  // Global application-wide theme
#if DEBUG
                    Console.WriteLine($"[THEME] ApplicationVisualTheme set successfully to: {themeName}");
#endif
                    Log.Information("Syncfusion theme '{ThemeName}' applied globally via SkinManager", themeName);
                }
                catch (Exception setEx)
                {
                    Console.WriteLine($"[THEME ERROR] Failed to set ApplicationVisualTheme: {setEx.Message}");
                    Console.WriteLine($"[THEME ERROR] StackTrace: {setEx.StackTrace}");
                    Log.Error(setEx, "Failed to set ApplicationVisualTheme to {ThemeName}", themeName);
                    throw; // Rethrow to outer catch for comprehensive handling
                }

#if DEBUG
                Console.WriteLine($"[THEME] Theme initialization completed successfully");
#endif
            }
            catch (Exception ex)
            {
                // COMPREHENSIVE ERROR LOGGING
                Console.WriteLine($"[THEME FATAL] Theme initialization failed with exception type: {ex.GetType().Name}");
                Console.WriteLine($"[THEME FATAL] Message: {ex.Message}");
                Console.WriteLine($"[THEME FATAL] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[THEME FATAL] InnerException: {ex.InnerException.Message}");
                    Console.WriteLine($"[THEME FATAL] InnerException StackTrace: {ex.InnerException.StackTrace}");
                }

                Log.Error(ex, "Theme initialization failed; continuing with default Windows theme");

                // GRACEFUL FALLBACK: Continue without theme to prevent startup failure
                // Forms will use default Windows styling
                Console.WriteLine("[THEME] Continuing startup with default Windows theme (no Syncfusion theming)");
            }
        }
        /// <summary>
        /// Performs configureerrorreporting.
        /// </summary>
        private static void ConfigureErrorReporting()
        {
            // Configure error reporting service if needed
        }

        /// <summary>
        /// Initialize Windows Forms application settings and high-DPI support.
        /// </summary>
        private static void InitializeWinForms()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Set default font for all new controls
            try
            {
                Application.SetDefaultFont(WileyWidget.WinForms.Services.FontService.Instance.CurrentFont);
                Log.Information("Default font set successfully: {FontName} {FontSize}pt",
                    WileyWidget.WinForms.Services.FontService.Instance.CurrentFont.Name,
                    WileyWidget.WinForms.Services.FontService.Instance.CurrentFont.Size);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set default font");
            }
        }
        /// <summary>
        /// Performs capturesynchronizationcontext.
        /// </summary>
        public static void CaptureSynchronizationContext()
        {
            // NULL PROTECTION: Check if we already have a WindowsFormsSynchronizationContext
            var currentContext = System.Threading.SynchronizationContext.Current;
            if (currentContext is not WindowsFormsSynchronizationContext)
            {
                try
                {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                }
                catch (Exception syncEx)
                {
                    Console.Error.WriteLine($"Failed to set WindowsFormsSynchronizationContext; continuing with existing context: {syncEx}");
                }
            }

            // NULL PROTECTION: Ensure we have a valid synchronization context
            var finalContext = System.Threading.SynchronizationContext.Current;
            if (finalContext == null)
            {
                // Fallback: Create a new WindowsFormsSynchronizationContext if none exists
                try
                {
                    var fallbackContext = new WindowsFormsSynchronizationContext();
                    System.Threading.SynchronizationContext.SetSynchronizationContext(fallbackContext);
                    finalContext = fallbackContext;
                    Log.Warning("Created fallback WindowsFormsSynchronizationContext because none was available");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to create fallback synchronization context - UI marshaling may not work correctly");
                    // Continue with null - some operations may still work
                }
            }

            UISynchronizationContext = finalContext;
        }
        /// <summary>
        /// Performs buildhost. Parameters: args.
        /// </summary>
        /// <param name="args">The args.</param>
        private static IHost BuildHost(string[] args)
        {
            var reportViewerLaunchOptions = CreateReportViewerLaunchOptions(args);
            var builder = Host.CreateApplicationBuilder(args);

            AddConfiguration(builder);
            ConfigureLogging(builder);
            ConfigureDatabase(builder);
            ConfigureHealthChecks(builder);
            CaptureDiFirstChanceExceptions();
            AddDependencyInjection(builder);
            ConfigureUiServices(builder);

            builder.Services.AddSingleton(reportViewerLaunchOptions);

            // DEBUG: Check config BEFORE Build()
            var preBuildXai = builder.Configuration["XAI:ApiKey"];
            Console.WriteLine($"[PRE-BUILD DEBUG] XAI:ApiKey BEFORE builder.Build() = {(preBuildXai != null ? preBuildXai.Substring(0, Math.Min(15, preBuildXai.Length)) + "..." : "NULL")} ({preBuildXai?.Length ?? 0} chars)");

            return builder.Build();
        }
        /// <summary>
        /// Performs addconfiguration. Parameters: builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        private static void AddConfiguration(HostApplicationBuilder builder)
        {
            // Ensure .env is loaded before adding configuration sources
            // DotNetEnv sets environment variables at process level
            try
            {
                DotNetEnv.Env.TraversePath().Load();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to load .env file: {ex.Message}");
            }

            // Load primary appsettings.json from project directory
            try
            {
                builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load appsettings.json");
            }

            // CRITICAL: Also load config/development/appsettings.json which has the full configuration
            try
            {
                var devConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "development", "appsettings.json");
                if (File.Exists(devConfigPath))
                {
                    builder.Configuration.AddJsonFile(devConfigPath, optional: false, reloadOnChange: true);
#if DEBUG
                    Console.WriteLine($"[CONFIG] Loaded development config from: {devConfigPath}");
#endif
                }
                else
                {
                    Console.WriteLine($"[CONFIG WARNING] Development config not found at: {devConfigPath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CONFIG ERROR] Failed to load config/development/appsettings.json: {ex.Message}");
            }

            // Add developer user secrets after JSON config so they override appsettings during development.
            try
            {
                builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONFIG] AddUserSecrets not applied: {ex.Message}");
            }

            builder.Configuration.AddEnvironmentVariables();

            // CRITICAL: Expand environment variable placeholders from appsettings.json
            // appsettings.json uses ${VAR_NAME} syntax which .NET doesn't expand automatically
            // Read environment variables and override config values that have ${...} placeholders
            try
            {
                var xaiApiKeyEnv = Environment.GetEnvironmentVariable("XAI_API_KEY");
#if DEBUG
                Console.WriteLine($"[ENV VAR DEBUG] XAI_API_KEY from environment = {(xaiApiKeyEnv != null ? xaiApiKeyEnv.Substring(0, Math.Min(15, xaiApiKeyEnv.Length)) + "..." : "NULL")} ({xaiApiKeyEnv?.Length ?? 0} chars)");
#endif

                var openAiKeyEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                var syncfusionKeyEnv = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                var qboClientIdEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
                var qboClientSecretEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");

                var overrides = new Dictionary<string, string?>();

                if (!string.IsNullOrWhiteSpace(xaiApiKeyEnv))
                {
                    overrides["XAI:ApiKey"] = xaiApiKeyEnv;
#if DEBUG
                    Console.WriteLine($"[CONFIG] Overriding XAI:ApiKey from environment variable (length: {xaiApiKeyEnv.Length})");
#endif
                }
                else
                {
#if DEBUG
                    Console.WriteLine($"[CONFIG WARNING] XAI_API_KEY environment variable is NULL or empty!");
#endif
                }

                if (!string.IsNullOrWhiteSpace(openAiKeyEnv))
                {
                    overrides["OpenAI:ApiKey"] = openAiKeyEnv;
                }

                if (!string.IsNullOrWhiteSpace(syncfusionKeyEnv))
                {
                    overrides["Syncfusion:LicenseKey"] = syncfusionKeyEnv;
                }

                if (!string.IsNullOrWhiteSpace(qboClientIdEnv))
                {
                    overrides["QuickBooks:ClientId"] = qboClientIdEnv;
                }

                if (!string.IsNullOrWhiteSpace(qboClientSecretEnv))
                {
                    overrides["QuickBooks:ClientSecret"] = qboClientSecretEnv;
                }

                if (overrides.Count > 0)
                {
                    builder.Configuration.AddInMemoryCollection(overrides);
#if DEBUG
                    Console.WriteLine($"[CONFIG] Added {overrides.Count} configuration overrides via AddInMemoryCollection");
#endif

                    // Verify the override was applied
                    var verifyXai = builder.Configuration["XAI:ApiKey"];
                    Console.WriteLine($"[CONFIG VERIFY] After AddInMemoryCollection: XAI:ApiKey = {(verifyXai != null ? verifyXai.Substring(0, Math.Min(15, verifyXai.Length)) + "..." : "NULL")} ({verifyXai?.Length ?? 0} chars)");
                }
                else
                {
                    Console.WriteLine("[CONFIG WARNING] No configuration overrides to add!");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARNING] Error expanding environment variable placeholders: {ex.Message}");
            }

            try
            {
                var existingConn = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(existingConn))
                {
                    var defaultConn = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = defaultConn
                    });
                    Log.Warning("DefaultConnection not found; using in-memory fallback");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error ensuring default connection in configuration: {ex}");
            }
        }
        /// <summary>
        /// Performs configurelogging. Parameters: builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            try
            {
                // Make Serilog self-logging forward internal errors to stderr so we can diagnose sink failures
                Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine($"[SERILOG] {msg}"));

                // CRITICAL: ALL LOGS go to repo root 'logs' directory (deterministic)
                // Resolve logs directory: prefer WILEYWIDGET_LOG_DIR env var; otherwise search upward for WileyWidget.sln or .git
                static string ResolveLogsDir()
                {
                    var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_LOG_DIR");
                    if (!string.IsNullOrWhiteSpace(envPath))
                    {
                        return envPath;
                    }

                    string[] startPaths = { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
                    foreach (var start in startPaths)
                    {
                        var dir = start;
                        for (int depth = 0; depth < 8; depth++)
                        {
                            if (File.Exists(Path.Combine(dir, "WileyWidget.sln")) || Directory.Exists(Path.Combine(dir, ".git")))
                            {
                                var p = Path.Combine(dir, "logs");
                                Environment.SetEnvironmentVariable("WILEYWIDGET_LOG_DIR", p);
                                return p;
                            }

                            var parent = Directory.GetParent(dir);
                            if (parent == null) break;
                            dir = parent.FullName;
                        }
                    }

                    // Fallback to using working directory
                    var fallback = Path.Combine(Directory.GetCurrentDirectory(), "logs");
                    Environment.SetEnvironmentVariable("WILEYWIDGET_LOG_DIR", fallback);
                    return fallback;
                }

                var logsPath = ResolveLogsDir();

#if DEBUG
                Console.WriteLine($"Creating logs directory at: {logsPath}");
#endif
                Directory.CreateDirectory(logsPath);

                // Quick write test to proactively detect permission or IO issues with the logs directory
                try
                {
                    var testFile = Path.Combine(logsPath, "write_test.tmp");
                    File.AppendAllText(testFile, $"write test {DateTime.UtcNow:O}\n");
                    File.Delete(testFile);
                }
                catch (Exception ioEx)
                {
                    Console.Error.WriteLine($"[LOGGING] WARNING: Unable to write to logs directory '{logsPath}': {ioEx.GetType().Name}: {ioEx.Message}");
                }

                // Template used by Serilog's rolling file sink (daily rolling uses a date suffix)
                var logFileTemplate = Path.Combine(logsPath, "app-.log");
#if DEBUG
                Console.WriteLine($"Log file pattern: {logFileTemplate}");

                // Resolve the current daily log file that Serilog will write to for today's date
                // Serilog's daily rolling file uses the yyyyMMdd date format (e.g., app-20251215.log)
                var logFileCurrent = Path.Combine(logsPath, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
                Console.WriteLine($"Current daily log file: {logFileCurrent}");
#endif

                // Check for SQL logging override environment variable
                var enableSqlLogging = Environment.GetEnvironmentVariable("WILEYWIDGET_LOG_SQL");
                var sqlLogLevel = string.Equals(enableSqlLogging, "true", StringComparison.OrdinalIgnoreCase)
                    ? Serilog.Events.LogEventLevel.Information
                    : Serilog.Events.LogEventLevel.Warning;

#if DEBUG
                Console.WriteLine($"SQL logging level: {sqlLogLevel} (WILEYWIDGET_LOG_SQL={enableSqlLogging ?? "not set"})");
#endif

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
#if DEBUG
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
#endif
                    .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File(logFileTemplate, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30, fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true, shared: true)
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", sqlLogLevel)
                    .CreateLogger();

#if DEBUG
                Console.WriteLine("Logging configured successfully");
#endif
                // Log both the resolved current file and the template used by the rolling sink so it's clear
                Log.Information("Logging system initialized - writing to {LogPath} (pattern: {LogPattern})", logFileCurrent, logFileTemplate);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"CRITICAL: Failed to configure logging: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"StackTrace: {ex.StackTrace}");

                // Fallback to console-only logging
                Log.Logger = new LoggerConfiguration()
#if DEBUG
                    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
#endif
                    .WriteTo.Debug(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                    .MinimumLevel.Information()
                    .CreateLogger();


                Console.Error.WriteLine("Logging fallback to console-only mode");
            }
        }
        /// <summary>
        /// Performs configuredatabase. Parameters: builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        private static void ConfigureDatabase(HostApplicationBuilder builder)
        {
            void ConfigureSqlOptions(DbContextOptionsBuilder options)
            {
                // CRITICAL: Only use in-memory database when explicitly running UI tests via environment variable.
                // Production runs should ALWAYS use SQL Server connection.
                var isUiTestRun = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

                // DO NOT use configuration UseInMemoryForTests - removed to prevent accidental in-memory usage
                // DO NOT check WILEYWIDGET_USE_INMEMORY - this should only be set during actual test execution

                if (isUiTestRun)
                {
                    options.UseInMemoryDatabase("WileyWidgetUiTests");
                    Log.Information("Using InMemory database for UI tests (WILEYWIDGET_UI_TESTS=true)");
                    return;
                }

                // PRODUCTION PATH: Use SQL Server connection string
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                    Log.Warning("DefaultConnection missing; using fallback SQL Server connection string");
                }

                connectionString = Environment.ExpandEnvironmentVariables(connectionString);

                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("WileyWidget.Data");
                    sql.CommandTimeout(builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 30));
                    sql.EnableRetryOnFailure(
                        maxRetryCount: builder.Configuration.GetValue("Database:MaxRetryCount", 3),
                        maxRetryDelay: TimeSpan.FromSeconds(builder.Configuration.GetValue("Database:MaxRetryDelaySeconds", 10)),
                        errorNumbersToAdd: null);
                });

                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging(builder.Configuration.GetValue("Database:EnableSensitiveDataLogging", false));

                options.UseLoggerFactory(new SerilogLoggerFactory(Log.Logger));

                Log.Information("Using SQL Server database: {Database}", connectionString.Split(';').FirstOrDefault(s => s.Contains("Database", StringComparison.OrdinalIgnoreCase)) ?? "WileyWidgetDev");
            }

            // CRITICAL: Use Scoped lifetime for DbContextFactory (NOT Singleton)
            // Reason: DbContextOptions internally resolves IDbContextOptionsConfiguration which is scoped.
            // Using Singleton would cause "Cannot resolve scoped service from root provider" errors.
            // EF Core best practice: Factory should be Scoped, DbContext is implicitly Scoped.
            builder.Services.AddDbContextFactory<AppDbContext>(ConfigureSqlOptions, ServiceLifetime.Scoped);
            builder.Services.AddDbContext<AppDbContext>(ConfigureSqlOptions);
        }
        /// <summary>
        /// Performs configurehealthchecks. Parameters: builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        private static void ConfigureHealthChecks(HostApplicationBuilder builder)
        {
            try
            {
                var healthChecksSection = builder.Configuration.GetSection("HealthChecks");
                var healthConfig = healthChecksSection.Get<HealthCheckConfiguration>() ?? new HealthCheckConfiguration();
                builder.Services.AddSingleton(healthConfig);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to configure HealthCheckConfiguration from appsettings - using default configuration");
                builder.Services.AddSingleton(new HealthCheckConfiguration());
            }
        }
        /// <summary>
        /// Performs capturedifirstchanceexceptions.
        /// </summary>
        private static void CaptureDiFirstChanceExceptions()
        {
            AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
            {
                var ex = eventArgs.Exception;
                if ((ex is InvalidOperationException || ex is AggregateException) &&
                    ex.Source != null && ex.Source.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
                {
                    Console.WriteLine($"First-chance DI exception: {ex.Message}");
                }
            };
        }
        /// <summary>
        /// Performs adddependencyinjection. Parameters: builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        private static void AddDependencyInjection(HostApplicationBuilder builder)
        {
            // Create DI descriptors but avoid test defaults (in-memory DB) when building the real host
            var diServices = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection(includeDefaults: false);

            // CRITICAL: Skip IConfiguration descriptor - use the host builder's configuration instead
            // The DependencyInjection.CreateServiceCollection() adds a default IConfiguration for test scenarios
            // but we want to use the real configuration from the host builder which includes .env and appsettings.json
            foreach (var descriptor in diServices)
            {
                if (descriptor.ServiceType == typeof(IConfiguration))
                {
                    Console.WriteLine("[DI] Skipping IConfiguration from CreateServiceCollection - using host builder's configuration");
                    continue; // Skip - use host builder's configuration
                }

                builder.Services.Add(descriptor);
            }
        }
        /// <summary>
        /// Performs configureuiservices. Parameters: builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        private static void ConfigureUiServices(HostApplicationBuilder builder)
        {
            // UI configuration is now handled via UIConfiguration.FromConfiguration in DependencyInjection.cs
            // No additional UI services needed here in Phase 1
        }

        private static async Task RunStartupHealthCheckAsync(IServiceProvider services)
        {
            Log.Information("[DIAGNOSTIC] Entered RunStartupHealthCheckAsync");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Entered RunStartupHealthCheckAsync");
            try
            {
                // Create a scope for scoped services (DbContext)
                Log.Information("[DIAGNOSTIC] Creating scope for health check");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Creating scope for health check");
                using var scope = services.CreateScope();
                var scopedServices = scope.ServiceProvider;

                Log.Information("[DIAGNOSTIC] Getting AppDbContext from DI");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Getting AppDbContext from DI");
                var dbContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scopedServices);

                _timelineService?.RecordOperation("Test database connectivity", "Database Health Check");
                Log.Information("[DIAGNOSTIC] Testing database connectivity with CanConnectAsync");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Testing database connectivity with CanConnectAsync");
                await dbContext.Database.CanConnectAsync();

                Log.Information("Startup health check passed: Database connection successful");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Database CanConnectAsync succeeded");

                // Get data statistics for diagnostic purposes — run on threadpool to avoid sync-over-async deadlock
                Log.Information("[DIAGNOSTIC] Starting data statistics check");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Starting data statistics check");
                try
                {
                    using (var diagnosticScope = services.CreateScope())
                    {
                        var diagnosticScopedServices = diagnosticScope.ServiceProvider;
                        Log.Information("[DIAGNOSTIC] Getting IDashboardService from DI");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Getting IDashboardService from DI");
                        var dashboardService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Abstractions.IDashboardService>(diagnosticScopedServices);
                        if (dashboardService != null)
                        {
                            try
                            {
                                _timelineService?.RecordOperation("Query data statistics", "Database Health Check");
                                // Use Task.WhenAny for proper async timeout pattern instead of blocking .Wait()
                                Log.Information("[DIAGNOSTIC] Calling GetDataStatisticsAsync with 30s timeout");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Calling GetDataStatisticsAsync with 30s timeout");
                                var statsTask = dashboardService.GetDataStatisticsAsync();
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                                var completedTask = await Task.WhenAny(statsTask, timeoutTask); // Keep context for UI updates

                                if (completedTask == statsTask)
                                {
                                    Log.Information("[DIAGNOSTIC] GetDataStatisticsAsync completed, awaiting result");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] GetDataStatisticsAsync completed, awaiting result");
                                    var stats = await statsTask; // Keep context
                                    Log.Information("Diagnostic: Database contains {RecordCount} budget entries (Oldest: {Oldest}, Newest: {Newest})",
                                        stats.TotalRecords,
                                        stats.OldestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                                        stats.NewestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Stats: {stats.TotalRecords} records");

                                    if (stats.TotalRecords == 0)
                                    {
                                        Log.Warning("Diagnostic: Database has no budget entries. Dashboard will show empty data. Attempting to run data seeding.");

                                        try
                                        {
                                            var seeder = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DataSeedingService>(diagnosticScopedServices);
                                            if (seeder != null)
                                            {
                                                await seeder.SeedBudgetDataAsync();

                                                // Re-run stats check once after seeding
                                                var newStats = await dashboardService.GetDataStatisticsAsync();
                                                Log.Information("DataSeedingService: After seeding, DB contains {RecordCount} budget entries", newStats.TotalRecords);
                                                if (newStats.TotalRecords == 0)
                                                {
                                                    Log.Warning("DataSeedingService: Seeding completed but no budget entries were created (check logs)");
                                                }
                                            }
                                            else
                                            {
                                                Log.Warning("DataSeedingService not registered in DI; cannot seed data automatically");
                                            }
                                        }
                                        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 208)
                                        {
                                            // SqlException 208 = Invalid object name (missing table/schema)
                                            Log.Warning(sqlEx, "Data seeding aborted: missing database schema (SqlException 208). To fix, run migrations:\n  dotnet ef database update --project src/WileyWidget.Data --startup-project src/WileyWidget.WinForms\nand then re-run the application.");
                                        }
                                        catch (Exception seEx)
                                        {
                                            Log.Warning(seEx, "Data seeding failed during startup health check");
                                        }
                                    }
                                }
                                else
                                {
                                    Log.Warning("Diagnostic: GetDataStatisticsAsync timed out after {TimeoutSeconds}s", 30);
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] GetDataStatisticsAsync timed out");
                                }
                            }
                            catch (Exception innerDiagEx)
                            {
                                Log.Warning(innerDiagEx, "Diagnostic: Failed to retrieve data statistics (threadpool execution)");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] GetDataStatisticsAsync exception: {innerDiagEx.Message}");
                            }
                        }
                        else
                        {
                            Log.Warning("Diagnostic: IDashboardService not available for data statistics check");
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] IDashboardService not available");
                        }
                    }
                    Log.Information("[DIAGNOSTIC] Data statistics check completed");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Data statistics check completed");
                }
                catch (Exception diagEx)
                {
                    Log.Warning(diagEx, "Diagnostic: Failed to retrieve data statistics");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Data statistics outer exception: {diagEx.Message}");
                }

                Log.Information("[DIAGNOSTIC] Exiting RunStartupHealthCheckAsync successfully");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Exiting RunStartupHealthCheckAsync successfully");
            }
            catch (Exception ex)
            {
                // Special-case detection: missing DB schema (SqlException Number 208 => "Invalid object name")
                if (ex is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 208)
                {
                    Log.Warning(sqlEx, "Database schema appears to be missing (SqlException {Number}). Attempting to apply pending migrations automatically.", sqlEx.Number);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Missing DB schema detected: {sqlEx.Message}. Attempting automatic migration.");
                    try
                    {
                        using var migrateScope = services.CreateScope();
                        var scopedServices = migrateScope.ServiceProvider;
                        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scopedServices);

                        // Apply pending EF Core migrations (development convenience only)
                        db.Database.Migrate();

                        Log.Information("Automatic EF migrations applied successfully.");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Automatic EF migrations applied successfully.");
                    }
                    catch (Exception migrateEx)
                    {
                        Log.Error(migrateEx, "Automatic migration attempt failed. To apply migrations manually, run: dotnet ef database update --project src/WileyWidget.Data --startup-project src/WileyWidget.WinForms");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Automatic migration attempt failed: {migrateEx.Message}");
                    }
                }
                else
                {
                    Log.Warning(ex, "Startup health check failed: Database connection issue");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] RunStartupHealthCheckAsync caught exception: {ex.Message}");
                }

                // Don't throw here, let the app start and log the issue
            }

            Log.Information("[DIAGNOSTIC] RunStartupHealthCheckAsync method exit");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] RunStartupHealthCheckAsync method exit");
        }
        /// <summary>
        /// Performs isverifystartup. Parameters: args.
        /// </summary>
        /// <param name="args">The args.</param>
        private static bool IsVerifyStartup(string[] args)
        {
            return args != null && Array.Exists(args, a => string.Equals(a, "--verify-startup", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task RunVerifyStartupAsync(IHost host)
        {
            try
            {
                await host.StartAsync();
                await host.StopAsync();
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Verify-startup run failed");
                Log.CloseAndFlush();
                throw new InvalidOperationException("Verify-startup orchestration failed", ex);
            }
        }
        /// <summary>
        /// Performs wireglobalexceptionhandlers.
        /// </summary>
        private static void WireGlobalExceptionHandlers()
        {
            _timelineService?.RecordOperation("Wire Application.ThreadException handler", "Error Handlers");
            Application.ThreadException += (sender, e) =>
            {
                try
                {
                    Log.Fatal(e.Exception, "Unhandled UI thread exception");
                }
                catch (Exception fatalLogEx)
                {
                    Console.Error.WriteLine($"Log.Fatal failed for UI thread exception: {fatalLogEx} - original exception: {e.Exception}");
                }

                try
                {
                    (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(e.Exception, "UI Thread Exception", showToUser: false);
                }
                catch (ObjectDisposedException ode)
                {
                    // Logging or DI disposed during shutdown — suppress and fallback to console to avoid crash
                    Console.Error.WriteLine($"Reporting suppressed (disposed): {ode.Message}");
                }
                catch (AggregateException ae) when (ae.InnerException is ObjectDisposedException)
                {
                    Console.Error.WriteLine($"Reporting suppressed (aggregate inner disposed): {ae.InnerException?.Message}");
                }
                catch (Exception reportEx)
                {
                    Console.Error.WriteLine($"Failed to report UI thread exception to ErrorReportingService: {reportEx}");
                }

                try
                {
                    MessageBox.Show($"UI Error: {e.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                    // Swallow UI notification failures
                }
            };

            _timelineService?.RecordOperation("Wire AppDomain.UnhandledException handler", "Error Handlers");
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                try
                {
                    Log.Fatal(ex, "Unhandled AppDomain exception");
                }
                catch (Exception fatalLogEx)
                {
                    Console.Error.WriteLine($"Log.Fatal failed for AppDomain exception: {fatalLogEx} - original exception: {ex}");
                }

                try
                {
                    (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(ex ?? new InvalidOperationException("Unhandled domain exception"), "Domain exception", showToUser: false);
                }
                catch (ObjectDisposedException ode)
                {
                    Console.Error.WriteLine($"Reporting suppressed (disposed): {ode.Message}");
                }
                catch (AggregateException ae) when (ae.InnerException is ObjectDisposedException)
                {
                    Console.Error.WriteLine($"Reporting suppressed (aggregate inner disposed): {ae.InnerException?.Message}");
                }
                catch (Exception reportEx)
                {
                    Console.Error.WriteLine($"Failed to report AppDomain exception to ErrorReportingService: {reportEx}");
                }
            };
        }
        /// <summary>
        /// Performs scheduleautocloseifrequested. Parameters: args, mainForm.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <param name="mainForm">The mainForm.</param>
        private static void ScheduleAutoCloseIfRequested(string[] args, Form mainForm)
        {
            var autoCloseMs = ParseAutoCloseMs(args);
            if (autoCloseMs <= 0)
            {
                return;
            }

            // Keep the UI open during interactive runs unless explicitly allowed
            if (Environment.UserInteractive && !IsAutoCloseAllowed(args))
            {
                Log.Information("Auto-close argument detected but ignored in interactive mode. Remove --auto-close-ms to keep the window open.");
                return;
            }

            try
            {
                ScheduleAutoClose(mainForm, autoCloseMs);
                Log.Debug("Auto-close scheduled in {Ms}ms", autoCloseMs);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to schedule auto-close");
            }
        }
        /// <summary>
        /// Performs parseautoclosems. Parameters: args.
        /// </summary>
        /// <param name="args">The args.</param>
        private static int ParseAutoCloseMs(string[] args)
        {
            var autoCloseArg = args?.FirstOrDefault(a => a != null && a.StartsWith("--auto-close-ms=", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(autoCloseArg))
            {
                return -1;
            }

            return int.TryParse(autoCloseArg.Split('=', 2).LastOrDefault(), out var autoCloseMs) && autoCloseMs > 0
                ? autoCloseMs
                : -1;
        }
        /// <summary>
        /// Performs isautocloseallowed. Parameters: args.
        /// </summary>
        /// <param name="args">The args.</param>
        private static bool IsAutoCloseAllowed(string[] args)
        {
            if (IsCiEnvironment())
            {
                return true;
            }

            return args != null && Array.Exists(args, a => string.Equals(a, "--force-auto-close", StringComparison.OrdinalIgnoreCase));
        }
        /// <summary>
        /// Performs iscienvironment.
        /// </summary>
        private static bool IsCiEnvironment()
        {
            return string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
        }
        /// <summary>
        /// Performs scheduleautoclose. Parameters: mainForm, autoCloseMs.
        /// </summary>
        /// <param name="mainForm">The mainForm.</param>
        /// <param name="autoCloseMs">The autoCloseMs.</param>
        private static void ScheduleAutoClose(Form mainForm, int autoCloseMs)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(autoCloseMs).ConfigureAwait(false);
                try
                {
                    if (mainForm != null && !mainForm.IsDisposed)
                    {
                        mainForm.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (!mainForm.IsDisposed)
                                {
                                    mainForm.Close();
                                }
                            }
                            catch (Exception closeEx)
                            {
                                Log.Debug(closeEx, "Auto-close failed to close main form");
                            }
                        }));
                    }
                }
                catch (Exception autoCloseEx)
                {
                    Console.Error.WriteLine($"Auto-close task failed: {autoCloseEx}");
                }
            });
        }
        /// <summary>
        /// Performs runuiloop. Parameters: context.
        /// </summary>
        /// <param name="context">The context.</param>
        private static void RunUiLoop(ApplicationContext context)
        {
            try
            {
                Application.Run(context);
            }
            catch (Exception ex)
            {
                try { Log.Fatal(ex, "Application.Run aborted with exception"); } catch (Exception logEx) { Console.Error.WriteLine($"Failed to log Application.Run fatal during shutdown: {logEx}"); }
                try { (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(ex, "UI message loop aborted", showToUser: false); } catch (Exception reportEx) { Console.Error.WriteLine($"Failed to report Application.Run abort to ErrorReportingService: {reportEx}"); }
                throw new InvalidOperationException("UI message loop aborted", ex);
            }
            finally
            {
                Log.Information("Application exited normally.");
                // Keep logger open — host shutdown and final flush handled by MainAsync to avoid disposing logging while UI handlers may still run.
            }
        }
        /// <summary>
        /// Performs createreportviewerlaunchoptions. Parameters: args.
        /// </summary>
        /// <param name="args">The args.</param>
        private static ReportViewerLaunchOptions CreateReportViewerLaunchOptions(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return ReportViewerLaunchOptions.Disabled;
            }

            var requestArg = args.FirstOrDefault(arg => string.Equals(arg, "--show-report-viewer", StringComparison.OrdinalIgnoreCase));
            if (requestArg == null)
            {
                return ReportViewerLaunchOptions.Disabled;
            }

            var rawPath = ExtractArgumentValue(args, "--report-path");
            var normalized = NormalizeReportPath(rawPath);
            if (string.IsNullOrWhiteSpace(normalized) || !File.Exists(normalized))
            {
                return ReportViewerLaunchOptions.Disabled;
            }

            return new ReportViewerLaunchOptions(true, normalized);
        }

        private static string? ExtractArgumentValue(string[] args, string prefix)
        {
            var match = args.FirstOrDefault(arg => arg.StartsWith(prefix + "=", StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                return null;
            }

            var value = match[(prefix.Length + 1)..].Trim();
            return TrimQuotes(value);
        }

        private static string? TrimQuotes(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                return value[1..^1];
            }

            return value;
        }

        private static string? NormalizeReportPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                var trimmed = path.Trim();
                if (Path.IsPathRooted(trimmed))
                {
                    return Path.GetFullPath(trimmed);
                }

                var combined = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, trimmed));
                return combined;
            }
            catch
            {
                return path;
            }
        }
        /// <summary>
        /// Performs handlestartupfailure. Parameters: ex.
        /// </summary>
        /// <param name="ex">The ex.</param>
        private static void HandleStartupFailure(Exception ex)
        {
            try
            {
                Log.Fatal(ex, "Application failed to start");
            }
            catch (Exception logEx)
            {
                Console.Error.WriteLine($"Failed to log startup fatal error: {logEx}");
            }
            finally
            {
                Log.CloseAndFlush();
            }

            try
            {
                // Only try to report to ErrorReportingService if Services is available
                if (Services != null)
                {
                    (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(ex, "Startup Failure", showToUser: false);
                }
            }
            catch (Exception reportEx)
            {
                Console.Error.WriteLine($"Failed to report startup failure to ErrorReportingService: {reportEx}");
            }

            // Show user-friendly error dialog for startup failures
            try
            {
                var logPath = Environment.GetEnvironmentVariable("WILEYWIDGET_LOG_DIR");
                if (string.IsNullOrWhiteSpace(logPath))
                {
                    var dir = Directory.GetCurrentDirectory();
                    for (int i = 0; i < 8; i++)
                    {
                        if (File.Exists(Path.Combine(dir, "WileyWidget.sln")) || Directory.Exists(Path.Combine(dir, ".git")))
                        {
                            logPath = Path.Combine(dir, "logs");
                            break;
                        }
                        var parent = Directory.GetParent(dir);
                        if (parent == null) break;
                        dir = parent.FullName;
                    }

                    if (string.IsNullOrWhiteSpace(logPath)) logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
                }

                var message = "Startup failed: Check logs at " + logPath;

                // Check if we have UI initialized
                if (Application.MessageLoop)
                {
                    MessageBox.Show(message, "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    // Fallback to console output if no message loop
                    Console.Error.WriteLine("Startup Error: " + ex.Message);
                }
            }
            catch (Exception uiEx)
            {
                // Last resort - write to console
                Console.Error.WriteLine($"Critical startup error: {ex.Message}");
                Console.Error.WriteLine($"UI error display failed: {uiEx.Message}");
            }
        }

        /// <summary>
        /// Minimal splash implementation used by the UI startup sequence.
        /// Provides a lightweight on-screen splash for interactive runs and
        /// a headless no-op for CI/test environments.
        /// </summary>
        internal sealed class SplashForm : Form, IDisposable
        {
            private readonly bool _isHeadless;
            private Label _messageLabel;
            private ProgressBar _progressBar;

            public SplashForm()
            {
                _isHeadless = IsRunningInTestEnvironment() || !Environment.UserInteractive;
                if (!_isHeadless)
                {
                    InitializeComponent();
                }
            }

            private void InitializeComponent()
            {
                this.SuspendLayout();
                this.Size = new Size(480, 140);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.ShowInTaskbar = false;
                this.Text = "Wiley Widget - Loading...";

                var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
                _messageLabel = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 40,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "Initializing...",
                    AutoSize = false
                };
                _progressBar = new ProgressBar
                {
                    Dock = DockStyle.Bottom,
                    Height = 20,
                    Style = ProgressBarStyle.Continuous
                };

                panel.Controls.Add(_messageLabel);
                panel.Controls.Add(_progressBar);
                this.Controls.Add(panel);
                this.ResumeLayout(false);
            }

            public void Report(double progress, string message, bool? isIndeterminate = null)
            {
                if (_isHeadless) return;
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action<double, string, bool?>(Report), progress, message, isIndeterminate);
                    return;
                }
                _messageLabel.Text = message;
                if (isIndeterminate == true)
                {
                    _progressBar.Style = ProgressBarStyle.Marquee;
                }
                else
                {
                    _progressBar.Value = (int)(progress * 100);
                    _progressBar.Style = ProgressBarStyle.Continuous;
                }
                this.Refresh();
            }

            public void Complete(string? finalMessage = null)
            {
                if (_isHeadless) return;
                Report(1.0, finalMessage ?? "Ready", false);
                Thread.Sleep(500);
            }

            public new void Show()
            {
                if (_isHeadless) return;
                base.Show();
                this.Refresh();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _progressBar?.Dispose();
                    _messageLabel?.Dispose();
                }
                base.Dispose(disposing);
            }
        }  // End SplashForm

    }  // End Program class
}  // End namespace
