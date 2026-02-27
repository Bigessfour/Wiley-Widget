using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;  // Add this using
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Forms;
using WileyWidget.Services;
using WileyWidget.WinForms.Themes;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Blazor;  // Add this for Syncfusion Blazor services
using Serilog;
using WileyWidget.Services.Logging;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms
{
    class Program
    {
        private static IServiceProvider? _services;
        private static MainForm? _mainFormInstance;
        private static SplashForm? _splashForm;
        private static int _syncfusionLicenseRegistrationAttempted;
        private static int _syncfusionUserSecretsGuidanceLogged;

        public static IServiceProvider Services => _services ?? CreateFallbackServiceProvider();

        /// <summary>
        /// Safe accessor that returns the internal services instance or null if not yet initialized.
        /// Use this when callers must avoid the exception thrown by <see cref="Services"/>.
        /// </summary>
        public static IServiceProvider? ServicesOrNull => _services;

        public static MainForm? MainFormInstance
        {
            get => _mainFormInstance;
            set => _mainFormInstance = value;
        }

        internal static void StartSplash(string message, string? themeName = null)
        {
            if (IsTestRuntime()) return;

            if (_splashForm == null)
            {
                _splashForm = new SplashForm(themeName);
            }

            _splashForm.Report(0.05, message, isIndeterminate: true);
        }

        internal static void ReportSplash(double progress, string message, bool isIndeterminate = false)
        {
            if (IsTestRuntime()) return;

            _splashForm?.Report(progress, message, isIndeterminate);
        }

        internal static void CompleteSplash(string message)
        {
            if (IsTestRuntime()) return;

            if (_splashForm == null)
            {
                return;
            }

            _splashForm.Complete(message);
            _splashForm.Dispose();
            _splashForm = null;
        }

        public static async Task RunStartupHealthCheckAsync(IServiceProvider services)
        {
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILogger>(services);
            logger?.LogInformation("Running startup health check");
            // Add health checks here, e.g., database connection
            await Task.CompletedTask;
        }

        /// <summary>
        /// Shows an error dialog with minimal detail to user, logs full exception to Serilog.
        /// </summary>
        private static void ShowErrorDialog(string title, string message, Exception ex)
        {
            if (IsTestRuntime())
            {
                Log.Warning("Suppressed error dialog in test runtime: {Title} - {Message}", title, message);
                return;
            }

            try
            {
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // Fallback if MessageBox fails (e.g., no UI)
                Console.WriteLine($"ERROR: {title} - {message}");
            }
        }

        private static bool IsTestRuntime()
        {
            static bool IsTruthy(string variableName)
            {
                var value = Environment.GetEnvironmentVariable(variableName);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
            }

            return IsTruthy("WILEYWIDGET_UI_TESTS")
                || IsTruthy("WILEYWIDGET_TESTS")
                || IsTruthy("DOTNET_RUNNING_IN_TEST")
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VSTEST_SESSION_ID"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XUNIT_TESTRUNNING"));
        }

        [STAThread]
        static void Main(string[] args)
        {
            // Handle test mode argument (sets environment variable for FlaUI integration tests)
            if (args.Contains("--testmode", StringComparer.OrdinalIgnoreCase) ||
                Environment.GetEnvironmentVariable("WILEY_TESTMODE") == "true")
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "true");
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
                Console.WriteLine("[TEST MODE] Enabled - using test database and UI automation settings");
            }

            // Set working directory to repo root for consistent logging paths
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var repoRoot = FindRepoRoot(currentDir) ?? FindRepoRoot(new DirectoryInfo(AppContext.BaseDirectory));
            if (repoRoot != null)
            {
                Directory.SetCurrentDirectory(repoRoot.FullName);
            }

            // Ensure log directory exists before Serilog initialization
            EnsureLogDirectoryExists();

            // Configure Serilog logger manually for WinForms app
            // IMPORTANT: Configuration order - later sources override earlier ones
            // Order: 1) appsettings.json, 2) user secrets, 3) environment variables (last for highest priority)
            var basePath = Directory.GetCurrentDirectory();
            var appSettingsPath = "src/WileyWidget.WinForms/appsettings.json";

            // If we are already in the project directory or the file isn't found at the repo-root relative path,
            // fallback to local directory (handles both dev-from-root and direct-exe-run scenarios).
            if (!File.Exists(Path.Combine(basePath, appSettingsPath)) && File.Exists(Path.Combine(basePath, "appsettings.json")))
            {
                appSettingsPath = "appsettings.json";
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true)
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables()  // Environment variables last - highest priority
                .Build();

            // Register Syncfusion license at the earliest point in startup,
            // before any UI/bootstrap path could instantiate Syncfusion controls.
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Registering Syncfusion license (early)...");
            RegisterSyncfusionLicense(configuration);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Syncfusion license registration attempted (early)");

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            Log.Information("Program.Main: Starting WileyWidget application");
            Log.Debug("Program.Main: Working directory set to {WorkingDirectory}", Directory.GetCurrentDirectory());

            // DEBUG: Log masked API key to verify configuration loading chain
            var apiKeyFromConfig = configuration["xAI:ApiKey"] ?? configuration["XAI:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKeyFromConfig))
            {
                var maskedKey = apiKeyFromConfig.Length > 8
                    ? apiKeyFromConfig.Substring(0, 4) + "***" + apiKeyFromConfig.Substring(apiKeyFromConfig.Length - 4)
                    : "***";
                Log.Debug("[CONFIG DEBUG] xAI:ApiKey found in configuration chain: {MaskedKey} (length: {Length})", maskedKey, apiKeyFromConfig.Length);
            }
            else
            {
                Log.Warning("[CONFIG DEBUG] xAI:ApiKey NOT found in configuration chain (checked: appsettings.json, user secrets, environment variables)");
            }
            Log.Information("Program.Main: Syncfusion license registration was attempted before UI/bootstrap initialization");

            // Read theme from configuration BEFORE creating splash to avoid visible theme flash
            var splashThemeName = configuration["UI:Theme"] ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            splashThemeName = WileyWidget.WinForms.Themes.ThemeColors.ValidateTheme(splashThemeName);
            Log.Debug("Program.Main: Using theme '{Theme}' for splash screen", splashThemeName);

            // Ensure theme assembly is loaded BEFORE splash creation
            try
            {
                WileyWidget.WinForms.Themes.ThemeColors.EnsureThemeAssemblyLoadedForTheme(splashThemeName);
                Log.Debug("Program.Main: Theme assembly loaded for '{Theme}'", splashThemeName);
            }
            catch (Exception themeEx)
            {
                Log.Warning(themeEx, "Program.Main: Failed to pre-load theme assembly for '{Theme}' (non-critical)", splashThemeName);
            }

            StartSplash("Starting Wiley Widget...", splashThemeName);

            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            // Wire global exception handlers for unobserved async tasks and UI thread errors
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                var ex = e.Exception.GetBaseException();

                // ✅ FIX (P3): Suppress BlockingCollection cancellation during shutdown (expected behavior)
                if (ex is OperationCanceledException &&
                    ex.StackTrace?.Contains("BlockingCollection", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Log.Debug("Background task cancelled during shutdown (expected): {Message}", ex.Message);
                    e.SetObserved();
                    return;
                }

                // WebView2 can surface E_ABORT during disposal/shutdown when controller creation is interrupted.
                // This is expected in teardown races and should not be treated as a runtime fault.
                if (ex is COMException comException
                    && (uint)comException.HResult == 0x80004004
                    && (ex.StackTrace?.Contains("WebView2", StringComparison.OrdinalIgnoreCase) == true
                        || ex.StackTrace?.Contains("BlazorWebView", StringComparison.OrdinalIgnoreCase) == true))
                {
                    Log.Debug("Suppressed expected WebView2 shutdown abort (E_ABORT): {Message}", ex.Message);
                    e.SetObserved();
                    return;
                }

                Log.Error(e.Exception, "Unobserved task exception (fire-and-forget task raised error)");
                e.SetObserved(); // Suppress crash, log only
            };

            // AppDomain-level handler to catch exceptions that ThreadException misses
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                var ex = ev.ExceptionObject as Exception;
                Log.Fatal(ex, "[FATAL] Unhandled AppDomain exception (isTerminating={IsTerminating})", ev.IsTerminating);
                if (ev.IsTerminating)
                {
                    try
                    {
                        var message = $"FATAL ERROR - Application terminating:\n\n{ex?.GetType().Name}: {ex?.Message}\n\nStack:\n{ex?.StackTrace}";
                        ShowErrorDialog("Fatal Application Error", message, ex ?? new InvalidOperationException(message));
                    }
                    catch { } // Ignore MessageBox errors during app shutdown
                }
            };

            System.Windows.Forms.Application.ThreadException += static (s, e) =>
            {
                if (e.Exception is NullReferenceException &&
                    e.Exception.StackTrace?.Contains("Syncfusion.Windows.Forms.Tools.DockingManager.HostControl_Paint", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Log.Warning(e.Exception, "[SYNCFUSION] Ignored known DockingManager HostControl paint null-reference exception");
                    return;
                }

                Log.Error(e.Exception, "[CRITICAL] Unhandled UI thread exception - application will terminate with code -1");
                // Log full exception details before showing dialog
                var ex = e.Exception;
                var depth = 0;
                while (ex != null && depth < 5)
                {
                    Log.Fatal($"[Depth {depth}] {ex.GetType().Name}: {ex.Message}");
                    if (!string.IsNullOrEmpty(ex.StackTrace))
                    {
                        Log.Fatal($"Stack Trace:\n{ex.StackTrace}");
                    }
                    ex = ex.InnerException;
                    depth++;
                }
                Log.CloseAndFlush(); // Flush logs before showing dialog
                // Show minimal error dialog to user
                try
                {
                    ShowErrorDialog("Application Error", e.Exception.Message, e.Exception);
                }
                catch (Exception dialogEx)
                {
                    Log.Error(dialogEx, "Failed to show error dialog");
                }
                // Note: ThreadExceptionEventArgs doesn't support suppression; application will terminate with code -1
            };

            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Creating host builder...");
                ReportSplash(0.15, "Building host...");
                var host = CreateHostBuilder(args).Build();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Host built successfully");
                ReportSplash(0.3, "Host built", isIndeterminate: false);

                _services = host.Services;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Services assigned to _services");
                Log.Information("ServiceProvider created successfully (services registered via ConfigureServices)");

                // Syncfusion license was registered earlier (before splash) to ensure
                // license is applied before any Syncfusion controls are instantiated.

                // Phase 1: Theming
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Initializing theme...");
                Log.Information("Program.Main: Starting theme initialization");
                ReportSplash(0.4, "Initializing theme...");
                InitializeTheme(host.Services);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Theme initialized");
                Log.Information("Program.Main: Theme initialization completed");
                ReportSplash(0.55, "Theme ready", isIndeterminate: false);

                // Log that exception handlers are active
                Log.Information("Exception handlers registered for unobserved tasks and UI thread errors");

                // Phase 2: Orchestration
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Attempting to resolve IStartupOrchestrator...");
                Log.Information("Program.Main: Resolving IStartupOrchestrator");
                var orchestrator = host.Services.GetService(typeof(IStartupOrchestrator)) as IStartupOrchestrator;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] IStartupOrchestrator resolved: {(orchestrator != null ? "SUCCESS" : "NULL")}");
                Log.Information("Program.Main: IStartupOrchestrator resolved: {Resolved}", orchestrator != null);

                if (orchestrator == null)
                {
                    // Try GetRequiredService to get better error message
                    try
                    {
                        orchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(host.Services);
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] GetRequiredService error: {innerEx.GetType().Name}: {innerEx.Message}");
                        Log.Error(innerEx, "Failed to resolve IStartupOrchestrator via GetRequiredService");
                        throw;
                    }
                }

                if (orchestrator == null)
                {
                    throw new InvalidOperationException("IStartupOrchestrator is not registered.");
                }

                // Async startup orchestration with a configurable timeout (Startup.TimeoutSeconds)
                // Only timebox startup initialization, not the full app lifetime.
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting application initialization with configured startup timeout...");
                Log.Information("Program.Main: Starting application initialization with timeout");
                ReportSplash(0.7, "Initializing services...");
                // CRITICAL: Use GetAwaiter().GetResult() to preserve STA thread mode for WinForms
                // async Task Main breaks STA context after await, causing drag-drop registration failures
                // See: errors-20260205.log - ThreadStateException during MainForm.OnHandleCreated
                ExecuteStartupWithTimeoutAsync(orchestrator, host.Services).GetAwaiter().GetResult();

                Log.Information("Program.Main: Application initialization completed");
                ReportSplash(0.85, "Starting application...");

                // Run the WinForms message loop without a timeout (normal app lifetime).
                Log.Information("Program.Main: Starting WinForms message loop");
                ReportSplash(0.95, "Launching main window...");
                try
                {
                    orchestrator.RunApplicationAsync(host.Services).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unhandled exception in Application.Run");
                    throw;
                }
                finally
                {
                    Log.Information("Application exited");
                }
            }
            catch (Exception ex)
            {
                CompleteSplash("Startup failed");
                // Log the full exception chain to find the root cause
                var current = ex;
                int depth = 0;
                while (current != null && depth < 5)
                {
                    Log.Fatal($"[Level {depth}] {current.GetType().Name}: {current.Message}");
                    current = current.InnerException;
                    depth++;
                }
                Log.Fatal(ex, "Application start-up failed");

                // [MODIFIED] Ensure the user sees the fatal crash reason before exit
                try
                {
                    var coreError = ex.GetBaseException();
                    var message = $"Wiley Widget failed to start.\n\nType: {ex.GetType().Name}\nMessage: {ex.Message}\n\nRoot Cause: {coreError.Message}\n\nCheck logs for full stack trace.";
                    ShowErrorDialog("Fatal Startup Error", message, ex);
                }
                catch { /* Suppress MessageBox failure in crash path */ }

                // Re-throw so application exits with visible error
                throw;
            }
            finally
            {
                CompleteSplash("Exiting...");
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Executes startup initialization with configurable timeout protection.
        /// Uses Task.WhenAny to enforce timeout and logs detailed phase budgets so diagnostics can point to the slowest phases.
        /// </summary>
        static async Task ExecuteStartupWithTimeoutAsync(IStartupOrchestrator orchestrator, IServiceProvider serviceProvider)
        {
            var startupOptions = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IOptions<StartupOptions>>(serviceProvider)?.Value ?? new StartupOptions();
            var timeoutSeconds = Math.Max(startupOptions.TimeoutSeconds, 120);
            var phaseTimeouts = startupOptions.PhaseTimeouts ?? new PhaseTimeoutsOptions();
            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(serviceProvider);
            var timelineScope = timelineService?.BeginPhaseScope("Application Startup", expectedOrder: 12, isUiCritical: true);

            try
            {
                var startupStopwatch = Stopwatch.StartNew();
                Log.Information(
                    "Application startup initialization beginning with {TimeoutSeconds}s timeout (phase budgets: docking {DockingInitMs}ms, viewmodel {ViewModelInitMs}ms, data {DataLoadMs}ms)",
                    timeoutSeconds,
                    phaseTimeouts.DockingInitMs,
                    phaseTimeouts.ViewModelInitMs,
                    phaseTimeouts.DataLoadMs);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🚀 Starting startup initialization with {timeoutSeconds}s timeout (phase budgets: docking {phaseTimeouts.DockingInitMs}ms, viewmodel {phaseTimeouts.ViewModelInitMs}ms, data {phaseTimeouts.DataLoadMs}ms)");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var initTask = orchestrator.InitializeAsync();
                var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cts.Token);

                var completedTask = await Task.WhenAny(initTask, delayTask);
                var elapsedMs = startupStopwatch.Elapsed.TotalMilliseconds;

                if (completedTask == delayTask)
                {
                    Log.Warning(
                        "⚠️ Startup initialization exceeded {TimeoutSeconds}s after {ElapsedMs:F0}ms (phase budgets: docking {DockingInitMs}ms, viewmodel {ViewModelInitMs}ms, data {DataLoadMs}ms). Consider increasing Startup.TimeoutSeconds or reviewing the slowest phases.",
                        timeoutSeconds,
                        elapsedMs,
                        phaseTimeouts.DockingInitMs,
                        phaseTimeouts.ViewModelInitMs,
                        phaseTimeouts.DataLoadMs);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ Startup initialization timeout ({timeoutSeconds}s) exceeded after {elapsedMs:F0}ms (phase budgets: docking {phaseTimeouts.DockingInitMs}ms, viewmodel {phaseTimeouts.ViewModelInitMs}ms, data {phaseTimeouts.DataLoadMs}ms)");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📋 Diagnostic: Check logs for slow phases (Docking, ViewModel, Data Load)");
                }
                else
                {
                    cts.Cancel();
                    try
                    {
                        await initTask; // Ensure any exceptions are propagated
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ Startup initialization completed in {elapsedMs:F0}ms (within {timeoutSeconds}s timeout)");
                        Log.Information("Startup initialization completed successfully in {ElapsedMs}ms (timeout {TimeoutSeconds}s)", elapsedMs, timeoutSeconds);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Startup initialization completed within timeout but raised exception");
                        throw;
                    }
                }
            }
            finally
            {
                timelineScope?.Dispose();
                timelineService?.GenerateReport();
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // IMPORTANT: Configuration loading order - later sources override earlier ones.
                    // Host.CreateDefaultBuilder() already adds appsettings.json and environment variables.
                    // Since we set the working directory to the repo root, we must explicitly add the
                    // project-specific appsettings.json from the src tree.
                    var appSettingsPath = "src/WileyWidget.WinForms/appsettings.json";
                    var envAppSettingsPath = $"src/WileyWidget.WinForms/appsettings.{context.HostingEnvironment.EnvironmentName}.json";

                    // Fallback to local paths if running from within the project directory or bin folder
                    if (!File.Exists(Path.Combine(context.HostingEnvironment.ContentRootPath, appSettingsPath)) &&
                        File.Exists(Path.Combine(context.HostingEnvironment.ContentRootPath, "appsettings.json")))
                    {
                        appSettingsPath = "appsettings.json";
                    }

                    if (!File.Exists(Path.Combine(context.HostingEnvironment.ContentRootPath, envAppSettingsPath)) &&
                        File.Exists(Path.Combine(context.HostingEnvironment.ContentRootPath, $"appsettings.{context.HostingEnvironment.EnvironmentName}.json")))
                    {
                        envAppSettingsPath = $"appsettings.{context.HostingEnvironment.EnvironmentName}.json";
                    }

                    config.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);
                    config.AddJsonFile(envAppSettingsPath, optional: true, reloadOnChange: true);

                    // We explicitly add user secrets AFTER CreateDefaultBuilder sets up defaults.
                    // Then we re-add environment variables at the END to ensure they have highest priority.
                    // This ensures: 1) appsettings.json, 2) appsettings.{Environment}.json, 3) user secrets, 4) environment variables

                    // Remove environment variables that were added by CreateDefaultBuilder
                    var envVarSources = config.Sources.Where(s => s.GetType().Name.Contains("EnvironmentVariables")).ToList();
                    foreach (var source in envVarSources)
                    {
                        config.Sources.Remove(source);
                    }

                    // Re-add in correct order: user secrets first
                    config.AddUserSecrets<Program>(optional: true);

                    // Then environment variables LAST (highest priority)
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Ensure core services are registered (moved into WileyWidget.Services)
                    services.AddWileyWidgetCoreServices(hostContext.Configuration);

                    // Register WinForms-specific services
                    services.AddWinFormsServices(hostContext.Configuration);

                    /// <summary>
                    /// NEW: Register Blazor WebView services (required for BlazorWebView to initialize)
                    /// Enables Windows Forms applications to host Blazor components and WebView controls.
                    /// </summary>
                    services.AddWindowsFormsBlazorWebView();
#if DEBUG
                    /// <summary>
                    /// Optional: Enable developer tools for debugging Blazor components in Debug builds.
                    /// Provides browser console access and component inspection capabilities.
                    /// </summary>
                    // Developer tools are available via AddBlazorWebViewDeveloperTools() if WebView2 supports it
#endif
                    /// <summary>
                    /// NEW: Required for Syncfusion Blazor components (e.g., SfAIAssistView).
                    /// Registers Syncfusion Blazor services and component infrastructure.
                    /// </summary>
                    services.AddSyncfusionBlazor();  // NEW: Required for Syncfusion Blazor components (e.g., SfAIAssistView)
                })
                .UseSerilog();

        static void InitializeTheme(IServiceProvider serviceProvider)
        {
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeService>(serviceProvider);
            var themeName = themeService?.CurrentTheme;

            if (string.IsNullOrWhiteSpace(themeName))
            {
                themeName = AppThemeColors.DefaultTheme;
            }

            themeName = AppThemeColors.ValidateTheme(themeName);

            // Load Office2019Theme assembly (required for Office2019Colorful and related themes)
            // Per Syncfusion best practices: Load assembly early, apply theme to individual forms
            // Note: ApplicationVisualTheme is optional - theme cascade from form.SetVisualStyle is sufficient
            try
            {
                AppThemeColors.EnsureThemeAssemblyLoadedForTheme(themeName);
                Log.Information("✅ Syncfusion theme assemblies loaded - supports Office2019, Office2016, and HighContrast WinForms themes");

                // Optional: Set global theme property (not required per official demos, but useful for dynamic control creation)
                SfSkinManager.ApplicationVisualTheme = themeName;
                Log.Information("Syncfusion theme initialized: {Theme}", themeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to load Office2019Theme assembly - UI will use default rendering");
                // Non-fatal: continue without theme, controls will render with default styling
            }
        }

        /// <summary>
        /// Registers Syncfusion license from runtime configuration sources.
        /// MUST be called BEFORE any Syncfusion controls are instantiated or theme assemblies are loaded.
        /// </summary>
        static void RegisterSyncfusionLicense(IConfiguration? configuration)
        {
            if (Interlocked.Exchange(ref _syncfusionLicenseRegistrationAttempted, 1) == 1)
            {
                Log.Debug("Syncfusion license registration was already attempted earlier; skipping duplicate attempt.");
                return;
            }

            var (licenseKey, source) = ResolveSyncfusionLicenseKey(configuration);
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                Log.Warning("⚠️ Syncfusion license key not found. Checked user-secrets, configuration aliases, environment aliases, and encrypted vault. Application will show trial/evaluation popup.");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ Syncfusion license key missing - running in evaluation mode");
                // Do not throw - missing license just shows trial popup, not a fatal error
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")))
                {
                    Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", licenseKey, EnvironmentVariableTarget.Process);
                    Log.Debug("Promoted resolved Syncfusion license key into Process environment for startup consistency");
                }

                LogUserSecretsGuidanceIfNeeded(source);

                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                Log.Information("✅ Syncfusion license registered successfully (source: {LicenseSource})", source ?? "unknown");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ Syncfusion license registered");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to register Syncfusion license - application will show trial popup");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ License registration failed: {ex.Message}");
                // Do not throw - license failure is non-fatal, just shows trial watermark/popup
            }
        }

        private static readonly string[] SyncfusionLicenseKeyAliases =
        {
            "WILEY_SYNC_LIC_KEY",
            "SYNCFUSION_LICENSE_KEY",
            "Syncfusion:LicenseKey",
            "Syncfusion__LicenseKey",
            "Syncfusion-LicenseKey",
            "SyncfusionLicenseKey",
            "syncfusion-license-key"
        };

        private static (string? LicenseKey, string? Source) ResolveSyncfusionLicenseKey(IConfiguration? configuration)
        {
            IConfiguration? userSecretsConfiguration = null;
            try
            {
                userSecretsConfiguration = new ConfigurationBuilder()
                    .AddUserSecrets<Program>(optional: true)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Unable to initialize user-secrets configuration for Syncfusion license resolution");
            }

            var candidates = new List<(string Source, string? Value)>();

            // 1) Prefer user-secrets first (explicit requirement)
            foreach (var alias in SyncfusionLicenseKeyAliases)
            {
                candidates.Add(($"UserSecrets:{alias}", userSecretsConfiguration?[alias]));
            }

            // 2) Then merged app configuration (appsettings + secrets + env per host configuration chain)
            foreach (var alias in SyncfusionLicenseKeyAliases)
            {
                candidates.Add(($"Configuration:{alias}", configuration?[alias]));
            }

            // 3) Then raw environment aliases
            foreach (var alias in SyncfusionLicenseKeyAliases)
            {
                candidates.Add(($"Environment:Process:{alias}", Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.Process)));
                candidates.Add(($"Environment:User:{alias}", Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.User)));
                candidates.Add(($"Environment:Machine:{alias}", Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.Machine)));
            }

            foreach (var candidate in candidates)
            {
                var normalized = NormalizeLicenseKey(candidate.Value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return (normalized, candidate.Source);
                }
            }

            var fromVault = TryResolveSyncfusionLicenseFromEncryptedVault(out var vaultSource);
            if (!string.IsNullOrWhiteSpace(fromVault))
            {
                return (fromVault, vaultSource);
            }

            return (null, null);
        }

        private static string? TryResolveSyncfusionLicenseFromEncryptedVault(out string? source)
        {
            source = null;

            try
            {
                using var vault = new EncryptedLocalSecretVaultService(NullLogger<EncryptedLocalSecretVaultService>.Instance);

                var secretNames = new[]
                {
                    "SYNCFUSION_LICENSE_KEY",
                    "syncfusion-license-key",
                    "Syncfusion-LicenseKey",
                    "Syncfusion__LicenseKey",
                    "Syncfusion:LicenseKey",
                    "SyncfusionLicenseKey"
                };

                foreach (var secretName in secretNames)
                {
                    var candidate = vault.GetSecret(secretName);
                    var normalized = NormalizeLicenseKey(candidate);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        source = $"EncryptedVault:{secretName}";
                        Log.Information("Loaded Syncfusion license key from encrypted secret vault ({SecretName})", secretName);
                        return normalized;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Unable to read Syncfusion license key from encrypted secret vault");
            }

            return null;
        }

        private static string? NormalizeLicenseKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if ((trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
                || (trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal)))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            if (trimmed.StartsWith("YOUR_SYNCFUSION_LICENSE_KEY", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("YOUR_LICENSE_KEY", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return trimmed;
        }

        private static void LogUserSecretsGuidanceIfNeeded(string? resolvedSource)
        {
            if (Interlocked.Exchange(ref _syncfusionUserSecretsGuidanceLogged, 1) == 1)
            {
                return;
            }

            if (HasValidLicenseInUserSecrets())
            {
                return;
            }

            var hasFallbackScopeLicense = HasValidLicenseInEnvironmentScope(EnvironmentVariableTarget.User)
                || HasValidLicenseInEnvironmentScope(EnvironmentVariableTarget.Machine)
                || (!string.IsNullOrWhiteSpace(resolvedSource)
                    && resolvedSource.StartsWith("EncryptedVault:", StringComparison.OrdinalIgnoreCase));

            if (!hasFallbackScopeLicense)
            {
                return;
            }

            Log.Warning(
                "Syncfusion license key resolved from {LicenseSource}, but no valid user-secrets entry was found. " +
                "Microsoft Learn guidance recommends storing development secrets with Secret Manager. " +
                "Set: dotnet user-secrets set \"Syncfusion:LicenseKey\" \"<key>\" --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj",
                resolvedSource ?? "unknown");
        }

        private static bool HasValidLicenseInUserSecrets()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddUserSecrets<Program>(optional: true)
                    .Build();

                foreach (var alias in SyncfusionLicenseKeyAliases)
                {
                    var normalized = NormalizeLicenseKey(config[alias]);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Unable to inspect user-secrets while evaluating Syncfusion license guidance logging");
            }

            return false;
        }

        private static bool HasValidLicenseInEnvironmentScope(EnvironmentVariableTarget scope)
        {
            foreach (var alias in SyncfusionLicenseKeyAliases)
            {
                var normalized = NormalizeLicenseKey(Environment.GetEnvironmentVariable(alias, scope));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ensures log directory exists before Serilog attempts to write.
        /// Uses the workspace logs directory when available.
        /// </summary>
        static void EnsureLogDirectoryExists()
        {
            try
            {
                _ = LogPathResolver.GetLogsDirectory();
            }
            catch (Exception ex)
            {
                // Fallback: write to console if directory creation fails
                Console.WriteLine($"Warning: Failed to create log directory: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the repository root by looking for WileyWidget.sln.
        /// </summary>
        private static DirectoryInfo? FindRepoRoot(DirectoryInfo? start)
        {
            while (start != null)
            {
                var solutionPath = Path.Combine(start.FullName, "WileyWidget.sln");
                if (File.Exists(solutionPath))
                {
                    return start;
                }

                start = start.Parent;
            }

            return null;
        }

        /// <summary>
        /// Creates a minimal IServiceProvider for first-run or fallback scenarios.
        /// Registers only essential services (logging + empty IConfiguration) so UI can initialize safely.
        /// </summary>
        public static IServiceProvider CreateFallbackServiceProvider()
        {
            try
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());
                var sp = services.BuildServiceProvider();
                Log.Warning("Program: using minimal fallback IServiceProvider created by CreateFallbackServiceProvider()");
                return sp;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Program: failed to create fallback IServiceProvider - returning empty provider");
                return new ServiceCollection().BuildServiceProvider();
            }
        }
    }
}
