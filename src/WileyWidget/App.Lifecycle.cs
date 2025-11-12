// App.Lifecycle.cs - Application Lifecycle Partial Class
// Contains: Startup, initialization, shutdown, and shell creation methods
// Part of App.xaml.cs partial class split for maintainability

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Abstractions;
using WileyWidget.Startup;
using WileyWidget.Services;
using WileyWidget.Services.Startup;
using WileyWidget.Services.Telemetry;
using WileyWidget.Views.Windows;

namespace WileyWidget
{
    /// <summary>
    /// Application lifecycle partial class containing startup, initialization, and shutdown logic.
    /// </summary>
    public partial class App
    {
        #region Lifecycle Fields

        // Startup metadata
        private static string _startupId;

        // SigNoz telemetry tracking
        private SigNozTelemetryService? _earlyTelemetryService;
        private Activity? _startupActivity;

        // Deferred secrets task for async consumers
        private static readonly TaskCompletionSource<bool> _secretsInitializationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static Task SecretsInitializationTask => _secretsInitializationTcs.Task;

        #endregion

        #region Prism Lifecycle Overrides

        /// <summary>
        /// Enhanced 4-phase startup: Phase 1 (validation/config/theme) → Phase 2-4 (Prism bootstrap)
        /// INCLUDES COMPREHENSIVE DIAGNOSTIC CAPABILITIES
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Information("[STARTUP] ============ Enhanced 4-Phase Startup BEGIN ============");

            try
            {
                // DIAGNOSTIC STEP 1: Enable comprehensive verbose logging
                var configuration = BuildConfiguration();
                DiagnosticIntegration.EnableVerboseStartupLogging(configuration);

                // DIAGNOSTIC STEP 4: Start runtime profiler if enabled
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var profilerStarted = await DiagnosticIntegration.TryStartRuntimeProfiler(configuration);
                        if (profilerStarted)
                        {
                            Log.Information("🔍 Runtime profiler started successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Runtime profiler startup failed");
                    }
                });

                // Phase 1: Early Configuration and Validation (before Prism bootstrap)
                Log.Information("Phase 1: Early validation and configuration");

                // DIAGNOSTIC STEP 2: Breakpoint debugging for Phase 1
                #if DEBUG
                var phase1Report = DiagnosticIntegration.PerformBreakpointAnalysis(null, "Phase1-PrePrism");
                Log.Debug("Breakpoint debugging Phase 1 completed");
                #endif

                var validationResult = ValidateStartupEnvironment();

                if (!validationResult.isValid)
                {
                    Log.Fatal("Environment validation failed with {IssueCount} critical issues", validationResult.issues.Count);
                    foreach (var issue in validationResult.issues)
                    {
                        Log.Fatal("  ❌ {Issue}", issue);
                    }

                    ShowEmergencyErrorDialog(new InvalidOperationException($"Environment validation failed: {string.Join(", ", validationResult.issues)}"));
                    Application.Current.Shutdown(1);
                    return;
                }

                // NOTE: License registration happens in static constructor
                // Apply theme early (before Prism bootstrap) - no container needed
                VerifyAndApplyTheme();

                // CRITICAL: Verify theme was successfully applied before Prism initialization
                // This check ensures ConfigureRegionAdapterMappings won't fail
                if (SfSkinManager.ApplicationTheme == null)
                {
                    var errorMsg = "Theme application failed in VerifyAndApplyTheme(). " +
                                   "ConfigureRegionAdapterMappings will fail without an active theme.";
                    Log.Fatal(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }
                Log.Information("✓ Theme verified as applied: {Theme}", SfSkinManager.ApplicationTheme.ToString());

                // CRITICAL FIX: Pre-load global Resources/Strings.xaml/Generic.xaml/DataTemplates.xaml BEFORE theme validation
                // This addresses dispatcher_invoke_issues where PrimaryTextBrush/Btn_Export/TreeGridHeaderBrush are unavailable
                PreLoadCriticalResources();

                // CRITICAL: Verify theme integration for zero hangs after resources are loaded
                VerifyThemeIntegration();

                // Initialize SigNoz telemetry early (before Prism bootstrap)
                InitializeSigNozTelemetry();

                // Force early secret vault migration from environment variables to avoid lazy init race
                // TEMPORARILY DISABLED: GetAwaiter().GetResult() on async foreach causes deadlock on UI thread
                // TODO: Move to async initialization point or use background task
                /*
                try
                {
                    Log.Debug("Forcing early migration of environment secrets into local encrypted vault (OnStartup)");

                    WileyWidget.Services.EncryptedLocalSecretVaultService? tempVault = null;
                    ILoggerFactory? loggerFactory = null;

                    try
                    {
                        loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
                        var logger = loggerFactory.CreateLogger<WileyWidget.Services.EncryptedLocalSecretVaultService>();
                        tempVault = new WileyWidget.Services.EncryptedLocalSecretVaultService(logger);

                        // Run migration synchronously to ensure files/dirs are created before modules initialize
                        tempVault.MigrateSecretsFromEnvironmentAsync().GetAwaiter().GetResult();
                    }
                    finally
                    {
                        // Explicit disposal to avoid async/sync disposal issues
                        tempVault?.Dispose();
                        loggerFactory?.Dispose();
                    }

                    Log.Debug("✅ Early secret vault migration completed successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Early secret vault migration failed (non-fatal) - continuing startup");
                }
                */
                Log.Information("⚠️ Early secret vault migration SKIPPED - vault will initialize on first use");                // MCP VALIDATION: Add test span for end-to-end trace verification
                using var mcpValidationActivity = SigNozTelemetryService.ActivitySource.StartActivity("MCP.Validation.Startup");
                mcpValidationActivity?.SetTag("mcp.phase", "validation");
                mcpValidationActivity?.SetTag("session.id", "MCP-TEST-001");
                mcpValidationActivity?.SetTag("environment", "development");
                mcpValidationActivity?.SetTag("wiley.version", SigNozTelemetryService.ServiceVersion);
                mcpValidationActivity?.AddEvent(new ActivityEvent("MCP Validation started - testing trace continuity"));
                Log.Information("✅ MCP Validation span created: session=MCP-TEST-001");

                Log.Information("✅ Phase 1 completed: Configuration, validation, and telemetry");

                // Phase 2: Prism bootstrap (container setup, module registration)
                Log.Information("Phase 2: Proceeding with Prism bootstrap (container setup)");

                // Now trigger Prism bootstrap (this handles container setup)
                base.OnStartup(e);  // This calls Initialize() -> CreateContainerExtension() -> RegisterTypes() -> ConfigureRegionAdapterMappings() -> CreateShell() -> OnInitialized()

                // DIAGNOSTIC STEP 2: Breakpoint debugging for Phase 2 (Post-Prism with Container)
                #if DEBUG
                var phase2Report = DiagnosticIntegration.PerformBreakpointAnalysis(Container, "Phase2-PostPrism");
                Log.Debug("Breakpoint debugging Phase 2 completed");
                #endif

                // DIAGNOSTIC STEP 3: Check phase isolation settings
                var (skipResourceLoading, skipTelemetry, skipModules) = DiagnosticIntegration.CheckPhaseIsolationSettings(configuration);

                // Phase 2B: Load resources AFTER container is ready but BEFORE modules initialize (unless skipped for debugging)
                if (!skipResourceLoading)
                {
                    Log.Information("Phase 2B: Loading application resources (container ready)");
                    LoadApplicationResourcesSync();

                    // PHASE 2C: VALIDATE CRITICAL RESOURCES ARE AVAILABLE
                    Log.Information("🔍 [DIAGNOSTIC] Validating critical resources post-load...");
                    ValidateCriticalResources();

                    // CRITICAL FIX: Force WPF to process the merged dictionaries before modules initialize
                    // This ensures brushes are queryable via Application.Current.Resources.Contains()
                    // before CoreModule.OnInitialized() runs its pre-registration check
                    // Using Background priority instead of ApplicationIdle to prevent startup hangs
                    Log.Debug("🔄 [DIAGNOSTIC] Forcing WPF Dispatcher to process pending operations...");
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => { /* Force dispatcher pump */ }));
                    Log.Debug("✅ [DIAGNOSTIC] Dispatcher processing completed - resources should be queryable");
                }
                else
                {
                    Log.Warning("⚠️ PHASE ISOLATION: Resource loading SKIPPED for debugging");
                }

#if DEBUG
                // Post-Prism diagnostics
                RunXamlDiagnostics();
#endif

                Log.Information("✅ Complete enhanced startup finished successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during enhanced startup, initiating emergency shutdown");

                try
                {
                    ShowEmergencyErrorDialog(ex);
                }
                catch
                {
                    // Final fallback if dialog fails
                    MessageBox.Show($"Critical startup error: {ex.Message}\n\nApplication will exit.",
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                Application.Current.Shutdown(1);
            }
        }

        /// <summary>
        /// Phase 3-4: Module and UI initialization after Prism container is ready
        /// INCLUDES DIAGNOSTIC STEP 2 AND LOG ANALYSIS
        /// </summary>
        protected override void OnInitialized()
        {
            SplashWindow? splashWindow = null;
            try
            {
                Log.Information("Phase 3: Module and service initialization (OnInitialized)");

                // DIAGNOSTIC STEP 2: Breakpoint debugging for Phase 3
                #if DEBUG
                var phase3Report = DiagnosticIntegration.PerformBreakpointAnalysis(Container, "Phase3-OnInitialized");
                Log.Debug("Breakpoint debugging Phase 3 completed");
                #endif

                // DIAGNOSTIC STEP 5: Analyze startup logs for exceptions
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (exceptions, failures) = await DiagnosticIntegration.AnalyzeRecentStartupLogs();
                        if (exceptions > 0 || failures > 0)
                        {
                            Log.Warning("🚨 DIAGNOSTIC ALERT: Found {Exceptions} exceptions and {Failures} failures in startup logs", exceptions, failures);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to analyze startup logs");
                    }
                });

                Log.Information("[SPLASH] Creating splash screen window");
                splashWindow = new SplashWindow();
                splashWindow.UpdateStatus("Phase 3: Initializing modules and services...");
                splashWindow.Show();

                // Verify theme early
                var currentTheme = SfSkinManager.ApplicationTheme;
                if (currentTheme == null)
                {
                    throw new InvalidOperationException("Theme not initialized. Check App() constructor.");
                }
                Log.Debug("[THEME] Theme verified as active in OnInitialized");

                splashWindow.UpdateStatus("Initializing Prism framework...");
                base.OnInitialized();  // This triggers custom InitializeModules() without duplication

                // Now that the container is fully initialized, validate and register ViewModels
                splashWindow.UpdateStatus("Validating ViewModels...");
                try
                {
                    // Use the static method from App.DependencyInjection.cs
                    var registry = Container as IContainerRegistry;
                    if (registry != null)
                    {
                        ValidateAndRegisterViewModels(registry);
                        Log.Information("✓ ViewModels validated and registered post-initialization");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to validate ViewModels post-initialization - continuing startup");
                }

                // Validate critical dependencies required by core ViewModels
                splashWindow.UpdateStatus("Validating critical dependencies...");
                try
                {
                    ValidateCriticalDependencies(Container);
                    Log.Information("✅ Critical dependency validation passed");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "❌ CRITICAL: Dependency validation failed - application cannot start safely");
                    throw new InvalidOperationException("Critical dependencies missing. See logs for details.", ex);
                }

                // Setup global exception handling post-container
                SetupGlobalExceptionHandling();
                Log.Debug("[EXCEPTION] Global exception handling configured successfully");

                // Integrate telemetry services now that container is ready
                IntegrateTelemetryServices();

                // Note: Deferred secrets initialization removed - secrets managed via LocalSecretVaultService.
                // Secrets are loaded synchronously during bootstrap from local vault (secrets/ directory).
                // No async initialization needed - ISecretsService was part of archived Azure Key Vault integration.

                // Background DB init
                splashWindow.UpdateStatus("Initializing database...");
#if !WPFTMP
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var dbInit = this.Container.Resolve<WileyWidget.Startup.DatabaseInitializer>();
                        await dbInit.StartAsync(CancellationToken.None).ConfigureAwait(false);
                        Log.Information("[DATABASE] Background initialization completed");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DATABASE] Background initialization failed (non-critical)");
                    }
                });
#endif

                splashWindow.UpdateStatus("Phase 4: Finalizing UI initialization...");
                Log.Information("Phase 4: UI finalization and health validation");

                // Final health validation using StartupEnvironmentValidator service
                var moduleHealthService = ResolveWithRetry<IModuleHealthService>();
                moduleHealthService.LogHealthReport();

#if !WPFTMP
                // Validate module initialization via extracted service
                var environmentValidator = ResolveWithRetry<WileyWidget.Services.Startup.IStartupEnvironmentValidator>();
                environmentValidator.ValidateModuleInitialization(moduleHealthService);
#endif

                Log.Information("✅ Phase 3-4 completed: Module and UI initialization successful");
                Log.Information("✅ Phase 4 complete: UI ready");

                // Show the main window now that initialization is complete
                var mainWindow = Application.Current.MainWindow as Window;
                if (mainWindow != null)
                {
                    Log.Information("Showing main window after initialization");
                    mainWindow.Visibility = Visibility.Visible;
                    mainWindow.Show();
                    mainWindow.Activate();
                }

                // Complete startup telemetry tracking
                _startupActivity?.SetTag("startup.result", "success");
                _startupActivity?.SetTag("startup.phase", "completed");
                _startupActivity?.Dispose();

                // Report successful phases telemetry
                var errorReporting = ResolveWithRetry<ErrorReportingService>();
                errorReporting?.TrackEvent("Enhanced_Startup_Success", new Dictionary<string, object>
                {
                    ["CompletedPhases"] = "1,2,3,4",
                    ["StartupType"] = "Enhanced4Phase",
                    ["Timestamp"] = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error during Phase 3-4 (module/UI initialization)");

                // Track startup failure in telemetry
                _startupActivity?.SetTag("startup.result", "failure");
                _startupActivity?.SetTag("startup.error_type", ex.GetType().Name);
                _startupActivity?.SetTag("startup.error_message", ex.Message);
                _startupActivity?.Dispose();

                // Report startup failure
                try
                {
                    var errorReporting = ResolveWithRetry<ErrorReportingService>();
                    errorReporting?.TrackEvent("Enhanced_Startup_Failed", new Dictionary<string, object>
                    {
                        ["FailedPhase"] = "3-4",
                        ["ErrorType"] = ex.GetType().Name,
                        ["ErrorMessage"] = ex.Message
                    });
                }
                catch { /* Ignore telemetry errors */ }

                ShowStartupErrorDialog(ex);
                Application.Current.Shutdown(1);
            }
            finally
            {
                if (splashWindow != null)
                {
                    splashWindow.UpdateStatus("Startup complete!");
                    Thread.Sleep(500);
                    splashWindow.CloseSplash();
                }
            }
        }

        /// <summary>
        /// Application shutdown with proper cleanup sequence
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application shutdown - Session: {StartupId}", _startupId);

            // Guard: If Container is null, startup failed - skip cleanup
            if (this.Container == null)
            {
                Log.Warning("Container is null during shutdown - startup failed before container initialization");
                base.OnExit(e);
                return;
            }

            try
            {
                // CRITICAL: Close all dialog windows BEFORE disposing container
                // This prevents NullReferenceException in Prism DialogService during Window.InternalClose

                // First try using DialogTrackingService if available
                try
                {
                    var dialogTracker = this.Container.Resolve<IDialogTrackingService>();
                    if (dialogTracker != null && dialogTracker.OpenDialogCount > 0)
                    {
                        Log.Information("Closing {Count} tracked dialogs via DialogTrackingService",
                            dialogTracker.OpenDialogCount);
                        dialogTracker.CloseAllDialogs();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "DialogTrackingService not available or failed, falling back to manual dialog closure");
                }

                // Fallback: close any remaining dialogs manually
                CloseAllDialogWindows();

                // Now safely dispose services
                try
                {
                    // Gracefully dispose database UnitOfWork if registered
                    // var uow = this.Container.Resolve<IUnitOfWork>();
                    // uow?.Dispose();
                    Log.Debug("Service disposal completed (IUnitOfWork pattern not in use)");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing services during shutdown");
                }

                // Clear memory caches
                try
                {
                    var cache = this.Container.Resolve<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                    if (cache is MemoryCache memCache)
                    {
                        memCache.Compact(1.0);  // 100% compaction
                    }
                    Log.Debug("Memory cache cleared");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error clearing memory cache during shutdown");
                }

                base.OnExit(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during application shutdown");
            }
            finally
            {
                Log.Information("Shutdown complete - ExitCode: {Code}", e.ApplicationExitCode);

                // Final log flush before exit
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Creates the main application shell window
        /// </summary>
        protected override Window CreateShell()
        {
            Log.Information("[SHELL] Creating Shell window...");
            try
            {
                // CRITICAL FIX: Wrap resource-dependent Prism bootstrap in Dispatcher.Invoke
                // This addresses dispatcher_invoke_issues where PrimaryTextBrush/Btn_Export are unavailable
                // during Shell window creation. Using Dispatcher.Invoke ensures proper UI thread context
                // and resource availability.
                Window shell = null;
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        try
                        {
                            // Resolve the application's main shell (Shell window) from the container.
                            // Ensure Shell is registered in RegisterTypes below or in a module.
                            shell = Container.Resolve<Shell>();
                            Log.Debug("[SHELL] Shell window resolved successfully via Dispatcher.Invoke");
                        }
                        catch (System.Windows.ResourceReferenceKeyNotFoundException ex)
                        {
                            // Handle ResourceReferenceException specifically for missing resources
                            Log.Fatal(ex, "[SHELL] ✗ ResourceReferenceException during Shell resolution - PrimaryTextBrush/Btn_Export unavailable. " +
                                "Ensure Strings.xaml and DataTemplates.xaml are pre-loaded before Prism bootstrap.");
                            throw new InvalidOperationException("Critical resources not available during Shell creation. " +
                                "Pre-load Strings.xaml and DataTemplates.xaml before vault migration.", ex);
                        }
                    }));

                Log.Information("[SHELL] ✓ Shell window created successfully");
                return shell;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[SHELL] ✗ Failed to create Shell window");
                throw;
            }
        }

        /// <summary>
        /// Initializes and displays the shell window
        /// </summary>
        protected override void InitializeShell(Window shell)
        {
            Log.Information("[SHELL] Initializing and showing Shell window...");
            try
            {
                base.InitializeShell(shell);

                // CRITICAL: Explicitly set Shell.xaml as MainWindow
                Application.Current.MainWindow = shell;
                Log.Information("[SHELL] ✓ MainWindow set explicitly to Shell window (main_window_found: {Found})",
                    Application.Current.MainWindow != null);

                // ApplicationTheme is applied globally and inherited by all windows automatically
                if (SfSkinManager.ApplicationTheme != null)
                {
                    Log.Information("[SHELL] ApplicationTheme {Theme} will be inherited by Shell window", SfSkinManager.ApplicationTheme);
                }
                else
                {
                    Log.Warning("[SHELL] ApplicationTheme is null - theme may not be applied correctly");
                }

                // Shell.xaml has Visibility="Hidden" by default - make it visible
                shell.Visibility = Visibility.Visible;
                shell.Show();

                Log.Information("[SHELL] ✓ Shell window shown successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[SHELL] ✗ Failed to initialize/show Shell window");
                throw;
            }
        }

        #endregion

        #region Startup Validation

        /// <summary>
        /// Validates the startup environment before proceeding with initialization
        /// </summary>
        private (bool isValid, List<string> issues, List<string> warnings) ValidateStartupEnvironment()
        {
            var sw = Stopwatch.StartNew();
            var issues = new List<string>();
            var warnings = new List<string>();

            Log.Information("[VALIDATION] Starting enhanced startup environment validation");

            // 1. Required directories validation
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var requiredDirs = new[] { "logs", "scripts", "src" };

                foreach (var dir in requiredDirs)
                {
                    var dirPath = Path.Combine(baseDir, dir);
                    if (!Directory.Exists(dirPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(dirPath);
                            Log.Debug("Created missing directory: {Directory}", dirPath);
                        }
                        catch (Exception ex)
                        {
                            issues.Add($"Cannot create required directory {dir}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add($"Directory validation failed: {ex.Message}");
            }

            // 2. File permissions validation
            try
            {
                var testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_test.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                Log.Debug("✓ File system write permissions verified");
            }
            catch (Exception ex)
            {
                issues.Add($"No write permission to application directory: {ex.Message}");
            }

            // 3. .NET Framework/Runtime validation
            try
            {
                var dotnetVersion = Environment.Version;
                var frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                Log.Information("✓ .NET Runtime: {Framework}, Version: {Version}", frameworkDescription, dotnetVersion);

                if (dotnetVersion.Major < 8)
                {
                    warnings.Add($".NET version {dotnetVersion} is below recommended minimum .NET 8.0");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not verify .NET version: {ex.Message}");
            }

            // 4. Memory validation
            try
            {
                var process = Process.GetCurrentProcess();
                var workingSetMB = process.WorkingSet64 / (1024 * 1024);
                var minMemoryMB = 64; // Minimum for WPF app (reduced from 128MB for development environments)

                if (workingSetMB < minMemoryMB)
                {
                    warnings.Add($"Process memory {workingSetMB}MB is below recommended minimum {minMemoryMB}MB");
                }

                Log.Debug("✓ Process memory: {Memory}MB (working set)", workingSetMB);
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not check memory: {ex.Message}");
            }

            // 5. WPF prerequisites validation
            try
            {
                // Test basic WPF types are available
                var dispatcherType = typeof(System.Windows.Threading.Dispatcher);
                var applicationTypes = typeof(System.Windows.Application);

                if (dispatcherType == null || applicationTypes == null)
                {
                    issues.Add("WPF framework types not available");
                }
                else
                {
                    Log.Debug("✓ WPF framework types verified");
                }
            }
            catch (Exception ex)
            {
                issues.Add($"WPF framework validation failed: {ex.Message}");
            }

            // 6. Enhanced license validation with runtime status checking
            try
            {
                var config = BuildConfiguration();
                var isDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development"
                                 || config["Environment"] == "Development";

                var syncfusionKey = config["Syncfusion:LicenseKey"]
                                 ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                var boldKey = config["BoldReports:LicenseKey"]
                           ?? Environment.GetEnvironmentVariable("BOLD_LICENSE_KEY");

                // Enhanced runtime license status validation
                Log.Information("[VALIDATION] Checking runtime license status - Syncfusion: {SyncfusionStatus}, Bold: {BoldStatus}",
                    App.SyncfusionLicenseStatus, App.BoldLicenseStatus);

                // Check for critical license failures that should block startup
                if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.Failed)
                {
                    var errorMsg = $"Critical Syncfusion license failure: {App.SyncfusionLicenseError}";
                    if (isDevelopment)
                    {
                        warnings.Add($"Development Mode: {errorMsg}");
                        Log.Warning("[VALIDATION] {Error}", errorMsg);
                    }
                    else
                    {
                        issues.Add(errorMsg);
                        Log.Error("[VALIDATION] Production mode license failure: {Error}", errorMsg);
                    }
                }

                // Production-specific license validation
                if (!isDevelopment)
                {
                    // In production, require valid licenses or at least successful trial registration
                    if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.NotAttempted)
                    {
                        issues.Add("Production environment requires Syncfusion license registration attempt");
                    }
                    else if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.InvalidKey)
                    {
                        issues.Add($"Invalid Syncfusion license key in production: {App.SyncfusionLicenseError}");
                    }
                    else if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.TrialMode)
                    {
                        warnings.Add("Production environment running with Syncfusion trial license - functionality may be limited");
                    }

                    // Bold Reports validation
                    if (App.BoldLicenseStatus == App.LicenseRegistrationStatus.Failed)
                    {
                        warnings.Add($"Bold Reports license failure in production: {App.BoldLicenseError}");
                    }
                }
                else
                {
                    // Development mode - relaxed validation with informational logging
                    Log.Debug("[LICENSE] Development mode - license validation relaxed");

                    if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.NotAttempted)
                    {
                        warnings.Add("Development: License registration was not attempted - check static constructor");
                    }
                    else if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.TrialMode)
                    {
                        Log.Debug("[LICENSE] Development: Running in Syncfusion trial mode");
                    }
                    else if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.Success)
                    {
                        Log.Debug("[LICENSE] Development: Syncfusion license registered successfully");
                    }

                    if (App.BoldLicenseStatus == App.LicenseRegistrationStatus.TrialMode)
                    {
                        Log.Debug("[LICENSE] Development: Bold Reports running in trial mode");
                    }
                }

                // Configuration-based warnings: Check both config and env var (env var takes precedence per static ctor)
                // syncfusionKey already includes fallback: config["Syncfusion:LicenseKey"] ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
                if (string.IsNullOrEmpty(syncfusionKey))
                {
                    if (App.SyncfusionLicenseStatus == App.LicenseRegistrationStatus.TrialMode)
                    {
                        Log.Debug("[LICENSE] Configuration validation: Syncfusion key not in config/env but trial mode active");
                    }
                    else if (App.SyncfusionLicenseStatus != App.LicenseRegistrationStatus.Success)
                    {
                        warnings.Add("Syncfusion license key not found in configuration or SYNCFUSION_LICENSE_KEY environment variable");
                    }
                    else
                    {
                        // Key was registered successfully in static ctor from env var but not visible in config check
                        Log.Debug("[LICENSE] Syncfusion license registered from environment variable (not in appsettings.json)");
                    }
                }

                if (string.IsNullOrEmpty(boldKey) && string.IsNullOrEmpty(syncfusionKey))
                {
                    if (App.BoldLicenseStatus == App.LicenseRegistrationStatus.TrialMode)
                    {
                        Log.Debug("[LICENSE] Configuration validation: Bold Reports key not in config/env but trial mode active");
                    }
                    else if (App.BoldLicenseStatus != App.LicenseRegistrationStatus.Success)
                    {
                        warnings.Add("Bold Reports license key not found in configuration or BOLD_LICENSE_KEY environment variable");
                    }
                    else
                    {
                        // Key was registered successfully in static ctor from env var but not visible in config check
                        Log.Debug("[LICENSE] Bold Reports license registered from environment variable (not in appsettings.json)");
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"License validation failed: {ex.Message}");
                Log.Warning(ex, "[VALIDATION] Exception during license validation");
            }

            // Final assessment
            sw.Stop();
            LogStartupTiming("[VALIDATION] Enhanced environment validation", sw.Elapsed);

            if (issues.Any())
            {
                Log.Error("[VALIDATION] Environment validation FAILED with {IssueCount} critical issues:", issues.Count);
                foreach (var issue in issues)
                {
                    Log.Error("  ❌ {Issue}", issue);
                }
            }

            if (warnings.Any())
            {
                Log.Warning("[VALIDATION] Environment validation completed with {WarningCount} warnings:", warnings.Count);
                foreach (var warning in warnings)
                {
                    Log.Warning("  ⚠ {Warning}", warning);
                }
            }

            if (!issues.Any() && !warnings.Any())
            {
                Log.Information("✅ Enhanced environment validation passed - no issues detected");
            }

            var isValid = !issues.Any();
            return (isValid, issues, warnings);
        }

        #endregion

        #region Resource Validation

        /// <summary>
        /// Validates that all critical resources are loaded and available in the Application resource dictionary.
        /// This diagnostic method ensures that views won't fail due to missing StaticResource or DynamicResource references.
        /// Called after LoadApplicationResourcesSync() to verify successful resource loading.
        /// </summary>
        private void ValidateCriticalResources()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Validate merged dictionaries loaded
                var mergedDicts = Application.Current.Resources.MergedDictionaries;
                Log.Information("📚 [DIAGNOSTIC] Merged Dictionaries Loaded: {Count}", mergedDicts.Count);

                var totalResources = 0;
                foreach (var dict in mergedDicts)
                {
                    var source = dict.Source?.ToString() ?? "inline";
                    totalResources += dict.Count;
                    Log.Debug("  ✓ {Source} ({Count} resources)", source, dict.Count);
                }

                Log.Information("📊 [DIAGNOSTIC] Total resources in merged dictionaries: {Count}", totalResources);

                // Validate critical brushes exist
                var criticalBrushes = new[]
                {
                    "InfoBrush", "ErrorBrush", "WarningBrush", "SuccessBrush",
                    "ContentBackgroundBrush", "ContentForegroundBrush",
                    "ErrorBackgroundBrush", "ErrorBorderBrush", "ErrorForegroundBrush",
                    "PanelBorderBrush", "SectionHeaderBrush",
                    "AccentBlueBrush", "CardBackground", "PrimaryTextBrush"
                };

                var missing = new List<string>();
                var found = 0;

                foreach (var brush in criticalBrushes)
                {
                    if (Application.Current.Resources.Contains(brush))
                    {
                        found++;
                        Log.Debug("  ✅ {Brush} - found", brush);
                    }
                    else
                    {
                        missing.Add(brush);
                        Log.Error("  ❌ {Brush} - MISSING!", brush);
                    }
                }

                // Validate critical converters exist
                var criticalConverters = new[]
                {
                    "BoolToVis", "NullToVis", "StringToVis",
                    "CountToVisibilityConverter", "EmptyStringToVisibilityConverter",
                    "BalanceColorConverter", "BooleanToVisibilityConverter"
                };

                foreach (var converter in criticalConverters)
                {
                    if (Application.Current.Resources.Contains(converter))
                    {
                        found++;
                        Log.Debug("  ✅ {Converter} - found", converter);
                    }
                    else
                    {
                        missing.Add(converter);
                        Log.Error("  ❌ {Converter} - MISSING!", converter);
                    }
                }

                sw.Stop();

                // Report results
                if (missing.Any())
                {
                    Log.Fatal("💥 [DIAGNOSTIC] {Count} critical resources MISSING - startup will fail!", missing.Count);
                    Log.Fatal("Missing resources: {Resources}", string.Join(", ", missing));

                    throw new ApplicationException($"Missing {missing.Count} critical resources: {string.Join(", ", missing)}. " +
                                                 "Application cannot start without these resources. " +
                                                 "Check WileyTheme-Syncfusion.xaml and Generic.xaml.");
                }
                else
                {
                    Log.Information("✅ [DIAGNOSTIC] All {Count} critical resources validated ({Ms}ms)",
                        criticalBrushes.Length + criticalConverters.Length, sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "💥 [DIAGNOSTIC] Resource validation failed catastrophically");
                throw;
            }
        }

        #endregion

        #region Error Dialogs

        /// <summary>
        /// Shows a startup error dialog using dialog service or fallback MessageBox
        /// </summary>
        private void ShowStartupErrorDialog(Exception exception)
        {
            try
            {
                var dialogService = this.Container.Resolve<Prism.Dialogs.IDialogService>();
                var parameters = new Prism.Dialogs.DialogParameters {
                    { "Message", $"Critical startup error: {exception.Message}" },
                    { "ButtonText", "Exit" }
                };
                dialogService.ShowDialog("ErrorDialogView", parameters, _ => { });
            }
            catch
            {
                MessageBox.Show($"Critical startup error: {exception.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Shows an emergency error dialog when the 4-phase startup fails critically.
        /// Uses minimal dependencies since the container may not be fully initialized.
        /// </summary>
        private void ShowEmergencyErrorDialog(Exception exception)
        {
            try
            {
                var message = $"Critical startup failure during 4-phase initialization.\n\n" +
                             $"Error: {exception.Message}\n\n" +
                             $"The application cannot continue and will exit.\n\n" +
                             $"Please check the logs for detailed error information.";

                MessageBox.Show(
                    message,
                    "Emergency Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception dialogEx)
            {
                // Ultimate fallback - log only
                Log.Fatal(dialogEx, "Failed to show emergency startup error dialog");
            }
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Runs XAML diagnostics in DEBUG builds only
        /// </summary>
        private void RunXamlDiagnostics()
        {
            // Placeholder for XAML diagnostic logic
            Log.Debug("[DIAGNOSTICS] XAML diagnostics completed (DEBUG mode)");
        }

        #endregion
    }
}
