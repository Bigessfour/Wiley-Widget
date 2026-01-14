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

            // Initialize logging first
            InitializeLogging();

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
                Log.CloseAndFlush();
            }
        }

        private static async Task RunApplicationAsync(string[] args)
        {
            // Build host and DI container
            var host = BuildHost(args);

            // Initialize Syncfusion licensing
            InitializeSyncfusionLicense(host.Services);

            // Initialize theme system
            InitializeTheme();

            // Capture UI synchronization context
            CaptureSynchronizationContext();

            // Create application-wide scope
            _applicationScope = host.Services.CreateScope();
            Services = _applicationScope.ServiceProvider;

            var startupOrchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(Services);
            await startupOrchestrator.ValidateServicesAsync(Services, CancellationToken.None).ConfigureAwait(false);
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

        private static void InitializeLogging()
        {
            var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            Directory.CreateDirectory(logsPath);
            var logFileTemplate = Path.Combine(logsPath, "app-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.File(logFileTemplate,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger();
        }

        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            builder.Services.AddSerilog();
        }

        private static void InitializeSyncfusionLicense(IServiceProvider services)
        {
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(services);
            var licenseKey = configuration["Syncfusion:LicenseKey"];

            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                Log.Debug("Syncfusion license registered successfully");
            }
            else
            {
                Log.Warning("Syncfusion license key not found in configuration");
            }
        }

        private static void InitializeTheme()
        {
            try
            {
                // Load all Syncfusion theme assemblies to support runtime theme switching
                // This enables: Office2019Colorful, Office2019Black, Office2019White,
                //              FluentLight, FluentDark, MaterialLight, MaterialDark
                try { Syncfusion.WinForms.Controls.SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly); } catch { }
                try
                {
                    var fluentAssembly = System.Reflection.Assembly.Load("Syncfusion.FluentTheme.WinForms");
                    Syncfusion.WinForms.Controls.SfSkinManager.LoadAssembly(fluentAssembly);
                }
                catch { }
                try
                {
                    var materialAssembly = System.Reflection.Assembly.Load("Syncfusion.MaterialTheme.WinForms");
                    Syncfusion.WinForms.Controls.SfSkinManager.LoadAssembly(materialAssembly);
                }
                catch { }

                // Get theme from configuration (appsettings.json UI:Theme), fallback to Office2019Colorful
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();

                var themeName = config["UI:Theme"] ?? "Office2019Colorful";

                // Set application theme globally before MainForm is created
                Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme = themeName;

                Log.Debug("Theme initialization completed successfully. Available themes loaded. Active theme: {Theme}", themeName);
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
