using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Forms;
using WileyWidget.Services;
using WileyWidget.WinForms.Themes;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Serilog;
using WileyWidget.Services.Logging;

namespace WileyWidget.WinForms
{
    class Program
    {
        private static IServiceProvider? _services;
        private static MainForm? _mainFormInstance;

        public static IServiceProvider Services => _services ?? throw new InvalidOperationException("Services not initialized");

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

        [STAThread]
        static void Main(string[] args)
        {
            // Set working directory to repo root for consistent logging paths
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var repoRoot = FindRepoRoot(currentDir) ?? FindRepoRoot(new DirectoryInfo(AppContext.BaseDirectory));
            if (repoRoot != null)
            {
                Directory.SetCurrentDirectory(repoRoot.FullName);
            }

            // Ensure log directory exists before Serilog initialization
            EnsureLogDirectoryExists();

            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            // Wire global exception handlers for unobserved async tasks and UI thread errors
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
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
                        MessageBox.Show(message, "Fatal Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch { } // Ignore MessageBox errors during app shutdown
                }
            };

            System.Windows.Forms.Application.ThreadException += static (s, e) =>
            {
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
                // Set up Syncfusion license (must be valid or commented out for trials)
                var licenseKey = System.Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register Syncfusion license");
                // Continue without license for trial/development
            }

            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Creating host builder...");
                var host = CreateHostBuilder(args).Build();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Host built successfully");

                _services = host.Services;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Services assigned to _services");

                // Diagnostic: List all registered services
                try
                {
                    var serviceDescriptors = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IEnumerable<Microsoft.Extensions.DependencyInjection.ServiceDescriptor>>(host.Services);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Total services registered: {(serviceDescriptors?.Count() ?? 0)}");
                }
                catch { }

                // Phase 1: Theming
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Initializing theme...");
                InitializeTheme(host.Services);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Theme initialized");

                // Log that exception handlers are active
                Log.Information("Exception handlers registered for unobserved tasks and UI thread errors");

                // Phase 2: Orchestration
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Attempting to resolve IStartupOrchestrator...");
                var orchestrator = host.Services.GetService(typeof(IStartupOrchestrator)) as IStartupOrchestrator;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] IStartupOrchestrator resolved: {(orchestrator != null ? "SUCCESS" : "NULL")}");

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
                ExecuteStartupWithTimeoutAsync(orchestrator, host.Services).GetAwaiter().GetResult();

                // Run the WinForms message loop without a timeout (normal app lifetime).
                orchestrator.RunApplicationAsync(host.Services).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
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
                // Re-throw so application exits with visible error
                throw;
            }
            finally
            {
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
                    // Ensure user-secrets are available even in non-standard host environments.
                    config.AddUserSecrets<Program>(optional: true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Ensure core services are registered (moved into WileyWidget.Services)
                    services.AddWileyWidgetCoreServices(hostContext.Configuration);

                    // Register WinForms-specific services
                    services.AddWinFormsServices(hostContext.Configuration);
                })
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext());

        static void InitializeTheme(IServiceProvider serviceProvider)
        {
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IThemeService>(serviceProvider);
            var themeName = themeService?.CurrentTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            // Load all required theme assemblies for Office2019Colorful and fallback themes
            try
            {
                // Primary theme: Office2019 (required for Office2019Colorful, Office2019Black, Office2019White, Office2019DarkGray)
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                Log.Information("✅ Loaded Office2019Theme assembly successfully - supports Office2019Colorful, Office2019Black, Office2019White, Office2019DarkGray");
                Log.Debug("Debug test log from Program.cs");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to load Office2019Theme assembly - falling back to default theme");
                // Fallback: continue without theme assembly, UI will use default rendering
                themeName = "Default";
            }

            // Set application-level theme
            try
            {
                SfSkinManager.ApplicationVisualTheme = themeName;
                Log.Information("Syncfusion theme initialized: {Theme} (Office2019Theme assembly loaded and ready)", themeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set ApplicationVisualTheme - theme may not apply correctly");
            }
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
