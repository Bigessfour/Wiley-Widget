using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.Http;
using Action = System.Action;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Security.Permissions;
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
        public static SynchronizationContext? UISynchronizationContext { get; private set; }
        private static IStartupTimelineService? _timelineService;

        [STAThread]
        static async Task Main(string[] args)
        {
            // ENHANCED EXCEPTION DIAGNOSTICS
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
            DotNetEnv.Env.Load("secrets/my.secrets");
            DotNetEnv.Env.Load();
            Log.Debug("Main method started");
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                if (resolveArgs.Name != null && resolveArgs.Name.StartsWith("Microsoft.WinForms.Utilities.Shared", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                return null;
            };

            SplashForm? splash = null;
            try
            {
                splash = new SplashForm();
                void SplashReport(double progress, string message, bool isIndeterminate = false)
                {
                    var s = splash;
                    if (s == null) return;
                    s.InvokeOnUiThread(() => s.Report(progress, message, isIndeterminate));
                }

                void SplashComplete(string message)
                {
                    var s = splash;
                    if (s == null) return;
                    s.InvokeOnUiThread(() => s.Complete(message));
                }

                SplashReport(0.05, "Building dependency injection container...");
                var hostBuildScope = System.Diagnostics.Stopwatch.StartNew();
                IHost host = BuildHost(args);
                hostBuildScope.Stop();
                Log.Debug("DI Container built in {Elapsed}ms", hostBuildScope.ElapsedMilliseconds);
                Log.Information("Startup milestone: DI Container Build complete");

                // Register Syncfusion license immediately after configuration is available
                var mainConfig = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(host.Services);
                var xaiKey = mainConfig["XAI:ApiKey"];
                var syncKey = mainConfig["Syncfusion:LicenseKey"];
                Log.Debug("[CONFIG] XAI:ApiKey present={Present}, length={Length}", xaiKey != null, xaiKey?.Length ?? 0);
                Log.Debug("[CONFIG] Syncfusion:LicenseKey present={Present}, length={Length}", syncKey != null, syncKey?.Length ?? 0);
                var startupOrchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(host.Services);

                using (_timelineService?.BeginPhaseScope("License Registration"))
                {
                    await startupOrchestrator.RegisterLicenseAsync().ConfigureAwait(false);
                    Log.Information("Startup milestone: License Registration complete");
                }

                using (_timelineService?.BeginPhaseScope("Theme Initialization"))
                {
                    await startupOrchestrator.InitializeThemeAsync().ConfigureAwait(false);
                    Log.Information("Startup milestone: Theme Initialization complete");
                }

                InitializeWinForms();
                Log.Information("Startup milestone: WinForms Initialization complete");

                SplashReport(0.10, "Validating configuration secrets...");
                using (_timelineService?.BeginPhaseScope("Secret Validation"))
                {
                    var secretServices = host.Services;
                    // Offload secret validation to background thread to avoid UI thread blocking
                    await Task.Run(() => ValidateSecrets(secretServices)).ConfigureAwait(false);
                    SplashReport(0.12, "Secrets validated");
                }

                using var uiScope = host.Services.CreateScope();
                Services = uiScope.ServiceProvider;

                _timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(Services);
                if (_timelineService != null && _timelineService.IsEnabled)
                {
                    Log.Debug("[TIMELINE] StartupTimelineService enabled - tracking startup phases");
                }

                var mainXaiKey = mainConfig["XAI:ApiKey"];
                Log.Debug("[MAIN CONFIG] XAI:ApiKey present={Present}, length={Length}", mainXaiKey != null, mainXaiKey?.Length ?? 0);

                using (_timelineService?.BeginPhaseScope("SynchronizationContext Capture"))
                {
                    CaptureSynchronizationContext();
                }

                SplashReport(0.10, "Validating service registration...");
                using (var validationScope = _timelineService?.BeginPhaseScope("DI Validation"))
                {
                    // Run DI validation on threadpool to keep startup UI responsive
                    var validationTask = Task.Run(async () =>
                        await startupOrchestrator.ValidateServicesAsync(uiScope.ServiceProvider, CancellationToken.None).ConfigureAwait(false));
                    await validationTask.ConfigureAwait(false);
                    SplashReport(0.18, "DI validation complete");
                }

                SplashReport(0.15, "Preparing theme system...");

                SplashReport(0.40, "Configuring error reporting...");
                _timelineService?.RecordOperation("Configure error reporting", "Error Handlers");
                ConfigureErrorReporting();

                // Verification mode: run deeper checks before exiting
                if (IsVerifyStartup(args))
                {
                    Log.Information("Startup verification mode active; running verification and exiting.");

                    SplashReport(0.50, "Verifying database connectivity...");
                    using (var healthScope = host.Services.CreateScope())
                    {
                        using (_timelineService?.BeginPhaseScope("Database Health Check"))
                        {
                            SplashReport(0.60, "Checking database health...");
                            await Task.Run(async () => await RunStartupHealthCheckAsync(healthScope.ServiceProvider).ConfigureAwait(false)).ConfigureAwait(false);
                            SplashReport(0.65, "Database verified");
                        }
                    }

                    SplashComplete("Startup verification complete");
                    await RunVerifyStartup(host);
                    return;
                }

                SplashReport(0.75, "Initializing main window...");
                MainForm mainForm;
                using (var mainFormScope = _timelineService?.BeginPhaseScope("MainForm Creation"))
                {
                    mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(Services);
                }
                Log.Debug("MainForm created");

                SplashReport(0.80, "Wiring global exception handlers...");
                using (var handlerScope = _timelineService?.BeginPhaseScope("Chrome Initialization"))
                {
                    WireGlobalExceptionHandlers();
                }
                Log.Debug("Exception handlers wired");

                SplashReport(0.95, "Ready");
                using (var splashHideScope = _timelineService?.BeginPhaseScope("Splash Screen Hide"))
                {
                    SplashComplete("Ready");
                    splash.Dispose();
                }
                Log.Information("Startup milestone: Ready");

                ScheduleAutoCloseIfRequested(args, mainForm);

                Log.Debug("Entering UI message loop");
                using (var uiLoopScope = _timelineService?.BeginPhaseScope("UI Message Loop"))
                {
                    RunUiLoop(mainForm);
                }
                Log.Debug("UI message loop exited");
            }
            catch (NullReferenceException nreEx)
            {
                Log.Fatal(nreEx, "═══ NULLREFERENCEEXCEPTION DURING STARTUP ═══\n" +
                    "Exception Type: {ExceptionType}\n" +
                    "Message: {Message}\n" +
                    "StackTrace:\n{StackTrace}\n" +
                    "Source: {Source}\n" +
                    "TargetSite: {TargetSite}\n" +
                    "HResult: {HResult}",
                    nreEx.GetType().FullName,
                    nreEx.Message,
                    nreEx.StackTrace ?? "(no stack trace)",
                    nreEx.Source ?? "(unknown)",
                    nreEx.TargetSite?.ToString() ?? "(unknown)",
                    nreEx.HResult);

                Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
                Console.WriteLine("║  CRITICAL: NullReferenceException During Startup");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
                Console.WriteLine($"Type:       {nreEx.GetType().FullName}");
                Console.WriteLine($"Message:    {nreEx.Message}");
                Console.WriteLine($"Source:     {nreEx.Source ?? "(unknown)"}");
                Console.WriteLine($"TargetSite: {nreEx.TargetSite?.ToString() ?? "(unknown)"}");
                Console.WriteLine($"HResult:    0x{nreEx.HResult:X8}");
                Console.WriteLine("\nStack Trace:");
                Console.WriteLine(nreEx.StackTrace ?? "(no stack trace available)");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

                HandleStartupFailure(nreEx);
                throw;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "═══ UNHANDLED EXCEPTION DURING STARTUP ═══\n" +
                    "Exception Type: {ExceptionType}\n" +
                    "Message: {Message}\n" +
                    "StackTrace:\n{StackTrace}\n" +
                    "Source: {Source}\n" +
                    "TargetSite: {TargetSite}\n" +
                    "HResult: {HResult}\n" +
                    "InnerException: {InnerException}",
                    ex.GetType().FullName,
                    ex.Message,
                    ex.StackTrace ?? "(no stack trace)",
                    ex.Source ?? "(unknown)",
                    ex.TargetSite?.ToString() ?? "(unknown)",
                    ex.HResult,
                    ex.InnerException?.ToString() ?? "(none)");

                Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
                Console.WriteLine("║  CRITICAL: Unhandled Exception During Startup");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════");
                Console.WriteLine($"Type:       {ex.GetType().FullName}");
                Console.WriteLine($"Message:    {ex.Message}");
                Console.WriteLine($"Source:     {ex.Source ?? "(unknown)"}");
                Console.WriteLine($"TargetSite: {ex.TargetSite?.ToString() ?? "(unknown)"}");
                Console.WriteLine($"HResult:    0x{ex.HResult:X8}");
                Console.WriteLine("\nStack Trace:");
                Console.WriteLine(ex.StackTrace ?? "(no stack trace available)");
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine($"  Type:    {ex.InnerException.GetType().FullName}");
                    Console.WriteLine($"  Message: {ex.InnerException.Message}");
                    Console.WriteLine($"  Stack:   {ex.InnerException.StackTrace ?? "(no stack trace)"}");
                }
                Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

                HandleStartupFailure(ex);
                throw;
            }
        }

        /// <summary>
        /// FirstChanceException handler for comprehensive exception diagnostics.
        /// Captures ALL exceptions thrown in the AppDomain BEFORE they are caught.
        /// Provides detailed logging for NullReferenceExceptions with full stack traces.
        /// </summary>
        private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is NullReferenceException nre)
            {
                // Log NullReferenceExceptions with full diagnostic details
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                Console.WriteLine($"\n[{timestamp}] ═══ FIRST CHANCE: NullReferenceException ═══");
                Console.WriteLine($"Message:    {nre.Message}");
                Console.WriteLine($"Source:     {nre.Source ?? "(unknown)"}");
                Console.WriteLine($"TargetSite: {nre.TargetSite?.ToString() ?? "(unknown)"}");
                Console.WriteLine($"HResult:    0x{nre.HResult:X8}");
                Console.WriteLine("Stack Trace:");
                Console.WriteLine(nre.StackTrace ?? "(no stack trace available)");
                Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

                // Also log via Serilog if available
                try
                {
                    Log.Warning(nre, "[FIRST CHANCE] NullReferenceException detected at {TargetSite}",
                        nre.TargetSite?.ToString() ?? "(unknown)");
                }
                catch
                {
                    // Serilog might not be initialized yet - ignore
                }
            }
            else if (e.Exception is InvalidOperationException ioe &&
                     (ioe.Source?.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) == true))
            {
                // Log DI-related exceptions for additional diagnostics
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                Console.WriteLine($"\n[{timestamp}] ═══ FIRST CHANCE: DI Exception ═══");
                Console.WriteLine($"Message: {ioe.Message}");
                Console.WriteLine($"Stack:   {ioe.StackTrace ?? "(no stack)"}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
            }
        }

        private static void RegisterSyncfusionLicense(IConfiguration configuration)
        {
            try
            {
                var licenseKey = configuration["Syncfusion:LicenseKey"];
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    throw new InvalidOperationException("Syncfusion license key not found in configuration.");
                }
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);

                // Validate the license key
                // bool isValid = Syncfusion.Licensing.SyncfusionLicenseProvider.ValidateLicense(Syncfusion.Licensing.Platform.WindowsForms);
                // if (!isValid)
                // {
                //     throw new InvalidOperationException("Syncfusion license key is invalid or does not match the package versions.");
                // }

                Log.Debug("Syncfusion license registered successfully.");
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
        /// Uses SkinManager.LoadAssembly and SkinManager.ApplicationVisualTheme (CORRECT API).
        /// Child forms automatically inherit this theme - do NOT call SetVisualStyle in form constructors.
        /// Reference: https://help.syncfusion.com/windowsforms/skins/getting-started
        /// </summary>
        private static void InitializeTheme()
        {
            try
            {
                Log.Debug("Starting theme initialization");

                // Null guard for timeline service
                if (_timelineService != null)
                {
                    _timelineService.RecordOperation("Load Office2019Theme assembly", "Theme Initialization");
                }

                // CRITICAL: Load theme assembly FIRST before setting ApplicationVisualTheme
                // Reference: Syncfusion WinForms Skins documentation
                try
                {
                    SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                    Log.Debug("Office2019Theme assembly loaded successfully");
                }
                catch (Exception loadEx)
                {

                    Log.Error(loadEx, "Failed to load Office2019Theme assembly");
                    throw; // Rethrow to outer catch for comprehensive handling
                }

                // Apply global theme - use default from ThemeColors (fallback to Office2019Colorful)
                var themeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                Log.Debug("Setting ApplicationVisualTheme to: {ThemeName}", themeName);

                if (_timelineService != null)
                {
                    _timelineService.RecordOperation($"Set ApplicationVisualTheme: {themeName}", "Theme Initialization");
                }

                // CRITICAL: Set ApplicationVisualTheme AFTER assembly load and BEFORE any form creation
                // This must be set after Application.EnableVisualStyles() for proper rendering
                try
                {
                    SkinManager.ApplicationVisualTheme = themeName;  // Global application-wide theme
                    Log.Debug("ApplicationVisualTheme set to: {ThemeName}", themeName);
                }
                catch (Exception setEx)
                {

                    Log.Error(setEx, "Failed to set ApplicationVisualTheme to {ThemeName}", themeName);
                    throw; // Rethrow to outer catch for comprehensive handling
                }

                Log.Debug("Theme initialization completed successfully");
            }
            catch (Exception ex)
            {
                // COMPREHENSIVE ERROR LOGGING

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[THEME FATAL] InnerException: {ex.InnerException.Message}");
                    Console.WriteLine($"[THEME FATAL] InnerException StackTrace: {ex.InnerException.StackTrace}");
                }

                Log.Error(ex, "Theme initialization failed; continuing with default Windows theme");

                // GRACEFUL FALLBACK: Continue without theme to prevent startup failure
                // Forms will use default Windows styling
                Console.WriteLine("[THEME] Continuing startup with default Windows theme (no Syncfusion theming)");
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
                Log.Debug("Default font set successfully: {FontName} {FontSize}pt",
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
            // Don't manually set WindowsFormsSynchronizationContext before Application.Run
            // Application.Run will set it automatically when the message pump starts
            // Just capture whatever context is current at this point
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

            // DEBUG: Check config BEFORE Build()
            var preBuildXai = builder.Configuration["XAI:ApiKey"];
            Log.Debug("[PRE-BUILD] XAI:ApiKey present={Present}, length={Length}", preBuildXai != null, preBuildXai?.Length ?? 0);

            // Register a global HttpClient with a sensible default timeout to avoid blocking external calls during startup
            try
            {
                var httpTimeoutSeconds = builder.Configuration.GetValue<int>("HttpClient:TimeoutSeconds", 30);
                // Register a named default HttpClient with configured timeout
                builder.Services.AddHttpClient("WileyWidgetDefault", c => c.Timeout = TimeSpan.FromSeconds(httpTimeoutSeconds));
                Log.Debug("[CONFIG] Registered global (named) HttpClient 'WileyWidgetDefault' with {Timeout}s timeout", httpTimeoutSeconds);
            }
            catch (Exception httpRegEx)
            {
                Log.Warning(httpRegEx, "Failed to register global HttpClient");
            }

            return builder.Build();
        }

        private static void AddConfiguration(HostApplicationBuilder builder)
        {
            // Ensure .env is loaded before adding configuration sources
            // DotNetEnv sets environment variables at process level
            try
            {
                DotNetEnv.Env.TraversePath().Load();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load .env file");
            }

            builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly());

            // Load primary appsettings.json from project directory
            try
            {
                builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load appsettings.json");
            }

            // CRITICAL: Also load config/development/appsettings.json which has the full configuration
            try
            {
                var devConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "development", "appsettings.json");
                if (File.Exists(devConfigPath))
                {
                    builder.Configuration.AddJsonFile(devConfigPath, optional: false, reloadOnChange: true);
                    Log.Debug("[CONFIG] Loaded development config from: {DevConfigPath}", devConfigPath);
                }
                else
                {
                    Log.Warning("Development config not found at: {DevConfigPath}", devConfigPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load config/development/appsettings.json");
            }

            builder.Configuration.AddEnvironmentVariables();

            // CRITICAL: Expand environment variable placeholders from appsettings.json
            // appsettings.json uses ${VAR_NAME} syntax which .NET doesn't expand automatically
            // Read environment variables and override config values that have ${...} placeholders
            try
            {
                var xaiApiKeyEnv = Environment.GetEnvironmentVariable("XAI_API_KEY");
                Log.Debug("[ENV VAR DEBUG] XAI_API_KEY present={Present}, length={Length}", xaiApiKeyEnv != null, xaiApiKeyEnv?.Length ?? 0);

                var openAiKeyEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                var syncfusionKeyEnv = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                var qboClientIdEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
                var qboClientSecretEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");

                var overrides = new Dictionary<string, string?>();

                if (!string.IsNullOrWhiteSpace(xaiApiKeyEnv))
                {
                    overrides["XAI:ApiKey"] = xaiApiKeyEnv;
                    Log.Debug("[CONFIG] Overriding XAI:ApiKey from environment variable (length: {Length})", xaiApiKeyEnv.Length);
                }
                else
                {
                    Log.Warning("XAI_API_KEY environment variable is NULL or empty!");
                }

                if (!string.IsNullOrWhiteSpace(openAiKeyEnv))
                {
                    overrides["OpenAI:ApiKey"] = openAiKeyEnv;
                }

                if (!string.IsNullOrWhiteSpace(syncfusionKeyEnv))
                {
                    overrides["Syncfusion:LicenseKey"] = syncfusionKeyEnv;
                }

                if (!string.IsNullOrWhiteSpace(qboClientIdEnv))
                {
                    overrides["QuickBooks:ClientId"] = qboClientIdEnv;
                }

                if (!string.IsNullOrWhiteSpace(qboClientSecretEnv))
                {
                    overrides["QuickBooks:ClientSecret"] = qboClientSecretEnv;
                }

                if (overrides.Count > 0)
                {
                    builder.Configuration.AddInMemoryCollection(overrides);
                    Log.Debug("[CONFIG] Added {Count} configuration overrides via AddInMemoryCollection", overrides.Count);

                    // Verify the override was applied
                    var verifyXai = builder.Configuration["XAI:ApiKey"];
                    Log.Debug("[CONFIG VERIFY] After AddInMemoryCollection: XAI:ApiKey present={Present}, length={Length}", verifyXai != null, verifyXai?.Length ?? 0);
                }
                else
                {
                    Log.Debug("No configuration overrides to add");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARNING] Error expanding environment variable placeholders: {ex.Message}");
            }

            try
            {
                var existingConn = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(existingConn))
                {
                    var defaultConn = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
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
                var logsPath = Path.Combine(projectRoot, "logs");

                // Always use root logs folder for centralized logging
                Log.Debug("Creating logs directory at: {LogsPath}", logsPath);
                Directory.CreateDirectory(logsPath);

                // Template used by Serilog's rolling file sink (daily rolling uses a date suffix)
                var logFileTemplate = Path.Combine(logsPath, "app-.log");
                Log.Debug("Log file pattern: {LogFileTemplate}", logFileTemplate);

                // Resolve the current daily log file that Serilog will write to for today's date
                // Serilog's daily rolling file uses the yyyyMMdd date format (e.g., app-20251215.log)
                var logFileCurrent = Path.Combine(logsPath, $"app-{DateTime.Now:yyyyMMdd}.log");
                Log.Debug("Current daily log file: {LogFileCurrent}", logFileCurrent);

                // Check for SQL logging override environment variable
                var enableSqlLogging = Environment.GetEnvironmentVariable("WILEYWIDGET_LOG_SQL");
                var sqlLogLevel = string.Equals(enableSqlLogging, "true", StringComparison.OrdinalIgnoreCase)
                    ? Serilog.Events.LogEventLevel.Information
                    : Serilog.Events.LogEventLevel.Warning;

                Log.Debug("SQL logging level: {SqlLogLevel} (WILEYWIDGET_LOG_SQL={EnableSqlLogging})", sqlLogLevel, enableSqlLogging ?? "not set");

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File(logFileTemplate, formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30, fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true, shared: true)
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", sqlLogLevel)
                    .CreateLogger();

                Log.Debug("Logging configured successfully");
                // Log both the resolved current file and the template used by the rolling sink so it's clear
                Log.Debug("Logging system initialized - writing to {LogPath} (pattern: {LogPattern})", logFileCurrent, logFileTemplate);
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
                    Log.Debug("Using InMemory database for UI tests (WILEYWIDGET_UI_TESTS=true)");
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
                    sql.CommandTimeout(builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 30));
                    sql.EnableRetryOnFailure(
                        maxRetryCount: builder.Configuration.GetValue("Database:MaxRetryCount", 3),
                        maxRetryDelay: TimeSpan.FromSeconds(builder.Configuration.GetValue("Database:MaxRetryDelaySeconds", 10)),
                        errorNumbersToAdd: null);
                });

                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging(builder.Configuration.GetValue("Database:EnableSensitiveDataLogging", false));

                options.UseLoggerFactory(new SerilogLoggerFactory(Log.Logger));

                Log.Debug("Using SQL Server database: {Database}", connectionString.Split(';').FirstOrDefault(s => s.Contains("Database", StringComparison.OrdinalIgnoreCase)) ?? "WileyWidgetDev");
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
                    Log.Warning("First-chance DI exception: {Message}", ex.Message);
                }
            };
        }

        private static void AddDependencyInjection(HostApplicationBuilder builder)
        {
            var diServices = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();

            // CRITICAL: Skip IConfiguration descriptor - use the host builder's configuration instead
            // The DependencyInjection.CreateServiceCollection() adds a default IConfiguration for test scenarios
            // but we want to use the real configuration from the host builder which includes .env and appsettings.json
            foreach (var descriptor in diServices)
            {
                if (descriptor.ServiceType == typeof(IConfiguration))
                {
                    Console.WriteLine("[DI] Skipping IConfiguration from CreateServiceCollection - using host builder's configuration");
                    continue; // Skip - use host builder's configuration
                }

                builder.Services.Add(descriptor);
            }
        }

        private static void ConfigureUiServices(HostApplicationBuilder builder)
        {
            // UI configuration is now handled via UIConfiguration.FromConfiguration in DependencyInjection.cs
            // No additional UI services needed here in Phase 1
        }

        /// <summary>
        /// Runs a startup health check against the database to validate connectivity before the main UI is shown.
        /// </summary>
        /// <remarks>
        /// This method is <c>internal</c> to allow automated tests to invoke the startup health check logic
        /// without launching the full application. It should not be called directly by production code
        /// outside of <c>Program.cs</c>.
        /// </remarks>
        internal static async Task RunStartupHealthCheckAsync(IServiceProvider services)
        {
            Log.Debug("[DIAGNOSTIC] Entered RunStartupHealthCheckAsync");
            try
            {
                // Create a scope for scoped services (DbContext)
                Log.Debug("[DIAGNOSTIC] Creating scope for health check");
                using var scope = services.CreateScope();
                var scopedServices = scope.ServiceProvider;

                Log.Debug("[DIAGNOSTIC] Getting AppDbContext from DI");
                var dbContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scopedServices);

                _timelineService?.RecordOperation("Test database connectivity", "Database Health Check");
                Log.Debug("[DIAGNOSTIC] Testing database connectivity with CanConnectAsync (10s timeout)");
                var connectTask = dbContext.Database.CanConnectAsync();
                var connectTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var connectCompletedTask = await Task.WhenAny(connectTask, connectTimeoutTask).ConfigureAwait(false);

                if (connectCompletedTask == connectTask)
                {
                    await connectTask.ConfigureAwait(false); // Ensure the task completed successfully
                    Log.Debug("Startup health check passed: Database connection successful");
                    Log.Debug("Database CanConnectAsync succeeded");
                }
                else
                {
                    Log.Warning("Database connectivity test timed out after 10 seconds");
                    Log.Warning("Database CanConnectAsync timed out");
                    throw new TimeoutException("Database connectivity test timed out after 10 seconds");
                }

                // Get data statistics for diagnostic purposes — run on threadpool to avoid sync-over-async deadlock
                Log.Debug("[DIAGNOSTIC] Starting data statistics check");
                try
                {
                    using (var diagnosticScope = services.CreateScope())
                    {
                        var diagnosticScopedServices = diagnosticScope.ServiceProvider;
                        Log.Debug("[DIAGNOSTIC] Getting IDashboardService from DI");
                        var dashboardService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Abstractions.IDashboardService>(diagnosticScopedServices);
                        if (dashboardService != null)
                        {
                            try
                            {
                                _timelineService?.RecordOperation("Query data statistics", "Database Health Check");
                                // Use Task.WhenAny for proper async timeout pattern instead of blocking .Wait()
                                Log.Debug("[DIAGNOSTIC] Calling GetDataStatisticsAsync with 30s timeout");
                                var statsTask = dashboardService.GetDataStatisticsAsync();
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                                var completedTask = await Task.WhenAny(statsTask, timeoutTask).ConfigureAwait(false);

                                if (completedTask == statsTask)
                                {
                                    Log.Debug("[DIAGNOSTIC] GetDataStatisticsAsync completed, awaiting result");
                                    var stats = await statsTask.ConfigureAwait(false);
                                    Log.Debug("Diagnostic: Database contains {RecordCount} budget entries (Oldest: {Oldest}, Newest: {Newest})",
                                        stats.TotalRecords,
                                        stats.OldestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                                        stats.NewestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A");
                                    Log.Debug("Stats: {TotalRecords} records", stats.TotalRecords);

                                    if (stats.TotalRecords == 0)
                                    {
                                        Log.Warning("Diagnostic: Database has no budget entries. Dashboard will show empty data. Consider running data seeding scripts.");
                                    }
                                }
                                else
                                {
                                    Log.Warning("Diagnostic: GetDataStatisticsAsync timed out after {TimeoutSeconds}s", 30);
                                    Log.Warning("GetDataStatisticsAsync timed out");
                                }
                            }
                            catch (Exception innerDiagEx)
                            {
                                Log.Warning(innerDiagEx, "Diagnostic: Failed to retrieve data statistics (threadpool execution)");
                                Log.Warning(innerDiagEx, "GetDataStatisticsAsync exception");
                            }
                        }
                        else
                        {
                            Log.Warning("Diagnostic: IDashboardService not available for data statistics check");
                            Log.Debug("IDashboardService not available");
                        }
                    }
                    Log.Debug("[DIAGNOSTIC] Data statistics check completed");
                }
                catch (Exception diagEx)
                {
                    Log.Warning(diagEx, "Diagnostic: Failed to retrieve data statistics");
                    Log.Warning(diagEx, "Data statistics outer exception");
                }

                Log.Debug("Exiting RunStartupHealthCheckAsync successfully");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Startup health check failed: Database connection issue");
                Log.Debug("[DIAGNOSTIC] RunStartupHealthCheckAsync caught exception: {Message}", ex.Message);
                // Don't throw here, let the app start and log the issue
            }

            Log.Debug("RunStartupHealthCheckAsync method exit");
        }

        private static bool IsVerifyStartup(string[] args)
        {
            return args != null && Array.Exists(args, a => string.Equals(a, "--verify-startup", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task RunVerifyStartup(IHost host)
        {
            // Prevent indefinite startup hang by timing out the StartAsync call
            using var startupCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await host.StartAsync(startupCts.Token).ConfigureAwait(false);
                // Immediately stop after successful start for verification mode
                await host.StopAsync().ConfigureAwait(false);
                Log.CloseAndFlush();
            }
            catch (OperationCanceledException oce)
            {
                Log.Fatal(oce, "Verify-startup timed out after 30 seconds");
                Log.CloseAndFlush();
                throw new InvalidOperationException("Verify-startup timed out", oce);
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
            _timelineService?.RecordOperation("Wire Application.ThreadException handler", "Error Handlers");
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

            _timelineService?.RecordOperation("Wire AppDomain.UnhandledException handler", "Error Handlers");
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
            try
            {
                if (autoCloseMs <= 0) return;
                var timer = new System.Timers.Timer(autoCloseMs) { AutoReset = false };
                timer.Elapsed += (sender, _) =>
                {
                    try
                    {
                        if (mainForm != null && !mainForm.IsDisposed)
                        {
                            mainForm.BeginInvoke(new Action(() =>
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
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Auto-close timer failed: {ex}");
                    }
                    finally
                    {
                        (sender as System.Timers.Timer)?.Dispose();
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to schedule auto-close: {ex}");
            }
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
                var logPath = Path.Combine(projectRoot, "logs");
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

        #region Critical Service Validation

        /// <summary>
        /// Validates all critical DI registrations using the dedicated validation service.
        /// This offloads validation logic to a testable, reusable service.
        /// Logs comprehensive details about each validation category and service registration.
        /// </summary>
        private static void ValidateCriticalServices(IServiceProvider services)
        {
            var startTime = DateTime.Now;
            try
            {
                Log.Debug("Starting DI validation at {Timestamp}", startTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

                // Use the WinForms-specific validator which provides categorized validation
                var validationService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<IWinFormsDiValidator>(services);

                // Note: The validator itself logs detailed category-by-category progress
                // including the formatted banner output, so we don't duplicate logging here
                var result = validationService.ValidateAll(services);

                var endTime = DateTime.Now;
                var totalDuration = endTime - startTime;

                if (!result.IsValid)
                {
                    Log.Fatal("╔════════════════════════════════════════════════════════════════╗");
                    Log.Fatal("║   ✗ DI VALIDATION FAILED - STARTUP CANNOT PROCEED             ║");
                    Log.Fatal("╠════════════════════════════════════════════════════════════════╣");
                    Log.Fatal("║ Total Errors:   {Count,4}                                          ║", result.Errors.Count);
                    Log.Fatal("║ Total Warnings: {Count,4}                                          ║", result.Warnings.Count);
                    Log.Fatal("║ Duration:       {Duration,4:F0}ms                                    ║", totalDuration.TotalMilliseconds);
                    Log.Fatal("╚════════════════════════════════════════════════════════════════╝");

                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╔════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║   ✗ DI VALIDATION FAILED - STARTUP CANNOT PROCEED             ║");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╠════════════════════════════════════════════════════════════════╣");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ║ Total Errors:   {result.Errors.Count,4}                                          ║");
                    Console.WriteLine($"[{endTime:HH:mm:ss.fff}] ╚════════════════════════════════════════════════════════════════╝");

                    foreach (var error in result.Errors)
                    {
                        Log.Fatal("  ✗ {Error}", error);
                        Console.WriteLine($"[{endTime:HH:mm:ss.fff}]   ✗ {error}");
                    }

                    throw new InvalidOperationException(
                        $"DI Validation failed with {result.Errors.Count} errors:{Environment.NewLine}" +
                        string.Join(Environment.NewLine, result.Errors));
                }

                // SUCCESS PATH - concise summary
                Log.Information("DI validation successful: {ServicesValidated} services validated, {Warnings} warnings, validation time: {ValidationMs}ms, total startup time: {TotalMs}ms",
                    result.SuccessMessages.Count, result.Warnings.Count, result.ValidationDuration.TotalMilliseconds, totalDuration.TotalMilliseconds);

                // Log any warnings if present
                if (result.Warnings.Count > 0)
                {
                    Log.Warning("DI Validation Warnings ({Count}):", result.Warnings.Count);
                    foreach (var warning in result.Warnings)
                    {
                        Log.Warning("  ⚠ {Warning}", warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "╔════════════════════════════════════════════════════════════════╗");
                Log.Fatal("║   ✗ CRITICAL FAILURE DURING DI VALIDATION                      ║");
                Log.Fatal("╠════════════════════════════════════════════════════════════════╣");
                Log.Fatal("║ Exception Type: {Type,-44} ║", ex.GetType().Name);
                Log.Fatal("║ Exception Msg:  {Message,-44} ║", ex.Message.Length > 44 ? ex.Message.Substring(0, 41) + "..." : ex.Message);
                Log.Fatal("╚════════════════════════════════════════════════════════════════╝");

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✗ CRITICAL: DI validation failed: {ex.Message}");
                throw;
            }
        }

        private static void ValidateSecrets(IServiceProvider services)
        {
            var config = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(services);

            // Check database connection string
            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                Log.Warning("No database connection string found in configuration. Ensure secrets/my.secrets or .env contains a valid 'ConnectionStrings:DefaultConnection'.");
            }
            else
            {
                Log.Debug("Database connection string configured (length: {Length})", connectionString.Length);
            }

            // Check Syncfusion license key
            var syncfusionKey = config["Syncfusion:LicenseKey"];
            if (string.IsNullOrEmpty(syncfusionKey))
            {
                Log.Warning("No Syncfusion license key found. UI controls may display evaluation nag screens or fail to initialize properly.");
            }
            else
            {
                Log.Debug("Syncfusion license key configured");
            }

            // Check xAI API key
            var xaiKey = config["XAI:ApiKey"];
            if (string.IsNullOrEmpty(xaiKey))
            {
                Log.Warning("No xAI API key found. AI recommendation services will use stub implementations.");
            }
            else
            {
                Log.Debug("xAI API key configured (length: {Length})", xaiKey.Length);
            }
        }
        #endregion
    }
}
