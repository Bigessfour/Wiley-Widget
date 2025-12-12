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
using WileyWidget.Data;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.WinForms.Services;

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
            RegisterSyncfusionLicense();
            InitializeWinForms();
            CaptureSynchronizationContext();

            try
            {
                using var host = BuildHost(args);
                using var uiScope = host.Services.CreateScope();
                Services = uiScope.ServiceProvider;

                InitializeTheme();

                ConfigureErrorReporting();

                UiTestDataSeeder.SeedIfEnabledAsync(uiScope.ServiceProvider).GetAwaiter().GetResult();

                if (IsVerifyStartup(args))
                {
                    RunVerifyStartup(host);
                    return;
                }

                WireGlobalExceptionHandlers();

                using var mainForm = CreateMainForm(uiScope.ServiceProvider);
                ScheduleAutoCloseIfRequested(args, mainForm);
                RunUiLoop(mainForm);
            }
            catch (Exception ex)
            {
                HandleStartupFailure(ex);
                throw;
            }
        }

        private static void RegisterSyncfusionLicense()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());
            var tempConfig = configBuilder.Build();
            var syncfusionLicense = tempConfig["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(syncfusionLicense))
            {
                SyncfusionLicenseProvider.RegisterLicense(syncfusionLicense);
                Log.Debug("Syncfusion license registered from user secrets");
            }
        }

        private static void InitializeWinForms()
        {
            ApplicationConfiguration.Initialize();
        }

        private static void InitializeTheme()
        {
            var themeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

            try
            {
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load Office2019 theme assembly via SfSkinManager");
            }

            try
            {
                SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                SkinManager.ApplicationVisualTheme = themeName;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to set application visual theme to {Theme}", themeName);
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
                    Log.Debug(syncEx, "Failed to set WindowsFormsSynchronizationContext; continuing with existing context");
                }
            }

            UISynchronizationContext = System.Threading.SynchronizationContext.Current;
        }

        private static IHost BuildHost(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            AddConfiguration(builder);
            ConfigureLogging(builder);
            ConfigureDatabase(builder);
            ConfigureHealthChecks(builder);
            CaptureDiFirstChanceExceptions();
            AddDependencyInjection(builder);

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
                Log.Debug(ex, "Error ensuring default connection in configuration");
            }
        }

        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.File("logs/winforms-di.log", formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3, shared: true)
                .Enrich.FromLogContext()
                .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger, dispose: true);
        }

        private static void ConfigureDatabase(HostApplicationBuilder builder)
        {
            void ConfigureSqlOptions(DbContextOptionsBuilder options)
            {
                var useInMemory = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY"), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
                    || builder.Configuration.GetValue<bool>("UI:UseInMemoryForTests", false);

                if (useInMemory)
                {
                    options.UseInMemoryDatabase("WileyWidgetUiTests");
                    Log.Information("Using InMemory database for UI tests");
                    return;
                }

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

                var loggerFactory = LoggerFactory.Create(logging =>
                {
                    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddDebug();
                });
                options.UseLoggerFactory(loggerFactory);
            }

            // Use scoped lifetime for the factory to avoid resolving scoped options from the root provider
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
                    Log.Error(ex, "First-chance DI exception");
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

        private static void ConfigureErrorReporting()
        {
            try
            {
                var errorReporting = Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService;
                if (errorReporting != null)
                {
                    errorReporting.SuppressUserDialogs = true;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to configure ErrorReportingService");
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
                Log.Fatal(e.Exception, "Unhandled UI thread exception");
                try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(e.Exception, "UI Thread Exception", showToUser: false); } catch (Exception reportEx) { Log.Debug(reportEx, "Failed to report UI thread exception to ErrorReportingService"); }
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log.Fatal(ex, "Unhandled AppDomain exception");
                try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex ?? new InvalidOperationException("Unhandled domain exception"), "Domain exception", showToUser: false); } catch (Exception reportEx) { Log.Debug(reportEx, "Failed to report AppDomain exception to ErrorReportingService"); }
            };
        }

        private static MainForm CreateMainForm(IServiceProvider serviceProvider)
        {
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(serviceProvider);
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
                    Log.Debug(autoCloseEx, "Auto-close task failed");
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
                try { Log.Fatal(ex, "Application.Run aborted with exception"); } catch (Exception logEx) { Log.Debug(logEx, "Failed to log Application.Run fatal during shutdown"); }
                try { (Services.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex, "UI message loop aborted", showToUser: false); } catch (Exception reportEx) { Log.Debug(reportEx, "Failed to report Application.Run abort to ErrorReportingService"); }
                throw new InvalidOperationException("UI message loop aborted", ex);
            }
            finally
            {
                Log.Information("Application exited normally.");
                Log.CloseAndFlush();
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
                Log.Debug(logEx, "Failed to log startup fatal error");
            }
            finally
            {
                Log.CloseAndFlush();
            }

            try
            {
                (Services?.GetService(typeof(ErrorReportingService)) as ErrorReportingService)?.ReportError(ex, "Startup Failure", showToUser: false);
            }
            catch (Exception reportEx)
            {
                Log.Debug(reportEx, "Failed to report startup failure to ErrorReportingService");
            }
        }
    }
}
