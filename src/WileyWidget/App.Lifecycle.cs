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
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Abstractions;
// using WileyWidget.Startup;  // Conditionally excluded for wpftmp builds
using WileyWidget.Services;
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
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Information("[STARTUP] ============ Enhanced 4-Phase Startup BEGIN ============");

            try
            {
                // Phase 1: Early Configuration and Validation (before Prism bootstrap)
                Log.Information("Phase 1: Early validation and configuration");
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
                // Load resources synchronously during WPF startup (async causes deadlocks)
                LoadApplicationResourcesSync();
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

                // Initialize SigNoz telemetry early (before Prism bootstrap)
                InitializeSigNozTelemetry();

                // MCP VALIDATION: Add test span for end-to-end trace verification
                using var mcpValidationActivity = SigNozTelemetryService.ActivitySource.StartActivity("MCP.Validation.Startup");
                mcpValidationActivity?.SetTag("mcp.phase", "validation");
                mcpValidationActivity?.SetTag("session.id", "MCP-TEST-001");
                mcpValidationActivity?.SetTag("environment", "development");
                mcpValidationActivity?.SetTag("wiley.version", SigNozTelemetryService.ServiceVersion);
                mcpValidationActivity?.AddEvent(new ActivityEvent("MCP Validation started - testing trace continuity"));
                Log.Information("✅ MCP Validation span created: session=MCP-TEST-001");

                Log.Information("✅ Phase 1 completed: Configuration, validation, and telemetry");

                // Phase 2-4: Prism will handle container setup, modules, and UI via RegisterTypes() and OnInitialized()
                Log.Information("Phase 2-4: Proceeding with Prism bootstrap (integrated phases)");

                // Now trigger Prism bootstrap (this handles remaining phases)
                base.OnStartup(e);  // This calls Initialize() -> CreateShell() -> OnInitialized()

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
        /// </summary>
        protected override void OnInitialized()
        {
            SplashWindow? splashWindow = null;
            try
            {
                Log.Information("Phase 3: Module and service initialization (OnInitialized)");

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

                // Setup global exception handling post-container
                SetupGlobalExceptionHandling();
                Log.Debug("[EXCEPTION] Global exception handling configured successfully");

                // Integrate telemetry services now that container is ready
                IntegrateTelemetryServices();

                // Start deferred secrets (non-blocking) - DISABLED: ISecretsService not implemented
                // TODO: Implement ISecretsService or remove this code
                /*
                splashWindow.UpdateStatus("Initializing secrets service...");
                _ = Task.Run(async () =>
                {
                    using var cts = new CancellationTokenSource(SecretsTimeout);
                    try
                    {
                        var secretsService = ResolveWithRetry<ISecretsService>();
                        await secretsService.InitializeAsync(cts.Token).ConfigureAwait(false);
                        _secretsInitializationTcs.TrySetResult(true);
                        Log.Information("[SECURITY] Deferred secrets initialization completed");
                    }
                    catch (Exception ex)
                    {
                        _secretsInitializationTcs.TrySetException(ex);
                        Log.Error(ex, "[SECURITY] Deferred secrets initialization failed");
                    }
                });

                // Brief await for secrets (non-blocking)
                _ = Task.WhenAny(SecretsInitializationTask, Task.Delay(BriefAwaitTimeout));
                */

                // Background DB init
                splashWindow.UpdateStatus("Initializing database...");
#if !WPFTMP
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var dbInit = this.Container.Resolve<WileyWidget.Startup.DatabaseInitializer>();
                        await dbInit.InitializeAsync().ConfigureAwait(false);
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
                // Final log flush before exit
                Log.CloseAndFlush();
            }

            Log.Information("Shutdown complete - ExitCode: {Code}", e.ApplicationExitCode);
        }

        /// <summary>
        /// Creates the main application shell window
        /// </summary>
        protected override Window CreateShell()
        {
            Log.Information("[SHELL] Creating Shell window...");
            try
            {
                // Resolve the application's main shell (Shell window) from the container.
                // Ensure Shell is registered in RegisterTypes below or in a module.
                var shell = Container.Resolve<Shell>();
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
                var availableMemoryMB = process.WorkingSet64 / (1024 * 1024);
                var minMemoryMB = 128; // Minimum for WPF app

                if (availableMemoryMB < minMemoryMB)
                {
                    warnings.Add($"Available memory {availableMemoryMB}MB is below recommended minimum {minMemoryMB}MB");
                }

                Log.Debug("✓ Process memory: {Memory}MB", availableMemoryMB);
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

            // 6. License key configuration validation (enhanced with dev mode support)
            try
            {
                var config = BuildConfiguration();
                var isDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development"
                                 || config["Environment"] == "Development";

                var syncfusionKey = config["Syncfusion:LicenseKey"]
                                 ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                var boldKey = config["BoldReports:LicenseKey"]
                           ?? Environment.GetEnvironmentVariable("BOLD_LICENSE_KEY");

                // Only warn in production or if explicitly checking licenses
                if (!isDevelopment)
                {
                    if (string.IsNullOrEmpty(syncfusionKey))
                    {
                        warnings.Add("Syncfusion license key not configured - will run in trial mode");
                    }

                    if (string.IsNullOrEmpty(boldKey) && string.IsNullOrEmpty(syncfusionKey))
                    {
                        warnings.Add("Bold Reports license key not configured - will run in trial mode");
                    }
                }
                else
                {
                    // Development mode - log but don't warn
                    Log.Debug("[LICENSE] Development mode - license validation relaxed");
                    if (string.IsNullOrEmpty(syncfusionKey))
                    {
                        Log.Debug("[LICENSE] Syncfusion license not set - will use trial/community mode");
                    }
                    if (string.IsNullOrEmpty(boldKey) && string.IsNullOrEmpty(syncfusionKey))
                    {
                        Log.Debug("[LICENSE] Bold Reports license not set - will use trial/community mode");
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"License key validation failed: {ex.Message}");
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
