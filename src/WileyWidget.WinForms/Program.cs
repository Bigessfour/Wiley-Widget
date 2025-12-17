using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Threading.Tasks;
using System.Globalization;
using Action = System.Action;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
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
        public static System.Threading.SynchronizationContext? UISynchronizationContext { get; private set; }

        [STAThread]
        static void Main(string[] args)
        {
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

            Console.WriteLine("Calling InitializeWinForms");
            InitializeWinForms();
            Console.WriteLine("InitializeWinForms completed");

            Console.WriteLine("Calling CaptureSynchronizationContext");
            CaptureSynchronizationContext();
            Console.WriteLine("CaptureSynchronizationContext completed");

            // Show splash screen early to prevent blank pause
            using var splash = new SplashForm();
            splash.Show();
            Application.DoEvents(); // Allow splash to paint

            try
            {
                // Use IStartupProgressReporter.Report() for granular progress tracking
                splash.Report(0.05, "Building dependency injection container...");
                using var host = BuildHost(args);
                using var uiScope = host.Services.CreateScope();
                Services = uiScope.ServiceProvider;

                splash.Report(0.15, "Registering licenses...");
                var config = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(host.Services);
                RegisterSyncfusionLicense(config);

                splash.Report(0.30, "Applying Office2019 theme...");
                InitializeTheme();

                splash.Report(0.40, "Configuring error reporting...");
                ConfigureErrorReporting();

                // Startup health check
                splash.Report(0.50, "Verifying database connectivity...");
                using (var healthScope = host.Services.CreateScope())
                {
                    RunStartupHealthCheckAsync(healthScope.ServiceProvider).GetAwaiter().GetResult(); // Safe: main thread before UI starts
                }

                // Run seeding on the threadpool with a timeout to avoid sync-over-async deadlocks
                splash.Report(0.60, "Seeding test data (if enabled)...");
                try
                {
                    var seedTask = Task.Run(() => UiTestDataSeeder.SeedIfEnabledAsync(host.Services));
                    if (!seedTask.Wait(TimeSpan.FromSeconds(60)))
                    {
                        Log.Warning("UI test data seeding timed out after {TimeoutSeconds}s", 60);
                    }
                }
                catch (Exception seedEx)
                {
                    Log.Warning(seedEx, "UI test data seeding failed");
                }

                if (IsVerifyStartup(args))
                {
                    splash.Complete("Startup verification complete");
                    RunVerifyStartup(host);
                    return;
                }

                splash.Report(0.75, "Wiring global exception handlers...");
                WireGlobalExceptionHandlers();

                splash.Report(0.85, "Initializing main window...");
                Console.WriteLine("Creating MainForm...");
                var mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(Services);
                Console.WriteLine("MainForm created successfully");

                // Complete splash screen with fade-out animation
                splash.Complete("Ready");

                ScheduleAutoCloseIfRequested(args, mainForm);
                Console.WriteLine("Starting application message loop...");
                RunUiLoop(mainForm);
            }
            catch (Exception ex)
            {
                HandleStartupFailure(ex);
                throw;
            }
        }

        private static void RegisterSyncfusionLicense(IConfiguration configuration)
        {
            try
            {
                var licenseKey = configuration["Syncfusion:LicenseKey"];
                if (string.IsNullOrEmpty(licenseKey))
                {
                    throw new InvalidOperationException("Syncfusion license key not found in configuration.");
                }
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);

                // Validate the license key
                bool isValid = Syncfusion.Licensing.SyncfusionLicenseProvider.ValidateLicense(Syncfusion.Licensing.Platform.WindowsForms);
                if (!isValid)
                {
                    throw new InvalidOperationException("Syncfusion license key is invalid or does not match the package versions.");
                }

                Log.Information("Syncfusion license registered and validated successfully.");
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
        /// Sets SfSkinManager.ApplicationVisualTheme globally for all forms and controls.
        /// Child forms automatically inherit this theme - do NOT call SetVisualStyle in form constructors.
        /// </summary>
        private static void InitializeTheme()
        {
            var themeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            try
            {
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                SfSkinManager.ApplicationVisualTheme = themeName;
                Log.Information("Theme initialized successfully: {ThemeName}", themeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Theme initialization failed; falling back to default Windows theme");
                // Optionally disable Syncfusion features or notify user
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

            return builder.Build();
        }

        private static void AddConfiguration(HostApplicationBuilder builder)
        {
            builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());
            try
            {
                builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load appsettings.json");
            }

            builder.Configuration.AddEnvironmentVariables();

            try
            {
                var existingConn = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(existingConn))
                {
                    var defaultConn = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                    builder.Configuration.AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
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
                var logsPath = Path.Combine(projectRoot, "src", "logs");

                // Fallback: if src/logs doesn't exist, try just logs in current directory
                if (!Directory.Exists(Path.Combine(projectRoot, "src")))
                {
                    logsPath = Path.Combine(projectRoot, "logs");
                }

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
                    sql.CommandTimeout(builder.Configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
                    sql.EnableRetryOnFailure(
                        maxRetryCount: builder.Configuration.GetValue<int>("Database:MaxRetryCount", 3),
                        maxRetryDelay: TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 10)),
                        errorNumbersToAdd: null);
                });

                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging(builder.Configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false));

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
            foreach (var descriptor in diServices)
            {
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
            try
            {
                // Create a scope for scoped services (DbContext)
                using var scope = services.CreateScope();
                var scopedServices = scope.ServiceProvider;

                var dbContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scopedServices);
                await dbContext.Database.CanConnectAsync();
                Log.Information("Startup health check passed: Database connection successful");

                // Get data statistics for diagnostic purposes — run on threadpool to avoid sync-over-async deadlock
                try
                {
                    using (var diagnosticScope = services.CreateScope())
                    {
                        var diagnosticScopedServices = diagnosticScope.ServiceProvider;
                        var dashboardService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Abstractions.IDashboardService>(diagnosticScopedServices);
                        if (dashboardService != null)
                        {
                            try
                            {
                                // Use Task.WhenAny for proper async timeout pattern instead of blocking .Wait()
                                var statsTask = dashboardService.GetDataStatisticsAsync();
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                                var completedTask = await Task.WhenAny(statsTask, timeoutTask).ConfigureAwait(false);

                                if (completedTask == statsTask)
                                {
                                    var stats = await statsTask.ConfigureAwait(false);
                                    Log.Information("Diagnostic: Database contains {RecordCount} budget entries (Oldest: {Oldest}, Newest: {Newest})",
                                        stats.TotalRecords,
                                        stats.OldestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                                        stats.NewestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A");

                                    if (stats.TotalRecords == 0)
                                    {
                                        Log.Warning("Diagnostic: Database has no budget entries. Dashboard will show empty data. Consider running data seeding scripts.");
                                    }
                                }
                                else
                                {
                                    Log.Warning("Diagnostic: GetDataStatisticsAsync timed out after {TimeoutSeconds}s", 30);
                                }
                            }
                            catch (Exception innerDiagEx)
                            {
                                Log.Warning(innerDiagEx, "Diagnostic: Failed to retrieve data statistics (threadpool execution)");
                            }
                        }
                        else
                        {
                            Log.Warning("Diagnostic: IDashboardService not available for data statistics check");
                        }
                    }
                }
                catch (Exception diagEx)
                {
                    Log.Warning(diagEx, "Diagnostic: Failed to retrieve data statistics");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Startup health check failed: Database connection issue");
                // Don't throw here, let the app start and log the issue
            }
        }

        private static bool IsVerifyStartup(string[] args)
        {
            return args != null && Array.Exists(args, a => string.Equals(a, "--verify-startup", StringComparison.OrdinalIgnoreCase));
        }

        private static void RunVerifyStartup(IHost host)
        {
            try
            {
                host.StartAsync().GetAwaiter().GetResult();
                host.StopAsync().GetAwaiter().GetResult();
                Log.CloseAndFlush();
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
                        mainForm.BeginInvoke(new System.Action(() =>
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
                var logPath = Path.Combine(projectRoot, "src", "logs");
                if (!Directory.Exists(Path.Combine(projectRoot, "src")))
                {
                    logPath = Path.Combine(projectRoot, "logs");
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
    }

    /// <summary>
    /// Minimal splash implementation used by the UI startup sequence.
    /// Provides a lightweight on-screen splash for interactive runs and
    /// a headless no-op for CI/test environments.
    /// </summary>
    internal sealed class SplashForm : IDisposable
    {
        private readonly bool _isHeadless;
        private readonly Form? _form;
        private readonly Label? _messageLabel;
        private readonly ProgressBar? _progressBar;

        public SplashForm()
        {
            // Run headless during UI tests or non-interactive contexts
            _isHeadless = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                          || !Environment.UserInteractive;

            if (_isHeadless)
            {
                return;
            }

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
        }

        public void Show()
        {
            if (_isHeadless || _form == null) return;
            try { _form.Show(); } catch { /* Ignore UI failures */ }
        }

        public void Report(double progress, string message, bool isIndeterminate = false)
        {
            if (_isHeadless)
            {
                try { Console.WriteLine($"{message} ({(int)(progress * 100)}%)"); } catch { }
                return;
            }

            if (_form == null) return;

            try
            {
                if (_form.InvokeRequired)
                {
                    _form.BeginInvoke(new Action(() => Report(progress, message, isIndeterminate)));
                    return;
                }

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

                _form.Refresh();
                Application.DoEvents();
            }
            catch
            {
                // Swallow errors from reporting to avoid breaking startup
            }
        }

        public void Complete(string finalMessage)
        {
            if (_isHeadless)
            {
                if (!string.IsNullOrEmpty(finalMessage)) Console.WriteLine(finalMessage);
                return;
            }

            if (_form == null) return;

            try
            {
                if (_form.InvokeRequired)
                {
                    _form.BeginInvoke(new Action(() => Complete(finalMessage)));
                    return;
                }

                _messageLabel!.Text = finalMessage ?? string.Empty;
                _progressBar!.Value = _progressBar.Maximum;
                _form.Refresh();
                Application.DoEvents();

                // Close the splash after a brief delay so the user can see the final message
                Task.Run(async () =>
                {
                    await Task.Delay(200).ConfigureAwait(false);
                    try
                    {
                        if (!_form.IsDisposed)
                        {
                            _form.BeginInvoke(new Action(() => { try { _form.Close(); } catch { } }));
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        public void Dispose()
        {
            try { _form?.Dispose(); } catch { }
        }
    }
}
