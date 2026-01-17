using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.ExceptionServices;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;
        private static IServiceScope? _applicationScope;
        private static SynchronizationContext? UISynchronizationContext;
        private const int WS_EX_COMPOSITED = 0x02000000;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // Set up WinForms application defaults
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // CRITICAL: Initialize logging VERY first, before any other operations
            InitializeLogging();

            // CRITICAL: Register Syncfusion license VERY early - before any Syncfusion control or theme is used
            // Per Syncfusion docs: RegisterLicense must be called before any Syncfusion component instantiation
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var licenseKey = config["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                Log.Debug("Syncfusion license registered successfully");
            }
            else
            {
                Log.Warning("Syncfusion license key not found in configuration");
            }

            try
            {
                // Run the async startup and block until complete
                RunApplicationAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                HandleFatalException(ex);
            }
            finally
            {
                try
                {
                    // Explicit Serilog shutdown: flush pending logs and dispose static logger
                    Serilog.Log.Information("Application shutdown initiated");
                    Serilog.Log.CloseAndFlush();
                }
                catch (ObjectDisposedException ex)
                {
                    // Safe to ignore during shutdown; log to console if needed
                    Console.WriteLine($"Serilog shutdown ignored: {ex.Message}");
                }
                catch (OperationCanceledException ex)
                {
                    // Async sink background worker may throw OperationCanceledException during flush
                    // when draining queue with a signaled cancellation token. This is expected during shutdown.
                    Console.WriteLine($"Serilog async sink cancellation during shutdown (expected): {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Serilog shutdown failed: {ex}");
                }
            }
        }

        private static async Task RunApplicationAsync(string[] args, CancellationToken cancellationToken = default)
        {
            // Build host and DI container
            var host = BuildHost(args);

            // Syncfusion license already registered in Main() - do not call again

            // Initialize theme system
            InitializeTheme();

            // Capture UI synchronization context
            CaptureSynchronizationContext();

            // Create application-wide scope
            _applicationScope = host.Services.CreateScope();
            Services = _applicationScope.ServiceProvider;

            var timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(Services);
            using var phase = timelineService?.BeginPhaseScope("Application Startup Orchestration");

            var startupOrchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(Services);
            var hostEnvironment = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IHostEnvironment>(Services);

            if (hostEnvironment.IsDevelopment())
            {
                await startupOrchestrator.ValidateServicesAsync(Services, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                Log.Information("DI validation skipped for environment {Environment}", hostEnvironment.EnvironmentName);
            }

            await startupOrchestrator.InitializeThemeAsync(CancellationToken.None).ConfigureAwait(false);
            startupOrchestrator.GenerateStartupReport();

            // Create and show main form
            var mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(Services);

            // Initialize async components after form is shown
            mainForm.Shown += async (s, e) =>
            {
                try
                {
                    await mainForm.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize MainForm async components");
                    MessageBox.Show($"Initialization error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Application.Run(mainForm);
        }

        private static IHost BuildHost(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            AddConfiguration(builder);
            ConfigureLogging(builder);
            ConfigureDatabase(builder);

            // Register ReportViewerLaunchOptions before general DI
            AddReportViewerOptions(builder, args);

            AddDependencyInjection(builder);

            return builder.Build();
        }

        private static void AddConfiguration(HostApplicationBuilder builder)
        {
            builder.Configuration.SetBasePath(AppContext.BaseDirectory);
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
            builder.Configuration.AddEnvironmentVariables();

            if (builder.Environment.IsDevelopment())
            {
                builder.Configuration.AddUserSecrets<StartupOrchestrator>(optional: true);
            }
        }

        private static void AddReportViewerOptions(HostApplicationBuilder builder, string[] args)
        {
            var showReportViewer = false;
            string? reportPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--ShowReportViewer", StringComparison.OrdinalIgnoreCase))
                {
                    showReportViewer = true;
                }
                else if (args[i].Equals("--ReportPath", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    reportPath = args[i + 1];
                    i++; // Skip the next argument as it is the value
                }
            }

            var options = new ReportViewerLaunchOptions(showReportViewer, reportPath);
            builder.Services.AddSingleton(options);

            Log.Debug("ReportViewerLaunchOptions registered: ShowReportViewer={ShowReportViewer}, ReportPath={ReportPath}", showReportViewer, reportPath ?? "(none)");
        }

        private static void InitializeLogging()
        {
            // Create logs folder in application root if it doesn't exist
            var projectRoot = Directory.GetCurrentDirectory();
            var logsDirectory = Path.Combine(projectRoot, "logs");
            Directory.CreateDirectory(logsDirectory);

            var logFileTemplate = Path.Combine(logsDirectory, "wiley-widget-{Date}.log");

            // Configure Serilog with maximum verbosity (Verbose level) for comprehensive debugging
            // NO MinimumLevel overrides - all levels honored everywhere
            Log.Logger = new LoggerConfiguration()
                // Set minimum level to Verbose for everything - enforced globally
                .MinimumLevel.Verbose()
                // Suppress expected cancellation exceptions to reduce noisy logs
                .Filter.ByExcluding(logEvent =>
                {
                    var exception = logEvent.Exception;
                    if (exception == null)
                    {
                        return false;
                    }

                    if (exception is OperationCanceledException)
                    {
                        return true;
                    }

                    if (exception is AggregateException aggregate)
                    {
                        return aggregate.InnerExceptions.All(inner => inner is OperationCanceledException);
                    }

                    return false;
                })
                // Enrich with comprehensive context information
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProcessId()
                .Enrich.WithEnvironmentName()
                // Write to Console with compact ANSI theme + full exception details
                .WriteTo.Console(
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code,
                    formatProvider: CultureInfo.InvariantCulture,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}")
                // Write to file sink wrapped in async queue to prevent blocking
                .WriteTo.Async(
                    a => a.File(
                        logFileTemplate,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 31, // Keep last month of logs
                        fileSizeLimitBytes: 52428800, // 50MB per file
                        rollOnFileSizeLimit: true,
                        buffered: false, // Force immediate writes for debugging
                        shared: true, // Allow multiple processes to write
                        formatProvider: CultureInfo.InvariantCulture,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {MachineName} {ThreadId} {SourceContext} {Message:lj}{NewLine}{Exception}"),
                    bufferSize: 10000,        // Configure queue size to prevent blocking on file I/O
                    blockWhenFull: false)     // Do not block producer if queue is full; drop if necessary
                .CreateLogger();

            // Test logs to verify configuration - these should appear immediately in logs/wiley-widget-{Date}.log
            Log.Verbose("Serilog VERBOSE test - should appear in file");
            Log.Debug("Serilog DEBUG test");
            Log.Information("Serilog INFO test - logs folder should now have a file at {LogPath}", logFileTemplate);
        }

        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            builder.Services.AddSerilog();
        }

        private static void InitializeTheme()
        {
            try
            {
                // Load Syncfusion theme assemblies to support runtime theme switching
                // Per Syncfusion docs (https://help.syncfusion.com/windowsforms/skins/getting-started):
                // Only Office2016Theme, Office2019Theme, and HighContrastTheme require separate assemblies.
                // FluentTheme and MaterialTheme are NOT supported in Windows Forms.

                try
                {
                    Syncfusion.WinForms.Controls.SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                    Log.Debug("Successfully loaded Office2019Theme assembly");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load Office2019Theme assembly - Office2019 themes will not be available");
                }

                // Get theme from configuration (appsettings.json UI:Theme), fallback to Office2019Colorful
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();

                var themeName = config["UI:Theme"] ?? "Office2019Colorful";

                // Set application theme globally before MainForm is created
                Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme = themeName;

                Log.Information("Theme initialization completed. Active theme: {Theme}", themeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Theme initialization failed; continuing with default Windows theme");
            }
        }

        private static void CaptureSynchronizationContext()
        {
            UISynchronizationContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(UISynchronizationContext);
        }

        private static void ConfigureDatabase(HostApplicationBuilder builder)
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";

            builder.Services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("WileyWidget.Data");
                    sql.CommandTimeout(60);
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                });

                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging(builder.Configuration.GetValue("Database:EnableSensitiveDataLogging", false));
            }, ServiceLifetime.Scoped);
        }

        private static void AddDependencyInjection(HostApplicationBuilder builder)
        {
            // Register all application services via DependencyInjection helper
            var diServices = DependencyInjection.CreateServiceCollection(includeDefaults: false);

            // Skip IConfiguration descriptor - use the host builder's configuration
            foreach (var descriptor in diServices)
            {
                if (descriptor.ServiceType == typeof(IConfiguration))
                {
                    continue; // Skip - use host builder's configuration
                }

                builder.Services.Add(descriptor);
            }
        }

        public static async Task RunStartupHealthCheckAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            // Run optional health checks at startup; log and continue on failure to avoid blocking UI
            var healthCheckService = serviceProvider.GetService(typeof(HealthCheckService)) as HealthCheckService;
            if (healthCheckService is null)
            {
                return;
            }

            try
            {
                await healthCheckService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Startup health check failed");
            }
        }

        private static void HandleFatalException(Exception ex)
        {
            Log.Fatal(ex, "Fatal exception during application startup");

            var projectRoot = Directory.GetCurrentDirectory();
            var logPath = Path.Combine(projectRoot, "logs");
            var message = $"A fatal error occurred:\n\n{ex.Message}\n\nSee logs at {logPath} for details.";

            MessageBox.Show(message, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
