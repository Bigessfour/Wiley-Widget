using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Extensions.Logging;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.Models;
using WileyWidget.Services;

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
            // 🔑 Register Syncfusion License FIRST - before ANY Syncfusion component initialization
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());
            var tempConfig = configBuilder.Build();
            var syncfusionLicense = tempConfig["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(syncfusionLicense))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicense);
            }

            // Standard WinForms init (per Microsoft/Syncfusion best practices)
            ApplicationConfiguration.Initialize();  // Includes EnableVisualStyles() and SetCompatibleTextRenderingDefault(false)

            // Ensure a WindowsFormsSynchronizationContext is installed and capture it for UI-thread marshaling.
            if (System.Threading.SynchronizationContext.Current == null || !(System.Threading.SynchronizationContext.Current is WindowsFormsSynchronizationContext))
            {
                try
                {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                }
                catch { }
            }
            UISynchronizationContext = System.Threading.SynchronizationContext.Current;

            try
            {
                var builder = Host.CreateApplicationBuilder(args);  // ✅ MCP-validated for .NET 9

                // Re-add user secrets to the main configuration (already loaded above, but ensure consistency)
                builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());

                // Explicitly ensure appsettings.json is loaded into configuration (helps when running with different content root)
                try
                {
                    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                }
                catch { }

                // Set default connection if missing so we don't fallback unexpectedly at runtime
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
                        Log.Warning("DefaultConnection not found in configuration. Using in-memory fallback: {Connection}", defaultConn);
                    }
                }
                catch (Exception ex) { Log.Debug(ex, "Error ensuring default connection in configuration"); }

                // Initialize Syncfusion theme system for .NET 9+ compatibility
                try
                {
                    // Ensure .NET 9+ compatibility by setting color mode to Classic for Syncfusion themes
                    Application.SetColorMode(SystemColorMode.Classic);
                    // Load Office2019Theme assembly for theme support
                    SfSkinManager.LoadAssembly(typeof(Syncfusion.WinForms.Themes.Office2019Theme).Assembly);
                    // Set global application theme early (before form creation)
                    // Try new theme name first (post-2025), fallback to lowercase
                    try
                    {
                        SkinManager.ApplicationVisualTheme = "Office2019Colorful";
                        Log.Information("Syncfusion theme initialized successfully with theme: {Theme}", SkinManager.ApplicationVisualTheme);
                    }
                    catch
                    {
                        SkinManager.ApplicationVisualTheme = "office2019colorful";
                        Log.Information("Syncfusion theme initialized with fallback theme: {Theme}", SkinManager.ApplicationVisualTheme);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize Syncfusion theme system - using defaults");
                }
                // Serilog configuration - read settings from appsettings.json and configuration
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File("logs/winforms-di.log", formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3, shared: true)
                    .Enrich.FromLogContext()
                    .CreateLogger();
                // Wire Serilog as DI logging provider (remove note and enable for consistency)
                builder.Logging.ClearProviders();
                builder.Logging.AddSerilog(Log.Logger, dispose: true);
                // Now log the license
                if (!string.IsNullOrWhiteSpace(syncfusionLicense))
                {
                    Log.Information("Syncfusion license registered successfully");
                }
                else
                {
                    Log.Warning("No Syncfusion license key found - running in trial mode");
                }

                // Attach Serilog to DI so DI-resolved loggers are wired up

                // Capture first-chance DI exceptions so we get a stack trace even when the debugger swallows them
                AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
                {
                    var ex = eventArgs.Exception;
                    if ((ex is InvalidOperationException || ex is AggregateException) &&
                        ex.Source != null && ex.Source.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
                    {
                        Log.Error(ex, "First-chance DI exception");
                    }
                };

                // 💉 DI - ✅ MCP-validated patterns
                // Use existing DependencyInjection configuration
                var diServices = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();
                foreach (var descriptor in diServices)
                {
                    builder.Services.Add(descriptor);
                }

                // Configure DbContextFactory with database provider
                builder.Services.AddDbContextFactory<WileyWidget.Data.AppDbContext>(options =>
                {
                    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                        Log.Warning("DefaultConnection missing; using fallback SQL Server connection string");
                    }

                    // Expand environment variables
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

                    // Add EF Core logging for debugging database operations
                    var loggerFactory = LoggerFactory.Create(logging =>
                    {
                        logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
                        logging.AddConsole();
                        logging.AddDebug();
                    });
                    options.UseLoggerFactory(loggerFactory);
                });

                // Configure and register HealthCheckConfiguration from appsettings if present
                var healthChecksSection = builder.Configuration.GetSection("HealthChecks");
                var healthConfig = healthChecksSection.Get<HealthCheckConfiguration>() ?? new HealthCheckConfiguration();
                builder.Services.AddSingleton(healthConfig);

                IHost host;
                try
                {
                    host = builder.Build();
                }
                catch (Exception ex)
                {
                    try { Log.Fatal(ex, "Host build failed"); } catch { }
                    Log.CloseAndFlush();
                    throw;
                }

                Services = host.Services;  // Make services available statically for forms

                // If an ErrorReportingService exists, suppress user dialogs on startup (we prefer logging for automated runs)
                try
                {
                    var errorReporting = Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService;
                    if (errorReporting != null)
                    {
                        errorReporting.SuppressUserDialogs = true;
                    }
                }
                catch
                {
                    // ignore - best effort
                }

                // Support a lightweight verify mode used by CI / automated smoke tests. When the app is launched with --verify-startup
                // we start the host to run hosted startup services (StartupOrchestrator) and then exit without starting the UI.
                if (args != null && Array.Exists(args, a => string.Equals(a, "--verify-startup", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        Log.Information("Running in --verify-startup mode: starting host and verifying startup orchestration.");
                        host.StartAsync().GetAwaiter().GetResult();
                        Log.Information("Host started successfully (verify mode). Stopping host and exiting.");
                        host.StopAsync().GetAwaiter().GetResult();
                        Log.CloseAndFlush();
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex, "Verify-startup run failed");
                        Log.CloseAndFlush();
                        throw;
                    }
                }

                // Global error handling — log everything to Serilog / ErrorReportingService and do NOT show popup dialogs
                Application.ThreadException += (sender, e) => {
                    Log.Fatal(e.Exception, "Unhandled UI thread exception");
                    try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(e.Exception, "UI Thread Exception", showToUser: false); } catch { }
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                    var ex = e.ExceptionObject as Exception;
                    Log.Fatal(ex, "Unhandled AppDomain exception");
                    try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex ?? new InvalidOperationException("Unhandled domain exception"), "Domain exception", showToUser: false); } catch { }
                };

                // Start UI with MainForm (which contains dashboard as a component)
                Log.Information("🚀 WileyWidget MainForm starting...");

                using var mainForm = new MainForm(Services, builder.Configuration, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(host.Services) ?? NullLogger<MainForm>.Instance);

                // Optional: auto-close the main form after a specified interval (ms) when
                // the opt-in arg `--auto-close-ms=<milliseconds>` is provided. This is
                // intended for non-interactive smoke tests in CI or headless runs.
                try
                {
                    if (args != null)
                    {
                        int autoCloseMs = -1;
                        foreach (var a in args)
                        {
                            if (a != null && a.StartsWith("--auto-close-ms=", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = a.Split('=', 2);
                                if (parts.Length == 2 && int.TryParse(parts[1], out var ms) && ms > 0)
                                {
                                    autoCloseMs = ms;
                                    break;
                                }
                            }
                        }

                        if (autoCloseMs > 0)
                        {
                            try
                            {
                                // Schedule a background task to close the main form on the UI thread
                                System.Threading.Tasks.Task.Run(async () =>
                                {
                                    await System.Threading.Tasks.Task.Delay(autoCloseMs).ConfigureAwait(false);
                                    try
                                    {
                                        if (mainForm != null && !mainForm.IsDisposed)
                                        {
                                            try { mainForm.BeginInvoke(new System.Action(() => { try { mainForm.Close(); } catch { } })); } catch { }
                                        }
                                    }
                                    catch { }
                                });
                                Log.Information("Auto-close scheduled in {Ms}ms", autoCloseMs);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to schedule auto-close");
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    Application.Run(mainForm);
                }
                catch (Exception ex)
                {
                    try { Log.Fatal(ex, "Application.Run aborted with exception"); } catch { }
                    try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex, "UI message loop aborted", showToUser: false); } catch { }
                    throw;
                }

                // Normal shutdown - flush logs
                Log.Information("Application exited normally.");
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                // Ensure startup failures are recorded
                try
                {
                    Log.Fatal(ex, "Application failed to start");
                }
                catch { /* swallow logging errors - avoid hiding original exception */ }
                finally
                {
                    Log.CloseAndFlush();
                }

                // Prefer structured logging and error reporting (no startup popups in automated/dev runs)
                try { (Services?.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex, "Startup Failure", showToUser: false); } catch { }
                Log.Fatal(ex, "Application failed to start (startup failure)");
                throw;
            }
        }
    }
}
