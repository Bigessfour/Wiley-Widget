using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;
using System.IO;
using Serilog;
using Serilog.Debugging;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Abstractions;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget
{
    public partial class App : Application
    {
        public static IServiceProvider? Services { get; private set; }
        private Window? m_window;
        private static StreamWriter? _selfLogWriter;

        public App()
        {
            // Enable Serilog self-diagnostics FIRST (before any Serilog operations)
            EnableSerilogSelfLog();
            
            // Setup logging BEFORE any Log.* calls
            SetupLogging();
            
            // Attempt to register Syncfusion license before initializing components
            TryRegisterSyncfusionLicense();

            this.InitializeComponent();
        }

        private void EnableSerilogSelfLog()
        {
            try
            {
                // Determine project root for self-log file
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var projectRoot = FindProjectRootQuick(baseDir);
                var logsDir = Path.Combine(projectRoot, "logs");
                Directory.CreateDirectory(logsDir);
                
                var selfLogPath = Path.Combine(logsDir, "serilog-selflog.txt");
                
                // Create writer and store reference for proper disposal
                _selfLogWriter = File.CreateText(selfLogPath);
                
                // Use synchronized writer for thread-safety
                var syncWriter = TextWriter.Synchronized(_selfLogWriter);
                SelfLog.Enable(syncWriter);
                
                // Also write to Debug output
                SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine($"[SERILOG] {msg}"));
                
                System.Diagnostics.Debug.WriteLine($"Serilog SelfLog enabled at: {selfLogPath}");
            }
            catch (Exception ex)
            {
                // Fallback to console if file creation fails
                System.Diagnostics.Debug.WriteLine($"Failed to create SelfLog file: {ex.Message}");
                SelfLog.Enable(Console.Error);
            }
        }
        
        private string FindProjectRootQuick(string startDir)
        {
            var current = new DirectoryInfo(startDir);
            while (current != null)
            {
                if (current.GetFiles("*.csproj").Any())
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            return startDir;
        }

        private void TryRegisterSyncfusionLicense()
        {
            try
            {
                // 1) Environment variable (preferred)
                string? licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(licenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey.Trim());
                    var maskedKey = licenseKey.Length > 10 ? licenseKey.Substring(0, 10) + "..." : "***";
                    Log.Information("Syncfusion license registered from environment variable (key: {MaskedKey})", maskedKey);
                    System.Diagnostics.Debug.WriteLine("Syncfusion license registered from environment variable.");
                    return;
                }

                // 2) license.key file next to executable
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var licenseFile = Path.Combine(baseDir, "license.key");
                Log.Debug("Checking for license file at: {LicenseFile}", licenseFile);
                
                if (File.Exists(licenseFile))
                {
                    var fileKey = File.ReadAllText(licenseFile).Trim();
                    if (!string.IsNullOrWhiteSpace(fileKey))
                    {
                        SyncfusionLicenseProvider.RegisterLicense(fileKey);
                        Log.Information("Syncfusion license registered from file: {LicenseFile}", licenseFile);
                        System.Diagnostics.Debug.WriteLine($"Syncfusion license registered from file: {licenseFile}");
                        return;
                    }
                }

                // No license found — leave to environment or external registration
                Log.Warning("Syncfusion license NOT registered (no env var or license.key found)");
                System.Diagnostics.Debug.WriteLine("Syncfusion license NOT registered (no env var or license.key found).");
            }
            catch (Exception ex)
            {
                // Avoid throwing during app construction; log to Debug only
                Log.Error(ex, "Syncfusion license registration failed");
                System.Diagnostics.Debug.WriteLine($"Syncfusion license registration failed: {ex}");
            }
        }

        private void SetupLogging()
        {
            try
            {
                // Determine project root directory (walk up from bin to find .csproj)
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var projectRoot = FindProjectRoot(baseDir);
                
                // Ensure logs directory exists in project root
                var logsDir = Path.Combine(projectRoot, "logs");
                Directory.CreateDirectory(logsDir);

                // Configure Serilog with enrichers and detailed formatting
                var logPath = Path.Combine(logsDir, "app.log");
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.WithThreadId()
                    .Enrich.WithProcessId()
                    .Enrich.WithProcessName()
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithMachineName()
                    .WriteTo.File(
                        logPath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture,
                        retainedFileCountLimit: 7)
                    .WriteTo.Debug(
                        outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.Console(
                        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture)
                    .CreateLogger();

                // Add global exception handler
                Microsoft.UI.Xaml.Application.Current.UnhandledException += (sender, e) =>
                {
                    Log.Error(e.Exception, "Unhandled exception occurred in UI thread");
                    e.Handled = true; // Prevent app crash for logged errors
                };

                Log.Information("==================== APPLICATION START ====================");
                Log.Information("Logging system initialized successfully");
                Log.Information("Log file location: {LogPath}", logPath);
                Log.Information("Project root: {ProjectRoot}", projectRoot);
                Log.Information("Base directory: {BaseDir}", baseDir);
            }
            catch (Exception ex)
            {
                // Fallback to Debug if logging setup fails
                System.Diagnostics.Debug.WriteLine($"Failed to setup logging: {ex}");
                throw;
            }
        }

        private string FindProjectRoot(string startDir)
        {
            var current = new DirectoryInfo(startDir);
            while (current != null)
            {
                // Look for .csproj file
                if (current.GetFiles("*.csproj").Any())
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            // Fallback to start directory if project root not found
            return startDir;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE"));
            var launchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Log.Information("==================== APPLICATION LAUNCH ====================");
                Log.Information("Launch arguments: {Args}", args?.Arguments ?? "<none>");
                Log.Information("Launch kind: {Kind}", args?.UWPLaunchActivatedEventArgs?.Kind.ToString() ?? "<unknown>");

                // DI Bootstrap - safe after InitializeComponent
                Log.Information("Starting DI container setup");
                var services = new ServiceCollection();
                
                // Add logging with Serilog
                services.AddLogging(builder =>
                {
                    builder.AddSerilog();
                });
                Log.Debug("Logging services added to DI");

                // Add configuration (appsettings.json optional + environment)
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();
                Log.Debug("Configuration built from appsettings.json and environment");

                services.AddSingleton<IConfiguration>(configuration);

                // Register UI pages and viewmodels for DI
                services.AddTransient<Views.BudgetOverviewPage>();
                services.AddTransient<ViewModels.BudgetOverviewViewModel>();
                Log.Debug("UI pages and ViewModels registered");

                // Register memory cache and cache service (direct to avoid extension null quirk)
                services.AddMemoryCache();
                services.AddSingleton<ICacheService, MemoryCacheService>();
                Log.Debug("Memory cache and cache service registered");

                // Try to wire backend data services. Use explicit registrations when project assemblies are available.
                try
                {
                    Log.Information("Attempting to register backend data services");
                    // AppDbContextFactory has constructors that accept IConfiguration
                    var dbFactory = new AppDbContextFactory(configuration);
                    services.AddSingleton<IDbContextFactory<AppDbContext>>(dbFactory);

                    // Register BudgetRepository
                    services.AddScoped<IBudgetRepository, BudgetRepository>();

                    // Register other Data services as needed (Transaction repository, etc.) if present
                    Log.Information("Backend data services registered successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Backend registration failed, continuing without data services");
                }

                Services = services.BuildServiceProvider();
                Log.Information("DI container built successfully");

                Log.Information("Creating main window");
                
                // Verify critical services are registered
                var budgetPage = Services.GetService<Views.BudgetOverviewPage>();
                Log.Debug("BudgetOverviewPage service resolution: {IsRegistered}", budgetPage != null ? "Success" : "Failed");
                
                m_window = new MainWindow();
                m_window.Activate();
                
                launchStopwatch.Stop();
                Log.Information("Main window activated successfully");
                Log.Information("==================== LAUNCH COMPLETE (Elapsed: {ElapsedMs}ms) ====================", launchStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                launchStopwatch.Stop();
                Log.Fatal(ex, "Critical error during application launch (Elapsed: {ElapsedMs}ms)", launchStopwatch.ElapsedMilliseconds);
                Log.CloseAndFlush(); // Ensure all logs are written before crash
                throw; // Re-throw to crash the app
            }
        }
    }
}
