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
using System.Net.Http;
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
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;
        // Captured UI thread SynchronizationContext for marshaling UI actions
        public static SynchronizationContext? UISynchronizationContext { get; private set; }
        private static IStartupTimelineService? _timelineService;

        [STAThread]
        static async Task Main(string[] args)
        {
            // ═══════════════════════════════════════════════════════════════════
            // ENHANCED EXCEPTION DIAGNOSTICS
            // ═══════════════════════════════════════════════════════════════════
            // Install FirstChanceException handler FIRST to capture ALL exceptions
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

            // Load .env file FIRST - before any configuration is read
            DotNetEnv.Env.Load("secrets/my.secrets"); // Load secrets first
            DotNetEnv.Env.Load(); // Then load .env (overrides if needed)
            Console.WriteLine("Main method started");

            // Suppress loading of Microsoft.WinForms.Utilities.Shared which is not needed at runtime
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                if (resolveArgs.Name != null && resolveArgs.Name.StartsWith("Microsoft.WinForms.Utilities.Shared", StringComparison.OrdinalIgnoreCase))
                {
                    return null; // Return null to indicate the assembly should not be loaded
                }
                return null;
            };

            // === Phase 1: License Registration (must complete BEFORE Theme Initialization) ===
            IStartupTimelineService? earlyTimeline = null;
            IDisposable? licensePhase = null;

            try
            {
                Console.WriteLine("Phase 1: Registering Syncfusion license...");
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
                    Console.WriteLine("[ERROR] earlyConfig is null - cannot proceed with license registration");
                    throw new InvalidOperationException("Configuration system failed to initialize");
                }

                // DEBUG: Log configuration values (masked)
                var xaiKey = earlyConfig["XAI:ApiKey"];
                var syncKey = earlyConfig["Syncfusion:LicenseKey"];
                Console.WriteLine($"[CONFIG DEBUG] XAI:ApiKey = {(xaiKey != null ? xaiKey.Substring(0, Math.Min(15, xaiKey.Length)) + "..." : "NULL")} ({xaiKey?.Length ?? 0} chars)");
                Console.WriteLine($"[CONFIG DEBUG] Syncfusion:LicenseKey = {(syncKey != null ? syncKey.Substring(0, Math.Min(15, syncKey.Length)) + "..." : "NULL")} ({syncKey?.Length ?? 0} chars)");

                RegisterSyncfusionLicense(earlyConfig);
                Console.WriteLine("Syncfusion license registered successfully");
            }
            finally
            {
                licensePhase?.Dispose(); // Phase 1 complete
                Console.WriteLine("[SYNC] Phase 1 complete - License Registration ready");
                System.Threading.Thread.Sleep(50); // Allow timeline service to process completion
            }

            // FirstChanceException handler already installed at method start

            // === Phase 2: Theme Initialization (MUST be after License, before WinForms) ===
            Console.WriteLine("Phase 2: Applying Office2019 theme");
            using (var themePhase = earlyTimeline?.BeginPhaseScope("Theme Initialization"))
            {
                InitializeTheme();
            } // Phase 2 complete
            Console.WriteLine("[SYNC] Phase 2 complete - Theme initialized and ready");
            System.Threading.Thread.Sleep(100); // Allow theme to fully propagate

            // MUST happen after Theme, before ANY Form/Window creation
            Console.WriteLine("Phase 3: Calling InitializeWinForms");
            using (var winformsPhase = earlyTimeline?.BeginPhaseScope("WinForms Initialization"))
            {
                InitializeWinForms();
            } // Phase 3 complete
            Console.WriteLine("[SYNC] Phase 3 complete - WinForms Initialization ready");
            System.Threading.Thread.Sleep(50); // Allow WinForms subsystem to stabilize
            Console.WriteLine("InitializeWinForms completed");

            // === Phase 4: Splash Screen (depends on WinForms Initialization) ===
            Console.WriteLine("Phase 4: Capturing SynchronizationContext");
            using (var splashSetupPhase = earlyTimeline?.BeginPhaseScope("Splash Screen"))
            {
                CaptureSynchronizationContext();
            } // Phase 4 complete
            Console.WriteLine("[SYNC] Phase 4 complete - SynchronizationContext captured");
            System.Threading.Thread.Sleep(50); // Ensure context is fully captured
            Console.WriteLine("CaptureSynchronizationContext completed");

            // License already registered above
            // Show splash screen on main thread to avoid cross-thread handle issues
            SplashForm? splash = null;

            Console.WriteLine("[SPLASH] About to create SplashForm instance");
            Log.Information("[SPLASH] Starting splash screen initialization");

            // ═══════════════════════════════════════════════════════════════════
            // COMPREHENSIVE STARTUP TRY-CATCH WRAPPER
            // Captures all exceptions during startup with full diagnostic details
            // ═══════════════════════════════════════════════════════════════════
            try
            {
                // Create splash on main UI thread
                Console.WriteLine("[SPLASH] Creating new SplashForm()");
                splash = new SplashForm();
                Console.WriteLine("[SPLASH] SplashForm created");
                Console.WriteLine("[SPLASH] splash.Show() completed");

                Application.DoEvents(); // Process show event
                Console.WriteLine("[SPLASH] First Application.DoEvents() after Show() completed");

                // === Phase 5: DI Container Build (must complete BEFORE DI Validation and DB Health Check) ===
                splash.Report(0.05, "Building dependency injection container...");
                IHost host;

                Console.WriteLine("Phase 5: Building DI Container...");
                using (var diContainerPhase = earlyTimeline?.BeginPhaseScope("DI Container Build"))
                {
                    var hostBuildScope = System.Diagnostics.Stopwatch.StartNew();
                    host = BuildHost(args);
                    hostBuildScope.Stop();
                    Console.WriteLine($"[TIMING] DI Container Build: {hostBuildScope.ElapsedMilliseconds}ms");
                } // Phase 5 complete - EXPLICIT COMPLETION MARKER
                Console.WriteLine("[SYNC] Phase 5 complete - DI Container ready");
                Application.DoEvents(); // Process any pending UI messages
                System.Threading.Thread.Sleep(100); // Allow DI container to fully initialize

                using var uiScope = host.Services.CreateScope();
                Services = uiScope.ServiceProvider;

                // Get timeline service from built DI container
                _timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(Services);
                if (_timelineService != null && _timelineService.IsEnabled)
                {
                    Log.Information("[TIMELINE] StartupTimelineService enabled - tracking startup phases");

                    // Retroactively record early phases that completed before DI container was available
                    if (earlyTimeline != _timelineService)
                    {
                        // If we had a different early timeline, mark its phases as complete in the main timeline
                        _timelineService.RecordOperation("Register Syncfusion license", "License Registration");
                        _timelineService.RecordOperation("Load Office2019Theme assembly and set global theme", "Theme Initialization");
                        _timelineService.RecordOperation("Enable visual styles and set DPI mode", "WinForms Initialization");
                        _timelineService.RecordOperation("Capture UI SynchronizationContext", "Splash Screen");
                        _timelineService.RecordOperation("Build host and create DI container", "DI Container Build");
                        Console.WriteLine("[SYNC] Retroactive phases recorded in timeline service");
                        System.Threading.Thread.Sleep(50); // Allow timeline service to process recorded operations
                    }
                }

                // DEBUG: Check main configuration
                var mainConfig = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(host.Services);
                var mainXaiKey = mainConfig["XAI:ApiKey"];
                Console.WriteLine($"[MAIN CONFIG DEBUG] XAI:ApiKey = {(mainXaiKey != null ? mainXaiKey.Substring(0, Math.Min(15, mainXaiKey.Length)) + "..." : "NULL")} ({mainXaiKey?.Length ?? 0} chars)");

                // === Phase 6: DI Validation (depends on DI Container Build completing first) ===
                // CRITICAL: Validate critical services are registered in DI before proceeding
                // Run on background thread to avoid blocking UI
                splash.Report(0.10, "Validating service registration...");
                Application.DoEvents(); // Keep UI responsive
                using (var validationScope = _timelineService?.BeginPhaseScope("DI Validation"))
                {
                    await Task.Run(() => ValidateCriticalServices(uiScope.ServiceProvider));
                } // Phase 6 complete
                Console.WriteLine("[SYNC] Phase 6 complete - DI Validation passed");
                Application.DoEvents(); // Process any pending UI messages
                System.Threading.Thread.Sleep(100); // Allow services to stabilize

                // === Phase 6: Theme Initialization - DEFERRED ===
                // Theme initialization moved to Phase 12a (after all DI setup, before MainForm creation)
                // This prevents NullReferenceException from premature WinForms control access
                splash.Report(0.15, "Preparing theme system...");
                Application.DoEvents();
                Console.WriteLine("[SYNC] Phase 6 complete - Theme initialization deferred to Phase 12a");
                System.Threading.Thread.Sleep(50);

                // === Phase 7: Configure Error Reporting (depends on Theme Initialization) ===
                splash.Report(0.40, "Configuring error reporting...");
                _timelineService?.RecordOperation("Configure error reporting", "Error Handlers");
                ConfigureErrorReporting();
                Console.WriteLine("[SYNC] Phase 7 complete - Error reporting configured");
                Application.DoEvents(); // Process any pending UI messages
                System.Threading.Thread.Sleep(50); // Allow error handlers to wire up

                // === Phase 8: Database Health Check (depends on DI Container Build completing first) ===
                // Startup health check - run synchronously for proper ordering
                Log.Information("[DIAGNOSTIC] Starting health check phase");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Starting health check phase");
                splash.Report(0.50, "Verifying database connectivity...");
                Application.DoEvents(); // Keep UI responsive
                using (var healthScope = host.Services.CreateScope())
                {
                    using (var healthPhaseScope = _timelineService?.BeginPhaseScope("Database Health Check"))
                    {
                        Log.Information("[DIAGNOSTIC] Calling RunStartupHealthCheckAsync");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Calling RunStartupHealthCheckAsync");
                        splash.Report(0.60, "Checking database health...");
                        Application.DoEvents(); // Keep splash responsive

                        // Run async without blocking UI thread
                        await Task.Run(async () => await RunStartupHealthCheckAsync(healthScope.ServiceProvider));

                        Log.Information("[DIAGNOSTIC] RunStartupHealthCheckAsync completed successfully");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] RunStartupHealthCheckAsync completed");
                    } // Phase 8 complete
                    Console.WriteLine("[SYNC] Phase 8 complete - Database health verified");
                    Application.DoEvents(); // Process any pending UI messages
                }

                Log.Information("[DIAGNOSTIC] Checking IsVerifyStartup at line 121");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Checking IsVerifyStartup");
                if (IsVerifyStartup(args))
                {
                    Log.Information("[DIAGNOSTIC] IsVerifyStartup=true, running verify-startup mode");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Running verify-startup mode");
                    splash.Complete("Startup verification complete");
                    await RunVerifyStartup(host);
                    return; // Exit Main() after verification
                }
                Log.Information("[DIAGNOSTIC] IsVerifyStartup=false, continuing normal startup");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Continuing normal startup");

                // === Phase 9: MainForm Creation (must complete BEFORE Chrome Init and Data Prefetch) ===
                Log.Information("[DIAGNOSTIC] Creating MainForm");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Creating MainForm");
                splash.Report(0.75, "Initializing main window...");
                Application.DoEvents(); // Keep UI responsive
                Console.WriteLine("Creating MainForm...");
                MainForm mainForm;
                using (var mainFormScope = _timelineService?.BeginPhaseScope("MainForm Creation"))
                {
                    mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(Services);
                } // Phase 9 complete - now Chrome Init and Data Prefetch can start
                Console.WriteLine("[SYNC] Phase 9 complete - MainForm created and ready");
                Application.DoEvents(); // Process MainForm initialization events
                System.Threading.Thread.Sleep(150); // Allow MainForm controls to initialize

                // === Phase 10: Chrome Initialization (depends on MainForm Creation) ===
                Log.Information("[DIAGNOSTIC] Wiring exception handlers");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Wiring exception handlers");
                splash.Report(0.80, "Wiring global exception handlers...");
                Application.DoEvents(); // Keep UI responsive
                using (var handlerScope = _timelineService?.BeginPhaseScope("Chrome Initialization"))
                {
                    WireGlobalExceptionHandlers();
                } // Phase 10 complete
                Console.WriteLine("[SYNC] Phase 10 complete - Exception handlers ready");
                Application.DoEvents(); // Process any pending UI messages
                System.Threading.Thread.Sleep(50); // Allow exception handlers to stabilize
                Log.Information("[DIAGNOSTIC] Exception handlers wired successfully");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Exception handlers wired");
                Log.Information("[DIAGNOSTIC] MainForm created successfully");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] MainForm created successfully");
                Console.WriteLine("MainForm created successfully");

                // === Phase 11: Data Prefetch (depends on MainForm Creation) ===
                // Data seeding happens AFTER MainForm creation to respect dependencies
                Log.Information("[DIAGNOSTIC] Starting seeding phase");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Starting seeding phase (synchronous)");
                splash.Report(0.85, "Seeding test data...");
                Application.DoEvents(); // Keep UI responsive
                using (var dataPrefetchScope = _timelineService?.BeginPhaseScope("Data Prefetch"))
                {
                    try
                    {
                        Log.Information("[DIAGNOSTIC] Seeding task started");
                        splash.Report(0.85, "Seeding test data...");
                        Application.DoEvents(); // Keep splash responsive

                        // Run async without blocking UI thread
                        await Task.Run(async () => await UiTestDataSeeder.SeedIfEnabledAsync(host.Services));

                        Log.Information("Test data seeding completed successfully");
                    }
                    catch (Exception seedEx)
                    {
                        Log.Warning(seedEx, "Test data seeding failed (non-critical)");
                    }
                } // Phase 11 complete
                Console.WriteLine("[SYNC] Phase 11 complete - Data seeding finished");
                Application.DoEvents(); // Process any pending UI messages
                System.Threading.Thread.Sleep(100); // Allow data operations to complete

                // === Phase 12a: Theme Initialization (Safe Point) ===
                // Apply theme AFTER full DI setup but BEFORE MainForm creation
                // This is the safest point per Syncfusion best practices
                splash.Report(0.92, "Applying global theme...");
                Application.DoEvents();
                using (var finalThemeScope = _timelineService?.BeginPhaseScope("Theme Initialization"))
                {
                    try
                    {
                        Console.WriteLine("[PHASE 12a] Applying global Syncfusion theme at safe point");

                        // Load required theme assembly
                        SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                        Console.WriteLine("[PHASE 12a] Office2019Theme assembly loaded");

                        // Apply theme from configuration (fallback to Office2019Colorful)
                        var config = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(Services);
                        var themeName = config["UI:Theme"] ?? "Office2019Colorful";
                        SkinManager.ApplicationVisualTheme = themeName;

                        Console.WriteLine($"[PHASE 12a] Global theme '{themeName}' applied via SkinManager");
                        Log.Information("Global Syncfusion theme '{ThemeName}' applied at final safe point", themeName);
                    }
                    catch (Exception themeEx)
                    {
                        Console.WriteLine($"[PHASE 12a ERROR] Theme application failed: {themeEx.Message}");
                        Log.Warning(themeEx, "Theme initialization at safe point failed - continuing with default styling");
                    }
                } // Phase 12a complete
                Console.WriteLine("[SYNC] Phase 12a complete - Theme applied (or defaulted)");
                Application.DoEvents();
                System.Threading.Thread.Sleep(100); // Allow theme to propagate

                // === Phase 12: Splash Screen Hide ===
                Log.Information("[DIAGNOSTIC] Completing splash screen");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Completing splash screen");
                splash.Report(0.95, "Ready");
                Application.DoEvents();
                using (var splashHideScope = _timelineService?.BeginPhaseScope("Splash Screen Hide"))
                {
                    splash.Complete("Ready");
                    Application.DoEvents(); // Process complete message

                    // Brief delay to show "Ready" message (under 300ms threshold)
                    System.Threading.Thread.Sleep(200); // Reduced from 500ms to avoid threshold warning
                    splash.Dispose();
                } // Phase 12 complete
                Console.WriteLine("[SYNC] Phase 12 complete - Splash screen hidden");
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

                ScheduleAutoCloseIfRequested(args, mainForm);

                // === CRITICAL: Show MainForm and bring to foreground ===
                Log.Information("[DIAGNOSTIC] Showing MainForm and bringing to foreground");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Showing MainForm");
                mainForm.Show();
                mainForm.BringToFront();
                mainForm.Activate();
                Application.DoEvents(); // Process show events
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] MainForm visible and active");

                Log.Information("[DIAGNOSTIC] Starting UI message loop");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Starting UI message loop");
                Console.WriteLine("Starting application message loop...");
                using (var uiLoopScope = _timelineService?.BeginPhaseScope("UI Message Loop"))
                {
                    RunUiLoop(mainForm);
                }
                Log.Information("[DIAGNOSTIC] UI message loop exited");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] UI message loop exited");
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
                    nreEx.Source ?? "(unknown)",
                    nreEx.TargetSite?.ToString() ?? "(unknown)",
                    nreEx.HResult);

                Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
                Console.WriteLine("║  CRITICAL: NullReferenceException During Startup");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
                Console.WriteLine($"Type:       {nreEx.GetType().FullName}");
                Console.WriteLine($"Message:    {nreEx.Message}");
                Console.WriteLine($"Source:     {nreEx.Source ?? "(unknown)"}");
                Console.WriteLine($"TargetSite: {nreEx.TargetSite?.ToString() ?? "(unknown)"}");
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
                    ex.Source ?? "(unknown)",
                    ex.TargetSite?.ToString() ?? "(unknown)",
                    ex.HResult,
                    ex.InnerException?.ToString() ?? "(none)");

                Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
                Console.WriteLine("║  CRITICAL: Unhandled Exception During Startup");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
                Console.WriteLine($"Type:       {ex.GetType().FullName}");
                Console.WriteLine($"Message:    {ex.Message}");
                Console.WriteLine($"Source:     {ex.Source ?? "(unknown)"}");
                Console.WriteLine($"TargetSite: {ex.TargetSite?.ToString() ?? "(unknown)"}");
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
                Console.WriteLine($"Source:     {nre.Source ?? "(unknown)"}");
                Console.WriteLine($"TargetSite: {nre.TargetSite?.ToString() ?? "(unknown)"}");
                Console.WriteLine($"HResult:    0x{nre.HResult:X8}");
                Console.WriteLine("Stack Trace:");
                Console.WriteLine(nre.StackTrace ?? "(no stack trace available)");
                Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

                // Also log via Serilog if available
                try
                {
                    Log.Warning(nre, "[FIRST CHANCE] NullReferenceException detected at {TargetSite}",
                        nre.TargetSite?.ToString() ?? "(unknown)");
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

        private static void RegisterSyncfusionLicense(IConfiguration configuration)
        {
            try
            {
                var licenseKey = configuration["Syncfusion:LicenseKey"];
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    throw new InvalidOperationException("Syncfusion license key not found in configuration.");
                }
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);

                // Validate the license key
                // bool isValid = Syncfusion.Licensing.SyncfusionLicenseProvider.ValidateLicense(Syncfusion.Licensing.Platform.WindowsForms);
                // if (!isValid)
                // {
                //     throw new InvalidOperationException("Syncfusion license key is invalid or does not match the package versions.");
                // }

                Log.Information("Syncfusion license registered successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register Syncfusion license.");
                throw;
            }
        }

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
        private static void InitializeTheme()
        {
            try
            {
                Console.WriteLine("[THEME] Starting theme initialization...");

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
                    Console.WriteLine("[THEME] Office2019Theme assembly loaded successfully");
                    Log.Information("Syncfusion Office2019Theme assembly loaded successfully");
                }
                catch (Exception loadEx)
                {
                    Console.WriteLine($"[THEME ERROR] Failed to load Office2019Theme assembly: {loadEx.Message}");
                    Console.WriteLine($"[THEME ERROR] StackTrace: {loadEx.StackTrace}");
                    Log.Error(loadEx, "Failed to load Office2019Theme assembly");
                    throw; // Rethrow to outer catch for comprehensive handling
                }

                // Apply global theme - use default from ThemeColors (fallback to Office2019Colorful)
                var themeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                Console.WriteLine($"[THEME] Setting ApplicationVisualTheme to: {themeName}");

                if (_timelineService != null)
                {
                    _timelineService.RecordOperation($"Set ApplicationVisualTheme: {themeName}", "Theme Initialization");
                }

                // CRITICAL: Set ApplicationVisualTheme AFTER assembly load and BEFORE any form creation
                // This must be set after Application.EnableVisualStyles() for proper rendering
                try
                {
                    SkinManager.ApplicationVisualTheme = themeName;  // Global application-wide theme
                    Console.WriteLine($"[THEME] ApplicationVisualTheme set successfully to: {themeName}");
                    Log.Information("Syncfusion theme '{ThemeName}' applied globally via SkinManager", themeName);
                }
                catch (Exception setEx)
                {
                    Console.WriteLine($"[THEME ERROR] Failed to set ApplicationVisualTheme: {setEx.Message}");
                    Console.WriteLine($"[THEME ERROR] StackTrace: {setEx.StackTrace}");
                    Log.Error(setEx, "Failed to set ApplicationVisualTheme to {ThemeName}", themeName);
                    throw; // Rethrow to outer catch for comprehensive handling
                }

                Console.WriteLine($"[THEME] Theme initialization completed successfully");
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

        private static void CaptureSynchronizationContext()
        {
            if (System.Threading.SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
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

            UISynchronizationContext = System.Threading.SynchronizationContext.Current;
        }

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

            // Register a global HttpClient with a sensible default timeout to avoid blocking external calls during startup
            try
            {
                var httpTimeoutSeconds = builder.Configuration.GetValue<int>("HttpClient:TimeoutSeconds", 30);
                // Register a named default HttpClient with configured timeout
                builder.Services.AddHttpClient("WileyWidgetDefault", c => c.Timeout = TimeSpan.FromSeconds(httpTimeoutSeconds));
                Console.WriteLine($"[CONFIG] Registered global (named) HttpClient 'WileyWidgetDefault' with {httpTimeoutSeconds}s timeout");
            }
            catch (Exception httpRegEx)
            {
                Console.WriteLine($"[CONFIG WARNING] Failed to register global HttpClient: {httpRegEx.Message}");
            }

            return builder.Build();
        }

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

            builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());

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
                    Console.WriteLine($"[CONFIG] Loaded development config from: {devConfigPath}");
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

            builder.Configuration.AddEnvironmentVariables();

            // CRITICAL: Expand environment variable placeholders from appsettings.json
            // appsettings.json uses ${VAR_NAME} syntax which .NET doesn't expand automatically
            // Read environment variables and override config values that have ${...} placeholders
            try
            {
                var xaiApiKeyEnv = Environment.GetEnvironmentVariable("XAI_API_KEY");
                Console.WriteLine($"[ENV VAR DEBUG] XAI_API_KEY from environment = {(xaiApiKeyEnv != null ? xaiApiKeyEnv.Substring(0, Math.Min(15, xaiApiKeyEnv.Length)) + "..." : "NULL")} ({xaiApiKeyEnv?.Length ?? 0} chars)");

                var openAiKeyEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                var syncfusionKeyEnv = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                var qboClientIdEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
                var qboClientSecretEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");

                var overrides = new Dictionary<string, string?>();

                if (!string.IsNullOrWhiteSpace(xaiApiKeyEnv))
                {
                    overrides["XAI:ApiKey"] = xaiApiKeyEnv;
                    Console.WriteLine($"[CONFIG] Overriding XAI:ApiKey from environment variable (length: {xaiApiKeyEnv.Length})");
                }
                else
                {
                    Console.WriteLine($"[CONFIG WARNING] XAI_API_KEY environment variable is NULL or empty!");
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
                    Console.WriteLine($"[CONFIG] Added {overrides.Count} configuration overrides via AddInMemoryCollection");

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

        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            try
            {
                // Make Serilog self-logging forward internal errors to stderr so we can diagnose sink failures
                Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine($"[SERILOG] {msg}"));

                // CRITICAL: ALL LOGS go to project root src/logs directory
                var projectRoot = Directory.GetCurrentDirectory();
                var logsPath = Path.Combine(projectRoot, "logs");

                // Always use root logs folder for centralized logging
                Console.WriteLine($"Creating logs directory at: {logsPath}");
                Directory.CreateDirectory(logsPath);

                // Template used by Serilog's rolling file sink (daily rolling uses a date suffix)
                var logFileTemplate = Path.Combine(logsPath, "app-.log");
                Console.WriteLine($"Log file pattern: {logFileTemplate}");

                // Resolve the current daily log file that Serilog will write to for today's date
                // Serilog's daily rolling file uses the yyyyMMdd date format (e.g., app-20251215.log)
                var logFileCurrent = Path.Combine(logsPath, $"app-{DateTime.Now:yyyyMMdd}.log");
                Console.WriteLine($"Current daily log file: {logFileCurrent}");

                // Check for SQL logging override environment variable
                var enableSqlLogging = Environment.GetEnvironmentVariable("WILEYWIDGET_LOG_SQL");
                var sqlLogLevel = string.Equals(enableSqlLogging, "true", StringComparison.OrdinalIgnoreCase)
                    ? Serilog.Events.LogEventLevel.Information
                    : Serilog.Events.LogEventLevel.Warning;

                Console.WriteLine($"SQL logging level: {sqlLogLevel} (WILEYWIDGET_LOG_SQL={enableSqlLogging ?? "not set"})");

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File(logFileTemplate, formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30, fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true, shared: true)
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", sqlLogLevel)
                    .CreateLogger();

                Console.WriteLine("Logging configured successfully");
                // Log both the resolved current file and the template used by the rolling sink so it's clear
                Log.Information("Logging system initialized - writing to {LogPath} (pattern: {LogPattern})", logFileCurrent, logFileTemplate);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"CRITICAL: Failed to configure logging: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"StackTrace: {ex.StackTrace}");

                // Fallback to console-only logging
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                    .WriteTo.Debug(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                    .MinimumLevel.Information()
                    .CreateLogger();


                Console.Error.WriteLine("Logging fallback to console-only mode");
            }
        }

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

        private static void AddDependencyInjection(HostApplicationBuilder builder)
        {
            var diServices = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();

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
                Log.Information("[DIAGNOSTIC] Testing database connectivity with CanConnectAsync (10s timeout)");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Testing database connectivity with CanConnectAsync (10s timeout)");
                var connectTask = dbContext.Database.CanConnectAsync();
                var connectTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var connectCompletedTask = await Task.WhenAny(connectTask, connectTimeoutTask).ConfigureAwait(false);

                if (connectCompletedTask == connectTask)
                {
                    await connectTask.ConfigureAwait(false); // Ensure the task completed successfully
                    Log.Information("Startup health check passed: Database connection successful");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Database CanConnectAsync succeeded");
                }
                else
                {
                    Log.Warning("Database connectivity test timed out after 10 seconds");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Database CanConnectAsync timed out");
                    throw new TimeoutException("Database connectivity test timed out after 10 seconds");
                }

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
                                var completedTask = await Task.WhenAny(statsTask, timeoutTask).ConfigureAwait(false);

                                if (completedTask == statsTask)
                                {
                                    Log.Information("[DIAGNOSTIC] GetDataStatisticsAsync completed, awaiting result");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] GetDataStatisticsAsync completed, awaiting result");
                                    var stats = await statsTask.ConfigureAwait(false);
                                    Log.Information("Diagnostic: Database contains {RecordCount} budget entries (Oldest: {Oldest}, Newest: {Newest})",
                                        stats.TotalRecords,
                                        stats.OldestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                                        stats.NewestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] Stats: {stats.TotalRecords} records");

                                    if (stats.TotalRecords == 0)
                                    {
                                        Log.Warning("Diagnostic: Database has no budget entries. Dashboard will show empty data. Consider running data seeding scripts.");
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
                Log.Warning(ex, "Startup health check failed: Database connection issue");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] RunStartupHealthCheckAsync caught exception: {ex.Message}");
                // Don't throw here, let the app start and log the issue
            }

            Log.Information("[DIAGNOSTIC] RunStartupHealthCheckAsync method exit");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [DIAGNOSTIC] RunStartupHealthCheckAsync method exit");
        }

        private static bool IsVerifyStartup(string[] args)
        {
            return args != null && Array.Exists(args, a => string.Equals(a, "--verify-startup", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task RunVerifyStartup(IHost host)
        {
            // Prevent indefinite startup hang by timing out the StartAsync call
            using var startupCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await host.StartAsync(startupCts.Token).ConfigureAwait(false);
                // Immediately stop after successful start for verification mode
                await host.StopAsync().ConfigureAwait(false);
                Log.CloseAndFlush();
            }
            catch (OperationCanceledException oce)
            {
                Log.Fatal(oce, "Verify-startup timed out after 30 seconds");
                Log.CloseAndFlush();
                throw new InvalidOperationException("Verify-startup timed out", oce);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Verify-startup run failed");
                Log.CloseAndFlush();
                throw new InvalidOperationException("Verify-startup orchestration failed", ex);
            }
        }

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
                catch (Exception reportEx)
                {
                    Console.Error.WriteLine($"Failed to report AppDomain exception to ErrorReportingService: {reportEx}");
                }
            };
        }



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

        private static bool IsAutoCloseAllowed(string[] args)
        {
            if (IsCiEnvironment())
            {
                return true;
            }

            return args != null && Array.Exists(args, a => string.Equals(a, "--force-auto-close", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCiEnvironment()
        {
            return string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
        }

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

        private static void RunUiLoop(Form mainForm)
        {
            try
            {
                Application.Run(mainForm);
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
                Log.CloseAndFlush();
            }
        }

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
                var projectRoot = Directory.GetCurrentDirectory();
                var logPath = Path.Combine(projectRoot, "logs");
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
        internal sealed class SplashForm : IDisposable
        {
            private readonly bool _isHeadless;
            internal readonly Form? _form;
            private readonly Label? _messageLabel;
            private readonly ProgressBar? _progressBar;
            private readonly CancellationTokenSource _cts = new();

            public SplashForm()
            {
                Console.WriteLine("[SPLASH] SplashForm constructor started");

                // Run headless during UI tests or non-interactive contexts
                _isHeadless = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                              || !Environment.UserInteractive;

                Console.WriteLine($"[SPLASH] Headless check: _isHeadless={_isHeadless}");
                Console.WriteLine($"[SPLASH] Environment.UserInteractive={Environment.UserInteractive}");
                Console.WriteLine($"[SPLASH] WILEYWIDGET_UI_TESTS={Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS")}");

                if (_isHeadless)
                {
                    Console.WriteLine("[SPLASH] Running in headless mode - no UI splash will be shown");
                    return;
                }

                Console.WriteLine("[SPLASH] Creating splash form UI elements");

                _form = new Form
                {
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    ShowInTaskbar = false,
                    Width = 480,
                    Height = 140,
                    Text = "Wiley Widget - Loading..."
                };

                var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
                _messageLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    Text = "Initializing...",
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };

                _progressBar = new ProgressBar
                {
                    Dock = DockStyle.Bottom,
                    Height = 20,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Style = ProgressBarStyle.Continuous
                };

                panel.Controls.Add(_messageLabel);
                panel.Controls.Add(_progressBar);
                _form.Controls.Add(panel);

                Console.WriteLine($"[SPLASH] SplashForm UI created successfully. Form handle: {_form.Handle}");
                Console.WriteLine($"[SPLASH] Form properties: Size={_form.Size}, Location={_form.Location}, Visible={_form.Visible}");
            }

            public void Show()
            {
                var logPath = "logs/splash-show-debug.txt";
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Show() called. _isHeadless={_isHeadless}, _form={(_form != null ? "not null" : "null")}\n");

                if (_isHeadless || _form == null)
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Show() exiting early - headless or form is null\n");
                    Console.WriteLine("[SPLASH] Show() exiting early - headless or form is null");
                    return;
                }

                try
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Calling _form.Show()\n");
                    _form.Show();
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] After Show() - Visible={_form.Visible}, IsHandleCreated={_form.IsHandleCreated}\n");

                    _form.Refresh(); // Force immediate paint
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Refresh() completed\n");

                    Application.DoEvents();
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] DoEvents() completed\n");
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Show() exception: {ex.GetType().Name} - {ex.Message}\n");
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] Stack trace: {ex.StackTrace}\n");
                }
            }

            public void Report(double progress, string message, bool isIndeterminate = false)
            {
                if (_isHeadless)
                {
                    try { Console.WriteLine($"{message} ({(int)(progress * 100)}%)"); } catch { }
                    return;
                }

                if (_form == null || _cts.IsCancellationRequested) return;

                try
                {
                    _messageLabel!.Text = message ?? string.Empty;
                    if (isIndeterminate)
                    {
                        _progressBar!.Style = ProgressBarStyle.Marquee;
                    }
                    else
                    {
                        _progressBar!.Style = ProgressBarStyle.Continuous;
                        var percent = (int)(progress * 100.0);
                        percent = Math.Max(0, Math.Min(100, percent));
                        _progressBar.Value = percent;
                    }

                    _form.Refresh(); // Force immediate paint
                    Application.DoEvents(); // Process UI events
                }
                catch
                {
                    // Swallow errors from reporting to avoid breaking startup
                }
            }

            public void Complete(string finalMessage)
            {
                _cts.Cancel(); // Stop further updates to prevent race conditions

                if (_isHeadless)
                {
                    if (!string.IsNullOrEmpty(finalMessage)) Console.WriteLine(finalMessage);
                    return;
                }

                if (_form == null) return;

                try
                {
                    _messageLabel!.Text = finalMessage ?? string.Empty;
                    _progressBar!.Value = _progressBar.Maximum;
                    _form.Refresh();
                    Application.DoEvents();
                }
                catch { }
            }

            public void Dispose()
            {
                try
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _form?.Dispose();
                }
                catch { }
            }
        }

        #region Critical Service Validation

        /// <summary>
        /// Validates all critical DI registrations using the dedicated validation service.
        /// This offloads validation logic to a testable, reusable service.
        /// Logs comprehensive details about each validation category and service registration.
        /// </summary>
        private static void ValidateCriticalServices(IServiceProvider services)
        {
            var startTime = DateTime.Now;
            try
            {
                Log.Information("╔════════════════════════════════════════════════════════════════╗");
                Log.Information("║   COMPREHENSIVE DI VALIDATION - STARTUP VERIFICATION           ║");
                Log.Information("╠════════════════════════════════════════════════════════════════╣");
                Log.Information("║ Timestamp: {Timestamp,-48} ║", startTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                Log.Information("╚════════════════════════════════════════════════════════════════╝");

                Console.WriteLine($"[{startTime:HH:mm:ss.fff}] ╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"[{startTime:HH:mm:ss.fff}] ║   COMPREHENSIVE DI VALIDATION - STARTUP VERIFICATION           ║");
                Console.WriteLine($"[{startTime:HH:mm:ss.fff}] ╚════════════════════════════════════════════════════════════════╝");

                // Use the WinForms-specific validator which provides categorized validation
                var validationService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<IWinFormsDiValidator>(services);

                // Note: The validator itself logs detailed category-by-category progress
                // including the formatted banner output, so we don't duplicate logging here
                var result = validationService.ValidateAll(services);

                var endTime = DateTime.Now;
                var totalDuration = endTime - startTime;

                if (!result.IsValid)
                {
                    Log.Fatal("╔════════════════════════════════════════════════════════════════╗");
                    Log.Fatal("║   ✗ DI VALIDATION FAILED - STARTUP CANNOT PROCEED             ║");
                    Log.Fatal("╠════════════════════════════════════════════════════════════════╣");
                    Log.Fatal("║ Total Errors:   {Count,4}                                          ║", result.Errors.Count);
                    Log.Fatal("║ Total Warnings: {Count,4}                                          ║", result.Warnings.Count);
                    Log.Fatal("║ Duration:       {Duration,4:F0}ms                                    ║", totalDuration.TotalMilliseconds);
                    Log.Fatal("╚════════════════════════════════════════════════════════════════╝");

                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╔════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║   ✗ DI VALIDATION FAILED - STARTUP CANNOT PROCEED             ║");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╠════════════════════════════════════════════════════════════════╣");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║ Total Errors:   {result.Errors.Count,4}                                          ║");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╚════════════════════════════════════════════════════════════════╝");

                    foreach (var error in result.Errors)
                    {
                        Log.Fatal("  ✗ {Error}", error);
                        Console.WriteLine($"[{endTime:HH:mm:ss.fff}]   ✗ {error}");
                    }

                    throw new InvalidOperationException(
                        $"DI Validation failed with {result.Errors.Count} errors:{Environment.NewLine}" +
                        string.Join(Environment.NewLine, result.Errors));
                }

                // SUCCESS PATH - Log comprehensive success details
                Log.Information("╔════════════════════════════════════════════════════════════════╗");
                Log.Information("║   ✓ DI VALIDATION SUCCESSFUL - ALL SERVICES REGISTERED        ║");
                Log.Information("╠════════════════════════════════════════════════════════════════╣");
                Log.Information("║ Services Validated: {Count,4}                                      ║", result.SuccessMessages.Count);
                Log.Information("║ Warnings:           {Count,4}                                      ║", result.Warnings.Count);
                Log.Information("║ Validation Time:    {Duration,4:F0}ms                                ║", result.ValidationDuration.TotalMilliseconds);
                Log.Information("║ Total Startup Time: {Duration,4:F0}ms                                ║", totalDuration.TotalMilliseconds);
                Log.Information("╠════════════════════════════════════════════════════════════════╣");
                Log.Information("║ Categories Validated:                                          ║");
                Log.Information("║   ✓ Critical Services (Configuration, Logging, Telemetry)     ║");
                Log.Information("║   ✓ Repositories (9 data access layers)                       ║");
                Log.Information("║   ✓ Business Services (26 application services)               ║");
                Log.Information("║   ✓ ViewModels (10 UI view models)                            ║");
                Log.Information("║   ✓ MainForm (panels resolved via navigation service)         ║");
                Log.Information("╠════════════════════════════════════════════════════════════════╣");
                Log.Information("║ Result: READY FOR MAIN FORM INSTANTIATION                     ║");
                Log.Information("╚════════════════════════════════════════════════════════════════╝");

                Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║   ✓ DI VALIDATION SUCCESSFUL - ALL SERVICES REGISTERED        ║");
                Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╠════════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║ Services Validated: {result.SuccessMessages.Count,4}                                      ║");
                Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║ Validation Time:    {result.ValidationDuration.TotalMilliseconds,4:F0}ms                                ║");
                Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║ Total Startup Time: {totalDuration.TotalMilliseconds,4:F0}ms                                ║");
                Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╚════════════════════════════════════════════════════════════════╝");

                // Log any warnings if present
                if (result.Warnings.Count > 0)
                {
                    Log.Warning("DI Validation Warnings ({Count}):", result.Warnings.Count);
                    foreach (var warning in result.Warnings)
                    {
                        Log.Warning("  ⚠ {Warning}", warning);
                        Console.WriteLine($"[{endTime:HH:mm:ss.fff}]   ⚠ {warning}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "╔════════════════════════════════════════════════════════════════╗");
                Log.Fatal("║   ✗ CRITICAL FAILURE DURING DI VALIDATION                      ║");
                Log.Fatal("╠════════════════════════════════════════════════════════════════╣");
                Log.Fatal("║ Exception Type: {Type,-44} ║", ex.GetType().Name);
                Log.Fatal("║ Exception Msg:  {Message,-44} ║", ex.Message.Length > 44 ? ex.Message.Substring(0, 41) + "..." : ex.Message);
                Log.Fatal("╚════════════════════════════════════════════════════════════════╝");

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✗ CRITICAL: DI validation failed: {ex.Message}");
                throw;
            }
        }
        #endregion
    }
}
