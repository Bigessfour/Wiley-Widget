using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget.WinForms
{
    static class Program
    {
        private static IServiceProvider? _services;

        public static IServiceProvider Services => _services ?? throw new InvalidOperationException("Services not initialized");

        public static async Task RunStartupHealthCheckAsync(IServiceProvider services)
        {
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILogger>(services);
            logger?.LogInformation("Running startup health check");
            // Add health checks here, e.g., database connection
            await Task.CompletedTask;
        }

        [STAThread]
        static void Main(string[] args)
        {
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

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
                // Wraps RunApplicationAsync with Task.WhenAny to enforce timeout
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting application orchestration with configured startup timeout...");
                ExecuteWithTimeoutAsync(orchestrator, host.Services).GetAwaiter().GetResult();
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
        /// Executes orchestrator initialization with configurable timeout protection.
        /// Uses Task.WhenAny to enforce timeout and logs detailed phase budgets so diagnostics can point to the slowest phases.
        /// </summary>
        static async Task ExecuteWithTimeoutAsync(IStartupOrchestrator orchestrator, IServiceProvider serviceProvider)
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
                    "Application startup beginning with {TimeoutSeconds}s timeout (phase budgets: docking {DockingInitMs}ms, viewmodel {ViewModelInitMs}ms, data {DataLoadMs}ms)",
                    timeoutSeconds,
                    phaseTimeouts.DockingInitMs,
                    phaseTimeouts.ViewModelInitMs,
                    phaseTimeouts.DataLoadMs);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🚀 Starting application initialization with {timeoutSeconds}s timeout (phase budgets: docking {phaseTimeouts.DockingInitMs}ms, viewmodel {phaseTimeouts.ViewModelInitMs}ms, data {phaseTimeouts.DataLoadMs}ms)");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var initTask = orchestrator.RunApplicationAsync(serviceProvider);
                var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cts.Token);

                var completedTask = await Task.WhenAny(initTask, delayTask);
                var elapsedMs = startupStopwatch.Elapsed.TotalMilliseconds;

                if (completedTask == delayTask)
                {
                    Log.Warning(
                        "⚠️ Application startup exceeded {TimeoutSeconds}s after {ElapsedMs:F0}ms (phase budgets: docking {DockingInitMs}ms, viewmodel {ViewModelInitMs}ms, data {DataLoadMs}ms). Consider increasing Startup.TimeoutSeconds or reviewing the slowest phases.",
                        timeoutSeconds,
                        elapsedMs,
                        phaseTimeouts.DockingInitMs,
                        phaseTimeouts.ViewModelInitMs,
                        phaseTimeouts.DataLoadMs);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ Startup timeout ({timeoutSeconds}s) exceeded after {elapsedMs:F0}ms (phase budgets: docking {phaseTimeouts.DockingInitMs}ms, viewmodel {phaseTimeouts.ViewModelInitMs}ms, data {phaseTimeouts.DataLoadMs}ms)");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📋 Diagnostic: Check logs for slow phases (Docking, ViewModel, Data Load)");

                    cts.Cancel();

                    try
                    {
                        await Task.WhenAny(initTask, Task.Delay(2000));
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Information("Initialization task cancelled after timeout");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Exception while cancelling initialization task");
                    }
                }
                else
                {
                    cts.Cancel();
                    try
                    {
                        await initTask; // Ensure any exceptions are propagated
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ Application initialization completed in {elapsedMs:F0}ms (within {timeoutSeconds}s timeout)");
                        Log.Information("Application initialization completed successfully in {ElapsedMs}ms (timeout {TimeoutSeconds}s)", elapsedMs, timeoutSeconds);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Initialization completed within timeout but raised exception");
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
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddWinFormsServices(hostContext.Configuration);


                })
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console());

        static void InitializeTheme(IServiceProvider serviceProvider)
        {
            // SfSkinManager initialization logic here
        }
    }
}
